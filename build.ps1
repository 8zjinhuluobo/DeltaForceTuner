$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "找不到 .NET Framework 编译器: $csc" }
if (-not (Test-Path 'assets\app.ico')) {
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -File 'assets\make-icon.ps1'
}

$refs = @(
    'System.dll',
    'System.Core.dll',
    'System.Drawing.dll',
    'System.Windows.Forms.dll',
    'System.Xml.dll'
)
$refArgs = @()
foreach ($r in $refs) { $refArgs += "/reference:$r" }
$files = Get-ChildItem 'src\*.cs' | ForEach-Object { $_.FullName }

New-Item -ItemType Directory -Force -Path 'dist' | Out-Null

& $csc /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 `
    /win32manifest:src\app.manifest /win32icon:assets\app.ico `
    "/out:dist\DeltaForceTuner.exe" @refArgs $files
if ($LASTEXITCODE -ne 0) { throw "GUI 编译失败 ($LASTEXITCODE)" }

& $csc /nologo /target:exe /platform:x64 /optimize+ /codepage:65001 `
    /win32icon:assets\app.ico `
    "/out:dist\DeltaForceTunerConsole.exe" @refArgs $files
if ($LASTEXITCODE -ne 0) { throw "测试版编译失败 ($LASTEXITCODE)" }

$hash = (Get-FileHash 'dist\DeltaForceTuner.exe' -Algorithm SHA256).Hash
$ver = '1.0.1.0'
$txt = $hash + "  DeltaForceTuner.exe`r`nName: 三角洲行动 · 本机调优助手`r`nVersion: $ver`r`nBuild: " + (Get-Date -Format 'yyyy-MM-dd') + "`r`n"
[System.IO.File]::WriteAllText((Join-Path $root 'dist\SHA256.txt'), $txt, (New-Object Text.UTF8Encoding($false)))
"GUI:  dist\DeltaForceTuner.exe"
"Test: dist\DeltaForceTunerConsole.exe"
"SHA256: $hash"
