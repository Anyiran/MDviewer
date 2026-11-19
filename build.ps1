# MDviewer build script
# Compiles the app with the in-box .NET Framework C# compiler, then lays out a
# self-contained portable folder under build\.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File build.ps1
#
# Requires: .NET Framework 4.6.2+ (Windows 7 SP1 can install 4.8)

$ErrorActionPreference = "Stop"

$root      = Split-Path -Parent $MyInvocation.MyCommand.Definition
$srcDir    = Join-Path $root "src\MDviewer"
$buildDir  = Join-Path $root "build"
$outDir    = Join-Path $buildDir "MDviewer"
$vendor    = Join-Path $root "vendor\webview2"
$assetsDir = Join-Path $srcDir "Assets"

Write-Host "==> MDviewer build" -ForegroundColor Cyan

# ---------------------------------------------------------------- toolchain
$cscCandidates = @(
    (Join-Path $env:WINDIR "Microsoft.NET\Framework64\v4.0.30319\csc.exe"),
    (Join-Path $env:WINDIR "Microsoft.NET\Framework\v4.0.30319\csc.exe")
)
$csc = $cscCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
if (-not $csc) {
    throw "找不到 .NET Framework 4.x 的 C# 编译器 (csc.exe)。请安装 .NET Framework 4.6.2 或更高版本。"
}
Write-Host "    编译器: $csc"

$fwDir = Split-Path -Parent $csc
$refs = @(
    (Join-Path $fwDir "System.dll"),
    (Join-Path $fwDir "System.Core.dll"),
    (Join-Path $fwDir "System.Drawing.dll"),
    (Join-Path $fwDir "System.Windows.Forms.dll"),
    (Join-Path $fwDir "System.Xml.dll"),
    (Join-Path $vendor "lib\Microsoft.Web.WebView2.Core.dll"),
    (Join-Path $vendor "lib\Microsoft.Web.WebView2.WinForms.dll")
)
foreach ($r in $refs) {
    if (-not (Test-Path $r)) { throw "缺少引用程序集: $r" }
}

# ------------------------------------------------------------------- icon
$icoPath = Join-Path $assetsDir "app.ico"
if (-not (Test-Path $icoPath)) {
    Write-Host "    生成应用图标..."
    $py = Get-Command python -ErrorAction SilentlyContinue
    if (-not $py) { throw "缺少 src\MDviewer\Assetspp.ico，且未找到 Python 以重新生成。" }
    & $py.Source (Join-Path $root "tools\make_icon.py")
    if ($LASTEXITCODE -ne 0) { throw "图标生成失败。" }
}

# ------------------------------------------------------------------ clean
if (Test-Path $buildDir) { Remove-Item $buildDir -Recurse -Force }
New-Item -ItemType Directory -Force -Path $outDir | Out-Null

# ---------------------------------------------------------------- compile
$sources = Get-ChildItem -Path $srcDir -Recurse -Filter *.cs | ForEach-Object { $_.FullName }
if (-not $sources -or $sources.Count -eq 0) { throw "没有找到源代码文件。" }

Write-Host "    编译源代码 ($($sources.Count) 个文件)..."
$argsList = @(
    "/nologo",
    "/target:winexe",
    "/platform:anycpu",
    "/optimize+",
    "/warn:4",
    "/langversion:5",
    "/win32icon:`"$icoPath`"",
    "/win32manifest:`"$(Join-Path $srcDir 'app.manifest')`"",
    "/resource:`"$icoPath`",MDviewer.app.ico",
    "/out:`"$(Join-Path $outDir 'MDviewer.exe')`""
)
foreach ($r in $refs) { $argsList += "/reference:`"$r`"" }
$argsList += $sources

& $csc $argsList
if ($LASTEXITCODE -ne 0) { throw "编译失败 (exit $LASTEXITCODE)。" }

# ------------------------------------------------------------------- layout
Write-Host "    复制运行时文件..."

Copy-Item (Join-Path $vendor "lib\Microsoft.Web.WebView2.Core.dll")     $outDir -Force
Copy-Item (Join-Path $vendor "lib\Microsoft.Web.WebView2.WinForms.dll") $outDir -Force

New-Item -ItemType Directory -Force -Path (Join-Path $outDir "WebView2Loader\x64") | Out-Null
New-Item -ItemType Directory -Force -Path (Join-Path $outDir "WebView2Loader\x86") | Out-Null
Copy-Item (Join-Path $vendor "loader\x64\WebView2Loader.dll") (Join-Path $outDir "WebView2Loader\x64") -Force
Copy-Item (Join-Path $vendor "loader\x86\WebView2Loader.dll") (Join-Path $outDir "WebView2Loader\x86") -Force

Copy-Item (Join-Path $srcDir "app.config") (Join-Path $outDir "MDviewer.exe.config") -Force

Copy-Item (Join-Path $srcDir "web") (Join-Path $outDir "web") -Recurse -Force

Copy-Item (Join-Path $root "README.md") $outDir -Force -ErrorAction SilentlyContinue
Copy-Item (Join-Path $root "LICENSE") $outDir -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force -Path (Join-Path $outDir "third-party") | Out-Null
Copy-Item (Join-Path $vendor "LICENSE.txt") (Join-Path $outDir "third-party\WebView2-LICENSE.txt") -Force
Copy-Item (Join-Path $vendor "NOTICE.txt")  (Join-Path $outDir "third-party\WebView2-NOTICE.txt") -Force

# -------------------------------------------------------------- portable zip
Write-Host "    打包便携版压缩包..."
$zip = Join-Path $buildDir "MDviewer-portable.zip"
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($outDir, $zip)

Write-Host ""
Write-Host "构建完成。" -ForegroundColor Green
Write-Host "  可执行目录: $outDir"
Write-Host "  便携版压缩包: $zip"
Write-Host ""
Write-Host "运行: $outDir\MDviewer.exe"
