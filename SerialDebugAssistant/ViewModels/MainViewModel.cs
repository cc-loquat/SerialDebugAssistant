using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using SerialDebugAssistant.Models;
using SerialDebugAssistant.Services;
using SerialDebugAssistant.Utils;
using SerialDebugAssistant.Views.Dialogs;

namespace SerialDebugAssistant.ViewModels;

public enum AppView { Serial, Pid, Ota }

public partial class MainViewModel : ViewModelBase, IDisposable
{
    private readonly ISerialService _serial;
    private readonly LogService _logService = new(new LogSettings { AutoSave = true });
    private readonly IUpdateService _updateService = new UpdateService();
    private readonly ObservableCollection<string> _availablePorts = new();

    public event Action<string, string, string>? DataLineReceived; // (text, direction, timestamp)
    public event Action? ClearReceivedRequested;

    [ObservableProperty] private string _selectedPort = "COM1";
    [ObservableProperty] private int _baudRate = 115200;
    [ObservableProperty] private int _dataBits = 8;
    [ObservableProperty] private StopBits _stopBits = StopBits.One;
    [ObservableProperty] private Parity _parity = Parity.None;
    [ObservableProperty] private Handshake _handshake = Handshake.None;
    [ObservableProperty] private bool _isConnected;
    [ObservableProperty] private string _receivedText = string.Empty;
    [ObservableProperty] private string _sendText = string.Empty;
    [ObservableProperty] private bool _sendAsHex;
    [ObservableProperty] private bool _receiveAsHex;
    [ObservableProperty] private AppView _selectedView = AppView.Serial;
    [ObservableProperty] private string _selectedTheme = ThemeService.LoadTheme();

    partial void OnSelectedViewChanged(AppView value)
    {
        if (value == AppView.Pid)
        {
            PidViewModel.IsPausedState = false;
        }
    }

    partial void OnSelectedThemeChanged(string value)
    {
        ThemeService.Apply(value);
        StatusMessage = $"已应用主题：{value}";
    }

    public bool ReceiveAsAscii { get => !ReceiveAsHex; set => ReceiveAsHex = !value; }
    public bool SendAsAscii { get => !SendAsHex; set => SendAsHex = !value; }

    partial void OnReceiveAsHexChanged(bool value) => OnPropertyChanged(nameof(ReceiveAsAscii));
    partial void OnSendAsHexChanged(bool value) => OnPropertyChanged(nameof(SendAsAscii));

    [ObservableProperty] private long _rxByteCount;
    [ObservableProperty] private long _txByteCount;
    [ObservableProperty] private string _statusMessage = "就绪";
    [ObservableProperty] private bool _updateAvailable;
    [ObservableProperty] private string _updateVersion = string.Empty;
    private string _updateReleaseNotes = string.Empty;

    public string AppVersion => $"Comm Terminal v{System.Reflection.Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "0.1"}";
    public PidViewModel PidViewModel { get; }
    public OtaViewModel OtaViewModel { get; }

    public ObservableCollection<string> AvailablePorts => _availablePorts;
    public IReadOnlyList<string> ThemeOptions { get; } = new[] { "深色", "浅色", "Lumi", "跟随系统" };

    public string ConnectButtonText => IsConnected ? "关闭" : "打开";

    public MainViewModel(ISerialService serial)
    {
        _serial = serial;
        PidViewModel = new PidViewModel(serial);
        OtaViewModel = new OtaViewModel(serial);
        _serial.DataReceived += OnDataReceived;
        _serial.ErrorOccurred += OnErrorOccurred;
        _serial.ConnectionChanged += OnConnectionChanged;
        RefreshPorts();
        _ = CheckUpdatesOnStartupAsync();
    }

    [RelayCommand]
    public void RefreshPorts()
    {
        _availablePorts.Clear();
        foreach (var p in _serial.GetAvailablePorts())
            _availablePorts.Add(p);
        if (_availablePorts.Count > 0 && string.IsNullOrEmpty(SelectedPort))
            SelectedPort = _availablePorts[0];
    }

