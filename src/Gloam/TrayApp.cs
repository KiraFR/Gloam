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
