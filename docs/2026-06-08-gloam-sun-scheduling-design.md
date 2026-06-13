# Gloam — Sun Scheduling, App Icon & Tray Autostart (Design)

Extension of the base Gloam app (see `2026-06-08-gloam-design.md`).

## Summary

Three additions:

1. **Sunrise/sunset scheduling** — an alternative to fixed times. In sun mode the
   go-dark time is the local sunset and the go-light time is the local sunrise,
   recomputed each day. Location comes from one of three sources: manual
   latitude/longitude, a one-shot Windows Location "Detect", or a city search.
2. **"Start with Windows" in the tray menu** — a checkable item mirroring the
   autostart state, in addition to the existing Settings checkbox.
3. **An application icon** — a proper `.ico` for the executable and the Settings
   window.

## Scope

**In scope**

- `ScheduleMode { Fixed, Sun }`. Sun mode derives the dark/light times from the
  day's sunset/sunrise; Fixed mode keeps the two configured times.
- `SunCalculator` — pure UTC sunrise/sunset (NOAA algorithm); `null` at polar
  day/night.
- Location input (Settings): manual lat/long; "Detect" via WinRT
  `Windows.Devices.Geolocation` (one-shot); city search via OpenStreetMap
  Nominatim (on-demand request).
- Settings UI: a mode selector that swaps between the Fixed panel (two time
  pickers) and the Sun panel (lat/long + Detect + city search + a read-only
  "today's sunrise/sunset" preview), with validation.
- Tray menu: a checkable **Start with Windows** item synced to
  `Startup.IsEnabled()`.
- App icon: `assets/gloam.ico` (half gold / half slate "gloaming" disc), used
  for the executable and the Settings window.

**Out of scope (YAGNI / future)**

- No offset from sunset/sunrise (dark = exact sunset, light = exact sunrise).
- No city autocomplete-as-you-type — Nominatim's usage policy forbids it; the
  city box resolves only when the user clicks Search.
- No continuous location tracking — Detect is a one-shot fill.

## Build / dependency changes

- **Target framework bump:** `src/Gloam` → `net8.0-windows10.0.19041.0` (needed
  to call the WinRT Geolocation APIs). The test project is bumped to the same
  TFM so it can reference the app project.
- **Nominatim** is reached with the BCL `HttpClient` (no NuGet package). Requests
  send a `User-Agent: Gloam/1.0 (github.com/KiraFR/Gloam)` header, as the policy
  requires.
- CI keeps `setup-dotnet` at `8.0.x`; the `windows-latest` runner has the
  Windows SDK needed for the `10.0.19041.0` TFM.

## Components

### `SunCalculator` (pure)

```
static (DateTime SunriseUtc, DateTime SunsetUtc)? SunTimesUtc(
    DateOnly date, double latitude, double longitude)
```

NOAA solar position algorithm. Returns `null` when the sun does not cross the
horizon that day at that location (polar day/night). Works entirely in UTC so it
is deterministic and unit-testable regardless of the machine's time zone.

### `ScheduleMode` (enum)

`Fixed` | `Sun`. Stored in `Config`.

### `Config` (extended)

New fields, with the existing ones kept:

- `ScheduleMode Mode` — default `Fixed`.
- `double Latitude` — default `48.8566` (Paris).
- `double Longitude` — default `2.3522` (Paris).
- existing: `DarkTime` (19:00), `LightTime` (07:00), `Auto`, `RunAtStartup`.

JSON load/save round-trips the new fields; missing fields fall back to defaults.

### `Schedule` (extended)

Keeps the existing pure `ModeFor(now, darkTime, lightTime)`. Adds a pure resolver
so the Fixed/Sun choice is testable without I/O:

```
static (TimeOnly Dark, TimeOnly Light) EffectiveTimes(
    ScheduleMode mode,
    TimeOnly fixedDark, TimeOnly fixedLight,
    TimeOnly? sunriseLocal, TimeOnly? sunsetLocal)
```

- Sun mode with both sun times present → `(Dark = sunsetLocal, Light = sunriseLocal)`.
- Otherwise (Fixed mode, or Sun mode with no sun event) → `(fixedDark, fixedLight)`.

### `Geocoder` (network)

