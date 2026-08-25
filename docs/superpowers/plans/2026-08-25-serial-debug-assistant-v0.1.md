# 串口调试助手 v0.1 基础版 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现一款 VSCode 风格的 Windows 桌面串口调试助手 v0.1，支持串口收发、参数配置、HEX/ASCII 切换、日志保存、自动更新。

**Architecture:** WPF + MVVM（CommunityToolkit.Mvvm）+ 分层（Models / Services / ViewModels / Views / Themes）。`SerialService` 通过 `ISerialService` 抽象便于测试，`UpdateService` 基于 Velopack 实现 GitHub Release 自动更新。

**Tech Stack:** .NET 8, WPF, C# 12, CommunityToolkit.Mvvm 8.x, System.IO.Ports 8.x, Velopack, HandyControl 3.x, Inno Setup 6, xUnit。

**Spec:** `docs/superpowers/specs/2026-08-25-serial-debug-assistant-design.md`

---

## File Structure

```
SerialDebugAssistant/
├── SerialDebugAssistant.csproj
├── App.xaml
├── App.xaml.cs
├── appsettings.json
├── Models/
│   ├── SerialPortConfig.cs         # 串口参数模型（端口/波特率/数据位/停止位/校验/流控）
│   ├── ReceivedData.cs             # 接收数据帧（时间戳/数据/方向）
│   └── LogSettings.cs              # 日志设置（路径/格式/自动保存开关）
├── Services/
│   ├── ISerialService.cs           # 串口服务接口
│   ├── SerialService.cs            # SerialPort 封装实现
│   ├── IUpdateService.cs           # 更新服务接口
│   ├── UpdateService.cs            # Velopack 自动更新实现
│   └── PortEnumerator.cs           # 可用串口枚举
├── ViewModels/
│   ├── MainViewModel.cs            # 主窗口 VM
│   └── ViewModelBase.cs            # VM 基类（ObservableObject）
├── Views/
│   ├── MainWindow.xaml
│   ├── MainWindow.xaml.cs
│   └── Controls/
│       ├── ActivityBar.xaml        # 左侧活动栏
│       ├── SidebarPanel.xaml       # 侧边栏（参数）
│       ├── DataDisplayPanel.xaml   # 收发显示区
│       └── StatusBar.xaml          # 底部状态栏
├── Themes/
│   ├── Colors.xaml                 # 颜色常量
│   └── VSCodeTheme.xaml            # 配色资源字典
└── Utils/
    └── HexConverter.cs             # HEX/ASCII 互转

SerialDebugAssistant.Tests/
├── SerialDebugAssistant.Tests.csproj
├── HexConverterTests.cs
├── SerialPortConfigTests.cs
├── MainViewModelTests.cs
└── SerialServiceIntegrationTests.cs

installer.iss                          # Inno Setup 脚本
RELEASE_NOTES.md
```

---

## Task 1: 创建解决方案与项目骨架

**Files:**
- Create: `SerialDebugAssistant/SerialDebugAssistant.csproj`
- Create: `SerialDebugAssistant/App.xaml`
- Create: `SerialDebugAssistant/App.xaml.cs`
- Create: `SerialDebugAssistant/Program.cs`（Velopack 入口）
- Create: `SerialDebugAssistant.Tests/SerialDebugAssistant.Tests.csproj`

- [ ] **Step 1: 创建主项目文件**

创建 `SerialDebugAssistant/SerialDebugAssistant.csproj`：

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <UseWPF>true</UseWPF>
    <ImplicitUsings>enable</ImplicitUsings>
    <ApplicationManifest>app.manifest</ApplicationManifest>
    <AssemblyName>SerialDebugAssistant</AssemblyName>
    <RootNamespace>SerialDebugAssistant</RootNamespace>
    <Version>0.1.0</Version>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
    <PackageReference Include="System.IO.Ports" Version="8.0.0" />
    <PackageReference Include="HandyControl" Version="3.5.1" />
    <PackageReference Include="Velopack" Version="0.0.942" />
  </ItemGroup>

</Project>
```

- [ ] **Step 2: 创建应用入口 App.xaml**

```xml
<Application x:Class="SerialDebugAssistant.App"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             ShutdownMode="OnMainWindowShutdown">
    <Application.Resources>
        <ResourceDictionary>
            <ResourceDictionary.MergedDictionaries>
                <ResourceDictionary Source="Themes/Colors.xaml"/>
                <ResourceDictionary Source="Themes/VSCodeTheme.xaml"/>
            </ResourceDictionary.MergedDictionaries>
        </ResourceDictionary>
    </Application.Resources>
</Application>
```

- [ ] **Step 3: 创建 App.xaml.cs**

```csharp
using System.Windows;

namespace SerialDebugAssistant;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
    }
}
```

- [ ] **Step 4: 创建 Velopack 入口 Program.cs**

`SerialDebugAssistant/Program.cs`：

```csharp
using System;
using System.Windows;
using Velopack;

namespace SerialDebugAssistant;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
```

注意：使用自定义 `Main` 后，需在 csproj 里不使用默认的 `App.xaml` 启动，已在 `<ApplicationManifest>` 配置。

- [ ] **Step 5: 创建测试项目 `SerialDebugAssistant.Tests/SerialDebugAssistant.Tests.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>net8.0-windows</TargetFramework>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
    <UseWPF>true</UseWPF>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.10.0" />
    <PackageReference Include="xunit" Version="2.9.0" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageReference Include="Moq" Version="4.20.70" />
  </ItemGroup>

  <ItemGroup>
    <ProjectReference Include="..\SerialDebugAssistant\SerialDebugAssistant.csproj" />
  </ItemGroup>

