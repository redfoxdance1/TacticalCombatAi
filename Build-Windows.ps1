param(
    [Parameter(Mandatory=$true)][string]$RimWorldDir,
    [Parameter(Mandatory=$true)][string]$HarmonyDll
)
$ErrorActionPreference = 'Stop'
if (!(Test-Path (Join-Path $RimWorldDir 'RimWorldWin64_Data\Managed\Assembly-CSharp.dll'))) { throw "RimWorldDir is not a RimWorld Windows install with Assembly-CSharp.dll: $RimWorldDir" }
if (!(Test-Path $HarmonyDll)) { throw "0Harmony.dll not found: $HarmonyDll" }
if (!(Get-Command dotnet -ErrorAction SilentlyContinue)) { throw 'dotnet SDK not available. Install Visual Studio Build Tools or .NET SDK with net472 reference assemblies.' }
& dotnet build (Join-Path $PSScriptRoot 'Source\TacticalCombatAI.csproj') -c Release "-p:RimWorldDir=$RimWorldDir" "-p:HarmonyDll=$HarmonyDll"
if ($LASTEXITCODE -ne 0) { throw "Build failed (exit code $LASTEXITCODE)" }
$target = Join-Path $PSScriptRoot '1.6\Assemblies\TacticalCombatAI.dll'
if (!(Test-Path $target)) { throw "Build reported success, but expected DLL missing: $target" }
Write-Host "Build succeeded: $target"
