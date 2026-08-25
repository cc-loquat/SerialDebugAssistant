# 串口调试助手 设计文档

- **日期**：2026-08-25
- **目标平台**：Windows 10/11 桌面
- **当前版本**：v0.1 基础版
- **作者**：黄意杰

---

## 1. 项目目标

开发一款 Windows 桌面端串口调试助手，采用 VSCode 风格界面，以 exe 安装包形式分发，支持自动更新。v0.1 实现基础串口调试功能，后续按迭代路径扩展。

### 1.1 目标用户

- 嵌入式开发工程师：调试 MCU 串口输出
- 硬件爱好者：与各类串口设备交互
- 学生：学习串口通信

### 1.2 非目标（v0.1 不做）

- 多串口同时打开
- 脚本/宏
- 协议解析（MODBUS 等）
- 数据曲线显示
- 跨平台
- 移动端

---

## 2. 技术栈

| 组件 | 选型 | 版本 | 用途 |
|---|---|---|---|
| 运行时 | .NET | 8 (LTS) | 框架 |
| UI 框架 | WPF | — | 桌面 UI |
| 语言 | C# | 12 | — |
| MVVM | CommunityToolkit.Mvvm | 8.x | ViewModel 基础 |
| 串口库 | System.IO.Ports | 8.x | 串口通信 |
| 自动更新 | Velopack | 0.x | GitHub Release 自动更新 |
| UI 控件库 | HandyControl | 3.x | UI 控件（VSCode 风配色自定义） |
| 打包 | Inno Setup | 6 | 生成 exe 安装包 |
| 字体 | Cascadia Code | — | 等宽字体（VSCode 默认） |

### 2.1 发布形态

- **自包含发布**（`--self-contained`），用户免装 .NET 8 运行时
- 安装包预计 25-35 MB
- 输出 `SerialDebugAssistantSetup.exe`

### 2.2 商标与协议注意

- 可参考 VSCode 的设计思想（布局、配色、交互逻辑），但**不可**使用 VSCode 名称、Logo 作为产品标识
- 产品名定为 **串口调试助手 / Serial Debug Assistant**

---

## 3. 项目结构

```
SerialDebugAssistant/
├── Models/
│   ├── SerialPortConfig.cs         # 串口参数模型
│   ├── ReceivedData.cs             # 接收数据帧模型
│   └── LogSettings.cs              # 日志设置模型
├── Services/
│   ├── ISerialService.cs           # 串口服务接口（便于测试 mock）
│   ├── SerialService.cs            # 串口收发实现
│   ├── IUpdateService.cs           # 更新服务接口
│   ├── UpdateService.cs            # Velopack 自动更新实现
│   └── PortEnumerator.cs           # 可用串口枚举
├── ViewModels/
│   ├── MainViewModel.cs            # 主窗口 VM
│   └── ViewModels/...              # 其他子 VM（按需拆分）
├── Views/
│   ├── MainWindow.xaml             # 主窗口
│   ├── Controls/
│   │   ├── ActivityBar.xaml        # 左侧活动栏（图标）
│   │   ├── SidebarPanel.xaml       # 侧边栏（串口参数）
│   │   ├── DataDisplayPanel.xaml   # 收发显示区
│   │   └── StatusBar.xaml          # 底部状态栏
│   └── Dialogs/
│       └── AboutDialog.xaml        # 关于对话框
├── Themes/
│   ├── VSCodeTheme.xaml            # VSCode 配色资源字典
│   └── Colors.xaml                 # 颜色常量
├── Utils/
│   ├── HexConverter.cs             # HEX/ASCII 互转
│   └── RelayCommand.cs             # 命令基类（若不直接用 CommunityToolkit）
├── App.xaml
├── App.xaml.cs
└── appsettings.json                # 用户配置（最后路径、参数等）
```

### 3.1 单元职责

