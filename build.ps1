$ErrorActionPreference = 'Stop'
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /target:winexe /platform:x64 /out:"$PSScriptRoot\KeyCraft.exe" /win32icon:"$PSScriptRoot\assets\keyboard.ico" /reference:System.Windows.Forms.dll /reference:System.Drawing.dll /reference:System.Management.dll "$PSScriptRoot\KeyCraft.cs" "$PSScriptRoot\NativeFeatures.cs" "$PSScriptRoot\MainWindow.cs" "$PSScriptRoot\MonitorBrightness.cs"
if ($LASTEXITCODE -ne 0) { throw 'Compilation failed' }
