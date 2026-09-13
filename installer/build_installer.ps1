param(
    [string]$Version = "0.2.0",
    $RunInstaller = $true,
    [ValidateSet("FrameworkDependent", "SelfContained", "Both")]
    [string]$PackageMode = "FrameworkDependent",
    [ValidateSet("Fast", "Normal", "Max")]
    [string]$Speed = "Fast",
    [switch]$ForceRebuild = $false
)

$RunInstaller = [System.Convert]::ToBoolean($RunInstaller)

# 规范化版本号 (移除前导 v 或 V)
$cleanVersion = $Version.TrimStart('v', 'V')
if ([string]::IsNullOrWhiteSpace($cleanVersion)) {
    $cleanVersion = "0.2.0"
}

# 提取纯数字版本用于 Windows 文件与程序集属性 (如 0.2.0 -> 0.2.0.0)
$rawNumeric = ($cleanVersion -split '-')[0]
$parts = $rawNumeric -split '\.'
while ($parts.Length -lt 3) { $parts += "0" }
$numericVersion = "$($parts[0]).$($parts[1]).$($parts[2]).0"

# 根据打包速度档位设置 Inno Setup 压缩级别
$compressionLevel = switch ($Speed) {
    "Fast"   { "lzma2/fast" }
    "Normal" { "lzma2/normal" }
    "Max"    { "lzma2/ultra64" }
    Default  { "lzma2/fast" }
}

# 自动化构建万客隆 POS Windows Setup 安装包脚本
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  万客隆 POS 系统 - Windows 安装包打包流水线" -ForegroundColor Cyan
Write-Host "  目标版本: v$cleanVersion | 打包规格: $PackageMode | 压缩档位: $Speed ($compressionLevel)" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. 停止运行中的进程
Stop-Process -Name "WanKePos.WinUI" -Force -ErrorAction SilentlyContinue
Stop-Process -Name "WanKePos" -Force -ErrorAction SilentlyContinue
Stop-Process -Name "WanKePos_Setup*" -Force -ErrorAction SilentlyContinue
cmd /c "taskkill /F /IM WanKePos.WinUI.exe /T 2>nul" | Out-Null
cmd /c "taskkill /F /IM WanKePos.exe /T 2>nul" | Out-Null
Start-Sleep -Milliseconds 800

$rootDir = Split-Path -Parent $PSScriptRoot
Set-Location $rootDir

# 多语言本地化文件夹精简函数 (仅保留中文及核心系统目录，移除 80+ 个无用外语 MUI)
function Prune-UnusedLocales($targetDir) {
    if (-not (Test-Path $targetDir)) { return }
    $allowedDirs = @("zh-Hans", "zh-CN", "zh-TW", "en-US", "runtimes", "Assets", "Templates", "Microsoft.UI.Xaml")
    Get-ChildItem -Path $targetDir -Directory | ForEach-Object {
        if ($allowedDirs -notcontains $_.Name) {
            $muiFiles = Get-ChildItem -Path $_.FullName -Filter "*.mui" -ErrorAction SilentlyContinue
            if ($muiFiles.Count -gt 0) {
                Remove-Item -Path $_.FullName -Recurse -Force -ErrorAction SilentlyContinue
            }
        }
    }
}

