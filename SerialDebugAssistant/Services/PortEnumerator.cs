using System.IO.Ports;

namespace SerialDebugAssistant.Services;

public static class PortEnumerator
{
    public static string[] GetAvailablePorts() => SerialPort.GetPortNames();
}
