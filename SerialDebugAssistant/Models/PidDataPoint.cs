namespace SerialDebugAssistant.Models;

/// <summary>PID 数据点</summary>
public record PidDataPoint(double Time, double Target, double Actual);
