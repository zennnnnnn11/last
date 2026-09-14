; ==============================================================================
; last 英雄联盟对局助手 - Inno Setup 打包脚本
; 支持无管理员权限安装（PrivilegesRequired=lowest），零 UAC 提权弹窗打扰
; ==============================================================================

#define MyAppName "last"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "zennnnnnn11"
#define MyAppURL "https://github.com/zennnnnnn11/last"
#define MyAppExeName "last.exe"
#define SourceDir "..\src\last\bin\Release\net10.0\win-x64\publish"

[Setup]
; 基础应用信息
AppId={{D38F0A27-724B-4E7B-8B41-B73C4E3D211A}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}
AppUpdatesURL={#MyAppURL}

; 安装路径：默认用户级目录，无需管理员权限，避免 UAC 弹窗
DefaultDirName={localappdata}\Programs\{#MyAppName}
DefaultGroupName={#MyAppName}
PrivilegesRequired=lowest
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; 安装包生成设置
OutputDir=..\dist
OutputBaseFilename=last-v{#MyAppVersion}-setup-x64
SetupIconFile=..\src\last\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

; 界面与行为
DisableProgramGroupPage=yes
CloseApplications=yes
RestartApplications=no

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
; 核心二进制文件（Native AOT 运行依赖，排除调试 PDB）
Source: "{#SourceDir}\last.exe"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\av_libglesv2.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\libHarfBuzzSharp.dll"; DestDir: "{app}"; Flags: ignoreversion
Source: "{#SourceDir}\libSkiaSharp.dll"; DestDir: "{app}"; Flags: ignoreversion

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"
Name: "{group}\{cm:UninstallProgram,{#MyAppName}}"; Filename: "{uninstallexe}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\{#MyAppExeName}"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent
