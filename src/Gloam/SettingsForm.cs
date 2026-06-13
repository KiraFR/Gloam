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
