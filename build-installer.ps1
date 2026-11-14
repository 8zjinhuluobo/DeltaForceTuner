$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) { throw "找不到 .NET Framework 编译器: $csc" }
foreach ($f in @('dist\DeltaForceTuner.exe', 'dist\使用说明.txt', 'dist\preview.png', 'dist\SHA256.txt', 'README.md', 'assets\app.ico')) {
    if (-not (Test-Path $f)) { throw "缺少文件: $f（请先运行 build.ps1）" }
}

$build = Join-Path $root 'build'
New-Item -ItemType Directory -Force -Path $build | Out-Null
New-Item -ItemType Directory -Force -Path 'dist' | Out-Null

$refs = @(
    'System.dll',
    'System.Core.dll',
    'System.Drawing.dll',
    'System.Windows.Forms.dll',
    'System.Xml.dll'
)
$refArgs = @()
foreach ($r in $refs) { $refArgs += "/reference:$r" }

$installerSources = @(
    'installer\InstallerCore.cs',
    'installer\InstallerUi.cs',
    'installer\InstallerProgram.cs',
    'installer\AssemblyInfo.cs',
    'src\Theme.cs'
)
$files = $installerSources | ForEach-Object { (Resolve-Path $_).Path }

Write-Host '编译卸载程序...'
& $csc /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 /define:UNINSTALL `
    /win32manifest:installer\setup.manifest /win32icon:assets\app.ico `
    "/out:$build\Uninstall.exe" @refArgs $files
if ($LASTEXITCODE -ne 0) { throw "卸载程序编译失败 ($LASTEXITCODE)" }

Write-Host '准备安装资源...'
Copy-Item 'dist\DeltaForceTuner.exe' "$build\app.exe" -Force
Copy-Item "$build\Uninstall.exe" "$build\uninstall.exe" -Force
Copy-Item 'dist\使用说明.txt' "$build\readme.txt" -Force
Copy-Item 'README.md' "$build\README.md" -Force
Copy-Item 'dist\preview.png' "$build\preview.png" -Force
Copy-Item 'dist\SHA256.txt' "$build\SHA256.txt" -Force

Write-Host '编译安装程序...'
$setupResources = @(
    "/resource:$build\app.exe,payload/app.exe",
    "/resource:$build\uninstall.exe,payload/uninstall.exe",
    "/resource:$build\readme.txt,payload/readme.txt",
    "/resource:$build\README.md,payload/README.md",
    "/resource:$build\preview.png,payload/preview.png",
    "/resource:$build\SHA256.txt,payload/sha256.txt"
)
& $csc /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 `
    /win32manifest:installer\setup.manifest /win32icon:assets\app.ico `
    "/out:dist\DeltaForceTunerSetup.exe" @refArgs @setupResources $files
if ($LASTEXITCODE -ne 0) { throw "安装程序编译失败 ($LASTEXITCODE)" }

$hash = (Get-FileHash 'dist\DeltaForceTunerSetup.exe' -Algorithm SHA256).Hash
$appHash = (Get-FileHash 'dist\DeltaForceTuner.exe' -Algorithm SHA256).Hash
@"
$hash  DeltaForceTunerSetup.exe
App SHA256: $appHash
Name: 三角洲行动 · 本机调优助手
Version: 1.0.1.0
Build: $(Get-Date -Format 'yyyy-MM-dd')
"@ | Set-Content 'dist\Setup-SHA256.txt' -Encoding UTF8

"GUI:  dist\DeltaForceTuner.exe"
"Setup: dist\DeltaForceTunerSetup.exe"
"Setup SHA256: $hash"
