using System.Collections.Generic;
using UnityEngine;

namespace Game.World.Generation
{
    /// <summary>
    /// Stage 4 (section 3.7): old trails linking all the lamps (a minimum spanning tree plus a couple of extra
    /// loops), wandering a little and petering out in stretches; a trodden ring round each lamp; the cabin and
    /// shacks set beside the trails, their front doors facing them; and short paths up to every structure.
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

            // The cabin and the shacks, along the lamp trails.
            var lampTrails = new List<Trail>(plan.Trails);
            for (int i = 0; i < s.shacks; i++)
            {
                var kind = i == 0 ? StructureKind.Cabin : StructureKind.Shack;
                // (Less fussy each pass if there's no room, so they're always all there.)
                for (int pass = 0; pass < 4; pass++)
                    if (PlaceBesideTrail(plan, s, rng, lampTrails, kind, pass)) break;
            }

            // Each lamp on a patch of trodden earth, its trails running out of it.
            foreach (var lamp in lamps) plan.Patches.Add(new DirtPatch { Centre = lamp.Position, Radius = s.lampPatchRadius, Seed = rng.Range(0, 100000) });

            foreach (var st in plan.Structures)
            {
                if (st.Kind == StructureKind.Shack || st.Kind == StructureKind.Cabin) continue; // (they have theirs)
                if (!NearestTrailPoint(plan, st.Position, out var to) || plan.Distance(st.Position, to) > 60f) continue;
                Vector2 toward = Wrap.Delta(st.Position, to, plan.Size).normalized;
                Vector2 from = st.Position + toward * (st.HalfSize.magnitude + 0.5f);
                if (st.Kind == StructureKind.Bunker)
                {
                    // Its doorway (local +z) faces the trail, and the path runs from it.
                    st.Yaw = Mathf.Atan2(toward.x, toward.y) * Mathf.Rad2Deg;
                    from = st.Position + toward * (st.HalfSize.y + 0.5f);
                }
                plan.Trails.Add(MakeTrail(plan, from, to, s, rng));
            }
        }

        // A building beside a trail, its front (local +z, where the door is) facing it, a couple of metres off.
        static bool PlaceBesideTrail(WorldPlan plan, WorldGenSettings s, Rng rng, List<Trail> trails, StructureKind kind, int pass)
        {
            if (trails.Count == 0) return false;
            float spacing = pass == 0 ? 1f : pass == 1 ? 0.6f : pass == 2 ? 0.35f : 0.2f;
            float slope = pass == 0 ? 0.22f : pass == 1 ? 0.3f : 0.6f;
            var half = LayoutStage.HalfSize(kind);
            float reach = half.magnitude;
            for (int attempt = 0; attempt < 4000; attempt++)
            {
                var trail = trails[rng.Range(0, trails.Count)];
                int n = trail.Points.Count;
                if (n < 12) continue;
                int i = rng.Range(n / 6, n - n / 6); // (not right by the lamps at its ends)
                Vector2 at = trail.Points[i];
                Vector2 along = Wrap.Delta(trail.Points[i - 1], trail.Points[i + 1], plan.Size);
                if (along.sqrMagnitude < 0.0001f) continue;
                along = along.normalized;
                Vector2 side = new Vector2(-along.y, along.x) * (rng.Chance(0.5f) ? 1f : -1f);
                Vector2 centre = Wrap.Into(at + side * (half.y + s.trailWidth * 0.5f + 2.5f), plan.Size);
                Vector2 face = -side; // (toward the trail)
                float yaw = Mathf.Atan2(face.x, face.y) * Mathf.Rad2Deg;

                if (plan.Distance(centre, plan.Start) < s.startClearing + reach + 6f) continue;
                if (pass < 3 && !LayoutStage.GoodGround(plan, centre, reach + 3f, slope)) continue;
                if (plan.InWater(centre)) continue;
                bool clash = false;
                foreach (var l in plan.Lamps)
                    if (plan.Distance(centre, l.Position) < 12f * Mathf.Max(spacing, 0.6f) + reach) { clash = true; break; }
                foreach (var o in plan.Structures)
                {
                    bool same = o.Kind == StructureKind.Shack || o.Kind == StructureKind.Cabin;
                    float need = same ? Mathf.Max(s.shacksApart * spacing, 15f) : 15f * spacing + reach + o.HalfSize.magnitude;
                    if (plan.Distance(centre, o.Position) < need) { clash = true; break; }
                }
                if (clash || plan.Distance(centre, plan.Door) < 20f) continue;
                var site = new StructureSite { Kind = kind, Position = centre, Yaw = yaw, HalfSize = half };
                // No trail running through it.
                foreach (var t in plan.Trails)
                {
                    foreach (var q in t.Points) if (site.Contains(q, plan.Size, 0.8f)) { clash = true; break; }
                    if (clash) break;
                }
                if (clash) continue;

                plan.Structures.Add(site);
                plan.Clearings.Add(new Clearing
                {
                    Kind = ClearingKind.Structure, Centre = centre, Radius = reach + 2f,
                    Ragged = 0.2f, Thinning = 0.3f, Seed = rng.Range(0, 100000),
                });
                float big = Mathf.Max(half.x, half.y);
                LayoutStage.Level(plan, centre, yaw, new Vector2(big, big), 1.5f, 5f);
                // A short path from the door to the trail.
                Vector2 door = centre + face * (half.y + 0.3f);
                plan.Trails.Add(MakeTrail(plan, door, at, s, rng));
                return true;
            }
            return false;
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
