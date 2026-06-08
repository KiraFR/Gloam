using System.Windows.Forms;

namespace Gloam;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        using var mutex = new Mutex(initiallyOwned: true, "Gloam.SingleInstance", out bool isNew);
        if (!isNew) return; // another instance already owns the tray icon

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}
