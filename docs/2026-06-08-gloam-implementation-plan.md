# Gloam Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build Gloam, a flash-free Windows system-tray app that switches the light/dark theme at fixed, configurable times.

**Architecture:** A single WinForms `ApplicationContext` (TrayApp) drives an internal 30 s timer. Pure, testable units (`Schedule`, `Config`) decide and persist; thin Windows-interop units (`ThemeSwitcher`, `Startup`) touch the registry and broadcast the native theme-change message. No wallpaper change, no scheduled tasks, no admin.

**Tech Stack:** .NET 8 (`net8.0-windows`), WinForms, `Microsoft.Win32.Registry`, xUnit. Git/GitHub.

---

## File Structure

```
C:\Users\jimmy\Documents\GitHub\Gloam\
  Gloam.sln
  .gitignore
  README.md
  LICENSE                         (already present, MIT)
  docs/                           (design + this plan)
  src/Gloam/
    Gloam.csproj                  WinExe, net8.0-windows, UseWindowsForms
    Program.cs                    Main: single-instance mutex + Application.Run(TrayApp)
    ThemeMode.cs                  enum { Light, Dark }
    Schedule.cs                   pure: ModeFor(now, darkTime, lightTime)
    Config.cs                     model + JSON Load/Save, DefaultPath
    ThemeSwitcher.cs              Apply(mode), GetCurrent() — registry + broadcast
    Startup.cs                    Enable/Disable/IsEnabled — HKCU\...\Run
    SettingsForm.cs               two time pickers + "start with Windows"
    TrayApp.cs                    NotifyIcon, menu, timer, wiring
  tests/Gloam.Tests/
    Gloam.Tests.csproj            net8.0-windows, xUnit, references Gloam
    ScheduleTests.cs
    ConfigTests.cs
```

`Schedule` and `Config` carry the logic and are unit-tested. `ThemeSwitcher` and `Startup` are kept thin and verified manually (they mutate the real user registry). `TrayApp`/`SettingsForm`/`Program` are UI wiring, verified by running the app.

---

## Task 0: Scaffold solution and projects

**Files:**
- Create: `Gloam.sln`, `src/Gloam/Gloam.csproj`, `tests/Gloam.Tests/Gloam.Tests.csproj`, `.gitignore`, `README.md`

- [ ] **Step 1: Scaffold via dotnet CLI**

Run from `C:\Users\jimmy\Documents\GitHub\Gloam`:

```powershell
dotnet new sln -n Gloam
dotnet new winforms -o src/Gloam -n Gloam
dotnet new xunit -o tests/Gloam.Tests -n Gloam.Tests
dotnet sln add src/Gloam/Gloam.csproj tests/Gloam.Tests/Gloam.Tests.csproj
dotnet add tests/Gloam.Tests/Gloam.Tests.csproj reference src/Gloam/Gloam.csproj
```

- [ ] **Step 2: Remove template cruft**

Delete the generated files we replace:

```powershell
Remove-Item src/Gloam/Form1.cs, src/Gloam/Form1.Designer.cs, src/Gloam/Form1.resx, tests/Gloam.Tests/UnitTest1.cs -ErrorAction SilentlyContinue
```

- [ ] **Step 3: Set the test project TFM to net8.0-windows**

Edit `tests/Gloam.Tests/Gloam.Tests.csproj` so it can reference the WinForms exe. Replace the whole file with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.11.1" />
    <PackageReference Include="xunit" Version="2.9.2" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\..\src\Gloam\Gloam.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 4: Pin the app csproj**

Replace `src/Gloam/Gloam.csproj` with:

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <UseWindowsForms>true</UseWindowsForms>
    <RootNamespace>Gloam</RootNamespace>
    <AssemblyName>Gloam</AssemblyName>
    <ApplicationIcon></ApplicationIcon>
  </PropertyGroup>

</Project>
```

- [ ] **Step 5: Add .gitignore**

Create `.gitignore`:

```gitignore
bin/
obj/
.vs/
*.user
[Dd]ebug/
[Rr]elease/
TestResults/
```

- [ ] **Step 6: Add README**

Create `README.md`:

```markdown
# Gloam

A tiny Windows system-tray app that switches the light/dark theme at fixed,
configurable times — instantly and **without the screen flash** caused by
full-theme switchers.

