<#
    Builds the mod and installs it into the local Timberborn Mods folder so the game
    loads it on next launch. Windows / PowerShell.

    Usage:
        pwsh scripts/package-mod.ps1
        pwsh scripts/package-mod.ps1 -Configuration Debug
#>
param(
    [string]$Configuration = "Release",
    [string]$ModsRoot = "$env:USERPROFILE\Documents\Timberborn\Mods"
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$modProject = Join-Path $root "mod\TimberOS.DataConsole.csproj"
$manifest = Join-Path $root "mod\manifest.json"
$modId = "rockmandew.TimberOSDataConsole"
$target = Join-Path $ModsRoot $modId

Write-Host "Building mod ($Configuration)..." -ForegroundColor Cyan
dotnet build $modProject -c $Configuration -v minimal

$dll = Join-Path $root "mod\bin\$Configuration\TimberOS.DataConsole.dll"
if (-not (Test-Path $dll)) { throw "Build output not found: $dll" }

New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item $dll     -Destination $target -Force
Copy-Item $manifest -Destination $target -Force

Write-Host "Installed to $target" -ForegroundColor Green
Write-Host "Launch Timberborn, load a settlement, then GET http://localhost:8080/timberos/v1/snapshot" -ForegroundColor Green
