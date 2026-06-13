# Gloam Sun Scheduling, Icon & Tray Autostart — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Extend Gloam with sunrise/sunset scheduling (manual lat-long, Windows detect, or city search), a tray "Start with Windows" toggle, and a real app icon.

**Architecture:** Add a pure `SunCalculator` (NOAA, UTC) and a pure `Schedule.EffectiveTimes` resolver; extend `Config`; add thin `Geocoder` (Nominatim) and `LocationDetector` (WinRT) wrappers; rework `SettingsForm` and wire it all in `TrayApp`. Reuse the existing tested `Schedule.ModeFor`.

**Tech Stack:** .NET `net8.0-windows10.0.19041.0`, WinForms, WinRT `Windows.Devices.Geolocation`, `HttpClient` (Nominatim), `System.Drawing`, xUnit.

---

## File Structure

```
src/Gloam/
  Gloam.csproj           MODIFY  TFM bump + <ApplicationIcon>
  ScheduleMode.cs        CREATE  enum { Fixed, Sun }
  SunCalculator.cs       CREATE  pure NOAA UTC sunrise/sunset
  Schedule.cs            MODIFY  add EffectiveTimes resolver
  Config.cs              MODIFY  add Mode, Latitude, Longitude
  Geocoder.cs            CREATE  Nominatim city lookup
  LocationDetector.cs    CREATE  WinRT one-shot detect
  TrayApp.cs             MODIFY  effective times + autostart item + window icon
  SettingsForm.cs        REWRITE mode panels + location inputs + preview
tests/Gloam.Tests/
  Gloam.Tests.csproj     MODIFY  TFM bump
  SunCalculatorTests.cs  CREATE
  ScheduleTests.cs       MODIFY  add EffectiveTimes tests
  ConfigTests.cs         MODIFY  add new-field tests
scripts/
  generate-icon.ps1      CREATE  draws assets/gloam.ico
assets/
  gloam.ico              CREATE  generated, committed
```

Commands: build `dotnet build`, test `dotnet test`. If git complains about identity, use `git -c user.name="jimmy" -c user.email="jimmydelannoy@gmail.com" commit ...`. Work on branch `feat/gloam-implementation` in `C:\Users\jimmy\Documents\GitHub\Gloam`.

---

## Task 1: Bump target framework for WinRT

**Files:** Modify `src/Gloam/Gloam.csproj`, `tests/Gloam.Tests/Gloam.Tests.csproj`

- [ ] **Step 1: Bump the app TFM**

In `src/Gloam/Gloam.csproj`, change the `<TargetFramework>` line from `net8.0-windows` to:
```xml
    <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
```
(Leave all other properties unchanged.)

- [ ] **Step 2: Bump the test TFM**

In `tests/Gloam.Tests/Gloam.Tests.csproj`, change `<TargetFramework>net8.0-windows</TargetFramework>` to:
```xml
    <TargetFramework>net8.0-windows10.0.19041.0</TargetFramework>
```

- [ ] **Step 3: Build and test to confirm nothing broke**

Run: `dotnet test`
Expected: build SUCCEEDS, all 11 existing tests PASS.

- [ ] **Step 4: Commit**
```powershell
git add src/Gloam/Gloam.csproj tests/Gloam.Tests/Gloam.Tests.csproj
git commit -m "build: target net8.0-windows10.0.19041.0 for WinRT geolocation"
```

---

## Task 2: ScheduleMode + Config extension (TDD)

**Files:** Create `src/Gloam/ScheduleMode.cs`; Modify `src/Gloam/Config.cs`; Modify `tests/Gloam.Tests/ConfigTests.cs`

- [ ] **Step 1: Add the failing tests**

Append these tests to `tests/Gloam.Tests/ConfigTests.cs` (inside the `ConfigTests` class):
```csharp
    [Fact]
    public void Defaults_are_fixed_mode_and_paris()
    {
        var c = new Config();
        Assert.Equal(ScheduleMode.Fixed, c.Mode);
        Assert.Equal(48.8566, c.Latitude, 4);
        Assert.Equal(2.3522, c.Longitude, 4);
    }

    [Fact]
    public void Save_then_load_roundtrips_mode_and_location()
    {
        var path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        try
        {
            var c = new Config { Mode = ScheduleMode.Sun, Latitude = 51.5074, Longitude = -0.1278 };
            c.Save(path);
            var loaded = Config.Load(path);
            Assert.Equal(ScheduleMode.Sun, loaded.Mode);
            Assert.Equal(51.5074, loaded.Latitude, 4);
            Assert.Equal(-0.1278, loaded.Longitude, 4);
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --filter ConfigTests`
Expected: FAIL — `ScheduleMode`, `Config.Mode`, `Config.Latitude`, `Config.Longitude` do not exist.

