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

    [ObservableProperty] private bool _isSerialConnected;
    [ObservableProperty] private string _connectionStatus = "请先在“串口参数”页打开串口。";
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

    public ObservableCollection<string> UpgradeLogs { get; } = new();
    public bool CanStartUpgrade => IsFirmwareValid && IsSerialConnected && !IsUpgrading;
    public bool CanStopUpgrade => IsUpgrading;

    public OtaViewModel(ISerialService serial)
    {
        _serial = serial;
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
        _upgradeCancellation?.Cancel();
    }
}
