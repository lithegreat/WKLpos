param(
    [string]$Version = "0.1.0",
    [bool]$RunInstaller = $true
)

# 规范化版本号 (移除前导 v 或 V)
$cleanVersion = $Version.TrimStart('v', 'V')
if ([string]::IsNullOrWhiteSpace($cleanVersion)) {
    $cleanVersion = "0.1.0"
}

# 自动化构建万客隆 POS Windows Setup 安装包脚本
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  万客隆 POS 系统 - Windows 安装包打包流水线" -ForegroundColor Cyan
Write-Host "  目标版本: v$cleanVersion" -ForegroundColor Cyan
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

# 2. 编译并发布 WinUI 3 独立程序
Write-Host "`n[1/3] 正在发布 WinUI 3 独立免依赖程序 (版本: v$cleanVersion)..." -ForegroundColor Yellow
$env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH", "User")
$dotnetCmd = "dotnet"
if (Test-Path "$env:USERPROFILE\.dotnet\dotnet.exe") {
    $dotnetCmd = "$env:USERPROFILE\.dotnet\dotnet.exe"
    $env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"
    $env:PATH = "$env:USERPROFILE\.dotnet;" + $env:PATH
}
& $dotnetCmd publish src\WanKePos.WinUI\WanKePos.WinUI.csproj -c Release -r win-x64 --self-contained true -o publish_winui /p:Version=$cleanVersion /p:AssemblyVersion=$cleanVersion /p:FileVersion=$cleanVersion

if ($LASTEXITCODE -ne 0) {
    Write-Host "编译发布失败，请检查代码错误!" -ForegroundColor Red
    exit 1
}

# 清理发布目录下的临时锁文件与运行时日志，避免将其打入安装包
Remove-Item -Path "$rootDir\publish_winui\*.db-shm" -Force -ErrorAction SilentlyContinue
Remove-Item -Path "$rootDir\publish_winui\*.db-wal" -Force -ErrorAction SilentlyContinue
Remove-Item -Path "$rootDir\publish_winui\*.log" -Force -ErrorAction SilentlyContinue

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

$outputBaseFilename = "WanKePos_Setup_v$cleanVersion"
& $iscc "/DMyAppVersion=$cleanVersion" "/DOutputBaseFilename=$outputBaseFilename" "$rootDir\installer\WanKePosSetup.iss"

if ($LASTEXITCODE -eq 0) {
    $setupFile = Get-Item "$rootDir\output_installer\$outputBaseFilename.exe"
    $fileSizeMB = [Math]::Round($setupFile.Length / 1MB, 2)
    Write-Host "`n==========================================" -ForegroundColor Green
    Write-Host "  安装包制作成功!" -ForegroundColor Green
    Write-Host "  版本: v$cleanVersion" -ForegroundColor Green
    Write-Host "  文件路径: $($setupFile.FullName)" -ForegroundColor White
    Write-Host "  文件大小: $fileSizeMB MB" -ForegroundColor White
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
        $progFilesExe = "C:\Program Files\WanKePos\WanKePos.WinUI.exe"
        $publishExe = "$rootDir\publish_winui\WanKePos.WinUI.exe"

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
                    Add-Type @'
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
