param(
    [Parameter(Mandatory = $true, Position = 0, HelpMessage = "目标发布版本号，例如: 0.2.1 或 v0.2.1")]
    [string]$Version,

    [Parameter(Mandatory = $false)]
    [string]$Title = "",

    [Parameter(Mandatory = $false)]
    [string]$Notes = "",

    [Parameter(Mandatory = $false)]
    [switch]$SkipTests,

    [Parameter(Mandatory = $false)]
    [switch]$DryRun
)

# 确保脚本在发生未捕获错误时终止
$ErrorActionPreference = "Stop"

$rootDir = $PSScriptRoot
Set-Location $rootDir

Write-Host "==========================================" -ForegroundColor Cyan
Write-Host "  万客隆 POS 系统 - 全自动发版程序" -ForegroundColor Cyan
Write-Host "==========================================" -ForegroundColor Cyan

# 1. 规范化与校验版本号
$cleanVersion = $Version.Trim().TrimStart('v', 'V')
$cleanVersion = $cleanVersion -replace '\s+', '-'
if ($cleanVersion -notmatch '^\d+\.\d+\.\d+(-[a-zA-Z0-9.]+)?$') {
    Write-Host "错误: 版本号格式不合法! 请使用标准语义化版本 (如 0.2.1 或 0.2.2-beta)。当前输入: $Version" -ForegroundColor Red
    exit 1
}
$isPrerelease = $cleanVersion -match "(pre|beta|alpha|rc)"
$tag = "v$cleanVersion"

$rawNumeric = ($cleanVersion -split '-')[0]
$parts = $rawNumeric -split '\.'
while ($parts.Length -lt 3) { $parts += "0" }
$numericVersion = "$($parts[0]).$($parts[1]).$($parts[2]).0"

if ([string]::IsNullOrWhiteSpace($Title)) {
    $typeLabel = if ($isPrerelease) { " (预览版 Pre-Release)" } else { "" }
    $Title = "万客隆 POS 系统 $tag$typeLabel"
}

$typeDesc = if ($isPrerelease) { "【预览版 Pre-Release】" } else { "【正式版 Stable】" }
Write-Host "目标版本: $cleanVersion $typeDesc (Tag: $tag | 程序集: $numericVersion)" -ForegroundColor Green

# 2. 检查依赖工具 (git 与 gh)
Write-Host "`n[1/6] 检查 Git 与 GitHub CLI 环境..." -ForegroundColor Yellow
if (-not (Get-Command "git" -ErrorAction SilentlyContinue)) {
    Write-Host "错误: 未找到 git 命令，请确认已安装 Git 并配置在 PATH 中。" -ForegroundColor Red
    exit 1
}
if (-not (Get-Command "gh" -ErrorAction SilentlyContinue)) {
    Write-Host "错误: 未找到 gh 命令，请安装 GitHub CLI (winget install GitHub.cli)。" -ForegroundColor Red
    exit 1
}

# 验证 gh 登录状态 (若未配置 GH_TOKEN，尝试从 Git Credential Manager 自动提取)
if ([string]::IsNullOrWhiteSpace($env:GH_TOKEN)) {
    try {
        $inputStr = "protocol=https`nhost=github.com`n"
        $credOutput = $inputStr | git credential fill 2>$null
        $token = (($credOutput -split "`r?`n" | Where-Object { $_ -match "^password=" }) -replace "^password=", "").Trim()
        if ($token) {
            $env:GH_TOKEN = $token
        }
    } catch { }
}

$ghUser = gh api user --jq .login 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($ghUser)) {
    Write-Host "错误: GitHub CLI 尚未登录，请先在终端运行 'gh auth login' 登录 GitHub。" -ForegroundColor Red
    exit 1
}
Write-Host "GitHub 用户: $ghUser (已认证)" -ForegroundColor Green

# 3. 检查 Tag 是否已经存在
Write-Host "`n[2/6] 检查 Tag 冲突..." -ForegroundColor Yellow
$localTag = git tag -l $tag
if ($localTag) {
    Write-Host "错误: 本地已存在 Tag '$tag'! 请指定新的版本号或先删除旧 Tag。" -ForegroundColor Red
    exit 1
}
$remoteTag = git ls-remote --tags origin "refs/tags/$tag"
if ($remoteTag) {
    Write-Host "错误: 远程仓库已存在 Tag '$tag'! 请指定新的版本号。" -ForegroundColor Red
    exit 1
}
Write-Host "Tag '$tag' 可用。" -ForegroundColor Green

