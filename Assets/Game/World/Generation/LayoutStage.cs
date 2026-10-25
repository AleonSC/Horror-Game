using UnityEngine;

namespace Game.World.Generation
{
    /// <summary>
    /// Stage 2 (section 3.4): the places that matter, before any forest. The start clearing in the middle (the
    /// biggest; which way you face and the giant's bearing were set with the terrain), ten lamps with their clearings, the shacks,
    /// campsites, watchtower, car and landmark tree, the lone door on flat ground, and the glades.
    /// </summary>
    public sealed class LayoutStage : IWorldStage
    {
        public string Name => "Layout";

        public void Run(WorldPlan plan, WorldGenSettings s, Rng rng)
        {
            plan.Clearings.Add(new Clearing
            {
                Kind = ClearingKind.Start, Centre = plan.Start, Radius = s.startClearing,
                Ragged = 0.125f, Thinning = 0.4f, Seed = rng.Range(0, 100000),
            });

            PlaceLamps(plan, s, rng);
            PlaceStructures(plan, s, rng);
            PlaceDoor(plan, s, rng);
            PlaceGlades(plan, s, rng);

            // Level a pad under every building and the lone door, so their floors and doorways sit on the ground.
            foreach (var st in plan.Structures)
                if (st.Kind != StructureKind.LandmarkTree)
                {
                    float big = Mathf.Max(st.HalfSize.x, st.HalfSize.y); // (square: right whichever way it's turned)
                    Level(plan, st.Position, st.Yaw, new Vector2(big, big), 1.5f, 5f);
                }
            Level(plan, plan.Door, plan.DoorYaw, new Vector2(1.5f, 1.5f), 1f, 4f);
        }

        /// <summary>
        /// Flatten the ground over a box (turned by yaw) plus a margin, to the box's average height, blending back
        /// into the slope over <paramref name="blend"/> metres beyond that.
        /// </summary>
        public static void Level(WorldPlan plan, Vector2 centre, float yaw, Vector2 half, float margin, float blend)
        {
            var h = plan.Height;
            float a = yaw * Mathf.Deg2Rad, cos = Mathf.Cos(a), sin = Mathf.Sin(a);
            float reach = half.magnitude + margin + blend;
            int r = Mathf.CeilToInt(reach) + 1;
            int cx = Mathf.RoundToInt(centre.x), cz = Mathf.RoundToInt(centre.y);

            // The level: the average height over the box.
            float sum = 0f;
            int n = 0;
            for (float x = -half.x; x <= half.x; x += 1f)
            for (float z = -half.y; z <= half.y; z += 1f)
            {
                sum += h.Sample(centre.x + x * cos + z * sin, centre.y - x * sin + z * cos);
                n++;
            }
            float level = n > 0 ? sum / n : h.Sample(centre);

            for (int dz = -r; dz <= r; dz++)
            for (int dx = -r; dx <= r; dx++)
            {
                int gx = cx + dx, gz = cz + dz;
                Vector2 d = Wrap.Delta(centre, new Vector2(h.WorldX(gx), h.WorldX(gz)), plan.Size);
                // How far outside the box (with its margin) this sample is, in the box's own frame.
                float lx = Mathf.Abs(d.x * cos - d.y * sin) - half.x - margin;
                float lz = Mathf.Abs(d.x * sin + d.y * cos) - half.y - margin;
                float outside = new Vector2(Mathf.Max(lx, 0f), Mathf.Max(lz, 0f)).magnitude;
                float w = 1f - Mathf.SmoothStep(0f, 1f, outside / Mathf.Max(0.01f, blend));
                if (w <= 0f) continue;
                h[gx, gz] = Mathf.Lerp(h[gx, gz], level, w);
            }
        }

        public static bool GoodGround(WorldPlan plan, Vector2 p, float pondMargin, float maxSlope = 0.3f)
        {
            if (plan.Height.Slope(p.x, p.y) > maxSlope) return false;
            foreach (var pond in plan.Ponds)
                if (plan.Distance(p, pond.Centre) < pond.Radius + pondMargin) return false;
            return true;
        }

