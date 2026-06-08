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
