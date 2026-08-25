using SerialDebugAssistant.Models;
using System.IO.Ports;
using Xunit;

namespace SerialDebugAssistant.Tests;

public class SerialPortConfigTests
{
    [Fact]
    public void Defaults_AreStandardValues()
    {
        var cfg = new SerialPortConfig();
        Assert.Equal(9600, cfg.BaudRate);
        Assert.Equal(8, cfg.DataBits);
        Assert.Equal(StopBits.One, cfg.StopBits);
        Assert.Equal(Parity.None, cfg.Parity);
        Assert.Equal(Handshake.None, cfg.Handshake);
    }

    [Theory]
    [InlineData(1200, true)]
    [InlineData(9600, true)]
    [InlineData(921600, true)]
    [InlineData(0, false)]
    [InlineData(-1, false)]
    [InlineData(1000000, false)]
    public void IsValidBaudRate_ConstrainsRange(int baud, bool expected)
    {
        Assert.Equal(expected, SerialPortConfig.IsValidBaudRate(baud));
    }

    [Theory]
    [InlineData(5, true)]
    [InlineData(8, true)]
    [InlineData(4, false)]
    [InlineData(9, false)]
    public void IsValidDataBits_ConstrainsRange(int bits, bool expected)
    {
        Assert.Equal(expected, SerialPortConfig.IsValidDataBits(bits));
    }
}
