using UnityEngine;

namespace Game.World.Generation
{
    /// <summary>
    /// The bunker's way down (in its own space: the doorway faces +Z, the floor at 0): through a doorway in the back
    /// of its room, a long concrete hallway running straight back, and at its end a staircase going down into the
    /// ground, into the dark. The ground's dug away under it all (see <see cref="Dig"/>), the bunker's mound over it.
    /// </summary>
    public static class BunkerPlan
    {
        /// <summary>The hallway's middle (across), its start (the room's back wall) and its end (the top of the stairs).</summary>
        public const float HallX = 0.75f, HallStart = -3.4f, HallLength = 8.5f;
        public const float HallEnd = HallStart - HallLength;
        /// <summary>Inside: half its width, its height.</summary>
        public const float HallHalf = 0.7f, HallHeight = 2.4f;
        /// <summary>The doorway through from the room: its width and height (wide enough you never catch your shoulders on it).</summary>
        public const float DoorWidth = 1.3f, DoorHeight = 2.2f;
        /// <summary>The stairs: each step's rise and run, and how many are built (the dark swallows the rest).</summary>
        public const float Rise = 0.18f, Run = 0.28f;
        public const int Steps = 26;
        public const float StairsEnd = HallEnd - Steps * Run;
        public const float Bottom = -Steps * Rise;
        /// <summary>
        /// How far from the bunker's middle its hallway, stairs and the mound over them can reach, whichever way it
        /// ends up facing (it's turned to its trail only later): nothing else is put within this (lamps and the lone
        /// door, a little further), so nothing stands in the way dug out for them.
        /// </summary>
        public const float Reach = -StairsEnd + 5f;

        /// <summary>
        /// Once everything's placed: nothing left standing in the way dug for the hallway and stairs (a rock, a log,
        /// a battery would be buried under the mound, or stand in the hallway).
        /// </summary>
        public static void ClearWay(WorldPlan plan)
        {
            foreach (var b in plan.Structures)
            {
                if (b.Kind != StructureKind.Bunker) continue;
                float a = b.Yaw * Mathf.Deg2Rad, cos = Mathf.Cos(a), sin = Mathf.Sin(a);
                bool In(Vector2 p)
                {
                    Vector2 d = Wrap.Delta(b.Position, p, plan.Size);
                    float lx = d.x * cos - d.y * sin, lz = d.x * sin + d.y * cos;
                    return Mathf.Abs(lx - HallX) < 2.6f && lz < HallStart + 0.5f && lz > StairsEnd - 2f;
                }
                plan.Trees.RemoveAll(s => In(s.Position));
                plan.Bushes.RemoveAll(s => In(s.Position));
                plan.LargeBushes.RemoveAll(s => In(s.Position));
                plan.Weeds.RemoveAll(s => In(s.Position));
                plan.Rocks.RemoveAll(s => In(s.Position));
                plan.Boulders.RemoveAll(s => In(s.Position));
                plan.Logs.RemoveAll(s => In(s.Position));
                plan.Grass.RemoveAll(s => In(s.Position));
                plan.TallGrass.RemoveAll(s => In(s.Position));
                plan.Batteries.RemoveAll(In);
            }
        }

        static Vector2 ToPlan(Vector2 centre, float yaw, float x, float z)
        {
            float a = yaw * Mathf.Deg2Rad, cos = Mathf.Cos(a), sin = Mathf.Sin(a);
            return new Vector2(centre.x + x * cos + z * sin, centre.y - x * sin + z * cos);
        }

        /// <summary>
        /// Once the bunker's turned: the ground flat at its floor down the length of the hallway and stairs (so none
        /// shows through their walls), dug out deep under the stairs (they go down into it), and a clearing over
        /// it all (no trees growing down through it).
        /// </summary>
        public static void Dig(WorldPlan plan, StructureSite bunker)
        {
            float floor = plan.Height.Sample(bunker.Position);
            float midZ = (HallStart + StairsEnd) * 0.5f, halfZ = (HallStart - StairsEnd) * 0.5f;
            Vector2 mid = ToPlan(bunker.Position, bunker.Yaw, HallX, midZ);
            LayoutStage.LevelTo(plan, mid, bunker.Yaw, new Vector2(2.2f, halfZ), 0.5f, 3f, floor); // (level with its floor; blended off before it reaches the room: the bunker stands where it stood)
            // The pit under the stairs: everything over their footprint (and a margin) well below their bottom.
            var h = plan.Height;
            float a = bunker.Yaw * Mathf.Deg2Rad, cos = Mathf.Cos(a), sin = Mathf.Sin(a);
            int r = Mathf.CeilToInt(halfZ + 4f);
            int cx = Mathf.RoundToInt(mid.x), cz = Mathf.RoundToInt(mid.y);
            for (int dz = -r; dz <= r; dz++)
            for (int dx = -r; dx <= r; dx++)
            {
                int gx = cx + dx, gz = cz + dz;
                Vector2 d = Wrap.Delta(bunker.Position, new Vector2(h.WorldX(gx), h.WorldX(gz)), plan.Size);
                float lx = d.x * cos - d.y * sin, lz = d.x * sin + d.y * cos; // (into the bunker's own frame)
                if (Mathf.Abs(lx - HallX) > 2.2f || lz > HallEnd + 0.6f || lz < StairsEnd - 1.6f) continue;
                h[gx, gz] = Mathf.Min(h[gx, gz], floor + Bottom - 3f);
            }
            float reach = halfZ + 3f;
            plan.Clearings.Add(new Clearing { Kind = ClearingKind.Structure, Centre = Wrap.Into(mid, plan.Size), Radius = reach, Ragged = 0.15f, Thinning = 0.25f, Seed = Mathf.RoundToInt(bunker.Position.x * 7f) });
        }
    }
}
