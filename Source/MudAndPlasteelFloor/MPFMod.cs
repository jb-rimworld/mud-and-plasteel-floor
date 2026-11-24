using UnityEngine;
using Verse;

namespace MudAndPlasteelFloor
{
    public class MPFMod : Mod
    {
        public static MPFSettings Settings;

        public MPFMod(ModContentPack content) : base(content)
        {
            Settings = GetSettings<MPFSettings>();
        }

        public override string SettingsCategory() => "MPF_SettingsCategory".Translate();

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Settings.DoWindowContents(inRect);
        }

        public override void WriteSettings()
        {
            base.WriteSettings();
            // Beauty / Cleanliness are def stats -> push them into the live defs so changes apply at once.
            MPFDefMutator.ApplyAll();
        }
    }

    public class MPFSettings : ModSettings
    {
        // --- Mud ---
        // Enemy movement SPEED on mud as a percentage of normal (100 = no effect, lower = slower).
        public float mudSpeedPercent = 40f;
        public bool mudAffectsEveryone = false;

        // --- Plasteel floor ---
        // Movement SPEED on the plasteel floor for everyone (100 = normal, 150 = +50%).
        public float plasteelSpeedPercent = 150f;
        public float plasteelBeauty = 5f;
        public float plasteelCleanliness = 1.2f;

        // Effective move-cost multipliers (cost = time = 100/speed). Read live by the patch.
        public float MudCostMult => 100f / Mathf.Max(1f, mudSpeedPercent);
        public float PlasteelCostMult => 100f / Mathf.Max(1f, plasteelSpeedPercent);

        public void DoWindowContents(Rect inRect)
        {
            var l = new Listing_Standard { maxOneColumn = true };
            l.Begin(inRect);

            Text.Font = GameFont.Medium;
            l.Label("MPF_SectionMud".Translate());
            Text.Font = GameFont.Small;
            l.GapLine();
            l.Label("MPF_MudSpeed".Translate(mudSpeedPercent.ToString("0")));
            mudSpeedPercent = Widgets.HorizontalSlider(l.GetRect(24f), mudSpeedPercent, 1f, 500f, false, null, "1%", "500%", 1f);
            l.Gap(4f);
            l.CheckboxLabeled("MPF_MudAffectsEveryone".Translate(), ref mudAffectsEveryone, "MPF_MudAffectsEveryoneTip".Translate());

            l.Gap(16f);
            Text.Font = GameFont.Medium;
            l.Label("MPF_SectionPlasteel".Translate());
            Text.Font = GameFont.Small;
            l.GapLine();
            l.Label("MPF_PlasteelSpeed".Translate(plasteelSpeedPercent.ToString("0")));
            plasteelSpeedPercent = Widgets.HorizontalSlider(l.GetRect(24f), plasteelSpeedPercent, 1f, 500f, false, null, "1%", "500%", 1f);
            l.Gap(4f);
            l.Label("MPF_Beauty".Translate(plasteelBeauty.ToString("0.0")));
            plasteelBeauty = Widgets.HorizontalSlider(l.GetRect(24f), plasteelBeauty, -10f, 20f, false, null, "-10", "20", 0.5f);
            l.Gap(4f);
            l.Label("MPF_Cleanliness".Translate(plasteelCleanliness.ToString("0.0")));
            plasteelCleanliness = Widgets.HorizontalSlider(l.GetRect(24f), plasteelCleanliness, -2f, 3f, false, null, "-2", "3", 0.1f);

            l.Gap(10f);
            if (l.ButtonText("MPF_Reset".Translate()))
            {
                mudSpeedPercent = 40f;
                mudAffectsEveryone = false;
                plasteelSpeedPercent = 150f;
                plasteelBeauty = 5f;
                plasteelCleanliness = 1.2f;
            }
            l.Label("MPF_LiveNote".Translate());

            l.End();

            // Beauty / Cleanliness live: push into the defs whenever a slider moved (cheap: a handful of defs).
            if (plasteelBeauty != lastAppliedBeauty || plasteelCleanliness != lastAppliedCleanliness)
                MPFDefMutator.ApplyAll();
        }

        // Last values written into the defs (to apply only on change while the window is open).
        public float lastAppliedBeauty = float.NaN;
        public float lastAppliedCleanliness = float.NaN;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref mudSpeedPercent, "mudSpeedPercent", 40f);
            Scribe_Values.Look(ref mudAffectsEveryone, "mudAffectsEveryone", false);
            Scribe_Values.Look(ref plasteelSpeedPercent, "plasteelSpeedPercent", 150f);
            Scribe_Values.Look(ref plasteelBeauty, "plasteelBeauty", 5f);
            Scribe_Values.Look(ref plasteelCleanliness, "plasteelCleanliness", 1.2f);
        }
    }
}
