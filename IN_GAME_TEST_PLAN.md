# RimWorld 1.6 in-game smoke test (experimental)

Use a **disposable save**. This mod compiles in CI, but successful compilation does
not verify Harmony patch resolution, job execution, faction handling, or compatibility.

## Install

1. Download the `TacticalCombatAI-RimWorld-1.6-test` artifact from a successful
   **Build RimWorld 1.6 test mod** workflow run in GitHub Actions.
2. Extract the artifact ZIP, then extract its inner
   `TacticalCombatAI-RimWorld-1.6-test.zip` into RimWorld's `Mods` folder.
3. Verify the folder looks like:
   ```text
   Mods/TacticalCombatAI/
     About/About.xml
     LoadFolders.xml
     1.6/Assemblies/TacticalCombatAI.dll
     1.6/Defs/JobDefs/TCA_JobDefs.xml
   ```
4. Enable Harmony first and Tactical Combat AI second. Restart the game.
5. Start with no other AI/combat mods, then test compatibility separately.

## First-launch gate

- [ ] RimWorld recognizes Tactical Combat AI in the Mods menu.
- [ ] No red errors appear during startup or when loading a map.
- [ ] The mod settings panel opens and its toggles work.
- [ ] A basic humanlike raid starts and runs normally with every feature off.

If startup fails, stop testing and capture the first red error.

## Functional checks (turn on one feature at a time)

- [ ] **Medics:** a capable NPC with Medicine 4+ and a bleeding, downed
      same-faction ally attempts the `stabilizing` job when conditions permit.
      Check both while enemies are nearby and after they disappear. Note that
      the combat job-giver may not be evaluated after combat ends; the
      post-combat case is **not guaranteed** just by the source-level fix.
- [ ] **Evacuation:** with nearby enemies, a rescuer carries a downed ally
      to a reachable safer cell and drops them.
- [ ] **Evacuation interruption:** interrupt the rescuer and confirm the
      casualty is not permanently stuck being carried.
- [ ] **Retreat rescue:** when raiders retreat, check whether they pick up
      same-faction downed allies, leave the map, and do **not** produce
      unexpected prisoner/kidnapping world-state effects.
- [ ] **Covering fire:** a ranged pawn in cover waits and still attacks
      enemies through vanilla combat behavior.
- [ ] **Advance:** some ranged pawns move toward reachable cover, without
      abandoning melee or creating an endless movement loop.
- [ ] **Coordination:** multiple same-faction ranged pawns can share
      a target without shooting through walls or attacking friendlies.
- [ ] **Stress test:** a large raid causes no major stutters, repeated
      errors, or rapidly repeating jobs.

## Report bugs

Include:
- RimWorld version (from the main menu), Harmony version, and mod list.
- Which feature toggle was enabled and how to reproduce the problem.
- The **first** red error with its stack trace.
- A short description of what the pawn actually did vs. what was expected.

On Windows, RimWorld's log is usually at:
`%USERPROFILE%\\AppData\\LocalLow\\Ludeon Studios\\RimWorld by Ludeon Studios\\Player.log`.

Do not treat a successful compile as evidence of correct in-game behavior.