</Project>
```

- [ ] **Step 6: 验证项目能编译**

Run: `dotnet build SerialDebugAssistant.sln`
Expected: Build succeeded, 0 Errors

- [ ] **Step 7: Commit**

```bash
git add SerialDebugAssistant/ SerialDebugAssistant.Tests/
git commit -m "chore: 初始化项目骨架"
```

---

## Task 2: 主题配色资源

**Files:**
- Create: `SerialDebugAssistant/Themes/Colors.xaml`
- Create: `SerialDebugAssistant/Themes/VSCodeTheme.xaml`

- [ ] **Step 1: 写颜色常量 Colors.xaml**

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <Color x:Key="MainBgColor">#1E1E1E</Color>
    <Color x:Key="ActivityBarBgColor">#333333</Color>
    <Color x:Key="SidebarBgColor">#252526</Color>
    <Color x:Key="StatusBarConnectedColor">#007ACC</Color>
    <Color x:Key="StatusBarDisconnectedColor">#1E1E1E</Color>
    <Color x:Key="AccentColor">#007ACC</Color>
    <Color x:Key="ForegroundColor">#CCCCCC</Color>
    <Color x:Key="RxTextColor">#D4D4D4</Color>
    <Color x:Key="TxTextColor">#569CD6</Color>
    <Color x:Key="ErrorColor">#F44747</Color>
    <Color x:Key="SuccessColor">#4EC9B0</Color>

    <SolidColorBrush x:Key="MainBgBrush" Color="{StaticResource MainBgColor}"/>
    <SolidColorBrush x:Key="ActivityBarBgBrush" Color="{StaticResource ActivityBarBgColor}"/>
    <SolidColorBrush x:Key="SidebarBgBrush" Color="{StaticResource SidebarBgColor}"/>
    <SolidColorBrush x:Key="StatusBarConnectedBrush" Color="{StaticResource StatusBarConnectedColor}"/>
    <SolidColorBrush x:Key="StatusBarDisconnectedBrush" Color="{StaticResource StatusBarDisconnectedColor}"/>
    <SolidColorBrush x:Key="AccentBrush" Color="{StaticResource AccentColor}"/>
    <SolidColorBrush x:Key="ForegroundBrush" Color="{StaticResource ForegroundColor}"/>
    <SolidColorBrush x:Key="RxTextBrush" Color="{StaticResource RxTextColor}"/>
    <SolidColorBrush x:Key="TxTextBrush" Color="{StaticResource TxTextColor}"/>
    <SolidColorBrush x:Key="ErrorBrush" Color="{StaticResource ErrorColor}"/>
    <SolidColorBrush x:Key="SuccessBrush" Color="{StaticResource SuccessColor}"/>
</ResourceDictionary>
```

- [ ] **Step 2: 写 VSCodeTheme.xaml 基础样式**

```xml
<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <Style x:Key="WindowBaseStyle" TargetType="Window">
        <Setter Property="Background" Value="{DynamicResource MainBgBrush}"/>
        <Setter Property="Foreground" Value="{DynamicResource ForegroundBrush}"/>
    </Style>

    <Style x:Key="ActivityIconButton" TargetType="Button">
        <Setter Property="Width" Value="48"/>
        <Setter Property="Height" Value="48"/>
        <Setter Property="Background" Value="Transparent"/>
        <Setter Property="Foreground" Value="{DynamicResource ForegroundBrush}"/>
        <Setter Property="BorderThickness" Value="0"/>
        <Setter Property="Template">
            <Setter.Value>
                <ControlTemplate TargetType="Button">
                    <Border Background="{TemplateBinding Background}">
                        <ContentPresenter HorizontalAlignment="Center" VerticalAlignment="Center"/>
                    </Border>
                </ControlTemplate>
            </Setter.Value>
        </Setter>
        <Style.Triggers>
            <Trigger Property="IsMouseOver" Value="True">
                <Setter Property="Background" Value="{DynamicResource AccentBrush}"/>
            </Trigger>
        </Style.Triggers>
    </Style>
</ResourceDictionary>
```

- [ ] **Step 3: 验证编译**

Run: `dotnet build SerialDebugAssistant/SerialDebugAssistant.csproj`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add SerialDebugAssistant/Themes/
git commit -m "feat: 添加 VSCode 风配色主题"
```

---

## Task 3: 工具类 HexConverter（TDD）

**Files:**
- Create: `SerialDebugAssistant/Utils/HexConverter.cs`
- Test: `SerialDebugAssistant.Tests/HexConverterTests.cs`

- [ ] **Step 1: 写失败的测试**

`SerialDebugAssistant.Tests/HexConverterTests.cs`：

```csharp
using SerialDebugAssistant.Utils;
using Xunit;

namespace SerialDebugAssistant.Tests;

public class HexConverterTests
{
    [Theory]
    [InlineData("", new byte[0])]
    [InlineData("41", new byte[] { 0x41 })]
    [InlineData("41 42", new byte[] { 0x41, 0x42 })]
    [InlineData("4142", new byte[] { 0x41, 0x42 })]
    [InlineData("0x41 0x42", new byte[] { 0x41, 0x42 })]
    public void HexStringToBytes_ValidInput_ReturnsBytes(string input, byte[] expected)
    {
        var result = HexConverter.HexStringToBytes(input);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("4")]
    [InlineData("4G")]
    [InlineData("412")]
    public void HexStringToBytes_InvalidInput_ThrowsFormatException(string input)
    {
        Assert.Throws<FormatException>(() => HexConverter.HexStringToBytes(input));
    }

    [Theory]
    [InlineData(new byte[] { 0x41, 0x42 }, "41 42")]
    [InlineData(new byte[0], "")]
    public void BytesToHexString_Converts(byte[] input, string expected)
    {
        Assert.Equal(expected, HexConverter.BytesToHexString(input));
    }

    [Theory]
    [InlineData("AB", new byte[] { 0x41, 0x42 })]
    public void AsciiToBytes_Converts(string input, byte[] expected)
    {
        Assert.Equal(expected, HexConverter.AsciiToBytes(input));
    }