- [ ] **Step 3: Create the enum**

Create `src/Gloam/ScheduleMode.cs`:
```csharp
namespace Gloam;

public enum ScheduleMode
{
    Fixed,
    Sun
}
```

- [ ] **Step 4: Extend Config**

In `src/Gloam/Config.cs`, add these three properties alongside the existing ones (after `RunAtStartup`):
```csharp
    public ScheduleMode Mode { get; set; } = ScheduleMode.Fixed;
    public double Latitude { get; set; } = 48.8566;   // Paris
    public double Longitude { get; set; } = 2.3522;   // Paris
```

- [ ] **Step 5: Run to verify pass**

Run: `dotnet test --filter ConfigTests`
Expected: PASS (5 tests).

- [ ] **Step 6: Commit**
```powershell
git add src/Gloam/ScheduleMode.cs src/Gloam/Config.cs tests/Gloam.Tests/ConfigTests.cs
git commit -m "feat: add ScheduleMode and location fields to Config"
```

---

## Task 3: SunCalculator (TDD)

**Files:** Create `src/Gloam/SunCalculator.cs`; Create `tests/Gloam.Tests/SunCalculatorTests.cs`

The expected values below are independent real-world times (Paris summer solstice ≈ sunrise 03:47 UTC, sunset 19:57 UTC; Tromsø winter solstice = polar night).

- [ ] **Step 1: Write the failing tests**

Create `tests/Gloam.Tests/SunCalculatorTests.cs`:
```csharp
using Gloam;
using Xunit;

public class SunCalculatorTests
{
    [Fact]
    public void Paris_summer_solstice_matches_real_utc_times()
    {
        var result = SunCalculator.SunTimesUtc(new DateOnly(2024, 6, 21), 48.8566, 2.3522);

        Assert.NotNull(result);
        var (sunrise, sunset) = result!.Value;

        var expectedSunrise = new DateTime(2024, 6, 21, 3, 47, 0, DateTimeKind.Utc);
        var expectedSunset = new DateTime(2024, 6, 21, 19, 57, 0, DateTimeKind.Utc);

        Assert.True(Math.Abs((sunrise - expectedSunrise).TotalMinutes) <= 3,
            $"sunrise was {sunrise:HH:mm} UTC");
        Assert.True(Math.Abs((sunset - expectedSunset).TotalMinutes) <= 3,
            $"sunset was {sunset:HH:mm} UTC");
    }

    [Fact]
    public void Sunset_is_after_sunrise()
    {
        var result = SunCalculator.SunTimesUtc(new DateOnly(2024, 3, 20), 40.0, -74.0);
        Assert.NotNull(result);
        Assert.True(result!.Value.SunsetUtc > result.Value.SunriseUtc);
    }

    [Fact]
    public void Polar_night_returns_null()
    {
        // Tromsø, deep winter: the sun does not rise.
        var result = SunCalculator.SunTimesUtc(new DateOnly(2024, 12, 21), 69.6492, 18.9553);
        Assert.Null(result);
    }
}
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --filter SunCalculatorTests`
Expected: FAIL — `SunCalculator` does not exist.

- [ ] **Step 3: Implement SunCalculator**

