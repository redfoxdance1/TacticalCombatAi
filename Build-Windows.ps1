# Compile with publicly available RimWorld and Harmony reference assemblies.
# Does not require a local RimWorld installation for compilation.
$ErrorActionPreference = 'Stop'
if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw 'The .NET SDK is required (https://dotnet.microsoft.com/download).'
}
$project = Join-Path $PSScriptRoot 'Source/TacticalCombatAI.csproj'
& dotnet restore $project
if ($LASTEXITCODE -ne 0) { throw "Restore failed (exit code $LASTEXITCODE)" }
& dotnet build $project -c Release --no-restore
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit code $LASTEXITCODE)" }
$target = Join-Path $PSScriptRoot '1.6/Assemblies/TacticalCombatAI.dll'
if (!(Test-Path $target)) { throw "Expected DLL missing: $target" }
Write-Host "Build succeeded: $target"
