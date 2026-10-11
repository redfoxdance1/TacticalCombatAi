# Audit report — Tactical Combat AI / RimWorld 1.6

## Evidence and limits

Date: 2026-10-11. Reviewed original `TacticalCombatAI_Source.zip`, rewrote decision flow, added two job definitions and two job drivers, and ran XML and structural checks. Publicly available RimWorld decompilation was consulted for the `JobGiver_AIFightEnemy.TryGiveJob`, `JobDriver_TendPatient`, `HealthAIUtility`, `Hediff.Bleeding`, `Toils_Tend.FinalizeTend`, and hauling interfaces. **Online decompilation is not guaranteed to match the installed 1.6 executable.** This environment has no `dotnet`, `mono`, `mcs`, `Assembly-CSharp.dll`, Unity game assemblies, Harmony binary, or game runtime, so neither compilation nor game validation was achievable here. No status of "working" is implied.

## High severity findings and changes

| Original condition | Impact | Revision |
| --- | --- | --- |
| `HediffDefOf.BloodLoss` check instead of checking real bleeding injuries | NPC medics often never act until extreme blood loss; injury untreated | Use `Hediff.Bleeding`, `TendableNow`, `HasHediffsNeedingTend` on actual patients |
| Vanilla `TendPatient` applied directly to humanlikes outside beds | Vanilla `JobDriver_TendPatient` checks `WorkGiver_Tend.GoodLayingStatusForTend`, refusing downed humanlikes on the ground | Add dedicated `TCA_FieldTend` driver which allows ground care and uses `Toils_Tend.FinalizeTend` |
| Using `HealthAIUtility.ShouldBeTendedNowByPlayer` on raiders | The vanilla method checks `playerSettings`, often null for NPC raiders; the care predicate would never match | Switch to NPC-compatible `health.HasHediffsNeedingTend(false)` |
| Vanilla `Rescue` requires an appropriate bed, with no open-ground fallback | Most raids lack a valid bed and wounded evacuation never occurs | Add `TCA_EvacuateAlly` job using reserved pickup and nearby safe-ground relocation |
| `AttackStatic` used for covering fire | May produce invalid/stationary attack jobs for unshootable targets; ignores much of native shooter AI | Use vanilla `Wait_Combat` with actual `Verb.CanHitTarget` and existing cover |
| No explicit Harmony dependency in `About.xml` | Wrong loading order or runtime missing assembly | Add `brrainz.harmony` dependency plus `loadAfter` |
| Incomplete `JobDefs` folder | No custom hauling/medical job binding available | Add XML definitions for both new job drivers |
| Potential stale per-pawn entry in static maps | Entries persist after pawn despawns, leading to memory growth | Replace persistent pawn-ID dictionaries with weak-keyed per-pawn state |
| No explicit `[HarmonyPostfix]` on patch method | Convention naming alone is less obvious and harder to audit | Add explicit annotation |
| Reassessment could repeatedly override vanilla behavior | Constant repositioning or interrupts | Separate general reassessment, medical, evacuation and advance cooldowns |
| Melee units given shooting jobs | Melee raiders stood still or attempted incompatible tactics | Check attack verb and leave melee pawn jobs unchanged |
| No test suite | No quick proof of package structure or critical invariants | Add `Tests/test_static.py` |

## What the current code actually implements

- Applies a postfix to `JobGiver_AIFightEnemy.TryGiveJob`. If prerequisites fail, leaves native result intact. This is not a universal ThinkTree replacement.
- Targets **non-player, spawned, living, mentally stable humanlike pawns**, regardless of whether their faction is attacking the player or defending as an ally. Does not enable friendly-fire or modify player orders.
- Builds local same-faction fireteams and target preferences, not cross-faction coalition squads and not actual command hierarchy.
- Picks cover positions using simple standability/reachability/line-of-sight criteria. `GetCover()` does not itself guarantee cover is on the enemy-facing side.
- Moves downed allies only if a safer candidate cell exists; a patient can still be threatened by foes outside the 34-cell local scan radius.
- Battlefield medical job performs a no-medicine tend using the built-in tend finalizer. No drug/medicine procurement or sophisticated triage.
- "Covering fire" is continuous ordinary shooting while teammates choose movement; it does not directly apply a suppression debuff or tactical pinning.

