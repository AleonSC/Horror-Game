using System.Collections.Generic;
using UnityEngine;

namespace Game.World.Generation
{
    /// <summary>
    /// The last stage: puddles, once everything else is placed, so none ends up in a building, round a trunk or
    /// in a pond. A few bigger ones, then lots of little ones (half gathered round the bigger), only on nearly
    /// level ground and mostly in its hollows, where rain would gather. Trails are fine: puddles collect on them.
    /// </summary>
    public sealed class PuddleStage : IWorldStage
    {
        public string Name => "Puddles";

        /// <summary>How many puddle shapes there are (the builder draws one texture for each).</summary>
        public const int Shapes = 3;

        public void Run(WorldPlan plan, WorldGenSettings s, Rng rng)
        {
            plan.Puddles.Clear();
            var trunks = new SpatialGrid(plan.Size, 2f);
            foreach (var t in plan.Trees) if (!t.Removed) trunks.Add(t.Position);
            var placed = new SpatialGrid(plan.Size, 1f);

            var big = new List<Vector2>();
            for (int attempt = 0, made = 0; attempt < s.puddles * 40 && made < s.puddles; attempt++)
            {
                var p = rng.InsideSquare(plan.Size);
                if (Try(plan, rng, trunks, placed, p, rng.Range(0.7f, 1.5f), 0.07f)) { big.Add(p); made++; }
            }
            for (int attempt = 0, made = 0; attempt < s.smallPuddles * 40 && made < s.smallPuddles; attempt++)
            {
                Vector2 p;
                if (big.Count > 0 && rng.Chance(0.5f))
                {
                    float a = rng.Angle();
                    p = Wrap.Into(big[rng.Range(0, big.Count)] + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * rng.Range(1.5f, 4f), plan.Size);
                }
                else p = rng.InsideSquare(plan.Size);
                if (Try(plan, rng, trunks, placed, p, rng.Range(0.25f, 0.6f), 0.05f)) made++;
            }
        }

        static bool Try(WorldPlan plan, Rng rng, SpatialGrid trunks, SpatialGrid placed, Vector2 p, float radius, float maxRise)
        {
            if (plan.Distance(p, plan.Start) < 6f || plan.InStructure(p, radius + 1f)) return false;
            if (trunks.AnyWithin(p, radius + 0.5f) || placed.AnyWithin(p, 0.5f)) return false;
            foreach (var pond in plan.Ponds)
                if (plan.Distance(p, pond.Centre) < pond.Radius + 3f) return false;

            // Nearly level, or the water would run off; and mostly in a dip.
            var h = plan.Height;
            float lo = float.MaxValue, hi = float.MinValue, around = 0f;
            for (int k = 0; k < 8; k++)
            {
                float a = k / 8f * Mathf.PI * 2f;
                var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                float y = h.Sample(p + dir * radius);
                lo = Mathf.Min(lo, y);
                hi = Mathf.Max(hi, y);
                around += h.Sample(p + dir * radius * 3f) / 8f;
            }
            if (hi - lo > maxRise) return false;
            if (h.Sample(p) > around + 0.01f && !rng.Chance(0.2f)) return false;

            float stretch = rng.Range(0.75f, 1.3f);
            var half = new Vector2(radius * stretch, radius / stretch) * 1.8f;
            plan.Puddles.Add(new Puddle
            {
                Centre = p,
                Radius = Mathf.Min(half.x, half.y) * 0.5f,
                HalfSize = half,
                Angle = rng.Range(0f, 360f),
                Shape = rng.Range(0, Shapes),
            });
            placed.Add(p);
            return true;
        }
    }
}