# 4. 运行单元测试
if (-not $SkipTests) {
    Write-Host "`n[3/6] 运行单元测试..." -ForegroundColor Yellow
    $dotnetCmd = "dotnet"
    if (Test-Path "$env:USERPROFILE\.dotnet\dotnet.exe") {
        $dotnetCmd = "$env:USERPROFILE\.dotnet\dotnet.exe"
        $env:DOTNET_ROOT = "$env:USERPROFILE\.dotnet"
        $env:PATH = "$env:USERPROFILE\.dotnet;" + $env:PATH
    }

    & $dotnetCmd test tests\WanKePos.Tests\WanKePos.Tests.csproj --configuration Release --verbosity minimal
    if ($LASTEXITCODE -ne 0) {
        Write-Host "错误: 单元测试未通过，发版终止! 请先修复测试或添加 -SkipTests 强制跳过。" -ForegroundColor Red
        exit 1
    }
    Write-Host "单元测试全部通过!" -ForegroundColor Green
} else {
    Write-Host "`n[3/6] 跳过单元测试 (-SkipTests)。" -ForegroundColor Gray
}

if ($DryRun) {
    Write-Host "`n[DryRun 模式] 模拟流程完成，未做实际文件修改与 Git 提交。" -ForegroundColor Magenta
    exit 0
}

# 5. 更新各工程版本号
Write-Host "`n[4/6] 自动更新工程版本号..." -ForegroundColor Yellow

# 5.1 WanKePos.WinUI.csproj
$csprojPath = Join-Path $rootDir "src\WanKePos.WinUI\WanKePos.WinUI.csproj"
if (Test-Path $csprojPath) {
    $csproj = [System.IO.File]::ReadAllText($csprojPath, [System.Text.Encoding]::UTF8)
    $csproj = [System.Text.RegularExpressions.Regex]::Replace($csproj, '<Version>.*?</Version>', "<Version>$cleanVersion</Version>")
    $csproj = [System.Text.RegularExpressions.Regex]::Replace($csproj, '<AssemblyVersion>.*?</AssemblyVersion>', "<AssemblyVersion>$numericVersion</AssemblyVersion>")
    $csproj = [System.Text.RegularExpressions.Regex]::Replace($csproj, '<FileVersion>.*?</FileVersion>', "<FileVersion>$numericVersion</FileVersion>")
    [System.IO.File]::WriteAllText($csprojPath, $csproj, [System.Text.Encoding]::UTF8)
    Write-Host "  -> 已更新: $csprojPath" -ForegroundColor Gray
}