| 单元 | 职责 | 依赖 |
|---|---|---|
| `SerialPortConfig` | 持有串口参数（端口、波特率、数据位、停止位、校验） | 无 |
| `ISerialService` / `SerialService` | 封装 `System.IO.Ports.SerialPort`，提供打开/关闭/收发/状态变更事件 | `SerialPortConfig` |
| `UpdateService` | 启动检查 + 手动检查 GitHub Release，借助 Velopack 下载安装 | Velopack |
| `MainViewModel` | 持有 UI 状态，协调 Services，暴露 `ICommand` 给 View 绑定 | `ISerialService`、`IUpdateService` |
| `Views/*` | 纯 UI，无业务逻辑 | `MainViewModel` |

---

## 4. UI 设计

### 4.1 布局

```
┌────┬─────────────┬──────────────────────────────┐
│ ▣  │ 串口参数    │                              │
│ 📋 │ ├ 端口 COM3 │      接收/发送显示区          │
│ ⚙  │ ├ 波特 11520│      [HEX] [ASCII] 切换       │
│ 📊 │ ├ 数据 8    │      ←—— 数据流 ——→           │
│ 📁 │ ├ 停止 1    │                              │
│    │ ├ 校验 None │                              │
│    │ └ 流控 None │                              │
│    │             │                              │
│    │ [打开] [清空]│                              │
├────┴─────────────┴──────────────────────────────┤
│ ● 已连接 COM3  115200-8-N-1  RX:1234 TX:56    ⏺ │
└──────────────────────────────────────────────────┘
```

- **左侧活动栏**（48px 宽）：图标按钮切换侧边栏面板（串口参数 / 日志 / 设置 / 关于）
- **侧边栏**（240px 宽，可折叠）：参数配置面板
- **中间显示区**（自适应）：接收区（上） + 发送区（下），可调高度比例
- **底部状态栏**（28px 高）：连接状态指示灯 + 参数摘要 + RX/TX 字节计数 + 自动更新提示

### 4.2 配色（VSCode Dark+）

| 用途 | 颜色 |
|---|---|
| 主背景 | `#1e1e1e` |
| 活动栏背景 | `#333333` |
| 侧边栏背景 | `#252526` |
| 编辑区背景 | `#1e1e1e` |
| 状态栏背景 | `#007acc`（已连接）/ `#1e1e1e`（未连接） |
| 强调色 | `#007acc` |
| 前景文字 | `#cccccc` |
| 接收文字色 | `#d4d4d4` |
| 发送文字色 | `#569cd6` |
| 错误色 | `#f44747` |
| 成功色 | `#4ec9b0` |

### 4.3 交互

- **端口下拉**：启动时自动枚举 + 点击刷新按钮
- **波特率**：下拉预设（9600/19200/38400/57600/115200/230400/460800/921600）+ 自定义输入
- **HEX/ASCII 切换**：分段控件，立即生效
- **接收区**：自动滚动到底部（可锁定滚动）
- **发送**：Ctrl+Enter 快捷发送
- **清空**：清空接收区或发送区
- **打开/关闭**：按钮文案切换，状态栏同步
- **自动更新**：启动后台静默检查，有新版时状态栏右侧显示 ↑ 图标，点击查看详情

---

## 5. 核心功能（v0.1）

### 5.1 串口配置

- 端口：自动枚举可用 COM 口
- 波特率：预设 + 自定义（1200 ~ 921600）
- 数据位：5 / 6 / 7 / 8
- 停止位：1 / 1.5 / 2
- 校验：None / Even / Odd / Mark / Space
- 流控：None / Hardware (RTS/CTS) / Software (XOn/XOff)

### 5.2 串口操作

- 打开 / 关闭
- 状态变更事件（连接 / 断开 / 错误）
- 意外断开自动检测，UI 状态同步

### 5.3 数据收发

**接收**：
- HEX / ASCII 显示切换
- 接收时间戳（可开关）
- 自动滚动（可锁定）
- 清空接收区

**发送**：
- 输入框（多行）
- HEX / ASCII 发送模式切换
- 末尾换行：无 / \r / \n / \r\n（可选）
- 定时发送：间隔 100ms ~ 60s，可启停
- 清空发送区
- Ctrl+Enter 快捷发送

### 5.4 日志

- 自动保存接收数据到文件
- 路径可配置（默认 `~/Documents/SerialDebugAssistant/Logs/`）
- 格式：`.txt`（ASCII 模式）/ `.hex`（HEX 模式）
- 文件按日期切分：`YYYY-MM-DD.txt`
- 启动自动继续 / 手动启停

