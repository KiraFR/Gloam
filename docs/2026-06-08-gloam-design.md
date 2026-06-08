# Gloam — Design

## Purpose

Gloam is a lightweight Windows system-tray utility that switches the Windows
light/dark theme at fixed, user-configurable times. It uses the native theme
mechanism (two registry values + a `WM_SETTINGCHANGE` broadcast), so the switch
is instant and **flash-free**.

## Background / Motivation

Third-party theme switchers (Auto Dark Mode, f.lux) caused visible screen
flashes: they reload full `.theme` files, change the wallpaper, and conflict
with one another. Each of those triggers a desktop repaint / display mode-set,
which blanks both monitors for a fraction of a second.

Gloam deliberately does the minimum Windows itself does when you toggle the
theme in Settings: write the two theme registry values and broadcast the
standard settings-change message. No wallpaper change, no `.theme` reload, no
heavy repaint — therefore no flash.

## Scope

**In scope**

- Switch the app theme and system theme (`AppsUseLightTheme`,
  `SystemUsesLightTheme`) at two configured times (go-dark, go-light).
- Tray icon (sun / moon depending on the active mode) with a context menu:
  Auto / Light / Dark, Settings…, Quit.
- Settings window: two time pickers (go-dark time, go-light time) and a
  "Start with Windows" checkbox.
- Catch-up on launch: apply the correct theme for the current time immediately.
- Persist configuration as JSON at `%AppData%\Gloam\config.json`.

**Out of scope (YAGNI)**

- No wallpaper change (this is what caused the flashes).
- No color-temperature / blue-light feature — Windows Night Light handles that
  natively and is already schedulable.
- No sunset/sunrise computation (fixed times only).
- No Windows Scheduled Tasks.
- No administrator rights / elevation.

## Tech stack

- .NET 8, target `net8.0-windows`.
- WinForms: `NotifyIcon` for the tray, a small `Form` for settings.
- Entirely user-level: `HKCU` registry, message broadcast, `HKCU\…\Run`
  autostart. No admin required.

## Architecture / components

Each component has one purpose and a clear interface; the scheduling logic and
config are pure/testable, the UI is a thin assembler.

1. **`ThemeSwitcher`** — the core action. Has no UI dependency.
   - `Apply(ThemeMode mode)`: writes the two registry DWORDs, then broadcasts
     the settings-change message.
   - `ThemeMode GetCurrent()`: reads the current registry state.

2. **`Schedule`** — pure scheduling logic.
   - `ThemeMode ModeFor(TimeOnly now, TimeOnly darkTime, TimeOnly lightTime)`:
     returns the mode that *should* be active at `now`, correctly handling the
     wrap-around midnight case (e.g. dark 19:00 → light 07:00).

3. **`Config`** — settings model and persistence.
   - Fields: `DarkTime`, `LightTime`, `Auto`, `RunAtStartup`.
   - `Load()` / `Save()` to `%AppData%\Gloam\config.json`.
   - Defaults: dark **19:00**, light **07:00**, `Auto = true`,
     `RunAtStartup = true`.

4. **`Startup`** — autostart management.
   - `Enable()` / `Disable()` add/remove the `HKCU\…\Run` value "Gloam"
     pointing at the executable path.
   - `IsEnabled()` reports current state.

5. **`TrayApp`** — the UI assembler.
   - Owns the `NotifyIcon`, context menu, settings form, and a ~30 s timer.
   - On Auto, each tick computes `Schedule.ModeFor(now)` and calls
     `ThemeSwitcher.Apply` only when the desired mode differs from the applied
     one. Updates the tray icon to match the active mode.

## Behavior

**Auto / Manual.** The menu has an **Auto** item (checked by default). While
Auto is on, the timer drives the theme. Choosing **Light** or **Dark** manually
turns Auto off and pins that mode. Re-enabling **Auto** immediately re-applies
the mode for the current time.

**Catch-up.** On launch (and on resume from sleep / system clock change), the
app recomputes the correct mode from the current local time and applies it, so
booting in the evening goes dark right away.

## Theme-switch mechanism (detail)

Registry path: `HKCU\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize`

- `AppsUseLightTheme` (DWORD): `0` = dark apps, `1` = light apps.
- `SystemUsesLightTheme` (DWORD): `0` = dark taskbar/system, `1` = light.

After writing both values, broadcast the change so running apps repaint:

```
SendMessageTimeout(HWND_BROADCAST, WM_SETTINGCHANGE /*0x001A*/, 0,
                   "ImmersiveColorSet", SMTO_ABORTIFHUNG, 100, out _);
```

The short timeout (100 ms) with `SMTO_ABORTIFHUNG` ensures an unresponsive
window cannot hang the switch. No admin rights are needed for any of this.

## Edge cases / error handling

- Registry write failure → show a tray balloon notification; never crash.
- Broadcast → use `SendMessageTimeout` with a short timeout (above).
- `DarkTime == LightTime` → rejected in the settings form with a validation
  message (an empty window would be ambiguous).
- Sleep/resume and clock changes → the timer recomputes from current local time
  each tick and self-corrects; optionally hook `SystemEvents` to apply
  immediately on resume.
- Single instance → a named mutex prevents a second tray icon.

## Testing (xUnit)

- `Schedule.ModeFor`: midday → light, evening → dark, wrap-around midnight,
  exact boundaries, and the `DarkTime == LightTime` case.
- `Config`: load/save round-trip, defaults when the file is missing.
- `ThemeSwitcher` is kept thin; the broadcast is verified manually.

## Repository layout

```
C:\Users\jimmy\Documents\GitHub\Gloam\
  Gloam.sln
  src/Gloam/            Program.cs, TrayApp, ThemeSwitcher, Schedule, Config, Startup
  tests/Gloam.Tests/    Schedule tests, Config tests
  docs/                 this design doc
  .gitignore            (.NET / Visual Studio)
  README.md
  LICENSE               MIT
```

Version control: Git / GitHub (standalone tool, not part of the Perforce game
project). All artifacts in English.

## Future (explicitly deferred)

- Sunset/sunrise scheduling based on location.
- Night Light integration.
- A scheduled-task fallback so switching works while the app is closed.