Create `src/Gloam/SunCalculator.cs`:
```csharp
namespace Gloam;

/// <summary>
/// Sunrise and sunset in UTC for a date and location, using the NOAA solar
/// approximation (accurate to ~1-2 minutes). Longitude is degrees east-positive,
/// latitude degrees north-positive. Returns null when the sun does not cross the
/// horizon that day (polar day or night).
/// </summary>
public static class SunCalculator
{
    public static (DateTime SunriseUtc, DateTime SunsetUtc)? SunTimesUtc(
        DateOnly date, double latitude, double longitude)
    {
        const double rad = Math.PI / 180.0;

        int n = date.DayOfYear;
        double gamma = 2.0 * Math.PI / 365.0 * (n - 1);

        double eqTime = 229.18 * (0.000075
            + 0.001868 * Math.Cos(gamma)
            - 0.032077 * Math.Sin(gamma)
            - 0.014615 * Math.Cos(2 * gamma)
            - 0.040849 * Math.Sin(2 * gamma));

        double decl = 0.006918
            - 0.399912 * Math.Cos(gamma)
            + 0.070257 * Math.Sin(gamma)
            - 0.006758 * Math.Cos(2 * gamma)
            + 0.000907 * Math.Sin(2 * gamma)
            - 0.002697 * Math.Cos(3 * gamma)
            + 0.001480 * Math.Sin(3 * gamma);

        double latRad = latitude * rad;
        double cosHourAngle =
            Math.Cos(90.833 * rad) / (Math.Cos(latRad) * Math.Cos(decl))
            - Math.Tan(latRad) * Math.Tan(decl);

        if (cosHourAngle > 1.0 || cosHourAngle < -1.0)
            return null; // polar day or night

        double hourAngleDeg = Math.Acos(cosHourAngle) / rad;

        // Minutes after 00:00 UTC. Longitude is east-positive.
        double solarNoonMin = 720.0 - 4.0 * longitude - eqTime;
        double sunriseMin = solarNoonMin - 4.0 * hourAngleDeg;
        double sunsetMin = solarNoonMin + 4.0 * hourAngleDeg;

        var midnightUtc = new DateTime(date.Year, date.Month, date.Day, 0, 0, 0, DateTimeKind.Utc);
        return (midnightUtc.AddMinutes(sunriseMin), midnightUtc.AddMinutes(sunsetMin));
    }
}
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test --filter SunCalculatorTests`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**
```powershell
git add src/Gloam/SunCalculator.cs tests/Gloam.Tests/SunCalculatorTests.cs
git commit -m "feat: add pure NOAA SunCalculator (UTC sunrise/sunset)"
```

---

## Task 4: Schedule.EffectiveTimes (TDD)

**Files:** Modify `src/Gloam/Schedule.cs`; Modify `tests/Gloam.Tests/ScheduleTests.cs`

- [ ] **Step 1: Add the failing tests**

Append to the `ScheduleTests` class in `tests/Gloam.Tests/ScheduleTests.cs`:
```csharp
    [Fact]
    public void EffectiveTimes_fixed_mode_passes_through()
    {
        var (dark, light) = Schedule.EffectiveTimes(
            ScheduleMode.Fixed, T(19), T(7), sunriseLocal: T(6), sunsetLocal: T(21));
        Assert.Equal(T(19), dark);
        Assert.Equal(T(7), light);
    }

    [Fact]
    public void EffectiveTimes_sun_mode_maps_sunset_to_dark_and_sunrise_to_light()
    {
        var (dark, light) = Schedule.EffectiveTimes(
            ScheduleMode.Sun, T(19), T(7), sunriseLocal: T(6, 12), sunsetLocal: T(21, 48));
        Assert.Equal(T(21, 48), dark);
        Assert.Equal(T(6, 12), light);
    }

    [Fact]
    public void EffectiveTimes_sun_mode_without_sun_times_falls_back_to_fixed()
    {
        var (dark, light) = Schedule.EffectiveTimes(
            ScheduleMode.Sun, T(19), T(7), sunriseLocal: null, sunsetLocal: null);
        Assert.Equal(T(19), dark);
        Assert.Equal(T(7), light);
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test --filter ScheduleTests`
Expected: FAIL — `Schedule.EffectiveTimes` does not exist.

- [ ] **Step 3: Implement EffectiveTimes**

In `src/Gloam/Schedule.cs`, add this method inside the `Schedule` class (after `ModeFor`):
```csharp
    /// <summary>
    /// Resolves the effective dark/light times. In Sun mode with both sun times
    /// present, dark = sunset and light = sunrise; otherwise the fixed times are
    /// used (also the graceful fallback for polar days with no sun event).
    /// </summary>
    public static (TimeOnly Dark, TimeOnly Light) EffectiveTimes(
        ScheduleMode mode,
        TimeOnly fixedDark, TimeOnly fixedLight,
        TimeOnly? sunriseLocal, TimeOnly? sunsetLocal)
    {
        if (mode == ScheduleMode.Sun && sunriseLocal is { } sunrise && sunsetLocal is { } sunset)
            return (sunset, sunrise);

        return (fixedDark, fixedLight);
    }
```

- [ ] **Step 4: Run to verify pass**

Run: `dotnet test --filter ScheduleTests`
Expected: PASS (11 tests: 8 original + 3 new).

- [ ] **Step 5: Commit**
```powershell
git add src/Gloam/Schedule.cs tests/Gloam.Tests/ScheduleTests.cs
git commit -m "feat: add Schedule.EffectiveTimes resolver for fixed/sun modes"
```

---

## Task 5: Geocoder (Nominatim city lookup)

**Files:** Create `src/Gloam/Geocoder.cs`

No automated test (network). Verified manually via the Settings city search later.

- [ ] **Step 1: Implement Geocoder**