It only flips the two Windows theme registry values and broadcasts the standard
`WM_SETTINGCHANGE` message (exactly what Windows does in Settings). No wallpaper
change, no admin rights.

## Build & run

```powershell
dotnet build
dotnet test
dotnet run --project src/Gloam
```

## Usage

Right-click the tray icon: **Auto** (switch on schedule), **Light**, **Dark**,
**Settings…** (set the go-dark / go-light times and autostart), **Quit**.

Config lives at `%AppData%\Gloam\config.json`.
```

- [ ] **Step 7: Build to verify scaffolding compiles (no source yet beyond template Program.cs)**

Run: `dotnet build`
Expected: build SUCCEEDS. (We replace `Program.cs` in Task 6; the template `Program.cs` still references `Form1` — if the build fails because `Form1` was deleted, proceed to Step 8 which fixes `Program.cs` to a stub.)

- [ ] **Step 8: Stub Program.cs so the solution builds**

Replace `src/Gloam/Program.cs` with a temporary stub (finalized in Task 6):

```csharp
namespace Gloam;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
    }
}
```

Run: `dotnet build`
Expected: build SUCCEEDS.

- [ ] **Step 9: Commit**

```powershell
git add -A
git commit -m "chore: scaffold Gloam solution, app and test projects"
```

---

## Task 1: Schedule (pure logic, TDD)

**Files:**
- Create: `src/Gloam/ThemeMode.cs`, `src/Gloam/Schedule.cs`
- Test: `tests/Gloam.Tests/ScheduleTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Gloam.Tests/ScheduleTests.cs`:

```csharp
using Gloam;
using Xunit;

public class ScheduleTests
{
    private static TimeOnly T(int h, int m = 0) => new(h, m);

    [Fact]
    public void Midday_is_light() =>
        Assert.Equal(ThemeMode.Light, Schedule.ModeFor(T(12), T(19), T(7)));

    [Fact]
    public void Evening_is_dark() =>
        Assert.Equal(ThemeMode.Dark, Schedule.ModeFor(T(20), T(19), T(7)));

    [Fact]
    public void Early_morning_is_dark_when_window_wraps_midnight() =>
        Assert.Equal(ThemeMode.Dark, Schedule.ModeFor(T(3), T(19), T(7)));

    [Fact]
    public void Dark_start_boundary_is_inclusive() =>
        Assert.Equal(ThemeMode.Dark, Schedule.ModeFor(T(19), T(19), T(7)));

    [Fact]
    public void Light_start_boundary_is_inclusive() =>
        Assert.Equal(ThemeMode.Light, Schedule.ModeFor(T(7), T(19), T(7)));

    [Fact]
    public void Non_wrapping_window_is_dark_inside() =>
        Assert.Equal(ThemeMode.Dark, Schedule.ModeFor(T(5), T(2), T(9)));

    [Fact]
    public void Non_wrapping_window_is_light_outside() =>
        Assert.Equal(ThemeMode.Light, Schedule.ModeFor(T(10), T(2), T(9)));

    [Fact]
    public void Equal_times_degenerate_to_light() =>
        Assert.Equal(ThemeMode.Light, Schedule.ModeFor(T(12), T(8), T(8)));
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter ScheduleTests`
Expected: FAIL — `ThemeMode` / `Schedule` do not exist (compile error).

- [ ] **Step 3: Create the enum**

Create `src/Gloam/ThemeMode.cs`:

```csharp
namespace Gloam;

public enum ThemeMode
{
    Light,
    Dark
}
```

- [ ] **Step 4: Implement Schedule**

Create `src/Gloam/Schedule.cs`:

```csharp
namespace Gloam;

/// <summary>
/// Pure scheduling logic: which theme should be active at a given time,
/// given the go-dark and go-light times. The dark window runs from
/// <c>darkTime</c> (inclusive) until <c>lightTime</c> (exclusive), wrapping
/// past midnight when <c>darkTime</c> is later in the day than <c>lightTime</c>.
/// </summary>
public static class Schedule
{
    public static ThemeMode ModeFor(TimeOnly now, TimeOnly darkTime, TimeOnly lightTime)
        => InDarkWindow(now, darkTime, lightTime) ? ThemeMode.Dark : ThemeMode.Light;