    [Theory]
    [InlineData(new byte[] { 0x41, 0x42 }, "AB")]
    public void BytesToAscii_Converts(byte[] input, string expected)
    {
        Assert.Equal(expected, HexConverter.BytesToAscii(input));
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test SerialDebugAssistant.Tests/SerialDebugAssistant.Tests.csproj --filter "FullyQualifiedName~HexConverterTests"`
Expected: FAIL，`HexConverter` 类型未定义。

- [ ] **Step 3: 写实现**

`SerialDebugAssistant/Utils/HexConverter.cs`：

```csharp
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SerialDebugAssistant.Utils;

public static class HexConverter
{
    public static byte[] HexStringToBytes(string hex)
    {
        if (hex is null) throw new ArgumentNullException(nameof(hex));
        var cleaned = hex.Replace("0x", string.Empty, StringComparison.OrdinalIgnoreCase)
                         .Replace(" ", string.Empty)
                         .Replace("\t", string.Empty)
                         .Replace("\r", string.Empty)
                         .Replace("\n", string.Empty);
        if (cleaned.Length == 0) return Array.Empty<byte>();
        if (cleaned.Length % 2 != 0) throw new FormatException("HEX 字符串长度必须为偶数");
        var bytes = new byte[cleaned.Length / 2];
        for (int i = 0; i < cleaned.Length; i += 2)
        {
            var pair = cleaned.Substring(i, 2);
            if (!byte.TryParse(pair, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var b))
                throw new FormatException($"非法 HEX 字符: {pair}");
            bytes[i / 2] = b;
        }
        return bytes;
    }

    public static string BytesToHexString(byte[] bytes)
    {
        if (bytes is null || bytes.Length == 0) return string.Empty;
        var sb = new StringBuilder(bytes.Length * 3);
        for (int i = 0; i < bytes.Length; i++)
        {
            if (i > 0) sb.Append(' ');
            sb.Append(bytes[i].ToString("X2"));
        }
        return sb.ToString();
    }

    public static byte[] AsciiToBytes(string ascii)
    {
        if (ascii is null) return Array.Empty<byte>();
        return Encoding.UTF8.GetBytes(ascii);
    }

    public static string BytesToAscii(byte[] bytes)
    {
        if (bytes is null || bytes.Length == 0) return string.Empty;
        return Encoding.UTF8.GetString(bytes);
    }
}
```

- [ ] **Step 4: 运行测试验证通过**

Run: `dotnet test --filter "FullyQualifiedName~HexConverterTests"`
Expected: PASS, 全部 4 个测试用例通过。

- [ ] **Step 5: Commit**

```bash
git add SerialDebugAssistant/Utils/HexConverter.cs SerialDebugAssistant.Tests/HexConverterTests.cs
git commit -m "feat: 实现 HEX/ASCII 互转工具"
```

---

## Task 4: 串口参数模型 SerialPortConfig

**Files:**
- Create: `SerialDebugAssistant/Models/SerialPortConfig.cs`
- Test: `SerialDebugAssistant.Tests/SerialPortConfigTests.cs`

- [ ] **Step 1: 写失败的测试**

```csharp
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
```

- [ ] **Step 2: 运行验证失败**

Run: `dotnet test --filter "FullyQualifiedName~SerialPortConfigTests"`
Expected: FAIL

- [ ] **Step 3: 写实现**

`SerialDebugAssistant/Models/SerialPortConfig.cs`：

```csharp
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
```

- [ ] **Step 4: 运行测试验证通过**

Run: `dotnet test --filter "FullyQualifiedName~SerialPortConfigTests"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add SerialDebugAssistant/Models/SerialPortConfig.cs SerialDebugAssistant.Tests/SerialPortConfigTests.cs
git commit -m "feat: 添加串口参数模型与验证"
```

---

## Task 5: ReceivedData 与 LogSettings 模型

**Files:**
- Create: `SerialDebugAssistant/Models/ReceivedData.cs`
- Create: `SerialDebugAssistant/Models/LogSettings.cs`

- [ ] **Step 1: 写 ReceivedData**

```csharp
using System;

namespace SerialDebugAssistant.Models;

public enum DataDirection
{
    Received,
    Sent
}

public class ReceivedData
{
    public DateTime Timestamp { get; init; } = DateTime.Now;
    public DataDirection Direction { get; init; }
    public byte[] RawBytes { get; init; } = Array.Empty<byte>();
    public string DisplayText { get; init; } = string.Empty;
}
```

- [ ] **Step 2: 写 LogSettings**

```csharp
using System;
using System.IO;

namespace SerialDebugAssistant.Models;

public class LogSettings
{
    public bool AutoSave { get; set; } = false;
    public string LogDirectory { get; set; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                     "SerialDebugAssistant", "Logs");
    public bool UseHexFormat { get; set; } = false;
    public bool IncludeTimestamp { get; set; } = true;

    public string GetDailyLogFile(DateTime date)
    {
        var ext = UseHexFormat ? ".hex" : ".txt";
        return Path.Combine(LogDirectory, $"{date:yyyy-MM-dd}{ext}");
    }
}
```

- [ ] **Step 3: 验证编译**

Run: `dotnet build SerialDebugAssistant/SerialDebugAssistant.csproj`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add SerialDebugAssistant/Models/ReceivedData.cs SerialDebugAssistant/Models/LogSettings.cs
git commit -m "feat: 添加数据帧与日志设置模型"
```

---

## Task 6: ISerialService 接口与 SerialService 实现

**Files:**
- Create: `SerialDebugAssistant/Services/ISerialService.cs`
- Create: `SerialDebugAssistant/Services/SerialService.cs`
- Create: `SerialDebugAssistant/Services/PortEnumerator.cs`
- Test: `SerialDebugAssistant.Tests/SerialServiceIntegrationTests.cs`

- [ ] **Step 1: 写接口**

`SerialDebugAssistant/Services/ISerialService.cs`：

```csharp
using System;
using System.Threading.Tasks;
using SerialDebugAssistant.Models;

namespace SerialDebugAssistant.Services;

public interface ISerialService : IDisposable
{
    bool IsOpen { get; }
    event EventHandler<DataReceivedEventArgs>? DataReceived;
    event EventHandler<SerialErrorEventArgs>? ErrorOccurred;
    event EventHandler? ConnectionChanged;

    string[] GetAvailablePorts();
    Task<bool> OpenAsync(SerialPortConfig config);
    Task CloseAsync();
    Task<int> SendAsync(byte[] data);
}

public class DataReceivedEventArgs : EventArgs
{
    public byte[] Data { get; init; } = Array.Empty<byte>();
    public DateTime Timestamp { get; init; } = DateTime.Now;
}

public class SerialErrorEventArgs : EventArgs
{
    public string Message { get; init; } = string.Empty;
}
```

- [ ] **Step 2: 写 PortEnumerator**

```csharp
using System.IO.Ports;

namespace SerialDebugAssistant.Services;

public static class PortEnumerator
{
    public static string[] GetAvailablePorts() => SerialPort.GetPortNames();
}
```

- [ ] **Step 3: 写 SerialService 实现**

`SerialDebugAssistant/Services/SerialService.cs`：

```csharp
using System;
using System.IO.Ports;
using System.Threading;
using System.Threading.Tasks;
using SerialDebugAssistant.Models;

namespace SerialDebugAssistant.Services;

public class SerialService : ISerialService
{
    private SerialPort? _port;
    private bool _disposed;

    public bool IsOpen => _port?.IsOpen ?? false;

    public event EventHandler<DataReceivedEventArgs>? DataReceived;
    public event EventHandler<SerialErrorEventArgs>? ErrorOccurred;
    public event EventHandler? ConnectionChanged;

    public string[] GetAvailablePorts() => SerialPort.GetPortNames();

    public Task<bool> OpenAsync(SerialPortConfig config)
    {
        if (IsOpen) throw new InvalidOperationException("串口已打开");
        _port = new SerialPort(
            config.PortName,
            config.BaudRate,
            config.Parity,
            config.DataBits,
            config.StopBits)
        {
            Handshake = config.Handshake,
            ReadTimeout = config.ReadTimeout,
            WriteTimeout = config.WriteTimeout,
            ReadBufferSize = 4096,
            WriteBufferSize = 4096
        };
        _port.DataReceived += OnDataReceived;
        _port.ErrorReceived += OnErrorReceived;

        try
        {
            _port.Open();
            ConnectionChanged?.Invoke(this, EventArgs.Empty);
            return Task.FromResult(true);
        }
        catch (Exception ex)
        {
            ErrorOccurred?.Invoke(this, new SerialErrorEventArgs { Message = ex.Message });
            return Task.FromResult(false);
        }
    }

    public Task CloseAsync()
    {
        if (_port is null || !_port.IsOpen) return Task.CompletedTask;
        _port.DataReceived -= OnDataReceived;
        _port.ErrorReceived -= OnErrorReceived;
        _port.Close();
        _port.Dispose();
        _port = null;
        ConnectionChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public async Task<int> SendAsync(byte[] data)
    {
        if (!IsOpen || _port is null) throw new InvalidOperationException("串口未打开");
        await Task.Run(() => _port.Write(data, 0, data.Length));
        return data.Length;
    }

    private void OnDataReceived(object sender, SerialDataReceivedEventArgs e)
    {
        if (_port is null || !_port.IsOpen) return;
        if (e.EventType != SerialData.Chars) return;
        var bytesToRead = _port.BytesToRead;
        if (bytesToRead <= 0) return;
        var buffer = new byte[bytesToRead];
        _port.BaseStream.Read(buffer, 0, bytesToRead);
        DataReceived?.Invoke(this, new DataReceivedEventArgs { Data = buffer });
    }

    private void OnErrorReceived(object sender, SerialErrorReceivedEventArgs e)
    {
        ErrorOccurred?.Invoke(this, new SerialErrorEventArgs { Message = $"串口错误: {e.EventType}" });
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        CloseAsync().GetAwaiter().GetResult();
    }
}
```

- [ ] **Step 4: 写集成测试**

`SerialDebugAssistant.Tests/SerialServiceIntegrationTests.cs`：

```csharp
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
```

- [ ] **Step 5: 运行测试**

Run: `dotnet test --filter "FullyQualifiedName~SerialServiceIntegrationTests"`
Expected: 2 个测试 PASS（COM999 不存在返回 false）

- [ ] **Step 6: Commit**

```bash
git add SerialDebugAssistant/Services/ SerialDebugAssistant.Tests/SerialServiceIntegrationTests.cs
git commit -m "feat: 实现串口服务"
```

---

## Task 7: ViewModelBase 与 MainViewModel（TDD）

**Files:**
- Create: `SerialDebugAssistant/ViewModels/ViewModelBase.cs`
- Create: `SerialDebugAssistant/ViewModels/MainViewModel.cs`
- Test: `SerialDebugAssistant.Tests/MainViewModelTests.cs`

- [ ] **Step 1: 写 MainViewModelTests**

```csharp
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
```

- [ ] **Step 2: 验证失败**

Run: `dotnet test --filter "FullyQualifiedName~MainViewModelTests"`
Expected: FAIL

- [ ] **Step 3: 写 ViewModelBase**

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace SerialDebugAssistant.ViewModels;

public class ViewModelBase : ObservableObject { }
```

- [ ] **Step 4: 写 MainViewModel**

```csharp
using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using SerialDebugAssistant.Models;
using SerialDebugAssistant.Services;
using SerialDebugAssistant.Utils;

namespace SerialDebugAssistant.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly ISerialService _serial;
    private readonly ObservableCollection<string> _availablePorts = new();

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
    [ObservableProperty] private long _rxByteCount;
    [ObservableProperty] private long _txByteCount;
    [ObservableProperty] private string _statusMessage = "就绪";

    public ObservableCollection<string> AvailablePorts => _availablePorts;

    public string ConnectButtonText => IsConnected ? "关闭" : "打开";

    public MainViewModel(ISerialService serial)
    {
        _serial = serial;
        _serial.DataReceived += OnDataReceived;
        _serial.ErrorOccurred += OnErrorOccurred;
        _serial.ConnectionChanged += OnConnectionChanged;
        RefreshPorts();
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
        ReceivedText += $"[TX] {HexConverter.BytesToHexString(data)}\n";
    }

    [RelayCommand]
    public void ClearReceived()
    {
        ReceivedText = string.Empty;
    }

    private void OnDataReceived(object? sender, DataReceivedEventArgs e)
    {
        RxByteCount += e.Data.Length;
        var text = ReceiveAsHex
            ? HexConverter.BytesToHexString(e.Data)
            : HexConverter.BytesToAscii(e.Data);
        var line = $"[RX {e.Timestamp:HH:mm:ss.fff}] {text}\n";
        System.Windows.Application.Current?.Dispatcher.Invoke(() => ReceivedText += line);
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
}
```

- [ ] **Step 5: 运行测试验证通过**

Run: `dotnet test --filter "FullyQualifiedName~MainViewModelTests"`
Expected: PASS, 4 个用例

- [ ] **Step 6: Commit**

```bash
git add SerialDebugAssistant/ViewModels/ SerialDebugAssistant.Tests/MainViewModelTests.cs
git commit -m "feat: 实现主 ViewModel"
```

---

## Task 8: 主窗口 MainWindow 与三栏布局

**Files:**
- Create: `SerialDebugAssistant/Views/MainWindow.xaml`
- Create: `SerialDebugAssistant/Views/MainWindow.xaml.cs`

- [ ] **Step 1: 写 MainWindow.xaml**

```xml
<Window x:Class="SerialDebugAssistant.Views.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        xmlns:vm="clr-namespace:SerialDebugAssistant.ViewModels"
        xmlns:controls="clr-namespace:SerialDebugAssistant.Views.Controls"
        Title="串口调试助手 v0.1" Height="600" Width="900"
        Background="{DynamicResource MainBgBrush}"
        WindowStartupLocation="CenterScreen">

    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>
            <RowDefinition Height="Auto"/>
        </Grid.RowDefinitions>

        <Grid Grid.Row="0">
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="Auto"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <controls:ActivityBar Grid.Column="0" Width="48"/>
            <controls:SidebarPanel Grid.Column="1" Width="240"/>
            <controls:DataDisplayPanel Grid.Column="2"/>
        </Grid>

        <controls:StatusBar Grid.Row="1"/>
    </Grid>
</Window>
```

- [ ] **Step 2: 写 code-behind**

```csharp
using System.Windows;
using SerialDebugAssistant.Services;
using SerialDebugAssistant.ViewModels;

namespace SerialDebugAssistant.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        DataContext = new MainViewModel(new SerialService());
    }
}
```

- [ ] **Step 3: 修改 App.xaml.cs 启动主窗口**

```csharp
using System.Windows;
using SerialDebugAssistant.Views;