Create `src/Gloam/Geocoder.cs`:
```csharp
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Gloam;

public readonly record struct GeoResult(double Latitude, double Longitude, string DisplayName);

/// <summary>
/// Resolves a place name to coordinates via the OpenStreetMap Nominatim API.
/// One request per call (no autocomplete), per Nominatim's usage policy.
/// </summary>
public static class Geocoder
{
    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("Gloam", "1.0"));
        client.DefaultRequestHeaders.UserAgent.Add(
            new ProductInfoHeaderValue("(+https://github.com/KiraFR/Gloam)"));
        return client;
    }

    public static async Task<GeoResult?> LookupAsync(string query, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return null;

        try
        {
            string url =
                $"https://nominatim.openstreetmap.org/search?format=json&limit=1&q={Uri.EscapeDataString(query)}";
            await using var stream = await Http.GetStreamAsync(url, ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                return null;

            var first = doc.RootElement[0];
            double lat = double.Parse(first.GetProperty("lat").GetString()!,
                System.Globalization.CultureInfo.InvariantCulture);
            double lon = double.Parse(first.GetProperty("lon").GetString()!,
                System.Globalization.CultureInfo.InvariantCulture);
            string name = first.TryGetProperty("display_name", out var dn)
                ? dn.GetString() ?? query
                : query;

            return new GeoResult(lat, lon, name);
        }
        catch
        {
            return null; // network/parse error -> treated as "not found" by the UI
        }
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: build SUCCEEDS.

- [ ] **Step 3: Commit**
```powershell
git add src/Gloam/Geocoder.cs
git commit -m "feat: add Geocoder (Nominatim city lookup)"
```

---

## Task 6: LocationDetector (WinRT one-shot)

**Files:** Create `src/Gloam/LocationDetector.cs`

No automated test (WinRT/permission). Verified manually via the Settings Detect button later.

- [ ] **Step 1: Implement LocationDetector**

Create `src/Gloam/LocationDetector.cs`:
```csharp
using Windows.Devices.Geolocation;

namespace Gloam;

/// <summary>
/// One-shot current-location lookup via the Windows location service.
/// Returns null when access is denied or the position is unavailable.
/// </summary>
public static class LocationDetector
{
    public static async Task<(double Latitude, double Longitude)?> DetectAsync()
    {
        try
        {
            var access = await Geolocator.RequestAccessAsync();
            if (access != GeolocationAccessStatus.Allowed)
                return null;

            var locator = new Geolocator { DesiredAccuracyInMeters = 5000 };
            var position = await locator.GetGeopositionAsync();
            var coord = position.Coordinate.Point.Position;
            return (coord.Latitude, coord.Longitude);
        }
        catch
        {
            return null; // denied / unavailable / WinRT error
        }
    }
}
```

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: build SUCCEEDS (the `Windows.Devices.Geolocation` namespace resolves because the TFM is `net8.0-windows10.0.19041.0`).

- [ ] **Step 3: Commit**
```powershell
git add src/Gloam/LocationDetector.cs
git commit -m "feat: add LocationDetector (WinRT one-shot geolocation)"
```

---

## Task 7: App icon

**Files:** Create `scripts/generate-icon.ps1`, `assets/gloam.ico`; Modify `src/Gloam/Gloam.csproj`

- [ ] **Step 1: Create the icon generator script**

Create `scripts/generate-icon.ps1`:
```powershell
# Generates assets/gloam.ico: a "gloaming" disc, gold left half, slate right half.
Add-Type -AssemblyName System.Drawing

$sizes = 16, 32, 48, 256
$pngs = @()

foreach ($s in $sizes) {
    $bmp = New-Object System.Drawing.Bitmap($s, $s)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias
    $g.Clear([System.Drawing.Color]::Transparent)
    $pad = [Math]::Max(1, [int]($s * 0.08))
    $d = $s - 2 * $pad
    $gold = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 215, 0))
    $slate = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 119, 136, 153))
    # Left semicircle (gold): start at 90 deg, sweep 180 clockwise.
    $g.FillPie($gold, $pad, $pad, $d, $d, 90, 180)
    # Right semicircle (slate): start at 270 deg, sweep 180.
    $g.FillPie($slate, $pad, $pad, $d, $d, 270, 180)
    $g.Dispose()

    $ms = New-Object System.IO.MemoryStream
    $bmp.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $bmp.Dispose()
    $pngs += , $ms.ToArray()
}

# Assemble an ICO that stores each image as PNG (supported on Windows Vista+).
$out = New-Object System.IO.MemoryStream
$bw = New-Object System.IO.BinaryWriter($out)
$bw.Write([UInt16]0)      # reserved
$bw.Write([UInt16]1)      # type: icon
$bw.Write([UInt16]$sizes.Count)

