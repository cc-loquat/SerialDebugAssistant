using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SerialDebugAssistant.Models;
using SerialDebugAssistant.Services;

namespace SerialDebugAssistant.ViewModels;

public partial class OtaViewModel : ViewModelBase, IDisposable
{
    private const int MaxFirmwareSize = 0xE0000;
    private readonly ISerialService _serial;
    private CancellationTokenSource? _upgradeCancellation;
    private readonly object _ymodemLock = new();
    private TaskCompletionSource<byte>? _ymodemSignal;
    private readonly List<byte> _fwp2ResponseBuffer = new();
    private TaskCompletionSource<byte[]>? _fwp2ResponseWaiter;

    [ObservableProperty] private bool _isSerialConnected;
    [ObservableProperty] private string _connectionStatus = "请先在“串口参数”页打开串口。";
    [ObservableProperty] private string _firmwarePath = string.Empty;
    [ObservableProperty] private string _firmwareName = "尚未选择固件";
    [ObservableProperty] private string _firmwareSize = "请选择 .bin 文件";
    [ObservableProperty] private string _firmwareCrc32 = "CRC32: --";
    [ObservableProperty] private string _firmwareVersion = "1";
    [ObservableProperty] private string _stageText = "等待开始";
    [ObservableProperty] private string _statusDetail = "选择固件后，按开发板复位键并开始升级。";
    [ObservableProperty] private int _progress;
    [ObservableProperty] private string _progressText = "0%";
    [ObservableProperty] private string _transferDetail = "尚未开始传输";
    [ObservableProperty] private bool _isUpgrading;
    [ObservableProperty] private bool _isSuccess;
    [ObservableProperty] private bool _isFailure;
    [ObservableProperty] private bool _isFirmwareValid;
    [ObservableProperty] private string _selectedProtocol = "FWP3";

    public ObservableCollection<string> UpgradeLogs { get; } = new();
    public IReadOnlyList<string> ProtocolOptions { get; } = new[] { "FWP3", "YModem-1K" };
    public bool CanStartUpgrade => IsFirmwareValid && IsSerialConnected && !IsUpgrading;
    public bool CanStopUpgrade => IsUpgrading;

    public OtaViewModel(ISerialService serial)
    {
        _serial = serial;
        _serial.DataReceived += OnSerialDataReceived;
        _serial.ConnectionChanged += OnConnectionChanged;
        UpdateConnectionState();
    }