namespace SerialDebugAssistant;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new MainWindow();
        window.Show();
    }
}
```

- [ ] **Step 4: 验证编译（先创建空控件占位）**

创建空的 `Views/Controls/ActivityBar.xaml`、`SidebarPanel.xaml`、`DataDisplayPanel.xaml`、`StatusBar.xaml`：

```xml
<UserControl x:Class="SerialDebugAssistant.Views.Controls.ActivityBar"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Background="{DynamicResource ActivityBarBgBrush}"/>
```

各控件的 code-behind：

```csharp
using System.Windows.Controls;
namespace SerialDebugAssistant.Views.Controls;
public partial class ActivityBar : UserControl
{
    public ActivityBar() => InitializeComponent();
}
```

对其他三个控件重复上述模式。

Run: `dotnet build`
Expected: Build succeeded

- [ ] **Step 5: Commit**

```bash
git add SerialDebugAssistant/Views/
git commit -m "feat: 实现主窗口三栏布局骨架"
```

---

## Task 9: ActivityBar 控件

**Files:**
- Modify: `SerialDebugAssistant/Views/Controls/ActivityBar.xaml`

- [ ] **Step 1: 写 ActivityBar.xaml**

```xml
<UserControl x:Class="SerialDebugAssistant.Views.Controls.ActivityBar"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Background="{DynamicResource ActivityBarBgBrush}">

    <StackPanel Orientation="Vertical" VerticalAlignment="Top">
        <Button Style="{DynamicResource ActivityIconButton}" Content="📋" ToolTip="串口参数"/>
        <Button Style="{DynamicResource ActivityIconButton}" Content="📁" ToolTip="日志"/>
        <Button Style="{DynamicResource ActivityIconButton}" Content="⚙" ToolTip="设置"/>
        <Button Style="{DynamicResource ActivityIconButton}" Content="ℹ" ToolTip="关于"/>
    </StackPanel>
