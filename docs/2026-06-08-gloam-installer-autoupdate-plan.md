# Gloam Installer & Auto-Update — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship Gloam as a self-updating per-user Windows install, published automatically to GitHub Releases on a `v*` tag.

**Architecture:** Use Velopack for the installer, GitHub-Releases auto-update, and .NET runtime bootstrap. The app calls `VelopackApp.Build().Run()` first thing in `Main`, and a small `Updater` class (guarded by `IsInstalled`) checks/downloads updates and surfaces a tray "Restart to update" action. A `release.yml` workflow publishes via `vpk`.

**Tech Stack:** .NET 8 WinForms, `Velopack` NuGet + `vpk` global tool, GitHub Actions.

---

## File Structure

```
src/Gloam/
  Gloam.csproj        MODIFY  add <PackageReference Include="Velopack" />
  Program.cs          MODIFY  VelopackApp.Build().Run() as first statement
  Updater.cs          CREATE  Velopack wrapper (check/download/apply)
  TrayApp.cs          MODIFY  hidden "Restart to update" item + kick the check
.github/workflows/
  release.yml         CREATE  build + vpk pack + upload on tag v*
README.md             MODIFY  Install & updates section
```

`Updater` isolates all Velopack use behind a 3-method surface so `TrayApp` only wires UI. No unit tests are added — the update code is guarded by `IsInstalled` (a no-op in dev/test) and is validated by an actual tagged release. The existing 19 tests must keep passing.

Work on branch `feat/release-autoupdate` in `C:\Users\jimmy\Documents\GitHub\Gloam`. Use PowerShell. If git complains about identity, use `git -c user.name="jimmy" -c user.email="jimmydelannoy@gmail.com" commit ...`.

---

## Task 1: Add Velopack and the VelopackApp hook

**Files:** Modify `src/Gloam/Gloam.csproj`, `src/Gloam/Program.cs`

- [ ] **Step 1: Add the Velopack package**

Run: `dotnet add src/Gloam/Gloam.csproj package Velopack`
Expected: adds a `<PackageReference Include="Velopack" Version="…" />` (latest stable) and restores.

- [ ] **Step 2: Put VelopackApp.Run first in Main**

Replace `src/Gloam/Program.cs` with:
```csharp
using System.Windows.Forms;
using Velopack;

namespace Gloam;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Must run before anything else: handles Velopack install/update hooks
        // and may exit the process during those.
        VelopackApp.Build().Run();

        using var mutex = new Mutex(initiallyOwned: true, "Gloam.SingleInstance", out bool isNew);
        if (!isNew) return; // another instance already owns the tray icon

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}
```

- [ ] **Step 3: Build and test**

Run: `dotnet build` then `dotnet test`
Expected: build SUCCEEDS, all 19 tests PASS.

- [ ] **Step 4: Commit**
```powershell
git add src/Gloam/Gloam.csproj src/Gloam/Program.cs
git commit -m "feat: add Velopack and run its app hook first in Main"
```

---

## Task 2: Updater (Velopack wrapper)

**Files:** Create `src/Gloam/Updater.cs`

No automated test (it talks to GitHub and only acts when installed via Velopack). Verified by an actual tagged release.

- [ ] **Step 1: Implement Updater**

Create `src/Gloam/Updater.cs`:
```csharp
using Velopack;
using Velopack.Sources;

namespace Gloam;

/// <summary>
/// Wraps Velopack auto-update. Every operation is a no-op unless the app was
/// installed via Velopack, and all failures (offline, GitHub errors) are
/// swallowed — updates are best-effort and never disrupt the app.
/// </summary>
public sealed class Updater
{
    private readonly UpdateManager _manager;
    private readonly Action<string> _onUpdateReady;
    private UpdateInfo? _pending;

    public Updater(Action<string> onUpdateReady)
    {
        _onUpdateReady = onUpdateReady;
        _manager = new UpdateManager(
            new GithubSource("https://github.com/KiraFR/Gloam", null, false));
    }

    /// <summary>Checks for and downloads a newer release; fires the ready callback.</summary>
    public async Task CheckAsync()
    {
        if (!_manager.IsInstalled) return;

        try
        {
            var info = await _manager.CheckForUpdatesAsync();
            if (info == null) return;

            await _manager.DownloadUpdatesAsync(info);
            _pending = info;
            _onUpdateReady(info.TargetFullRelease.Version.ToString());
        }
        catch
        {
            // best-effort: ignore network/update errors
        }
    }

    /// <summary>Applies the downloaded update and restarts the app.</summary>
    public void ApplyAndRestart()
    {
        if (_pending != null)
            _manager.ApplyUpdatesAndRestart(_pending);
    }
}
```

