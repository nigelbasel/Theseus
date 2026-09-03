<#
.SYNOPSIS
    Builds the Theseus MSI.

.DESCRIPTION
    Publishes a Release build, checks the launcher shim came with it, and packages the result with
    the WiX Toolset. The MSI installs to Program Files, adds a Start Menu shortcut, and replaces any
    previous version.

    Unlike Deploy-Theseus.ps1 this needs no elevation: it only produces the .msi. Installing it does.

.PARAMETER OutputDirectory
    Where to write the .msi. Defaults to dist\ in the repository.

.PARAMETER SelfContained
    Bundle the .NET runtime, so the MSI installs on a machine without the desktop runtime. Much
    larger, and only worth it for a machine that is not this one.

.EXAMPLE
    .\Build-Installer.ps1
    Produces dist\Theseus-<version>.msi.
#>
[CmdletBinding()]
param(
    [string] $OutputDirectory,
    [switch] $SelfContained
)

$ErrorActionPreference = 'Stop'

$repository = Split-Path -Parent $PSScriptRoot
$project = Join-Path $repository 'src\Theseus.App\Theseus.App.csproj'
$source = Join-Path $repository 'installer\Theseus.wxs'
$icon = Join-Path $repository 'src\Theseus.App\Assets\theseus.ico'
$installerDir = Join-Path $repository 'installer'
$staging = Join-Path ([System.IO.Path]::GetTempPath()) ("theseus-msi-" + [Guid]::NewGuid().ToString('N'))

if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $repository 'dist'
}

function Resolve-Wix {
    $command = Get-Command wix -ErrorAction SilentlyContinue
    if ($command) { return $command.Source }

    $installed = 'C:\Program Files\WiX Toolset v6.0\bin\wix.exe'
    if (Test-Path $installed) { return $installed }

    throw @'
The WiX Toolset was not found.

Install it with:
    dotnet tool install --global wix
or from https://wixtoolset.org.
'@
}

$wix = Resolve-Wix

Write-Host 'Theseus installer' -ForegroundColor Cyan
Write-Host "  repository  $repository"
Write-Host "  wix         $wix"
Write-Host ''
Write-Host 'Publishing...' -ForegroundColor Cyan

& dotnet publish $project -c Release -r win-x64 -o $staging --nologo "-p:SelfContained=$([bool]$SelfContained)"
if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE."
}

$application = Join-Path $staging 'Theseus.exe'
$shim = Join-Path $staging 'Theseus.Launcher.exe'

if (-not (Test-Path $application)) { throw "The publish produced no Theseus.exe in $staging." }
if (-not (Test-Path $shim)) { throw 'The publish produced no Theseus.Launcher.exe. Every Start button would be dead.' }

$hook = Join-Path $staging 'Theseus.Hook.exe'
if (-not (Test-Path $hook)) { throw 'The publish produced no Theseus.Hook.exe. Stop could only ever use the clipboard.' }

# The full stamp identifies the build; the MSI gets only the date, which is all it can carry.
$stamp = (Get-Item $application).VersionInfo.FileVersion
$parts = $stamp.Split('.')
$productVersion = '{0}.{1}.{2}' -f ([int]$parts[0] - 2000), [int]$parts[1], [int]$parts[2]

Write-Host ''
Write-Host "  file version    $stamp"
Write-Host "  msi version     $productVersion"

New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$msi = Join-Path $OutputDirectory ("Theseus-$stamp.msi")

Write-Host ''
Write-Host 'Packaging...' -ForegroundColor Cyan

& $wix build $source `
    -arch x64 `
    -ext WixToolset.UI.wixext `
    -d "PublishDir=$staging" `
    -d "ProductVersion=$productVersion" `
    -d "IconPath=$icon" `
    -d "InstallerDir=$installerDir" `
    -o $msi

if ($LASTEXITCODE -ne 0) {
    throw "wix build failed with exit code $LASTEXITCODE."
}

Remove-Item $staging -Recurse -Force

$size = [Math]::Round((Get-Item $msi).Length / 1MB, 1)

Write-Host ''
Write-Host 'Built.' -ForegroundColor Green
Write-Host "  $msi  (${size} MB)"
Write-Host ''
Write-Host 'Install it by double-clicking, or:'
Write-Host "    msiexec /i `"$msi`""
Write-Host ''
Write-Host 'Theseus keeps its board, runs, settings and logs in %LOCALAPPDATA%\Theseus,'
Write-Host 'which the installer neither writes nor removes.'
