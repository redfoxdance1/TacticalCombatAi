# Retreat casualty extraction — RimWorld 1.6 manual test matrix

**Must run on a disposable 1.6 game save after successfully compiling.** Structural checks do not prove runtime behavior.

| ID | Setup / action | Expected behavior |
| --- | --- | --- |
| R1 | 6 same-faction armed raiders, one downed within 10 cells; make raid retreat | Exiting rescuer changes vanilla ExitMapBest Goto to Kidnap, picks up **same-faction** casualty and leaves map |
| R2 | Downed colonist near retreating raiders, no downed raiders | No additional rescue job; ordinary **vanilla** colonist kidnapping remains possible under vanilla `Kidnap` duty |
| R3 | One same-faction downed raider, but rescuer is still fighting | No map-exit extraction job from this hook; battlefield evacuation may take patient to safe ground |
| R4 | Downed pawn already reserved by an allied medic, or behind blocked terrain | Rescuer does not assign extraction job; default vanilla exit is preserved |
| R5 | Two downed same-faction raiders and two rescuers retreat | At most one rescuer reserves/picks up each casualty; no duplicate pickup jobs |
| R6 | Same-faction downed pawn is 20+ cells away | Rescuer keeps vanilla exit; patient not selected |
| R7 | Same-faction pawn is dead, awake or prisoner | Not selected |
| R8 | Downed friendly visitor (non-player) retreats | Rescuer may extract same-faction wounded visitor; actual vanilla world-state should be inspected |
| R9 | Disable **Carry downed allies off-map during retreat** | Rescuer uses vanilla retreat logic; other tactical settings work independently |
| R10 | Interrupt a short-range evacuation after pickup | Rescuer drops their carried patient nearby if alive and spawned; check no invisible/missing pawn |
| R11 | 50+ pawns retreat simultaneously | No persistent exceptions or massive reassessment stalls |
| R12 | Humanlike modded faction and mechanoid raid | Humanoid NPC extraction only; mechs do not get jobs |
| R13 | Existing in-game Kidnap raid order and guest evacuation | Existing kidnapping and `TakeWoundedGuest` jobs are not overwritten |
| R14 | Flee/exit through breakable wall requiring dig job | Existing dig/attack job not replaced; no rescue if safe exit route invalid |

Inspect `Player.log` for `JobGiver_ExitMap`, `RetreatCasualtyRescuePatch`, `Kidnap`, `TCA_EvacuateAlly`, `NullReferenceException` and `HarmonyException`. Confirm `Kidnap` removes a friendly casualty from the map **without marking them a prisoner** for the exact game version and scenario. Use debug actions / Dev Mode to set up raids and force retreat.

## Actual verification in this package

- Static XML/structure test: executable through `python Tests/test_static.py`
- Exact RimWorld 1.6 API compile: **NOT RUN**
- Game integration (R1–R14): **NOT RUN**