Note: Velopack's API names can vary slightly between versions. If the build fails on `CheckForUpdatesAsync` / `DownloadUpdatesAsync` / `ApplyUpdatesAndRestart` / `info.TargetFullRelease.Version`, adapt minimally to the installed package — the required behavior is: guard on `IsInstalled`, check → download → store, and apply-and-restart. Do not change the public surface (`CheckAsync`, `ApplyAndRestart`, the constructor).

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: build SUCCEEDS.

- [ ] **Step 3: Commit**
```powershell
git add src/Gloam/Updater.cs
git commit -m "feat: add Updater wrapping Velopack GitHub auto-update"
```

---

## Task 3: TrayApp update integration

**Files:** Modify `src/Gloam/TrayApp.cs`

- [ ] **Step 1: Add the update field and menu item**

In `src/Gloam/TrayApp.cs`, add two fields next to the other `ToolStripMenuItem` fields (after `_startupItem`):
```csharp
    private readonly ToolStripMenuItem _updateItem;
    private readonly Updater _updater;
```

- [ ] **Step 2: Wire the item, the updater, and a one-shot check in the constructor**

In the constructor, add the update item creation just before `var menu = new ContextMenuStrip();`:
```csharp
        _updateItem = new ToolStripMenuItem("Restart to update", null, (_, _) => _updater.ApplyAndRestart())
        {
            Visible = false
        };
```

Then make `_updateItem` the first entry in the menu by changing the `menu.Items.AddRange(...)` array so it begins with `_updateItem`:
```csharp
        menu.Items.AddRange(new ToolStripItem[]
        {
            _updateItem,
            _autoItem, _lightItem, _darkItem,
            new ToolStripSeparator(),
            _startupItem,
            new ToolStripSeparator(),
            settingsItem,
            new ToolStripSeparator(),
            quitItem
        });
```

Then, after `ApplyForNow(); // catch-up on launch` at the end of the constructor, add:
```csharp
        _updater = new Updater(version =>
        {
            _updateItem.Text = $"Restart to update {version}";
            _updateItem.Visible = true;
            _icon.ShowBalloonTip(5000, "Gloam",
                $"Update {version} downloaded — open the tray menu to restart.", ToolTipIcon.Info);
        });

        // Check for updates a few seconds after launch, once the message loop is
        // running so the callback marshals back to the UI thread.
        var updateCheck = new System.Windows.Forms.Timer { Interval = 3000 };
        updateCheck.Tick += (_, _) =>
        {
            updateCheck.Stop();
            updateCheck.Dispose();
            _ = _updater.CheckAsync();
        };
        updateCheck.Start();
```

- [ ] **Step 3: Build and test**

Run: `dotnet build` then `dotnet test`
Expected: build SUCCEEDS, all 19 tests PASS.

- [ ] **Step 4: Commit**
```powershell
git add src/Gloam/TrayApp.cs
git commit -m "feat: surface a tray Restart-to-update action via Updater"
```

---

## Task 4: Release workflow

**Files:** Create `.github/workflows/release.yml`

- [ ] **Step 1: Create the workflow**

