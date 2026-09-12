; Inno Setup Script for 万客隆 POS 智能收银系统
; Documentation: https://jrsoftware.org/isinfo.php

#ifndef MyAppVersion
  #define MyAppVersion "0.1.0"
#endif

#ifndef OutputBaseFilename
  #define OutputBaseFilename "WanKePos_Setup_v" + MyAppVersion
#endif

#define MyAppName "万客隆 POS 智能收银系统"
#define MyAppPublisher "万客隆软件"
#define MyAppExeName "WanKePos.WinUI.exe"

[Setup]
; 基础应用信息
AppId={{C8A53E2B-914F-4C8E-98DF-B3F78652D819}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} v{#MyAppVersion}
AppPublisher={#MyAppPublisher}
DefaultDirName={autopf}\WanKePos
DefaultGroupName={#MyAppName}
AllowNoIcons=yes
OutputDir=..\output_installer
OutputBaseFilename={#OutputBaseFilename}
SetupIconFile=pos_icon.ico
UninstallDisplayIcon={app}\Assets\pos_icon.ico
Compression=lzma2/ultra64
SolidCompression=yes
ChangesAssociations=yes

; 现代化 UI 向导设置
WizardStyle=modern
WizardSizePercent=100
DisableWelcomePage=no
DisableProgramGroupPage=yes
DisableReadyPage=yes

ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; 界面语言与显示
ShowLanguageDialog=no

[Languages]
Name: "chinesesimplified"; MessagesFile: "ChineseSimplified.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: checkedonce

[Files]
; 打包 publish_winui 目录下的所有程序文件及运行依赖，排除运行时生成的日志与数据库临时锁文件
Source: "..\publish_winui\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.db-shm,*.db-wal,*.log"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\pos_icon.ico"; WorkingDir: "{app}"; AppUserModelID: "WanKePos.SmartPOS.App"
Name: "{group}\卸载 {#MyAppName}"; Filename: "{uninstallexe}"; IconFilename: "{app}\Assets\pos_icon.ico"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\pos_icon.ico"; Tasks: desktopicon; WorkingDir: "{app}"; AppUserModelID: "WanKePos.SmartPOS.App"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent; WorkingDir: "{app}"