# 2. 编译并发布 WinUI 3 程序
$dotnetCmd = "dotnet"
if (Test-Path "$env:USERPROFILE\.dotnet\dotnet.exe") {
    $dotnetCmd = "$env:USERPROFILE\.dotnet\dotnet.exe"
    $env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"
    $env:PATH = "$env:USERPROFILE\.dotnet;" + $env:PATH
} else {
    $env:PATH = "$env:PATH;" + [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH", "User")
}

# 检查源码或资产是否有变更（增量构建判断）
function Test-ProjectNeedsPublish($targetDir) {
    if ($ForceRebuild) { 
        Write-Host "检测到 -ForceRebuild 参数，执行全量编译发布。" -ForegroundColor Gray
        return $true 
    }
    $targetExe = Join-Path $targetDir "WanKePos.WinUI.exe"
    $targetDll = Join-Path $targetDir "WanKePos.WinUI.dll"
    if (-not (Test-Path $targetExe) -or -not (Test-Path $targetDll)) {
        return $true
    }

    try {
        $fileVer = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($targetExe).FileVersion
        if ($fileVer -ne $numericVersion) {
            Write-Host "检测到版本号变更 ($fileVer -> $numericVersion)，执行重新编译。" -ForegroundColor Gray
            return $true
        }
    } catch {
        return $true
    }

    $targetTime = (Get-Item $targetDll).LastWriteTime

    # 扫描 src 目录下的源码、配置与关键资产文件
    $srcFiles = Get-ChildItem -Path "$rootDir\src" -Recurse -File -ErrorAction SilentlyContinue | Where-Object {
        $_.Extension -in @(".cs", ".xaml", ".csproj", ".props", ".targets", ".json", ".manifest") -or 
        $_.FullName -like "*\Templates\*" -or 
        $_.FullName -like "*\Assets\*"
    }

    if ($srcFiles.Count -eq 0) { return $false }
    $maxSrcTime = ($srcFiles | Measure-Object -Property LastWriteTime -Maximum).Maximum

    if ($maxSrcTime -gt $targetTime) {
        Write-Host "检测到源码或资产有更新 (代码更新: $($maxSrcTime.ToString('HH:mm:ss')), 上次发布: $($targetTime.ToString('HH:mm:ss')))，触发增量发布。" -ForegroundColor Gray
        return $true
    }

    return $false
}

# 2.1 自包含版发布 (可选完整版 Self-Contained)
if ($PackageMode -eq "SelfContained" -or $PackageMode -eq "Both") {
    if (Test-ProjectNeedsPublish "$rootDir\publish_winui") {
        Write-Host "`n[1/3] 正在发布 WinUI 3 独立自包含免依赖程序 (Self-Contained)..." -ForegroundColor Yellow
        & $dotnetCmd publish src\WanKePos.WinUI\WanKePos.WinUI.csproj -c Release -r win-x64 --self-contained true --no-restore -o publish_winui /p:Version=$cleanVersion /p:AssemblyVersion=$numericVersion /p:FileVersion=$numericVersion
        if ($LASTEXITCODE -ne 0) {
            Write-Host "尝试全量还原发布..." -ForegroundColor Yellow
            & $dotnetCmd publish src\WanKePos.WinUI\WanKePos.WinUI.csproj -c Release -r win-x64 --self-contained true -o publish_winui /p:Version=$cleanVersion /p:AssemblyVersion=$numericVersion /p:FileVersion=$numericVersion
        }

        if ($LASTEXITCODE -ne 0) {
            Write-Host "自包含版本编译发布失败，请检查代码错误!" -ForegroundColor Red
            exit 1
        }

        # 清理临时锁文件、日志、PDB 与无用多语言资源
        Remove-Item -Path "$rootDir\publish_winui\*.db-shm" -Force -ErrorAction SilentlyContinue
        Remove-Item -Path "$rootDir\publish_winui\*.db-wal" -Force -ErrorAction SilentlyContinue
        Remove-Item -Path "$rootDir\publish_winui\*.log" -Force -ErrorAction SilentlyContinue
        Remove-Item -Path "$rootDir\publish_winui\*.pdb" -Force -ErrorAction SilentlyContinue
        Prune-UnusedLocales "$rootDir\publish_winui"
    } else {
        Write-Host "`n[1/3] 增量检查: 源码与资产未变动，复用现有自包含发布产物 (耗时: 0s)。" -ForegroundColor Green
    }
}

# 2.2 轻量框架依赖版发布 (默认发布规格: 极小体积 Framework-Dependent, ~23MB)
if ($PackageMode -eq "FrameworkDependent" -or $PackageMode -eq "Both") {
    if (Test-ProjectNeedsPublish "$rootDir\publish_winui_slim") {
        Write-Host "`n[1/3] 正在发布 WinUI 3 轻量框架依赖程序 (Framework-Dependent)..." -ForegroundColor Yellow
        & $dotnetCmd publish src\WanKePos.WinUI\WanKePos.WinUI.csproj -c Release -r win-x64 --self-contained false --no-restore -o publish_winui_slim /p:Version=$cleanVersion /p:AssemblyVersion=$numericVersion /p:FileVersion=$numericVersion
        if ($LASTEXITCODE -ne 0) {
            Write-Host "尝试全量还原发布..." -ForegroundColor Yellow
            & $dotnetCmd publish src\WanKePos.WinUI\WanKePos.WinUI.csproj -c Release -r win-x64 --self-contained false -o publish_winui_slim /p:Version=$cleanVersion /p:AssemblyVersion=$numericVersion /p:FileVersion=$numericVersion
        }

        if ($LASTEXITCODE -ne 0) {
            Write-Host "轻量框架依赖版编译发布失败，请检查代码错误!" -ForegroundColor Red
            exit 1
        }

        Remove-Item -Path "$rootDir\publish_winui_slim\*.db-shm" -Force -ErrorAction SilentlyContinue
        Remove-Item -Path "$rootDir\publish_winui_slim\*.db-wal" -Force -ErrorAction SilentlyContinue
        Remove-Item -Path "$rootDir\publish_winui_slim\*.log" -Force -ErrorAction SilentlyContinue
        Remove-Item -Path "$rootDir\publish_winui_slim\*.pdb" -Force -ErrorAction SilentlyContinue
        Prune-UnusedLocales "$rootDir\publish_winui_slim"
    } else {
        Write-Host "`n[1/3] 增量检查: 源码与资产未变动，复用现有轻量发布产物 (耗时: 0s)。" -ForegroundColor Green
    }
}

# 3. 定位 Inno Setup 编译器 ISCC.exe
Write-Host "`n[2/3] 正在查找 Inno Setup 编译器..." -ForegroundColor Yellow
$isccPaths = @(
    "$env:LOCALAPPDATA\Programs\Inno Setup 6\ISCC.exe",
    "$env:LOCALAPPDATA\Programs\Inno Setup 7\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    "C:\Program Files\Inno Setup 6\ISCC.exe",
    "C:\Program Files (x86)\Inno Setup 7\ISCC.exe",
    "C:\Program Files\Inno Setup 7\ISCC.exe",
    "C:\ProgramData\chocolatey\bin\iscc.exe"
)

$iscc = $null

$pathCmd = Get-Command "iscc.exe" -ErrorAction SilentlyContinue
if ($pathCmd) {
    $iscc = $pathCmd.Source
}

if (-not $iscc) {
    foreach ($path in $isccPaths) {
        if (Test-Path $path) {
            $iscc = $path
            break
        }
    }
}

if (-not $iscc) {
    Write-Host "未找到 Inno Setup 编译器，正在尝试自动安装..." -ForegroundColor Yellow
    winget install --id JRSoftware.InnoSetup -e --silent --accept-package-agreements --accept-source-agreements
    foreach ($path in $isccPaths) {
        if (Test-Path $path) {
            $iscc = $path
            break
        }
    }
}

if (-not $iscc) {
    Write-Host "错误: 无法找到或安装 Inno Setup 编译器!" -ForegroundColor Red
    exit 1
}

Write-Host "使用编译器: $iscc" -ForegroundColor Green

# 4. 执行 Inno Setup 打包
Write-Host "`n[3/3] 正在生成 Windows Setup 安装包..." -ForegroundColor Yellow
if (-not (Test-Path "$rootDir\output_installer")) {
    New-Item -ItemType Directory -Path "$rootDir\output_installer" | Out-Null
}

# 确保目标发布目录无多余语言包干扰
if ($PackageMode -eq "FrameworkDependent" -or $PackageMode -eq "Both") {
    Prune-UnusedLocales "$rootDir\publish_winui_slim"
}
if ($PackageMode -eq "SelfContained" -or $PackageMode -eq "Both") {
    Prune-UnusedLocales "$rootDir\publish_winui"
}

$outputBaseFilename = "WanKePos_Setup_v$cleanVersion"

if ($PackageMode -eq "FrameworkDependent") {
    Write-Host "--> 打包标准轻量安装包: $outputBaseFilename.exe (压缩模式: $compressionLevel)" -ForegroundColor Cyan
    & $iscc "/DMyAppVersion=$cleanVersion" "/DOutputBaseFilename=$outputBaseFilename" "/DSourceDir=..\publish_winui_slim" "/DCompressionLevel=$compressionLevel" "$rootDir\installer\WanKePosSetup.iss"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "生成轻量安装包失败!" -ForegroundColor Red
        exit 1
    }
} elseif ($PackageMode -eq "SelfContained") {
    Write-Host "--> 打包自包含安装包: $outputBaseFilename.exe (压缩模式: $compressionLevel)" -ForegroundColor Cyan
    & $iscc "/DMyAppVersion=$cleanVersion" "/DOutputBaseFilename=$outputBaseFilename" "/DSourceDir=..\publish_winui" "/DCompressionLevel=$compressionLevel" "$rootDir\installer\WanKePosSetup.iss"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "生成自包含安装包失败!" -ForegroundColor Red
        exit 1
    }
} elseif ($PackageMode -eq "Both") {
    Write-Host "--> 打包标准轻量安装包: $outputBaseFilename.exe (压缩模式: $compressionLevel)" -ForegroundColor Cyan
    & $iscc "/DMyAppVersion=$cleanVersion" "/DOutputBaseFilename=$outputBaseFilename" "/DSourceDir=..\publish_winui_slim" "/DCompressionLevel=$compressionLevel" "$rootDir\installer\WanKePosSetup.iss"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "生成轻量安装包失败!" -ForegroundColor Red
        exit 1
    }

    $fullBaseFilename = "WanKePos_Setup_v${cleanVersion}_Full"
    Write-Host "--> 打包自包含完整安装包: $fullBaseFilename.exe (压缩模式: $compressionLevel)" -ForegroundColor Cyan
    & $iscc "/DMyAppVersion=$cleanVersion" "/DOutputBaseFilename=$fullBaseFilename" "/DSourceDir=..\publish_winui" "/DCompressionLevel=$compressionLevel" "$rootDir\installer\WanKePosSetup.iss"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "生成自包含安装包失败!" -ForegroundColor Red
        exit 1
    }
}