</UserControl>
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build`
Expected: Build succeeded

- [ ] **Step 3: Commit**

```bash
git add SerialDebugAssistant/Views/Controls/ActivityBar.xaml
git commit -m "feat: 实现活动栏"
```

---

## Task 10: SidebarPanel 控件（串口参数）

**Files:**
- Modify: `SerialDebugAssistant/Views/Controls/SidebarPanel.xaml`

- [ ] **Step 1: 写 SidebarPanel.xaml**

```xml
<UserControl x:Class="SerialDebugAssistant.Views.Controls.SidebarPanel"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Background="{DynamicResource SidebarBgBrush}">
    <ScrollViewer VerticalScrollBarVisibility="Auto">
        <StackPanel Margin="10">
            <TextBlock Text="串口参数" Foreground="{DynamicResource ForegroundBrush}" FontWeight="Bold" Margin="0,0,0,8"/>

            <TextBlock Text="端口" Foreground="{DynamicResource ForegroundBrush}"/>
            <Grid>
                <Grid.ColumnDefinitions>
                    <ColumnDefinition Width="*"/>
                    <ColumnDefinition Width="Auto"/>
                </Grid.ColumnDefinitions>
                <ComboBox Grid.Column="0" x:Name="PortCombo"
                          ItemsSource="{Binding AvailablePorts}"
                          SelectedItem="{Binding SelectedPort}"/>
                <Button Grid.Column="1" Content="⟳" Width="24" Margin="2,0,0,0"
                        Command="{Binding RefreshPortsCommand}"/>
            </Grid>

            <TextBlock Text="波特率" Foreground="{DynamicResource ForegroundBrush}" Margin="0,8,0,0"/>
            <ComboBox x:Name="BaudCombo" Text="{Binding BaudRate, Mode=TwoWay}" IsEditable="True">
                <ComboBoxItem>9600</ComboBoxItem>
                <ComboBoxItem>19200</ComboBoxItem>
                <ComboBoxItem>38400</ComboBoxItem>
                <ComboBoxItem>57600</ComboBoxItem>
                <ComboBoxItem>115200</ComboBoxItem>
                <ComboBoxItem>230400</ComboBoxItem>
                <ComboBoxItem>460800</ComboBoxItem>
                <ComboBoxItem>921600</ComboBoxItem>
            </ComboBox>

            <TextBlock Text="数据位" Foreground="{DynamicResource ForegroundBrush}" Margin="0,8,0,0"/>
            <ComboBox x:Name="DataCombo" SelectedItem="{Binding DataBits, Mode=TwoWay}">
                <ComboBoxItem>5</ComboBoxItem>
                <ComboBoxItem>6</ComboBoxItem>
                <ComboBoxItem>7</ComboBoxItem>
                <ComboBoxItem>8</ComboBoxItem>
            </ComboBox>

            <TextBlock Text="停止位" Foreground="{DynamicResource ForegroundBrush}" Margin="0,8,0,0"/>
            <ComboBox x:Name="StopCombo" SelectedIndex="0"
                      SelectedItem="{Binding StopBits, Mode=TwoWay}">
                <ComboBoxItem>One</ComboBoxItem>
                <ComboBoxItem>One5</ComboBoxItem>
                <ComboBoxItem>Two</ComboBoxItem>
            </ComboBox>

            <TextBlock Text="校验" Foreground="{DynamicResource ForegroundBrush}" Margin="0,8,0,0"/>
            <ComboBox x:Name="ParityCombo" SelectedIndex="0"
                      SelectedItem="{Binding Parity, Mode=TwoWay}">
                <ComboBoxItem>None</ComboBoxItem>
                <ComboBoxItem>Even</ComboBoxItem>
                <ComboBoxItem>Odd</ComboBoxItem>
                <ComboBoxItem>Mark</ComboBoxItem>
                <ComboBoxItem>Space</ComboBoxItem>
            </ComboBox>

            <TextBlock Text="流控" Foreground="{DynamicResource ForegroundBrush}" Margin="0,8,0,0"/>
            <ComboBox x:Name="FlowCombo" SelectedIndex="0"
                      SelectedItem="{Binding Handshake, Mode=TwoWay}">
                <ComboBoxItem>None</ComboBoxItem>
                <ComboBoxItem>RequestToSend</ComboBoxItem>
                <ComboBoxItem>XOnXOff</ComboBoxItem>
                <ComboBoxItem>RequestToSendXOnXOff</ComboBoxItem>
            </ComboBox>

            <Button Content="{Binding ConnectButtonText}" Margin="0,16,0,0"
                    Height="32" Background="{DynamicResource AccentBrush}"
                    Foreground="White" Command="{Binding ConnectCommand}"/>
            <Button Content="清空接收" Margin="0,8,0,0" Height="28"
                    Command="{Binding ClearReceivedCommand}"/>
        </StackPanel>
    </ScrollViewer>
