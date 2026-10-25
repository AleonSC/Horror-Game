using UnityEngine;

namespace Game.World.Generation
{
    /// <summary>
    /// Stage 1 (section 3.3): rolling ground from broad and small waves that tile across the map, hills (some
    /// stretched into ridges) kept away from the start, ponds pressed in as bowls, and puddles on flat ground.
    /// </summary>
    public sealed class TerrainStage : IWorldStage
    {
        public string Name => "Terrain";

        struct Wave { public int Kx, Kz; public float Amplitude, Phase; }

        public void Run(WorldPlan plan, WorldGenSettings s, Rng rng)
        {
            float size = plan.Size;
            plan.Start = new Vector2(size * 0.5f, size * 0.5f);
            // Which way you face when you wake, and the giant just off it: decided first, so no hill hides it.
            plan.StartYaw = rng.Range(0f, 360f);
            plan.GiantBearing = plan.StartYaw + rng.Range(-s.giantSpread, s.giantSpread);

            var waves = new Wave[s.broadWaves + s.smallWaves];
            for (int i = 0; i < waves.Length; i++)
            {
                bool broad = i < s.broadWaves;
                // Whole cycles across the map so the ground tiles; few cycles = long waves.
                int maxCycles = Mathf.Max(1, Mathf.FloorToInt(size / (broad ? s.broadMinWavelength : s.smallMinWavelength)));
                int kx, kz;
                do
                {
                    kx = rng.Range(-maxCycles, maxCycles + 1);
                    kz = rng.Range(-maxCycles, maxCycles + 1);
                } while ((kx == 0 && kz == 0) || kx * kx + kz * kz > maxCycles * maxCycles);
                waves[i] = new Wave { Kx = kx, Kz = kz, Amplitude = (broad ? s.broadAmplitude : s.smallAmplitude) * s.bumpiness, Phase = rng.Angle() };
            }

            PlaceHills(plan, s, rng);

            var h = plan.Height;
            int n = h.Resolution;
            // Hummocks: low mounds and dips everywhere (a seed of their own, so the rest of the plan's draws are
            // unchanged), with a finer, smaller lumpiness on them.
            int hummockSeed = plan.Seed * 91 + 7;
            int hummockCells = Mathf.Max(2, Mathf.RoundToInt(size / Mathf.Max(2f, s.hummockSize)));
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                float wx = h.WorldX(x), wz = h.WorldX(z);
                float y = 0f;
                foreach (var w in waves) y += w.Amplitude * Mathf.Sin((w.Kx * wx + w.Kz * wz) / size * Mathf.PI * 2f + w.Phase);
                foreach (var hill in plan.Hills) y += hill.Height * hill.Profile(new Vector2(wx, wz), size);
                if (s.hummocks > 0f)
                    y += (PeriodicNoise.Fractal(wx, wz, size, hummockCells, hummockSeed) - 0.5f) * 2f * s.hummocks
                       + (PeriodicNoise.Fractal(wx, wz, size, hummockCells * 3, hummockSeed + 1, 2) - 0.5f) * 0.6f * s.hummocks;
                h[x, z] = y;
            }

            PlacePonds(plan, s, rng);
            PlacePuddles(plan, s, rng);
        }

        static void PlaceHills(WorldPlan plan, WorldGenSettings s, Rng rng)
        {
            for (int i = 0; i < s.hills; i++)
            {
                for (int attempt = 0; attempt < 300; attempt++)
                {
                    float r = rng.Range(s.hillRadius.x, s.hillRadius.y);
                    var p = rng.InsideSquare(plan.Size);
                    if (plan.Distance(p, plan.Start) < s.startClearing + 1.3f * r) continue;
                    bool clash = false;
                    foreach (var other in plan.Hills)
                        if (plan.Distance(p, other.Centre) < 0.8f * (r + other.Radius)) { clash = true; break; }
                    if (clash) continue;

                    float a = rng.Angle();
                    var hill = new Hill
                    {
                        Centre = p,
                        Radius = r,
                        Height = rng.Range(s.hillHeight.x, s.hillHeight.y),
                        Axis = new Vector2(Mathf.Cos(a), Mathf.Sin(a)),
                        Stretch = rng.Chance(s.ridgeChance) ? rng.Range(s.ridgeStretch.x, s.ridgeStretch.y) : 1f,
                        Seed = rng.Range(0, 100000),
                    };
                    if (HidesGiant(plan, s, hill)) continue;
                    plan.Hills.Add(hill);
                    break;
                }
            }
        }

