using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

// Each patch sits on one of the six methods
// that emotion mods commonly patch. The hook number is noted on each class.

namespace Sociopath
{
    // Seam 1 + 6: memory intake (and grief, which arrives through it).
    // - Inner circle: grief for someone they merely liked is refused unless that person was in the inner circle.
    // - Grudge ledger: grudges last longer, gratitude fades faster.
    [HarmonyPatch(typeof(MemoryThoughtHandler), nameof(MemoryThoughtHandler.TryGainMemory),
        new[] { typeof(Thought_Memory), typeof(Pawn) })]
    public static class Patch_TryGainMemory
    {
        public static bool Prefix(MemoryThoughtHandler __instance, Thought_Memory newThought, Pawn otherPawn)
        {
            Pawn pawn = __instance.pawn;
            if (newThought?.def == null || !SP.IsCold(pawn))
            {
                return true;
            }
            ThoughtDef def = newThought.def;

            // Both degrees: grief only for the inner circle.
            if (SP.OpinionGriefThoughts.Contains(def))
            {
                Pawn about = otherPawn ?? newThought.otherPawn;
                if (!SP.InInnerCircle(pawn, about))
                {
                    return false;
                }
            }

            // Full Sociopath only: the grudge ledger.
            if (SP.IsSociopath(pawn) && def.durationDays > 0f && newThought.durationTicksOverride < 0)
            {
                if (SP.GrudgeThoughts.Contains(def))
                {
                    newThought.durationTicksOverride = (int)(def.DurationTicks * Tuning.GrudgeDurationFactor);
                }
                else if (SP.GratitudeThoughts.Contains(def))
                {
                    newThought.durationTicksOverride = (int)(def.DurationTicks * Tuning.GratitudeDurationFactor);
                }
            }
            return true;
        }
    }

    // Seam 2: how strongly a thought is felt.
    // Shallow affect: memories (events) move mood less. Situational thoughts (needs, rooms) are untouched.
    [HarmonyPatch(typeof(ThoughtHandler), nameof(ThoughtHandler.MoodOffsetOfGroup))]
    public static class Patch_MoodOffsetOfGroup
    {
        public static void Postfix(ThoughtHandler __instance, Thought group, ref float __result)
        {
            if (__result == 0f || !(group is Thought_Memory) || SP.OwnThoughts.Contains(group.def))
            {
                return;
            }
            if (SP.IsSociopath(__instance.pawn))
            {
                __result *= Tuning.ShallowAffectFactor;
            }
        }
    }

    // Seam 4: which break is chosen.
    // Cause-linked breaks: with a grudge target around, targeted breaks become far more likely.
    // Moping breaks never happen (the trait's disallowedMentalStates handles sad wander, hide-in-room and daze;
    // catatonia has no mental state, so it is zeroed here).
    [HarmonyPatch(typeof(MentalBreakWorker), nameof(MentalBreakWorker.CommonalityFor))]
    public static class Patch_CommonalityFor
    {
        public static void Postfix(MentalBreakWorker __instance, Pawn pawn, ref float __result)
        {
            if (__result <= 0f || !SP.IsSociopath(pawn))
            {
                return;
            }
            string name = __instance.def.defName;
            if (name == "Catatonic")
            {
                __result = 0f;
                return;
            }
            bool targeted = name == "TargetedInsultingSpree" || name == "MurderousRage";
            bool untargetedInsult = name == "InsultingSpree";
            if (!targeted && !untargetedInsult)
            {
                return;
            }
            if (SP.FindGrudgeTarget(pawn, requireReachable: false) == null)
            {
                return;
            }
            __result *= targeted ? Tuning.TargetedBreakFactorWithGrudge : Tuning.UntargetedInsultFactorWithGrudge;
        }
    }

    // Seam 4, continued: aim the targeted insulting spree at the grudge target instead of a random colonist.
    [HarmonyPatch(typeof(MentalState_TargetedInsultingSpree), nameof(MentalState_TargetedInsultingSpree.PreStart))]
    public static class Patch_TargetedInsultingSpree_PreStart
    {
        private static readonly System.Collections.Generic.List<Pawn> candidates = new System.Collections.Generic.List<Pawn>();

        public static void Postfix(MentalState_TargetedInsultingSpree __instance)
        {
            Pawn pawn = __instance.pawn;
            if (!SP.IsSociopath(pawn))
            {
                return;
            }
            Pawn grudge = SP.FindGrudgeTarget(pawn, requireReachable: true);
            if (grudge == null)
            {
                return;
            }
            InsultingSpreeMentalStateUtility.GetInsultCandidatesFor(pawn, candidates, allowPrisoners: false);
            if (candidates.Contains(grudge))
            {
                __instance.target = grudge;
            }
            candidates.Clear();
        }
    }

