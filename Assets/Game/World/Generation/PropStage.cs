using UnityEngine;

namespace Game.World.Generation
{
    /// <summary>Stage 6 (section 3.7): rocks sunk into the ground, half-buried boulders and fallen logs.</summary>
    public sealed class PropStage : IWorldStage
    {
        public string Name => "Props";

        public void Run(WorldPlan plan, WorldGenSettings s, Rng rng)
        {
            var mask = new ForestMask(plan, s, 0);
            var trunks = new SpatialGrid(plan.Size, 2f);
            foreach (var t in plan.Trees) if (!t.Removed) trunks.Add(t.Position);

            for (int attempt = 0; attempt < s.rocks * 20 && plan.Rocks.Count < s.rocks; attempt++)
            {
                var p = rng.InsideSquare(plan.Size);
                if (!mask.Free(p, 0.3f) || trunks.AnyWithin(p, 0.8f)) continue;
                float v = rng.Value();
                plan.Rocks.Add(new Spot { Position = p, Size = Mathf.Lerp(s.rockSize.x, s.rockSize.y, v * v), Yaw = rng.Range(0f, 360f), Variant = (byte)rng.Range(0, 6) });
            }

            for (int attempt = 0; attempt < s.boulders * 50 && plan.Boulders.Count < s.boulders; attempt++)
            {
                var p = rng.InsideSquare(plan.Size);
                float size = rng.Range(s.boulderSize.x, s.boulderSize.y);
                if (!mask.Free(p, size * 0.5f) || mask.OnTrail(p, size * 0.5f) || trunks.AnyWithin(p, size * 0.5f + 0.5f)) continue;
                if (mask.ClearingFactor(p) < 0.5f) continue; // not in the middle of a clearing
                bool nearLamp = false;
                foreach (var l in plan.Lamps) if (plan.Distance(p, l.Position) < 8f + size) { nearLamp = true; break; }
                if (nearLamp) continue;
                plan.Boulders.Add(new Spot { Position = p, Size = size, Yaw = rng.Range(0f, 360f), Variant = (byte)rng.Range(0, 6) });
            }

            for (int attempt = 0; attempt < s.logs * 30 && plan.Logs.Count < s.logs; attempt++)
            {
                var p = rng.InsideSquare(plan.Size);
                float length = rng.Range(3f, 7f);
                float yaw = rng.Range(0f, 360f);
                Vector2 along = SightlineStage.Direction(yaw) * length * 0.5f;
                if (!mask.Free(p) || !mask.Free(Wrap.Into(p + along, plan.Size)) || !mask.Free(Wrap.Into(p - along, plan.Size))) continue;
                if (mask.OnTrail(p, 1f) || mask.OnTrail(Wrap.Into(p + along, plan.Size), 0.5f) || mask.OnTrail(Wrap.Into(p - along, plan.Size), 0.5f)) continue;
                if (trunks.AnyWithin(p, 1f)) continue;
                plan.Logs.Add(new Spot { Position = p, Size = length, Yaw = yaw, Variant = (byte)rng.Range(0, 3) });
            }
        }
    }
}
