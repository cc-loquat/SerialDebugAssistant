using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
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
    private readonly StringBuilder _responseBuffer = new();
    private CancellationTokenSource? _upgradeCancellation;
    private TaskCompletionSource<string>? _responseWaiter;
    private Func<string, bool>? _expectedResponse;
    private bool _isReconnecting;

    [ObservableProperty] private string _selectedPort = string.Empty;
    [ObservableProperty] private string _firmwarePath = string.Empty;
    [ObservableProperty] private string _firmwareName = "尚未选择固件";
    [ObservableProperty] private string _firmwareSize = "请选择 .bin 文件";
    [ObservableProperty] private string _firmwareCrc32 = "CRC32: --";
    [ObservableProperty] private string _stageText = "等待开始";
    [ObservableProperty] private string _statusDetail = "选择固件后，按开发板复位键并开始升级。";
    [ObservableProperty] private int _progress;
    [ObservableProperty] private string _progressText = "0%";
    [ObservableProperty] private string _transferDetail = "尚未开始传输";
    [ObservableProperty] private bool _isUpgrading;
    [ObservableProperty] private bool _isSuccess;
    [ObservableProperty] private bool _isFailure;
    [ObservableProperty] private bool _isFirmwareValid;

    public ObservableCollection<string> AvailablePorts { get; } = new();
    public ObservableCollection<string> UpgradeLogs { get; } = new();
    public bool CanStartUpgrade => IsFirmwareValid && !IsUpgrading && !string.IsNullOrWhiteSpace(SelectedPort);
    public bool CanStopUpgrade => IsUpgrading;

    public OtaViewModel(ISerialService serial)
    {
        _serial = serial;
        _serial.DataReceived += OnDataReceived;
        _serial.ConnectionChanged += OnConnectionChanged;
        RefreshPorts();
    }

    partial void OnSelectedPortChanged(string value) => StartUpgradeCommand.NotifyCanExecuteChanged();
    partial void OnIsUpgradingChanged(bool value)
    {
        StartUpgradeCommand.NotifyCanExecuteChanged();
        StopUpgradeCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsFirmwareValidChanged(bool value) => StartUpgradeCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private void RefreshPorts()
    {
        var selected = SelectedPort;
        AvailablePorts.Clear();
        foreach (var port in _serial.GetAvailablePorts()) AvailablePorts.Add(port);
        SelectedPort = AvailablePorts.Contains(selected) ? selected : (AvailablePorts.Count > 0 ? AvailablePorts[0] : string.Empty);
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
        StatusDetail = "固件已就绪。开始后请在 3 秒内按下开发板复位键。";
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
            _isReconnecting = true;
            if (_serial.IsOpen) await _serial.CloseAsync();
            var opened = await _serial.OpenAsync(new SerialPortConfig
            {
                PortName = SelectedPort,
                BaudRate = 115200,
                DataBits = 8,
                Parity = Parity.None,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                ReadTimeout = 1000,
                WriteTimeout = 3000
            });
            _isReconnecting = false;
            if (!opened) throw new InvalidOperationException($"无法打开 {SelectedPort}。");

            AddLog($"已打开 {SelectedPort}，115200 8N1");
            StageText = "等待 Bootloader";
            StatusDetail = "请在 3 秒内按下开发板复位键，正在发送进入升级模式指令。";
            await SendUntilResponseAsync(new byte[] { (byte)'u' }, text => text.Contains("Upgrade mode", StringComparison.OrdinalIgnoreCase), TimeSpan.FromSeconds(3), token);

            StageText = "正在擦除 App";
            StatusDetail = "设备已进入 Upgrade mode，正在擦除当前 App。";
            AddLog("已进入 Upgrade mode");
            await SendAndWaitAsync(new byte[] { (byte)'e' }, text => text.Contains("Erase complete", StringComparison.OrdinalIgnoreCase), TimeSpan.FromSeconds(20), token);
            AddLog("擦除完成，准备发送固件。");

            StageText = "正在传输固件";
            var crc = CalculateCrc32(firmware);
            var header = Encoding.ASCII.GetBytes("FWUP");
            await _serial.SendAsync(header);
            await _serial.SendAsync(BitConverter.GetBytes(firmware.Length));
            await _serial.SendAsync(BitConverter.GetBytes(crc));
            AddLog($"已发送 FWUP 头，长度 {firmware.Length:N0}，CRC32 {crc:X8}");

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

            StageText = "正在校验固件";
            StatusDetail = "固件传输完成，正在等待设备校验结果。";
            var result = await WaitForResponseAsync(text =>
                text.Contains("Firmware OK", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("Firmware failed", StringComparison.OrdinalIgnoreCase) ||
                text.Contains("CRC error", StringComparison.OrdinalIgnoreCase), TimeSpan.FromSeconds(45), token);

            if (result.Contains("Firmware OK", StringComparison.OrdinalIgnoreCase))
            {
                StageText = "升级完成";
                StatusDetail = "升级成功，设备正在重启。";
                TransferDetail = $"已完成 {firmware.Length / 1024d:F1} KB 传输";
                Progress = 100;
                ProgressText = "100%";
                IsSuccess = true;
                AddLog("Firmware OK. Resetting...");
            }
            else throw new InvalidOperationException("设备返回 Firmware failed 或 CRC error。");
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
            _isReconnecting = false;
            _responseWaiter = null;
            _expectedResponse = null;
            IsUpgrading = false;
            _upgradeCancellation?.Dispose();
            _upgradeCancellation = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanStopUpgrade))]
    private void StopUpgrade() => _upgradeCancellation?.Cancel();

    [RelayCommand]
    private void ClearLogs() => UpgradeLogs.Clear();

    private async Task SendAndWaitAsync(byte[] data, Func<string, bool> expected, TimeSpan timeout, CancellationToken token)
    {
        var wait = BeginWait(expected, token);
        await _serial.SendAsync(data);
        await WaitWithTimeoutAsync(wait, timeout, token);
    }

    private async Task SendUntilResponseAsync(byte[] data, Func<string, bool> expected, TimeSpan timeout, CancellationToken token)
    {
        var wait = BeginWait(expected, token);
        var deadline = DateTime.UtcNow + timeout;
        while (!wait.Task.IsCompleted && DateTime.UtcNow < deadline)
        {
            await _serial.SendAsync(data);
            await Task.Delay(250, token);
        }
        await WaitWithTimeoutAsync(wait, TimeSpan.Zero, token);
    }

    private Task<string> WaitForResponseAsync(Func<string, bool> expected, TimeSpan timeout, CancellationToken token) => WaitWithTimeoutAsync(BeginWait(expected, token), timeout, token);

    private TaskCompletionSource<string> BeginWait(Func<string, bool> expected, CancellationToken token)
    {
        _responseBuffer.Clear();
        _expectedResponse = expected;
        _responseWaiter = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        token.Register(() => _responseWaiter.TrySetCanceled(token));
        return _responseWaiter;
    }

    private static async Task<string> WaitWithTimeoutAsync(TaskCompletionSource<string> waiter, TimeSpan timeout, CancellationToken token)
    {
        if (timeout == TimeSpan.Zero)
        {
            if (!waiter.Task.IsCompleted) throw new TimeoutException("未收到 Upgrade mode，请确认复位时机和串口连接。");
            return await waiter.Task;
        }
        var completed = await Task.WhenAny(waiter.Task, Task.Delay(timeout, token));
        if (completed != waiter.Task)
        {
            token.ThrowIfCancellationRequested();
            throw new TimeoutException("设备未在规定时间内返回预期响应。");
        }
        return await waiter.Task;
    }

    private void OnDataReceived(object? sender, SerialDebugAssistant.Services.DataReceivedEventArgs e)
    {
        if (!IsUpgrading) return;
        var text = Encoding.ASCII.GetString(e.Data);
        Application.Current.Dispatcher.Invoke(() =>
        {
            _responseBuffer.Append(text);
            if (_responseBuffer.Length > 4096) _responseBuffer.Remove(0, _responseBuffer.Length - 4096);
            var response = _responseBuffer.ToString();
            if (_expectedResponse?.Invoke(response) == true) _responseWaiter?.TrySetResult(response);
            if (text.Trim().Length > 0) AddLog($"设备: {text.Trim()}");
        });
    }

    private void OnConnectionChanged(object? sender, EventArgs e)
    {
        if (IsUpgrading && !_isReconnecting && !_serial.IsOpen) _upgradeCancellation?.Cancel();
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
        _serial.DataReceived -= OnDataReceived;
        _serial.ConnectionChanged -= OnConnectionChanged;
        _upgradeCancellation?.Cancel();
    }
}