    partial void OnIsSerialConnectedChanged(bool value) => StartUpgradeCommand.NotifyCanExecuteChanged();
    partial void OnIsUpgradingChanged(bool value)
    {
        StartUpgradeCommand.NotifyCanExecuteChanged();
        StopUpgradeCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsFirmwareValidChanged(bool value) => StartUpgradeCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private void ChooseFirmware()
    {
        var dialog = new OpenFileDialog { Filter = "固件文件 (*.bin)|*.bin", Multiselect = false };
        if (dialog.ShowDialog() != true) return;

        var info = new FileInfo(dialog.FileName);
        if (info.Length <= 0 || info.Length > MaxFirmwareSize)
        {
            FirmwarePath = string.Empty;
            FirmwareName = "固件文件不可用";
            FirmwareSize = $"文件大小必须大于 0 且不超过 {MaxFirmwareSize:N0} 字节";
            FirmwareCrc32 = "CRC32: --";
            IsFirmwareValid = false;
            AddLog("文件大小不符合 Bootloader 限制。", true);
            return;
        }

        var bytes = File.ReadAllBytes(dialog.FileName);
        FirmwarePath = dialog.FileName;
        FirmwareName = info.Name;
        FirmwareSize = $"{info.Length:N0} 字节 ({info.Length / 1024d:F1} KB)";
        FirmwareCrc32 = $"CRC32: {CalculateCrc32(bytes):X8}";
        IsFirmwareValid = true;
        IsFailure = false;
        StatusDetail = "固件已就绪。请先在串口参数页让设备进入升级模式。";
        AddLog($"已选择固件: {info.Name}，CRC32 {CalculateCrc32(bytes):X8}");
    }

    [RelayCommand(CanExecute = nameof(CanStartUpgrade))]
    private async Task StartUpgradeAsync()
    {
        if (!IsFirmwareValid) return;
        var firmware = await File.ReadAllBytesAsync(FirmwarePath);
        _upgradeCancellation = new CancellationTokenSource();
        var token = _upgradeCancellation.Token;
        IsUpgrading = true;
        IsSuccess = false;
        IsFailure = false;
        Progress = 0;
        ProgressText = "0%";
        UpgradeLogs.Clear();

        try
        {
            if (!_serial.IsOpen) throw new InvalidOperationException("串口未打开，请先在“串口参数”页连接设备。");
            StageText = "正在传输固件";
            if (SelectedProtocol == "FWP3")
                await SendFwp3Async(firmware, token);
            else if (SelectedProtocol == "YModem-1K")
                await SendYModemAsync(firmware, infoName: Path.GetFileName(FirmwarePath), token);
            else if (SelectedProtocol == "自定义 FWUP")
            {
                var crc = CalculateCrc32(firmware);
                await _serial.SendAsync(Encoding.ASCII.GetBytes("FWUP"));
                await _serial.SendAsync(BitConverter.GetBytes(firmware.Length));
                await _serial.SendAsync(BitConverter.GetBytes(crc));
                AddLog($"已发送 FWUP 头，长度 {firmware.Length:N0}，CRC32 {crc:X8}");
            }

            if (SelectedProtocol != "自定义 FWUP")
            {
                StageText = "固件已发送";
                StatusDetail = "发送完成";
                Progress = 100;
                ProgressText = "100%";
                IsSuccess = true;
                return;
            }
            var stopwatch = Stopwatch.StartNew();
            const int chunkSize = 512;
            for (var offset = 0; offset < firmware.Length; offset += chunkSize)
            {
                token.ThrowIfCancellationRequested();
                var count = Math.Min(chunkSize, firmware.Length - offset);
                var chunk = new byte[count];
                Buffer.BlockCopy(firmware, offset, chunk, 0, count);
                await _serial.SendAsync(chunk);
                var sent = offset + count;
                Progress = (int)Math.Round(sent * 100d / firmware.Length);
                ProgressText = $"{Progress}%";
                var seconds = Math.Max(stopwatch.Elapsed.TotalSeconds, 0.1);
                var speed = sent / 1024d / seconds;
                var remaining = speed > 0 ? (firmware.Length - sent) / 1024d / speed : 0;
                TransferDetail = $"已传输 {sent / 1024d:F1} / {firmware.Length / 1024d:F1} KB  ·  {speed:F1} KB/s  ·  剩余约 {Math.Ceiling(remaining)} 秒";
            }

            StageText = "固件已发送";
            StatusDetail = "发送完成";
            TransferDetail = $"已发送 {firmware.Length / 1024d:F1} KB 固件数据";
            Progress = 100;
            ProgressText = "100%";
            IsSuccess = true;
            AddLog("固件数据发送完成。");
        }
        catch (OperationCanceledException)
        {
            StageText = "已停止";
            StatusDetail = "升级已停止。设备上的 App 可能已被擦除，请重新升级。";
            AddLog("用户停止了升级。", true);
        }
        catch (Exception ex)
        {
            StageText = "升级失败";
            StatusDetail = ex.Message;
            IsFailure = true;
            AddLog($"失败: {ex.Message}", true);
        }
        finally
        {
            IsUpgrading = false;
            _upgradeCancellation?.Dispose();
            _upgradeCancellation = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStopUpgrade))]
    private void StopUpgrade() => _upgradeCancellation?.Cancel();

    [RelayCommand]
    private void ClearLogs() => UpgradeLogs.Clear();

    private void OnConnectionChanged(object? sender, EventArgs e)
    {
        Application.Current.Dispatcher.Invoke(UpdateConnectionState);
        if (IsUpgrading && !_serial.IsOpen) _upgradeCancellation?.Cancel();
    }

    private void OnSerialDataReceived(object? sender, SerialDebugAssistant.Services.DataReceivedEventArgs e)
    {
        if (IsUpgrading && SelectedProtocol == "YModem-1K") OnYModemData(e.Data);
        if (IsUpgrading && SelectedProtocol == "FWP3")
        {
            lock (_fwp2ResponseBuffer)
            {
                _fwp2ResponseBuffer.AddRange(e.Data);
                if (_fwp2ResponseWaiter is { } waiter && _fwp2ResponseBuffer.Count >= 3)
                {
                    var response = _fwp2ResponseBuffer.Take(3).ToArray();
                    _fwp2ResponseBuffer.RemoveRange(0, 3);
                    waiter.TrySetResult(response);
                }
            }
        }
    }