```
static async Task<GeoResult?> LookupAsync(string query, CancellationToken ct)
// GeoResult { double Latitude; double Longitude; string DisplayName; }
```

`GET https://nominatim.openstreetmap.org/search?q={query}&format=json&limit=1`
with the required `User-Agent`. Returns the first result, or `null` on empty
results / network error (caught).

### `LocationDetector` (WinRT)

```
static async Task<(double Latitude, double Longitude)?> DetectAsync()
```

`Geolocator.RequestAccessAsync()`; if allowed, `GetGeopositionAsync()` → coords;
otherwise (denied/unavailable/exception) → `null`.

### `TrayApp` (integration)

- Computes effective times for the current day, converting `SunCalculator`'s UTC
  result to local with `TimeZoneInfo.Local`, then feeds
  `Schedule.EffectiveTimes` → `Schedule.ModeFor`. The 30 s timer recomputes each
  tick, so sun times follow the calendar and DST automatically.
- New checkable tray item **Start with Windows**: on menu open, `Checked =
  Startup.IsEnabled()`; on click, toggle `Startup.Enable(Application.ExecutablePath)`
  / `Startup.Disable()`, set `Config.RunAtStartup`, and save.
- Sets the window/app icon from `assets/gloam.ico`. The tray `NotifyIcon` keeps
  its dynamic mode-tinted icon (gold = light, slate = dark) for at-a-glance state.

### `SettingsForm` (extended)

- A mode selector (radio: **Fixed times** / **Sunrise-sunset**) swaps between:
  - **Fixed panel** — the two existing time pickers.
  - **Sun panel** — latitude and longitude fields; a **Detect** button
    (`LocationDetector`, fills the fields, shows a message on failure); a city
    text box + **Search** button (`Geocoder`, fills the fields and shows the
    resolved name); a read-only **Today: sunrise HH:mm · sunset HH:mm** preview
    that refreshes when the coordinates change.
- Validation: latitude in [-90, 90], longitude in [-180, 180]; the Fixed-mode
  equal-times rejection is kept.
- Exposes `Mode`, `Latitude`, `Longitude` in addition to the existing
  `DarkTime`, `LightTime`, `RunAtStartup`.
- Uses `gloam.ico` for its window icon. Layout stays auto-sized.

### Icon asset

`assets/gloam.ico` — a "gloaming" disc, left half gold (`#FFD700`), right half
slate (`LightSlateGray`), at multiple sizes (16/32/48/256). Generated with
`System.Drawing` and committed to the repo. Referenced via `<ApplicationIcon>`.

## Data flow

- **Launch:** `Config.Load` → `EffectiveTimes(today)` → `ModeFor(now)` →
  `ThemeSwitcher.Apply`.
- **Tick (30 s):** recompute `EffectiveTimes(today)` → `ModeFor(now)` → apply if
  the desired mode changed.
- **Settings save:** persist mode / times / lat / lon / autostart → re-apply if
  `Auto`.
- **Detect / Search:** fill the lat/long fields and refresh the preview; nothing
  is applied until the user confirms Settings with OK.

## Error handling

- `SunCalculator` returns `null` (polar) → `EffectiveTimes` falls back to the
  fixed times; sun mode degrades gracefully instead of failing.
- `Geocoder` / `LocationDetector` failure → `null` → a Settings message ("City
  not found", "Location unavailable"); the fields are left unchanged. Network
  and WinRT exceptions are caught — never crash.
- Theme write failure already shows a tray balloon (base app).

## Testing (xUnit)

- `SunCalculator.SunTimesUtc`: known location/date with expected UTC sunrise and
  sunset within ±2 minutes (e.g. Paris 48.8566 / 2.3522 on 2024-06-21), and a
  polar location/date returning `null`. Deterministic (UTC).
- `Schedule.EffectiveTimes`: Sun mode maps sunset→dark and sunrise→light; Sun
  mode with `null` sun times falls back to fixed; Fixed mode passes the fixed
  times through.
- `Config`: round-trip of the new fields and the Paris/Fixed defaults.
- `Geocoder` and `LocationDetector` are thin wrappers over network/WinRT and are
  verified manually, not unit-tested.

## Future

- Sunset/sunrise offset (e.g. go dark 30 min after sunset).
- City autocomplete (would need a bundled city database to respect Nominatim's
  policy).
