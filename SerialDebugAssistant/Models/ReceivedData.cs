using System;

namespace SerialDebugAssistant.Models;

public enum DataDirection
{
    Received,
    Sent
}

public class ReceivedData
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public DataDirection Direction { get; init; }
    public byte[] RawBytes { get; init; } = Array.Empty<byte>();
    public string DisplayText { get; init; } = string.Empty;
}
