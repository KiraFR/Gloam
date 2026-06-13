using System.Windows.Forms;
using Velopack;

namespace Gloam;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        // Must run before anything else: handles Velopack install/update hooks
        // and may exit the process during those.
        VelopackApp.Build().Run();

        using var mutex = new Mutex(initiallyOwned: true, "Gloam.SingleInstance", out bool isNew);
        if (!isNew) return; // another instance already owns the tray icon

        ApplicationConfiguration.Initialize();
        Application.Run(new TrayApp());
    }
}
