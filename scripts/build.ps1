# 构建脚本：编译 PhoneMirror.cs 并组装出可运行的 dist\ 目录
# 用法：powershell -ExecutionPolicy Bypass -File scripts\build.ps1
$ErrorActionPreference = 'Stop'

$root   = Split-Path $PSScriptRoot -Parent
$src    = Join-Path $root 'src\PhoneMirror.cs'
$dist   = Join-Path $root 'dist'
$assets = Join-Path $root 'assets'

New-Item -ItemType Directory -Force $dist | Out-Null

# 1) 编译（.NET Framework 4.x 自带 csc，无需安装任何 SDK）
$csc = 'C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { $csc = 'C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe' }
if (-not (Test-Path $csc)) { throw '.NET Framework 4.x 未找到（csc.exe），无法编译' }

& $csc /nologo /target:winexe /codepage:65001 `
    /win32icon:"$assets\手机投屏.ico" `
    /out:"$dist\手机投屏.exe" `
    /r:System.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll $src
if ($LASTEXITCODE -ne 0) { throw '编译失败' }
Write-Host 'OK: dist\手机投屏.exe'

# 2) scrcpy 运行时（缺失时自动下载官方最新 win64 包）
$scrcpyDir = Join-Path $dist 'scrcpy'
if (-not (Test-Path (Join-Path $scrcpyDir 'scrcpy.exe'))) {
    Write-Host '正在下载 scrcpy 官方运行时...'
    $rel  = Invoke-RestMethod 'https://api.github.com/repos/Genymobile/scrcpy/releases/latest' -TimeoutSec 60
    $asset = $rel.assets | Where-Object { $_.name -match 'win64.*\.zip$' } | Select-Object -First 1
    if (-not $asset) { throw '未找到 scrcpy win64 发布包' }
    $zip = Join-Path $dist $asset.name
    Invoke-WebRequest -Uri $asset.browser_download_url -OutFile $zip -UseBasicParsing -TimeoutSec 300
    Expand-Archive $zip -DestinationPath $dist -Force
    Get-ChildItem (Join-Path $dist 'scrcpy-win64-*') | Move-Item -Destination $scrcpyDir
    Remove-Item $zip -Force
}
Write-Host 'OK: dist\scrcpy'

# 3) 使用说明与默认配置
Copy-Item (Join-Path $root 'docs\usage.zh.md') (Join-Path $dist '使用说明.txt') -Force
if (-not (Test-Path (Join-Path $dist 'config.ini'))) {
    Copy-Item (Join-Path $root 'config.example.ini') (Join-Path $dist 'config.ini') -Force
}

Write-Host "构建完成：$dist"
