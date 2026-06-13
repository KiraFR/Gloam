# Gloam

A tiny Windows system-tray app that switches the light/dark theme at fixed,
configurable times — instantly and **without the screen flash** caused by
full-theme switchers.

It only flips the two Windows theme registry values and broadcasts the standard
`WM_SETTINGCHANGE` message (exactly what Windows does in Settings). No wallpaper
change, no admin rights.

## Install

Download the latest **`Gloam-win-Setup.exe`** from the
[Releases page](https://github.com/KiraFR/Gloam/releases) and run it. It installs
per-user (no admin) and pulls in the .NET 8 Desktop Runtime if you don't have it.

Gloam updates itself: when a new release is published it downloads in the
background and the tray menu shows **Restart to update** — click it whenever you
like.

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