    private static bool InDarkWindow(TimeOnly now, TimeOnly darkTime, TimeOnly lightTime)
    {
        if (darkTime <= lightTime)
            return now >= darkTime && now < lightTime;

        // Window wraps past midnight (e.g. dark 19:00 -> light 07:00).
        return now >= darkTime || now < lightTime;
    }
}
```

- [ ] **Step 5: Run tests to verify they pass**

Run: `dotnet test --filter ScheduleTests`
Expected: PASS (8 tests).

- [ ] **Step 6: Commit**

```powershell
git add src/Gloam/ThemeMode.cs src/Gloam/Schedule.cs tests/Gloam.Tests/ScheduleTests.cs
git commit -m "feat: add ThemeMode and pure Schedule.ModeFor logic"
```

---

## Task 2: Config (model + JSON persistence, TDD)

**Files:**
- Create: `src/Gloam/Config.cs`
- Test: `tests/Gloam.Tests/ConfigTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/Gloam.Tests/ConfigTests.cs`:

```csharp
using Gloam;
using Xunit;

public class ConfigTests
{
    [Fact]
    public void Defaults_are_dark_19_light_7_auto_and_startup_on()
    {
        var c = new Config();
        Assert.Equal(new TimeOnly(19, 0), c.DarkTime);
        Assert.Equal(new TimeOnly(7, 0), c.LightTime);
        Assert.True(c.Auto);
        Assert.True(c.RunAtStartup);
    }

    [Fact]
    public void Load_missing_file_returns_defaults()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        var c = Config.Load(path);
        Assert.Equal(new TimeOnly(19, 0), c.DarkTime);
        Assert.True(c.Auto);
    }

