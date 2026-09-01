; TinyGUI Inno Setup 安装脚本
; 编译方法（二选一）：
;   1) 安装 Inno Setup 6 (https://jrsoftware.org/isinfo.php)，右键本文件 -> Compile
;   2) 命令行：iscc TinyGUI-setup.iss
; 前提：本 .iss 与 net48\ 文件夹放在同一目录（net48 为 dotnet publish -c Release 产物）
; 说明：安装到 Program Files；因该目录标准用户不可写，应用会自动把设置回退到
;       %LocalAppData%\TinyGUI\settings.json（AppSettings 的便携+回退逻辑已处理）。
; 体积：LZMA2 压缩后安装包约 3-4M，对方无需安装任何运行时（Win10/11 自带 .NET Framework 4.8）。

#define MyAppName "TinyGUI"
; 版本号直接从编译产物读取（ISPP 的 GetFileVersion），与 csproj 的 <Version> 单一同源，
; 改版本只动 csproj 一行，安装包自动同步，避免 1.0.0.0 写死造成的脱节。
#define MyAppVersion GetFileVersion("net48\TinyGUI.exe")
#define MyAppPublisher "Tony"
#define MyAppURL "https://tinypng.com/developers"
#define MyAppExeName "TinyGUI.exe"

[Setup]
AppId=A1B2C3D4-E5F6-4A7B-8C9D-0E1F2A3B4C5D
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
; 控制面板「程序和功能」里显示的版本信息，随 MyAppVersion 自动同步
VersionInfoVersion={#MyAppVersion}
VersionInfoCompany={#MyAppPublisher}
VersionInfoDescription={#MyAppName} - TinyPNG 图片压缩工具
DefaultDirName={autopf}\{#MyAppName}
DefaultGroupName={#MyAppName}
DisableProgramGroupPage=no
OutputDir=.
OutputBaseFilename=TinyGUI-setup
Compression=lzma2
SolidCompression=yes
WizardStyle=modern
ArchitecturesAllowed=x64 x86
ArchitecturesInstallIn64BitMode=x64
; 以当前用户权限安装即可（设置写在用户 AppData，不写 Program Files）
PrivilegesRequired=lowest

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"
Name: "chinese"; MessagesFile: "compiler:Languages\ChineseSimplified.isl"

[Files]
Source: "net48\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式"; GroupDescription: "额外任务:"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "启动 {#MyAppName}"; Flags: nowait postinstall skipifsilent
