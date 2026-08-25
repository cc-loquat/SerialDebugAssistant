using System.IO.Ports;

namespace SerialDebugAssistant.Models;

public class SerialPortConfig
{
    public string PortName { get; set; } = "COM1";
    public int BaudRate { get; set; } = 9600;
    public int DataBits { get; set; } = 8;
    public StopBits StopBits { get; set; } = StopBits.One;
    public Parity Parity { get; set; } = Parity.None;
    public Handshake Handshake { get; set; } = Handshake.None;
    public int ReadTimeout { get; set; } = 1000;
    public int WriteTimeout { get; set; } = 1000;

    public static bool IsValidBaudRate(int baud) => baud >= 1200 && baud <= 921600;
    public static bool IsValidDataBits(int bits) => bits >= 5 && bits <= 8;
}
