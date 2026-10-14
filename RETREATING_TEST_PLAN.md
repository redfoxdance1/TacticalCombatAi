# Retreating mental state — in-game validation (RimWorld 1.6)

**Status:** compile-time checks are not proof of gameplay behavior. Test on a disposable save with Harmony and Tactical Combat AI before adding Combat Extended.

## Expected logic

When a **hostile, non-player humanlike pawn** is first assigned the vanilla `PanicFlee` mental state, there is a **50% independent chance** that it receives `TCA_Retreating` instead. The chance is not a guarantee for small groups. A repeated panic request while the pawn is already panicking/retreating does not reroll.

The Retreating mental-state think tree tries `JobGiver_RetreatWithAlly` before vanilla `JobGiver_ExitMapPanic`. It looks for a living, downed, same-faction humanlike within **28 cells**, requires a free reservation, manipulation, and a reachable exit, then starts vanilla `Kidnap` on that ally. If rescue is unavailable, it leaves normally. Player pawns and non-hostile pawns are not converted.

## Test cases

1. **State distribution:** Cause at least 20 hostile humanlike raiders to panic (preferably across several raids). Inspect their mental state. Expect both `PanicFlee` and `Retreating` to occur, approximately half each in a large sample.
2. **Pickup and exit:** Place one downed same-faction raider within 28 cells of a newly Retreating raider. Confirm the standing raider gets `Kidnap`, picks up the ally, and both disappear on reaching the edge.
3. **No casualty:** Start Retreating with no downed allies nearby. The raider must head to an exit as vanilla panic would, not freeze or attack.
4. **Already reserved casualty:** Have two Retreating raiders near one downed ally. Only one should reserve and carry that casualty; the other should leave or select another casualty.
5. **Inaccessible casualty/exit:** Wall off a casualty or make the route to an exit unreachable. Confirm fallback behavior and no endless job restart or red error.
6. **Vanilla panic untouched:** A pawn that remains `PanicFlee` should not pick up allies because of the new Retreating job giver.
7. **Save/load:** Save during a Retreating episode, reload, and verify the mental state persists and job selection resumes.
8. **Combat Extended:** Repeat pickup, exit and fallback with CE enabled; watch for suppression and job override conflicts.

Enable **Log retreat-rescue diagnostics** in mod settings to see transitions and rescue choices. If any red error occurs, provide the first complete stack trace from Player.log along with the pawn's mental state, current job and nearby downed allies.

**Important:** This implementation uses vanilla `JobDefOf.Kidnap` on same-faction casualties. The exact off-map world-state effects need in-game verification.
