# Tactical Combat AI — RimWorld 1.6

**Status: experimental source prototype. Not compiled against RimWorld 1.6 or tested in-game.**

A Harmony-based mod prototype adding tactical decisions for **non-player humanlike faction pawns** (hostile raiders and allied NPCs).

## Features

| Feature | Implementation |
| --- | --- |
| Combat medics | Tend bleeding, downed or in-bed same-faction allies without medicine |
| Battlefield evacuation | Carry nearby downed allies toward safer terrain and drop them |
| Retreat extraction | Reuse RimWorld's vanilla `Kidnap` job to carry same-faction casualties off-map when retreating |
| Covering fire | Some shooters hold cover and use normal combat-wait auto-fire (no suppression effect) |
| Tactical advances | Move ranged shooters to reachable positions with cover |
| Coordination | Nearby same-faction soldiers prefer shared targets they can hit |

All features can be enabled or disabled independently from mod settings. Only supported AI job-giver paths are patched, so not every modded or vanilla encounter uses these behaviors.

## Prerequisites

- RimWorld **1.6 for Windows**, including the game's `Assembly-CSharp.dll` and Unity assemblies
- [Harmony](https://steamcommunity.com/sharedfiles/filedetails/?id=2009463077) and its `0Harmony.dll`
- .NET SDK / Visual Studio Build Tools with .NET Framework 4.7.2 reference assemblies

## Build

Run PowerShell from the repository root, replacing the paths as needed:

```powershell
.\Build-Windows.ps1 -RimWorldDir "C:\Program Files (x86)\Steam\steamapps\common\RimWorld" -HarmonyDll "C:\Program Files (x86)\Steam\steamapps\workshop\content\294100\2009463077\Current\Assemblies\0Harmony.dll"
```

After a successful build, the DLL should be at `1.6/Assemblies/TacticalCombatAI.dll`. **This repository does not include a compiled DLL.**

Copy the repository folder into `RimWorld/Mods`, enable Harmony before Tactical Combat AI, and restart the game.

## Structural tests

```bash
python Tests/test_static.py
```

These tests validate package XML and selected source invariants only. **They do not verify compilation or game behavior.** Test on a disposable save with Harmony and the mod only before testing compatibility with other mods.

Consult [AUDIT_REPORT.md](AUDIT_REPORT.md) for risks and [RETREAT_RESCUE_TEST_PLAN.md](RETREAT_RESCUE_TEST_PLAN.md) for in-game evacuation scenarios.

## Source layout

- `Source/TacticalCombatAI.cs` — combat patches and field tend / evacuation jobs
- `Source/TacticalCombatAI.csproj` — .NET Framework build project
- `1.6/Defs/JobDefs/TCA_JobDefs.xml` — custom jobs
- `About/About.xml`, `LoadFolders.xml` — RimWorld metadata
- `Build-Windows.ps1` — build helper
- `Tests/test_static.py` — structural checks
- `AUDIT_REPORT.md`, `RETREAT_RESCUE_TEST_PLAN.md` — limitations and manual tests

The retreat job follows vanilla logic resembling `JobGiver_TakeWoundedGuest` and `JobDriver_Kidnap`. It must still be verified with the exact game build.

**Experimental: not guaranteed compatible or stable.**
