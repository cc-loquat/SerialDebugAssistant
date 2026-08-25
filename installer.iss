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
Source: "SerialDebugAssistant\bin\Release\net8.0-windows\win-x64\publish\*"; DestDir: "{app}"; Flags: recursesubdirs createallsubdirs

[Icons]
Name: "{group}\串口调试助手"; Filename: "{app}\SerialDebugAssistant.exe"
Name: "{group}\卸载串口调试助手"; Filename: "{uninstallexe}"
Name: "{commondesktop}\串口调试助手"; Filename: "{app}\SerialDebugAssistant.exe"; Tasks: desktopicon

[Run]
Filename: "{app}\SerialDebugAssistant.exe"; Description: "立即启动"; Flags: nowait postinstall skipifsilent
