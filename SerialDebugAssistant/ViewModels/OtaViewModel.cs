using System;
using System.Collections.ObjectModel;
using System.Buffers.Binary;
using System.Security.Cryptography;
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
    private TaskCompletionSource<byte>? _fwp4ReadyWaiter;

    [ObservableProperty] private bool _isSerialConnected;
    [ObservableProperty] private string _connectionStatus = "请先在“串口参数”页打开串口。";
    [ObservableProperty] private string _firmwarePath = string.Empty;
    [ObservableProperty] private string _firmwareName = "尚未选择固件";
    [ObservableProperty] private string _firmwareSize = "请选择 .bin 文件";
    [ObservableProperty] private string _firmwareCrc32 = "CRC32: --";
    [ObservableProperty] private string _firmwareVersion = "1";
    [ObservableProperty] private string _signatureKeyPath = string.Empty;
    [ObservableProperty] private string _signatureStatus = "未选择签名私钥";
    [ObservableProperty] private string _targetSlot = "自动（A/B）";
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
    public IReadOnlyList<string> ProtocolOptions { get; } = new[] { "FWP3", "FWP4", "YModem-1K" };
    public IReadOnlyList<string> TargetSlotOptions { get; } = new[] { "自动（A/B）", "A", "B" };
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
    private void ChooseSigningKey()
    {
        var dialog = new OpenFileDialog { Filter = "PEM 私钥 (*.pem)|*.pem|所有文件 (*.*)|*.*" };
        if (dialog.ShowDialog() == true) { SignatureKeyPath = dialog.FileName; SignatureStatus = "已选择签名私钥"; }
    }

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
            else if (SelectedProtocol == "FWP4")
                await SendFwp4Async(firmware, token);
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
    private async Task StopUpgrade()
    {
        if (!IsUpgrading) return;
        try { await _serial.SendAsync(new byte[] { 0x18 }); } catch { }
        _upgradeCancellation?.Cancel();
    }

    [RelayCommand]
    private void ClearLogs() => UpgradeLogs.Clear();

    private void OnConnectionChanged(object? sender, EventArgs e)
    {
        Application.Current.Dispatcher.Invoke(UpdateConnectionState);
        if (IsUpgrading && !_serial.IsOpen) _upgradeCancellation?.Cancel();
    }

    private void OnSerialDataReceived(object? sender, SerialDebugAssistant.Services.DataReceivedEventArgs e)
    {
        if (IsUpgrading && SelectedProtocol == "FWP4" && _fwp4ReadyWaiter is { } ready)
            foreach (var value in e.Data) if (value == 0x06) ready.TrySetResult(value);
        if (IsUpgrading && SelectedProtocol == "YModem-1K") OnYModemData(e.Data);
        if (IsUpgrading && SelectedProtocol == "FWP3")
        {
            lock (_fwp2ResponseBuffer)
            {
                _fwp2ResponseBuffer.AddRange(e.Data);
                if (_fwp2ResponseWaiter is { } waiter && TryTakeFwp3Response(out var response))
                {
                    waiter.TrySetResult(response);
                }
            }
        }
    }

    private async Task SendFwp4Async(byte[] firmware, CancellationToken token)
    {
        if (string.IsNullOrWhiteSpace(SignatureKeyPath) || !File.Exists(SignatureKeyPath)) throw new InvalidOperationException("FWP4 必须先选择 ota_private.pem。");
        if (!uint.TryParse(FirmwareVersion, out var version) || version == 0) throw new InvalidOperationException("版本号必须是大于 0 的 uint32。");
        using var ecdsa = ECDsa.Create();
        ecdsa.ImportFromPem(await File.ReadAllTextAsync(SignatureKeyPath, token));
        var digest = SHA256.HashData(firmware);
        var der = ecdsa.SignHash(digest);
        var signature = ConvertDerSignatureToRaw(der);
        SignatureStatus = $"签名已生成（64 字节原始 r||s）";
        AddLog($"FWP4 SHA256: {ToHex(digest)}");
        AddLog($"FWP4 签名 ({signature.Length} 字节 r||s): {ToHex(signature)}");
        var header = new byte[112];
        Encoding.ASCII.GetBytes("FWP4").CopyTo(header, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), version);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8), (uint)firmware.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12), CalculateCrc32(firmware));
        digest.CopyTo(header, 16); signature.CopyTo(header, 48);
        AddLog($"FWP4 头 ({header.Length} 字节): {ToHex(header)}");
        _fwp4ReadyWaiter = new TaskCompletionSource<byte>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var reg = token.Register(() => _fwp4ReadyWaiter.TrySetCanceled(token));
        var headerWritten = await _serial.SendAsync(header);
        AddLog($"FWP4 头实际写入串口: {headerWritten}/{header.Length} 字节");
        await _fwp4ReadyWaiter.Task.WaitAsync(TimeSpan.FromSeconds(10), token);
        await SendFwp3Async(firmware, token, false, "FWP4");
    }

    private static byte[] ConvertDerSignatureToRaw(byte[] der)
    {
        if (der.Length == 64) return der;
        var offset = 0;
        if (der[offset++] != 0x30) throw new CryptographicException("无效 ECDSA 签名：缺少序列。");
        ReadDerLength(der, ref offset);
        if (der[offset++] != 0x02) throw new CryptographicException("无效 ECDSA 签名：缺少 r。");
        var rLen = ReadDerLength(der, ref offset); var r = der.AsSpan(offset, rLen); offset += rLen;
        if (der[offset++] != 0x02) throw new CryptographicException("无效 ECDSA 签名：缺少 s。");
        var sLen = ReadDerLength(der, ref offset); var s = der.AsSpan(offset, sLen);
        var raw = new byte[64]; CopyUnsignedBigEndian(r, raw.AsSpan(0, 32)); CopyUnsignedBigEndian(s, raw.AsSpan(32, 32)); return raw;
    }

    private static int ReadDerLength(byte[] data, ref int offset)
    {
        if (offset >= data.Length) throw new CryptographicException("无效 DER 长度。");
        var value = data[offset++];
        if ((value & 0x80) == 0) return value & 0x7F;
        var count = value & 0x7F; if (count is 0 or > 4 || offset + count > data.Length) throw new CryptographicException("无效 DER 长度。");
        var length = 0; for (var i = 0; i < count; i++) length = (length << 8) | data[offset++]; return length;
    }

    private static void CopyUnsignedBigEndian(ReadOnlySpan<byte> source, Span<byte> target)
    {
        while (source.Length > 0 && source[0] == 0) source = source[1..];
        if (source.Length > target.Length) throw new CryptographicException("ECDSA 分量超过 32 字节。");
        source.CopyTo(target[(target.Length - source.Length)..]);
    }

    private async Task SendFwp3Async(byte[] firmware, CancellationToken token, bool sendHeader = true, string protocolName = "FWP3")
    {
        StageText = "正在传输固件";
        if (!sendHeader) goto packets;
        if (!uint.TryParse(FirmwareVersion, out var version)) throw new InvalidOperationException("版本号必须是 0 到 4294967295 的整数。");
        var magic = Encoding.ASCII.GetBytes("FWP3");
        if (magic.Length != 4) throw new InvalidOperationException("FWP3 标识长度异常。");
        var header = new byte[16];
        Buffer.BlockCopy(magic, 0, header, 0, 4);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4, 4), version);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8, 4), (uint)firmware.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12, 4), CalculateCrc32(firmware));
        AddLog($"实际写入串口 FWP3 头 ({header.Length} 字节): {ToHex(header)}");
        await _serial.SendAsync(header);
        AddLog("等待设备 READY [06 FF FF]...");
        var readyDeadline = DateTime.UtcNow.AddSeconds(10);
        while (true)
        {
            var remaining = readyDeadline - DateTime.UtcNow;
            if (remaining <= TimeSpan.Zero) throw new TimeoutException("FWP3 等待设备 READY [06 FF FF] 超时。");
            var ready = await WaitFwp2ResponseAsync(remaining, token);
            AddLog($"响应 [{ToHex(ready)}]");
            if (ready[0] == 0x06 && ready[1] == 0xFF && ready[2] == 0xFF) break;
        }
    packets:
        const int chunkSize = 256;
        var total = (firmware.Length + chunkSize - 1) / chunkSize;
        for (var sequence = 0; sequence < total; sequence++)
        {
            token.ThrowIfCancellationRequested();
            var offset = sequence * chunkSize;
            var count = Math.Min(chunkSize, firmware.Length - offset);
            var packet = new byte[10 + count];
            packet[0] = 0xA5;
            packet[1] = 0x5A;
            BitConverter.GetBytes((ushort)sequence).CopyTo(packet, 2);
            BitConverter.GetBytes((ushort)count).CopyTo(packet, 4);
            Buffer.BlockCopy(firmware, offset, packet, 6, count);
            BitConverter.GetBytes(CalculateCrc32(firmware.AsSpan(offset, count).ToArray())).CopyTo(packet, 6 + count);
            var packetCrc = CalculateCrc32(firmware.AsSpan(offset, count).ToArray());
            AddLog($"包 {sequence}: 长度 {count}，CRC32 {packetCrc:X8}");
            var attempts = 0;
            while (attempts++ < 5)
            {
                token.ThrowIfCancellationRequested();
                await _serial.SendAsync(packet);
                byte[] response;
                try { response = await WaitFwp2ResponseAsync(TimeSpan.FromSeconds(3), token); }
                catch (TimeoutException) { AddLog($"{protocolName} 包 {sequence} 等待 ACK 超时，第 {attempts} 次重试", true); continue; }
                AddLog($"{protocolName} 响应 [{ToHex(response)}]，第 {attempts} 次发送");
                var responseSequence = (ushort)(response[1] | (response[2] << 8));
                if (response[0] == 0x06 && responseSequence == sequence) break;
                if (response[0] == 0x15 && responseSequence == sequence) continue;
                throw new InvalidOperationException($"{protocolName} 收到无效响应 0x{response[0]:X2}，序号 {responseSequence}。");
            }
            if (attempts > 5) throw new TimeoutException($"{protocolName} 第 {sequence} 包重试 5 次仍未成功。");
            var sent = offset + count;
            Progress = (int)(sent * 100L / firmware.Length);
            ProgressText = $"{Progress}%";
            TransferDetail = $"已传输 {sent / 1024d:F1} / {firmware.Length / 1024d:F1} KB";
        }
        AddLog($"{protocolName} 发送完成，共 {total} 包。");
    }

    private async Task<byte[]> WaitFwp2ResponseAsync(TimeSpan timeout, CancellationToken token)
    {
        lock (_fwp2ResponseBuffer)
        {
            _fwp2ResponseWaiter = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);
            if (TryTakeFwp3Response(out var buffered)) _fwp2ResponseWaiter.TrySetResult(buffered);
        }
        var waiter = _fwp2ResponseWaiter;
        using var registration = token.Register(() => waiter.TrySetCanceled(token));
        var completed = await Task.WhenAny(waiter.Task, Task.Delay(timeout, token));
        if (completed != waiter.Task) { token.ThrowIfCancellationRequested(); throw new TimeoutException("FWP3 等待设备响应超时。"); }
        var result = await waiter.Task;
        lock (_fwp2ResponseBuffer) _fwp2ResponseWaiter = null;
        return result;
    }

    private bool TryTakeFwp3Response(out byte[] response)
    {
        for (var index = 0; index <= _fwp2ResponseBuffer.Count - 3; index++)
        {
            var status = _fwp2ResponseBuffer[index];
            if (status is 0x06 or 0x15)
            {
                response = _fwp2ResponseBuffer.Skip(index).Take(3).ToArray();
                _fwp2ResponseBuffer.RemoveRange(0, index + 3);
                return true;
            }
        }
        response = Array.Empty<byte>();
        return false;
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

    private static string ToHex(byte[] data) => string.Join(" ", data.Select(value => value.ToString("X2")));

    public void Dispose()
    {
        _serial.ConnectionChanged -= OnConnectionChanged;
        _serial.DataReceived -= OnSerialDataReceived;
        _upgradeCancellation?.Cancel();
    }
}