    private async Task SendFwp3Async(byte[] firmware, CancellationToken token)
    {
        StageText = "正在传输固件";
        await _serial.SendAsync(Encoding.ASCII.GetBytes("FWP3"));
        if (!uint.TryParse(FirmwareVersion, out var version)) throw new InvalidOperationException("版本号必须是 0 到 4294967295 的整数。");
        await _serial.SendAsync(BitConverter.GetBytes(version));
        await _serial.SendAsync(BitConverter.GetBytes(firmware.Length));
        await _serial.SendAsync(BitConverter.GetBytes(CalculateCrc32(firmware)));
        const int chunkSize = 256;
        var total = (firmware.Length + chunkSize - 1) / chunkSize;
        for (var sequence = 0; sequence < total; sequence++)
        {
            token.ThrowIfCancellationRequested();
            var offset = sequence * chunkSize;
            var count = Math.Min(chunkSize, firmware.Length - offset);
            var packet = new byte[8 + count];
            BitConverter.GetBytes((ushort)sequence).CopyTo(packet, 0);
            BitConverter.GetBytes((ushort)count).CopyTo(packet, 2);
            Buffer.BlockCopy(firmware, offset, packet, 4, count);
            BitConverter.GetBytes(CalculateCrc32(firmware.AsSpan(offset, count).ToArray())).CopyTo(packet, 4 + count);
            while (true)
            {
                token.ThrowIfCancellationRequested();
                await _serial.SendAsync(packet);
                var response = await WaitFwp2ResponseAsync(TimeSpan.FromSeconds(5), token);
                var responseSequence = (ushort)(response[1] | (response[2] << 8));
                if (response[0] == 0x06 && responseSequence == sequence) break;
                if (response[0] == 0x15 && responseSequence == sequence) continue;
                throw new InvalidOperationException($"FWP3 收到无效响应 0x{response[0]:X2}，序号 {responseSequence}。");
            }
            var sent = offset + count;
            Progress = (int)(sent * 100L / firmware.Length);
            ProgressText = $"{Progress}%";
            TransferDetail = $"已传输 {sent / 1024d:F1} / {firmware.Length / 1024d:F1} KB";
        }
        AddLog($"FWP3 发送完成，共 {total} 包。");
    }

    private async Task<byte[]> WaitFwp2ResponseAsync(TimeSpan timeout, CancellationToken token)
    {
        lock (_fwp2ResponseBuffer) _fwp2ResponseWaiter = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
        var waiter = _fwp2ResponseWaiter;
        using var registration = token.Register(() => waiter.TrySetCanceled(token));
        var completed = await Task.WhenAny(waiter.Task, Task.Delay(timeout, token));
        if (completed != waiter.Task) { token.ThrowIfCancellationRequested(); throw new TimeoutException("FWP3 等待设备响应超时。"); }
        var result = await waiter.Task;
        lock (_fwp2ResponseBuffer) _fwp2ResponseWaiter = null;
        return result;
    }

    private async Task SendYModemAsync(byte[] firmware, string infoName, CancellationToken token)
    {
        StageText = "等待 YModem";
        StatusDetail = "等待设备发送 C。";
        await WaitYModemByteAsync(0x43, TimeSpan.FromSeconds(15), token);
        await SendYModemPacketAsync(0, BuildHeader(infoName, firmware.Length), token);
        await WaitYModemByteAsync(0x06, TimeSpan.FromSeconds(5), token);
        await WaitYModemByteAsync(0x43, TimeSpan.FromSeconds(5), token);

        StageText = "正在传输固件";
        const int chunkSize = 1024;
        byte packet = 1;
        for (var offset = 0; offset < firmware.Length; offset += chunkSize, packet++)
        {
            token.ThrowIfCancellationRequested();
            var data = new byte[chunkSize];
            Array.Fill(data, (byte)0x1A);
            Buffer.BlockCopy(firmware, offset, data, 0, Math.Min(chunkSize, firmware.Length - offset));
            await SendYModemPacketAsync(packet, data, token);
            await WaitYModemByteAsync(0x06, TimeSpan.FromSeconds(5), token);
            var sent = Math.Min(offset + chunkSize, firmware.Length);
            Progress = (int)(sent * 100L / firmware.Length);
            ProgressText = $"{Progress}%";
            TransferDetail = $"已传输 {sent / 1024d:F1} / {firmware.Length / 1024d:F1} KB";
        }
        await _serial.SendAsync(new byte[] { 0x04 });
        await WaitYModemByteAsync(0x15, TimeSpan.FromSeconds(5), token);
        await _serial.SendAsync(new byte[] { 0x04 });
        await WaitYModemByteAsync(0x06, TimeSpan.FromSeconds(5), token);
        await SendYModemPacketAsync(0, new byte[128], token, soh: true);
        await WaitYModemByteAsync(0x06, TimeSpan.FromSeconds(5), token);
        AddLog("YModem-1K 发送完成。");
    }

