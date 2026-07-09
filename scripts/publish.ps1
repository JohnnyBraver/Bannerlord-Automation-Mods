# Scripts/publish.ps1
param(
    [string[]]$Mods = @(),
    [string]$BannerlordGameRoot = "E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord",
    [string]$BannerlordModulesRoot = "",
    [string]$UIExtenderExPath = "",
    [string]$MCMv5Path = ""
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $repoRoot "publish"
$tempPublishDir = Join-Path $repoRoot ".tmp_publish"

if ([string]::IsNullOrWhiteSpace($BannerlordModulesRoot)) {
    $BannerlordModulesRoot = Join-Path $BannerlordGameRoot "Modules"
}
if ([string]::IsNullOrWhiteSpace($UIExtenderExPath)) {
    $UIExtenderExPath = Join-Path $BannerlordModulesRoot "Bannerlord.UIExtenderEx\bin\Win64_Shipping_Client\Bannerlord.UIExtenderEx.dll"
}
if ([string]::IsNullOrWhiteSpace($MCMv5Path)) {
    $MCMv5Path = Join-Path $BannerlordModulesRoot "Bannerlord.MBOptionScreen\bin\Win64_Shipping_Client\MCMv5.dll"
}

$allMods = @(
    "SettlementAutomationCore",
    "SmithingOptimizer",
    "FiefManager",
    "TradeOptimizer",
    "PartyManager",
    "EquipmentManager"
)

# 1. Determine which mods to process
$selectedMods = @()
if ($Mods.Count -gt 0) {
    foreach ($mod in $Mods) {
        if ($allMods -contains $mod) {
            $selectedMods += $mod
        } else {
            Write-Error "Invalid mod name: '$mod'. Valid mods are: $($allMods -join ', ')"
            exit 1
        }
    }
    Write-Host "Explicitly selected mods for build and packaging: $($selectedMods -join ', ')" -ForegroundColor Cyan
} else {
    # Auto-detect using git status
    try {
        $gitStatus = git status --porcelain
        if ($LASTEXITCODE -eq 0 -and $gitStatus) {
            $changedDirs = New-Object System.Collections.Generic.HashSet[string]
            foreach ($line in $gitStatus) {
                # Format: " M path/to/file" or "?? path/to/file"
                if ($line -match '^\s*\S+\s+(.+)$') {
                    $path = $Matches[1]
                    $parts = $path -split '/'
                    if ($parts.Count -gt 0 -and $allMods -contains $parts[0]) {
                        $changedDirs.Add($parts[0]) | Out-Null
                    }
                }
            }
            if ($changedDirs.Count -gt 0) {
                $selectedMods = @($changedDirs)
                Write-Host "Auto-detected modified mods from git status: $($selectedMods -join ', ')" -ForegroundColor Cyan
            }
        }
    } catch {
        # Git failed or not available
    }

    if ($selectedMods.Count -eq 0) {
        $selectedMods = $allMods
        Write-Host "No modifications detected or git status empty. Processing all mods." -ForegroundColor Cyan
    }
}

# 2. Build selected mods
Write-Host "Building selected mods..." -ForegroundColor Cyan
foreach ($mod in $selectedMods) {
    $projectPath = Join-Path (Join-Path $repoRoot $mod) "$mod.csproj"
    if (-not (Test-Path $projectPath)) {
        Write-Error "Could not find project file for $mod at $projectPath"
        exit 1
    }

    Write-Host "Building $mod..." -ForegroundColor Green
    & dotnet build $projectPath -c Release -m:1 `
        -p:BannerlordGameRoot="$BannerlordGameRoot" `
        -p:BannerlordModulesRoot="$BannerlordModulesRoot" `
        -p:UIExtenderExPath="$UIExtenderExPath" `
        -p:MCMv5Path="$MCMv5Path"
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed for $mod."
        exit $LASTEXITCODE
    }
}

# 3. Package selected zips
Write-Host "Packaging release zips..." -ForegroundColor Cyan
if (-not (Test-Path $publishDir)) {
    New-Item -ItemType Directory -Path $publishDir | Out-Null
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

foreach ($mod in $selectedMods) {
    Write-Host "Packaging $mod..." -ForegroundColor Green
    
    # Setup clean temp publishing directory for this mod
    if (Test-Path $tempPublishDir) {
        Remove-Item -Recurse -Force $tempPublishDir | Out-Null
    }
    New-Item -ItemType Directory -Path (Join-Path $tempPublishDir $mod) | Out-Null
    
    $sourcePath = Join-Path $BannerlordModulesRoot $mod
    if (-not (Test-Path $sourcePath)) {
        Write-Error "Could not find deployed mod files at $sourcePath"
        exit 1
    }
    
    # Copy deployed files to temp directory
    Copy-Item -Path "$sourcePath\*" -Destination (Join-Path $tempPublishDir $mod) -Recurse -Force | Out-Null
    
    # Remove any stray backup/pdb files if they exist in the target
    Get-ChildItem -Path (Join-Path $tempPublishDir $mod) -Filter "*.pdb" -Recurse | Remove-Item -Force | Out-Null

    $zipPath = Join-Path $publishDir "$mod.zip"
    if (Test-Path $zipPath) {
        Remove-Item -Force $zipPath | Out-Null
    }
    
    # Create the zip file using .NET ZipFile to ensure exact directory layout
    [System.IO.Compression.ZipFile]::CreateFromDirectory($tempPublishDir, $zipPath)
}

# Cleanup temp publishing directory
if (Test-Path $tempPublishDir) {
    Remove-Item -Recurse -Force $tempPublishDir | Out-Null
}

Write-Host "Build, deployment, and packaging completed successfully for: $($selectedMods -join ', ')" -ForegroundColor Green
