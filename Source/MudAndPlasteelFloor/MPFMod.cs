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

        public override string SettingsCategory() => "Mud and Plasteel Floor";

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
            l.Label("Mud");
            Text.Font = GameFont.Small;
            l.GapLine();
            l.Label($"Enemy movement speed on mud: {mudSpeedPercent:0}%   (100% = normal, lower = slower)");
            mudSpeedPercent = Widgets.HorizontalSlider(l.GetRect(24f), mudSpeedPercent, 1f, 500f, false, null, "1%", "500%", 1f);
            l.Gap(4f);
            l.CheckboxLabeled("Mud slows everyone (not just enemies)", ref mudAffectsEveryone,
                "Off by default: only pawns hostile to you are slowed, so your colonists cross mud at full speed. Turn on for the classic 'slows everyone' behaviour.");

            l.Gap(16f);
            Text.Font = GameFont.Medium;
            l.Label("Plasteel floor");
            Text.Font = GameFont.Small;
            l.GapLine();
            l.Label($"Movement speed on plasteel floor: {plasteelSpeedPercent:0}%   (100% = normal, 150% = +50%)");
            plasteelSpeedPercent = Widgets.HorizontalSlider(l.GetRect(24f), plasteelSpeedPercent, 1f, 500f, false, null, "1%", "500%", 1f);
            l.Gap(4f);
            l.Label($"Beauty: {plasteelBeauty:0.0}   (fine floor = 3)");
            plasteelBeauty = Widgets.HorizontalSlider(l.GetRect(24f), plasteelBeauty, -10f, 20f, false, null, "-10", "20", 0.5f);
            l.Gap(4f);
            l.Label($"Cleanliness: {plasteelCleanliness:0.0}   (sterile tile = 0.6)");
            plasteelCleanliness = Widgets.HorizontalSlider(l.GetRect(24f), plasteelCleanliness, -2f, 3f, false, null, "-2", "3", 0.1f);

            l.Gap(10f);
            if (l.ButtonText("Reset to defaults"))
            {
                mudSpeedPercent = 40f;
                mudAffectsEveryone = false;
                plasteelSpeedPercent = 150f;
                plasteelBeauty = 5f;
                plasteelCleanliness = 1.2f;
            }
            l.Label("Speed changes apply instantly. Beauty and cleanliness apply when you close this window.");

            l.End();
        }

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