### 5.5 状态栏

- 连接状态指示灯（红/绿/黄）
- 当前参数摘要：`COM3 115200-8-N-1`
- RX 字节累计 / TX 字节累计
- 自动更新提示图标（有新版时）

### 5.6 自动更新

- 启动时后台检查 GitHub Release 最新版本
- 有新版：状态栏显示 ↑ 图标，点击弹出更新对话框（版本号 + 更新说明 + 立即更新/稍后）
- 用户确认后 Velopack 下载差异包，安装后自动重启
- 菜单手动检查更新

---

## 6. 错误处理

| 场景 | 处理 |
|---|---|
| 端口不存在 / 被占用 | 弹出 MessageBox 提示原因，状态栏黄灯 |
| 无权限（驱动问题） | 提示"无法打开串口，请检查驱动或权限" |
| HEX 输入格式错误 | 输入框红色边框 + Tooltip 提示，禁用发送 |
| 串口意外断开 | 自动捕获 `ErrorReceived` 事件，UI 切回未连接，状态栏红灯 + 错误信息 |
| 日志写入失败 | 状态栏显示警告图标，不阻断使用 |
| 自动更新失败 | 静默失败，不影响主程序，手动重试 |

---

## 7. 测试策略

### 7.1 单元测试（xUnit）

- `HexConverter`：HEX/ASCII 互转边界情况（空串、奇数位、非法字符、空格）
- `MainViewModel`：mock `ISerialService`，验证打开/关闭/发送/接收命令的状态切换
- `SerialPortConfig`：参数验证

### 7.2 集成测试

- 用虚拟串口对（com0com）做真实收发测试
- 验证高波特率（921600）下不丢包
- 验证长时间运行稳定性（持续 30 分钟接收）

### 7.3 手动 UI 验证

- 端口热插拔：拔插 USB-TTL 时端口列表能刷新
- 主题切换（深色）
- 不同分辨率（HD/FHD/4K）下布局正常
- 最小窗口尺寸限制（防止 UI 错乱）

---

## 8. 发布与分发

### 8.1 构建流程

```
dotnet publish SerialDebugAssistant.csproj -c Release \
  -r win-x64 \
  --self-contained true \
  -p:PublishSingleFile=true \
  -p:PublishReadyToRun=true
```

输出目录：`bin/Release/net8.0/publish/win-x64/`

### 8.2 安装包

使用 Inno Setup 脚本 `installer.iss`：
- 复制 publish 产物到 `Program Files/SerialDebugAssistant/`
- 创建开始菜单快捷方式 + 桌面快捷方式（可选）
- 关联 `.hex` 文件（可选）
- 卸载入口

输出：`SerialDebugAssistantSetup.exe`（预计 25-35 MB）

### 8.3 自动更新

- 本地 Velopack 打包：`vpk pack` 生成 `.nupkg` 差异包
- 上传到 GitHub Release（带版本号 tag）
- 客户端启动时 `UpdateManager.CheckForUpdatesAsync()` 检查
- 有新版下载差异包，应用后重启

### 8.4 发布清单

- `SerialDebugAssistantSetup.exe`
- `RELEASE_NOTES.md`（版本说明）
- 源码 zip（可选）

---

## 9. 迭代路径

| 版本 | 主要内容 |
|---|---|
| **v0.1（当前）** | 基础串口调试：收发 + 配置 + 日志 + 自动更新 |
| v0.2 | 多串口同时打开、定时发送、报文模板 |
| v0.3 | 脚本/宏、CSV 导出 |
| v0.4 | MODBUS RTU 协议解析 |
| v0.5 | 数据曲线显示（串口示波器） |
| v0.6 | 自定义协议插件系统 |

---

## 10. 开放问题

- **GitHub Release 仓库**：需创建一个公开 GitHub 仓库用于发布（用户已有 GitHub 账号，仓库地址待定）
- **数字签名**：是否购买代码签名证书（避免 SmartScreen 警告）？v0.1 可暂不做，后续视用户反馈决定
- **图标设计**：应用图标待定，先使用占位图标