    [RelayCommand]
    public async Task ConnectAsync()
    {
        if (IsConnected)
        {
            await _serial.CloseAsync();
        }
        else
        {
            if (!SerialPortConfig.IsValidBaudRate(BaudRate) ||
                !SerialPortConfig.IsValidDataBits(DataBits))
            {
                StatusMessage = "参数非法";
                return;
            }
            var cfg = new SerialPortConfig
            {
                PortName = SelectedPort,
                BaudRate = BaudRate,
                DataBits = DataBits,
                StopBits = StopBits,
                Parity = Parity,
                Handshake = Handshake
            };
            var ok = await _serial.OpenAsync(cfg);
            IsConnected = ok;
            if (!ok) StatusMessage = "打开失败";
        }
    }

    [RelayCommand]
    public async Task SendAsync()
    {
        if (!IsConnected) return;
        var data = SendAsHex
            ? HexConverter.HexStringToBytes(SendText)
            : HexConverter.AsciiToBytes(SendText);
        if (data.Length == 0) return;
        await _serial.SendAsync(data);
        TxByteCount += data.Length;
        var hex = HexConverter.BytesToHexString(data);
        ReceivedText += $"[TX] {hex}\n";
        DataLineReceived?.Invoke(hex, "TX", DateTime.Now.ToString("HH:mm:ss.fff"));
        SendText = string.Empty;
    }

    [RelayCommand]
    public void ClearReceived()
    {
        ReceivedText = string.Empty;
        ClearReceivedRequested?.Invoke();
    }

    [RelayCommand]
    public void SwitchToSerial() => SelectedView = AppView.Serial;

    [RelayCommand]
    public void SwitchToPid() => SelectedView = AppView.Pid;

    [RelayCommand]
    public void SwitchToOta() => SelectedView = AppView.Ota;

    private async Task CheckUpdatesOnStartupAsync()
    {
        try
        {
            var info = await _updateService.CheckForUpdatesAsync();
            if (info != null)
            {
                UpdateAvailable = true;
                UpdateVersion = info.Version;
                _updateReleaseNotes = info.ReleaseNotes;
                StatusMessage = $"发现新版本: {info.Version}";

                // 在 UI 线程弹窗
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(async () =>
                {
                    var dialog = new UpdateDialog(info.Version, info.ReleaseNotes)
                    {
                        Owner = System.Windows.Application.Current.MainWindow
                    };
                    dialog.ShowDialog();
                    if (dialog.ShouldUpdate)
                    {
                        await _updateService.DownloadAndInstallUpdateAsync();
                    }
                });
            }
        }
        catch
        {
            // Silently fail — update check is best-effort
        }
    }

    [RelayCommand]
    public async Task ApplyUpdateAsync()
    {
        await _updateService.DownloadAndInstallUpdateAsync();
    }

    private void OnDataReceived(object? sender, DataReceivedEventArgs e)
    {
        RxByteCount += e.Data.Length;
        var text = ReceiveAsHex
            ? HexConverter.BytesToHexString(e.Data)
            : HexConverter.BytesToAscii(e.Data);
        var ts = e.Timestamp;
        var line = $"[RX {ts:HH:mm:ss.fff}] {text}\n";
        System.Windows.Application.Current?.Dispatcher.Invoke(() =>
        {
            ReceivedText += line;
            DataLineReceived?.Invoke(text, "RX", ts.ToString("HH:mm:ss.fff"));

            // Forward PID data to PidViewModel
            PidViewModel.OnDataReceived(text, ts.ToString("HH:mm:ss.fff"));
        });
        _ = _logService.AppendAsync(new ReceivedData
        {
            Timestamp = ts,
            Direction = DataDirection.Received,
            RawBytes = e.Data,
            DisplayText = text
        });
    }

    private void OnErrorOccurred(object? sender, SerialErrorEventArgs e)
    {
        StatusMessage = e.Message;
    }

    private void OnConnectionChanged(object? sender, EventArgs e)
    {
        IsConnected = _serial.IsOpen;
        OnPropertyChanged(nameof(ConnectButtonText));
        StatusMessage = IsConnected ? $"已连接 {SelectedPort}" : "已断开";
    }

    partial void OnIsConnectedChanged(bool value) => OnPropertyChanged(nameof(ConnectButtonText));

    public void Dispose()
    {
        OtaViewModel.Dispose();
        _serial?.Dispose();
        GC.SuppressFinalize(this);
    }
}
