# Tactical Combat AI — RimWorld 1.6

**Status: experimental test build. Compiles successfully against RimWorld 1.6 reference assemblies in GitHub Actions; not yet validated in-game.**

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

## Get a compiled test build (no game DLL uploads)

This repository has a [GitHub Actions build workflow](.github/workflows/build-rimworld.yml).
The workflow restores publicly available RimWorld 1.6 reference assemblies and Harmony
reference assemblies, compiles `TacticalCombatAI.dll`, runs structural tests, and
produces `TacticalCombatAI-RimWorld-1.6-test.zip` as a workflow artifact **only if the build succeeds**.

1. Open the **Actions** tab and select **Build RimWorld 1.6 test mod**.
2. Choose **Run workflow** (or use the automatic push/PR build).
3. Open the successful run, download the **TacticalCombatAI-RimWorld-1.6-test** artifact.
4. Unzip the downloaded GitHub artifact to obtain `TacticalCombatAI-RimWorld-1.6-test.zip`.
5. Extract that inner ZIP into RimWorld's `Mods` directory. It contains a single
   `TacticalCombatAI` folder with `About`, `LoadFolders.xml`, and
   `1.6/Assemblies/TacticalCombatAI.dll` plus the custom JobDefs.
6. Enable the **Harmony** mod before **Tactical Combat AI**, restart RimWorld 1.6,
   and test on a disposable save.

**Important:** Passing compilation does not prove that Harmony patches resolve,
jobs run correctly, or friendly retreat extraction behaves safely. Check RimWorld's
in-game log for red errors. Test without Combat Extended or other AI mods first.

## Build locally

Install a .NET SDK (8 or newer) and run:

```powershell
dotnet restore Source/TacticalCombatAI.csproj
dotnet build Source/TacticalCombatAI.csproj -c Release --no-restore
```

The output DLL is `1.6/Assemblies/TacticalCombatAI.dll`. No RimWorld installation
or private game assemblies are needed **for compilation** because
`Krafs.Rimworld.Ref` provides RimWorld API signatures and `Lib.Harmony.Ref`
provides Harmony signatures. The actual game and Harmony mod are still needed
**to run the mod**.

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