        static void PlaceLamps(WorldPlan plan, WorldGenSettings s, Rng rng)
        {
            for (int attempt = 0; attempt < 20000 && plan.Lamps.Count < s.lamps; attempt++)
            {
                var p = rng.InsideSquare(plan.Size);
                if (plan.Distance(p, plan.Start) < s.lampMinFromStart) continue;
                if (!GoodGround(plan, p, 5f)) continue;
                bool clash = false;
                foreach (var l in plan.Lamps)
                    if (plan.Distance(p, l.Position) < s.lampMinApart) { clash = true; break; }
                if (clash) continue;

                var clearing = new Clearing
                {
                    Kind = ClearingKind.Lamp, Centre = p, Radius = s.lampClearing,
                    Ragged = s.lampClearingRagged, Thinning = 0.3f, Seed = rng.Range(0, 100000),
                };
                plan.Clearings.Add(clearing);
                plan.Lamps.Add(new LampSite { Index = plan.Lamps.Count, Position = p, Clearing = clearing });
            }
        }

        public static Vector2 HalfSize(StructureKind kind)
        {
            switch (kind)
            {
                case StructureKind.Cabin: return new Vector2(5f, 4.5f);
                case StructureKind.Shack: return new Vector2(3f, 3.5f);
                case StructureKind.Campsite: return new Vector2(5f, 5f);
                case StructureKind.Watchtower: return new Vector2(3f, 3f);
                case StructureKind.Car: return new Vector2(2.5f, 3.5f);
                case StructureKind.Bunker: return new Vector2(5.5f, 6f);
                default: return new Vector2(4f, 4f); // the landmark tree
            }
        }

        static void PlaceStructures(WorldPlan plan, WorldGenSettings s, Rng rng)
        {
            // (The cabin and shacks come later, beside the trails: see PathStage.)
            for (int i = 0; i < s.campsites; i++) Place(plan, s, rng, StructureKind.Campsite, s.campsitesApart, StructureKind.Campsite, StructureKind.Campsite);
            for (int i = 0; i < s.watchtowers; i++) Place(plan, s, rng, StructureKind.Watchtower, s.watchtowersApart, StructureKind.Watchtower, StructureKind.Watchtower);
            Place(plan, s, rng, StructureKind.Car, 0f, StructureKind.Car, StructureKind.Car);
            for (int i = 0; i < s.bunkers; i++) Place(plan, s, rng, StructureKind.Bunker, 80f, StructureKind.Bunker, StructureKind.Bunker);
            if (s.landmarkTree) Place(plan, s, rng, StructureKind.LandmarkTree, 0f, StructureKind.LandmarkTree, StructureKind.LandmarkTree);
        }

        // One structure: clear of the start, at least 7 m from any lamp, apart from its own kind (and 15 m
        // from anything else), on reasonable ground. If nowhere will do, it tries again less fussily (closer to
        // the others, on steeper ground), so it's always there.
        static void Place(WorldPlan plan, WorldGenSettings s, Rng rng, StructureKind kind, float apart, StructureKind same1, StructureKind same2)
        {
            for (int pass = 0; pass < 4; pass++)
                if (TryPlace(plan, s, rng, kind, apart, same1, same2, pass)) return;
        }