</UserControl>
```

- [ ] **Step 2: 验证编译并启动**

Run: `dotnet build`
Expected: Build succeeded

Run: `dotnet run --project SerialDebugAssistant`（手动验证窗口能显示，参数能选择）
Expected: 窗口正常显示，参数可改

- [ ] **Step 3: Commit**

```bash
git add SerialDebugAssistant/Views/Controls/SidebarPanel.xaml
git commit -m "feat: 实现串口参数侧边栏"
```

---

## Task 11: DataDisplayPanel 控件（收发区）

**Files:**
- Modify: `SerialDebugAssistant/Views/Controls/DataDisplayPanel.xaml`

- [ ] **Step 1: 写 DataDisplayPanel.xaml**

```xml
<UserControl x:Class="SerialDebugAssistant.Views.Controls.DataDisplayPanel"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Background="{DynamicResource MainBgBrush}">
    <Grid>
        <Grid.RowDefinitions>
            <RowDefinition Height="3*"/>
            <RowDefinition Height="Auto"/>
            <RowDefinition Height="2*"/>
        </Grid.RowDefinitions>

        <GroupBox Grid.Row="0" Header="接收区" Foreground="{DynamicResource ForegroundBrush}" BorderBrush="{DynamicResource SidebarBgBrush}">
            <Grid>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="*"/>
                </Grid.RowDefinitions>
                <StackPanel Orientation="Horizontal" Margin="2">
                    <RadioButton Content="ASCII" IsChecked="{Binding ReceiveAsHex, Converter={StaticResource InverseBooleanConverter}, Mode=TwoWay}"
                                 Foreground="{DynamicResource ForegroundBrush}" Margin="0,0,8,0"/>
                    <RadioButton Content="HEX" IsChecked="{Binding ReceiveAsHex, Mode=TwoWay}"
                                 Foreground="{DynamicResource ForegroundBrush}"/>
                </StackPanel>
                <TextBox Grid.Row="1" x:Name="RecvBox" Text="{Binding ReceivedText, Mode=OneWay}"
                         FontFamily="Cascadia Code" FontSize="13"
                         Foreground="{DynamicResource RxTextBrush}"
                         Background="{DynamicResource MainBgBrush}"
                         VerticalScrollBarVisibility="Auto"
                         HorizontalScrollBarVisibility="Auto"
                         IsReadOnly="True" TextWrapping="NoWrap"/>
            </Grid>
        </GroupBox>

        <GridSplitter Grid.Row="1" Height="5" HorizontalAlignment="Stretch"
                      Background="{DynamicResource ActivityBarBgBrush}"/>

        <GroupBox Grid.Row="2" Header="发送区" Foreground="{DynamicResource ForegroundBrush}" BorderBrush="{DynamicResource SidebarBgBrush}">
            <Grid>
                <Grid.RowDefinitions>
                    <RowDefinition Height="Auto"/>
                    <RowDefinition Height="*"/>
                    <RowDefinition Height="Auto"/>
                </Grid.RowDefinitions>
                <StackPanel Orientation="Horizontal" Margin="2">
                    <RadioButton Content="ASCII" IsChecked="{Binding SendAsHex, Converter={StaticResource InverseBooleanConverter}, Mode=TwoWay}"
                                 Foreground="{DynamicResource ForegroundBrush}" Margin="0,0,8,0"/>
                    <RadioButton Content="HEX" IsChecked="{Binding SendAsHex, Mode=TwoWay}"
                                 Foreground="{DynamicResource ForegroundBrush}"/>
                </StackPanel>
                <TextBox Grid.Row="1" x:Name="SendBox" Text="{Binding SendText, Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
                         FontFamily="Cascadia Code" FontSize="13"
                         Foreground="{DynamicResource TxTextBrush}"
                         Background="{DynamicResource MainBgBrush}"
                         VerticalScrollBarVisibility="Auto"
                         AcceptsReturn="True" TextWrapping="NoWrap"/>
                <StackPanel Grid.Row="2" Orientation="Horizontal" HorizontalAlignment="Right" Margin="2">
                    <Button Content="发送" Width="80" Height="28" Background="{DynamicResource AccentBrush}"
                            Foreground="White" Command="{Binding SendCommand}"/>
                </StackPanel>
            </Grid>
        </GroupBox>
    </Grid>
</UserControl>
```

注意：需要在 App.xaml 中添加 `InverseBooleanConverter`（Task 12 一并处理）。如果暂时没有，可以先去掉 RadioButton 的 Converter，用代码切换。

- [ ] **Step 2: 临时简化（避免 InverseBooleanConverter 编译错误）**

修改 RadioButton 的 IsChecked 绑定为 `Mode=OneWay`，并用单独 bool 属性 `ReceiveAsAscii = !ReceiveAsHex`。在 MainViewModel 添加：

```csharp
public bool ReceiveAsAscii { get => !ReceiveAsHex; set => ReceiveAsHex = !value; }
public bool SendAsAscii { get => !SendAsHex; set => SendAsHex = !value; }
```

（保证 INotifyPropertyChanged 通过 `OnReceiveAsHexChanged` 部分方法触发关联属性）

在 MainViewModel 中加：

```csharp
partial void OnReceiveAsHexChanged(bool value) => OnPropertyChanged(nameof(ReceiveAsAscii));
partial void OnSendAsHexChanged(bool value) => OnPropertyChanged(nameof(SendAsAscii));
```

XAML 改为：`IsChecked="{Binding ReceiveAsAscii, Mode=TwoWay}"` 和 `IsChecked="{Binding ReceiveAsHex, Mode=TwoWay}"`

- [ ] **Step 3: 验证编译**

Run: `dotnet build`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add SerialDebugAssistant/Views/Controls/DataDisplayPanel.xaml SerialDebugAssistant/ViewModels/MainViewModel.cs
git commit -m "feat: 实现收发显示区"
```

---

## Task 12: StatusBar 控件

**Files:**
- Modify: `SerialDebugAssistant/Views/Controls/StatusBar.xaml`

- [ ] **Step 1: 写 StatusBar.xaml**