    // Seam 4, continued: aim a murderous rage at the grudge target.
    [HarmonyPatch(typeof(MurderousRageMentalStateUtility), nameof(MurderousRageMentalStateUtility.FindPawnToKill))]
    public static class Patch_FindPawnToKill
    {
        public static void Postfix(Pawn pawn, ref Pawn __result)
        {
            if (__result == null || !SP.IsSociopath(pawn))
            {
                return;
            }
            Pawn grudge = SP.FindGrudgeTarget(pawn, requireReachable: true);
            if (grudge != null)
            {
                __result = grudge;
            }
        }
    }

    // Seam 5: ending a break.
    // Settling scores: if the break was aimed at someone they resented, the grudge is cleared and they feel satisfied.
    // Any break is also a thrill.
    [HarmonyPatch(typeof(MentalState), nameof(MentalState.RecoverFromState))]
    public static class Patch_RecoverFromState
    {
        public static void Prefix(MentalState __instance, out Pawn __state)
        {
            __state = null;
            Pawn pawn = __instance.pawn;
            if (pawn == null || pawn.Dead || !SP.IsSociopath(pawn))
            {
                return;
            }
            Pawn target = null;
            if (__instance is MentalState_InsultingSpree spree)
            {
                target = spree.target;
            }
            else if (__instance is MentalState_MurderousRage rage)
            {
                target = rage.target;
            }
            if (target != null && !target.Dead && SP.Opinion(pawn, target) <= Tuning.GrudgeOpinion)
            {
                __state = target;
            }
        }

        public static void Postfix(MentalState __instance, Pawn __state)
        {
            Pawn pawn = __instance.pawn;
            if (pawn == null || pawn.Dead || !SP.IsSociopath(pawn))
            {
                return;
            }
            Need_Thrill.Gain(pawn, Tuning.ThrillFromBreak);
            if (__state != null && pawn.needs?.mood != null)
            {
                SP.ClearGrudgesAbout(pawn, __state);
                pawn.needs.mood.thoughts.memories.TryGainMemory(SP_DefOf.SP_SettledScore, __state);
            }
        }
    }

    // Seam 6: death thoughts. A Sociopath who kills someone they resented feels it was deserved.
    [HarmonyPatch(typeof(PawnDiedOrDownedThoughtsUtility), nameof(PawnDiedOrDownedThoughtsUtility.TryGiveThoughts),
        new[] { typeof(Pawn), typeof(DamageInfo?), typeof(PawnDiedOrDownedThoughtsKind) })]
    public static class Patch_TryGiveThoughts
    {
        public static void Postfix(Pawn victim, DamageInfo? dinfo, PawnDiedOrDownedThoughtsKind thoughtsKind)
        {
            if (thoughtsKind != PawnDiedOrDownedThoughtsKind.Died || victim?.RaceProps == null || !victim.RaceProps.Humanlike)
            {
                return;
            }
            if (!(dinfo?.Instigator is Pawn killer) || killer == victim || !SP.IsSociopath(killer))
            {
                return;
            }
            Need_Thrill.Gain(killer, Tuning.ThrillFromKill);
            if (killer.needs?.mood != null && SP.Opinion(killer, victim) <= Tuning.GrudgeOpinion)
            {
                killer.needs.mood.thoughts.memories.TryGainMemory(SP_DefOf.SP_SettledScoreForGood, victim);
            }
        }
    }

    // The mask: charming when content, cutting when stressed.
    [HarmonyPatch(typeof(NegativeInteractionUtility), nameof(NegativeInteractionUtility.NegativeInteractionChanceFactor))]
    public static class Patch_NegativeInteractionChanceFactor
    {
        public static void Postfix(Pawn initiator, ref float __result)
        {
            if (__result > 0f && SP.IsCold(initiator))
            {
                __result *= SP.IsStressed(initiator)
                    ? Tuning.NegativeInteractionFactorStressed
                    : Tuning.NegativeInteractionFactorContent;
            }
        }
    }

