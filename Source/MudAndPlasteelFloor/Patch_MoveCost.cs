using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MudAndPlasteelFloor
{
    // Postfix on the per-cell move-cost calc. This is the only vanilla place move cost can be made
    // faction-dependent or expressed as a clean percentage (terrain pathCost applies to everyone and
    // is a flat integer). Mud slows hostiles; the plasteel floor speeds up everyone.
    [HarmonyPatch(typeof(Pawn_PathFollower), "CostToMoveIntoCell", new[] { typeof(Pawn), typeof(IntVec3) })]
    public static class Patch_CostToMoveIntoCell
    {
        static void Postfix(Pawn pawn, IntVec3 c, ref float __result)
        {
            if (pawn == null || pawn.Map == null) return;
            if (MPFCore.MudTerrains.Count == 0 && MPFCore.FastTerrains.Count == 0) return;

            TerrainDef terrain = c.GetTerrain(pawn.Map);
            if (terrain == null) return;

            var s = MPFMod.Settings;
            if (s == null) return;

            // Plasteel floor: everyone moves faster.
            if (MPFCore.FastTerrains.Contains(terrain))
            {
                __result = Mathf.Max(1f, __result * s.PlasteelCostMult);
                return;
            }

            // Mud: slow hostiles (or everyone, if the toggle is on).
            if (MPFCore.MudTerrains.Contains(terrain))
            {
                if (s.mudAffectsEveryone || IsHostileToPlayer(pawn))
                    __result = Mathf.Max(1f, __result * s.MudCostMult);
            }
        }

        private static bool IsHostileToPlayer(Pawn pawn)
        {
            Faction player = Faction.OfPlayerSilentFail;
            if (player == null) return false;
            return pawn.HostileTo(player);
        }
    }
}