$offset = 6 + 16 * $sizes.Count
for ($i = 0; $i -lt $sizes.Count; $i++) {
    $s = $sizes[$i]
    $bytes = $pngs[$i]
    $dim = if ($s -ge 256) { 0 } else { $s }
    $bw.Write([Byte]$dim)         # width
    $bw.Write([Byte]$dim)         # height
    $bw.Write([Byte]0)            # color count
    $bw.Write([Byte]0)            # reserved
    $bw.Write([UInt16]1)          # color planes
    $bw.Write([UInt16]32)         # bits per pixel
    $bw.Write([UInt32]$bytes.Length)
    $bw.Write([UInt32]$offset)
    $offset += $bytes.Length
}
foreach ($bytes in $pngs) { $bw.Write($bytes) }
$bw.Flush()

$dir = Join-Path $PSScriptRoot "..\assets"
New-Item -ItemType Directory -Force -Path $dir | Out-Null
[System.IO.File]::WriteAllBytes((Join-Path $dir "gloam.ico"), $out.ToArray())
Write-Host "Wrote assets/gloam.ico ($($out.Length) bytes)"
```

- [ ] **Step 2: Run the generator**

Run: `pwsh -File scripts/generate-icon.ps1`
Expected: prints "Wrote assets/gloam.ico (...)" and `assets/gloam.ico` exists.

- [ ] **Step 3: Verify it loads as an icon**

Run:
```powershell
Add-Type -AssemblyName System.Drawing
$ico = New-Object System.Drawing.Icon("assets/gloam.ico")
"Loaded icon, size $($ico.Width)x$($ico.Height)"
$ico.Dispose()
```
Expected: prints a size with no exception.

- [ ] **Step 4: Reference the icon in the app**

In `src/Gloam/Gloam.csproj`, replace the empty `<ApplicationIcon></ApplicationIcon>` line with:
```xml
    <ApplicationIcon>..\..\assets\gloam.ico</ApplicationIcon>
