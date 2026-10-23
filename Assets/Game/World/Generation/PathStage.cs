using System.Collections.Generic;
using UnityEngine;

namespace Game.World.Generation
{
    /// <summary>
    /// Stage 4 (section 3.7): old trails linking all the lamps (a minimum spanning tree plus a couple of extra
    /// loops), wandering a little and petering out in stretches; a trodden ring round each lamp; and short
    /// paths up to the structures.
    /// </summary>
    public sealed class PathStage : IWorldStage
    {
        public string Name => "Paths";

        public void Run(WorldPlan plan, WorldGenSettings s, Rng rng)
        {
            var lamps = plan.Lamps;
            int n = lamps.Count;
            if (n >= 2)
            {
                foreach (var (a, b) in Edges(plan, s.extraLoops))
                    plan.Trails.Add(MakeTrail(plan, lamps[a].Position, lamps[b].Position, s, rng));
            }

            foreach (var lamp in lamps) plan.Trails.Add(Ring(lamp.Position, 3.5f, s));

            foreach (var st in plan.Structures)
            {
                if (!NearestTrailPoint(plan, st.Position, out var to) || plan.Distance(st.Position, to) > 60f) continue;
                Vector2 from = st.Position + Wrap.Delta(st.Position, to, plan.Size).normalized * (st.HalfSize.magnitude + 0.5f);
                plan.Trails.Add(MakeTrail(plan, from, to, s, rng));
            }
        }

        // A minimum spanning tree over the lamps (Prim's, by wrapped distance), plus the shortest extra edges.
        static List<(int, int)> Edges(WorldPlan plan, int extra)
        {
            var lamps = plan.Lamps;
            int n = lamps.Count;
            var result = new List<(int, int)>();
            var inTree = new bool[n];
            inTree[0] = true;
            for (int added = 1; added < n; added++)
            {
                float best = float.MaxValue;
                int ba = -1, bb = -1;
                for (int a = 0; a < n; a++)
                {
                    if (!inTree[a]) continue;
                    for (int b = 0; b < n; b++)
                    {
                        if (inTree[b]) continue;
                        float d = plan.Distance(lamps[a].Position, lamps[b].Position);
                        if (d < best) { best = d; ba = a; bb = b; }
                    }
                }
                inTree[bb] = true;
                result.Add((ba, bb));
            }

            var candidates = new List<(float, int, int)>();
            for (int a = 0; a < n; a++)
            for (int b = a + 1; b < n; b++)
                if (!result.Contains((a, b)) && !result.Contains((b, a)))
                    candidates.Add((plan.Distance(lamps[a].Position, lamps[b].Position), a, b));
            candidates.Sort((x, y) => x.Item1.CompareTo(y.Item1));
            for (int i = 0; i < Mathf.Min(extra, candidates.Count); i++) result.Add((candidates[i].Item2, candidates[i].Item3));
            return result;
        }

        // A wandering line from a to b (the short way round), a point every metre, worn in stretches.
        static Trail MakeTrail(WorldPlan plan, Vector2 a, Vector2 b, WorldGenSettings s, Rng rng)
        {
            var trail = new Trail { Width = s.trailWidth };
            Vector2 d = Wrap.Delta(a, b, plan.Size);
            float length = d.magnitude;
            Vector2 side = length > 0.01f ? new Vector2(-d.y, d.x) / length : Vector2.zero;
            int steps = Mathf.Max(2, Mathf.CeilToInt(length));
            int seedA = rng.Range(0, 100000), seedB = rng.Range(0, 100000);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float along = t * length;
                float wander = (PeriodicNoise.Line(along, 14f, seedA) * 2f - 1f) * 4f * Mathf.Sin(t * Mathf.PI);
                trail.Points.Add(Wrap.Into(a + d * t + side * wander, plan.Size));
                trail.Worn.Add(PeriodicNoise.Line(along, 25f, seedB) < s.trailWorn);
            }
            return trail;
        }

        static Trail Ring(Vector2 centre, float radius, WorldGenSettings s)
        {
            var trail = new Trail { Width = s.trailWidth };
            int steps = Mathf.CeilToInt(radius * Mathf.PI * 2f);
            for (int i = 0; i <= steps; i++)
            {
                float a = i / (float)steps * Mathf.PI * 2f;
                trail.Points.Add(centre + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius);
                trail.Worn.Add(true);
            }
            return trail;
        }

        static bool NearestTrailPoint(WorldPlan plan, Vector2 p, out Vector2 nearest)
        {
            nearest = default;
            float best = float.MaxValue;
            foreach (var t in plan.Trails)
                foreach (var q in t.Points)
                {
                    float d = Wrap.SqrDistance(p, q, plan.Size);
                    if (d < best) { best = d; nearest = q; }
                }
            return best < float.MaxValue;
        }
    }
}
