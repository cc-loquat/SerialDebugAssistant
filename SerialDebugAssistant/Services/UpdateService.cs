using System.Threading.Tasks;
using Velopack;

namespace SerialDebugAssistant.Services;

public class UpdateService : IUpdateService
{
    private readonly UpdateManager _mgr;

    public UpdateService()
    {
        _mgr = new UpdateManager("https://github.com/cc-loquat/SerialDebugAssistant/releases/latest/download/");
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        try
        {
            var v = await _mgr.CheckForUpdatesAsync();
            if (v is null) return null;
            return new UpdateInfo
            {
                Version = v.TargetFullRelease.Version.ToString(),
                ReleaseNotes = v.TargetFullRelease.NotesMarkdown ?? string.Empty
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task DownloadAndInstallUpdateAsync()
    {
        var v = await _mgr.CheckForUpdatesAsync();
        if (v is null) return;
        await _mgr.DownloadUpdatesAsync(v);
        _mgr.ApplyUpdatesAndRestart(v);
    }
}
