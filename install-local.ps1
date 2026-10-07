param([string]$BuildDirectory = 'dist\0.3', [switch]$SkipDesktopShortcut)
$ErrorActionPreference = 'Stop'
$projectRoot = [System.IO.Path]::GetFullPath($PSScriptRoot)
$buildRoot = [System.IO.Path]::GetFullPath((Join-Path $projectRoot $BuildDirectory))
if (-not $buildRoot.StartsWith($projectRoot + '\', [StringComparison]::OrdinalIgnoreCase)) { throw 'Build directory must stay inside the project.' }
foreach ($name in @('拾词.exe','拾词.exe.config','shici.ico')) {
    if (-not (Test-Path -LiteralPath (Join-Path $buildRoot $name) -PathType Leaf)) { throw 'Build files are missing. Run build.ps1 first.' }
}
try {
    foreach ($name in @('拾词.exe','拾词.exe.config','shici.ico')) {
        Copy-Item -LiteralPath (Join-Path $buildRoot $name) -Destination (Join-Path $projectRoot $name) -Force
    }
} catch { throw '无法更新拾词。请先右键托盘图标退出拾词，再重新运行安装脚本。' }
$executable = Join-Path $projectRoot '拾词.exe'
if (-not $SkipDesktopShortcut) {
    $desktopDirectory = [Environment]::GetFolderPath('Desktop')
    $shellObject = New-Object -ComObject WScript.Shell
    try {
        $shortcut = $shellObject.CreateShortcut((Join-Path $desktopDirectory '拾词.lnk'))
        $shortcut.TargetPath = $executable
        $shortcut.WorkingDirectory = $projectRoot
        $shortcut.IconLocation = $executable + ',0'
        $shortcut.Description = '拾词 · Alt + E 划词翻译'
        $shortcut.Save()
        [System.Runtime.InteropServices.Marshal]::ReleaseComObject($shortcut) | Out-Null
    } finally { [System.Runtime.InteropServices.Marshal]::ReleaseComObject($shellObject) | Out-Null }
    Write-Output (Join-Path $desktopDirectory '拾词.lnk')
}
Write-Output $executable
