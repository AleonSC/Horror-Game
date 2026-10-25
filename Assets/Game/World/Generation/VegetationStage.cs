using Game.Core;
using UnityEngine;

namespace Game.World.Generation
{
    /// <summary>
    /// Stage 5 (section 3.6): the forest grown round the layout. Trees in patchy density (dead ones, the odd old
    /// giant with room round it, natural gaps), bushes, meadow grass and tufts at tree feet, and tall grass in the
    /// lamp clearings and glades, and grassy verges along the trails.
    /// </summary>
    public sealed class VegetationStage : IWorldStage
    {
        public string Name => "Vegetation";

        public void Run(WorldPlan plan, WorldGenSettings s, Rng rng)
        {
            var mask = new ForestMask(plan, s, rng.Range(0, 100000));
            var trees = PlantTrees(plan, s, rng.Fork(1), mask);
            PlantBushes(plan, s, rng.Fork(2), mask, trees);
            PlantGrass(plan, s, rng.Fork(3), mask);
            PlantTallGrass(plan, s, rng.Fork(4), mask);
            PlantVerges(plan, s, rng.Fork(5), mask);
            PlantGrassPatches(plan, s, rng.Fork(6), mask);
        }

        static SpatialGrid PlantTrees(WorldPlan plan, WorldGenSettings s, Rng rng, ForestMask mask)
        {
            var grid = new SpatialGrid(plan.Size, s.treeSpacing);
            var giants = new SpatialGrid(plan.Size, 5f);
            int planned = Mathf.RoundToInt(s.trees / Mathf.Max(0.01f, 1f - s.emptyShare));
            int spots = 0;
            for (int attempt = 0; attempt < planned * 60 && spots < planned; attempt++)
            {
                var p = rng.InsideSquare(plan.Size);
                if (!rng.Chance(mask.TreeDensity(p))) continue;
                if (grid.AnyWithin(p, s.treeSpacing) || giants.AnyWithin(p, 4.5f)) continue;
                grid.Add(p);
                spots++;
                if (rng.Chance(s.emptyShare)) continue; // a planned spot left empty: a natural gap

                bool dead = rng.Chance(s.deadShare);
                bool giant = !dead && rng.Chance(s.oldGiantShare) && grid.CountWithin(p, 4.5f) <= 1;
                if (giant) giants.Add(p);
                var kind = dead ? TreeKind.Dead : TreeKind.Pine;
                plan.Trees.Add(new TreeSpot
                {
                    Position = p,
                    Kind = kind,
                    Variant = (byte)rng.Range(0, TreeShapes.Count(kind, s)),
                    Scale = s.treeScale * rng.Range(0.8f, 1.2f) * (giant ? rng.Range(s.oldGiantScale.x, s.oldGiantScale.y) : 1f),
                    Yaw = rng.Range(0f, 360f),
                    OldGiant = giant,
                });
            }
            return grid;
        }

        static void PlantBushes(WorldPlan plan, WorldGenSettings s, Rng rng, ForestMask mask, SpatialGrid trees)
        {
            for (int attempt = 0; attempt < s.bushes * 20 && plan.Bushes.Count < s.bushes; attempt++)
            {
                var p = rng.InsideSquare(plan.Size);
                if (!mask.Free(p, 0.3f) || !rng.Chance(mask.ClearingFactor(p, true) * 0.9f + 0.05f)) continue;
                if (trees.AnyWithin(p, 0.8f)) continue;
                plan.Bushes.Add(new Spot
                {
                    Position = p,
                    Size = rng.Range(s.bushSize.x, s.bushSize.y),
                    Yaw = rng.Range(0f, 360f),
                    Kind = (byte)rng.Range(0, 3),
                    Variant = (byte)rng.Range(0, 4),
                });
            }
        }

        static void PlantGrass(WorldPlan plan, WorldGenSettings s, Rng rng, ForestMask mask)
        {
            int meadowSeed = rng.Range(0, 100000);
            for (int attempt = 0; attempt < s.grass * 12 && plan.Grass.Count < s.grass; attempt++)
            {
                var p = rng.InsideSquare(plan.Size);
                float meadow = PeriodicNoise.Fractal(p.x, p.y, plan.Size, s.meadowCells, meadowSeed);
                if (!rng.Chance(MathUtil.SmoothThreshold(0.42f, 0.65f, meadow)) || !mask.Free(p)) continue;
                plan.Grass.Add(new Spot { Position = p, Size = rng.Range(0.3f, 0.9f), Yaw = rng.Range(0f, 360f), Variant = (byte)rng.Range(0, 4) });
            }
            // Tufts round the foot of each tree.
            foreach (var t in plan.Trees)
            {
                int n = rng.Range(s.tuftsPerTree.x, s.tuftsPerTree.y + 1);
                for (int i = 0; i < n; i++)
                {
                    float a = rng.Angle(), r = rng.Range(0.35f, 1.1f) * Mathf.Max(1f, t.Scale);
                    var p = t.Position + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                    if (!mask.Free(p)) continue;
                    plan.Grass.Add(new Spot { Position = Wrap.Into(p, plan.Size), Size = rng.Range(0.25f, 0.6f), Yaw = rng.Range(0f, 360f), Variant = (byte)rng.Range(0, 4) });
                }
            }
        }

