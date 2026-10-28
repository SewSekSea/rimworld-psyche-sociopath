using UnityEngine;
using Verse;

namespace Sociopath
{
    public class SociopathSettings : ModSettings
    {
        public float grudgeDurationFactor = 3f;
        public float gratitudeDurationFactor = 1f / 3f;
        public float shallowAffectFactor = 0.6f;
        public int innerCircleOpinion = 60;
        public int grudgeOpinion = -20;
        public float targetedBreakFactor = 4f;
        public float thrillFallPerDay = 0.2f;
        public float thrillFromWorkPerDay = 0.8f;
        public float charmWeight = 0.2f;
        public float insultFactorContent = 0.4f;
        public float insultFactorStressed = 2.5f;
        public float unsettlingRadius = 10f;

        public bool seenThrough = true;
        public bool protectiveStreak = true;
        public bool unsettlingPresence = true;
        public bool coldUnderFire = true;

        public override void ExposeData()
        {
            SociopathSettings d = new SociopathSettings();
            Scribe_Values.Look(ref grudgeDurationFactor, "grudgeDurationFactor", d.grudgeDurationFactor);
            Scribe_Values.Look(ref gratitudeDurationFactor, "gratitudeDurationFactor", d.gratitudeDurationFactor);
            Scribe_Values.Look(ref shallowAffectFactor, "shallowAffectFactor", d.shallowAffectFactor);
            Scribe_Values.Look(ref innerCircleOpinion, "innerCircleOpinion", d.innerCircleOpinion);
            Scribe_Values.Look(ref grudgeOpinion, "grudgeOpinion", d.grudgeOpinion);
            Scribe_Values.Look(ref targetedBreakFactor, "targetedBreakFactor", d.targetedBreakFactor);
            Scribe_Values.Look(ref thrillFallPerDay, "thrillFallPerDay", d.thrillFallPerDay);
            Scribe_Values.Look(ref thrillFromWorkPerDay, "thrillFromWorkPerDay", d.thrillFromWorkPerDay);
            Scribe_Values.Look(ref charmWeight, "charmWeight", d.charmWeight);
            Scribe_Values.Look(ref insultFactorContent, "insultFactorContent", d.insultFactorContent);
            Scribe_Values.Look(ref insultFactorStressed, "insultFactorStressed", d.insultFactorStressed);
            Scribe_Values.Look(ref unsettlingRadius, "unsettlingRadius", d.unsettlingRadius);
            Scribe_Values.Look(ref seenThrough, "seenThrough", true);
            Scribe_Values.Look(ref protectiveStreak, "protectiveStreak", true);
            Scribe_Values.Look(ref unsettlingPresence, "unsettlingPresence", true);
            Scribe_Values.Look(ref coldUnderFire, "coldUnderFire", true);
        }

        public void Reset()
        {
            SociopathSettings d = new SociopathSettings();
            grudgeDurationFactor = d.grudgeDurationFactor;
            gratitudeDurationFactor = d.gratitudeDurationFactor;
            shallowAffectFactor = d.shallowAffectFactor;
            innerCircleOpinion = d.innerCircleOpinion;
            grudgeOpinion = d.grudgeOpinion;
            targetedBreakFactor = d.targetedBreakFactor;
            thrillFallPerDay = d.thrillFallPerDay;
            thrillFromWorkPerDay = d.thrillFromWorkPerDay;
            charmWeight = d.charmWeight;
            insultFactorContent = d.insultFactorContent;
            insultFactorStressed = d.insultFactorStressed;
            unsettlingRadius = d.unsettlingRadius;
            seenThrough = protectiveStreak = unsettlingPresence = coldUnderFire = true;
        }
    }

    public class SociopathMod : Mod
    {
        public static SociopathSettings Settings = new SociopathSettings();

        public SociopathMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<SociopathSettings>();
        }

        public override string SettingsCategory() => "Sociopath";