# 5.2 installer\WanKePosSetup.iss
$issPath = Join-Path $rootDir "installer\WanKePosSetup.iss"
if (Test-Path $issPath) {
    $iss = [System.IO.File]::ReadAllText($issPath, [System.Text.Encoding]::UTF8)
    $iss = [System.Text.RegularExpressions.Regex]::Replace($iss, '(#define\s+MyAppVersion\s+)"[^"]+"', "`$1`"$cleanVersion`"")
    [System.IO.File]::WriteAllText($issPath, $iss, [System.Text.Encoding]::UTF8)
    Write-Host "  -> 已更新: $issPath" -ForegroundColor Gray
}

# 5.3 installer\build_installer.ps1 (带 UTF-8 BOM 保存以兼容 PowerShell 5.1)
$buildScriptPath = Join-Path $rootDir "installer\build_installer.ps1"
if (Test-Path $buildScriptPath) {
    $bs = [System.IO.File]::ReadAllText($buildScriptPath, [System.Text.Encoding]::UTF8)
    $bs = [System.Text.RegularExpressions.Regex]::Replace($bs, '(\$Version\s*=\s*)"[^"]+"', "`$1`"$cleanVersion`"")
    $bs = [System.Text.RegularExpressions.Regex]::Replace($bs, '(\$cleanVersion\s*=\s*)"[^"]+"', "`$1`"$cleanVersion`"")
    $utf8WithBom = New-Object System.Text.UTF8Encoding($true)
    [System.IO.File]::WriteAllText($buildScriptPath, $bs, $utf8WithBom)
    Write-Host "  -> 已更新: $buildScriptPath" -ForegroundColor Gray
}

# 5.4 README.md
$readmePath = Join-Path $rootDir "README.md"
if (Test-Path $readmePath) {
    $readme = [System.IO.File]::ReadAllText($readmePath, [System.Text.Encoding]::UTF8)
    $readme = [System.Text.RegularExpressions.Regex]::Replace($readme, '当前版本：\*\*v.*?\*\*', "当前版本：**$tag**")
    $readme = [System.Text.RegularExpressions.Regex]::Replace($readme, 'WanKePos_Setup_v.*?\.exe', "WanKePos_Setup_$tag.exe")
    [System.IO.File]::WriteAllText($readmePath, $readme, [System.Text.Encoding]::UTF8)
    Write-Host "  -> 已更新: $readmePath" -ForegroundColor Gray
}

# 5.5 MainViewModel.cs
$mainVmPath = Join-Path $rootDir "src\WanKePos.WinUI\ViewModels\MainViewModel.cs"
if (Test-Path $mainVmPath) {
    $mainVm = [System.IO.File]::ReadAllText($mainVmPath, [System.Text.Encoding]::UTF8)
    $mainVm = [System.Text.RegularExpressions.Regex]::Replace($mainVm, 'return "v\d+\.\d+\.\d+(-[a-zA-Z0-9.]+)?";', "return `"$tag`";")
    [System.IO.File]::WriteAllText($mainVmPath, $mainVm, [System.Text.Encoding]::UTF8)
    Write-Host "  -> 已更新: $mainVmPath" -ForegroundColor Gray
}

# 5.6 SettingsViewModel.cs
$settingsVmPath = Join-Path $rootDir "src\WanKePos.WinUI\ViewModels\SettingsViewModel.cs"
if (Test-Path $settingsVmPath) {
    $settingsVm = [System.IO.File]::ReadAllText($settingsVmPath, [System.Text.Encoding]::UTF8)
    $settingsVm = [System.Text.RegularExpressions.Regex]::Replace($settingsVm, 'return "\d+\.\d+\.\d+(-[a-zA-Z0-9.]+)?";', "return `"$cleanVersion`";")
    [System.IO.File]::WriteAllText($settingsVmPath, $settingsVm, [System.Text.Encoding]::UTF8)
    Write-Host "  -> 已更新: $settingsVmPath" -ForegroundColor Gray
}

# 6. 分支管理、提交与合并至 main
Write-Host "`n[5/6] 提交更改并合入 main 分支..." -ForegroundColor Yellow

$currentBranch = (git branch --show-current).Trim()
Write-Host "当前工作分支: $currentBranch" -ForegroundColor Gray

# 确定发版工作分支
$releaseBranch = $currentBranch
if ($currentBranch -ne "release/$tag") {
    $releaseBranch = "release/$tag"
    $existingBranch = (git branch --list $releaseBranch).Trim()
    if ($existingBranch) {
        git branch -D $releaseBranch
    }
    Write-Host "自动切换至发版临时分支: $releaseBranch" -ForegroundColor Cyan
    git checkout -b $releaseBranch
}

# 暂存并提交所有更改
git add -A
$hasChanges = (git status --porcelain).Trim()
if ($hasChanges) {
    git commit -m "chore: release $tag"
    Write-Host "已提交更改: chore: release $tag" -ForegroundColor Green
} else {
    Write-Host "无额外未提交代码更改。" -ForegroundColor Gray
}

# 推送当前发版分支至远端
Write-Host "正在推送分支 $releaseBranch 到 GitHub..." -ForegroundColor Yellow
git push -u origin $releaseBranch

# 若发版分支不是 main，则通过 PR 自动合入 main（符合分支保护策略）
if ($releaseBranch -ne "main") {
    Write-Host "正在创建 Pull Request 合入 main..." -ForegroundColor Yellow
    $prBody = if ([string]::IsNullOrWhiteSpace($Notes)) {
        "自动发版工作流: 发布 $tag`n- 统一升级工程版本号至 $cleanVersion`n- 包含近期特性与缺陷修复"
    } else {
        $Notes
    }

    $prUrl = gh pr create --base main --head $releaseBranch --title "Release ${tag}: $Title" --body "$prBody"
    Write-Host "已创建 PR: $prUrl" -ForegroundColor Green

    Write-Host "正在合并 PR 到 main..." -ForegroundColor Yellow
    gh pr merge $releaseBranch --merge --delete-branch
    Write-Host "PR 已成功合并至 main!" -ForegroundColor Green

    # 切换回 main 并同步
    git checkout main
    git pull origin main
}

# 7. 创建并推送 Git Tag 触发 GitHub Actions CI
Write-Host "`n[6/6] 创建并推送 Git Tag: $tag..." -ForegroundColor Yellow
git tag -a $tag -m "Release ${tag}: $Title"
git push origin $tag

Write-Host "`n============================================================" -ForegroundColor Green
Write-Host "  发版流程执行成功! Tag '$tag' 已成功推送至 GitHub!" -ForegroundColor Green
Write-Host "============================================================" -ForegroundColor Green
Write-Host "GitHub Actions CI 已自动触发构建流程，正在云端完成：" -ForegroundColor Cyan
Write-Host "  1. 编译 .NET 10 代码并运行单元测试" -ForegroundColor Cyan
Write-Host "  2. 安装 Inno Setup 并打包 Windows 安装程序" -ForegroundColor Cyan
Write-Host "  3. 自动发布至 GitHub Release 并上传 WanKePos_Setup_$tag.exe" -ForegroundColor Cyan
Write-Host "`n您可以点击以下链接实时监控构建进度与下载安装包：" -ForegroundColor White
Write-Host "  • CI 构建流水线: https://github.com/lithegreat/WKLpos/actions" -ForegroundColor Yellow
Write-Host "  • 对应版本发布页: https://github.com/lithegreat/WKLpos/releases/tag/$tag" -ForegroundColor Yellow
Write-Host "============================================================" -ForegroundColor Green