## Outstanding risks requiring in-game tests

1. **API build:** All C# member names and overloads MUST be checked by compiling with the exact 1.6 game binaries (at minimum `Assembly-CSharp.dll`, Unity assemblies and Harmony).
2. **Harmony attach:** Verify the postfix is actually installed on the intended combat thinker; other modded combat job givers may bypass it.
3. **New JobDef parse and driver activation:** `TCA_EvacuateAlly` and `TCA_FieldTend` must both be loaded before jobs are issued.
4. **Medics:** Verify ground tending on downed patient without a bed actually reaches `Toils_Tend.FinalizeTend`, reduces bleeding, and completes. Confirm with multiple hediffs.
5. **Evacuation:** Verify reserved target is carried and released when destination is blocked, when pawn is wounded mid-carry, and when the raiders retreat.
6. **Ranged combat:** Verify the covering member actually fires while movers use new cells; check for shooters with too-short ranges, shields, turret targets, melee-only pawns and fire interruption.
7. **Allies and factions:** Check that player colonists stay untouched, friendly visitors use their own faction correctly, and pawns from rival factions do not heal each other.
8. **Performance:** Run a 100+ pawn raid and measure spike when candidates are evaluated on every `ReassessmentTicks` interval. No benchmarks have been run.
9. **Compatibility:** Test vanilla first, then add Combat Extended, Combat AI, and custom races one at a time; integrations are not included.
10. **Save safety:** Use a disposable test save. No 1.6 gameplay test has been performed.

## Validation actually executed

- XML parser: `About/About.xml`, `LoadFolders.xml`, both JobDefs in `TCA_JobDefs.xml`, and `Source/TacticalCombatAI.csproj`: PASS.
- Source-invariant checks for Harmony postfix, cover combat job, medical job, evacuation, weak state, drop fallback, obsolete calls removed, and brace counts: PASS.
- Actual compile against 1.6: **NOT RUN (no compiler/game references)**.
- Actual RimWorld 1.6 runtime test: **NOT RUN (no game environment)**.

Keep this audit report with the source. If compiling produces errors, start by using the exact RimWorld 1.6 binaries and `HarmonyDll` matching the installed Harmony mod; then examine the first compiler error, not the downstream cascade.

## 2026-10-11 update: reuse vanilla Kidnap for retreat casualty extraction

This revision adds a postfix to the **declaring** `JobGiver_ExitMap.TryGiveJob` method (not the derived `ExitMapBest` which inherits it). It only acts when the underlying node is `JobGiver_ExitMapBest` and the vanilla job is `Goto` with `exitMapOnArrival=true`. It selects the nearest *downed, living, same-faction, non-prisoner humanlike pawn* within 18 cells, provided it can be reserved and reached and the vanilla exit point is reachable. It then creates vanilla `JobDefOf.Kidnap` (targetA=patient, targetB=exit). This mirrors `JobGiver_TakeWoundedGuest` instead of duplicating the map-exit/carry driver.

For mid-combat safe-tile extraction the custom `TCA_EvacuateAlly` job remains necessary because the standard `Kidnap` job **exits the map**. The custom job now uses the same vanilla `Toils_Haul.StartCarryThing` overload as kidnapping, with explicit `GotoCell` and a best-effort `AddFinishAction` drop on interruption.

**Testing limitations remain:** no game DLL, compiler or 1.6 runtime; no C# compilation or gameplay test. Validate enemy-vs-friendly target selection, normal kidnappings, retreat behavior, interrupted pickups, map exits, and safe drops in a separate save.
