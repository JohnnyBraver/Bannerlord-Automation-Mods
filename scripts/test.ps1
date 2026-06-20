param(
    [string]$Configuration = "Release",
    [string]$Filter = "",
    [Alias("Project")]
    [string[]]$TestProject = @(),
    [switch]$Restore
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot
$tmpRoot = Join-Path $repoRoot ".tmp"
$dotnetTemp = Join-Path $tmpRoot "dotnet-temp"

New-Item -ItemType Directory -Force -Path $dotnetTemp | Out-Null

$env:DOTNET_SKIP_FIRST_TIME_EXPERIENCE = "1"
$env:DOTNET_CLI_TELEMETRY_OPTOUT = "1"
$env:MSBUILDDISABLENODEREUSE = "1"
$env:TEMP = $dotnetTemp
$env:TMP = $dotnetTemp

$testProjects = @(Get-ChildItem -Path (Join-Path $repoRoot "tests") -Filter "*.csproj" -Recurse |
    Sort-Object FullName)

if ($TestProject.Count -gt 0) {
    $projectPatterns = $TestProject
    $testProjects = @($testProjects | Where-Object {
        $projectFile = $_
        $projectPatterns | Where-Object {
            $projectFile.BaseName -like "*$_*" -or $projectFile.FullName -like "*$_*"
        }
    })
}
elseif (-not [string]::IsNullOrWhiteSpace($Filter)) {
    $filterProjectMap = @{
        "Automation" = "SettlementAutomationCore"
        "Core" = "SettlementAutomationCore"
        "Equipment" = "EquipmentManager"
        "Fief" = "FiefManager"
        "Party" = "PartyManager"
        "Policy" = "SettlementAutomationCore"
        "Recruit" = "PartyManager"
        "Request" = "SettlementAutomationCore"
        "Trade" = "TradeOptimizer"
    }

    $matchedProjectNames = foreach ($entry in $filterProjectMap.GetEnumerator()) {
        if ($Filter -match [regex]::Escape($entry.Key)) {
            $entry.Value
        }
    }

    $matchedProjectNames = @($matchedProjectNames | Select-Object -Unique)
    if ($matchedProjectNames.Count -gt 0) {
        $testProjects = @($testProjects | Where-Object {
            $projectFile = $_
            $matchedProjectNames | Where-Object {
                $projectFile.BaseName -like "$($_).Tests"
            }
        })
    }
}

$testExitCode = 0
try {
    if ($testProjects.Count -eq 0) {
        Write-Error "No test projects matched the requested project/filter selection."
    }

    foreach ($project in $testProjects) {
        $testArgs = @(
            "test",
            $project.FullName,
            "-c", $Configuration,
            "-m:1",
            "-nr:false",
            "-p:SkipBannerlordModuleCopy=true",
            "-p:UseSharedCompilation=false"
        )

        if (-not $Restore) {
            $testArgs += "--no-restore"
        }

        if (-not [string]::IsNullOrWhiteSpace($Filter)) {
            $testArgs += @("--filter", $Filter)
        }

        dotnet @testArgs
        if ($LASTEXITCODE -ne 0) {
            $testExitCode = $LASTEXITCODE
            break
        }
    }
}
finally {
    dotnet build-server shutdown | Out-Null
}

if ($testExitCode -ne 0) {
    exit $testExitCode
}

