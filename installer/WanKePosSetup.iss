; Inno Setup Script for 万客隆 POS 智能收银系统
; Documentation: https://jrsoftware.org/isinfo.php

#define MyAppName "万客隆 POS 智能收银系统"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "万客隆软件"
#define MyAppExeName "WanKePos.WinUI.exe"

[Setup]
; 基础应用信息
AppId={{C8A53E2B-914F-4C8E-98DF-B3F78652D819}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\WanKePos
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\output_installer
OutputBaseFilename=WanKePos_Setup_v1.0.0
SetupIconFile=pos_icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog
DisableProgramGroupPage=yes

; 界面语言与显示
ShowLanguageDialog=no

[Languages]
Name: "chinesesimp"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "创建桌面快捷方式 (&D)"; GroupDescription: "附加快捷方式:"; Flags: checkedonce

[Files]
; 打包 publish_winui 目录下的所有程序文件及运行依赖
Source: "..\publish_winui\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\pos_icon.ico"
Name: "{group}\卸载 {#MyAppName}"; Filename: "{uninstallexe}"; IconFilename: "{app}\Assets\pos_icon.ico"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\pos_icon.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "立即启动 {#MyAppName}"; Flags: nowait postinstall skipifsilent
