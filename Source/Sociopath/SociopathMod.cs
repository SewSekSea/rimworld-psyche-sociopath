using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace Sociopath
{
    [DefOf]
    public static class SP_DefOf
    {
        public static TraitDef SP_Sociopath;
        public static NeedDef SP_Thrill;
        public static ThoughtDef SP_ThrillLevel;
        public static ThoughtDef SP_SettledScore;
        public static ThoughtDef SP_SettledScoreForGood;
        public static ThoughtDef SP_Charmed;
        public static ThoughtDef SP_CharmedMood;
        public static ThoughtDef SP_SawThrough;
        public static ThoughtDef SP_HurtOneOfMine;
        public static ThoughtDef SP_HeldItTogether;
        public static InteractionDef SP_Charm;

        static SP_DefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(SP_DefOf));
        }
    }

    /// <summary>
    /// All the tuning numbers in one place. The ones players can change come from the settings menu.
    /// </summary>
    public static class Tuning
    {
        private static SociopathSettings S => SociopathMod.Settings;

        // Grudge ledger: how long memories last for a Sociopath, relative to vanilla.
        public static float GrudgeDurationFactor => S.grudgeDurationFactor;
        public static float GratitudeDurationFactor => S.gratitudeDurationFactor;

        // Shallow affect: multiplier on mood from memories (not needs, rooms or other situations).
        public static float ShallowAffectFactor => S.shallowAffectFactor;

        // Inner circle: opinion at or above this counts, as do blood relatives and partners.
        public static int InnerCircleOpinion => S.innerCircleOpinion;

        // Grudge target: opinion at or below this.
        public static int GrudgeOpinion => S.grudgeOpinion;

        // Cause-linked breaks.
        public static float TargetedBreakFactorWithGrudge => S.targetedBreakFactor;
        public const float UntargetedInsultFactorWithGrudge = 0.5f;

        // The mask: multiplier on the chance of insults and slights.
        public static float NegativeInteractionFactorContent => S.insultFactorContent;
        public static float NegativeInteractionFactorStressed => S.insultFactorStressed;
        public static float CharmWeight => S.charmWeight;

        // Thrill need.
        public static float ThrillFallPerDay => S.thrillFallPerDay;
        public static float ThrillFromWorkPerDay => S.thrillFromWorkPerDay;
        public const float ThrillFromInsult = 0.08f;
        public const float ThrillFromCharm = 0.04f;
        public const float ThrillFromSocialFight = 0.15f;
        public const float ThrillPerHitOnPerson = 0.03f;
        public const float ThrillPerHitOnAnimal = 0.015f;
        public const float ThrillFromKill = 0.3f;
        public const float ThrillFromBreak = 0.3f;

        // Optional behaviours.
        public static bool SeenThrough => S.seenThrough;
        public static bool ProtectiveStreak => S.protectiveStreak;
        public static bool UnsettlingPresence => S.unsettlingPresence;
        public static float UnsettlingRadius => S.unsettlingRadius;
        public static bool ColdUnderFire => S.coldUnderFire;

        // Cold under fire: how recently a pawn must have fought or been hurt to count as "in combat".
        public const int CombatWindowTicks = 2500;
    }

    [StaticConstructorOnStartup]
    public static class SP
    {
        /// <summary>Negative social memories the Sociopath holds onto (the grudge ledger).</summary>
        public static readonly HashSet<ThoughtDef> GrudgeThoughts = new HashSet<ThoughtDef>();

        /// <summary>Positive social memories the Sociopath forgets quickly.</summary>
        public static readonly HashSet<ThoughtDef> GratitudeThoughts = new HashSet<ThoughtDef>();

        /// <summary>Grief for someone they liked. Kept only for the inner circle.</summary>
        public static readonly HashSet<ThoughtDef> OpinionGriefThoughts = new HashSet<ThoughtDef>();

        /// <summary>This mod's own thoughts, which are never scaled by shallow affect.</summary>
        public static readonly HashSet<ThoughtDef> OwnThoughts = new HashSet<ThoughtDef>();

        public const int DegreeCallous = 1;
        public const int DegreeSociopath = 2;

        static SP()
        {
            Fill(GrudgeThoughts, "Insulted", "Slighted", "HarmedMe", "HadAngeringFight", "BotchedMySurgery",
                "RebuffedMyRomanceAttempt", "BrokeUpWithMe", "CheatedOnMe", "DivorcedMe", "RejectedMyProposal",
                "ForcedMeToTakeDrugs", "ForcedMeToTakeLuciferium", "SoldMyLovedOne", "SoldMyBondedAnimal",
                // From the Squad mod, if installed (silently skipped otherwise).
                "SQ_FrozeUnderFire");
            Fill(GratitudeThoughts, "KindWords", "RescuedMe", "RescuedMeByOfferingHelp", "RecruitedMe",
                "RapportBuilt", "HadCatharticFight",
                // From the Squad mod, if installed (silently skipped otherwise).
                "SQ_FoughtBeside");
            Fill(OpinionGriefThoughts, "PawnWithGoodOpinionDied", "PawnWithGoodOpinionLost");
            OwnThoughts.Add(SP_DefOf.SP_ThrillLevel);
            OwnThoughts.Add(SP_DefOf.SP_SettledScore);
            OwnThoughts.Add(SP_DefOf.SP_SettledScoreForGood);
            OwnThoughts.Add(SP_DefOf.SP_HeldItTogether);

            new Harmony("sociopath.trait").PatchAll();
            Log.Message($"[Sociopath] Loaded. Tracking {GrudgeThoughts.Count} grudge thoughts, {GratitudeThoughts.Count} gratitude thoughts.");
        }

        private static void Fill(HashSet<ThoughtDef> set, params string[] defNames)
        {
            foreach (string name in defNames)
            {
                ThoughtDef def = DefDatabase<ThoughtDef>.GetNamedSilentFail(name);
                if (def != null)
                {
                    set.Add(def);
                }
            }
        }

        /// <summary>0 = neither, 1 = Callous, 2 = Sociopath.</summary>
        public static int Degree(Pawn pawn)
        {
            Trait trait = pawn?.story?.traits?.GetTrait(SP_DefOf.SP_Sociopath);
            return trait == null ? 0 : trait.Degree;
        }

        /// <summary>The full trait: grudges, shallow affect, thrill, targeted breaks.</summary>
        public static bool IsSociopath(Pawn pawn)
        {
            return Degree(pawn) >= DegreeSociopath;
        }

        /// <summary>Either degree: selective empathy and the mask.</summary>
        public static bool IsCold(Pawn pawn)
        {
            return Degree(pawn) >= DegreeCallous;
        }

        public static bool IsStressed(Pawn pawn)
        {
            if (pawn?.needs?.mood == null || pawn.mindState?.mentalBreaker == null)
            {
                return false;
            }
            return pawn.needs.mood.CurLevel < pawn.mindState.mentalBreaker.BreakThresholdMinor;
        }

        public static bool InCombat(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }
            if (pawn.Drafted)
            {
                return true;
            }
            Pawn_MindState mind = pawn.mindState;
            if (mind == null)
            {
                return false;
            }
            int now = Find.TickManager.TicksGame;
            return now - mind.lastEngageTargetTick < Tuning.CombatWindowTicks ||
                   now - mind.lastAttackTargetTick < Tuning.CombatWindowTicks ||
                   now - mind.lastHarmTick < Tuning.CombatWindowTicks;
        }

        public static int Opinion(Pawn of, Pawn about)
        {
            if (of?.relations == null || about == null || of == about)
            {
                return 0;
            }
            return of.relations.OpinionOf(about);
        }

        /// <summary>Family by blood, partners, and anyone rated highly enough.</summary>
        public static bool InInnerCircle(Pawn sociopath, Pawn other)
        {
            if (sociopath == null || other == null)
            {
                return false;
            }
            foreach (PawnRelationDef rel in sociopath.GetRelations(other))
            {
                if (rel.familyByBloodRelation || rel == PawnRelationDefOf.Spouse ||
                    rel == PawnRelationDefOf.Lover || rel == PawnRelationDefOf.Fiance)
                {
                    return true;
                }
            }
            return Opinion(sociopath, other) >= Tuning.InnerCircleOpinion;
        }

        /// <summary>
        /// The person on this map the Sociopath most resents, if anyone is at or below the grudge line.
        /// Candidates match what a murderous rage could target: same faction or its prisoners, humanlike, reachable.
        /// </summary>
        public static Pawn FindGrudgeTarget(Pawn sociopath, bool requireReachable)
        {
            if (sociopath == null || !sociopath.Spawned || sociopath.relations == null)
            {
                return null;
            }
            Pawn best = null;
            int bestOpinion = Tuning.GrudgeOpinion + 1;
            IReadOnlyList<Pawn> all = sociopath.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < all.Count; i++)
            {
                Pawn other = all[i];
                if (other == sociopath || other.Dead || !other.RaceProps.Humanlike)
                {
                    continue;
                }
                bool sameSide = other.Faction == sociopath.Faction ||
                                (other.IsPrisoner && other.HostFaction == sociopath.Faction);
                if (!sameSide)
                {
                    continue;
                }
                int opinion = sociopath.relations.OpinionOf(other);
                if (opinion >= bestOpinion)
                {
                    continue;
                }
                if (requireReachable && !sociopath.CanReach(other, PathEndMode.Touch, Danger.Deadly))
                {
                    continue;
                }
                if (other.CurJob != null && other.CurJob.exitMapOnArrival)
                {
                    continue;
                }
                best = other;
                bestOpinion = opinion;
            }
            return best;
        }

        /// <summary>Remove every grudge memory the Sociopath holds about this person.</summary>
        public static void ClearGrudgesAbout(Pawn sociopath, Pawn target)
        {
            MemoryThoughtHandler memories = sociopath?.needs?.mood?.thoughts?.memories;
            if (memories == null || target == null)
            {
                return;
            }
            foreach (ThoughtDef def in GrudgeThoughts)
            {
                memories.RemoveMemoriesOfDefWhereOtherPawnIs(def, target);
            }
            memories.RemoveMemoriesOfDefWhereOtherPawnIs(SP_DefOf.SP_HurtOneOfMine, target);
        }

        public static bool HasMemoryAbout(Pawn pawn, ThoughtDef def, Pawn about)
        {
            List<Thought_Memory> memories = pawn?.needs?.mood?.thoughts?.memories?.Memories;
            if (memories == null)
            {
                return false;
            }
            for (int i = 0; i < memories.Count; i++)
            {
                if (memories[i].def == def && memories[i].otherPawn == about)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
