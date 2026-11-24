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

            bool changed = s.lastAppliedBeauty != s.plasteelBeauty || s.lastAppliedCleanliness != s.plasteelCleanliness;
            foreach (var def in MPFCore.FastTerrains)
            {
                SetStat(def, StatDefOf.Beauty, s.plasteelBeauty);
                SetStat(def, StatDefOf.Cleanliness, s.plasteelCleanliness);
            }
            s.lastAppliedBeauty = s.plasteelBeauty;
            s.lastAppliedCleanliness = s.plasteelCleanliness;
            // Rooms cache their beauty and cleanliness scores; without a nudge the room readout would lag behind the slider.
            if (changed) MarkRoomsDirty();
        }

        private static void MarkRoomsDirty()
        {
#if RW17
            try
            {
                if (Current.ProgramState != ProgramState.Playing) return;
                foreach (Map map in Find.Maps)
                    foreach (Room room in map.regionGrid.AllRooms)
                        room.Notify_TerrainChanged();
            }
            catch (System.Exception e) { Log.Warning("[MudAndPlasteelFloor] Could not refresh room readouts: " + e.Message); }
#endif
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