    // After an insult or slight:
    // - Seen through (both degrees): if the recipient was charmed by this person, they realise it was an act.
    // - Thrill (Sociopath only).
    [HarmonyPatch(typeof(Pawn_InteractionsTracker), nameof(Pawn_InteractionsTracker.TryInteractWith))]
    public static class Patch_TryInteractWith
    {
        public static void Postfix(bool __result, Pawn ___pawn, Pawn recipient, InteractionDef intDef)
        {
            if (!__result || intDef == null || !SP.IsCold(___pawn))
            {
                return;
            }
            if (intDef != InteractionDefOf.Insult && intDef.defName != "Slight")
            {
                return;
            }
            if (SP.IsSociopath(___pawn))
            {
                Need_Thrill.Gain(___pawn, Tuning.ThrillFromInsult);
            }
            if (Tuning.SeenThrough && recipient?.needs?.mood != null &&
                SP.HasMemoryAbout(recipient, SP_DefOf.SP_Charmed, ___pawn))
            {
                MemoryThoughtHandler memories = recipient.needs.mood.thoughts.memories;
                memories.RemoveMemoriesOfDefWhereOtherPawnIs(SP_DefOf.SP_Charmed, ___pawn);
                memories.TryGainMemory(SP_DefOf.SP_SawThrough, ___pawn);
                if (PawnUtility.ShouldSendNotificationAbout(recipient))
                {
                    Messages.Message($"{recipient.LabelShort} has seen through {___pawn.LabelShort}'s charm.",
                        recipient, MessageTypeDefOf.NeutralEvent);
                }
            }
        }
    }

    // Cold under fire (hook 3, break risk): a Sociopath doesn't break mid-fight.
    // The break is cancelled, and they get a mood penalty for the next day instead.
    [HarmonyPatch(typeof(MentalBreaker), nameof(MentalBreaker.TryDoRandomMoodCausedMentalBreak))]
    public static class Patch_TryDoRandomMoodCausedMentalBreak
    {
        public static bool Prefix(Pawn ___pawn, ref bool __result)
        {
            if (!Tuning.ColdUnderFire || !SP.IsSociopath(___pawn) || !SP.InCombat(___pawn))
            {
                return true;
            }
            ___pawn.needs?.mood?.thoughts?.memories?.TryGainMemory(SP_DefOf.SP_HeldItTogether);
            __result = false;
            return false;
        }
    }

    // Thrill source: social fights.
    [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState))]
    public static class Patch_TryStartMentalState
    {
        public static void Postfix(MentalStateHandler __instance, bool __result, MentalStateDef stateDef)
        {
            if (__result && stateDef == MentalStateDefOf.SocialFighting)
            {
                Pawn pawn = __instance.CurState?.pawn;
                if (SP.IsSociopath(pawn))
                {
                    Need_Thrill.Gain(pawn, Tuning.ThrillFromSocialFight);
                }
            }
        }
    }

    // After any pawn takes damage:
    // - Thrill (Sociopath attacker).
    // - Protective streak: a Sociopath whose inner-circle member was hurt turns on the attacker.
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PostApplyDamage))]
    public static class Patch_PostApplyDamage
    {
        public static void Postfix(Pawn __instance, DamageInfo dinfo, float totalDamageDealt)
        {
            if (totalDamageDealt <= 0f || !(dinfo.Instigator is Pawn attacker) || attacker == __instance)
            {
                return;
            }
            bool victimIsPerson = __instance.RaceProps != null && __instance.RaceProps.Humanlike;
            if (SP.IsSociopath(attacker))
            {
                Need_Thrill.Gain(attacker, victimIsPerson ? Tuning.ThrillPerHitOnPerson : Tuning.ThrillPerHitOnAnimal);
            }
            if (Tuning.ProtectiveStreak && victimIsPerson && attacker.RaceProps != null && attacker.RaceProps.Humanlike)
            {
                ProtectiveStreak(__instance, attacker);
            }
        }

        private static void ProtectiveStreak(Pawn victim, Pawn attacker)
        {
            Map map = victim.MapHeld;
            if (map == null)
            {
                return;
            }
            IReadOnlyList<Pawn> colonists = map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn sociopath = colonists[i];
                if (sociopath == victim || sociopath == attacker || !SP.IsSociopath(sociopath) ||
                    sociopath.needs?.mood == null || !SP.InInnerCircle(sociopath, victim))
                {
                    continue;
                }
                bool first = !SP.HasMemoryAbout(sociopath, SP_DefOf.SP_HurtOneOfMine, attacker);
                sociopath.needs.mood.thoughts.memories.TryGainMemory(SP_DefOf.SP_HurtOneOfMine, attacker);
                bool couldBeTarget = attacker.Faction == sociopath.Faction ||
                                     (attacker.IsPrisoner && attacker.HostFaction == sociopath.Faction);
                if (first && couldBeTarget && PawnUtility.ShouldSendNotificationAbout(sociopath))
                {
                    Messages.Message($"{sociopath.LabelShort} won't forget that {attacker.LabelShort} hurt {victim.LabelShort}.",
                        sociopath, MessageTypeDefOf.NegativeEvent);
                }
            }
        }
    }
}
