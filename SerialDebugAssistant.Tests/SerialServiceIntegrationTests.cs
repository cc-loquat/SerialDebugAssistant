using SerialDebugAssistant.Models;
using SerialDebugAssistant.Services;
using System.Threading.Tasks;
using Xunit;

namespace SerialDebugAssistant.Tests;

public class SerialServiceIntegrationTests
{
    [Fact]
    public void GetAvailablePorts_ReturnsArray()
    {
        using var svc = new SerialService();
        var ports = svc.GetAvailablePorts();
        Assert.NotNull(ports);
    }

    [Fact]
    public async Task OpenAsync_InvalidPort_ReturnsFalse()
    {
        using var svc = new SerialService();
        var cfg = new SerialPortConfig { PortName = "COM999" };
        var ok = await svc.OpenAsync(cfg);
        Assert.False(ok);
    }
}
