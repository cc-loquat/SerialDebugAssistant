using Moq;
using SerialDebugAssistant.Models;
using SerialDebugAssistant.Services;
using SerialDebugAssistant.ViewModels;
using System.Threading.Tasks;
using Xunit;

namespace SerialDebugAssistant.Tests;

public class MainViewModelTests
{
    private readonly Mock<ISerialService> _serialMock = new();

    [Fact]
    public void InitialState_IsDisconnected()
    {
        var vm = new MainViewModel(_serialMock.Object);
        Assert.False(vm.IsConnected);
        Assert.Equal("打开", vm.ConnectButtonText);
    }

    [Fact]
    public async Task ConnectCommand_WhenDisconnected_OpensPort()
    {
        _serialMock.Setup(s => s.OpenAsync(It.IsAny<SerialPortConfig>()))
                   .ReturnsAsync(true);
        var vm = new MainViewModel(_serialMock.Object);
        vm.SelectedPort = "COM3";
        await vm.ConnectCommand.ExecuteAsync(null);
        _serialMock.Verify(s => s.OpenAsync(It.IsAny<SerialPortConfig>()), Times.Once);
        Assert.True(vm.IsConnected);
        Assert.Equal("关闭", vm.ConnectButtonText);
    }

    [Fact]
    public async Task ConnectCommand_OpenFails_StaysDisconnected()
    {
        _serialMock.Setup(s => s.OpenAsync(It.IsAny<SerialPortConfig>()))
                   .ReturnsAsync(false);
        var vm = new MainViewModel(_serialMock.Object);
        vm.SelectedPort = "COM3";
        await vm.ConnectCommand.ExecuteAsync(null);
        Assert.False(vm.IsConnected);
    }

    [Fact]
    public void ClearReceivedCommand_ClearsReceivedText()
    {
        var vm = new MainViewModel(_serialMock.Object);
        vm.ReceivedText = "hello";
        vm.ClearReceivedCommand.Execute(null);
        Assert.Equal(string.Empty, vm.ReceivedText);
    }
}