```

- [ ] **Step 5: Build**

Run: `dotnet build`
Expected: build SUCCEEDS (the exe now carries the icon).

- [ ] **Step 6: Commit**
```powershell
git add scripts/generate-icon.ps1 assets/gloam.ico src/Gloam/Gloam.csproj
git commit -m "feat: add gloaming app icon and wire ApplicationIcon"
```

---

## Task 8: TrayApp integration

**Files:** Modify `src/Gloam/TrayApp.cs`

- [ ] **Step 1: Replace TrayApp.cs**

Replace the entire `src/Gloam/TrayApp.cs` with the version below. Changes vs. the current file: a `_startupItem` tray entry, a `menu.Opening` sync, an `EffectiveTimes` helper, `Tick`/`ApplyForNow` using effective times, the `ToggleStartup` method, and the Settings window now also persisting `Mode`/`Latitude`/`Longitude`. The Settings window icon is handled inside `SettingsForm` (Task 9).

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
    private readonly ToolStripMenuItem _startupItem;
    private readonly Config _config;
    private ThemeMode _applied;
    private IntPtr _hIcon = IntPtr.Zero;

    public TrayApp()
    {
        _config = Config.Load(Config.DefaultPath);

        _autoItem = new ToolStripMenuItem("Auto", null, (_, _) => SetAuto());
        _lightItem = new ToolStripMenuItem("Light", null, (_, _) => SetManual(ThemeMode.Light));
        _darkItem = new ToolStripMenuItem("Dark", null, (_, _) => SetManual(ThemeMode.Dark));
        _startupItem = new ToolStripMenuItem("Start with Windows", null, (_, _) => ToggleStartup());
        var settingsItem = new ToolStripMenuItem("Settings…", null, (_, _) => OpenSettings());
        var quitItem = new ToolStripMenuItem("Quit", null, (_, _) => Quit());

        var menu = new ContextMenuStrip();
        menu.Items.AddRange(new ToolStripItem[]
        {
            _autoItem, _lightItem, _darkItem,
            new ToolStripSeparator(),
            _startupItem,
            new ToolStripSeparator(),
            settingsItem,
            new ToolStripSeparator(),
            quitItem
        });
        menu.Opening += (_, _) => _startupItem.Checked = Startup.IsEnabled();

        _icon = new NotifyIcon { Visible = true, ContextMenuStrip = menu, Text = "Gloam" };

        _timer = new System.Windows.Forms.Timer { Interval = 30_000 };
        _timer.Tick += (_, _) => Tick();
        _timer.Start();

        ApplyForNow(); // catch-up on launch
    }

    private (TimeOnly Dark, TimeOnly Light) EffectiveTimes(DateOnly date)
    {
        TimeOnly? sunrise = null, sunset = null;
        if (_config.Mode == ScheduleMode.Sun)
        {
            var sun = SunCalculator.SunTimesUtc(date, _config.Latitude, _config.Longitude);
            if (sun is { } v)
            {
                sunrise = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(v.SunriseUtc, TimeZoneInfo.Local));
                sunset = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(v.SunsetUtc, TimeZoneInfo.Local));
            }
        }
        return Schedule.EffectiveTimes(_config.Mode, _config.DarkTime, _config.LightTime, sunrise, sunset);
    }

    private ThemeMode DesiredNow()
    {
        var now = DateTime.Now;
        var (dark, light) = EffectiveTimes(DateOnly.FromDateTime(now));
        return Schedule.ModeFor(TimeOnly.FromDateTime(now), dark, light);
    }

    private void Tick()
    {
        if (!_config.Auto) return;
        var desired = DesiredNow();
        if (desired != _applied) ApplyMode(desired);
    }

    private void ApplyForNow()
    {
        var mode = _config.Auto ? DesiredNow() : ThemeSwitcher.GetCurrent();
        ApplyMode(mode);
    }

    private void ApplyMode(ThemeMode mode)
    {
        try
        {
            ThemeSwitcher.Apply(mode);
        }
        catch (Exception ex)
        {
            _icon.ShowBalloonTip(5000, "Gloam",
                $"Could not change the theme: {ex.Message}", ToolTipIcon.Warning);
            return;
        }

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

    private void ToggleStartup()
    {
        if (Startup.IsEnabled()) Startup.Disable();
        else Startup.Enable(Application.ExecutablePath);

        _config.RunAtStartup = Startup.IsEnabled();
        _config.Save(Config.DefaultPath);
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(_config);
        if (form.ShowDialog() != DialogResult.OK) return;

        _config.Mode = form.Mode;
        _config.DarkTime = form.DarkTime;
        _config.LightTime = form.LightTime;
        _config.Latitude = form.Latitude;
        _config.Longitude = form.Longitude;
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

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: build will FAIL only if `SettingsForm` does not yet expose `Mode`/`Latitude`/`Longitude` — that is added in Task 9. If it fails for that reason, proceed to Task 9 and build there. Otherwise it SUCCEEDS.

- [ ] **Step 3: Commit**
```powershell
git add src/Gloam/TrayApp.cs
git commit -m "feat: wire sun scheduling and Start-with-Windows item into TrayApp"
```
(If the build is blocked pending Task 9, still commit — Task 9 completes the compile.)

---

## Task 9: SettingsForm rewrite

**Files:** Rewrite `src/Gloam/SettingsForm.cs`

- [ ] **Step 1: Replace SettingsForm.cs**

Replace the entire `src/Gloam/SettingsForm.cs` with:
```csharp
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;

namespace Gloam;

public sealed class SettingsForm : Form
{
    private readonly RadioButton _fixedRadio = new() { Text = "Fixed times", AutoSize = true, Checked = true };
    private readonly RadioButton _sunRadio = new() { Text = "Sunrise / sunset", AutoSize = true };

    private readonly DateTimePicker _darkPicker =
        new() { Format = DateTimePickerFormat.Time, ShowUpDown = true, Width = 90 };
    private readonly DateTimePicker _lightPicker =
        new() { Format = DateTimePickerFormat.Time, ShowUpDown = true, Width = 90 };

    private readonly NumericUpDown _latInput =
        new() { Minimum = -90, Maximum = 90, DecimalPlaces = 4, Increment = 0.1m, Width = 90 };
    private readonly NumericUpDown _lonInput =
        new() { Minimum = -180, Maximum = 180, DecimalPlaces = 4, Increment = 0.1m, Width = 90 };
    private readonly TextBox _cityInput = new() { Width = 150 };
    private readonly Label _preview = new() { AutoSize = true, ForeColor = SystemColors.GrayText };

    private readonly CheckBox _startupCheck = new() { Text = "Start with Windows", AutoSize = true };

    private readonly Panel _fixedPanel = new() { AutoSize = true };
    private readonly Panel _sunPanel = new() { AutoSize = true };

    public ScheduleMode Mode => _sunRadio.Checked ? ScheduleMode.Sun : ScheduleMode.Fixed;
    public TimeOnly DarkTime => TimeOnly.FromDateTime(_darkPicker.Value);
    public TimeOnly LightTime => TimeOnly.FromDateTime(_lightPicker.Value);
    public double Latitude => (double)_latInput.Value;
    public double Longitude => (double)_lonInput.Value;
    public bool RunAtStartup => _startupCheck.Checked;

