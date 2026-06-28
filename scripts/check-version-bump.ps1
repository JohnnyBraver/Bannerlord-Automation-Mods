# Scripts/check-version-bump.ps1
param(
    [string]$BaseRef = "origin/main"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

# Clean up BaseRef if it contains GitHub Actions empty SHA placeholder
if ($BaseRef -eq "0000000000000000000000000000000000000000" -or [string]::IsNullOrEmpty($BaseRef)) {
    Write-Host "BaseRef ref was empty or all zeros. Defaulting to HEAD~1" -ForegroundColor Yellow
    $BaseRef = "HEAD~1"
}

# 1. Helper function to parse version strings (handles vX.Y.Z, eX.Y.Z, X.Y.Z)
function Parse-VersionString {
    param([string]$v)
    if ($v -match '^[ve]?(\d+)\.(\d+)\.(\d+)(?:\.(\d+))?') {
        return [PSCustomObject]@{
            Major = [int]$Matches[1]
            Minor = [int]$Matches[2]
            Patch = [int]$Matches[3]
            Build = if ($Matches[4]) { [int]$Matches[4] } else { 0 }
            Raw   = $v
        }
    }
    return $null
}

# 2. Helper function to compare two version objects
function Compare-Versions {
    param($v1, $v2)
    # Returns -1 if v1 < v2, 0 if v1 == v2, 1 if v1 > v2
    if ($null -eq $v1 -and $null -eq $v2) { return 0 }
    if ($null -eq $v1) { return -1 }
    if ($null -eq $v2) { return 1 }
    
    if ($v1.Major -ne $v2.Major) { return $v1.Major.CompareTo($v2.Major) }
    if ($v1.Minor -ne $v2.Minor) { return $v1.Minor.CompareTo($v2.Minor) }
    if ($v1.Patch -ne $v2.Patch) { return $v1.Patch.CompareTo($v2.Patch) }
    if ($v1.Build -ne $v2.Build) { return $v1.Build.CompareTo($v2.Build) }
    return 0
}

Write-Host "Determining changes against base ref: $BaseRef" -ForegroundColor Cyan

# 3. Get all files changed between BaseRef and HEAD
# Using ... (three dots) shows changes in HEAD since the common ancestor of BaseRef and HEAD
try {
    $changedFiles = git diff --name-only "$BaseRef...HEAD"
} catch {
    Write-Error "Failed to run git diff. Make sure $BaseRef is fetched and exists."
    exit 1
}

if ($changedFiles.Count -eq 0) {
    Write-Host "No changed files detected in this PR/comparison." -ForegroundColor Green
    exit 0
}

# 4. Discover all mods dynamically (directories containing SubModule.xml)
$mods = Get-ChildItem -Path $repoRoot -Directory | Where-Object {
    Test-Path (Join-Path $_.FullName "SubModule.xml")
} | Select-Object -ExpandProperty Name

$errors = @()
$checkedCount = 0

foreach ($mod in $mods) {
    # Check if files in this mod directory have changed
    $modChanges = $changedFiles | Where-Object { $_ -like "$mod/*" }
    if ($modChanges.Count -eq 0) {
        continue
    }

    $checkedCount++
    Write-Host "Mod '$mod' has changes. Verifying version bump..." -ForegroundColor Cyan

    $modDir = Join-Path $repoRoot $mod
    $xmlPath = Join-Path $modDir "SubModule.xml"

    # Get current version from SubModule.xml
    $headVersionStr = $null
    $headVersion = $null
    try {
        [xml]$xml = Get-Content $xmlPath
        $headVersionStr = $xml.Module.Version.value
        $headVersion = Parse-VersionString $headVersionStr
    } catch {
        $errors += "Mod '$mod' SubModule.xml is malformed or invalid XML: $_"
        continue
    }

    if ($null -eq $headVersion) {
        $errors += "Mod '$mod' current version string '$headVersionStr' is invalid or not in SemVer format."
        continue
    }

    # Get base version from BaseRef
    $baseVersionStr = $null
    $baseVersion = $null
    $gitShowOutput = git show "${BaseRef}:${mod}/SubModule.xml" 2>$null
    if ($LASTEXITCODE -eq 0 -and $gitShowOutput) {
        try {
            $xmlStr = $gitShowOutput -join [Environment]::NewLine
            [xml]$baseXml = $xmlStr
            $baseVersionStr = $baseXml.Module.Version.value
            $baseVersion = Parse-VersionString $baseVersionStr
        } catch {
            Write-Warning "Could not parse base SubModule.xml version for $mod on $BaseRef."
        }
    } else {
        Write-Host "Mod '$mod' does not exist in $BaseRef (new mod). Version check skipped." -ForegroundColor Yellow
        continue
    }

    if ($null -eq $baseVersion) {
        Write-Warning "No valid version found for '$mod' in $BaseRef. Version bump verification skipped."
        continue
    }

    # Compare versions
    $comparison = Compare-Versions $headVersion $baseVersion
    if ($comparison -le 0) {
        $errors += "Mod '$mod' has changes but its version was not bumped. Base ($BaseRef): $baseVersionStr, PR (HEAD): $headVersionStr"
    } else {
        Write-Host "  Success: version bumped from $baseVersionStr to $headVersionStr." -ForegroundColor Green
    }
}

if ($errors.Count -gt 0) {
    Write-Host ""
    Write-Host "Validation failed:" -ForegroundColor Red
    foreach ($err in $errors) {
        Write-Host "  [ERROR] $err" -ForegroundColor Red
    }
    exit 1
}

Write-Host "All $checkedCount modified mods have been successfully bumped!" -ForegroundColor Green
exit 0