        static bool TryPlace(WorldPlan plan, WorldGenSettings s, Rng rng, StructureKind kind, float apart, StructureKind same1, StructureKind same2, int pass)
        {
            var half = HalfSize(kind);
            float reach = half.magnitude;
            float spacing = pass == 0 ? 1f : pass == 1 ? 0.6f : pass == 2 ? 0.35f : 0.2f;
            float slope = pass == 0 ? 0.22f : pass == 1 ? 0.3f : 0.6f;
            for (int attempt = 0; attempt < 3000; attempt++)
            {
                var p = rng.InsideSquare(plan.Size);
                if (plan.Distance(p, plan.Start) < s.startClearing + reach + 8f) continue;
                if (pass < 3 && !GoodGround(plan, p, reach + 3f, slope)) continue;
                if (pass == 3 && plan.InWater(p)) continue;
                bool clash = false;
                foreach (var l in plan.Lamps)
                    if (plan.Distance(p, l.Position) < 7f + reach) { clash = true; break; }
                foreach (var o in plan.Structures)
                {
                    float need = (o.Kind == same1 || o.Kind == same2) ? Mathf.Max(apart * spacing, 15f) : (15f * spacing + reach + o.HalfSize.magnitude);
                    if (plan.Distance(p, o.Position) < need) { clash = true; break; }
                }
                if (clash) continue;

                plan.Structures.Add(new StructureSite { Kind = kind, Position = p, Yaw = rng.Range(0f, 360f), HalfSize = half });
                // (A watchtower stands in a small clearing of its own: you can see it from the trees round it.)
                bool tower = kind == StructureKind.Watchtower;
                plan.Clearings.Add(new Clearing
                {
                    Kind = ClearingKind.Structure, Centre = p, Radius = tower ? Mathf.Max(reach + 2f, s.watchtowerClearing) : reach + 2f,
                    Ragged = tower ? 0.25f : 0.2f, Thinning = tower ? 0.35f : 0.3f, Seed = rng.Range(0, 100000),
                });
                if (kind == StructureKind.Bunker) Level(plan, p, 0f, new Vector2(6.5f, 6.5f), 1f, 5f); // (square: it's turned to face its path later)
                return true;
            }
            return false;
        }

        // The lone door: far from the start, on the flattest ground found, clear of structures, and well inside
        // the map (its dark room is built far below it).
        static void PlaceDoor(WorldPlan plan, WorldGenSettings s, Rng rng)
        {
            float best = float.MaxValue;
            Vector2 bestSpot = plan.Start + new Vector2(s.doorMinFromStart, 0f);
            for (int attempt = 0; attempt < s.doorAttempts; attempt++)
            {
                var p = new Vector2(rng.Range(s.doorFromEdge, plan.Size - s.doorFromEdge), rng.Range(s.doorFromEdge, plan.Size - s.doorFromEdge));
                if (plan.Distance(p, plan.Start) < s.doorMinFromStart) continue;
                if (!GoodGround(plan, p, 8f)) continue;
                bool clash = false;
                foreach (var o in plan.Structures)
                    if (plan.Distance(p, o.Position) < s.doorFromStructures) { clash = true; break; }
                foreach (var l in plan.Lamps)
                    if (plan.Distance(p, l.Position) < 12f) { clash = true; break; }
                if (clash) continue;

                float roughness = 0f;
                for (int k = 0; k < 8; k++)
                {
                    float a = k / 8f * Mathf.PI * 2f;
                    roughness += Mathf.Abs(plan.Height.Sample(p.x + Mathf.Cos(a) * 3f, p.y + Mathf.Sin(a) * 3f) - plan.Height.Sample(p));
                }
                if (roughness < best)
                {
                    best = roughness;
                    bestSpot = p;
                }
            }
            plan.Door = bestSpot;
            plan.DoorYaw = rng.Range(0f, 360f);
            plan.Clearings.Add(new Clearing { Kind = ClearingKind.Structure, Centre = bestSpot, Radius = 5f, Ragged = 0.2f, Thinning = 0.3f, Seed = rng.Range(0, 100000) });
        }

        static void PlaceGlades(WorldPlan plan, WorldGenSettings s, Rng rng)
        {
            int placed = 0;
            for (int attempt = 0; attempt < 6000 && placed < s.glades; attempt++)
            {
                float r = rng.Range(s.gladeRadius.x, s.gladeRadius.y);
                var p = rng.InsideSquare(plan.Size);
                if (plan.Distance(p, plan.Start) < s.startClearing + r + 6f) continue;
                if (plan.HillTop(p) > 0.2f || !GoodGround(plan, p, r)) continue;
                bool clash = false;
                foreach (var c in plan.Clearings)
                {
                    if (c.Kind == ClearingKind.Start) continue;
                    float need = c.Kind == ClearingKind.Glade ? r + c.Radius + 10f : r + c.Radius + 4f;
                    if (plan.Distance(p, c.Centre) < need) { clash = true; break; }
                }
                if (clash) continue;
                plan.Clearings.Add(new Clearing { Kind = ClearingKind.Glade, Centre = p, Radius = r, Ragged = 0.3f, Thinning = 0.25f, Seed = rng.Range(0, 100000) });
                placed++;
            }
        }
    }
}
