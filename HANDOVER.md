# 串口调试助手 - 开发交接文档

> **最后更新**：2026-08-25
> **当前版本**：v0.1.0
> **仓库**：https://github.com/cc-loquat/SerialDebugAssistant
> **分支**：main

---

## 1. 项目概述

Windows 桌面串口调试助手，VSCode Dark Modern 风界面，用于嵌入式开发、硬件调试、串口通信学习。

### 技术栈
- **.NET 8 (LTS)** + **WPF** + **C# 12**
- **CommunityToolkit.Mvvm 8.2.2** —— MVVM 框架（源生成器 `[ObservableProperty]` / `[RelayCommand]`）
- **System.IO.Ports 8.0.0** —— 串口通信
- **Velopack 0.0.942** —— 自动更新（GitHub Release 检查）
- **xUnit 2.9.0** + **Moq 4.20.70** —— 单元测试
- **Inno Setup 6** —— 安装包打包

### 发布形态
- 自包含单文件 exe（`--self-contained true -p:PublishSingleFile=true`）
- 约 165 MB（包含 .NET 运行时 + WPF 原生依赖）
- 5 个 native dll 必须与 exe 同目录（WPF 限制，无法合并）

---

## 2. 当前功能（v0.1）

### 已实现
- ✅ 串口参数配置（端口/波特率/数据位/停止位/校验/流控）
- ✅ 端口自动枚举 + 手动刷新
- ✅ HEX/ASCII 双模式收发（接收和发送独立切换）
- ✅ 接收时间戳（毫秒级）
- ✅ RX/TX 字节计数
- ✅ 自动日志保存（按日期切分，`Documents/SerialDebugAssistant/Logs/YYYY-MM-DD.txt`）
- ✅ Velopack 启动检查更新（指向 `https://github.com/cc-loquat/SerialDebugAssistant/releases/latest`）
- ✅ VSCode Dark Modern 配色（黑色主调，蓝色仅用于主按钮）
- ✅ 三栏布局（ActivityBar 48px / Sidebar 240px / DisplayArea 自适应）+ 状态栏

### 未实现（留给 v0.2+）
- ❌ 活动栏 4 个图标按钮（≡ ⋞ ⚙ i）未接线，点击无反应
- ❌ 多串口同时打开
- ❌ 定时发送
- ❌ 报文模板/宏
- ❌ MODBUS 协议解析
- ❌ 数据曲线显示
- ❌ 设置持久化（每次启动用默认值，不保存上次配置）
- ❌ 资源 Dispose（窗口关闭时 SerialPort 不一定立即释放）
- ❌ TX 数据未写日志（只有 RX 写）
- ❌ 发送快捷键（Ctrl+Enter）

---

## 3. 项目结构

```
SerialDebugAssistant/
├── SerialDebugAssistant/              # 主项目
│   ├── Models/
│   │   ├── SerialPortConfig.cs        # 串口参数模型 + 验证
│   │   ├── ReceivedData.cs            # 数据帧模型（Timestamp/Direction/RawBytes/DisplayText）
│   │   └── LogSettings.cs             # 日志设置（路径/格式/时间戳开关）
│   ├── Services/
│   │   ├── ISerialService.cs          # 串口服务接口 + 事件参数类
│   │   ├── SerialService.cs           # SerialPort 封装实现
│   │   ├── LogService.cs              # 日志自动保存
│   │   ├── IUpdateService.cs          # 更新服务接口 + UpdateInfo DTO
│   │   └── UpdateService.cs           # Velopack 实现
│   ├── ViewModels/
│   │   ├── ViewModelBase.cs           # 继承 ObservableObject
│   │   └── MainViewModel.cs           # 主 VM（协调所有服务）
│   ├── Views/
│   │   ├── MainWindow.xaml(.cs)       # 主窗口（5 列 Grid 布局）
│   │   └── Controls/
│   │       ├── ActivityBar.xaml(.cs)  # 左侧活动栏（4 个图标）
│   │       ├── SidebarPanel.xaml(.cs) # 串口参数面板
│   │       ├── DataDisplayPanel.xaml(.cs) # 收发显示区
│   │       └── StatusBar.xaml(.cs)    # 底部状态栏
│   ├── Themes/
│   │   ├── Colors.xaml                # 颜色/画笔资源字典
│   │   └── VSCodeTheme.xaml           # 控件样式（按钮/输入框/GroupBox 等）
│   ├── Utils/
│   │   └── HexConverter.cs            # HEX/ASCII/byte[] 互转
│   ├── App.xaml(.cs)                  # 应用入口（合并主题字典）
│   ├── Program.cs                     # Velopack 入口（自定义 Main）
│   └── app.manifest                   # DPI 感知 + asInvoker
├── SerialDebugAssistant.Tests/        # 测试项目（33 个测试）
│   ├── HexConverterTests.cs           # 16 个
│   ├── SerialPortConfigTests.cs       # 11 个
│   ├── MainViewModelTests.cs          # 4 个
│   └── SerialServiceIntegrationTests.cs # 2 个
├── SerialDebugAssistant.sln
├── installer.iss                      # Inno Setup 安装包脚本
├── RELEASE_NOTES.md                   # 版本说明
└── docs/
    └── superpowers/
        ├── specs/2026-08-25-serial-debug-assistant-design.md     # 设计文档
        └── plans/2026-08-25-serial-debug-assistant-v0.1.md       # 实施计划
```

