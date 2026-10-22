# QuickInput 一键编译
# 用 Windows 自带的 C# 编译器，不需要装任何东西。

$ErrorActionPreference = 'Stop'
$here = Split-Path -Parent $MyInvocation.MyCommand.Path
Set-Location $here

$csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path $csc)) {
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
if (-not (Test-Path $csc)) {
    Write-Host '找不到 .NET Framework 的 csc.exe，无法编译。' -ForegroundColor Red
    exit 1
}

Write-Host "编译器: $csc"
Write-Host '正在编译...'

& $csc /nologo /target:winexe /out:QuickInput.exe /optimize+ /codepage:65001 `
    QuickInput.cs `
    /resource:QuickInput.ico /win32icon:QuickInput.ico `
    /r:System.dll /r:System.Windows.Forms.dll /r:System.Drawing.dll

if ($LASTEXITCODE -ne 0) {
    Write-Host '编译失败。' -ForegroundColor Red
    exit $LASTEXITCODE
}

if (Test-Path 'QuickInput.exe') {
    $size = [math]::Round((Get-Item 'QuickInput.exe').Length / 1KB, 1)
    Write-Host "编译成功: QuickInput.exe ($size KB)" -ForegroundColor Green
    Write-Host ''
    Write-Host '直接双击 QuickInput.exe 即可运行（会在托盘出现图标）。'
    Write-Host '首次运行会自动生成 qui.ini。'
} else {
    Write-Host '编译结束但没有产出 exe。' -ForegroundColor Red
    exit 1
}