        public override void DoSettingsWindowContents(Rect inRect)
        {
            SociopathSettings s = Settings;
            Listing_Standard list = new Listing_Standard { ColumnWidth = (inRect.width - 34f) / 2f };
            list.Begin(inRect);

            list.Label("Memory and mood");
            s.grudgeDurationFactor = list.SliderLabeled($"Grudges last {s.grudgeDurationFactor:0.0}× as long",
                s.grudgeDurationFactor, 1f, 6f, 0.6f, "How long insults, harm and betrayals are remembered, compared with a normal colonist.");
            s.gratitudeDurationFactor = list.SliderLabeled($"Kindness lasts {s.gratitudeDurationFactor:0.00}× as long",
                s.gratitudeDurationFactor, 0.1f, 1f, 0.6f, "How long kind words, rescues and rapport are remembered.");
            s.shallowAffectFactor = list.SliderLabeled($"Events felt at {s.shallowAffectFactor:P0}",
                s.shallowAffectFactor, 0.2f, 1f, 0.6f, "Strength of mood from memories (events). Needs and surroundings are always felt in full.");
            s.innerCircleOpinion = Mathf.RoundToInt(list.SliderLabeled($"Inner circle at opinion {s.innerCircleOpinion}+",
                s.innerCircleOpinion, 20f, 100f, 0.6f, "Besides family and partners, anyone rated at least this highly still counts. Their deaths are grieved."));
            s.grudgeOpinion = Mathf.RoundToInt(list.SliderLabeled($"Grudge at opinion {s.grudgeOpinion} or lower",
                s.grudgeOpinion, -80f, -5f, 0.6f, "Anyone at or below this becomes a target when the Sociopath breaks."));

            list.Gap();
            list.Label("Breaks and the mask");
            s.targetedBreakFactor = list.SliderLabeled($"Targeted breaks {s.targetedBreakFactor:0.0}× as likely",
                s.targetedBreakFactor, 1f, 10f, 0.6f, "How much likelier targeted insulting sprees and murderous rages are while someone is a grudge target.");
            s.charmWeight = list.SliderLabeled($"Charm frequency {s.charmWeight:0.00}",
                s.charmWeight, 0f, 1f, 0.6f, "How often the Charm interaction is chosen. Vanilla chit-chat is 1.00.");
            s.insultFactorContent = list.SliderLabeled($"Insults when content ×{s.insultFactorContent:0.0}",
                s.insultFactorContent, 0f, 1f, 0.6f);
            s.insultFactorStressed = list.SliderLabeled($"Insults when stressed ×{s.insultFactorStressed:0.0}",
                s.insultFactorStressed, 1f, 5f, 0.6f);
            s.thrillFallPerDay = list.SliderLabeled($"Thrill drains {s.thrillFallPerDay:P0} a day",
                s.thrillFallPerDay, 0f, 1f, 0.6f, "Set to 0% to turn boredom off.");
            s.thrillFromWorkPerDay = list.SliderLabeled($"Violent work refills {s.thrillFromWorkPerDay:P0} a day",
                s.thrillFromWorkPerDay, 0f, 2f, 0.6f, "While hunting, slaughtering, executing or fighting, thrill rises at this rate instead of draining. Only work you assign counts; the colonist never seeks it out. Set to 0% to turn this off.");

            list.NewColumn();
            list.Label("Optional behaviours");
            list.CheckboxLabeled("Seen through", ref s.seenThrough,
                "Someone who was charmed and is later insulted by the same person realises it was an act.");
            list.CheckboxLabeled("Protective streak", ref s.protectiveStreak,
                "Anyone who hurts a Sociopath's inner circle gets a large opinion penalty with them.");
            list.CheckboxLabeled("Unsettling presence", ref s.unsettlingPresence,
                "Colonists near a stressed Sociopath or Callous colonist feel uneasy.");
            if (s.unsettlingPresence)
            {
                s.unsettlingRadius = list.SliderLabeled($"  Unease radius {s.unsettlingRadius:0} cells",
                    s.unsettlingRadius, 3f, 25f, 0.6f);
            }
            list.CheckboxLabeled("Cold under fire", ref s.coldUnderFire,
                "No mood breaks while drafted or in combat. They pay for it with a mood penalty afterwards.");

            list.Gap(24f);
            if (list.ButtonText("Reset to defaults"))
            {
                s.Reset();
            }
            list.End();
        }
    }
}
