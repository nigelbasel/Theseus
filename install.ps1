<#
.SYNOPSIS
    Builds Theseus and installs it. One command, from a normal terminal.

.DESCRIPTION
    Packages the current source into an MSI and installs it, asking for elevation only for the
    install itself. This is the script to run when you want the version you are working on to become
    the version on the Start Menu.

    Running sessions are not disturbed: they run a copy of the launcher rather than the installed
    one, so the installer can replace it underneath them, and they pick up the new one when they
    next start. Sessions old enough to predate that are still at risk, and are counted first.

.PARAMETER Force
    Install without asking, even with sessions running the installed launcher. They will be lost.

.PARAMETER BuildOnly
    Produce the MSI in dist\ and stop, without installing it.

.PARAMETER SelfContained
    Bundle the .NET runtime, for a machine without the desktop runtime installed.

.EXAMPLE
    .\install.ps1
    Builds and installs. Prompts for elevation.

.EXAMPLE
    .\install.ps1 -BuildOnly
    Just writes dist\Theseus-<version>.msi.
#>
[CmdletBinding()]
param(
    [switch] $Force,
    [switch] $BuildOnly,
    [switch] $SelfContained
)

$ErrorActionPreference = 'Stop'

$repository = Split-Path -Parent $PSCommandPath
$builder = Join-Path $repository 'scripts\Build-Installer.ps1'

function Get-SessionsAtRisk {
    # Sessions run a copy of the launcher, so the installer can replace the original underneath them.
    # Only a session still running the installed launcher - one started before that change shipped -
    # would be closed by the install.
    $kits = Join-Path $env:LOCALAPPDATA 'Theseus\launcher'
    $shims = @(Get-Process -Name 'Theseus.Launcher' -ErrorAction SilentlyContinue)

    return @($shims | Where-Object { $_.Path -and -not $_.Path.StartsWith($kits, 'OrdinalIgnoreCase') }).Count
}

if (-not $BuildOnly) {
    $running = Get-SessionsAtRisk

    if ($running -gt 0 -and -not $Force) {
        Write-Host ''
        Write-Host "$running session(s) are still running the installed launcher." -ForegroundColor Yellow
        Write-Host 'Installing replaces the files those tabs are running, which closes them.'
        Write-Host 'They will not get the chance to write a handoff.'
        Write-Host ''
        Write-Host 'Stop them from Theseus first (Stop all), or re-run with -Force to lose them.'
        Write-Host ''

        $answer = Read-Host 'Install anyway? (y/N)'
        if ($answer -ne 'y') {
            Write-Host 'Nothing was installed.' -ForegroundColor Cyan
            return
        }
    }
}

& $builder -SelfContained:$SelfContained
if ($LASTEXITCODE -ne 0 -and $null -ne $LASTEXITCODE) {
    throw "Building the installer failed with exit code $LASTEXITCODE."
}

$msi = Get-ChildItem (Join-Path $repository 'dist') -Filter 'Theseus-*.msi' |
    Sort-Object LastWriteTime -Descending |
    Select-Object -First 1

if ($null -eq $msi) {
    throw 'The build reported success but produced no .msi in dist\.'
}

if ($BuildOnly) {
    Write-Host ''
    Write-Host 'Built, not installed.' -ForegroundColor Green
    Write-Host "  $($msi.FullName)"
    return
}

Write-Host ''
Write-Host 'Installing...' -ForegroundColor Cyan
Write-Host '  Windows will ask for administrator rights.'

$log = Join-Path $env:TEMP 'theseus-install.log'
$arguments = '/i "{0}" /passive /norestart /l*v "{1}"' -f $msi.FullName, $log

$process = Start-Process -FilePath 'msiexec.exe' -ArgumentList $arguments -Verb RunAs -Wait -PassThru

if ($process.ExitCode -eq 3010) {
    Write-Host ''
    Write-Host 'Installed, but Windows wants a restart to finish replacing a file in use.' -ForegroundColor Yellow
    Write-Host 'Close any terminal tabs running Theseus sessions and re-run this script to avoid that.'
    return
}

if ($process.ExitCode -ne 0) {
    throw "msiexec failed with exit code $($process.ExitCode). The log is at $log."
}

Write-Host ''
Write-Host 'Installed.' -ForegroundColor Green
Write-Host '  C:\Program Files\Theseus\Theseus.exe'
Write-Host '  Start Menu > Theseus'
Write-Host ''
Write-Host 'The board, runs, settings and logs in %LOCALAPPDATA%\Theseus were left alone.'
