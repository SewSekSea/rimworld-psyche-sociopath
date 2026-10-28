using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace Sociopath
{
    /// <summary>Boredom meter. Drains while awake; topped up by conflict, violence and manipulation.</summary>
    public class Need_Thrill : Need
    {
        private const int IntervalTicks = 150;

        public Need_Thrill(Pawn pawn) : base(pawn)
        {
            threshPercents = new List<float> { 0.1f, 0.3f, 0.85f };
        }

        // Jobs that count as thrilling work. The player still assigns them; this only changes how they feel.
        private static HashSet<JobDef> thrillJobs;

        private static HashSet<JobDef> ThrillJobs
        {
            get
            {
                if (thrillJobs == null)
                {
                    thrillJobs = new HashSet<JobDef>();
                    foreach (string name in new[] { "Hunt", "Slaughter", "PrisonerExecution", "GuiltyColonistExecution", "AttackMelee", "AttackStatic" })
                    {
                        JobDef def = DefDatabase<JobDef>.GetNamedSilentFail(name);
                        if (def != null)
                        {
                            thrillJobs.Add(def);
                        }
                    }
                }
                return thrillJobs;
            }
        }

        /// <summary>True while the pawn is hunting, slaughtering, executing or fighting.</summary>
        public static bool DoingThrillingWork(Pawn pawn)
        {
            Job job = pawn?.CurJob;
            if (job == null)
            {
                return false;
            }
            return ThrillJobs.Contains(job.def) || job.workGiverDef?.workType == WorkTypeDefOf.Hunting;
        }

        public override int GUIChangeArrow
        {
            get
            {
                if (IsFrozen)
                {
                    return 0;
                }
                return DoingThrillingWork(pawn) && Tuning.ThrillFromWorkPerDay > 0f ? 1 : -1;
            }
        }

        public override void SetInitialLevel()
        {
            CurLevel = 0.6f;
        }

        public override void NeedInterval()
        {
            if (IsFrozen)
            {
                return;
            }
            float perDay = DoingThrillingWork(pawn) && Tuning.ThrillFromWorkPerDay > 0f
                ? Tuning.ThrillFromWorkPerDay
                : -Tuning.ThrillFallPerDay;
            CurLevel += perDay * IntervalTicks / GenDate.TicksPerDay;
        }

        public static void Gain(Pawn pawn, float amount)
        {
            Need_Thrill need = pawn?.needs?.TryGetNeed<Need_Thrill>();
            if (need != null)
            {
                need.CurLevel += amount;
            }
        }
    }

    public class ThoughtWorker_Thrill : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            Need_Thrill need = p.needs?.TryGetNeed<Need_Thrill>();
            if (need == null)
            {
                return ThoughtState.Inactive;
            }
            float level = need.CurLevel;
            if (level < 0.1f)
            {
                return ThoughtState.ActiveAtStage(0);
            }
            if (level < 0.3f)
            {
                return ThoughtState.ActiveAtStage(1);
            }
            if (level >= 0.85f)
            {
                return ThoughtState.ActiveAtStage(2);
            }
            return ThoughtState.Inactive;
        }
    }

    /// <summary>
    /// The charm offensive. Only Sociopaths and Callous colonists use it, and only while they are keeping it together:
    /// once they're stressed the mask slips and this interaction stops happening.
    /// </summary>
    public class InteractionWorker_Charm : InteractionWorker
    {
        public override float RandomSelectionWeight(Pawn initiator, Pawn recipient)
        {
            if (!SP.IsCold(initiator) || SP.IsStressed(initiator))
            {
                return 0f;
            }
            if (recipient?.RaceProps == null || !recipient.RaceProps.Humanlike)
            {
                return 0f;
            }
            if (SP.Opinion(initiator, recipient) <= Tuning.GrudgeOpinion)
            {
                return 0f;
            }
            return Tuning.CharmWeight;
        }

        public override void Interacted(Pawn initiator, Pawn recipient, List<RulePackDef> extraSentencePacks,
            out string letterText, out string letterLabel, out LetterDef letterDef, out LookTargets lookTargets)
        {
            letterText = null;
            letterLabel = null;
            letterDef = null;
            lookTargets = null;
            Need_Thrill.Gain(initiator, Tuning.ThrillFromCharm);
        }
    }
}

namespace Sociopath
{
    /// <summary>
    /// Unsettling presence: colonists near a stressed Sociopath or Callous colonist, whose mask has slipped, feel uneasy.
    /// Sociopaths, Callous colonists and Psychopaths are immune (via nullifyingTraits in XML).
    /// </summary>
    public class ThoughtWorker_Unsettled : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!Tuning.UnsettlingPresence || !p.Spawned || !p.IsColonist || p.Map == null)
            {
                return ThoughtState.Inactive;
            }
            float radiusSq = Tuning.UnsettlingRadius * Tuning.UnsettlingRadius;
            IReadOnlyList<Pawn> colonists = p.Map.mapPawns.FreeColonistsSpawned;
            for (int i = 0; i < colonists.Count; i++)
            {
                Pawn other = colonists[i];
                if (other == p || !SP.IsCold(other) || !other.Awake() || !SP.IsStressed(other))
                {
                    continue;
                }
                if (other.Position.DistanceToSquared(p.Position) <= radiusSq)
                {
                    return ThoughtState.ActiveAtStage(0);
                }
            }
            return ThoughtState.Inactive;
        }
    }
}
