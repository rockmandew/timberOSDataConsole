<#
    Builds the mod and installs it into the local Timberborn Mods folder so the game
    loads it on next launch. Windows / PowerShell.

    The mod compiles against the game's own assemblies, so the build needs to know where
    Timberborn is installed. This script finds it automatically:

      1. -TimberbornManaged parameter, if you pass one.
      2. $env:TIMBERBORN_MANAGED, if set.
      3. Auto-detection: every Steam library on every drive (parsed from Steam's
         libraryfolders.vdf) plus a few common install paths.
      4. If none of those work, it asks you where Timberborn is installed.

    Usage:
        pwsh scripts/package-mod.ps1
        pwsh scripts/package-mod.ps1 -Configuration Debug
        pwsh scripts/package-mod.ps1 -TimberbornManaged "D:\SteamLibrary\steamapps\common\Timberborn\Timberborn_Data\Managed"
        # or point at the Timberborn folder itself and let the script find Managed:
        pwsh scripts/package-mod.ps1 -TimberbornManaged "D:\SteamLibrary\steamapps\common\Timberborn"
#>
param(
    [string]$Configuration = "Release",
    [string]$ModsRoot = "",
    [string]$TimberbornManaged = ""
)

$ErrorActionPreference = "Stop"
$root = Split-Path -Parent $PSScriptRoot
$modProject = Join-Path $root "mod\TimberOS.DataConsole.csproj"
$manifest = Join-Path $root "mod\manifest.json"
$modId = "rockmandew.TimberOSDataConsole"

# Resolve the game's Mods folder under the user's real Documents path. GetFolderPath
# honors OneDrive's "Documents" redirection (common on Windows 11), so we don't
# hardcode $USERPROFILE\Documents, which is the *wrong* folder when Documents is
# redirected and would silently install the mod where the game never looks.
if ([string]::IsNullOrWhiteSpace($ModsRoot)) {
    $docs = [Environment]::GetFolderPath('MyDocuments')
    if ([string]::IsNullOrWhiteSpace($docs)) { $docs = Join-Path $env:USERPROFILE "Documents" }
    $ModsRoot = Join-Path $docs "Timberborn\Mods"
}
$target = Join-Path $ModsRoot $modId

# Accept either the ...\Timberborn_Data\Managed folder or the Timberborn game folder
# (or its parent) and normalize to the Managed folder. Returns $null if it doesn't
# resolve to a folder that actually holds the game's assemblies.
function Resolve-Managed([string]$path) {
    if ([string]::IsNullOrWhiteSpace($path)) { return $null }
    $path = $path.Trim().Trim('"')
    $candidates = @(
        $path,
        (Join-Path $path "Timberborn_Data\Managed"),
        (Join-Path $path "Timberborn\Timberborn_Data\Managed")
    )
    foreach ($c in $candidates) {
        if (Test-Path $c) {
            $hasGame = Get-ChildItem -Path $c -Filter "Timberborn.*.dll" -ErrorAction SilentlyContinue | Select-Object -First 1
            if ($hasGame) { return (Resolve-Path $c).Path }
        }
    }
    return $null
}

# Parse Steam's libraryfolders.vdf to find every Steam library across all drives, then
# return the Timberborn Managed folder from whichever library actually has the game.
function Find-SteamManaged {
    $steamRoots = @()

    # Steam install path from the registry (covers non-default Steam installs).
    foreach ($key in @("HKCU:\Software\Valve\Steam", "HKLM:\SOFTWARE\WOW6432Node\Valve\Steam", "HKLM:\SOFTWARE\Valve\Steam")) {
        try {
            $p = (Get-ItemProperty -Path $key -ErrorAction Stop).SteamPath
            if ($p) { $steamRoots += $p }
        } catch { }
    }
    # Common defaults as a backstop.
    $steamRoots += @(
        "C:\Program Files (x86)\Steam",
        "C:\Program Files\Steam",
        "$env:ProgramFiles\Steam",
        "${env:ProgramFiles(x86)}\Steam"
    )

    $libraries = New-Object System.Collections.Generic.List[string]
    foreach ($steam in ($steamRoots | Where-Object { $_ } | Select-Object -Unique)) {
        if (Test-Path $steam) { $libraries.Add($steam) }
        $vdf = Join-Path $steam "steamapps\libraryfolders.vdf"
        if (Test-Path $vdf) {
            # Lines look like:  "path"   "D:\\SteamLibrary"
            foreach ($m in [regex]::Matches((Get-Content $vdf -Raw), '"path"\s*"([^"]+)"')) {
                $libraries.Add($m.Groups[1].Value.Replace('\\', '\'))
            }
        }
    }

    foreach ($lib in ($libraries | Select-Object -Unique)) {
        $managed = Resolve-Managed (Join-Path $lib "steamapps\common\Timberborn")
        if ($managed) { return $managed }
    }
    return $null
}

# --- Resolve the game's Managed folder ------------------------------------------------
$managed = Resolve-Managed $TimberbornManaged
if (-not $managed -and $env:TIMBERBORN_MANAGED) { $managed = Resolve-Managed $env:TIMBERBORN_MANAGED }
if (-not $managed) {
    Write-Host "Looking for your Timberborn install..." -ForegroundColor Cyan
    $managed = Find-SteamManaged
}

# Last resort: ask the user (only if we're attached to an interactive console).
if (-not $managed) {
    Write-Host "Couldn't find Timberborn automatically." -ForegroundColor Yellow
    if ([Environment]::UserInteractive -and -not [Console]::IsInputRedirected) {
        Write-Host "Enter the path to your Timberborn folder (the one containing Timberborn.exe)," -ForegroundColor Yellow
        Write-Host "for example: D:\SteamLibrary\steamapps\common\Timberborn" -ForegroundColor Yellow
        $entered = Read-Host "Timberborn path"
        $managed = Resolve-Managed $entered
    }
}

if (-not $managed) {
    throw @"
Could not locate the Timberborn game assemblies.

Find your Timberborn folder (it contains Timberborn.exe), then re-run with:

    pwsh scripts/package-mod.ps1 -TimberbornManaged "D:\YourPath\Timberborn"

or set it once for your session:

    `$env:TIMBERBORN_MANAGED = "D:\YourPath\Timberborn\Timberborn_Data\Managed"
    pwsh scripts/package-mod.ps1
"@
}

Write-Host "Using Timberborn assemblies: $managed" -ForegroundColor Green

# --- Build ----------------------------------------------------------------------------
Write-Host "Building mod ($Configuration)..." -ForegroundColor Cyan
dotnet build $modProject -c $Configuration -v minimal "-p:TimberbornManaged=$managed"
if ($LASTEXITCODE -ne 0) { throw "Build failed (dotnet build exit code $LASTEXITCODE)." }

$dll = Join-Path $root "mod\bin\$Configuration\TimberOS.DataConsole.dll"
if (-not (Test-Path $dll)) { throw "Build output not found: $dll" }

New-Item -ItemType Directory -Force -Path $target | Out-Null
Copy-Item $dll     -Destination $target -Force
Copy-Item $manifest -Destination $target -Force

Write-Host "Installed to $target" -ForegroundColor Green
Write-Host "Launch Timberborn, load a settlement, then GET http://localhost:8080/timberos/v1/snapshot" -ForegroundColor Green
