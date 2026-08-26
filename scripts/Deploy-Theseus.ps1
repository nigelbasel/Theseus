<#
.SYNOPSIS
    Publishes Theseus and installs it into Program Files.

.DESCRIPTION
    Builds a Release publish of the WPF application, checks that the launcher shim came with it,
    stops any running copy, and replaces the installed files.

    The shim matters: Windows Terminal starts it in each tab, and without it every Start button is
    dead. It is verified before anything is copied rather than discovered missing at first use.

.PARAMETER Destination
    Where to install. Defaults to C:\Program Files\Theseus, which needs an elevated terminal.

.PARAMETER SelfContained
    Bundle the .NET runtime. Slower and much larger, but does not need the desktop runtime installed.

.PARAMETER NoShortcut
    Skip creating the Start Menu shortcut.

.EXAMPLE
    .\Deploy-Theseus.ps1
    Publishes and installs to C:\Program Files\Theseus. Run from an elevated terminal.
#>
[CmdletBinding()]
param(
    [string] $Destination = 'C:\Program Files\Theseus',
    [switch] $SelfContained,
    [switch] $NoShortcut
)

$ErrorActionPreference = 'Stop'

$repository = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repository 'src\Theseus.App\Theseus.App.csproj'
$staging = Join-Path ([System.IO.Path]::GetTempPath()) ("theseus-publish-" + [Guid]::NewGuid().ToString('N'))

function Test-Elevated {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

Write-Host "Theseus deploy" -ForegroundColor Cyan
Write-Host "  repository   $repository"
Write-Host "  destination  $Destination"

# Fail on the permission problem now, with a usable instruction, rather than half way through the copy.
$parent = Split-Path -Parent $Destination
if (-not (Test-Elevated)) {
    try {
        $probe = Join-Path $parent ([Guid]::NewGuid().ToString('N'))
        New-Item -ItemType Directory -Path $probe -ErrorAction Stop | Out-Null
        Remove-Item $probe -Force
    }
    catch {
        Write-Host ''
        Write-Host "  $Destination needs an elevated terminal." -ForegroundColor Yellow
        Write-Host '  Open Windows Terminal as administrator and run this script again,'
        Write-Host '  or pass -Destination with somewhere you can write.'
        exit 1
    }
}

Write-Host ''
Write-Host 'Publishing...' -ForegroundColor Cyan

$arguments = @(
    'publish', $project,
    '-c', 'Release',
    '-r', 'win-x64',
    '-o', $staging,
    '--nologo',
    "-p:SelfContained=$([bool]$SelfContained)"
)

& dotnet @arguments
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$application = Join-Path $staging 'Theseus.exe'
$shim = Join-Path $staging 'Theseus.Launcher.exe'

if (-not (Test-Path $application)) { throw "The publish produced no Theseus.exe in $staging." }
if (-not (Test-Path $shim)) { throw "The publish produced no Theseus.Launcher.exe. Every Start button would be dead." }

$running = Get-Process -Name 'Theseus' -ErrorAction SilentlyContinue
if ($running) {
    Write-Host ''
    Write-Host "Stopping $($running.Count) running copy..." -ForegroundColor Cyan
    $running | Stop-Process -Force
    Start-Sleep -Milliseconds 500
}

Write-Host ''
Write-Host 'Installing...' -ForegroundColor Cyan

New-Item -ItemType Directory -Path $Destination -Force | Out-Null
Copy-Item -Path (Join-Path $staging '*') -Destination $Destination -Recurse -Force

Remove-Item $staging -Recurse -Force

if (-not $NoShortcut) {
    $startMenu = Join-Path ([Environment]::GetFolderPath('CommonPrograms')) 'Theseus.lnk'
    try {
        $shell = New-Object -ComObject WScript.Shell
        $shortcut = $shell.CreateShortcut($startMenu)
        $shortcut.TargetPath = Join-Path $Destination 'Theseus.exe'
        $shortcut.WorkingDirectory = $Destination
        $shortcut.Description = 'Theseus - Claude Code session orchestrator'
        # Named explicitly rather than left to inherit: a shortcut created before the executable had
        # an icon keeps showing the generic one, and a pinned copy of it caches that indefinitely.
        $shortcut.IconLocation = "$(Join-Path $Destination 'Theseus.exe'),0"
        $shortcut.Save()
        Write-Host "  shortcut     $startMenu"
    }
    catch {
        Write-Host "  shortcut     skipped ($($_.Exception.Message))" -ForegroundColor Yellow
    }
}

$version = (Get-Item (Join-Path $Destination 'Theseus.exe')).VersionInfo.FileVersion

Write-Host ''
Write-Host 'Installed.' -ForegroundColor Green
Write-Host "  $Destination\Theseus.exe  ($version)"
Write-Host "  launcher shim present     $(Test-Path (Join-Path $Destination 'Theseus.Launcher.exe'))"
Write-Host ''
Write-Host 'Theseus keeps its board, runs and settings in %LOCALAPPDATA%\Theseus,'
Write-Host 'so an installed copy and a development build share the same state.'
