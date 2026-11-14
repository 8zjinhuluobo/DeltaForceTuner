$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $root

$gpp = 'D:\APPs_D\C++\RedPanda-Cpp\MinGW32\bin\g++.exe'
$windres = 'D:\APPs_D\C++\RedPanda-Cpp\MinGW32\bin\windres.exe'
if (-not (Test-Path $gpp)) { throw "找不到 g++: $gpp" }
if (-not (Test-Path $windres)) { throw "找不到 windres: $windres" }
foreach ($f in @('dist\DeltaForceTuner.exe', 'dist\使用说明.txt', 'dist\preview.png', 'dist\SHA256.txt', 'README.md', 'assets\app.ico')) {
    if (-not (Test-Path $f)) { throw "缺少文件: $f（请先运行 build.ps1）" }
}

$build = Join-Path $root 'build-cpp'
New-Item -ItemType Directory -Force -Path $build | Out-Null
New-Item -ItemType Directory -Force -Path 'dist' | Out-Null

Copy-Item 'dist\DeltaForceTuner.exe' "$build\app.exe" -Force
Copy-Item 'dist\使用说明.txt' "$build\readme.txt" -Force
Copy-Item 'README.md' "$build\README.md" -Force
Copy-Item 'dist\preview.png' "$build\preview.png" -Force
Copy-Item 'dist\SHA256.txt' "$build\SHA256.txt" -Force

$libs = @('-lcomctl32','-lole32','-luuid','-lshell32','-lshlwapi','-ladvapi32','-luser32','-lgdi32')
$common = @('-std=c++11','-municode','-mwindows','-O2','-s','-static','-static-libgcc','-static-libstdc++')

Write-Host '编译卸载程序...'
& $windres -I installer-cpp -I . -O coff -o "$build\uninstall.res.o" installer-cpp\uninstall.rc
if ($LASTEXITCODE -ne 0) { throw "卸载程序资源编译失败 ($LASTEXITCODE)" }
& $gpp @common -DUNINSTALL installer-cpp\installer.cpp "$build\uninstall.res.o" -o "$build\Uninstall.exe" @libs
if ($LASTEXITCODE -ne 0) { throw "卸载程序编译失败 ($LASTEXITCODE)" }

Write-Host '编译安装程序...'
& $windres -I installer-cpp -I . -O coff -o "$build\setup.res.o" installer-cpp\setup.rc
if ($LASTEXITCODE -ne 0) { throw "安装程序资源编译失败 ($LASTEXITCODE)" }
& $gpp @common installer-cpp\installer.cpp "$build\setup.res.o" -o 'dist\DeltaForceTunerSetup.exe' @libs
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

"Setup: dist\DeltaForceTunerSetup.exe"
"Setup SHA256: $hash"
"Uninstaller: build-cpp\Uninstall.exe"
