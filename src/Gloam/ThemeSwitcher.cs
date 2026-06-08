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