if ($LASTEXITCODE -eq 0) {
    $setupFile = Get-Item "$rootDir\output_installer\$outputBaseFilename.exe"
    $fileSizeMB = [Math]::Round($setupFile.Length / 1MB, 2)
    Write-Host "`n==========================================" -ForegroundColor Green
    Write-Host "  安装包制作成功!" -ForegroundColor Green
    Write-Host "  版本: v$cleanVersion" -ForegroundColor Green
    Write-Host "  发布安装包 ($PackageMode): $($setupFile.FullName) ($fileSizeMB MB)" -ForegroundColor White
    if (Test-Path "$rootDir\output_installer\WanKePos_Setup_v${cleanVersion}_Full.exe") {
        $fullFile = Get-Item "$rootDir\output_installer\WanKePos_Setup_v${cleanVersion}_Full.exe"
        $fullSizeMB = [Math]::Round($fullFile.Length / 1MB, 2)
        Write-Host "  自包含全量包: $($fullFile.FullName) ($fullSizeMB MB)" -ForegroundColor White
    }
    Write-Host "==========================================" -ForegroundColor Green

    if ($RunInstaller -and ($env:CI -ne "true") -and ($env:GITHUB_ACTIONS -ne "true")) {
        Write-Host "`n[4/4] Auto-installing and launching updated app..." -ForegroundColor Cyan
        
        # Step 1: Run silent installer
        Write-Host "Running silent installation without desktop icon..." -ForegroundColor Yellow
        $installProc = Start-Process -FilePath $setupFile.FullName -ArgumentList "/VERYSILENT", "/SP-", "/SUPPRESSMSGBOXES", "/NORESTART", "/MERGETASKS=""!desktopicon""" -PassThru -Wait
        Write-Host "Silent installation completed with exit code: $($installProc.ExitCode)" -ForegroundColor Green

        # 确保清理用户桌面与公共桌面上可能残留的快捷方式
        try {
            Remove-Item "$([Environment]::GetFolderPath('Desktop'))\*万客隆*.lnk" -Force -ErrorAction SilentlyContinue
            Remove-Item "$([Environment]::GetFolderPath('CommonDesktop'))\*万客隆*.lnk" -Force -ErrorAction SilentlyContinue
            Remove-Item "$([Environment]::GetFolderPath('Desktop'))\*WanKe*.lnk" -Force -ErrorAction SilentlyContinue
            Remove-Item "$([Environment]::GetFolderPath('CommonDesktop'))\*WanKe*.lnk" -Force -ErrorAction SilentlyContinue
        } catch { }

        # 刷新 Windows 任务栏与外壳图标缓存，确保新图标即时生效
        try {
            & ie4uinit.exe -show 2>$null
        } catch { }

        # Step 2: Locate installed executable
        $localAppExe = "$env:LOCALAPPDATA\Programs\WanKePos\WanKePos.WinUI.exe"
        $publishExe = "$rootDir\publish_winui_slim\WanKePos.WinUI.exe"
        if (-not (Test-Path -Path $publishExe)) {
            $publishExe = "$rootDir\publish_winui\WanKePos.WinUI.exe"
        }

        $targetExe = $null
        if (Test-Path -Path $localAppExe) {
            $targetExe = $localAppExe
        } elseif (Test-Path -Path $progFilesExe) {
            $targetExe = $progFilesExe
        } elseif (Test-Path -Path $publishExe) {
            $targetExe = $publishExe
        }

        # Step 3: Launch the updated application
        if ($targetExe -and (Test-Path -Path $targetExe)) {
            Write-Host "Launching updated application: $targetExe" -ForegroundColor Green
            $appDir = Split-Path -Parent $targetExe
            try {
                if (-not ([System.Management.Automation.PSTypeName]'DesktopProcessLauncher').Type) {
$desktopLauncherSource = @'
using System;
using System.Runtime.InteropServices;

public class DesktopProcessLauncher {
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct STARTUPINFO {
        public int cb;
        public string lpReserved;
        public string lpDesktop;
        public string lpTitle;
        public int dwX;
        public int dwY;
        public int dwXSize;
        public int dwYSize;
        public int dwXCountChars;
        public int dwYCountChars;
        public int dwFillAttribute;
        public int dwFlags;
        public short wShowWindow;
        public short cbReserved2;
        public IntPtr lpReserved2;
        public IntPtr hStdInput;
        public IntPtr hStdOutput;
        public IntPtr hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct PROCESS_INFORMATION {
        public IntPtr hProcess;
        public IntPtr hThread;
        public int dwProcessId;
        public int dwThreadId;
    }

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    public static extern bool CreateProcess(
        string lpApplicationName,
        string lpCommandLine,
        IntPtr lpProcessAttributes,
        IntPtr lpThreadAttributes,
        bool bInheritHandles,
        uint dwCreationFlags,
        IntPtr lpEnvironment,
        string lpCurrentDirectory,
        ref STARTUPINFO lpStartupInfo,
        out PROCESS_INFORMATION lpProcessInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("shell32.dll")]
    public static extern void SHChangeNotify(int wEventId, uint uFlags, IntPtr dwItem1, IntPtr dwItem2);

    public static int StartOnInteractiveDesktop(string appPath, string workingDir) {
        try {
            SHChangeNotify(0x08000000, 0, IntPtr.Zero, IntPtr.Zero);
        } catch { }

        STARTUPINFO si = new STARTUPINFO();
        si.cb = Marshal.SizeOf(si);
        si.lpDesktop = @"WinSta0\Default";
        si.dwFlags = 1; // STARTF_USESHOWWINDOW
        si.wShowWindow = 5; // SW_SHOW
        PROCESS_INFORMATION pi = new PROCESS_INFORMATION();

        // Try with CREATE_BREAKAWAY_FROM_JOB (0x01000000) | CREATE_NEW_PROCESS_GROUP (0x00000200)
        string cmdLine = "\"" + appPath + "\"";
        bool success = CreateProcess(
            null,
            cmdLine,
            IntPtr.Zero,
            IntPtr.Zero,
            false,
            0x01000200,
            IntPtr.Zero,
            workingDir,
            ref si,
            out pi);

        if (!success) {
            // Fallback without breakaway flag
            success = CreateProcess(
                null,
                cmdLine,
                IntPtr.Zero,
                IntPtr.Zero,
                false,
                0x00000200,
                IntPtr.Zero,
                workingDir,
                ref si,
                out pi);
        }

        if (success) {
            CloseHandle(pi.hThread);
            CloseHandle(pi.hProcess);
            return pi.dwProcessId;
        }
        return 0;
    }
}
'@
                    Add-Type -TypeDefinition $desktopLauncherSource
                }
                $launchedPid = [DesktopProcessLauncher]::StartOnInteractiveDesktop($targetExe, $appDir)
                if ($launchedPid -gt 0) {
                    Write-Host "WanKePos system launched successfully on interactive desktop (PID: $launchedPid)!" -ForegroundColor Green
                } else {
                    Start-Process -FilePath $targetExe -WorkingDirectory $appDir
                    Write-Host "WanKePos system launched successfully (fallback Start-Process)!" -ForegroundColor Green
                }
            } catch {
                Start-Process -FilePath $targetExe -WorkingDirectory $appDir
                Write-Host "WanKePos system launched successfully (fallback)!" -ForegroundColor Green
            }
        } else {
            Write-Host "Error: Could not locate installed executable." -ForegroundColor Red
        }
    } elseif ($env:CI -eq "true" -or $env:GITHUB_ACTIONS -eq "true") {
        Write-Host "`n[4/4] CI/CD environment detected, skipping GUI launch." -ForegroundColor Yellow
    }
} else {
    Write-Host "Failed to build installer package!" -ForegroundColor Red
    exit 1
}