    [Fact]
    public void Save_then_load_roundtrips_all_fields()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        try
        {
            var c = new Config
            {
                DarkTime = new TimeOnly(21, 30),
                LightTime = new TimeOnly(6, 15),
                Auto = false,
                RunAtStartup = false
            };
            c.Save(path);

            var loaded = Config.Load(path);
            Assert.Equal(new TimeOnly(21, 30), loaded.DarkTime);
            Assert.Equal(new TimeOnly(6, 15), loaded.LightTime);
            Assert.False(loaded.Auto);
            Assert.False(loaded.RunAtStartup);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `dotnet test --filter ConfigTests`
Expected: FAIL — `Config` does not exist.

- [ ] **Step 3: Implement Config**

Create `src/Gloam/Config.cs`:

```csharp
using System.Text.Json;

namespace Gloam;

public sealed class Config
{
    public TimeOnly DarkTime { get; set; } = new(19, 0);
    public TimeOnly LightTime { get; set; } = new(7, 0);
    public bool Auto { get; set; } = true;
    public bool RunAtStartup { get; set; } = true;

    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public static string DefaultPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Gloam",
        "config.json");

    public static Config Load(string path)
    {
        if (!File.Exists(path))
            return new Config();

        try
        {
            return JsonSerializer.Deserialize<Config>(File.ReadAllText(path), Options)
                   ?? new Config();
        }
        catch
        {
            // Corrupt or unreadable config: fall back to defaults rather than crash.
            return new Config();
        }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, Options));
    }
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `dotnet test --filter ConfigTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```powershell
git add src/Gloam/Config.cs tests/Gloam.Tests/ConfigTests.cs
git commit -m "feat: add Config model with JSON load/save"
```

---

## Task 3: ThemeSwitcher (registry + native broadcast)

**Files:**
- Create: `src/Gloam/ThemeSwitcher.cs`

No automated test: this mutates `HKCU` and changes the live theme. Verified manually in Step 3.

- [ ] **Step 1: Implement ThemeSwitcher**

Create `src/Gloam/ThemeSwitcher.cs`:

```csharp
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace Gloam;

/// <summary>
/// Applies a theme the same minimal way Windows does: write the two
/// Personalize registry values, then broadcast WM_SETTINGCHANGE so running
/// apps repaint. No wallpaper change, no admin rights.
/// </summary>
public static class ThemeSwitcher
{
    private const string KeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";

    public static void Apply(ThemeMode mode)
    {
        int lightValue = mode == ThemeMode.Light ? 1 : 0;

        using (var key = Registry.CurrentUser.CreateSubKey(KeyPath))
        {
            key.SetValue("AppsUseLightTheme", lightValue, RegistryValueKind.DWord);
            key.SetValue("SystemUsesLightTheme", lightValue, RegistryValueKind.DWord);
        }

        Broadcast();
    }

    public static ThemeMode GetCurrent()
    {
        using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
        var value = key?.GetValue("AppsUseLightTheme");
        return value is int i && i == 1 ? ThemeMode.Light : ThemeMode.Dark;
    }

    private const int WM_SETTINGCHANGE = 0x001A;
    private const int SMTO_ABORTIFHUNG = 0x0002;
    private static readonly IntPtr HWND_BROADCAST = new(0xFFFF);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, int msg, IntPtr wParam, string lParam,
        int flags, int timeoutMs, out IntPtr result);

    private static void Broadcast()
    {
        SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE, IntPtr.Zero,
            "ImmersiveColorSet", SMTO_ABORTIFHUNG, 100, out _);
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: build SUCCEEDS.

- [ ] **Step 3: Manual smoke test**

Add a temporary line to `Program.cs` `Main` body: `Gloam.ThemeSwitcher.Apply(Gloam.ThemeMode.Dark);` then run `dotnet run --project src/Gloam`. Confirm Windows switches to dark instantly with no wallpaper flash. Repeat with `ThemeMode.Light`. Then **remove the temporary line**.

- [ ] **Step 4: Commit**

```powershell
git add src/Gloam/ThemeSwitcher.cs
git commit -m "feat: add ThemeSwitcher (registry write + WM_SETTINGCHANGE broadcast)"
```

---

## Task 4: Startup (autostart registration)

**Files:**
- Create: `src/Gloam/Startup.cs`

No automated test (mutates `HKCU\...\Run`). Verified manually later when the app runs.

- [ ] **Step 1: Implement Startup**

Create `src/Gloam/Startup.cs`:

```csharp
using Microsoft.Win32;

namespace Gloam;

/// <summary>
/// Manages the per-user autostart entry under
/// HKCU\Software\Microsoft\Windows\CurrentVersion\Run.
/// </summary>
public static class Startup
{
    private const string RunKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "Gloam";

    public static void Enable(string exePath)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        key.SetValue(ValueName, $"\"{exePath}\"");
    }

    public static void Disable()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
    }

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        return key?.GetValue(ValueName) != null;
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: build SUCCEEDS.

- [ ] **Step 3: Commit**

```powershell
git add src/Gloam/Startup.cs
git commit -m "feat: add Startup autostart (HKCU Run) helper"
```

---

## Task 5: SettingsForm (time pickers + startup checkbox)

**Files:**
- Create: `src/Gloam/SettingsForm.cs`

- [ ] **Step 1: Implement SettingsForm**

Create `src/Gloam/SettingsForm.cs`:

```csharp
using System.Drawing;
using System.Windows.Forms;

namespace Gloam;

public sealed class SettingsForm : Form
{
    private readonly DateTimePicker _darkPicker =
        new() { Format = DateTimePickerFormat.Time, ShowUpDown = true };
    private readonly DateTimePicker _lightPicker =
        new() { Format = DateTimePickerFormat.Time, ShowUpDown = true };
    private readonly CheckBox _startupCheck = new() { Text = "Start with Windows" };

    public TimeOnly DarkTime => TimeOnly.FromDateTime(_darkPicker.Value);
    public TimeOnly LightTime => TimeOnly.FromDateTime(_lightPicker.Value);
    public bool RunAtStartup => _startupCheck.Checked;

    public SettingsForm(Config config)
    {
        Text = "Gloam — Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(260, 170);

        var today = DateTime.Today;
        _darkPicker.Value = today + config.DarkTime.ToTimeSpan();
        _lightPicker.Value = today + config.LightTime.ToTimeSpan();
        _startupCheck.Checked = config.RunAtStartup;

        var darkLabel = new Label { Text = "Go dark at:", Left = 20, Top = 20, Width = 95 };
        _darkPicker.SetBounds(120, 18, 110, 23);

        var lightLabel = new Label { Text = "Go light at:", Left = 20, Top = 55, Width = 95 };
        _lightPicker.SetBounds(120, 53, 110, 23);

        _startupCheck.SetBounds(20, 90, 210, 23);

        var ok = new Button
        {
            Text = "OK", DialogResult = DialogResult.OK, Left = 60, Top = 125, Width = 75
        };
        var cancel = new Button
        {
            Text = "Cancel", DialogResult = DialogResult.Cancel, Left = 145, Top = 125, Width = 75
        };

        ok.Click += (_, _) =>
        {
            if (DarkTime == LightTime)
            {
                MessageBox.Show("Dark and light times must differ.", "Gloam",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None; // keep the dialog open
            }
        };

        Controls.AddRange(new Control[]
        {
            darkLabel, _darkPicker, lightLabel, _lightPicker, _startupCheck, ok, cancel
        });

        AcceptButton = ok;
        CancelButton = cancel;
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: build SUCCEEDS.

- [ ] **Step 3: Commit**

```powershell
git add src/Gloam/SettingsForm.cs
git commit -m "feat: add SettingsForm with time pickers and startup toggle"
```

---

## Task 6: TrayApp + Program (wire everything together)

**Files:**
- Create: `src/Gloam/TrayApp.cs`
- Modify: `src/Gloam/Program.cs`

- [ ] **Step 1: Implement TrayApp**

Create `src/Gloam/TrayApp.cs`:

```csharp
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Gloam;

public sealed class TrayApp : ApplicationContext
{
    private readonly NotifyIcon _icon;
    private readonly System.Windows.Forms.Timer _timer;
    private readonly ToolStripMenuItem _autoItem;
    private readonly ToolStripMenuItem _lightItem;
    private readonly ToolStripMenuItem _darkItem;
    private readonly Config _config;
    private ThemeMode _applied;
    private IntPtr _hIcon = IntPtr.Zero;

