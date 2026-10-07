$ErrorActionPreference = 'Stop'
$framework = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319'
$testOutput = Join-Path $PSScriptRoot 'dist\tests'
New-Item -ItemType Directory -Force -Path $testOutput | Out-Null
$testArgs = @('/nologo','/target:exe','/platform:x64','/codepage:65001',('/out:' + (Join-Path $testOutput 'Checks.exe')))
foreach ($assembly in @('System.dll','System.Core.dll','System.Xaml.dll','System.Security.dll','System.Net.Http.dll','System.Web.Extensions.dll','WPF\WindowsBase.dll','WPF\PresentationCore.dll','WPF\PresentationFramework.dll','WPF\UIAutomationClient.dll','WPF\UIAutomationTypes.dll')) { $testArgs += '/reference:' + (Join-Path $framework $assembly) }
foreach ($source in @('TextTools.cs','CapturePolicy.cs','CaptureResult.cs','CaptureEngine.cs','ClipboardCapture.cs','ReleaseGate.cs','Native.cs','TranslationClient.cs','Settings.cs','SettingsWindow.cs','WindowPreferences.cs','WindowLayout.cs')) { $testArgs += Join-Path $PSScriptRoot ('src\' + $source) }
$testArgs += Join-Path $PSScriptRoot 'tests\TestRunner.cs'
$testArgs += Join-Path $PSScriptRoot 'tests\UiSmoke.cs'
& (Join-Path $framework 'csc.exe') @testArgs
if ($LASTEXITCODE -ne 0) { throw 'Test compilation failed.' }
& (Join-Path $testOutput 'Checks.exe')
if ($LASTEXITCODE -ne 0) { throw 'Checks failed.' }
