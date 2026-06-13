using System.Drawing;
using System.Windows.Forms;

namespace Gloam;

public sealed class SettingsForm : Form
{
    private readonly DateTimePicker _darkPicker =
        new() { Format = DateTimePickerFormat.Time, ShowUpDown = true, Width = 90 };
    private readonly DateTimePicker _lightPicker =
        new() { Format = DateTimePickerFormat.Time, ShowUpDown = true, Width = 90 };
    private readonly CheckBox _startupCheck =
        new() { Text = "Start with Windows", AutoSize = true };

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
        AutoScaleMode = AutoScaleMode.Dpi;

        // The window sizes itself to its content, so the buttons are never
        // clipped regardless of display DPI / scaling.
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(12);

        var today = DateTime.Today;
        _darkPicker.Value = today + config.DarkTime.ToTimeSpan();
        _lightPicker.Value = today + config.LightTime.ToTimeSpan();
        _startupCheck.Checked = config.RunAtStartup;

        var darkLabel = new Label
        {
            Text = "Go dark at:", AutoSize = true,
            Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 12, 6)
        };
        var lightLabel = new Label
        {
            Text = "Go light at:", AutoSize = true,
            Anchor = AnchorStyles.Left, Margin = new Padding(0, 6, 12, 6)
        };

        var ok = new Button
        {
            Text = "OK", DialogResult = DialogResult.OK,
            AutoSize = true, Margin = new Padding(6, 0, 0, 0)
        };
        var cancel = new Button
        {
            Text = "Cancel", DialogResult = DialogResult.Cancel,
            AutoSize = true, Margin = new Padding(6, 0, 0, 0)
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

        // Right-aligned [OK] [Cancel]: in a right-to-left flow the first added
        // control sits furthest right, so add Cancel first then OK.
        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            Dock = DockStyle.Fill,
            Margin = new Padding(0, 10, 0, 0)
        };
        buttons.Controls.Add(cancel);
        buttons.Controls.Add(ok);

        var layout = new TableLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 2,
            RowCount = 4,
            Dock = DockStyle.Fill
        };
        layout.Controls.Add(darkLabel, 0, 0);
        layout.Controls.Add(_darkPicker, 1, 0);
        layout.Controls.Add(lightLabel, 0, 1);
        layout.Controls.Add(_lightPicker, 1, 1);
        layout.Controls.Add(_startupCheck, 0, 2);
        layout.SetColumnSpan(_startupCheck, 2);
        layout.Controls.Add(buttons, 0, 3);
        layout.SetColumnSpan(buttons, 2);

        Controls.Add(layout);

        AcceptButton = ok;
        CancelButton = cancel;
    }
}
