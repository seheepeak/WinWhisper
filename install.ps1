<#
.SYNOPSIS
    Builds WinWhisper in Release and installs it as a per-user startup app.

.DESCRIPTION
    1. Stops any running WinWhisper instance.
    2. Publishes a Release build directly into %LocalAppData%\Programs\WinWhisper.
    3. Creates a shortcut in the user's Startup folder so the app
       launches automatically on next login.

    Re-running this script performs an in-place update.
#>

[CmdletBinding()]
param(
    [switch]$NoLaunch
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$ProjectRoot  = Split-Path -Parent $MyInvocation.MyCommand.Path
$InstallDir   = Join-Path $env:LOCALAPPDATA 'Programs\WinWhisper'
$ExePath      = Join-Path $InstallDir 'WinWhisper.exe'
$StartupDir   = [Environment]::GetFolderPath('Startup')
$ShortcutPath = Join-Path $StartupDir 'WinWhisper.lnk'

function Write-Step($Message) {
    Write-Host "==> $Message" -ForegroundColor Cyan
}

Write-Step "Stopping any running WinWhisper instance"
Get-Process -Name 'WinWhisper' -ErrorAction SilentlyContinue | ForEach-Object {
    $_ | Stop-Process -Force
    $_.WaitForExit(3000) | Out-Null
}

Write-Step "Cleaning $InstallDir"
if (Test-Path $InstallDir) {
    Remove-Item -Recurse -Force $InstallDir
}

Write-Step "Publishing Release build to $InstallDir"
& dotnet publish $ProjectRoot -c Release -o $InstallDir
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

Write-Step "Creating Startup shortcut at $ShortcutPath"
$WshShell = New-Object -ComObject WScript.Shell
$Shortcut = $WshShell.CreateShortcut($ShortcutPath)
$Shortcut.TargetPath       = $ExePath
$Shortcut.WorkingDirectory = $InstallDir
$Shortcut.IconLocation     = $ExePath
$Shortcut.Description      = 'WinWhisper - speech-to-text'
$Shortcut.Save()

Write-Host ""
Write-Host "Done." -ForegroundColor Green
Write-Host "  Installed: $ExePath"
Write-Host "  Shortcut:  $ShortcutPath"
Write-Host "  WinWhisper will start automatically on next login."

if (-not $NoLaunch) {
    Write-Host ""
    Write-Step "Launching WinWhisper"
    Start-Process -FilePath $ExePath
}