---

## 4. 关键代码位置

### 串口服务
- `SerialDebugAssistant/Services/SerialService.cs`
  - `OpenAsync` —— 开串口，失败返回 false（不抛异常），成功触发 `ConnectionChanged` 事件
  - `OnDataReceived` —— 在线程池线程触发，UI 更新需 Dispatcher.Marshal
  - `Dispose` —— sync-over-async（`CloseAsync().GetAwaiter().GetResult()`），可改进

### 主 ViewModel
- `SerialDebugAssistant/ViewModels/MainViewModel.cs`
  - 14 个 `[ObservableProperty]` 字段（SelectedPort/BaudRate/DataBits/StopBits/Parity/Handshake/IsConnected/ReceivedText/SendText/SendAsHex/ReceiveAsHex/RxByteCount/TxByteCount/StatusMessage + UpdateAvailable/UpdateVersion）
  - 5 个 `[RelayCommand]`：RefreshPorts / Connect / Send / ClearReceived / ApplyUpdate
  - `OnDataReceived` —— UI 线程 marshaling + 日志写入
  - `CheckUpdatesOnStartupAsync` —— 启动时 fire-and-forget 检查更新

### UI 绑定
- `MainViewModel` 在 `MainWindow.xaml.cs` 构造时创建：`DataContext = new MainViewModel(new SerialService())`
- **未用 DI 容器**，UpdateService/LogService 在 VM 里硬编码 `new`（v0.2 可改进）

### 配色
- `Themes/Colors.xaml` —— 15+ 颜色和画刷，全部对齐 VSCode Dark Modern 源码
- `Themes/VSCodeTheme.xaml` —— 8 个复用样式（PrimaryButton/SecondaryButton/IconButton/ComboBox/GroupBox/RadioButton/TextBox/ActivityIcon）

### 自动更新 URL
- `SerialDebugAssistant/Services/UpdateService.cs:15`
  ```csharp
  _mgr = new UpdateManager("https://github.com/cc-loquat/SerialDebugAssistant/releases/latest");
  ```
  如果仓库改名，这里必须同步修改

---

## 5. 本地运行

### 开发调试
```bash
cd "C:/Users/28244/Desktop/串口调试助手开发"
dotnet run --project SerialDebugAssistant
```

### 单元测试
```bash
dotnet test SerialDebugAssistant.sln
```
当前 33/33 通过。

### 发布单文件 exe
```bash
dotnet publish SerialDebugAssistant/SerialDebugAssistant.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:PublishReadyToRun=true
```
输出：`SerialDebugAssistant/bin/Release/net8.0-windows/win-x64/publish/`

### 桌面运行副本
桌面 `串口调试助手/` 文件夹有 6 个文件（exe + 5 个 native dll），双击 `SerialDebugAssistant.exe` 即可运行。

---

## 6. 发布到 GitHub Release（自动更新流程）

### 前置工具
1. **Inno Setup**（生成安装包）：https://jrsoftware.org/isdl.php
2. **Velopack CLI**：`dotnet tool install -g vpk`

