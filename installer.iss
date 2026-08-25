; SerialDebugAssistant 安装程序 — Inno Setup 6
; 双击安装，支持选择安装路径、同意协议、创建快捷方式

#define MyAppName "SerialDebugAssistant"
#define MyAppVersion "0.1.1"
#define MyAppPublisher "SerialDebugAssistant"
#define MyAppURL "https://github.com/cc-loquat/SerialDebugAssistant"
#define MyAppExeName "SerialDebugAssistant.exe"

[Setup]
AppId={{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=yes
AllowNoIcons=yes
PrivilegesRequired=admin
OutputBaseFilename=SerialDebugAssistant-Setup-v{#MyAppVersion}
SetupIconFile=C:\Users\28244\Desktop\串口调试助手开发\SerialDebugAssistant\icon.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra
SolidCompression=yes
WizardStyle=modern
; 不显示完成页面上的"运行"选项（我们默认启动）
; DisableFinishedPage=no

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "快捷方式："; Flags: checkedonce

[Files]
Source: "C:\Users\28244\Desktop\串口调试助手开发\publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\卸载 {#MyAppName}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 {#MyAppName}"; Flags: nowait postinstall skipifsilent

[Code]
function InitializeSetup: Boolean;
begin
  Result := True;
end;