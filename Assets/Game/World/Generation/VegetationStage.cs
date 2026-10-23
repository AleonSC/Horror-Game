using UnityEngine;

namespace Game.World.Generation
{
    /// <summary>
    /// Stage 5 (section 3.6): the forest grown round the layout. Trees in patchy density (dead ones, the odd old
    /// giant with room round it, natural gaps), bushes, meadow grass and tufts at tree feet, and tall grass in the
    /// lamp clearings and glades.
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
                    Variant = (byte)rng.Range(0, kind == TreeKind.Pine ? TreeShapes.PineVariants : TreeShapes.DeadVariants),
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
                if (!rng.Chance(Mathf.SmoothStep(0.42f, 0.65f, meadow)) || !mask.Free(p)) continue;
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
                    if (!lamp && !rng.Chance(1f - Mathf.SmoothStep(0.55f, 1f, d / c.Radius))) continue; // thinning to the glade's edge
                    var p = Wrap.Into(c.Centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * d, plan.Size);
                    if (!mask.Free(p, 0.2f)) continue;
                    plan.TallGrass.Add(new Spot { Position = p, Size = rng.Range(s.tallGrassHeight.x, s.tallGrassHeight.y), Yaw = rng.Range(0f, 360f), Variant = (byte)rng.Range(0, 4) });
                }
            }
        }
    }
}