Create `.github/workflows/release.yml`:
```yaml
name: Release

on:
  push:
    tags: [ 'v*' ]

permissions:
  contents: write

jobs:
  release:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4

      - name: Setup .NET 8
        uses: actions/setup-dotnet@v4
        with:
          dotnet-version: 8.0.x

      - name: Derive version from tag
        id: ver
        shell: pwsh
        run: |
          $v = "${{ github.ref_name }}".TrimStart('v')
          "version=$v" >> $env:GITHUB_OUTPUT

      - name: Publish (framework-dependent, win-x64)
        run: dotnet publish src/Gloam/Gloam.csproj -c Release -r win-x64 --self-contained false -p:Version=${{ steps.ver.outputs.version }} -o publish

      - name: Install Velopack CLI
        run: dotnet tool install -g vpk

      - name: Pack
        run: vpk pack -u Gloam -v ${{ steps.ver.outputs.version }} -p publish -e Gloam.exe -f net8.0-x64-desktop

      - name: Upload to GitHub Release
        run: vpk upload github --repoUrl https://github.com/KiraFR/Gloam --publish --releaseName "Gloam ${{ steps.ver.outputs.version }}" --tag ${{ github.ref_name }} --token ${{ secrets.GITHUB_TOKEN }}
```

- [ ] **Step 2: Validate the YAML parses**

Run:
```powershell
pwsh -Command "Get-Content .github/workflows/release.yml -Raw | Out-Null; 'release.yml present'"
```
Expected: prints `release.yml present` (the workflow is static YAML; its real validation is the first tag push).

- [ ] **Step 3: Commit**
```powershell
git add .github/workflows/release.yml
git commit -m "ci: add release workflow (Velopack pack + GitHub upload on v* tag)"
```

---

## Task 5: README install & updates section

**Files:** Modify `README.md`

- [ ] **Step 1: Add the section**

In `README.md`, add this section immediately after the top description paragraph (before `## Build & run`):
```markdown
## Install

Download the latest **`Gloam-win-Setup.exe`** from the
[Releases page](https://github.com/KiraFR/Gloam/releases) and run it. It installs
per-user (no admin) and pulls in the .NET 8 Desktop Runtime if you don't have it.

Gloam updates itself: when a new release is published it downloads in the
background and the tray menu shows **Restart to update** — click it whenever you
like.

```

- [ ] **Step 2: Commit**
```powershell
git add README.md
git commit -m "docs: document install and auto-update in README"
```

---

## Task 6: Final verification

- [ ] **Step 1: Release build + full test**

Run: `dotnet build -c Release` then `dotnet test -c Release`
Expected: build SUCCEEDS, all 19 tests PASS.

- [ ] **Step 2: Confirm dev run is unaffected by Velopack**

The update path is guarded by `UpdateManager.IsInstalled`, which is false for a
plain `dotnet`/exe launch, so a normal run must behave exactly as before (no
update check side effects). Do not launch here if it would disturb the user's
session — this is a code-review confirmation, not a runtime step.

- [ ] **Step 3: Push the branch**
```powershell
git push -u origin feat/release-autoupdate
```

- [ ] **Step 4: Release is cut by pushing a tag (manual, after merge)**

After this branch is merged to `main`, publishing a release is:
```powershell
git tag v0.1.0
git push origin v0.1.0
```
The `release.yml` workflow then builds and publishes `Gloam-win-Setup.exe` to the
GitHub Release for `v0.1.0`.

---

## Self-Review (completed)

- **Spec coverage:** Velopack package + `VelopackApp.Build().Run()` first (Task 1); `Updater` with check/download/apply guarded by `IsInstalled`, GitHub source, swallowed errors (Task 2); tray hidden "Restart to update" item + delayed UI-thread check + balloon (Task 3); `release.yml` on `v*` with publish → `vpk pack -f net8.0-x64-desktop` → `vpk upload github` and `contents: write` (Task 4); README install/update docs (Task 5); version-from-tag throughout. All spec sections map to a task.
- **Placeholders:** none — every code/config step is complete.
- **Type consistency:** `Updater(Action<string>)`, `Updater.CheckAsync()`, `Updater.ApplyAndRestart()` are defined in Task 2 and used identically in Task 3; `_updateItem`/`_updater` field names are consistent within Task 3.
- **Note carried into execution:** Velopack method names may differ by version (flagged in Task 2); `vpk` flag names may differ by version (validated only on a real tag push, per Task 4/6).
