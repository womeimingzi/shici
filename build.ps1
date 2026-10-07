param([string]$OutputDirectory = 'dist')
$ErrorActionPreference = 'Stop'
$projectRoot = $PSScriptRoot
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$compiler = Join-Path $framework 'csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) { throw 'This build requires the Windows .NET Framework 4.x compiler.' }
$outputDir = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $OutputDirectory))
if (-not $outputDir.StartsWith($projectRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Output must stay inside the project directory.' }
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
Add-Type -AssemblyName System.Drawing
$iconPath = Join-Path $outputDir 'shici.ico'
$bitmap = New-Object System.Drawing.Bitmap 32,32
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.Clear([System.Drawing.Color]::FromArgb(24,92,69))
$pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::White),2
$graphics.DrawLine($pen,8,8,24,8)
$graphics.DrawLine($pen,8,14,20,14)
$graphics.DrawLine($pen,8,20,15,20)
$graphics.DrawEllipse($pen,18,18,7,7)
$graphics.DrawLine($pen,24,24,28,28)
$icon = [System.Drawing.Icon]::FromHandle($bitmap.GetHicon())
$iconStream = [System.IO.File]::Create($iconPath)
try { $icon.Save($iconStream) } finally { $iconStream.Dispose(); $icon.Dispose(); $pen.Dispose(); $graphics.Dispose(); $bitmap.Dispose() }
$references = @('System.dll','System.Core.dll','System.Drawing.dll','System.Windows.Forms.dll','System.Xaml.dll','System.Net.Http.dll','System.Web.Extensions.dll','System.Security.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','WPF\UIAutomationClient.dll','WPF\UIAutomationTypes.dll')
$compilerArgs = @('/nologo','/target:winexe','/platform:x64','/optimize+','/codepage:65001',('/win32manifest:' + (Join-Path $projectRoot 'app.manifest')),('/out:' + (Join-Path $outputDir '拾词.exe')))
$compilerArgs += '/win32icon:' + $iconPath
$compilerArgs += '/resource:' + (Join-Path $projectRoot 'ui\Main.xaml') + ',Shici.Main.xaml'
foreach ($reference in $references) { $compilerArgs += '/reference:' + (Join-Path $framework $reference) }
$compilerArgs += @(Get-ChildItem -LiteralPath (Join-Path $projectRoot 'src') -Filter '*.cs' | ForEach-Object { $_.FullName })
& $compiler @compilerArgs
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Copy-Item -LiteralPath (Join-Path $projectRoot 'App.config') -Destination (Join-Path $outputDir '拾词.exe.config') -Force
Write-Output (Join-Path $outputDir '拾词.exe')
