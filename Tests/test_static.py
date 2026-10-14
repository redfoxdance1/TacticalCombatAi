"""Run via python Tests/test_static.py. Structural checks; does NOT compile or run the mod."""
from pathlib import Path
from xml.etree import ElementTree as ET
root = Path(__file__).resolve().parents[1]
code = (root/'Source/TacticalCombatAI.cs').read_text()
for path in [root/'About/About.xml', root/'LoadFolders.xml', root/'1.6/Defs/JobDefs/TCA_JobDefs.xml', root/'1.6/Defs/MentalStateDefs/TCA_MentalStates.xml', root/'1.6/Patches/TCA_RetreatThinkTree.xml', root/'Source/TacticalCombatAI.csproj']:
    ET.parse(path)
xml = ET.parse(root/'1.6/Defs/JobDefs/TCA_JobDefs.xml').getroot()
assert set(n.text for n in xml.findall('.//defName')) == {'TCA_EvacuateAlly', 'TCA_FieldTend'}
assert set(n.text for n in xml.findall('.//driverClass')) == {'TacticalCombatAI.JobDriver_EvacuateAlly', 'TacticalCombatAI.JobDriver_FieldTend'}
about = ET.parse(root/'About/About.xml').getroot()
assert any(x.text == 'brrainz.harmony' for x in about.findall('.//modDependencies/li/packageId'))
assert '1.6' in [node.text for node in about.findall('.//supportedVersions/li')]
assert '[HarmonyPostfix]' in code
assert 'PawnStates.GetValue' in code
assert 'GetValue(pawn, p => new State())' in code
assert 'JobDefOf.Wait_Combat' in code
assert 'TacticalJobDefOf.TCA_FieldTend' in code
assert 'TCA_EvacuateAlly' in code
assert 'JobDriver_EvacuateAlly' in code
assert 'TryDropCarriedThing' in code
assert 'JobMaker.MakeJob(JobDefOf.AttackStatic' not in code
assert 'HediffDefOf.BloodLoss' not in code
assert 'JobMaker.MakeJob(JobDefOf.Rescue' not in code
assert code.count('{') == code.count('}')
# The retreat hook must attach to the method declared in JobGiver_ExitMap,
# which JobGiver_ExitMapBest inherits, NOT an override on the derived class.
assert '[HarmonyPatch(typeof(JobGiver_ExitMap), "TryGiveJob")]' in code
# All JobGiver_ExitMap subclasses must be eligible: panic fleeing may not
# use JobGiver_ExitMapBest, while the exit-map Goto is still mandatory.
assert 'if (!(__instance is JobGiver_ExitMapBest)) return;' not in code
assert 'if (dutyName == "Kidnap"' not in code
assert 'string dutyName = pawn.mindState.duty?.def?.defName;' in code
assert 'RetreatDiagnostics = false' in code
assert 'Log retreat-rescue diagnostics (for testing)' in code
assert 'giver=" + __instance.GetType().Name' in code
# Optional Combat Extended integration must not override its custom jobs.
assert 'ModsConfig.IsActive("ceteam.combatextended")' in code
assert '__result.def != JobDefOf.Goto' in code
assert '__result.def != JobDefOf.Wait_Combat' in code
assert '__result.def != JobDefOf.AttackStatic' in code
assert '__result.def != JobDefOf.AttackMelee' in code
# Never turn arbitrary movement/combat/dig actions into a kidnapping job.
assert '__result.def != JobDefOf.Goto || !__result.exitMapOnArrival' in code
# Keep vanilla victim selection out of this patch; only same-faction downed NPCs.
assert 'pawn.Map.mapPawns.SpawnedPawnsInFaction(pawn.Faction)' in code
assert 'p.Downed' in code and '!p.Dead' in code and '!p.IsPrisoner' in code
assert 'pawn.CanReserveAndReach(p, PathEndMode.OnCell, Danger.Some)' in code
assert 'p.Position.InHorDistOf(pawn.Position, 28f)' in code
assert 'downedAllies28=' in code
# Vanilla uses the same Kidnap job + target pair in TakeWoundedGuest.
assert 'JobMaker.MakeJob(JobDefOf.Kidnap, patient, exitCell)' in code
assert 'rescue.count = 1;' in code
assert 'JobDefOf.Kidnap' not in code.split('internal static class RetreatCasualtyRescuePatch')[0]
# For active combat, use a custom near-cell drop and a finish cleanup.
assert 'AddFinishAction(condition =>' in code
assert 'Toils_Haul.StartCarryThing(TargetIndex.A, false, false, false)' in code
assert 'Toils_Goto.GotoCell(TargetIndex.B, PathEndMode.OnCell)' in code
assert 'drop.defaultCompleteMode = ToilCompleteMode.Instant;' in code
# Regression: medics must work when there are no nearby enemies.
assert 'if (enemies.Count == 0) return;' not in code
assert 'Pawn nearest = enemies.OrderBy(e => e.Position.DistanceToSquared(pawn.Position)).FirstOrDefault();' in code
assert '(nearest == null || pawn.Position.DistanceToSquared(nearest.Position) >= 144)' in code
assert 'if (nearest == null) return;' in code
# A distinct, opt-in rescue retreat must replace exactly half of hostile
# humanlike panic starts, not hijack the normal panic-flee think tree.
states = ET.parse(root/'1.6/Defs/MentalStateDefs/TCA_MentalStates.xml').getroot()
assert states.find('.//MentalStateDef/defName').text == 'TCA_Retreating'
assert states.find('.//MentalStateDef/stateClass').text == 'MentalState_PanicFlee'
patch = ET.parse(root/'1.6/Patches/TCA_RetreatThinkTree.xml').getroot()
assert patch.find('.//xpath').text == 'Defs/ThinkTreeDef[defName="MentalStateNonCritical"]/thinkRoot/subNodes'
branch = patch.find('.//li[@Class="ThinkNode_ConditionalMentalState"]')
assert branch.find('state').text == 'TCA_Retreating'
assert [x.get('Class') for x in branch.findall('./subNodes/li')] == [
    'TacticalCombatAI.JobGiver_RetreatWithAlly',
    'JobGiver_ExitMapPanic',
    'JobGiver_WanderAnywhere'
]
assert 'HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState))' in code
assert 'stateDef != MentalStateDefOf.PanicFlee' in code
assert 'Rand.Chance(0.5f)' in code
assert '___pawn.MentalStateDef == TacticalMentalStateDefOf.TCA_Retreating' in code
assert '___pawn.Faction.HostileTo(Faction.OfPlayer)' in code
assert 'class JobGiver_RetreatWithAlly : ThinkNode_JobGiver' in code
assert 'pawn.MentalStateDef != TacticalMentalStateDefOf.TCA_Retreating' in code
assert 'RCellFinder.TryFindBestExitSpot(pawn, out exitCell)' in code
assert 'JobMaker.MakeJob(JobDefOf.Kidnap, patient, exitCell)' in code
assert 'return rescue;' in code
workflow = (root/'.github/workflows/build-rimworld.yml').read_text()
assert "Copy-Item '1.6/Defs/*'" in workflow
assert "Copy-Item '1.6/Patches/*'" in workflow
project = (root/'Source/TacticalCombatAI.csproj').read_text()
assert 'Krafs.Rimworld.Ref' in project
assert 'Lib.Harmony.Ref' in project
assert 'RimWorldDir' not in project
assert 'RetreatRescue = true' in code
assert 'ref RetreatRescue, "retreatRescue", true' in code
print('PASS: XML/project parses, 50% panic-to-Retreating conversion, rescue-first think tree, vanilla panic fallback, same-faction casualty and packaging checks. NOT compiled or game-tested.')
