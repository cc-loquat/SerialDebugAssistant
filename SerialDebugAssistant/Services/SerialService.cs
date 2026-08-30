using System;
using System.IO.Ports;
using System.Threading.Tasks;
using SerialDebugAssistant.Models;

namespace SerialDebugAssistant.Services;

public class SerialService : ISerialService
{
    private SerialPort? _port;
    private bool _disposed;

    public bool IsOpen => _port?.IsOpen ?? false;

    public event EventHandler<DataReceivedEventArgs>? DataReceived;
    public event EventHandler<SerialErrorEventArgs>? ErrorOccurred;
    public event EventHandler? ConnectionChanged;

    public string[] GetAvailablePorts() => SerialPort.GetPortNames();

    public Task<bool> OpenAsync(SerialPortConfig config)
    {
        if (IsOpen) throw new InvalidOperationException("串口已打开");
        _port = new SerialPort(
            config.PortName,
            config.BaudRate,
            config.Parity,
            config.DataBits,
            config.StopBits)
        {
            Handshake = config.Handshake,
            ReadTimeout = config.ReadTimeout,
            WriteTimeout = config.WriteTimeout,
            ReadBufferSize = 4096,
            WriteBufferSize = 4096
        };
        _port.DataReceived += OnDataReceived;
        _port.ErrorReceived += OnErrorReceived;

        try
        {
            _port.Open();
            ConnectionChanged?.Invoke(this, EventArgs.Empty);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, new SerialErrorEventArgs { Message = ex.Message });
            return Task.FromResult(false);
        }
    }

    public Task CloseAsync()
    {
        if (_port is null || !_port.IsOpen) return Task.CompletedTask;
        _port.DataReceived -= OnDataReceived;
        _port.ErrorReceived -= OnErrorReceived;
        _port.Close();
        _port.Dispose();
        _port = null;
        ConnectionChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public async Task<int> SendAsync(byte[] data)
    {
        if (!IsOpen || _port is null) throw new InvalidOperationException("串口未打开");
        await Task.Run(() =>
        {
            _port.Write(data, 0, data.Length);
            _port.BaseStream.Flush();
        });
        return data.Length;
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (_port is null || !_port.IsOpen) return;
        if (e.EventType != SerialData.Chars) return;
        var bytesToRead = _port.BytesToRead;
        if (bytesToRead <= 0) return;
        var buffer = new byte[bytesToRead];
        _port.BaseStream.Read(buffer, 0, bytesToRead);
        DataReceived?.Invoke(this, new DataReceivedEventArgs { Data = buffer });
    }

    private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        ErrorOccurred?.Invoke(this, new SerialErrorEventArgs { Message = $"串口错误: {e.EventType}" });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CloseAsync().GetAwaiter().GetResult();
    }
}
