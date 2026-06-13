# Gloam — Installer & Auto-Update (Design)

Extension of the base Gloam app. Adds a Windows installer with automatic
updates, published by GitHub Actions on a version tag.

## Summary

Use **Velopack** (the Squirrel successor) to provide, in one tool: a per-user
installer, automatic updates from GitHub Releases, .NET runtime installation
when missing, and small delta updates. A GitHub Actions workflow builds and
publishes a Release whenever a `v*` tag is pushed. The running app checks for
updates on launch, downloads them silently, and offers a tray **Restart to
update** action — never a forced restart.

## Tooling

- `Velopack` NuGet package (app-side update API).
- `vpk` .NET global tool (CI-side packaging and GitHub upload).

## App changes

- **`Program.cs`** — `VelopackApp.Build().Run();` becomes the **first statement**
  in `Main`, before the single-instance mutex and `Application.Run`. Velopack's
  install/update hooks run here and may exit the process; nothing else may run
  before it.
- **`Updater`** (new class) — isolates all Velopack use:
  - `Updater(Action<string> onUpdateReady)` — the callback is invoked (on the UI
    thread) with the new version string once an update is downloaded.
  - `Task CheckAsync()` — no-op unless `UpdateManager.IsInstalled`; otherwise
    checks the GitHub source, downloads any newer release, stores it, and fires
    the callback. All exceptions (offline, etc.) are swallowed — updates are
    best-effort and never disrupt the app.
  - `void ApplyAndRestart()` — applies the stored update and restarts.
  - Source: `new GithubSource("https://github.com/KiraFR/Gloam", null, false)`.
- **`TrayApp`** — owns a hidden `Restart to update` menu item. In the
  constructor it creates the `Updater` (callback reveals the item, sets its text
  to `Restart to update <version>`, and shows a tray balloon) and starts
  `CheckAsync()` fire-and-forget. The item's click handler calls
  `ApplyAndRestart()`.

The update path is fully guarded by `IsInstalled`, so `dotnet run` and the unit
tests are unaffected (the check is a no-op when not installed via Velopack).

## Build & publish (CI)

1. Framework-dependent win-x64 publish:
   `dotnet publish src/Gloam/Gloam.csproj -c Release -r win-x64 --self-contained false -p:Version=<version> -o publish`
2. Pack with Velopack:
   `vpk pack -u Gloam -v <version> -p publish -e Gloam.exe -f net8.0-x64-desktop`
   - `-f net8.0-x64-desktop` makes the installer install the .NET 8 Desktop
     Runtime if it is missing.
3. Upload to a GitHub Release:
   `vpk upload github --repoUrl https://github.com/KiraFR/Gloam --publish --releaseName "Gloam <version>" --tag v<version> --token <GITHUB_TOKEN>`
   - Publishes `Gloam-win-Setup.exe`, the `.nupkg`, and the `RELEASES`/manifest
     assets that the in-app updater reads.

## Workflow: `.github/workflows/release.yml`

- **Trigger:** push of a tag matching `v*`.
- **Permissions:** `contents: write` (to create the Release).
- **Runner:** `windows-latest`.
- **Steps:** checkout → `setup-dotnet` 8 → derive `version` from the tag (strip
  the leading `v`) → `dotnet publish` → `dotnet tool install -g vpk` →
  `vpk pack …` → `vpk upload github …` using the built-in `GITHUB_TOKEN`.

## Versioning

The version is derived entirely from the tag: `v1.2.3` → `1.2.3`, passed to
`dotnet publish` (`-p:Version=`) and to `vpk pack` (`-v`).

## Install / runtime

- **Per-user** install under `%LocalAppData%`, **no admin / no UAC**.
- Velopack installs the **.NET 8 Desktop Runtime** if missing (via the
  `net8.0-x64-desktop` framework dependency).

## Update UX

On launch, an installed app checks GitHub; if a newer release exists it is
downloaded silently. When ready, a tray balloon appears and the **Restart to
update <version>** menu item becomes visible. Clicking it applies the update and
restarts. There is no forced restart; if the user never clicks it, the update
applies the next time Velopack runs. Network failures are ignored silently.

## Out of scope (YAGNI)

- Code signing (the installer is unsigned; SmartScreen will warn — expected).
- Beta/stable channels.
- Forced or background-silent auto-apply.

## Testing

- The existing 19 unit tests are unaffected (update code is guarded by
  `IsInstalled`).
- `Updater` and the workflow are thin glue, verified by an actual tagged release
  rather than unit tests.
- End-to-end validation: push `v0.1.0`, confirm the Release and `Setup.exe`
  appear, install it, then push `v0.1.1` and confirm the running app offers the
  update.

## Edge cases

- **Not installed via Velopack** (dev / `dotnet run`): `IsInstalled` is false →
  the whole update path is skipped.
- **Offline or GitHub error**: caught inside `Updater`; no crash, no UI noise.
- **`VelopackApp.Run` ordering**: it must be the first line of `Main`; it may
  terminate the process during install/update hook invocations.
