using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace TacticalCombatAI
{
    public sealed class TacticalSettings : ModSettings
    {
        public bool Medics = true;
        public bool Evacuation = true;
        public bool RetreatRescue = true;
        public bool RetreatDiagnostics = false;
        public bool CoveringFire = true;
        public bool TacticalAdvance = true;
        public bool Coordination = true;
        public int ReassessmentTicks = 180;

        public override void ExposeData()
        {
            Scribe_Values.Look(ref Medics, "medics", true);
            Scribe_Values.Look(ref Evacuation, "evacuation", true);
            Scribe_Values.Look(ref RetreatRescue, "retreatRescue", true);
            Scribe_Values.Look(ref RetreatDiagnostics, "retreatDiagnostics", false);
            Scribe_Values.Look(ref CoveringFire, "coveringFire", true);
            Scribe_Values.Look(ref TacticalAdvance, "tacticalAdvance", true);
            Scribe_Values.Look(ref Coordination, "coordination", true);
            Scribe_Values.Look(ref ReassessmentTicks, "reassessmentTicks", 180);
            ReassessmentTicks = Mathf.Clamp(ReassessmentTicks, 90, 600);
            base.ExposeData();
        }
    }

    public sealed class TacticalMod : Mod
    {
        public static TacticalSettings Settings;

        public TacticalMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<TacticalSettings>();
            new Harmony("community.tacticalcombatai").PatchAll(typeof(TacticalMod).Assembly);
            Log.Message("[TacticalCombatAI] Harmony combat patch installed.");
        }

        public override string SettingsCategory() => "Tactical Combat AI";

        public override void DoSettingsWindowContents(Rect area)
        {
            Listing_Standard list = new Listing_Standard();
            list.Begin(area);
            list.CheckboxLabeled("Field tending (no medicine)", ref Settings.Medics);
            list.CheckboxLabeled("Evacuate downed allies to safer ground", ref Settings.Evacuation);
            list.CheckboxLabeled("Rescue allies during retreat (50% of panic fleeing becomes Retreating)", ref Settings.RetreatRescue);
            list.CheckboxLabeled("Log retreat-rescue diagnostics (for testing)", ref Settings.RetreatDiagnostics);
            list.CheckboxLabeled("Ranged covering fire", ref Settings.CoveringFire);
            list.CheckboxLabeled("Cover-to-cover advances", ref Settings.TacticalAdvance);
            list.CheckboxLabeled("Shared fire-team targets", ref Settings.Coordination);
            list.Label("Decision interval: " + Settings.ReassessmentTicks + " ticks");
            Settings.ReassessmentTicks = (int)list.Slider(Settings.ReassessmentTicks, 90, 600);
            list.End();
            base.DoSettingsWindowContents(area);
        }
    }

    // Patch only the combat thinker: do not interrupt sleeping, civilian work, flee jobs or player orders.
    // The original job is preserved whenever a tactical action cannot be proved feasible.
    [HarmonyPatch(typeof(JobGiver_AIFightEnemy), "TryGiveJob")]
    internal static class CombatDecisionPatch
    {
        private sealed class State
        {
            internal int LastDecision = -10000;
            internal int LastMove = -10000;
            internal int LastAid = -10000;
            internal int LastEvac = -10000;
        }

        // Weak keys prevent static entries retaining pawns after they leave the map.
        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Pawn, State> PawnStates =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Pawn, State>();
        private static readonly HashSet<string> Warned = new HashSet<string>();

        [HarmonyPostfix]
        private static void Postfix(Pawn pawn, ref Job __result)
        {
            if (!Eligible(pawn)) return;
            State state = PawnStates.GetValue(pawn, p => new State());
            int now = Find.TickManager.TicksGame;
            if (now - state.LastDecision < TacticalMod.Settings.ReassessmentTicks) return;
            state.LastDecision = now;

            try
            {
                // Combat Extended owns its own reload, suppression and attack jobs.
                // Do not replace an unfamiliar job returned by its combat AI.
                if (ModsConfig.IsActive("ceteam.combatextended") && __result != null &&
                    __result.def != JobDefOf.Goto &&
                    __result.def != JobDefOf.Wait_Combat &&
                    __result.def != JobDefOf.AttackStatic &&
                    __result.def != JobDefOf.AttackMelee)
                    return;

                Map map = pawn.Map;
                List<Pawn> allies = new List<Pawn>();
                List<Pawn> enemies = new List<Pawn>();
                foreach (Pawn other in map.mapPawns.AllPawnsSpawned)
                {
                    if (other == pawn || other.Dead || other.Faction == null) continue;
                    if (other.Faction == pawn.Faction)
                    {
                        if (other.RaceProps.Humanlike && pawn.Position.InHorDistOf(other.Position, 23f))
                            allies.Add(other);
                    }
                    else if (!other.Downed && pawn.Faction.HostileTo(other.Faction) &&
                             pawn.Position.InHorDistOf(other.Position, 34f))
                    {
                        enemies.Add(other);
                    }
                }
                // Medics may still tend downed allies after nearby enemies have left.
                Pawn nearest = enemies.OrderBy(e => e.Position.DistanceToSquared(pawn.Position)).FirstOrDefault();
                bool manipulates = pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation);

                // First aid is possible only if the patient is lying down and the medic is not under close threat.
                // Ground tending uses a custom driver; vanilla TendPatient refuses humanlikes outside beds.
                if (TacticalMod.Settings.Medics && manipulates &&
                    pawn.skills != null && !pawn.WorkTagIsDisabled(WorkTags.Caring) &&
                    pawn.skills.GetSkill(SkillDefOf.Medicine).Level >= 4 && now - state.LastAid > 750 &&
                    (nearest == null || pawn.Position.DistanceToSquared(nearest.Position) >= 144))
                {
                    Pawn patient = allies.Where(a => (a.Downed || a.InBed()) &&
                                          a.Position.InHorDistOf(pawn.Position, 9f) &&
                                          enemies.All(e => a.Position.DistanceToSquared(e.Position) >= 100) &&
                                          a.health != null && a.health.HasHediffsNeedingTend(false) &&
                                          a.health.hediffSet.hediffs.Any(h => h.Bleeding && h.TendableNow(false)) &&
                                          pawn.CanReserveAndReach(a, PathEndMode.Touch, Danger.Some))
                                         .OrderBy(a => a.Position.DistanceToSquared(pawn.Position))
                                         .FirstOrDefault();
                    if (patient != null)
                    {
                        Job tend = JobMaker.MakeJob(TacticalJobDefOf.TCA_FieldTend, patient);
                        tend.endAfterTendedOnce = true;
                        state.LastAid = now;
                        __result = tend;
                        return;
                    }
                }

                // Evacuation and combat maneuvers need a nearby hostile to evaluate.
                if (nearest == null) return;

                // Unlike vanilla Rescue, custom evacuation has a reachable safe-ground destination and needs no bed.
                // Only pick up a threatened, living, unreserved downed ally.
                if (TacticalMod.Settings.Evacuation && manipulates && now - state.LastEvac > 900 &&
                    pawn.carryTracker != null && pawn.carryTracker.CarriedThing == null &&
                    pawn.Position.DistanceToSquared(nearest.Position) > 36)
                {
                    foreach (Pawn patient in allies.Where(a => a.Downed && a.Spawned &&
                             a.Position.InHorDistOf(pawn.Position, 12f))
                             .OrderBy(a => a.Position.DistanceToSquared(pawn.Position)))
                    {
                        if (!pawn.CanReserveAndReach(patient, PathEndMode.Touch, Danger.Some)) continue;
                        Pawn threat = enemies.OrderBy(e => e.Position.DistanceToSquared(patient.Position)).First();
                        float fromThreat = patient.Position.DistanceTo(threat.Position);
                        if (fromThreat > 15f) continue; // already out of immediate danger
                        IntVec3 escape = FindEvacDestination(pawn, patient, enemies);
                        if (!escape.IsValid) continue;
                        Job job = JobMaker.MakeJob(TacticalJobDefOf.TCA_EvacuateAlly, patient, escape);
                        job.count = 1;
                        job.expiryInterval = 1800;
                        state.LastEvac = now;
                        __result = job;
                        return;
                    }
                }

                // Keep melee fighters on vanilla melee AI; do not strand them in ranged overwatch.
                Verb verb = pawn.TryGetAttackVerb(nearest, !pawn.IsColonist);
                if (verb == null || verb.verbProps.IsMeleeAttack) return;

                List<Pawn> squad = allies.Where(a => !a.Downed &&
                       a.Position.InHorDistOf(pawn.Position, 17f))
                       .ToList();
                squad.Add(pawn);
                squad.Sort((a, b) => a.thingIDNumber.CompareTo(b.thingIDNumber));
                int slot = squad.IndexOf(pawn) % 3;

                // Shared target is only accepted if the individual pawn can actually shoot it.
                if (TacticalMod.Settings.Coordination && squad.Count >= 2)
                {
                    Pawn shared = enemies.OrderBy(e => squad.Sum(a => e.Position.DistanceToSquared(a.Position)))
                                         .FirstOrDefault(e => verb.CanHitTarget(e));
                    if (shared != null)
                    {
                        pawn.mindState.enemyTarget = shared;
                        nearest = shared;
                    }
                }

                bool canShoot = verb.CanHitTarget(nearest);
                bool hasCover = pawn.Position.GetCover(map) != null;
                // Wait_Combat uses RimWorld's native auto-attack behavior (AttackStatic doesn't).
                if (TacticalMod.Settings.CoveringFire && squad.Count > 1 && slot == 0 &&
                    canShoot && hasCover)
                {
                    __result = new Job(JobDefOf.Wait_Combat, 240, true);
                    return;
                }

                // Move only when behind-the-lines shooters can improve position, never on each think tick.
                if (TacticalMod.Settings.TacticalAdvance && slot != 0 && now - state.LastMove >= 660 &&
                    pawn.Position.DistanceToSquared(nearest.Position) > 100)
                {
                    IntVec3 dest = FindAdvanceDestination(pawn, nearest, verb);
                    if (dest.IsValid && dest != pawn.Position)
                    {
                        Job job = JobMaker.MakeJob(JobDefOf.Goto, dest);
                        job.expiryInterval = 400;
                        job.checkOverrideOnExpire = true;
                        job.expireRequiresEnemiesNearby = true;
                        state.LastMove = now;
                        __result = job;
                    }
                }
            }
            catch (Exception ex)
            {
                // No forced job replacement on failure; vanilla __result stays intact.
                string key = ex.GetType().FullName + ":" + ex.Message;
                if (Warned.Add(key)) Log.Warning("[TacticalCombatAI] Falling back to vanilla AI: " + ex);
            }
        }

        internal static bool Eligible(Pawn p)
        {
            return TacticalMod.Settings != null && p != null && p.Spawned && !p.Dead && !p.Downed &&
                   p.RaceProps.Humanlike && p.Faction != null && p.Faction != Faction.OfPlayer &&
                   !p.Drafted && !p.IsPrisoner && !p.InMentalState && p.mindState != null && p.Map != null;
        }

        private static IntVec3 FindAdvanceDestination(Pawn soldier, Pawn target, Verb verb)
        {
            Map map = soldier.Map;
            float oldRange = soldier.Position.DistanceTo(target.Position);
            float bestScore = float.MinValue;
            IntVec3 result = IntVec3.Invalid;
            foreach (IntVec3 c in GenRadial.RadialCellsAround(soldier.Position, 8f, true))
            {
                if (!c.InBounds(map) || !c.Standable(map) || c == soldier.Position) continue;
                float range = c.DistanceTo(target.Position);
                if (range < 8f || range >= oldRange - 2f || range > verb.verbProps.range - 1f) continue;
                if (c.GetCover(map) == null || !soldier.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                if (!GenSight.LineOfSight(c, target.Position, map, true)) continue;
                float score = (oldRange - range) * 1.4f - c.DistanceTo(soldier.Position) * 0.7f;
                if (score > bestScore) { bestScore = score; result = c; }
            }
            return result;
        }

        private static IntVec3 FindEvacDestination(Pawn rescuer, Pawn patient, List<Pawn> enemies)
        {
            Map map = rescuer.Map;
            float initial = enemies.Min(e => e.Position.DistanceTo(patient.Position));
            IntVec3 result = IntVec3.Invalid;
            float bestScore = float.MinValue;
            foreach (IntVec3 c in GenRadial.RadialCellsAround(patient.Position, 16f, true))
            {
                if (!c.InBounds(map) || !c.Standable(map) || !rescuer.CanReach(c, PathEndMode.OnCell, Danger.Some)) continue;
                float nearestEnemy = enemies.Min(e => e.Position.DistanceTo(c));
                if (nearestEnemy < initial + 6f || nearestEnemy < 15f) continue;
                float score = nearestEnemy - patient.Position.DistanceTo(c) * 0.38f +
                              (c.GetCover(map) != null ? 3f : 0f);
                if (score > bestScore) { bestScore = score; result = c; }
            }
            return result;
        }
    }

    // Panic fleeing is a vanilla mental state; raid stealing and kidnapping are
    // duties. Convert half of hostile humanlike panic starts into a dedicated
    // mental state with a rescue-first think tree, without changing the raid lord.
    [DefOf]
    internal static class TacticalMentalStateDefOf
    {
        public static MentalStateDef TCA_Retreating;
        static TacticalMentalStateDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(TacticalMentalStateDefOf));
        }
    }

    [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState))]
    internal static class RetreatingMentalStatePatch
    {
        [HarmonyPrefix]
        private static void Prefix(Pawn ___pawn, ref MentalStateDef stateDef)
        {
            if (stateDef != MentalStateDefOf.PanicFlee || ___pawn == null ||
                TacticalMod.Settings == null || !TacticalMod.Settings.RetreatRescue) return;

            // A repeated request to panic should not reroll an existing retreat.
            if (___pawn.MentalStateDef == TacticalMentalStateDefOf.TCA_Retreating)
            {
                stateDef = TacticalMentalStateDefOf.TCA_Retreating;
                return;
            }
            // Do not reroll an existing vanilla panic on repeated state requests.
            if (___pawn.MentalStateDef == MentalStateDefOf.PanicFlee) return;

            if (!___pawn.Spawned || ___pawn.Dead || ___pawn.Downed ||
                !___pawn.RaceProps.Humanlike || ___pawn.IsPrisoner ||
                ___pawn.Faction == null || ___pawn.Faction == Faction.OfPlayer ||
                !___pawn.Faction.HostileTo(Faction.OfPlayer)) return;

            if (Rand.Chance(0.5f))
                stateDef = TacticalMentalStateDefOf.TCA_Retreating;
        }

        [HarmonyPostfix]
        private static void Postfix(Pawn ___pawn, MentalStateDef stateDef, bool __result)
        {
            if (__result && stateDef == TacticalMentalStateDefOf.TCA_Retreating &&
                TacticalMod.Settings != null && TacticalMod.Settings.RetreatDiagnostics)
                Log.Message("[TacticalCombatAI] " + ___pawn.LabelShortCap +
                            " entered Retreating; will seek a wounded faction ally before exiting.");
        }
    }

    // The Retreating mental-state think tree invokes this before vanilla
    // JobGiver_ExitMapPanic. It never steals or kidnaps enemies.
    public sealed class JobGiver_RetreatWithAlly : ThinkNode_JobGiver
    {
        protected override Job TryGiveJob(Pawn pawn)
        {
            if (pawn == null || pawn.MentalStateDef != TacticalMentalStateDefOf.TCA_Retreating ||
                TacticalMod.Settings == null || !TacticalMod.Settings.RetreatRescue ||
                !pawn.Spawned || pawn.Dead || pawn.Downed || pawn.Faction == null ||
                pawn.carryTracker == null || pawn.carryTracker.CarriedThing != null ||
                !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation))
                return null;

            // If we cannot reach the exit with the casualty, fall through to
            // vanilla panic escape instead of leaving a pawn stranded.
            IntVec3 exitCell;
            if (!RCellFinder.TryFindBestExitSpot(pawn, out exitCell) ||
                !exitCell.IsValid || !pawn.CanReach(exitCell, PathEndMode.OnCell, Danger.Some))
                return null;

            Pawn patient = pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction)
                .Where(p => p != pawn && p.Spawned && !p.Dead && p.Downed &&
                            p.RaceProps.Humanlike && !p.IsPrisoner &&
                            p.Position.InHorDistOf(pawn.Position, 28f) &&
                            pawn.CanReserveAndReach(p, PathEndMode.OnCell, Danger.Some))
                .OrderBy(p => p.Position.DistanceToSquared(pawn.Position))
                .FirstOrDefault();
            if (patient == null) return null;

            Job rescue = JobMaker.MakeJob(JobDefOf.Kidnap, patient, exitCell);
            rescue.count = 1;
            if (TacticalMod.Settings.RetreatDiagnostics)
                Log.Message("[TacticalCombatAI] Retreating rescue: " + pawn.LabelShortCap +
                            " carrying " + patient.LabelShortCap + " off-map.");
            return rescue;
        }
    }

    // Vanilla JobGiver_TakeWoundedGuest already assigns the Kidnap job for a
    // non-hostile casualty: JobDriver_Kidnap inherits TakeAndExitMap and
    // provides pickup, hauling and map exit without our own exit-map driver.
    //
    // Panic-fleeing raiders may use a different JobGiver_ExitMap subclass
    // than the one used by kidnap/steal duties. Patch the DECLARING base method
    // and recognize ANY subclass, but only replace an already-selected vanilla
    // exit-map Goto; never replace a fight, dig, or special extraction job.
    [HarmonyPatch(typeof(JobGiver_ExitMap), "TryGiveJob")]
    internal static class RetreatCasualtyRescuePatch
    {
        private sealed class RetreatState
        {
            internal int LastRescue = -10000;
            internal int LastDiagnostic = -10000;
        }

        private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<Pawn, RetreatState> States =
            new System.Runtime.CompilerServices.ConditionalWeakTable<Pawn, RetreatState>();
        private static bool warned;

        [HarmonyPostfix]
        private static void Postfix(JobGiver_ExitMap __instance, Pawn pawn, ref Job __result)
        {
            if (TacticalMod.Settings == null || !TacticalMod.Settings.RetreatRescue) return;
            if (!CombatDecisionPatch.Eligible(pawn)) return;
            // Preserve existing behavior for kidnap/steal duties when they are
            // already exiting empty-handed. Active Kidnap/Steal jobs are protected
            // by the strict Goto + exitMapOnArrival check below.
            string dutyName = pawn.mindState.duty?.def?.defName;

            RetreatState state = States.GetValue(pawn, p => new RetreatState());
            int tick = Find.TickManager.TicksGame;
            // Opt-in, throttled diagnostics help identify the panic-flee job giver
            // if a mod changes how exit jobs are issued.
            if (TacticalMod.Settings.RetreatDiagnostics && tick - state.LastDiagnostic >= 600)
            {
                state.LastDiagnostic = tick;
                Log.Message("[TacticalCombatAI] Exit job: pawn=" + pawn.LabelShortCap +
                            ", giver=" + __instance.GetType().Name +
                            ", duty=" + (dutyName ?? "none") +
                            ", job=" + (__result?.def?.defName ?? "none") +
                            ", exits=" + (__result != null && __result.exitMapOnArrival) +
                            ", downedAllies28=" + pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction)
                                .Count(p => p != pawn && p.Downed && p.Position.InHorDistOf(pawn.Position, 28f)));
            }

            // An exit-map Goto proves the AI has decided to leave.
            // Do not replace an existing Kidnap, dig, fight or CE-specific job.
            if (__result == null || __result.def != JobDefOf.Goto || !__result.exitMapOnArrival) return;
            if (pawn.carryTracker == null || pawn.carryTracker.CarriedThing != null) return;
            if (!pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)) return;
            if (tick - state.LastRescue < 600) return;
            try
            {
                IntVec3 exitCell = __result.targetA.Cell;
                if (!exitCell.IsValid || !pawn.CanReach(exitCell, PathEndMode.OnCell, Danger.Some)) return;
                // Restrict to same-faction, downed, living humanlikes; ordinary
                // kidnapping of opponents remains completely vanilla.
                Pawn patient = pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction)
                    .Where(p => p != pawn && p.Spawned && !p.Dead && p.Downed &&
                                p.RaceProps.Humanlike && !p.IsPrisoner &&
                                p.Position.InHorDistOf(pawn.Position, 28f) &&
                                pawn.CanReserveAndReach(p, PathEndMode.OnCell, Danger.Some))
                    .OrderBy(p => p.Position.DistanceToSquared(pawn.Position))
                    .FirstOrDefault();
                if (patient == null) return;

                // Exactly the same vanilla job + target layout used by
                // JobGiver_TakeWoundedGuest; vanilla handles exit and carry.
                Job rescue = JobMaker.MakeJob(JobDefOf.Kidnap, patient, exitCell);
                rescue.count = 1;
                state.LastRescue = tick;
                if (TacticalMod.Settings.RetreatDiagnostics)
                    Log.Message("[TacticalCombatAI] Retreat rescue: " + pawn.LabelShortCap +
                                " carrying " + patient.LabelShortCap + " via " + __instance.GetType().Name);
                __result = rescue;
            }
            catch (Exception ex)
            {
                if (!warned)
                {
                    warned = true;
                    Log.Warning("[TacticalCombatAI] Retreat casualty rescue skipped; vanilla exit retained: " + ex);
                }
            }
        }
    }

    [DefOf]
    internal static class TacticalJobDefOf
    {
        public static JobDef TCA_EvacuateAlly;
        public static JobDef TCA_FieldTend;
        static TacticalJobDefOf() { DefOfHelper.EnsureInitializedInCtor(typeof(TacticalJobDefOf)); }
    }

    // The vanilla TendPatient driver explicitly refuses a humanlike not in bed.
    // This field version deliberately permits tending a downed pawn on the ground;
    // healing and XP are still processed through RimWorld's own Toils_Tend.
    public sealed class JobDriver_FieldTend : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedNullOrForbidden(TargetIndex.A);
            this.FailOn(() =>
            {
                Pawn patient = job.GetTarget(TargetIndex.A).Thing as Pawn;
                return patient == null || patient.Dead || !(patient.Downed || patient.InBed()) ||
                       !patient.health.HasHediffsNeedingTend(false);
            });
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.Touch);
            yield return Toils_General.Wait(420, TargetIndex.None)
                .FailOnCannotTouch(TargetIndex.A, PathEndMode.Touch)
                .WithProgressBarToilDelay(TargetIndex.A, false, -0.5f);
            Pawn wounded = (Pawn)job.GetTarget(TargetIndex.A).Thing;
            yield return Toils_Tend.FinalizeTend(wounded);
        }
    }

    // Carry a downed allied pawn from the firing line to a traversable, previously screened tile.
    // Reuses vanilla hauling toils, including reservations and pickup/drop handling.
    public sealed class JobDriver_EvacuateAlly : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed) &&
                   pawn.Reserve(job.GetTarget(TargetIndex.B), job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDestroyedNullOrForbidden(TargetIndex.A);
            this.FailOn(() => job.GetTarget(TargetIndex.A).Thing is Pawn p && (!p.Downed || p.Dead));
            // If combat or pathfinding interrupts our short-range rescue,
            // never leave the patient attached to a pawn that has resumed fighting.
            AddFinishAction(condition =>
            {
                if (!pawn.Spawned || pawn.carryTracker == null ||
                    pawn.carryTracker.CarriedThing != job.GetTarget(TargetIndex.A).Thing) return;
                Thing dropped;
                pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out dropped);
            });
            // These vanilla kidnapping toils handle reservation, pathfinding
            // and the actual pickup of a living downed pawn.
            yield return Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch)
                .FailOnSomeonePhysicallyInteracting(TargetIndex.A);
            yield return Toils_Haul.StartCarryThing(TargetIndex.A, false, false, false);
            // Do not use TakeAndExitMap for active fighting; it would cause
            // a rescuer to leave the raid. Carry only to our safe-ground tile.
            yield return Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell);
            Toil drop = new Toil();
            drop.initAction = () =>
            {
                if (pawn.carryTracker.CarriedThing != job.GetTarget(TargetIndex.A).Thing) return;
                Thing placed;
                IntVec3 dest = job.GetTarget(TargetIndex.B).Cell;
                if (!pawn.carryTracker.TryDropCarriedThing(dest, ThingPlaceMode.Near, out placed) &&
                    !pawn.carryTracker.TryDropCarriedThing(pawn.Position, ThingPlaceMode.Near, out placed))
                    EndJobWith(JobCondition.Incompletable);
            };
            drop.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return drop;
        }
    }
}
