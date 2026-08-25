using System;
using System.IO;
using System.Threading.Tasks;
using SerialDebugAssistant.Models;
using SerialDebugAssistant.Utils;

namespace SerialDebugAssistant.Services;

public class LogService
{
    private readonly LogSettings _settings;

    public LogService(LogSettings settings)
    {
        _settings = settings;
        Directory.CreateDirectory(settings.LogDirectory);
    }

    public async Task AppendAsync(ReceivedData data)
    {
        if (!_settings.AutoSave) return;
        var path = _settings.GetDailyLogFile(data.Timestamp);
        var line = _settings.IncludeTimestamp
            ? $"[{data.Timestamp:HH:mm:ss.fff}] {GetDisplayText(data)}{Environment.NewLine}"
            : $"{GetDisplayText(data)}{Environment.NewLine}";
        await File.AppendAllTextAsync(path, line);
    }

    private string GetDisplayText(ReceivedData data)
    {
        return _settings.UseHexFormat
            ? HexConverter.BytesToHexString(data.RawBytes)
            : HexConverter.BytesToAscii(data.RawBytes);
    }
}
