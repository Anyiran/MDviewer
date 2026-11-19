# Build the MDviewer installer
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1
#   powershell -ExecutionPolicy Bypass -File installer\build-installer.ps1 -InnoPath "C:\Program Files (x86)\Inno Setup 6\ISCC.exe"
#
# Requires Inno Setup 6:  https://jrsoftware.org/isdl.php

param(
    [string]$InnoPath = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Definition)

Write-Host "==> 1/2 构建应用..." -ForegroundColor Cyan
& (Join-Path $root "build.ps1")
if ($LASTEXITCODE -ne 0) { throw "应用构建失败。" }

Write-Host "==> 2/2 生成安装包..." -ForegroundColor Cyan

$candidates = @()
if ($InnoPath) { $candidates += $InnoPath }
$candidates += @(
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 6\ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 6\ISCC.exe"),
    (Join-Path ${env:ProgramFiles(x86)} "Inno Setup 5\ISCC.exe"),
    (Join-Path $env:ProgramFiles "Inno Setup 5\ISCC.exe")
)

$iscc = $candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
if (-not $iscc) {
    Write-Host ""
    Write-Host "未找到 Inno Setup 编译器 (ISCC.exe)。" -ForegroundColor Yellow
    Write-Host "请先安装 Inno Setup 6: https://jrsoftware.org/isdl.php"
    Write-Host "然后重新运行本脚本，或通过 -InnoPath 指定 ISCC.exe 路径。"
    Write-Host ""
    Write-Host "便携版已生成: $root\build\MDviewer-portable.zip"
    exit 1
}

$iss = Join-Path $root "installer\MDviewer.iss"
& $iscc $iss
if ($LASTEXITCODE -ne 0) { throw "Inno Setup 编译失败 (exit $LASTEXITCODE)。" }

Write-Host ""
Write-Host "安装包已生成:" -ForegroundColor Green
Get-ChildItem (Join-Path $root "build") -Filter "MDviewer-Setup-*.exe" |
    ForEach-Object { Write-Host "  $($_.FullName)" }
