# Testing

Use the repository test wrapper instead of calling `dotnet test` directly.

The test projects target .NET Framework and reference Bannerlord, MCM, and UIExtenderEx assemblies from the local Steam install. Raw `dotnet test` can spawn many MSBuild/testhost workers and stall for a long time in this setup. The wrapper applies the flags that keep the run predictable:

- Release configuration.
- One MSBuild node per project.
- MSBuild node reuse disabled.
- shared compilation disabled.
- module-copy targets skipped for tests.
- temporary files kept under `.tmp/dotnet-temp`.
- build servers shut down after the run.

## Common Commands

Run every test project:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\test.ps1
```

Run one test project:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\test.ps1 -TestProject EquipmentManager
powershell -ExecutionPolicy Bypass -File scripts\test.ps1 -TestProject PartyManager
powershell -ExecutionPolicy Bypass -File scripts\test.ps1 -TestProject SettlementAutomationCore
```

Run tests matching an xUnit filter:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\test.ps1 -Filter Equipment
powershell -ExecutionPolicy Bypass -File scripts\test.ps1 -Filter Recruitment
```

Run with restore when packages or project references changed:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\test.ps1 -Restore
```

## Avoid

Avoid raw project test commands such as:

```powershell
dotnet test tests\EquipmentManager.Tests\EquipmentManager.Tests.csproj --no-restore
```

That path has repeatedly taken about 15 minutes before timing out or returning incomplete diagnostics. If a direct `dotnet` command is needed for troubleshooting, use the same flags as the wrapper:

```powershell
dotnet test tests\EquipmentManager.Tests\EquipmentManager.Tests.csproj -c Release --no-restore -m:1 -nr:false -p:SkipBannerlordModuleCopy=true -p:UseSharedCompilation=false
```

## Local Dependency Paths

Test projects inherit dependency paths from `tests/Directory.Build.props`:

- `BannerlordGameBin`
- `BannerlordWorkshopRoot`
- `UIExtenderExPath`
- `MCMv5Path`

Override those properties on the command line if the local Steam or workshop install is somewhere else.
