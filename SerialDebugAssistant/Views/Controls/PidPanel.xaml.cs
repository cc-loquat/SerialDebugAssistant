using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SerialDebugAssistant.Views.Controls;

public partial class PidPanel : UserControl
{
    private Polyline _targetLine = null!;
    private Polyline _actualLine = null!;
    private bool _isDragging;
    private Point _lastMousePos;

    public PidPanel()
    {
        InitializeComponent();
        InitializeLines();
    }

    private void InitializeLines()
    {
        _targetLine = new Polyline
        {
            StrokeThickness = 2,
            StrokeDashArray = new DoubleCollection { 5, 3 },
            Fill = Brushes.Transparent
        };
        _actualLine = new Polyline
        {
            StrokeThickness = 2.2,
            Fill = Brushes.Transparent
        };
        ChartCanvas.Children.Add(_targetLine);
        ChartCanvas.Children.Add(_actualLine);
        ApplyChartTheme();
    }

    private void ChartCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        DrawChart();
    }

    public void DrawChart()
    {
        if (DataContext is not ViewModels.PidViewModel vm) return;
        if (vm.DataPoints.Count == 0) return;
        if (ChartCanvas.ActualWidth < 1 || ChartCanvas.ActualHeight < 1) return;

        var points = vm.DataPoints;
        ApplyChartTheme();
        var margin = new Thickness(40, 20, 20, 30);
        var chartW = ChartCanvas.ActualWidth - margin.Left - margin.Right;
        var chartH = ChartCanvas.ActualHeight - margin.Top - margin.Bottom;

        // 计算可见范围
        var timeRange = vm.TimeRange > 0 ? vm.TimeRange : 10000; // 默认10秒
        var minTime = vm.TimeOffset;
        var maxTime = minTime + timeRange;
        var yMin = vm.YMin;
        var yMax = vm.YMax;
        var yRange = yMax - yMin;
        if (yRange <= 0) yRange = 1;

        // 清除旧线条，保留两条折线
        var toRemove = ChartCanvas.Children.OfType<UIElement>()
            .Where(c => c is not Polyline)
            .ToList();
        foreach (var c in toRemove) ChartCanvas.Children.Remove(c);

        // 绘制网格线和标签
        DrawGrid(margin, chartW, chartH, minTime, maxTime, yMin, yMax);

        // 绘制目标值线
        if (vm.ShowTargetLine)
        {
            _targetLine.Visibility = Visibility.Visible;
            _targetLine.Points = MapPoints(points, p => p.Target, minTime, maxTime, yMin, yRange, margin, chartW, chartH);
        }
        else _targetLine.Visibility = Visibility.Collapsed;

        // 绘制实际值线
        if (vm.ShowActualLine)
        {
            _actualLine.Visibility = Visibility.Visible;
            _actualLine.Points = MapPoints(points, p => p.Actual, minTime, maxTime, yMin, yRange, margin, chartW, chartH);
        }
        else _actualLine.Visibility = Visibility.Collapsed;
    }

    private PointCollection MapPoints(IList<Models.PidDataPoint> points,
        Func<Models.PidDataPoint, double> selector,
        double minTime, double maxTime, double yMin, double yRange,
        Thickness margin, double chartW, double chartH)
    {
        var pc = new PointCollection();
        var timeRange = maxTime - minTime;
        if (timeRange <= 0) timeRange = 1;

        foreach (var pt in points)
        {
            if (pt.Time < minTime || pt.Time > maxTime) continue;
            var x = margin.Left + (pt.Time - minTime) / timeRange * chartW;
            var val = selector(pt);
            var y = margin.Top + chartH - (val - yMin) / yRange * chartH;
            pc.Add(new Point(x, y));
        }
        return pc;
    }

    private void DrawGrid(Thickness margin, double chartW, double chartH,
        double minTime, double maxTime, double yMin, double yMax)
    {
        var gridBrush = (Brush)FindResource("ChartGridBrush");
        var textBrush = (Brush)FindResource("ChartTextBrush");

        // 水平网格线 (Y轴)
        for (int i = 0; i <= 4; i++)
        {
            var y = margin.Top + chartH * i / 4;
            var line = new Line
            {
                X1 = margin.Left, Y1 = y,
                X2 = margin.Left + chartW, Y2 = y,
                Stroke = gridBrush, StrokeThickness = 0.5
            };
            ChartCanvas.Children.Add(line);

            var val = yMax - (yMax - yMin) * i / 4;
            var tb = new System.Windows.Controls.TextBlock
            {
                Text = val.ToString("F1"),
                Foreground = textBrush, FontSize = 9,
                Margin = new Thickness(2, y - 6, 0, 0)
            };
            ChartCanvas.Children.Add(tb);
        }

        // 垂直网格线 (时间轴)
        for (int i = 0; i <= 5; i++)
        {
            var x = margin.Left + chartW * i / 5;
            var line = new Line
            {
                X1 = x, Y1 = margin.Top,
                X2 = x, Y2 = margin.Top + chartH,
                Stroke = gridBrush, StrokeThickness = 0.5
            };
            ChartCanvas.Children.Add(line);

            var t = minTime + (maxTime - minTime) * i / 5;
            var tb = new System.Windows.Controls.TextBlock
            {
                Text = $"{(t / 1000):F1}s",
                Foreground = textBrush, FontSize = 9,
                Margin = new Thickness(x - 10, margin.Top + chartH + 2, 0, 0)
            };
            ChartCanvas.Children.Add(tb);
        }
    }

    private void ApplyChartTheme()
    {
        _targetLine.Stroke = (Brush)FindResource("ChartTargetBrush");
        _actualLine.Stroke = (Brush)FindResource("ChartActualBrush");
    }

    private void ChartCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (DataContext is not ViewModels.PidViewModel vm) return;

        if (Keyboard.IsKeyDown(Key.LeftAlt) || Keyboard.IsKeyDown(Key.RightAlt))
        {
            // Alt+滚轮: Y轴缩放
            var factor = e.Delta > 0 ? 0.9 : 1.1;
            vm.ZoomY(factor);
        }
        else
        {
            // 滚轮: X轴时间缩放
            var factor = e.Delta > 0 ? 0.9 : 1.1;
            vm.ZoomX(factor);
        }
        e.Handled = true;
    }

    private void ChartCanvas_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _isDragging = true;
            _lastMousePos = e.GetPosition(ChartCanvas);
            ChartCanvas.CaptureMouse();
        }
    }

    private void ChartCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isDragging || DataContext is not ViewModels.PidViewModel vm) return;

        var pos = e.GetPosition(ChartCanvas);
        var dx = pos.X - _lastMousePos.X;
        var dy = pos.Y - _lastMousePos.Y;
        _lastMousePos = pos;

        vm.Pan(dx, dy, ChartCanvas.ActualWidth, ChartCanvas.ActualHeight);
    }

    private void ChartCanvas_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _isDragging = false;
        ChartCanvas.ReleaseMouseCapture();
    }
}
