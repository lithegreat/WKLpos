; Inno Setup Script for 万客隆 POS 智能收银系统
; Documentation: https://jrsoftware.org/isinfo.php

#ifndef MyAppVersion
  #define MyAppVersion "0.2.0-preview"
#endif

#ifndef OutputBaseFilename
  #define OutputBaseFilename "WanKePos_Setup_v" + MyAppVersion
#endif

#ifndef SourceDir
  #define SourceDir "..\publish_winui_slim"
#endif

#ifndef CompressionLevel
  #define CompressionLevel "lzma2/fast"
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
Compression={#CompressionLevel}
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
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked

[InstallDelete]
; 升级时清理旧版自包含模式遗留的运行时核心 DLL 与库，避免干扰 Framework-Dependent 轻量模式
Type: files; Name: "{app}\coreclr.dll"
Type: files; Name: "{app}\clrjit.dll"
Type: files; Name: "{app}\hostfxr.dll"
Type: files; Name: "{app}\hostpolicy.dll"
Type: files; Name: "{app}\mscordaccore.dll"
Type: files; Name: "{app}\mscorrc.dll"
Type: files; Name: "{app}\clrcompression.dll"
Type: files; Name: "{app}\System.*.dll"
Type: files; Name: "{app}\Microsoft.CSharp.dll"
Type: files; Name: "{app}\Microsoft.VisualBasic*.dll"
Type: files; Name: "{app}\Microsoft.Win32*.dll"

[Files]
; 打包指定发布目录下的所有程序文件及运行依赖，排除运行时生成的日志与数据库临时锁文件
Source: "{#SourceDir}\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs; Excludes: "*.db-shm,*.db-wal,*.log,*.pdb"

[Icons]
Name: "{group}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\pos_icon.ico"; WorkingDir: "{app}"; AppUserModelID: "WanKePos.SmartPOS.App"
Name: "{group}\卸载 {#MyAppName}"; Filename: "{uninstallexe}"; IconFilename: "{app}\Assets\pos_icon.ico"; WorkingDir: "{app}"
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\pos_icon.ico"; Tasks: desktopicon; WorkingDir: "{app}"; AppUserModelID: "WanKePos.SmartPOS.App"

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#MyAppName}}"; Flags: nowait postinstall skipifsilent; WorkingDir: "{app}"

[Code]
procedure CurStepChanged(CurStep: TSetupStep);
var
  FindRec: TFindRec;
  ItemPath: string;
begin
  if CurStep = ssInstall then
  begin
    // 升级安装时自动清理旧版本可能残留的非必要语言子目录
    if FindFirst(ExpandConstant('{app}\*'), FindRec) then
    begin
      try
        repeat
          if (FindRec.Attributes and FILE_ATTRIBUTE_DIRECTORY <> 0) and
             (FindRec.Name <> '.') and (FindRec.Name <> '..') and
             (FindRec.Name <> 'zh-CN') and (FindRec.Name <> 'zh-TW') and
             (FindRec.Name <> 'en-us') and (FindRec.Name <> 'Microsoft.UI.Xaml') and
             (FindRec.Name <> 'Assets') and (FindRec.Name <> 'Templates') and
             (FindRec.Name <> 'runtimes') then
          begin
            ItemPath := ExpandConstant('{app}\') + FindRec.Name;
            DelTree(ItemPath, True, True, True);
          end;
        until not FindNext(FindRec);
      finally
        FindClose(FindRec);
      end;
    end;
  end;
end;

