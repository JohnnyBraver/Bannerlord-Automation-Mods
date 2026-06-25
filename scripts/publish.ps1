# Scripts/publish.ps1
$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$publishDir = Join-Path $repoRoot "publish"
$tempPublishDir = Join-Path $repoRoot ".tmp_publish"
$gameModulesRoot = "E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules"

$mods = @(
    "SettlementAutomationCore",
    "SmithingOptimizer",
    "FiefManager",
    "TradeOptimizer",
    "PartyManager",
    "EquipmentManager"
)

Write-Host "Building solution in Release configuration and deploying to the game..." -ForegroundColor Cyan
& dotnet build -c Release -m:1 -p:UIExtenderExPath="E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\Bannerlord.UIExtenderEx\bin\Win64_Shipping_Client\Bannerlord.UIExtenderEx.dll" -p:MCMv5Path="E:\SteamLibrary\steamapps\common\Mount & Blade II Bannerlord\Modules\Bannerlord.MBOptionScreen\bin\Win64_Shipping_Client\MCMv5.dll"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Build failed. Cannot publish release zips."
    exit $LASTEXITCODE
}

Write-Host "Packaging release zips..." -ForegroundColor Cyan
if (-not (Test-Path $publishDir)) {
    New-Item -ItemType Directory -Path $publishDir | Out-Null
}

Add-Type -AssemblyName System.IO.Compression.FileSystem

foreach ($mod in $mods) {
    Write-Host "Packaging $mod..." -ForegroundColor Green
    
    # Setup clean temp publishing directory for this mod
    if (Test-Path $tempPublishDir) {
        Remove-Item -Recurse -Force $tempPublishDir | Out-Null
    }
    New-Item -ItemType Directory -Path (Join-Path $tempPublishDir $mod) | Out-Null
    
    $sourcePath = Join-Path $gameModulesRoot $mod
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

Write-Host "All release zips successfully built, packaged, and deployed to the game!" -ForegroundColor Green