    private async Task SendYModemPacketAsync(byte number, byte[] data, CancellationToken token, bool soh = false)
    {
        var packet = new byte[(soh ? 128 : 1024) + 5];
        packet[0] = soh ? (byte)0x01 : (byte)0x02;
        packet[1] = number;
        packet[2] = (byte)~number;
        Array.Copy(data, 0, packet, 3, Math.Min(data.Length, packet.Length - 5));
        var crc = CalculateCrc16(packet, 1, packet.Length - 3);
        packet[^2] = (byte)(crc >> 8);
        packet[^1] = (byte)crc;
        await _serial.SendAsync(packet);
    }

    private Task<byte> WaitYModemByteAsync(byte expected, TimeSpan timeout, CancellationToken token)
    {
        _ymodemSignal = new TaskCompletionSource<byte>(TaskCreationOptions.RunContinuationsAsynchronously);
        token.Register(() => _ymodemSignal.TrySetCanceled(token));
        return WaitSignalAsync(expected, timeout, token);
    }

    private async Task<byte> WaitSignalAsync(byte expected, TimeSpan timeout, CancellationToken token)
    {
        var signal = _ymodemSignal!;
        var completed = await Task.WhenAny(signal.Task, Task.Delay(timeout, token));
        if (completed != signal.Task) { token.ThrowIfCancellationRequested(); throw new TimeoutException($"YModem 等待 0x{expected:X2} 超时。"); }
        var value = await signal.Task;
        if (value != expected) throw new InvalidOperationException($"YModem 收到 0x{value:X2}，预期 0x{expected:X2}。");
        return value;
    }

    private void OnYModemData(byte[] data)
    {
        foreach (var value in data)
            if (_ymodemSignal is { } signal && !signal.Task.IsCompleted) signal.TrySetResult(value);
    }

    private static byte[] BuildHeader(string name, int length)
    {
        var bytes = new byte[1024];
        var text = Encoding.ASCII.GetBytes($"{name}\0{length}\0");
        Array.Copy(text, bytes, Math.Min(text.Length, bytes.Length));
        return bytes;
    }

    private static ushort CalculateCrc16(byte[] data, int offset, int length)
    {
        ushort crc = 0;
        for (var i = offset; i < offset + length; i++)
        {
            crc ^= (ushort)(data[i] << 8);
            for (var bit = 0; bit < 8; bit++) crc = (ushort)((crc & 0x8000) != 0 ? (crc << 1) ^ 0x1021 : crc << 1);
        }
        return crc;
    }

    private void UpdateConnectionState()
    {
        IsSerialConnected = _serial.IsOpen;
        ConnectionStatus = IsSerialConnected
            ? "串口已连接。请先通过“串口参数”页完成设备的升级准备。"
            : "请先在“串口参数”页打开串口。";
    }

    private void AddLog(string message, bool isError = false)
    {
        UpgradeLogs.Add($"{DateTime.Now:HH:mm:ss}  {(isError ? "! " : string.Empty)}{message}");
        while (UpgradeLogs.Count > 300) UpgradeLogs.RemoveAt(0);
    }

    private static uint CalculateCrc32(byte[] data)
    {
        uint crc = 0xFFFFFFFF;
        foreach (var value in data)
        {
            crc ^= value;
            for (var i = 0; i < 8; i++) crc = (crc & 1) != 0 ? (crc >> 1) ^ 0xEDB88320 : crc >> 1;
        }
        return ~crc;
    }

    public void Dispose()
    {
        _serial.ConnectionChanged -= OnConnectionChanged;
        _serial.DataReceived -= OnSerialDataReceived;
        _upgradeCancellation?.Cancel();
    }
}
