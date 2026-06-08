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