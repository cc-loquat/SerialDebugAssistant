using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using SerialDebugAssistant.Models;
using SerialDebugAssistant.Services;

namespace SerialDebugAssistant.ViewModels;

public partial class PidViewModel : ObservableObject
{
    private readonly ISerialService _serial;
    private readonly StringBuilder _rawDataBuilder = new();
    private readonly List<PidDataPoint> _allPoints = new();
    private double _firstTimestamp = -1;

    // ===== PID 参数 =====
    [ObservableProperty] private double _kp = 1.0;
    [ObservableProperty] private double _ki = 0.1;
    [ObservableProperty] private double _kd = 0.05;

    // ===== 目标值范围 =====
    [ObservableProperty] private double _targetMin = 0;
    [ObservableProperty] private double _targetMax = 100;
    [ObservableProperty] private double _targetValue = 50;

    // ===== 曲线显示控制 =====
    [ObservableProperty] private bool _showTargetLine = true;
    [ObservableProperty] private bool _showActualLine = true;
    [ObservableProperty] private bool _isPausedState = false;
    [ObservableProperty] private bool _hasData = false;

    // ===== 时间轴控制 =====
    [ObservableProperty] private double _timeRange = 10000; // 默认10秒(ms)
    [ObservableProperty] private double _timeOffset = 0;    // 起始时间偏移

    // ===== Y轴控制 =====
    [ObservableProperty] private double _yMin = 0;
    [ObservableProperty] private double _yMax = 100;
    [ObservableProperty] private bool _autoYRange = true;

    // ===== 数据解析设置 =====
    [ObservableProperty] private string _targetPrefix = "T:";
    [ObservableProperty] private string _actualPrefix = "A:";
    [ObservableProperty] private string _dataSeparator = ",";

    // ===== 数据点集合 =====
    public ObservableCollection<PidDataPoint> DataPoints { get; } = new();

    // ===== UI 绑定 =====
    public string PauseButtonText => IsPausedState ? "继续" : "暂停";
    public string YAxisMaxText => YMax.ToString("F1");
    public string YAxisMidText => ((YMin + YMax) / 2).ToString("F1");
    public string YAxisMinText => YMin.ToString("F1");