    public SettingsForm(Config config)
    {
        Text = "Gloam — Settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);
        try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { /* no icon */ }

        var today = DateTime.Today;
        _darkPicker.Value = today + config.DarkTime.ToTimeSpan();
        _lightPicker.Value = today + config.LightTime.ToTimeSpan();
        _latInput.Value = (decimal)config.Latitude;
        _lonInput.Value = (decimal)config.Longitude;
        _startupCheck.Checked = config.RunAtStartup;
        _fixedRadio.Checked = config.Mode == ScheduleMode.Fixed;
        _sunRadio.Checked = config.Mode == ScheduleMode.Sun;

        BuildFixedPanel();
        BuildSunPanel();

        _fixedRadio.CheckedChanged += (_, _) => UpdatePanels();
        _sunRadio.CheckedChanged += (_, _) => UpdatePanels();
        _latInput.ValueChanged += (_, _) => RefreshPreview();
        _lonInput.ValueChanged += (_, _) => RefreshPreview();

        var ok = new Button
        {
            Text = "OK", DialogResult = DialogResult.OK, AutoSize = true, Margin = new Padding(6, 0, 0, 0)
        };
        var cancel = new Button
        {
            Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true, Margin = new Padding(6, 0, 0, 0)
        };
        ok.Click += (_, _) =>
        {
            if (Mode == ScheduleMode.Fixed && DarkTime == LightTime)
            {
                MessageBox.Show("Dark and light times must differ.", "Gloam",
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                DialogResult = DialogResult.None;
            }
        };

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill, Margin = new Padding(0, 10, 0, 0)
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);

        var root = new TableLayoutPanel
        {
            AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1, Dock = DockStyle.Fill
        };
        var modeRow = new FlowLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink };
        modeRow.Controls.Add(_fixedRadio);
        modeRow.Controls.Add(_sunRadio);
        root.Controls.Add(modeRow);
        root.Controls.Add(_fixedPanel);
        root.Controls.Add(_sunPanel);
        root.Controls.Add(_startupCheck);
        root.Controls.Add(buttons);

        Controls.Add(root);
        AcceptButton = ok;
        CancelButton = cancel;

