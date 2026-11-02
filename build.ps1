$ErrorActionPreference = 'Stop'
$root = $PSScriptRoot
New-Item -ItemType Directory -Force -Path (Join-Path $root 'dist') | Out-Null
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw '需要 Windows x64 和 .NET Framework 4.8。' }
$references = @('System.dll','System.Core.dll','System.Xaml.dll','System.Drawing.dll','System.Windows.Forms.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll')
$arguments = @('/nologo','/target:winexe','/platform:x64','/optimize+','/utf8output','/codepage:65001',('/out:' + (Join-Path $root 'dist\BudsDesk.exe')),('/resource:' + (Join-Path $root 'src\MainWindow.xaml') + ',MainWindow.xaml'))
foreach ($ref in $references) { $arguments += '/reference:' + (Join-Path $framework $ref) }
$arguments += (Get-ChildItem -LiteralPath (Join-Path $root 'src') -Filter '*.cs').FullName
& $compiler @arguments
if ($LASTEXITCODE -ne 0) { throw '编译失败。' }
Write-Output (Join-Path $root 'dist\BudsDesk.exe')