        // Grass lining both sides of the worn trails and the rims of the lamps' patches, thickest right at the edge,
        // so the trodden earth runs between grassy banks; a few taller clumps among it.
        static void PlantVerges(WorldPlan plan, WorldGenSettings s, Rng rng, ForestMask mask)
        {
            float half = s.trailWidth * 0.5f;
            void Verge(Vector2 at, Vector2 outward, float along, bool tall)
            {
                int count = Mathf.FloorToInt(s.trailVergeGrass * along + rng.Value());
                for (int i = 0; i < count; i++)
                {
                    float out_ = rng.Range(0f, 1f);
                    out_ *= out_; // (bunched at the edge)
                    var p = Wrap.Into(at + outward * (half + 0.05f + out_ * 1.1f) + new Vector2(rng.Range(-0.5f, 0.5f), rng.Range(-0.5f, 0.5f)) * along, plan.Size);
                    if (!mask.Free(p, 0.02f)) continue;
                    plan.Grass.Add(new Spot { Position = p, Size = rng.Range(0.45f, 1f), Yaw = rng.Range(0f, 360f), Variant = (byte)rng.Range(0, 4) });
                }
                if (tall && rng.Chance(s.trailVergeTallGrass * along))
                {
                    var p = Wrap.Into(at + outward * (half + rng.Range(0.4f, 1.4f)), plan.Size);
                    if (mask.Free(p, 0.3f) && !plan.InWater(p))
                        plan.TallGrass.Add(new Spot { Position = p, Size = rng.Range(s.tallGrassHeight.x, s.tallGrassHeight.y) * 0.85f, Yaw = rng.Range(0f, 360f), Variant = (byte)rng.Range(0, 4) });
                }
            }
            foreach (var trail in plan.Trails)
                for (int i = 0; i < trail.Points.Count - 1; i++)
                {
                    if (!trail.Worn[i] || !trail.Worn[i + 1]) continue;
                    Vector2 a = trail.Points[i];
                    Vector2 d = Wrap.Delta(a, trail.Points[i + 1], plan.Size);
                    float length = d.magnitude;
                    if (length < 0.01f) continue;
                    Vector2 side = new Vector2(-d.y, d.x) / length;
                    Vector2 mid = a + d * 0.5f;
                    Verge(mid, side, length, true);
                    Verge(mid, -side, length, true);
                }
            foreach (var patch in plan.Patches)
            {
                int around = Mathf.CeilToInt(patch.Radius * Mathf.PI * 2f);
                for (int i = 0; i < around; i++)
                {
                    float a = (i + rng.Value()) / around * Mathf.PI * 2f;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Verge(patch.Centre + dir * (patch.RadiusAt(a) - half), dir, 1f, false); // (no tall grass by the lamp)
                }
            }
        }

        // Patches of thick grass, ragged at the edges and thinning out, about the forest floor; most of them on the
        // trails, so a path runs through them with grass either side (none on the trodden earth itself).
        static void PlantGrassPatches(WorldPlan plan, WorldGenSettings s, Rng rng, ForestMask mask)
        {
            var onTrails = new System.Collections.Generic.List<Vector2>();
            foreach (var t in plan.Trails)
                for (int i = 0; i < t.Points.Count; i += 3)
                    if (t.Worn[i]) onTrails.Add(t.Points[i]);
            float small = Mathf.Min(s.grassPatchSize.x, s.grassPatchSize.y), big = Mathf.Max(s.grassPatchSize.x, s.grassPatchSize.y);
            for (int n = 0; n < s.grassPatches; n++)
            {
                Vector2 centre = onTrails.Count > 0 && rng.Chance(s.grassPatchesOnTrails)
                    ? onTrails[rng.Range(0, onTrails.Count)] + new Vector2(rng.Range(-1f, 1f), rng.Range(-1f, 1f))
                    : rng.InsideSquare(plan.Size);
                if (plan.InWater(centre) || plan.InStructure(centre, 1f)) continue;
                float radius = rng.Range(small, big);
                int seed = rng.Range(0, 100000);
                int count = Mathf.RoundToInt(Mathf.PI * radius * radius * s.grassPatchDensity * 0.6f);
                for (int i = 0; i < count; i++)
                {
                    float a = rng.Angle();
                    float edge = radius * (0.7f + 0.3f * PeriodicNoise.Around(a, 5, seed) * 2f); // (a ragged outline)
                    float d = Mathf.Sqrt(rng.Value()) * edge;
                    if (!rng.Chance(1f - MathUtil.SmoothThreshold(0.5f, 1f, d / edge) * 0.8f)) continue; // thinning to the edge
                    var p = Wrap.Into(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d, plan.Size);
                    if (!mask.Free(p, 0.02f)) continue;
                    plan.Grass.Add(new Spot { Position = p, Size = rng.Range(0.4f, 1f), Yaw = rng.Range(0f, 360f), Variant = (byte)rng.Range(0, 4) });
                }
            }
        }

        static void PlantTallGrass(WorldPlan plan, WorldGenSettings s, Rng rng, ForestMask mask)
        {
            foreach (var c in plan.Clearings)
            {
                bool lamp = c.Kind == ClearingKind.Lamp;
                if (!lamp && c.Kind != ClearingKind.Glade) continue;
                float inner = lamp ? s.lampTallGrassGap : 0f;
                float area = Mathf.PI * (c.Radius * c.Radius - inner * inner);
                int count = Mathf.RoundToInt(area * (lamp ? 0.35f : s.gladeGrassDensity));
                for (int i = 0; i < count; i++)
                {
                    float a = rng.Angle();
                    float d = Mathf.Sqrt(rng.Range(inner * inner, c.Radius * c.Radius));
                    if (!lamp && !rng.Chance(1f - MathUtil.SmoothThreshold(0.55f, 1f, d / c.Radius))) continue; // thinning to the glade's edge
                    var p = Wrap.Into(c.Centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d, plan.Size);
                    if (!mask.Free(p, 0.2f)) continue;
                    plan.TallGrass.Add(new Spot { Position = p, Size = rng.Range(s.tallGrassHeight.x, s.tallGrassHeight.y), Yaw = rng.Range(0f, 360f), Variant = (byte)rng.Range(0, 4) });
                }
            }
        }
    }
}
