using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MudAndPlasteelFloor
{
    // Startup: apply Harmony patches, cache which terrains are ours, and push the tunable
    // def-stat values (plasteel beauty/cleanliness) into the loaded defs.
    [StaticConstructorOnStartup]
    public static class MPFCore
    {
        // Cached terrain sets, rebuilt at startup. Reference-equality HashSets -> O(1) lookup in the move-cost patch.
        public static readonly HashSet<TerrainDef> MudTerrains = new HashSet<TerrainDef>();
        public static readonly HashSet<TerrainDef> FastTerrains = new HashSet<TerrainDef>();

        static MPFCore()
        {
            new Harmony("juliusberger00.mudandplasteelfloor").PatchAll(typeof(MPFCore).Assembly);

            RebuildTerrainSets();
            MPFDefMutator.ApplyAll();

            Log.Message($"[MudAndPlasteelFloor] Startup complete: {MudTerrains.Count} mud terrain(s), {FastTerrains.Count} plasteel terrain(s).");
        }

        public static void RebuildTerrainSets()
        {
            MudTerrains.Clear();
            FastTerrains.Clear();
            foreach (var def in DefDatabase<TerrainDef>.AllDefsListForReading)
            {
                var ext = def.GetModExtension<MPFFloorExtension>();
                if (ext == null) continue;
                if (ext.isMud) MudTerrains.Add(def);
                if (ext.isFastFloor) FastTerrains.Add(def);
            }
        }

        public static MPFSettings S => MPFMod.Settings;
    }
}
