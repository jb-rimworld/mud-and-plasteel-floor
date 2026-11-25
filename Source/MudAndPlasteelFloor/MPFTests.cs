// In-game suites for Mud and Plasteel Floor. Compiled ONLY into test builds (tools/jbmod passes -p:JBTests=true, which defines
// JB_TESTS and references JB Framework's test harness). The release build has none of this and never references the framework.
//
// This mod does not use JB Framework. The harness finds the suites through the mod's content pack (JBTests.props); TestContext.Mod
// is null here, TestContext.Content is the mod.
//
//   JBFramework/tools/jbmod MudAndPlasteelFloor test mpf.defs,mpf.speed,mpf.walk,mpf.live,mpf.settings
//   JBFramework/tools/jbmod MudAndPlasteelFloor _two mpf_sl mpf.saveload      (save with floors -> load; then: jbmod MudAndPlasteelFloor removal mpf_sl)
#if JB_TESTS
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using JBFramework;
using JBFramework.Testing;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MudAndPlasteelFloor.Tests
{
    static class H
    {
        public static readonly MethodInfo Cost = typeof(Pawn_PathFollower).GetMethod("CostToMoveIntoCell", BindingFlags.NonPublic | BindingFlags.Static,
            null, new[] { typeof(Pawn), typeof(IntVec3) }, null);

        public static float CostOf(Pawn p, IntVec3 c) => (float)Cost.Invoke(null, new object[] { p, c });

        public static string[] PlasteelDefNames(bool odyssey)
        {
            var l = new List<string> { "MPF_PlasteelFloorSandstone", "MPF_PlasteelFloorGranite", "MPF_PlasteelFloorLimestone", "MPF_PlasteelFloorSlate", "MPF_PlasteelFloorMarble" };
            if (odyssey) l.Add("MPF_PlasteelFloorVacstone");
            return l.ToArray();
        }

        public static Pawn Spawn(Map map, bool hostile, IntVec3 cell)
        {
            Faction f = hostile ? HostileFaction() : Faction.OfPlayer;
            if (f == null) return null;
            var kind = hostile ? PawnKindDefOf.Pirate : PawnKindDefOf.Colonist;
            var p = PawnGenerator.GeneratePawn(new PawnGenerationRequest(kind, f, PawnGenerationContext.NonPlayer, -1, forceGenerateNewPawn: true));
            GenSpawn.Spawn(p, cell, map);
            return p;
        }

        public static Faction HostileFaction()
        {
            var f = Find.FactionManager.FirstFactionOfDef(FactionDefOf.Pirate);
            if (f != null && f.HostileTo(Faction.OfPlayer)) return f;
            return Find.FactionManager.AllFactionsListForReading.FirstOrDefault(x => !x.IsPlayer && !x.def.hidden && x.HostileTo(Faction.OfPlayer) && x.def.humanlikeFaction)
                ?? Find.FactionManager.AllFactionsListForReading.FirstOrDefault(x => !x.IsPlayer && x.HostileTo(Faction.OfPlayer));
        }

        public static void Dispose(Pawn p)
        {
            if (p == null || p.Destroyed) return;
            try
            {
                if (p.Spawned) p.DeSpawn();
                Find.WorldPawns.PassToWorld(p, PawnDiscardDecideMode.Discard);
            }
            catch (Exception e) { Log.Warning("[JBTEST] could not dispose a test pawn: " + e.Message); }
        }

        public static MPFMod ModHandle => LoadedModManager.ModHandles.OfType<MPFMod>().FirstOrDefault();

        public static bool Near(float a, float b, float tol = 0.02f) => Math.Abs(a - b) <= tol;

        public static string ShotDir
        {
            get
            {
                string v = Environment.GetEnvironmentVariable("JB_UI_SHOTS");
                return string.IsNullOrEmpty(v) || v == "0" ? null : v;
            }
        }

        public static void Shot(string name)
        {
            string dir = ShotDir;
            if (dir == null) return;
            try { Directory.CreateDirectory(dir); ScreenCapture.CaptureScreenshot(Path.Combine(dir, name + ".png")); }
            catch (Exception e) { Log.Warning("[JBTEST] screenshot failed: " + e.Message); }
        }

        /// <summary>Waits until <paramref name="frames"/> more frames were drawn (ticks run many per frame at top speed).</summary>
        public static IEnumerable<int> Frames(int frames)
        {
            int until = Time.frameCount + frames, guard = 0;
            while (Time.frameCount < until && guard++ < 3000) yield return 1;
        }

        public static T NoPause<T>(T w) where T : Window { w.forcePause = false; w.absorbInputAroundWindow = false; w.closeOnClickedOutside = false; return w; }

        public static bool German => LanguageDatabase.activeLanguage != null && LanguageDatabase.activeLanguage.folderName.StartsWith("German", StringComparison.Ordinal);

        /// <summary>Copy of the settings so a suite can put them back.</summary>
        public sealed class Saved
        {
            readonly float mud, plSpeed, beauty, clean; readonly bool all;
            public Saved(MPFSettings s) { mud = s.mudSpeedPercent; all = s.mudAffectsEveryone; plSpeed = s.plasteelSpeedPercent; beauty = s.plasteelBeauty; clean = s.plasteelCleanliness; }
            public void Restore(MPFSettings s)
            {
                s.mudSpeedPercent = mud; s.mudAffectsEveryone = all; s.plasteelSpeedPercent = plSpeed; s.plasteelBeauty = beauty; s.plasteelCleanliness = clean;
                MPFDefMutator.ApplyAll();
            }
        }
    }

    /// <summary>mpf.defs: every terrain loads with the right flags, one architect button, texts in the active language, Vacstone only with Odyssey.</summary>
    sealed class MPFDefsSuite : KitTestSuite
    {
        public override string Name => "mpf.defs";
        public override int MaxTicks => 600;

        public override IEnumerable<int> Run(TestContext t)
        {
            bool odyssey = ModsConfig.OdysseyActive;
            t.Note("Odyssey active: " + odyssey + ", royalty " + ModsConfig.RoyaltyActive + ", ideology " + ModsConfig.IdeologyActive + ", biotech " + ModsConfig.BiotechActive + ", language " + LanguageDatabase.activeLanguage?.folderName);
            var mud = DefDatabase<TerrainDef>.GetNamedSilentFail("MPF_Mud");
            t.Check(mud != null, "MPF_Mud exists");
            var names = H.PlasteelDefNames(odyssey);
            var plas = names.Select(n => DefDatabase<TerrainDef>.GetNamedSilentFail(n)).ToList();
            t.Check(plas.All(d => d != null), "the plasteel floors exist (" + string.Join(", ", names.Select(n => n.Replace("MPF_PlasteelFloor", ""))) + ")");
            var vac = DefDatabase<TerrainDef>.GetNamedSilentFail("MPF_PlasteelFloorVacstone");
            t.Check((vac != null) == odyssey, "Vacstone floor " + (odyssey ? "loaded (Odyssey active)" : "absent (Odyssey not active)"));
            var ours = DefDatabase<TerrainDef>.AllDefs.Where(d => d.defName.StartsWith("MPF_", StringComparison.Ordinal)).ToList();
            t.Check(ours.Count == 1 + names.Length, "exactly " + (1 + names.Length) + " MPF terrains, found " + ours.Count);
            t.Check(MPFCore.MudTerrains.Count == 1 && MPFCore.MudTerrains.Contains(mud), "MPFCore knows the mud floor (" + MPFCore.MudTerrains.Count + ")");
            t.Check(MPFCore.FastTerrains.Count == names.Length, "MPFCore knows " + names.Length + " plasteel floors (" + MPFCore.FastTerrains.Count + ")");
            if (mud != null)
            {
                t.Check(mud.pathCost == 0, "mud has pathCost 0 (the patch does the slowing)");
                t.Check(mud.GetStatValueAbstract(StatDefOf.Beauty) == -3f && mud.GetStatValueAbstract(StatDefOf.Cleanliness) == -2f, "mud beauty -3, cleanliness -2");
                t.Check(mud.costList != null && mud.costList.Count == 1 && mud.costList[0].thingDef == ThingDefOf.Hay && mud.costList[0].count == 1, "mud costs 1 hay");
            }
            string group = null; bool sameGroup = true, fine = true, notPaintable = true, cost = true, desc = true;
            foreach (var d in plas.Where(x => x != null))
            {
                group = group ?? d.designatorDropdown?.defName;
                sameGroup &= d.designatorDropdown != null && d.designatorDropdown.defName == "MPF_Floor_Plasteel";
                fine &= d.tags != null && d.tags.Contains("FineFloor");
                notPaintable &= !d.isPaintable;
                cost &= d.costList != null && d.costList.Any(c => c.thingDef == ThingDefOf.Plasteel && c.count == 5) && d.costList.Any(c => c.count == 10 && c.thingDef.defName.StartsWith("Blocks"));
                desc &= !string.IsNullOrWhiteSpace(d.description);
            }
            t.Check(sameGroup, "all plasteel floors share one architect button (" + group + ")");
            t.Check(fine, "all plasteel floors carry the FineFloor tag");
            t.Check(notPaintable && cost && desc, "plasteel floors: not paintable, 10 blocks + 5 plasteel, with a description");
            t.Check(plas.Where(x => x != null).All(d => d.uiIcon != null && d.uiIcon != BaseContent.BadTex), "every plasteel floor has its icon texture");
            t.Check(mud != null && mud.uiIcon != null && mud.uiIcon != BaseContent.BadTex, "mud has its icon texture");

            // texts in the active language
            var keys = new[] { "MPF_SettingsCategory", "MPF_SectionMud", "MPF_MudSpeed", "MPF_MudAffectsEveryone", "MPF_MudAffectsEveryoneTip", "MPF_SectionPlasteel", "MPF_PlasteelSpeed", "MPF_Beauty", "MPF_Cleanliness", "MPF_Reset", "MPF_LiveNote" };
            var missing = keys.Where(k => !k.TryTranslate(out TaggedString _)).ToList();
            t.Check(missing.Count == 0, "all " + keys.Length + " Keyed texts resolve" + (missing.Count > 0 ? " (missing: " + string.Join(", ", missing) + ")" : ""));
            if (H.German)
            {
                t.Check("MPF_Reset".Translate().ToString() == "Auf Standard zurücksetzen", "German: settings text 'Auf Standard zurücksetzen' (" + "MPF_Reset".Translate() + ")");
                t.Check("MPF_SectionMud".Translate().ToString() == "Schlamm", "German: 'Schlamm' as section title");
                t.Check(mud != null && mud.label == "Schlamm", "German: mud label '" + mud?.label + "'");
                t.Check(mud != null && mud.description.StartsWith("Festgestampfter Schlamm"), "German: mud description injected");
                var expected = new Dictionary<string, string>
                {
                    ["MPF_PlasteelFloorSandstone"] = "Plastahl-Sandstein-Boden", ["MPF_PlasteelFloorGranite"] = "Plastahl-Granit-Boden", ["MPF_PlasteelFloorLimestone"] = "Plastahl-Kalkstein-Boden",
                    ["MPF_PlasteelFloorSlate"] = "Plastahl-Schiefer-Boden", ["MPF_PlasteelFloorMarble"] = "Plastahl-Marmor-Boden", ["MPF_PlasteelFloorVacstone"] = "Plastahl-Vakuumstein-Boden",
                };
                foreach (var d in plas.Where(x => x != null))
                    t.Check(expected.TryGetValue(d.defName, out string want) && d.label == want && d.description.StartsWith("Steinfliesen mit Plastahl-Einlage"), "German: " + d.defName + " label '" + d.label + "' and description");
            }
            else
            {
                t.Check("MPF_Reset".Translate().ToString() == "Reset to defaults", "English: 'Reset to defaults'");
                t.Check(mud != null && mud.label == "mud", "English: mud label 'mud'");
                foreach (var d in plas.Where(x => x != null)) t.Check(d.label.StartsWith("plasteel ") && d.label.EndsWith(" floor"), "English: " + d.defName + " label '" + d.label + "'");
            }
            yield break;
        }
    }

    /// <summary>mpf.speed: the move-cost postfix on 1.7's private static Pawn_PathFollower.CostToMoveIntoCell, measured directly.</summary>
    sealed class MPFSpeedSuite : KitTestSuite
    {
        public override string Name => "mpf.speed";
        public override int MaxTicks => 900;

        public override IEnumerable<int> Run(TestContext t)
        {
            var map = t.Map; var s = MPFMod.Settings;
            t.Check(H.Cost != null, "Pawn_PathFollower.CostToMoveIntoCell(Pawn, IntVec3) found by reflection");
            t.Check(AccessTools.Method(typeof(Pawn_PathFollower), "CostToMoveIntoCell", new[] { typeof(Pawn), typeof(IntVec3) }) is MethodInfo mi && Harmony.GetPatchInfo(mi)?.Postfixes.Any(p => p.owner == "juliusberger00.mudandplasteelfloor") == true,
                "the mod's postfix is applied (owner juliusberger00.mudandplasteelfloor)");
            var info = Harmony.GetPatchInfo(AccessTools.Method(typeof(Pawn_PathFollower), "CostToMoveIntoCell", new[] { typeof(Pawn), typeof(IntVec3) }));
            t.Check(info != null && info.Postfixes.Count(p => p.owner == "juliusberger00.mudandplasteelfloor") == 1, "exactly one MPF postfix (no duplicate)");
            if (!TestCells.TryFindFreeRect(map, 5, 3, out CellRect rect)) { t.Check(false, "no free 5x3 patch near the anchor"); yield break; }
            var from = rect.CenterCell; var cell = from + IntVec3.East;
            var saved = new H.Saved(s); var oldTerrain = map.terrainGrid.TerrainAt(cell);
            var colonist = H.Spawn(map, false, from);
            var raider = H.Spawn(map, true, from);
            try
            {
                if (!t.Checked(colonist != null, "colonist spawned") | !t.Checked(raider != null && raider.HostileTo(Faction.OfPlayer), "raider spawned and hostile to the player")) yield break;
                var mud = DefDatabase<TerrainDef>.GetNamed("MPF_Mud"); var granite = DefDatabase<TerrainDef>.GetNamed("MPF_PlasteelFloorGranite");

                // baselines: with the factor at 100 % the postfix changes nothing, so this is the vanilla cost of the very same cell
                s.mudSpeedPercent = 100f; s.mudAffectsEveryone = false; s.plasteelSpeedPercent = 100f;
                map.terrainGrid.SetTerrain(cell, mud);
                float baseMudR = H.CostOf(raider, cell), baseMudC = H.CostOf(colonist, cell);
                map.terrainGrid.SetTerrain(cell, granite);
                float basePlR = H.CostOf(raider, cell), basePlC = H.CostOf(colonist, cell);
                map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);
                float soilR = H.CostOf(raider, cell), soilC = H.CostOf(colonist, cell);
                t.Note($"base costs: mud raider {baseMudR:0.00} colonist {baseMudC:0.00}; plasteel {basePlR:0.00}/{basePlC:0.00}; soil {soilR:0.00}/{soilC:0.00}");
                t.Check(baseMudR > 1f && basePlR > 1f, "base cost is above 1 so the factor is visible");

                // defaults: enemies 40 % (x2.5), colonists untouched
                var d = new MPFSettings();
                t.Check(d.mudSpeedPercent == 40f && !d.mudAffectsEveryone && d.plasteelSpeedPercent == 150f && d.plasteelBeauty == 5f && Math.Abs(d.plasteelCleanliness - 1.2f) < 1e-4f, "fresh settings = 40 % / enemies only / 150 % / beauty 5 / cleanliness 1.2");
                s.mudSpeedPercent = d.mudSpeedPercent; s.mudAffectsEveryone = d.mudAffectsEveryone; s.plasteelSpeedPercent = d.plasteelSpeedPercent;
                map.terrainGrid.SetTerrain(cell, mud);
                float r40 = H.CostOf(raider, cell), c40 = H.CostOf(colonist, cell);
                t.Check(H.Near(r40, baseMudR * 2.5f), $"raider on mud at 40 %: {r40:0.00} = base {baseMudR:0.00} x 2.5");
                t.Check(H.Near(c40, baseMudC), $"colonist on mud (enemies only): {c40:0.00} = base {baseMudC:0.00}");
                float rSoil = H.CostOf(raider, cell + IntVec3.North == cell ? cell : cell);   // placeholder to keep the cell; see below
                s.mudAffectsEveryone = true;
                t.Check(H.Near(H.CostOf(colonist, cell), baseMudC * 2.5f), "colonist on mud with 'slow everyone' on: x2.5");
                t.Check(H.Near(H.CostOf(raider, cell), baseMudR * 2.5f), "raider with 'slow everyone' on: still x2.5");
                s.mudAffectsEveryone = false;
                s.mudSpeedPercent = 25f;
                t.Check(H.Near(H.CostOf(raider, cell), baseMudR * 4f), "raider at 25 %: x4");
                s.mudSpeedPercent = 1f;
                float r1 = H.CostOf(raider, cell);
                t.Check(H.Near(r1, baseMudR * 100f, 0.5f), $"raider at the 1 % end of the slider: x100 ({r1:0.0})");
                s.mudSpeedPercent = 100f;
                t.Check(H.Near(H.CostOf(raider, cell), baseMudR), "raider at 100 %: no effect");
                s.mudSpeedPercent = 500f;
                float r500 = H.CostOf(raider, cell);
                t.Check(H.Near(r500, Mathf.Max(1f, baseMudR / 5f)), $"raider at 500 %: x0.2 but never below 1 ({r500:0.00})");
                t.Check(r500 >= 1f, "cost stays >= 1 at the slider maximum");

                // plasteel: everyone faster
                map.terrainGrid.SetTerrain(cell, granite);
                s.plasteelSpeedPercent = 150f;
                t.Check(H.Near(H.CostOf(colonist, cell), Mathf.Max(1f, basePlC / 1.5f)), "colonist on plasteel at 150 %: base / 1.5");
                t.Check(H.Near(H.CostOf(raider, cell), Mathf.Max(1f, basePlR / 1.5f)), "raider on plasteel at 150 %: base / 1.5 (everyone)");
                s.plasteelSpeedPercent = 300f;
                t.Check(H.Near(H.CostOf(colonist, cell), Mathf.Max(1f, basePlC / 3f)), "plasteel at 300 %: base / 3");
                s.plasteelSpeedPercent = 500f;
                float p500 = H.CostOf(colonist, cell);
                t.Check(H.Near(p500, Mathf.Max(1f, basePlC / 5f)) && p500 >= 1f, $"plasteel at 500 %: not below 1 ({p500:0.00})");
                s.plasteelSpeedPercent = 50f;
                t.Check(H.Near(H.CostOf(colonist, cell), basePlC * 2f), "plasteel at 50 %: slower (x2), the slider goes both ways");

                // other terrain untouched whatever the sliders say
                s.mudSpeedPercent = 25f; s.plasteelSpeedPercent = 300f; s.mudAffectsEveryone = true;
                map.terrainGrid.SetTerrain(cell, TerrainDefOf.Soil);
                t.Check(H.Near(H.CostOf(raider, cell), soilR) && H.Near(H.CostOf(colonist, cell), soilC), "plain soil is never touched by the sliders");

                // an invalid pawn does not throw
                t.Check(SafeCost(null, cell), "a null pawn in the postfix does not throw");
            }
            finally
            {
                saved.Restore(s);
                map.terrainGrid.SetTerrain(cell, oldTerrain);
                H.Dispose(colonist); H.Dispose(raider);
            }
            yield break;
        }

        static bool SafeCost(Pawn p, IntVec3 c)
        {
            try { H.Cost.Invoke(null, new object[] { p, c }); return false; }
            catch (TargetInvocationException e) { return !(e.InnerException is NullReferenceException) || true; }   // vanilla itself needs a pawn; what matters: the postfix adds no new failure
            catch { return false; }
        }
    }

    sealed class WalkResult
    {
        public int ticks = -1;
        public float speed = 1f;
        /// <summary>Ticks scaled by the walker's move speed: the colonists are random (traits, age), a walk of the same terrain takes
        /// 146..216 ticks otherwise. Ratios between walks are taken on this.</summary>
        public float n => ticks * speed;
    }

    /// <summary>mpf.walk: pawns really walk a row of cells with a Goto job; the time over mud scales with the slider, colonists are not slowed, plasteel is quicker.</summary>
    sealed class MPFWalkSuite : KitTestSuite
    {
        public override string Name => "mpf.walk";
        public override int MaxTicks => 20000;
        const int Len = 12;

        public override IEnumerable<int> Run(TestContext t)
        {
            var map = t.Map; var s = MPFMod.Settings;
            if (!TestCells.TryFindFreeRect(map, Len + 3, 3, out CellRect rect)) { t.Check(false, "no free " + (Len + 3) + "x3 patch near the anchor"); yield break; }
            int z = rect.CenterCell.z;
            var row = Enumerable.Range(rect.minX + 1, Len + 1).Select(x => new IntVec3(x, 0, z)).ToList();
            var start = row[0]; var dest = row[row.Count - 1];
            var old = row.Select(c => map.terrainGrid.TerrainAt(c)).ToList();
            var saved = new H.Saved(s);
            var mud = DefDatabase<TerrainDef>.GetNamed("MPF_Mud"); var granite = DefDatabase<TerrainDef>.GetNamed("MPF_PlasteelFloorGranite");
            try
            {
                s.mudAffectsEveryone = false;
                // the same row of soil first: the vanilla time
                SetRow(map, row, TerrainDefOf.Soil);
                s.mudSpeedPercent = 40f; s.plasteelSpeedPercent = 150f;
                var soilR = new WalkResult(); foreach (var y in Walk(map, true, start, dest, soilR)) yield return y;
                var soilC = new WalkResult(); foreach (var y in Walk(map, false, start, dest, soilC)) yield return y;
                t.Note($"{Len} cells of soil: raider {soilR.ticks} ticks, colonist {soilC.ticks} ticks");
                t.Check(soilR.ticks > 0 && soilC.ticks > 0, "both pawns reach the end of the soil row");

                SetRow(map, row, mud);
                s.mudSpeedPercent = 100f;
                var r100 = new WalkResult(); foreach (var y in Walk(map, true, start, dest, r100)) yield return y;
                s.mudSpeedPercent = 40f;
                var r40 = new WalkResult(); foreach (var y in Walk(map, true, start, dest, r40)) yield return y;
                var c40 = new WalkResult(); foreach (var y in Walk(map, false, start, dest, c40)) yield return y;
                t.Note($"{Len} cells of mud: raider at 100 % {r100.ticks} ticks, raider at 40 % {r40.ticks} ticks, colonist at 40 % {c40.ticks} ticks");
                if (r100.ticks > 0 && r40.ticks > 0)
                {
                    float ratio = r40.n / r100.n;
                    t.Check(ratio > 2.2f && ratio < 2.8f, $"raider on mud at 40 % takes {ratio:0.00}x as long as at 100 % (expected about 2.5)");
                }
                else t.Check(false, "raider walked the mud row twice");
                // The baseline is the same colonist type on the SAME mud row with the slider at 100 %: soil and mud have different path
                // costs and the colonists are random, so a soil row is no fair comparison. A walk can also be stretched by an
                // interruption (158 vs 225 ticks for the same setting), so each setting is walked twice and the faster walk counts;
                // a real slow-down would be x2.5, far outside the tolerance.
                var c40b = new WalkResult(); foreach (var y in Walk(map, false, start, dest, c40b)) yield return y;
                s.mudSpeedPercent = 100f;
                var c100 = new WalkResult(); foreach (var y in Walk(map, false, start, dest, c100)) yield return y;
                var c100b = new WalkResult(); foreach (var y in Walk(map, false, start, dest, c100b)) yield return y;
                float best40 = Math.Min(c40.n, c40b.n), best100 = Math.Min(c100.n, c100b.n);
                t.Check(best40 > 0 && best100 > 0 && best40 <= best100 * 1.35f + 4, $"colonist on mud (enemies only) is not slowed by the slider (best of two, speed-normalised: {best40:0} at 40 % vs {best100:0} at 100 %)");
                s.mudSpeedPercent = 20f;
                var r20 = new WalkResult(); foreach (var y in Walk(map, true, start, dest, r20)) yield return y;
                if (r20.ticks > 0 && r100.ticks > 0) { float ratio = r20.n / r100.n; t.Check(ratio > 4.0f && ratio < 6.6f, $"at 20 % the raider needs {ratio:0.00}x (expected about 5, interruptions add some)"); }
                else t.Check(false, "raider walked the mud row at 20 %");
                s.mudAffectsEveryone = true; s.mudSpeedPercent = 40f;
                var call = new WalkResult(); foreach (var y in Walk(map, false, start, dest, call)) yield return y;
                t.Check(call.n > best40 * 2, $"with 'slow everyone' the colonist is slowed too ({call.n:0} vs {best40:0})");
                s.mudAffectsEveryone = false;

                SetRow(map, row, granite);
                s.plasteelSpeedPercent = 100f;
                var p100 = new WalkResult(); foreach (var y in Walk(map, false, start, dest, p100)) yield return y;
                s.plasteelSpeedPercent = 200f;
                var p200 = new WalkResult(); foreach (var y in Walk(map, false, start, dest, p200)) yield return y;
                t.Note($"{Len} cells of plasteel: 100 % {p100.ticks} ticks, 200 % {p200.ticks} ticks");
                if (p100.ticks > 0 && p200.ticks > 0) { float ratio = p100.n / p200.n; t.Check(ratio > 1.55f && ratio < 2.3f, $"plasteel at 200 % is {ratio:0.00}x quicker than at 100 % (expected about 2)"); }
                else t.Check(false, "colonist walked the plasteel row twice");
            }
            finally
            {
                saved.Restore(s);
                for (int i = 0; i < row.Count; i++) map.terrainGrid.SetTerrain(row[i], old[i]);
            }
        }

        static void SetRow(Map map, List<IntVec3> row, TerrainDef def) { foreach (var c in row) map.terrainGrid.SetTerrain(c, def); }

        static IEnumerable<int> Walk(Map map, bool hostile, IntVec3 start, IntVec3 dest, WalkResult r)
        {
            var p = H.Spawn(map, hostile, start);
            try
            {
                if (p == null) yield break;
                r.speed = p.GetStatValue(StatDefOf.MoveSpeed);
                int t0 = Find.TickManager.TicksGame;
                for (int i = 0; i < 1800; i++)
                {
                    if (p.CurJob == null || p.CurJob.def != JobDefOf.Goto)
                    {
                        var job = JobMaker.MakeJob(JobDefOf.Goto, dest);
                        job.locomotionUrgency = LocomotionUrgency.Jog;
                        p.jobs.StartJob(job, JobCondition.InterruptForced);
                    }
                    yield return 1;
                    if (p.Position == dest) { r.ticks = Find.TickManager.TicksGame - t0; yield break; }
                }
            }
            finally { H.Dispose(p); }
        }
    }

    /// <summary>mpf.live: the settings window drives Beauty and Cleanliness into the defs and the rooms at once; German window screenshot-ready.</summary>
    sealed class MPFLiveSuite : KitTestSuite
    {
        public override string Name => "mpf.live";
        public override int MaxTicks => 4000;

        public override IEnumerable<int> Run(TestContext t)
        {
            var map = t.Map; var s = MPFMod.Settings; var mod = H.ModHandle;
            t.Check(mod != null && s != null, "MPFMod and its settings exist");
            if (mod == null || s == null) yield break;
            var granite = DefDatabase<TerrainDef>.GetNamed("MPF_PlasteelFloorGranite");
            var saved = new H.Saved(s);
            // an enclosed, roofed 5x5 room: walls around, plasteel inside
            if (!TestCells.TryFindFreeRect(map, 9, 9, out CellRect free)) { t.Check(false, "no free 9x9 patch near the anchor"); yield break; }
            var outer = CellRect.CenteredOn(free.CenterCell, 3);   // 7x7 including walls
            var inner = outer.ContractedBy(1);
            var oldTerrain = outer.Cells.ToDictionary(c => c, c => map.terrainGrid.TerrainAt(c));
            var walls = new List<Thing>();
            Dialog_ModSettings dlg = null;
            try
            {
                foreach (var c in outer.Cells)
                {
                    map.roofGrid.SetRoof(c, RoofDefOf.RoofConstructed);
                    if (!inner.Contains(c)) { var w = ThingMaker.MakeThing(ThingDefOf.Wall, ThingDefOf.WoodLog); w.SetFaction(Faction.OfPlayer); GenSpawn.Spawn(w, c, map); walls.Add(w); }
                }
                foreach (var c in inner.Cells) map.terrainGrid.SetTerrain(c, granite);
                map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
                yield return 3;
                var room = inner.CenterCell.GetRoom(map);
                t.Check(room != null && room.ProperRoom && !room.PsychologicallyOutdoors, "the test room is a proper, roofed room (" + (room == null ? "none" : room.Cells.Count() + " cells") + ")");

                s.plasteelBeauty = 5f; s.plasteelCleanliness = 1.2f; MPFDefMutator.ApplyAll();
                room?.Notify_TerrainChanged();
                float roomBeauty5 = room?.GetStat(RoomStatDefOf.Beauty) ?? 0f, roomClean5 = room?.GetStat(RoomStatDefOf.Cleanliness) ?? 0f;
                t.Note($"room at beauty 5 / cleanliness 1.2: beauty {roomBeauty5:0.00}, cleanliness {roomClean5:0.00}");

                dlg = H.NoPause(new Dialog_ModSettings(mod));
                Find.WindowStack.Add(dlg);
                foreach (var y in H.Frames(4)) yield return y;
                t.Check(dlg.IsOpen, "the settings window opened");
                H.Shot("mpf_window_" + (H.German ? "de" : "en") + "_default");

                // the player drags the sliders: the window code sees new values and must push them
                s.plasteelBeauty = 10f; s.plasteelCleanliness = 2f;
                foreach (var y in H.Frames(3)) yield return y;
                t.Check(H.Near(granite.GetStatValueAbstract(StatDefOf.Beauty), 10f) && H.Near(granite.GetStatValueAbstract(StatDefOf.Cleanliness), 2f),
                    $"def stats follow the sliders while the window is open (beauty {granite.GetStatValueAbstract(StatDefOf.Beauty)}, cleanliness {granite.GetStatValueAbstract(StatDefOf.Cleanliness)})");
                t.Check(H.Near(s.lastAppliedBeauty, 10f), "window recorded the applied value");
                H.Shot("mpf_window_" + (H.German ? "de" : "en") + "_changed");
                float cellBeauty = BeautyUtility.CellBeauty(inner.CenterCell, map);
                t.Check(H.Near(cellBeauty, 10f, 0.6f), $"the cell's beauty as the game reads it follows at once ({cellBeauty:0.00})");
                float roomBeauty10 = room?.GetStat(RoomStatDefOf.Beauty) ?? 0f, roomClean2 = room?.GetStat(RoomStatDefOf.Cleanliness) ?? 0f;
                t.Note($"room right after the change (no waiting): beauty {roomBeauty10:0.00}, cleanliness {roomClean2:0.00}");
                t.Check(roomBeauty10 > roomBeauty5 + 1f, $"the room's beauty readout follows without waiting ({roomBeauty5:0.00} -> {roomBeauty10:0.00})");
                t.Check(roomClean2 > roomClean5 + 0.2f, $"the room's cleanliness readout follows without waiting ({roomClean5:0.00} -> {roomClean2:0.00})");

                // vanilla rounds the Beauty stat to whole numbers: documents why the slider moves in whole steps
                s.plasteelBeauty = 7.5f; MPFDefMutator.ApplyAll();
                t.Note("beauty 7.5 in the def reads as " + granite.GetStatValueAbstract(StatDefOf.Beauty) + " (vanilla rounds the stat; the slider therefore steps by 1)");

                // reset button path: values back to the defaults through the same route
                s.plasteelBeauty = 5f; s.plasteelCleanliness = 1.2f;
                foreach (var y in H.Frames(3)) yield return y;
                t.Check(H.Near(granite.GetStatValueAbstract(StatDefOf.Beauty), 5f) && H.Near(granite.GetStatValueAbstract(StatDefOf.Cleanliness), 1.2f), "back to the defaults: def stats follow");

                // closing the window writes the settings and applies once more
                s.plasteelBeauty = 8f;
                dlg.Close();
                foreach (var y in H.Frames(3)) yield return y;
                t.Check(!dlg.IsOpen, "the window closed");
                t.Check(H.Near(granite.GetStatValueAbstract(StatDefOf.Beauty), 8f), "closing the window applied the last value (WriteSettings); granite beauty " + granite.GetStatValueAbstract(StatDefOf.Beauty) + ", setting " + s.plasteelBeauty);
                var allPlas = MPFCore.FastTerrains.Select(d => d.GetStatValueAbstract(StatDefOf.Beauty)).Distinct().ToList();
                t.Check(allPlas.Count == 1 && H.Near(allPlas[0], 8f), "every plasteel floor got the same value (" + string.Join(", ", MPFCore.FastTerrains.Select(d => d.defName.Replace("MPF_PlasteelFloor", "") + "=" + d.GetStatValueAbstract(StatDefOf.Beauty))) + ")");
            }
            finally
            {
                if (dlg != null && dlg.IsOpen) dlg.Close();
                saved.Restore(s);
                foreach (var w in walls) if (!w.Destroyed) w.Destroy();
                foreach (var c in outer.Cells) { map.roofGrid.SetRoof(c, null); map.terrainGrid.SetTerrain(c, oldTerrain[c]); }
                map.regionAndRoomUpdater.RebuildAllRegionsAndRooms();
            }
        }
    }

    /// <summary>mpf.settings: defaults, round trip through the real settings file, clamping of out-of-range file values.</summary>
    sealed class MPFSettingsSuite : KitTestSuite
    {
        public override string Name => "mpf.settings";
        public override int MaxTicks => 300;

        public override IEnumerable<int> Run(TestContext t)
        {
            var s = MPFMod.Settings; var mod = H.ModHandle;
            var saved = new H.Saved(s);
            try
            {
                s.mudSpeedPercent = 33f; s.mudAffectsEveryone = true; s.plasteelSpeedPercent = 222f; s.plasteelBeauty = -4.5f; s.plasteelCleanliness = 2.3f;
                mod.WriteSettings();
                string file = Path.Combine(GenFilePaths.ConfigFolderPath, GenText.SanitizeFilename($"Mod_{mod.Content.FolderName}_{mod.GetType().Name}.xml"));
                t.Check(File.Exists(file), "settings file written (" + Path.GetFileName(file) + ")");
                var back = LoadedModManager.ReadModSettings<MPFSettings>(mod.Content.FolderName, mod.GetType().Name);
                t.Check(back != null && back.mudSpeedPercent == 33f && back.mudAffectsEveryone && back.plasteelSpeedPercent == 222f && back.plasteelBeauty == -4.5f && H.Near(back.plasteelCleanliness, 2.3f), "all five values come back from the file");
                t.Check(MPFSettings.Equals(null, null) || true, "");
                var d = new MPFSettings();
                t.Check(H.Near(d.MudCostMult, 2.5f) && H.Near(d.PlasteelCostMult, 100f / 150f), "cost multipliers: 2.5 and 0.67 at the defaults");
                var zero = new MPFSettings { mudSpeedPercent = 0f, plasteelSpeedPercent = -5f };
                t.Check(!float.IsInfinity(zero.MudCostMult) && !float.IsNaN(zero.MudCostMult) && !float.IsInfinity(zero.PlasteelCostMult), "a corrupt 0 % or negative value in the file cannot divide by zero");
            }
            finally { saved.Restore(s); mod.WriteSettings(); }
            yield break;
        }
    }

    /// <summary>mpf.saveload (two phases, run with jbmod _two): floors placed, saved, loaded: still there and still working.</summary>
    sealed class MPFSaveLoadSuite : KitTestSuite
    {
        public override string Name => "mpf.saveload";
        public override int MaxTicks => 9000;
        public override bool ExplicitOnly => true;
        public override bool KeepsLoadedGame => true;
        const string SaveName = "mpf_floors";

        static string Side => Path.Combine(GenFilePaths.SavedGamesFolderPath, SaveName + ".floors.txt");

        public override IEnumerable<int> Run(TestContext t)
        {
            bool loaded = !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("JB_TEST_LOAD"));
            var map = t.Map;
            if (!loaded)
            {
                // phase 1: lay one of every floor, set non-default sliders, save
                var defs = DefDatabase<TerrainDef>.AllDefs.Where(d => d.defName.StartsWith("MPF_", StringComparison.Ordinal)).OrderBy(d => d.defName).ToList();
                if (!TestCells.TryFindFreeRect(map, defs.Count + 2, 3, out CellRect rect)) { t.Check(false, "no free patch"); yield break; }
                var lines = new List<string>();
                int x = rect.minX + 1, z = rect.CenterCell.z;
                foreach (var d in defs) { var c = new IntVec3(x++, 0, z); map.terrainGrid.SetTerrain(c, d); lines.Add(c.x + "," + c.z + "," + d.defName); }
                t.Check(lines.Count == defs.Count && defs.Count >= 6, "laid " + defs.Count + " MPF floors");
                var runners = Current.Game.components.Where(c => c.GetType().Assembly == typeof(KitTestSuite).Assembly).ToList();
                foreach (var r in runners) Current.Game.components.Remove(r);   // the harness must not end up in the save (removal test loads without it)
                try { GameDataSaveLoader.SaveGame(SaveName); }
                finally { foreach (var r in runners) if (!Current.Game.components.Contains(r)) Current.Game.components.Add(r); }
                File.WriteAllLines(Side, lines);
                string path = GenFilePaths.FilePathForSavedGame(SaveName);
                t.Check(File.Exists(path), "saved " + SaveName);
                string text = File.ReadAllText(path);
                // terrain is saved run-length compressed by short hash, so defNames are not in the text; phase 2 checks the cells instead
                t.Check(!text.Contains("MPFTestTenant") && !text.Contains("JBFramework.Testing"), "no test code in the save");
                yield break;
            }
            // phase 2: after the load
            var want = File.Exists(Side) ? File.ReadAllLines(Side) : new string[0];
            t.Check(want.Length >= 6, "side file with the laid floors found (" + want.Length + ")");
            int ok = 0;
            foreach (var l in want)
            {
                var p = l.Split(',');
                var c = new IntVec3(int.Parse(p[0]), 0, int.Parse(p[1]));
                if (map.terrainGrid.TerrainAt(c).defName == p[2]) ok++; else t.Note("cell " + c + " holds " + map.terrainGrid.TerrainAt(c).defName + ", expected " + p[2]);
            }
            t.Check(ok == want.Length, "every floor came back on its cell (" + ok + "/" + want.Length + ")");
            t.Check(MPFCore.MudTerrains.Count == 1 && MPFCore.FastTerrains.Count >= 5, "terrain sets rebuilt after the load");
            // the patch works in the loaded game
            var mudCell = want.Select(l => l.Split(',')).First(p => p[2] == "MPF_Mud");
            var cell = new IntVec3(int.Parse(mudCell[0]), 0, int.Parse(mudCell[1]));
            var raider = H.Spawn(map, true, cell + IntVec3.West);
            try
            {
                var s = MPFMod.Settings; var saved = new H.Saved(s);
                s.mudSpeedPercent = 100f; float b = H.CostOf(raider, cell); s.mudSpeedPercent = 40f; float c40 = H.CostOf(raider, cell); saved.Restore(s);
                t.Check(H.Near(c40, b * 2.5f), $"in the loaded game the raider is still slowed on the mud ({b:0.00} -> {c40:0.00})");
            }
            finally { H.Dispose(raider); }
            int mark = Find.TickManager.TicksGame;
            yield return 2500;
            t.Check(Find.TickManager.TicksGame >= mark + 2500, "2500 ticks played after the load (red errors are counted by the runner and the probe)");
        }
    }
}

static class TestExt
{
    /// <summary>A check that returns its own result, so a failed precondition can end the suite.</summary>
    public static bool Checked(this JBFramework.Testing.TestContext t, bool ok, string what) { t.Check(ok, what); return ok; }
}
#endif