        UpdatePanels();
    }

    private void BuildFixedPanel()
    {
        var t = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 2 };
        t.Controls.Add(new Label { Text = "Go dark at:", AutoSize = true, Margin = new Padding(0, 6, 12, 6) }, 0, 0);
        t.Controls.Add(_darkPicker, 1, 0);
        t.Controls.Add(new Label { Text = "Go light at:", AutoSize = true, Margin = new Padding(0, 6, 12, 6) }, 0, 1);
        t.Controls.Add(_lightPicker, 1, 1);
        _fixedPanel.Controls.Add(t);
    }

    private void BuildSunPanel()
    {
        var t = new TableLayoutPanel { AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, ColumnCount = 3 };

        t.Controls.Add(new Label { Text = "Latitude:", AutoSize = true, Margin = new Padding(0, 6, 12, 6) }, 0, 0);
        t.Controls.Add(_latInput, 1, 0);
        var detect = new Button { Text = "Detect", AutoSize = true, Margin = new Padding(12, 2, 0, 2) };
        detect.Click += async (_, _) => await DetectAsync(detect);
        t.Controls.Add(detect, 2, 0);

        t.Controls.Add(new Label { Text = "Longitude:", AutoSize = true, Margin = new Padding(0, 6, 12, 6) }, 0, 1);
        t.Controls.Add(_lonInput, 1, 1);

        t.Controls.Add(new Label { Text = "City:", AutoSize = true, Margin = new Padding(0, 6, 12, 6) }, 0, 2);
        t.Controls.Add(_cityInput, 1, 2);
        var search = new Button { Text = "Search", AutoSize = true, Margin = new Padding(12, 2, 0, 2) };
        search.Click += async (_, _) => await SearchAsync(search);
        t.Controls.Add(search, 2, 2);

        t.Controls.Add(_preview, 0, 3);
        t.SetColumnSpan(_preview, 3);

        _sunPanel.Controls.Add(t);
    }

    private void UpdatePanels()
    {
        _fixedPanel.Visible = _fixedRadio.Checked;
        _sunPanel.Visible = _sunRadio.Checked;
        if (_sunRadio.Checked) RefreshPreview();
    }

    private void RefreshPreview()
    {
        var sun = SunCalculator.SunTimesUtc(DateOnly.FromDateTime(DateTime.Today), Latitude, Longitude);
        if (sun is { } v)
        {
            var sunrise = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(v.SunriseUtc, TimeZoneInfo.Local));
            var sunset = TimeOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(v.SunsetUtc, TimeZoneInfo.Local));
            _preview.Text = $"Today: sunrise {sunrise:HH:mm} · sunset {sunset:HH:mm}";
        }
        else
        {
            _preview.Text = "No sunrise/sunset today at this location.";
        }
    }

    private async Task DetectAsync(Button source)
    {
        source.Enabled = false;
        try
        {
            var loc = await LocationDetector.DetectAsync();
            if (loc is { } p)
            {
                _latInput.Value = ClampLat((decimal)p.Latitude);
                _lonInput.Value = ClampLon((decimal)p.Longitude);
            }
            else
            {
                MessageBox.Show("Location unavailable (check Windows location permission).", "Gloam",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        finally { source.Enabled = true; }
    }

    private async Task SearchAsync(Button source)
    {
        source.Enabled = false;
        try
        {
            var result = await Geocoder.LookupAsync(_cityInput.Text);
            if (result is { } r)
            {
                _latInput.Value = ClampLat((decimal)r.Latitude);
                _lonInput.Value = ClampLon((decimal)r.Longitude);
                _cityInput.Text = r.DisplayName;
            }
            else
            {
                MessageBox.Show("City not found.", "Gloam",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        finally { source.Enabled = true; }
    }

    private static decimal ClampLat(decimal v) => Math.Clamp(v, -90m, 90m);
    private static decimal ClampLon(decimal v) => Math.Clamp(v, -180m, 180m);
}
```

- [ ] **Step 2: Build**

Run: `dotnet build`
Expected: build SUCCEEDS (TrayApp from Task 8 now compiles against the new `SettingsForm` members).

- [ ] **Step 3: Run the full test suite**

Run: `dotnet test`
Expected: PASS — **19 tests**: ScheduleTests 11 (8 original + 3 EffectiveTimes), SunCalculatorTests 3, ConfigTests 5 (3 original + 2 new). Confirm 0 failures.

- [ ] **Step 4: Commit**
```powershell
git add src/Gloam/SettingsForm.cs
git commit -m "feat: settings UI for fixed/sun modes with detect and city search"
```

---

## Task 10: Final verification

- [ ] **Step 1: Release build + full test**

Run: `dotnet build -c Release` then `dotnet test -c Release`
Expected: build SUCCEEDS, all tests PASS (0 failures).

- [ ] **Step 2: Manual smoke (controller/user, optional)**

Launch `src/Gloam/bin/Release/net8.0-windows10.0.19041.0/Gloam.exe`, then:
- The exe/window shows the gloaming icon.
- Tray menu shows **Start with Windows** with the correct check state; toggling it adds/removes the `Gloam` value under `HKCU\…\Run`.
- Settings: switch to **Sunrise / sunset**; the preview shows today's sunrise/sunset for the Paris default; **Detect** fills coordinates (or reports unavailable); **Search** a city fills coordinates; OK saves.
- In sun mode, the applied theme matches day/night for the configured location.

- [ ] **Step 3: Push**
```powershell
git push
```

---

## Self-Review (completed)

- **Spec coverage:** ScheduleMode + Config (Task 2); SunCalculator pure UTC + polar null (Task 3); EffectiveTimes resolver (Task 4); Geocoder/Nominatim (Task 5); LocationDetector/WinRT + TFM bump (Tasks 1, 6); icon (Task 7); TrayApp sun integration + Start-with-Windows item synced to IsEnabled + window icon (Tasks 8, 9); Settings mode panels + manual lat/long + Detect + city search + preview + validation (Task 9). All spec sections map to a task.
- **Placeholders:** none — every code step has complete content.
- **Type consistency:** `ScheduleMode`, `Config.{Mode,Latitude,Longitude}`, `SunCalculator.SunTimesUtc(DateOnly,double,double) -> (DateTime SunriseUtc, DateTime SunsetUtc)?`, `Schedule.EffectiveTimes(ScheduleMode,TimeOnly,TimeOnly,TimeOnly?,TimeOnly?)`, `Geocoder.LookupAsync -> GeoResult?` with `GeoResult(Latitude,Longitude,DisplayName)`, `LocationDetector.DetectAsync -> (double Latitude,double Longitude)?`, and `SettingsForm.{Mode,DarkTime,LightTime,Latitude,Longitude,RunAtStartup}` are used consistently across Tasks 8–9.