        // Would this hill rise into the view of the giant's feet from where you wake? (Allowing a metre or so
        // of the rolling waves on top.)
        static bool HidesGiant(WorldPlan plan, WorldGenSettings s, Hill hill)
        {
            Vector2 dir = SightlineStage.Direction(plan.GiantBearing);
            float limit = Mathf.Tan((s.giantLow - 1.5f) * Mathf.Deg2Rad);
            for (float r = 10f; r < plan.Size * 0.5f; r += 2f)
            {
                var p = Wrap.Into(plan.Start + dir * r, plan.Size);
                float rise = hill.Height * hill.Profile(p, plan.Size) + 1.5f - SightlineStage.EyeHeight;
                if (rise > r * limit) return true;
            }
            return false;
        }

        // Each pond is a smooth bowl: the water sits 0.1 m under the lowest point of its rim, and the middle is
        // at least pondDepth under the water.
        static void PlacePonds(WorldPlan plan, WorldGenSettings s, Rng rng)
        {
            var h = plan.Height;
            for (int i = 0; i < s.ponds; i++)
            {
                for (int attempt = 0; attempt < 300; attempt++)
                {
                    float r = rng.Range(s.pondRadius.x, s.pondRadius.y);
                    var c = rng.InsideSquare(plan.Size);
                    if (plan.Distance(c, plan.Start) < s.startClearing + r + 10f) continue;
                    if (plan.HillTop(c) > 0.05f) continue;
                    bool clash = false;
                    foreach (var other in plan.Ponds)
                        if (plan.Distance(c, other.Centre) < r + other.Radius + 20f) { clash = true; break; }
                    if (clash) continue;

                    float rim = float.MaxValue;
                    for (int k = 0; k < 32; k++)
                    {
                        float a = k / 32f * Mathf.PI * 2f;
                        rim = Mathf.Min(rim, h.Sample(c.x + Mathf.Cos(a) * r, c.y + Mathf.Sin(a) * r));
                    }
                    float water = rim - 0.1f;
                    float bottom = water - s.pondDepth - 0.1f;
                    int span = Mathf.CeilToInt(r) + 1;
                    int cx = Mathf.RoundToInt(c.x), cz = Mathf.RoundToInt(c.y);
                    for (int dz = -span; dz <= span; dz++)
                    for (int dx = -span; dx <= span; dx++)
                    {
                        var p = new Vector2(cx + dx, cz + dz);
                        float d = Wrap.Distance(c, p, plan.Size) / r;
                        if (d >= 1f) continue;
                        float bowl = Mathf.Lerp(bottom, rim + 0.05f, d * d); // a smooth bowl from the middle up to the rim
                        int gx = cx + dx, gz = cz + dz;
                        h[gx, gz] = Mathf.Min(h[gx, gz], bowl);
                    }
                    plan.Ponds.Add(new Pond { Centre = c, Radius = r, Water = water });
                    break;
                }
            }
        }

        static void PlacePuddles(WorldPlan plan, WorldGenSettings s, Rng rng)
        {
            for (int attempt = 0; attempt < s.puddles * 40 && plan.Puddles.Count < s.puddles; attempt++)
            {
                var p = rng.InsideSquare(plan.Size);
                if (plan.Height.Slope(p.x, p.y) > 0.04f || plan.InWater(p)) continue;
                if (plan.Distance(p, plan.Start) < 6f) continue;
                plan.Puddles.Add(p);
            }
        }
    }
}
