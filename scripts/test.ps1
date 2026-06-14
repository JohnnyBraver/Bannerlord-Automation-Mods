param(
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path -Parent $PSScriptRoot

dotnet test (Join-Path $repoRoot "BannerlordMods.sln") -c $Configuration -p:SkipBannerlordModuleCopy=true

