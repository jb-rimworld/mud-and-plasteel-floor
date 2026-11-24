using RimWorld;
using Verse;

namespace MudAndPlasteelFloor
{
    // Writes the tunable stat values from settings straight into the loaded plasteel TerrainDefs,
    // so Beauty / Cleanliness sliders apply live (the game re-reads statBases continuously).
    public static class MPFDefMutator
    {
        public static void ApplyAll()
        {
            var s = MPFMod.Settings;
            if (s == null) return;

            foreach (var def in MPFCore.FastTerrains)
            {
                SetStat(def, StatDefOf.Beauty, s.plasteelBeauty);
                SetStat(def, StatDefOf.Cleanliness, s.plasteelCleanliness);
            }
            s.lastAppliedBeauty = s.plasteelBeauty;
            s.lastAppliedCleanliness = s.plasteelCleanliness;
        }

        private static void SetStat(TerrainDef def, StatDef stat, float value)
        {
            if (def?.statBases == null || stat == null) return;
            var existing = def.statBases.Find(m => m.stat == stat);
            if (existing != null) existing.value = value;
            else def.statBases.Add(new StatModifier { stat = stat, value = value });
        }
    }
}