### 发布步骤
```bash
# 1. 打 tag
git tag v0.1.0
git push origin v0.1.0

# 2. 构建发布产物
dotnet publish SerialDebugAssistant/SerialDebugAssistant.csproj \
  -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true -p:PublishReadyToRun=true

# 3. 生成 Inno Setup 安装包
iscc installer.iss
# 输出: Output/SerialDebugAssistantSetup.exe

# 4. 生成 Velopack 更新包
vpk pack -u SerialDebugAssistant -v 0.1.0 \
  -p SerialDebugAssistant/bin/Release/net8.0-windows/win-x64/publish \
  -e SerialDebugAssistant.exe \
  -r Output

# 5. 上传到 GitHub Release
# - SerialDebugAssistantSetup.exe (安装包)
# - SerialDebugAssistant-0.1.0-full.nupkg (Velopack 更新包)
```

用户安装后，启动软件会后台检查 GitHub Release，有新版自动提示下载。

---

## 7. 已知问题

### Critical
无（v0.1 基础功能正常）

### Important（v0.2 应修复）
1. **资源未 Dispose** —— `MainViewModel` 订阅了 `ISerialService` 的 3 个事件但从不退订，窗口关闭时 `SerialPort` 不确定性释放。建议加 `Window.Closing` → 调用 `VM.Dispose()`。
2. **TX 未写日志** —— `MainViewModel.SendAsync` 只往 `ReceivedText` 追加 `[TX]`，没调 `_logService.AppendAsync`。日志只记 RX，不对称。
3. **SerialService.OnDataReceived 无 try/catch** —— `BaseStream.Read` 在 USB 拔出等场景会抛异常，当前静默吞掉，不触发 `ErrorOccurred`。
4. **LogService 并发不安全** —— 多次 `File.AppendAllTextAsync` 并发调用可能交错。建议加 `SemaphoreSlim`。
5. **状态栏参数显示冗余** —— 显示 `115200-8-None-One`，可改为短格式 `115200-8-N-1`（需要 value converter）。

### Minor
6. `Program.cs` 的 `args` 参数未使用
7. `MainViewModel` 里 `OnConnectionChanged` 和 `OnIsConnectedChanged` 都触发 `OnPropertyChanged(nameof(ConnectButtonText))`，冗余
8. `SerialService` 的 `ReadBufferSize/WriteBufferSize = 4096`，高波特率（921600）下可能不够
9. ActivityBar 4 个按钮纯装饰，无 Command 绑定
10. `ReceiveText` 无上限，高频接收会撑爆 TextBox

---

## 8. v0.2 规划建议

按优先级：

1. **资源 Dispose** —— `MainViewModel : IDisposable`，`Window.Closing` 调用
2. **设置持久化** —— `%AppData%/SerialDebugAssistant/settings.json`，保存上次端口/波特率/HEX 模式/窗口尺寸
3. **TX 日志对称** —— SendAsync 也调 LogService
4. **定时发送** —— 间隔可设，启停按钮
5. **活动栏按钮接线** —— 至少 ⚙ 设置面板（含设置持久化的 UI）
6. **Ctrl+Enter 发送快捷键**
7. **多串口支持** —— 每个 tab 一个串口（架构改动较大）
8. **报文模板** —— 常用报文保存/复用

---

## 9. 设计与实施文档

详细设计和实施计划在 `docs/superpowers/` 下（不推 GitHub，本地保留）：

- `specs/2026-08-25-serial-debug-assistant-design.md` —— 完整设计文档（架构/功能/UI/错误处理/测试/发布）
- `plans/2026-08-25-serial-debug-assistant-v0.1.md` —— 16 个任务的实施计划（TDD 步骤）

实施过程遵循 `superpowers` skill 体系：
- brainstorming → writing-plans → subagent-driven-development → finishing-a-development-branch
- 每个 task 走 TDD：test-first → verify fail → implement → verify pass → commit
- 每个 task 有 spec compliance review + code quality review 两阶段审查

---

## 10. 开发环境

- **OS**：Windows 11 Home China 10.0.26200
- **.NET SDK**：8.0.424（`C:/Program Files/dotnet/dotnet.exe`）
- **IDE**：VS Code（可选）/ 命令行
- **Git**：用户名 `cc-loquat`，邮箱 `cc-loquat@users.noreply.github.com`

### 配置注意
- `.gitignore` 已排除 `bin/ obj/ Output/ *.user .vs/`
- `.claude/settings.local.json` 是 Claude Code 本地配置，不推
- git 行尾警告（LF→CRLF）是 Windows 正常现象，不影响功能