```xml
<UserControl x:Class="SerialDebugAssistant.Views.Controls.StatusBar"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
             Height="28">
    <Grid>
        <Grid.Style>
            <Style TargetType="Grid">
                <Setter Property="Background" Value="{DynamicResource StatusBarDisconnectedBrush}"/>
                <Style.Triggers>
                    <DataTrigger Binding="{Binding IsConnected}" Value="True">
                        <Setter Property="Background" Value="{DynamicResource StatusBarConnectedBrush}"/>
                    </DataTrigger>
                </Style.Triggers>
            </Style>
        </Grid.Style>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="Auto"/>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="Auto"/>
            <ColumnDefinition Width="Auto"/>
            <ColumnDefinition Width="Auto"/>
        </Grid.ColumnDefinitions>

        <Ellipse Grid.Column="0" Width="10" Height="10" Fill="White" Margin="8,0"/>
        <TextBlock Grid.Column="1" Text="{Binding StatusMessage}"
                   Foreground="White" VerticalAlignment="Center" Margin="8,0,0,0"/>
        <TextBlock Grid.Column="2" Text="{Binding SelectedPort, StringFormat=' {0}'}"
                   Foreground="White" VerticalAlignment="Center" Margin="8,0"/>
        <TextBlock Grid.Column="3" Text="{Binding BaudRate, StringFormat=' {0}-8-N-1'}"
                   Foreground="White" VerticalAlignment="Center" Margin="8,0"/>
        <TextBlock Grid.Column="4" VerticalAlignment="Center" Margin="8,0">
            <Run Text="RX:" Foreground="White"/>
            <Run Text="{Binding RxByteCount}" Foreground="White"/>
            <Run Text="TX:" Foreground="White"/>
            <Run Text="{Binding TxByteCount}" Foreground="White"/>
        </TextBlock>
    </Grid>
</UserControl>
```

- [ ] **Step 2: 验证编译并运行**

Run: `dotnet build && dotnet run --project SerialDebugAssistant`
Expected: 窗口显示，状态栏显示"就绪 COM1 115200-8-N-1 RX:0 TX:0"

- [ ] **Step 3: Commit**

```bash
git add SerialDebugAssistant/Views/Controls/StatusBar.xaml
git commit -m "feat: 实现状态栏"
```

---

## Task 13: 日志保存服务

**Files:**
- Create: `SerialDebugAssistant/Services/LogService.cs`
- Modify: `SerialDebugAssistant/ViewModels/MainViewModel.cs`

- [ ] **Step 1: 写 LogService**

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using SerialDebugAssistant.Models;
using SerialDebugAssistant.Utils;

namespace SerialDebugAssistant.Services;

public class LogService
{
    private readonly LogSettings _settings;

    public LogService(LogSettings settings)
    {
        _settings = settings;
        Directory.CreateDirectory(settings.LogDirectory);
    }

    public async Task AppendAsync(ReceivedData data)
    {
        if (!_settings.AutoSave) return;
        var path = _settings.GetDailyLogFile(data.Timestamp);
        var line = _settings.IncludeTimestamp
            ? $"[{data.Timestamp:HH:mm:ss.fff}] {GetDisplayText(data)}{Environment.NewLine}"
            : $"{GetDisplayText(data)}{Environment.NewLine}";
        await File.AppendAllTextAsync(path, line);
    }

    private string GetDisplayText(ReceivedData data)
    {
        return _settings.UseHexFormat
            ? HexConverter.BytesToHexString(data.RawBytes)
            : HexConverter.BytesToAscii(data.RawBytes);
    }
}
```

- [ ] **Step 2: 在 MainViewModel 中接入日志**

修改 `OnDataReceived`：

```csharp
private readonly LogService _logService = new(new LogSettings { AutoSave = true });

private void OnDataReceived(object? sender, DataReceivedEventArgs e)
{
    RxByteCount += e.Data.Length;
    var text = ReceiveAsHex
        ? HexConverter.BytesToHexString(e.Data)
        : HexConverter.BytesToAscii(e.Data);
    var ts = e.Timestamp;
    var line = $"[RX {ts:HH:mm:ss.fff}] {text}\n";
    System.Windows.Application.Current?.Dispatcher.Invoke(() => ReceivedText += line);
    _ = _logService.AppendAsync(new ReceivedData
    {
        Timestamp = ts,
        Direction = DataDirection.Received,
        RawBytes = e.Data,
        DisplayText = text
    });
}
```

- [ ] **Step 3: 验证编译**

Run: `dotnet build`
Expected: Build succeeded

- [ ] **Step 4: Commit**

```bash
git add SerialDebugAssistant/Services/LogService.cs SerialDebugAssistant/ViewModels/MainViewModel.cs
git commit -m "feat: 实现日志自动保存"
```

---

## Task 14: Velopack 自动更新服务

**Files:**
- Create: `SerialDebugAssistant/Services/IUpdateService.cs`
- Create: `SerialDebugAssistant/Services/UpdateService.cs`
- Modify: `SerialDebugAssistant/Program.cs`

- [ ] **Step 1: 写 IUpdateService**

```csharp
using System.Threading.Tasks;

namespace SerialDebugAssistant.Services;

public interface IUpdateService
{
    Task<UpdateInfo?> CheckForUpdatesAsync();
    Task DownloadAndInstallUpdateAsync();
}

public class UpdateInfo
{
    public string Version { get; set; } = string.Empty;
    public string ReleaseNotes { get; set; } = string.Empty;
}
```

- [ ] **Step 2: 写 UpdateService**

```csharp
using System;
using System.Threading.Tasks;
using Velopack;

namespace SerialDebugAssistant.Services;

public class UpdateService : IUpdateService
{
    private readonly UpdateManager _mgr;

    public UpdateService()
    {
        _mgr = new UpdateManager("https://github.com/2824418868-cpu/SerialDebugAssistant/releases/latest");
    }

    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        try
        {
            var v = await _mgr.CheckForUpdatesAsync();
            if (v is null) return null;
            return new UpdateInfo
            {
                Version = v.TargetFullRelease.Version.ToString(),
                ReleaseNotes = v.GetReleaseNotes() ?? string.Empty
            };
        }
        catch
        {
            return null;
        }
    }

    public async Task DownloadAndInstallUpdateAsync()
    {
        var v = await _mgr.CheckForUpdatesAsync();
        if (v is null) return;
        await _mgr.DownloadUpdatesAsync(v);
        _mgr.ApplyUpdatesAndRestart(v);
    }
}
```

- [ ] **Step 3: 修改 Program.cs 增加 Velopack hook**

```csharp
using System;
using Velopack;
using System.Windows;

namespace SerialDebugAssistant;

public static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();
        var app = new App();
        app.InitializeComponent();
        app.Run();
    }
}
```

（保持原样，VelopackApp.Build().Run() 已就绪）

- [ ] **Step 4: 在 MainViewModel 中接入启动检查**

在 MainViewModel 构造函数末尾加：

```csharp
_ = CheckUpdatesOnStartupAsync();
```

并添加：

```csharp
private readonly IUpdateService _updateService = new UpdateService();
[ObservableProperty] private bool _updateAvailable;
[ObservableProperty] private string _updateVersion = string.Empty;

private async Task CheckUpdatesOnStartupAsync()
{
    var info = await _updateService.CheckForUpdatesAsync();
    if (info != null)
    {
        UpdateAvailable = true;
        UpdateVersion = info.Version;
        StatusMessage = $"发现新版本: {info.Version}";
    }
}