    public PidViewModel(ISerialService serial)
    {
        _serial = serial;
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsPausedState))
                OnPropertyChanged(nameof(PauseButtonText));
            if (e.PropertyName is nameof(YMin) or nameof(YMax))
            {
                OnPropertyChanged(nameof(YAxisMaxText));
                OnPropertyChanged(nameof(YAxisMidText));
                OnPropertyChanged(nameof(YAxisMinText));
            }
        };
    }

    // ===== 命令 =====

    [RelayCommand]
    private void SendAll()
    {
        var msg = $"Kp:{Kp:F4},Ki:{Ki:F4},Kd:{Kd:F4},Target:{TargetValue:F2}\r\n";
        _ = _serial.SendAsync(System.Text.Encoding.UTF8.GetBytes(msg));
    }

    [RelayCommand]
    private void SendPidParams()
    {
        var msg = $"Kp:{Kp:F4},Ki:{Ki:F4},Kd:{Kd:F4}\r\n";
        _ = _serial.SendAsync(System.Text.Encoding.UTF8.GetBytes(msg));
    }

    [RelayCommand]
    private void SendTarget()
    {
        var msg = $"Target:{TargetValue:F2}\r\n";
        _ = _serial.SendAsync(System.Text.Encoding.UTF8.GetBytes(msg));
    }

    [RelayCommand]
    private void TogglePause() => IsPausedState = !IsPausedState;

    [RelayCommand]
    private void ClearData()
    {
        _allPoints.Clear();
        DataPoints.Clear();
        _firstTimestamp = -1;
        HasData = false;
        if (AutoYRange)
        {
            YMin = TargetMin;
            YMax = TargetMax;
        }
        RequestChartUpdate?.Invoke();
    }

    [RelayCommand]
    private void ExportCsv()
    {
        if (_allPoints.Count == 0) return;

        var dlg = new SaveFileDialog
        {
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            FileName = $"pid_data_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
        };
        if (dlg.ShowDialog() != true) return;

        var sb = new StringBuilder();
        sb.AppendLine("Time(ms),Target,Actual");
        foreach (var p in _allPoints)
            sb.AppendLine($"{p.Time:F0},{p.Target:F4},{p.Actual:F4}");

        File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
    }

    // ===== 数据处理 =====

    public void OnDataReceived(string text, string timestamp)
    {
        // 追加到原始数据
        Application.Current?.Dispatcher.Invoke(() =>
        {
            _rawDataBuilder.AppendLine($"[{timestamp}] {text}");
        });

        if (IsPausedState) return;

        // 解析数据格式: T:90.0,A:88.5 或 Target:90,Actual:88.5
        var target = ExtractValue(text, TargetPrefix);
        var actual = ExtractValue(text, ActualPrefix);

        if (double.IsNaN(target) && double.IsNaN(actual)) return;

        // 获取当前时间戳(ms)
        var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        if (_firstTimestamp < 0) _firstTimestamp = now;
        var elapsed = now - _firstTimestamp;

        // 使用上次的值填充缺失的数据
        var lastTarget = _allPoints.Count > 0 ? _allPoints[^1].Target : TargetValue;
        var lastActual = _allPoints.Count > 0 ? _allPoints[^1].Actual : 0;

        var point = new PidDataPoint(
            elapsed,
            double.IsNaN(target) ? lastTarget : target,
            double.IsNaN(actual) ? lastActual : actual
        );

        _allPoints.Add(point);

        Application.Current?.Dispatcher.Invoke(() =>
        {
            DataPoints.Add(point);
            HasData = true;

            // 自动调整Y轴范围
            if (AutoYRange)
            {
                var allVals = _allPoints.SelectMany(p => new[] { p.Target, p.Actual });
                var min = allVals.Min();
                var max = allVals.Max();
                var margin = (max - min) * 0.1;
                if (margin < 1) margin = 1;
                YMin = min - margin;
                YMax = max + margin;
            }

            // 自动滚动：最新数据始终可见
            if (elapsed > TimeRange)
            {
                TimeOffset = elapsed - TimeRange;
            }

            RequestChartUpdate?.Invoke();
        });
    }

    private double ExtractValue(string text, string prefix)
    {
        if (string.IsNullOrEmpty(prefix)) return double.NaN;

        // 支持格式: T:90.5 或 T=90.5 或 T 90.5
        var pattern = Regex.Escape(prefix) + @"\s*[:=]?\s*([-+]?\d+\.?\d*)";
        var match = Regex.Match(text, pattern);
        if (match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
            return val;

        return double.NaN;
    }

    // ===== 缩放/平移 =====

    public void ZoomX(double factor)
    {
        TimeRange *= factor;
        if (TimeRange < 1000) TimeRange = 1000;   // 最小1秒
        if (TimeRange > 60000) TimeRange = 60000; // 最大60秒
        RequestChartUpdate?.Invoke();
    }

    public void ZoomY(double factor)
    {
        AutoYRange = false;
        var center = (YMin + YMax) / 2;
        var half = (YMax - YMin) / 2 * factor;
        if (half < 0.5) half = 0.5;
        YMin = center - half;
        YMax = center + half;
        RequestChartUpdate?.Invoke();
    }

    public void Pan(double dx, double dy, double canvasWidth, double canvasHeight)
    {
        // X轴平移
        if (canvasWidth > 0)
        {
            var ratio = dx / canvasWidth;
            TimeOffset -= ratio * TimeRange;
            if (TimeOffset < 0) TimeOffset = 0;
        }

        // Y轴平移
        if (!AutoYRange && canvasHeight > 0)
        {
            var ratio = dy / canvasHeight;
            var shift = ratio * (YMax - YMin);
            YMin += shift;
            YMax += shift;
        }

        RequestChartUpdate?.Invoke();
    }

    // ===== 事件 =====
    public event Action? RequestChartUpdate;
}