    public TrayApp()
    {
        _config = Config.Load(Config.DefaultPath);

        _autoItem = new ToolStripMenuItem("Auto", null, (_, _) => SetAuto());
        _lightItem = new ToolStripMenuItem("Light", null, (_, _) => SetManual(ThemeMode.Light));
        _darkItem = new ToolStripMenuItem("Dark", null, (_, _) => SetManual(ThemeMode.Dark));
        var settingsItem = new ToolStripMenuItem("Settings…", null, (_, _) => OpenSettings());
        var quitItem = new ToolStripMenuItem("Quit", null, (_, _) => Quit());

        var menu = new ContextMenuStrip();
        menu.Items.AddRange(new ToolStripItem[]
        {
            _autoItem, _lightItem, _darkItem,
            new ToolStripSeparator(),
            settingsItem,
            new ToolStripSeparator(),
            quitItem
        });

        _icon = new NotifyIcon { Visible = true, ContextMenuStrip = menu, Text = "Gloam" };

        _timer = new System.Windows.Forms.Timer { Interval = 30_000 };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();

        ApplyForNow(); // catch-up on launch
    }

    private void Tick()
    {
        if (!_config.Auto) return;
        var desired = Schedule.ModeFor(
            TimeOnly.FromDateTime(DateTime.Now), _config.DarkTime, _config.LightTime);
        if (desired != _applied) ApplyMode(desired);
    }

    private void ApplyForNow()
    {
        var mode = _config.Auto
            ? Schedule.ModeFor(TimeOnly.FromDateTime(DateTime.Now), _config.DarkTime, _config.LightTime)
            : ThemeSwitcher.GetCurrent();
        ApplyMode(mode);
    }

    private void ApplyMode(ThemeMode mode)
    {
        ThemeSwitcher.Apply(mode);
        _applied = mode;
        UpdateUi();
    }

    private void SetAuto()
    {
        _config.Auto = true;
        _config.Save(Config.DefaultPath);
        ApplyForNow();
    }

    private void SetManual(ThemeMode mode)
    {
        _config.Auto = false;
        _config.Save(Config.DefaultPath);
        ApplyMode(mode);
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(_config);
        if (form.ShowDialog() != DialogResult.OK) return;

        _config.DarkTime = form.DarkTime;
        _config.LightTime = form.LightTime;
        _config.RunAtStartup = form.RunAtStartup;
        _config.Save(Config.DefaultPath);

        if (_config.RunAtStartup) Startup.Enable(Application.ExecutablePath);
        else Startup.Disable();

        if (_config.Auto) ApplyForNow();
    }

