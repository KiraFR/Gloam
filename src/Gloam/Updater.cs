using Velopack;
using Velopack.Sources;

namespace Gloam;

/// <summary>
/// Wraps Velopack auto-update. Every operation is a no-op unless the app was
/// installed via Velopack, and all failures (offline, GitHub errors) are
/// swallowed — updates are best-effort and never disrupt the app.
/// </summary>
public sealed class Updater
{
    private readonly UpdateManager _manager;
    private readonly Action<string> _onUpdateReady;
    private UpdateInfo? _pending;

    public Updater(Action<string> onUpdateReady)
    {
        _onUpdateReady = onUpdateReady;
        _manager = new UpdateManager(
            new GithubSource("https://github.com/KiraFR/Gloam", null, false));
    }

    /// <summary>Checks for and downloads a newer release; fires the ready callback.</summary>
    public async Task CheckAsync()
    {
        if (!_manager.IsInstalled) return;

        try
        {
            var info = await _manager.CheckForUpdatesAsync();
            if (info == null) return;

            await _manager.DownloadUpdatesAsync(info);
            _pending = info;
            _onUpdateReady(info.TargetFullRelease.Version.ToString());
        }
        catch
        {
            // best-effort: ignore network/update errors
        }
    }

    /// <summary>Applies the downloaded update and restarts the app.</summary>
    public void ApplyAndRestart()
    {
        if (_pending != null)
            _manager.ApplyUpdatesAndRestart(_pending);
    }
}
