param(
    [string]$Version = "1.1.0"
)

# 规范化版本号 (移除前导 v 或 V)
$cleanVersion = $Version.TrimStart('v', 'V')
if ([string]::IsNullOrWhiteSpace($cleanVersion)) {
    $cleanVersion = "1.1.0"
}

# 自动化构建万客隆 POS Windows Setup 安装包脚本
Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  万客隆 POS 系统 - Windows 安装包打包流水线" -ForegroundColor Cyan
Write-Host "  目标版本: v$cleanVersion" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. 停止运行中的进程
Stop-Process -Name "WanKePos.WinUI" -Force -ErrorAction SilentlyContinue
Stop-Process -Name "WanKePos" -Force -ErrorAction SilentlyContinue

$rootDir = Split-Path -Parent $PSScriptRoot
Set-Location $rootDir

# 2. 编译并发布 WinUI 3 独立程序
Write-Host "`n[1/3] 正在发布 WinUI 3 独立免依赖程序 (版本: v$cleanVersion)..." -ForegroundColor Yellow
$env:PATH = [System.Environment]::GetEnvironmentVariable("PATH", "Machine") + ";" + [System.Environment]::GetEnvironmentVariable("PATH", "User")
dotnet publish src\WanKePos.WinUI\WanKePos.WinUI.csproj -c Release -r win-x64 --self-contained true -o publish_winui /p:Version=$cleanVersion /p:AssemblyVersion=$cleanVersion /p:FileVersion=$cleanVersion

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
} else {
    Write-Host "安装包打包失败!" -ForegroundColor Red
    exit 1
}