    private void UpdateUi()
    {
        _autoItem.Checked = _config.Auto;
        _lightItem.Checked = !_config.Auto && _applied == ThemeMode.Light;
        _darkItem.Checked = !_config.Auto && _applied == ThemeMode.Dark;
        _icon.Text = $"Gloam — {_applied}";
        SetIcon(_applied);
    }

    private void SetIcon(ThemeMode mode)
    {
        using var bmp = new Bitmap(16, 16);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            var color = mode == ThemeMode.Light ? Color.Gold : Color.LightSlateGray;
            using var brush = new SolidBrush(color);
            g.FillEllipse(brush, 2, 2, 12, 12);
        }

        IntPtr newHandle = bmp.GetHicon();
        _icon.Icon = Icon.FromHandle(newHandle);
        if (_hIcon != IntPtr.Zero) DestroyIcon(_hIcon);
        _hIcon = newHandle;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr handle);

    private void Quit()
    {
        _timer.Stop();
        _icon.Visible = false;
        _icon.Dispose();
        if (_hIcon != IntPtr.Zero) DestroyIcon(_hIcon);
        ExitThread();
    }
}
```

- [ ] **Step 2: Finalize Program.cs**

Replace `src/Gloam/Program.cs` with:

```csharp
using System.Windows.Forms;

namespace Gloam;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, "Gloam.SingleInstance", out bool isNew);
        if (!isNew) return; // another instance already owns the tray icon

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}
```

- [ ] **Step 3: Build**

Run: `dotnet build`
Expected: build SUCCEEDS.

- [ ] **Step 4: Run and verify the full app**

Run: `dotnet run --project src/Gloam`
Verify manually:
- Tray icon appears (gold = light, slate = dark) matching the current time vs the 19:00/07:00 defaults.
- Right-click → **Light** switches instantly, no flash; **Dark** likewise; **Auto** re-applies the time-appropriate mode.
- **Settings…** opens, lets you change the two times and "Start with Windows"; OK saves. Setting equal times is rejected.
- With "Start with Windows" checked, confirm a `Gloam` value exists under `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`; unchecking removes it.
- Launching a second instance does nothing (no duplicate icon).
- **Quit** removes the icon and exits.

- [ ] **Step 5: Commit**

```powershell
git add src/Gloam/TrayApp.cs src/Gloam/Program.cs
git commit -m "feat: add TrayApp tray UI and single-instance entry point"
```

---

## Task 7: Final verification and push

- [ ] **Step 1: Full build and test**

Run: `dotnet build` then `dotnet test`
Expected: build SUCCEEDS; all tests PASS (11 total: 8 Schedule + 3 Config).

- [ ] **Step 2: Confirm no leftover temporary code**

Verify `Program.cs` has no temporary `ThemeSwitcher.Apply` smoke-test line from Task 3.

- [ ] **Step 3: Push**

```powershell
git push
```

Expected: pushes all commits to `origin/main` on https://github.com/KiraFR/Gloam

---

## Self-Review (completed)

- **Spec coverage:** switch app+system theme (Task 3), fixed configurable times (Task 2 + SettingsForm Task 5), tray menu Auto/Light/Dark/Settings/Quit (Task 6), catch-up on launch (TrayApp `ApplyForNow`), JSON config at `%AppData%\Gloam` (Task 2), autostart toggle (Task 4 + Task 6), no wallpaper / no admin / no scheduled task (by construction), single instance (Task 6), equal-times rejected (Task 5), flash-free native mechanism (Task 3). All covered.
- **Placeholders:** none — every code step contains full content.
- **Type consistency:** `ThemeMode`, `Schedule.ModeFor(now, darkTime, lightTime)`, `Config.{DarkTime,LightTime,Auto,RunAtStartup}`, `Config.{DefaultPath,Load,Save}`, `ThemeSwitcher.{Apply,GetCurrent}`, `Startup.{Enable,Disable,IsEnabled}` are used consistently across tasks.
