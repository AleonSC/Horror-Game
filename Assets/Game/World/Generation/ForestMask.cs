using UnityEngine;

namespace Game.World.Generation
{
    /// <summary>
    /// Where things may grow, worked out once from the layout: open clearings, water, structures, trails and
    /// hilltops. Shared by the vegetation, props and pickups stages.
    /// </summary>
    public sealed class ForestMask
    {
        readonly WorldPlan plan;
        readonly SpatialGrid trailGrid, wornGrid;
        readonly float trailHalfWidth;
        readonly int patchSeed;
        readonly int patchCells;

        public ForestMask(WorldPlan plan, WorldGenSettings s, int seed)
        {
            this.plan = plan;
            trailHalfWidth = s.trailWidth * 0.5f;
            patchSeed = seed;
            patchCells = s.treePatchCells;
            trailGrid = new SpatialGrid(plan.Size, 2f);
            wornGrid = new SpatialGrid(plan.Size, 2f);
            foreach (var t in plan.Trails)
                for (int i = 0; i < t.Points.Count; i++)
                {
                    trailGrid.Add(t.Points[i]);
                    if (t.Worn[i]) wornGrid.Add(t.Points[i]);
                }
        }

        public bool OnTrail(Vector2 p, float margin = 0f) => trailGrid.AnyWithin(p, trailHalfWidth + margin);
        public bool OnWornTrail(Vector2 p, float margin = 0f) => wornGrid.AnyWithin(p, trailHalfWidth + margin);

        /// <summary>0 in the open middle of any clearing, 1 out in the full forest. Lenient: clearings' edges count as open less.</summary>
        public float ClearingFactor(Vector2 p, bool lenient = false)
        {
            float f = 1f;
            foreach (var c in plan.Clearings)
            {
                float reach = c.Radius * (1f + c.Ragged) * (1f + c.Thinning) + 1f;
                if (Wrap.SqrDistance(c.Centre, p, plan.Size) > reach * reach) continue;
                float t = c.TreeFactor(p, plan.Size);
                f = Mathf.Min(f, lenient ? Mathf.Sqrt(t) : t);
                if (f <= 0f) break;
            }
            return f;
        }

        /// <summary>The chance (0-1) a tree grows here.</summary>
        public float TreeDensity(Vector2 p)
        {
            if (plan.InWater(p) || plan.InStructure(p, 1f) || OnTrail(p, 0.6f)) return 0f;
            float f = ClearingFactor(p);
            if (f <= 0f) return 0f;
            float top = plan.HillTop(p);
            f *= 1f - 0.75f * top * top; // hilltops: few trees, lookouts over the canopy
            float patch = PeriodicNoise.Fractal(p.x, p.y, plan.Size, patchCells, patchSeed);
            return f * Mathf.Lerp(0.35f, 1f, Mathf.SmoothStep(0.3f, 0.7f, patch));
        }

        /// <summary>Is the ground here free for a plant or prop (not water, not a building, not a worn trail)?</summary>
        public bool Free(Vector2 p, float trailMargin = 0f) => !plan.InWater(p) && !plan.InStructure(p, 0.5f) && !OnWornTrail(p, trailMargin);
    }
}
