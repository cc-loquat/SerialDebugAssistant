using System;
using System.IO;

namespace SerialDebugAssistant.Models;

public class LogSettings
{
    public bool AutoSave { get; set; } = false;
    public string LogDirectory { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                     "SerialDebugAssistant", "Logs");
    public bool UseHexFormat { get; set; } = false;
    public bool IncludeTimestamp { get; set; } = true;

    public string GetDailyLogFile(DateTime date)
    {
        var ext = UseHexFormat ? ".hex" : ".txt";
        return Path.Combine(LogDirectory, $"{date:yyyy-MM-dd}{ext}");
    }
}
