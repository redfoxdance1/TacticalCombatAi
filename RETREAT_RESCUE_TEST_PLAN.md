# Retreat casualty extraction — RimWorld 1.6 manual test matrix

**Must run on a disposable 1.6 game save after successfully compiling.** Structural checks do not prove runtime behavior.

| ID | Setup / action | Expected behavior |
| --- | --- | --- |
| R1 | 6 same-faction armed raiders, one downed within 10 cells; make raid retreat | Exiting rescuer changes a vanilla exit-map Goto to Kidnap, picks up **same-faction** casualty and leaves map |
| R2 | Downed colonist near retreating raiders, no downed raiders | No additional rescue job; ordinary **vanilla** colonist kidnapping remains possible under vanilla `Kidnap` duty |
| R3 | One same-faction downed raider, but rescuer is still fighting | No map-exit extraction job from this hook; battlefield evacuation may take patient to safe ground |
| R4 | Downed pawn already reserved by an allied medic, or behind blocked terrain | Rescuer does not assign extraction job; default vanilla exit is preserved |
| R5 | Two downed same-faction raiders and two rescuers retreat | At most one rescuer reserves/picks up each casualty; no duplicate pickup jobs |
| R6 | Same-faction downed pawn is 30+ cells away | Rescuer keeps vanilla exit; patient not selected |
| R7 | Same-faction pawn is dead, awake or prisoner | Not selected |
| R8 | Downed friendly visitor (non-player) retreats | Rescuer may extract same-faction wounded visitor; actual vanilla world-state should be inspected |
| R9 | Disable **Carry downed allies off-map during retreat** | Rescuer uses vanilla retreat logic; other tactical settings work independently |
| R10 | Interrupt a short-range evacuation after pickup | Rescuer drops their carried patient nearby if alive and spawned; check no invisible/missing pawn |
| R11 | 50+ pawns retreat simultaneously | No persistent exceptions or massive reassessment stalls |
| R12 | Humanlike modded faction and mechanoid raid | Humanoid NPC extraction only; mechs do not get jobs |
| R13 | Existing in-game Kidnap raid order and guest evacuation | Existing kidnapping and `TakeWoundedGuest` jobs are not overwritten |
| R14 | Flee/exit through breakable wall requiring dig job | Existing dig/attack job not replaced; no rescue if safe exit route invalid |
| R15 | Raiders lose the fight and their name labels turn blue; a downed same-faction raider is nearby | A fleeing raider using any vanilla ExitMap job giver attempts to carry the ally off-map (when reachable); no dependence on label color |
| R16 | Raider still has red name and is on a Kidnap or Steal duty | An active Kidnap/Steal job is preserved; if vanilla instead returns an exit-map Goto, same-faction casualty rescue remains possible as before |
| R17 | Enable **Log retreat-rescue diagnostics** and trigger panic retreat | Log records giver type, duty, original job, exit flag and successful rescue; disabling logging stops messages |
| R18 | Run with Combat Extended installed; raider reloads, suppresses or uses CE-specific combat job | TCA does not replace CE's custom combat jobs; test medic, evac, retreat and movement separately |


Inspect `Player.log` for `JobGiver_ExitMap`, `RetreatCasualtyRescuePatch`, `Kidnap`, `TCA_EvacuateAlly`, `NullReferenceException` and `HarmonyException`. Confirm `Kidnap` removes a friendly casualty from the map **without marking them a prisoner** for the exact game version and scenario. Use debug actions / Dev Mode to set up raids and force retreat.

## Actual verification in this package

- Static XML/structure test: executable through `python Tests/test_static.py`
- Exact RimWorld 1.6 API compile: **PASS in GitHub Actions for prior test build; recheck this revision**
- Game integration (R1–R18): **Initial player report: red-name retreat rescue occurred, blue-name panic retreat rescue did not; updated fix requires retesting.**