[RelayCommand]
public async Task ApplyUpdateAsync()
{
    await _updateService.DownloadAndInstallUpdateAsync();
}
```

- [ ] **Step 5: 验证编译**

Run: `dotnet build`
Expected: Build succeeded

- [ ] **Step 6: Commit**

```bash
git add SerialDebugAssistant/Services/IUpdateService.cs SerialDebugAssistant/Services/UpdateService.cs SerialDebugAssistant/ViewModels/MainViewModel.cs
git commit -m "feat: 接入 Velopack 自动更新"
```

---

## Task 15: 发布与打包配置

**Files:**
- Create: `installer.iss`
- Create: `RELEASE_NOTES.md`
- Create: `SerialDebugAssistant/app.manifest`

- [ ] **Step 1: 写 app.manifest（管理员权限 + DPI 感知）**

```xml
<?xml version="1.0" encoding="utf-8"?>
<assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1">
  <assemblyIdentity version="0.1.0.0" name="SerialDebugAssistant"/>
  <trustInfo xmlns="urn:schemas-microsoft-com:asm.v2">
    <security>
      <requestedPrivileges xmlns="urn:schemas-microsoft-com:asm.v3">
        <requestedExecutionLevel level="asInvoker" uiAccess="false"/>
      </requestedPrivileges>
    </security>
  </trustInfo>
  <application xmlns="urn:schemas-microsoft-com:asm.v3">
    <windowsSettings>
      <dpiAwareness xmlns="http://schemas.microsoft.com/SMI/2019/WindowsManifest">PerMonitorV2</dpiAwareness>
    </windowsSettings>
  </application>
</assembly>
```

- [ ] **Step 2: 写 RELEASE_NOTES.md**

```markdown
# Release Notes

## v0.1.0 - 2026-08-25

首版基础功能：
- 串口参数配置（端口/波特率/数据位/停止位/校验/流控）
- 收发显示，HEX/ASCII 切换
- 接收时间戳
- 自动日志保存
- Velopack 自动更新
```

- [ ] **Step 3: 写 installer.iss（Inno Setup 脚本）**

```ini
[Setup]
AppName=Serial Debug Assistant
AppVersion=0.1.0
AppPublisher=2824418868-cpu
DefaultDirName={pf}\SerialDebugAssistant
DefaultGroupName=Serial Debug Assistant
UninstallDisplayIcon={app}\SerialDebugAssistant.exe
Compression=lzma2
SolidCompression=yes
OutputDir=.\Output
OutputBaseFilename=SerialDebugAssistantSetup
ArchitecturesAllowed=x64
ArchitecturesInstallIn64BitMode=x64

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "附加任务:"; Flags: checkableonce

[Files]
Source: "SerialDebugAssistant\bin\Release\net8.0\publish\win-x64\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs

[Icons]
Name: "{group}\串口调试助手"; Filename: "{app}\SerialDebugAssistant.exe"
Name: "{group}\卸载串口调试助手"; Filename: "{unficon}\Unins000.exe"
Name: "{commondesktop}\串口调试助手"; Filename: "{app}\SerialDebugAssistant.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\SerialDebugAssistant.exe"; Description: "立即启动"; Flags: nowait postinstall skipifsilent
```

- [ ] **Step 4: 执行发布构建**

```bash
dotnet publish SerialDebugAssistant/SerialDebugAssistant.csproj -c Release \
  -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishReadyToRun=true
```
Expected: 在 `bin/Release/net8.0/publish/win-x64/` 生成单文件 exe（约 30MB）

- [ ] **Step 5: Velopack 打包**

```bash
vpk pack -u SerialDebugAssistant -v 0.1.0 -p SerialDebugAssistant/bin/Release/net8.0/publish/win-x64 -e SerialDebugAssistant.exe -r Output
```
Expected: 在 `Output/` 生成 `SerialDebugAssistant-0.1.0-full.nupkg`

- [ ] **Step 6: 用 Inno Setup 编译 installer.iss**

```bash
iscc installer.iss
```
Expected: 在 `Output/` 生成 `SerialDebugAssistantSetup.exe`

- [ ] **Step 7: Commit**

```bash
git add installer.iss RELEASE_NOTES.md SerialDebugAssistant/app.manifest
git commit -m "chore: 添加发布打包配置"
```

---

## Task 16: 集成验证（手动）

**Files:** 无修改

- [ ] **Step 1: 全量测试**

Run: `dotnet test SerialDebugAssistant.sln`
Expected: 全部测试 PASS

- [ ] **Step 2: 手动功能验证（用 com0com 虚拟串口对）**

1. 用 com0com 创建虚拟串口对 `COM10 <-> COM11`
2. 启动本软件，打开 COM10
3. 用其他工具（如友善串口助手）打开 COM11
4. 验证：COM11 发"hello"，本软件 COM10 能收到并显示
5. 验证：本软件切换为 HEX 模式发送 `41 42 43`，COM11 收到 `ABC`
6. 验证：拔掉 COM10 时，状态栏变红，连接自动断开
7. 验证：日志文件生成在 `Documents/SerialDebugAssistant/Logs/2026-08-25.txt`

- [ ] **Step 3: 最终 Commit**

```bash
git add -A
git commit -m "chore: v0.1.0 集成验证通过"
```

---

## Self-Review 检查

**Spec 覆盖：**
- 第 2 节技术栈 → Task 1 ✅
- 第 3 节项目结构 → Task 1-13 ✅
- 第 4 节 UI 布局 → Task 8-12 ✅
- 第 5.1 串口配置 → Task 4, 10 ✅
- 第 5.2 串口操作 → Task 6 ✅
- 第 5.3 数据收发 → Task 7, 11 ✅
- 第 5.4 日志 → Task 13 ✅
- 第 5.5 状态栏 → Task 12 ✅
- 第 5.6 自动更新 → Task 14 ✅
- 第 6 错误处理 → Task 6 (ErrorOccurred 事件)、Task 7 (StatusMessage) ✅
- 第 7 测试 → Task 3, 4, 6, 7 ✅
- 第 8 发布 → Task 15 ✅

**类型一致性检查：**
- `ISerialService.OpenAsync` 返回 `Task<bool>` → MainViewModel 与测试调用一致 ✅
- `SerialPortConfig` 属性名（BaudRate/DataBits/StopBits/Parity/Handshake）在 Task 4 定义，Task 7 MainViewModel 与 Task 10 绑定一致 ✅
- `HexConverter` 方法名在 Task 3 定义后，Task 7 与 Task 13 调用一致 ✅
- `ReceivedData.Direction` 枚举在 Task 5 定义，Task 13 调用 `DataDirection.Received` 一致 ✅

**占位扫描：** 无 TBD/TODO/模糊描述 ✅

**Scope Check：** v0.1 范围聚焦单一，迭代路径在 spec 第 9 节说明 ✅

