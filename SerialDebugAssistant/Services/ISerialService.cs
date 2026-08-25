using System;
using System.Threading.Tasks;
using SerialDebugAssistant.Models;

namespace SerialDebugAssistant.Services;

public interface ISerialService : IDisposable
{
    bool IsOpen { get; }
    event EventHandler<DataReceivedEventArgs>? DataReceived;
    event EventHandler<SerialErrorEventArgs>? ErrorOccurred;
    event EventHandler? ConnectionChanged;

    string[] GetAvailablePorts();
    Task<bool> OpenAsync(SerialPortConfig config);
    Task CloseAsync();
    Task<int> SendAsync(byte[] data);
}

public class DataReceivedEventArgs : EventArgs
{
    public byte[] Data { get; init; } = Array.Empty<byte>();
    public DateTime Timestamp { get; init; } = DateTime.Now;
}

public class SerialErrorEventArgs : EventArgs
{
    public string Message { get; init; } = string.Empty;
}
