using System.Collections.Generic;
using Game.Core;
using Game.Rendering;
using UnityEngine;

namespace Game.World.Build
{
    /// <summary>
    /// The look of a shack or the cabin, built in, over the plain room you bump into (its walls, floor and door
    /// are <c>Placeholders.Room</c>'s, hidden). Front +Z, the door in the middle of it.
    /// <para>The shack: weathered grey planks with gaps between them and corner posts, a rusty corrugated tin roof
    /// on a low gable, a window each side (one boarded up), a plank door with its Z-brace, a step. Inside: a plank
    /// floor, a cot with a stained mattress and a blanket, a table and a chair, a shelf of jars and cans, a bucket,
    /// a barrel.</para>
    /// <para>The cabin: log walls, the logs crossing at the corners, a steep shingled roof, four windows, a porch
    /// across the front (its deck, posts, railings, steps and its own roof) with a rocking chair, a stove inside
    /// with its pipe up through the roof. Inside: a plank floor, a rug, a bed, a table with two chairs (one knocked
    /// over), a bookshelf, shelves of jars, a hanging lantern.</para>
    /// </summary>
    public sealed class HutModel
    {
        readonly List<Object> made;
        readonly Material stone;
        readonly Material plankA, plankB, plankC, log, logEnd, tin, shingle, iron, glass, fabric, blanket, rug, jar, paper;
        System.Random rng;

        public HutModel(List<Object> made, Material stone)
        {
            this.made = made;
            this.stone = stone;
            plankA = Keep(RuntimeMaterials.Lit("Planks (grey)", new Color(0.2f, 0.185f, 0.16f), 0.1f));
            plankB = Keep(RuntimeMaterials.Lit("Planks (brown)", new Color(0.17f, 0.12f, 0.085f), 0.12f));
            plankC = Keep(RuntimeMaterials.Lit("Planks (dark)", new Color(0.11f, 0.08f, 0.06f), 0.12f));
            log = Keep(RuntimeMaterials.Lit("Logs", new Color(0.19f, 0.13f, 0.085f), 0.15f));
            logEnd = Keep(RuntimeMaterials.Lit("Log ends", new Color(0.3f, 0.23f, 0.15f), 0.1f));
            tin = Keep(RuntimeMaterials.Lit("Rusty tin", new Color(0.2f, 0.12f, 0.08f), 0.35f));
            tin.SetFloat("_Metallic", 0.5f);
            shingle = Keep(RuntimeMaterials.Lit("Shingles", new Color(0.08f, 0.07f, 0.065f), 0.15f));
            iron = Keep(RuntimeMaterials.Lit("Iron", new Color(0.04f, 0.04f, 0.04f), 0.4f));
            iron.SetFloat("_Metallic", 0.7f);
            glass = Keep(RuntimeMaterials.Glass("Dirty glass", new Color(0.18f, 0.17f, 0.13f, 0.55f)));
            fabric = Keep(RuntimeMaterials.Lit("Mattress", new Color(0.32f, 0.29f, 0.22f), 0.1f));
            blanket = Keep(RuntimeMaterials.Lit("Blanket", new Color(0.22f, 0.05f, 0.04f), 0.1f));
            rug = Keep(RuntimeMaterials.Lit("Rug", new Color(0.2f, 0.09f, 0.05f), 0.05f));
            jar = Keep(RuntimeMaterials.Glass("Jars", new Color(0.3f, 0.28f, 0.18f, 0.7f)));
            paper = Keep(RuntimeMaterials.Lit("Paper", new Color(0.5f, 0.46f, 0.36f), 0.1f));
        }

        Material Keep(Material m)
        {
            made.Add(m);
            return m;
        }

        Material Plank => rng.Next(3) switch { 0 => plankA, 1 => plankB, _ => plankC };
        float R(float a, float b) => a + (b - a) * (float)rng.NextDouble();

        /// <param name="door">The door's hinge (its own planks go on it).</param>
        public void Build(Transform t, Transform door, Vector3 size, float doorWidth, float doorHeight, bool cabin, Vector3 tableSpot, System.Random random)
        {
            rng = random;
            float w = size.x, h = size.y, d = size.z;
            float hw = w * 0.5f, hd = d * 0.5f;
            // Windows: (wall: 0 left, 1 right, 2 front; where along it; boarded up?)
            // (The cabin's side windows light its back rooms; the front room has two each side of the door.)
            var windows = cabin
                ? new List<(int wall, float at, bool boarded)> { (0, -CabinBackRoom, false), (1, -CabinBackRoom, false), (2, -2.7f, false), (2, 2.7f, false) }
                : new List<(int wall, float at, bool boarded)> { (0, 0f, false), (1, 0f, true) };
            const float winBottom = 1.0f, winTop = 1.8f, winHalf = 0.45f;

            // Footings under the corners and along the sides.
            for (int i = 0; i < 6; i++)
            {
                float x = i % 2 == 0 ? -hw + 0.2f : hw - 0.2f, z = Mathf.Lerp(-hd + 0.2f, hd - 0.2f, (i / 2) / 2f);
                Box(t, "Footing", new Vector3(x, -0.05f, z), new Vector3(0.35f, 0.2f, 0.35f) * R(0.9f, 1.15f), stone);
            }
            // The floor: planks across.
            for (float z = -hd + 0.1f; z < hd; z += 0.2f)
                Box(t, "Floorboard", new Vector3(0f, 0.04f, z), new Vector3(w - 0.25f, 0.03f, 0.185f), Plank);

            // The walls.
            if (cabin) LogWalls(t, w, h, d, doorWidth, doorHeight, windows, winBottom, winTop, winHalf);
            else PlankWalls(t, w, h, d, doorWidth, doorHeight, windows, winBottom, winTop, winHalf);
            foreach (var win in windows) Window(t, WallPoint(win.wall, win.at, hw, hd), WallFacing(win.wall), winBottom, winTop, winHalf, win.boarded);
            // The doorway's frame, and the door itself (planks, its Z-brace, iron hinges, a latch).
            Box(t, "Door frame", new Vector3(-doorWidth * 0.5f - 0.05f, doorHeight * 0.5f, hd), new Vector3(0.1f, doorHeight, 0.18f), plankC);
            Box(t, "Door frame", new Vector3(doorWidth * 0.5f + 0.05f, doorHeight * 0.5f, hd), new Vector3(0.1f, doorHeight, 0.18f), plankC);
            Box(t, "Lintel", new Vector3(0f, doorHeight + 0.06f, hd), new Vector3(doorWidth + 0.3f, 0.12f, 0.2f), plankC);
            int planks = 5;
            for (int i = 0; i < planks; i++)
            {
                float x = (i + 0.5f) / planks * (doorWidth - 0.04f);
                Box(door, "Door plank", new Vector3(x, (doorHeight - 0.02f) * 0.5f, 0f), new Vector3((doorWidth - 0.04f) / planks - 0.01f, doorHeight - 0.02f, 0.05f), Plank);
            }
            foreach (float y in new[] { 0.35f, doorHeight - 0.35f })
            {
                Box(door, "Brace", new Vector3(doorWidth * 0.5f, y, -0.04f), new Vector3(doorWidth - 0.12f, 0.12f, 0.03f), plankC);
                Box(door, "Strap hinge", new Vector3(0.18f, y, 0.03f), new Vector3(0.36f, 0.05f, 0.01f), iron);
            }
            var brace = Box(door, "Brace", new Vector3(doorWidth * 0.5f, doorHeight * 0.5f, -0.04f), new Vector3(0.1f, doorHeight - 0.7f, 0.03f), plankC);
            brace.transform.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(doorWidth - 0.2f, doorHeight - 0.7f) * Mathf.Rad2Deg);
            Box(door, "Latch", new Vector3(doorWidth - 0.15f, 1.0f, 0.04f), new Vector3(0.12f, 0.03f, 0.02f), iron);
            Cyl(door, "Pull", new Vector3(doorWidth - 0.15f, 0.92f, 0.06f), 0.015f, 0.05f, iron, Vector3.forward);

            // The roof.
            float rise = cabin ? 1.7f : 1.0f, eave = 0.4f;
            Roof(t, w, h, d, rise, eave, cabin);

            // Outside the door: a porch (the cabin), or a step.
            if (cabin) Porch(t, w, h, hd, doorWidth);
            else Box(t, "Step", new Vector3(0f, 0.08f, hd + 0.3f), new Vector3(doorWidth + 0.3f, 0.16f, 0.45f), plankC);

            // Inside.
            Table(t, tableSpot, cabin);
            if (cabin) CabinRooms(t, hw, hd, h);
            else
            {
                ShackInside(t, hw, hd, h);
                ShackOutside(t, hw, hd, h, doorWidth, doorHeight);
            }
        }

        /// <summary>Where the cabin's back rooms' windows are (their middle, metres behind the cabin's middle).</summary>
        const float CabinBackRoom = 2.3f;
        /// <summary>The cabin's two doorways through to the back rooms (each this far either side of the middle), this wide.</summary>
        const float BackDoorAt = 2.3f, BackDoorWidth = 1.0f, BackDoorHeight = 2.1f;

        static Vector3 WallPoint(int wall, float at, float hw, float hd) => wall switch
        {
            0 => new Vector3(-hw, 0f, at),
            1 => new Vector3(hw, 0f, at),
            _ => new Vector3(at, 0f, hd),
        };

        static Vector3 WallFacing(int wall) => wall switch { 0 => Vector3.left, 1 => Vector3.right, _ => Vector3.forward };

        // Is this spot on a wall clear of the door and windows? (along: position along the wall; y: height)
        static bool Solid(int wall, float along, float y, float doorWidth, float doorHeight, List<(int wall, float at, bool boarded)> windows, float winBottom, float winTop, float winHalf)
        {
            if (wall == 2 && Mathf.Abs(along) < doorWidth * 0.5f + 0.02f && y < doorHeight + 0.02f) return false;
            foreach (var win in windows)
                if (win.wall == wall && Mathf.Abs(along - win.at) < winHalf + 0.02f && y > winBottom - 0.02f && y < winTop + 0.02f) return false;
            return true;
        }

        // The shack's walls: horizontal weathered planks (a gap between each) on corner posts, broken round the
        // door and windows.
        void PlankWalls(Transform t, float w, float h, float d, float doorWidth, float doorHeight, List<(int wall, float at, bool boarded)> windows, float winBottom, float winTop, float winHalf)
        {
            float hw = w * 0.5f, hd = d * 0.5f;
            const float board = 0.19f;
            for (int wall = 0; wall < 4; wall++)
            {
                float length = wall < 2 ? d : w;
                for (float y = 0.1f + board * 0.5f; y < h; y += board + 0.012f)
                {
                    // The wall in runs, split where the door or a window cuts it.
                    int w0 = wall;
                    float yb = y;
                    Runs(length, a => Solid(w0, a, yb, doorWidth, doorHeight, windows, winBottom, winTop, winHalf), (from, to) => Board(t, w0, from, to, yb, board, hw, hd));
                }
            }
            for (int c = 0; c < 4; c++)
                Box(t, "Corner post", new Vector3(c % 2 == 0 ? -hw : hw, h * 0.5f, c < 2 ? -hd : hd), new Vector3(0.14f, h, 0.14f), plankC);
        }

        // Walk along a wall (centred, this long) in small even steps, and lay a piece over each stretch that's
        // solid (between the openings). (Counted in whole steps, so the last stretch always reaches the end.)
        static void Runs(float length, System.Func<float, bool> solid, System.Action<float, float> lay)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(length / 0.05f));
            float step = length / steps, start = -length * 0.5f;
            bool inRun = false;
            for (int k = 0; k <= steps; k++)
            {
                float a = -length * 0.5f + k * step;
                bool here = k < steps && solid(a + step * 0.5f);
                if (here && !inRun) { start = a; inRun = true; }
                else if (!here && inRun)
                {
                    if (a - start > 0.06f) lay(start, a);
                    inRun = false;
                }
            }
        }

        void Board(Transform t, int wall, float from, float to, float y, float height, float hw, float hd)
        {
            float mid = (from + to) * 0.5f, len = to - from;
            float lean = R(-0.6f, 0.6f); // (warped a little)
            GameObject b = wall switch
            {
                0 => Box(t, "Board", new Vector3(-hw - 0.01f, y, mid), new Vector3(0.03f, height, len), Plank),
                1 => Box(t, "Board", new Vector3(hw + 0.01f, y, mid), new Vector3(0.03f, height, len), Plank),
                2 => Box(t, "Board", new Vector3(mid, y, hd + 0.01f), new Vector3(len, height, 0.03f), Plank),
                _ => Box(t, "Board", new Vector3(mid, y, -hd - 0.01f), new Vector3(len, height, 0.03f), Plank),
            };
            b.transform.localRotation = Quaternion.Euler(wall < 2 ? lean : 0f, 0f, wall >= 2 ? lean : 0f);
        }

        // The cabin's walls: stacked logs, crossing and sticking out at the corners, broken round the door and
        // windows (with a log over each opening).
        void LogWalls(Transform t, float w, float h, float d, float doorWidth, float doorHeight, List<(int wall, float at, bool boarded)> windows, float winBottom, float winTop, float winHalf)
        {
            float hw = w * 0.5f, hd = d * 0.5f;
            const float r = 0.13f;
            int row = 0;
            for (float y = r; y < h + 0.05f; y += r * 1.85f, row++)
            {
                for (int wall = 0; wall < 4; wall++)
                {
                    bool alongZ = wall < 2;
                    float length = (alongZ ? d : w) + 0.5f; // (out past the corners)
                    float yy = y + (alongZ == (row % 2 == 0) ? 0f : r * 0.92f) - r * 0.46f; // (side and end logs interleave)
                    int w0 = wall;
                    float half = alongZ ? hd : hw;
                    Runs(length, a => Mathf.Abs(a) > half - 0.05f || Solid(w0, a, yy, doorWidth, doorHeight, windows, winBottom, winTop, winHalf), (from, to) => Log(t, w0, from, to, yy, r, hw, hd));
                }
            }
        }

        void Log(Transform t, int wall, float from, float to, float y, float r, float hw, float hd)
        {
            float mid = (from + to) * 0.5f, len = to - from;
            float rr = r * R(0.9f, 1.08f);
            Vector3 at = wall switch
            {
                0 => new Vector3(-hw, y, mid),
                1 => new Vector3(hw, y, mid),
                2 => new Vector3(mid, y, hd),
                _ => new Vector3(mid, y, -hd),
            };
            Vector3 axis = wall < 2 ? Vector3.forward : Vector3.right;
            Cyl(t, "Log", at, rr, len, log, axis);
            // The cut ends, paler, where a log runs out past a corner.
            float half = wall < 2 ? hd : hw;
            if (to > half + 0.1f) Cyl(t, "Log end", at + axis * (len * 0.5f + 0.004f), rr * 0.96f, 0.01f, logEnd, axis);
            if (from < -half - 0.1f) Cyl(t, "Log end", at - axis * (len * 0.5f + 0.004f), rr * 0.96f, 0.01f, logEnd, axis);
        }

        // A window in a wall: its frame, a cross of glazing bars, dirty glass, a sill; or boarded over.
        void Window(Transform t, Vector3 wallAt, Vector3 facing, float bottom, float top, float half, bool boarded)
        {
            var win = new GameObject("Window").transform;
            win.SetParent(t, false);
            win.localPosition = wallAt;
            win.localRotation = Quaternion.LookRotation(facing);
            float mid = (bottom + top) * 0.5f, height = top - bottom;
            Box(win, "Frame", new Vector3(-half - 0.04f, mid, 0.02f), new Vector3(0.08f, height + 0.16f, 0.16f), plankC);
            Box(win, "Frame", new Vector3(half + 0.04f, mid, 0.02f), new Vector3(0.08f, height + 0.16f, 0.16f), plankC);
            Box(win, "Frame", new Vector3(0f, top + 0.04f, 0.02f), new Vector3(half * 2f + 0.16f, 0.08f, 0.16f), plankC);
            Box(win, "Sill", new Vector3(0f, bottom - 0.03f, 0.06f), new Vector3(half * 2f + 0.24f, 0.06f, 0.24f), plankC);
            var pane = Box(win, "Glass", new Vector3(0f, mid, 0f), new Vector3(half * 2f, height, 0.01f), glass);
            pane.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Box(win, "Glazing bar", new Vector3(0f, mid, 0.01f), new Vector3(0.03f, height, 0.03f), plankC);
            Box(win, "Glazing bar", new Vector3(0f, mid, 0.01f), new Vector3(half * 2f, 0.03f, 0.03f), plankC);
            if (!boarded) return;
            for (int i = 0; i < 3; i++)
            {
                var plank = Box(win, "Boarded up", new Vector3(R(-0.05f, 0.05f), bottom + 0.15f + i * 0.25f, 0.1f), new Vector3(half * 2f + 0.35f, 0.16f, 0.03f), Plank);
                plank.transform.localRotation = Quaternion.Euler(0f, 0f, R(-12f, 12f));
            }
        }

        // A gable roof, its ridge front to back: tin sheets in ribs (the shack) or rows of shingles (the cabin),
        // the triangles of the gable ends in planks, barge boards along the edges, a ridge cap.
        void Roof(Transform t, float w, float h, float d, float rise, float eave, bool cabin)
        {
            float hw = w * 0.5f + eave, len = d + eave * 2f;
            float slope = Mathf.Sqrt(hw * hw + rise * rise), angle = Mathf.Atan2(rise, hw) * Mathf.Rad2Deg;
            for (int side = -1; side <= 1; side += 2)
            {
                var plane = new GameObject("Roof").transform;
                plane.SetParent(t, false);
                plane.localPosition = new Vector3(side * hw * 0.5f, h + rise * 0.5f, 0f);
                plane.localRotation = Quaternion.Euler(0f, 0f, -side * angle);
                Box(plane, "Roof deck", Vector3.zero, new Vector3(slope, 0.05f, len), cabin ? plankC : tin);
                if (cabin)
                {
                    // Shingles: rows overlapping down the slope, each in a few uneven runs (the joins don't line up).
                    int rows = Mathf.CeilToInt(slope / 0.22f);
                    for (int r = 0; r < rows; r++)
                    {
                        float x = -side * (slope * 0.5f - 0.11f - r * 0.22f);
                        float z = -len * 0.5f;
                        while (z < len * 0.5f - 0.05f)
                        {
                            float run = Mathf.Min(R(1.2f, 2.6f), len * 0.5f - z);
                            var strip = Box(plane, "Shingles", new Vector3(x, 0.04f + R(0f, 0.008f), z + run * 0.5f), new Vector3(0.26f, 0.025f, run - 0.02f), shingle);
                            strip.transform.localRotation = Quaternion.Euler(0f, 0f, side * 4f);
                            z += run;
                        }
                    }
                }
                else
                {
                    // Tin: ribs running down the slope, rust streaks.
                    for (float z = -len * 0.5f + 0.1f; z < len * 0.5f; z += 0.16f)
                        Box(plane, "Rib", new Vector3(0f, 0.035f, z), new Vector3(slope, 0.02f, 0.03f), tin);
                }
                // Barge boards along the front and back edges.
                foreach (float z in new[] { len * 0.5f, -len * 0.5f })
                    Box(plane, "Barge board", new Vector3(0f, -0.02f, z), new Vector3(slope, 0.16f, 0.04f), plankC);
            }
            Box(t, "Ridge cap", new Vector3(0f, h + rise + 0.03f, 0f), new Vector3(0.2f, 0.06f, len + 0.02f), cabin ? plankC : tin);
            // The gable ends: triangles of vertical planks (front and back).
            foreach (float z in new[] { d * 0.5f, -d * 0.5f })
            {
                float halfW = w * 0.5f;
                for (float x = -halfW + 0.1f; x < halfW; x += 0.2f)
                {
                    float top = rise * (1f - Mathf.Abs(x) / halfW);
                    if (top < 0.05f) continue;
                    Box(t, "Gable board", new Vector3(x, h + top * 0.5f, z + Mathf.Sign(z) * 0.015f), new Vector3(0.19f, top, 0.03f), Plank);
                }
            }
        }

        // The cabin's porch: a deck across the front on posts, steps down, railings either side of the steps, its
        // own shallow roof, and a rocking chair.
        void Porch(Transform t, float w, float h, float hd, float doorWidth)
        {
            const float depth = 1.8f;
            float hw = w * 0.5f, z0 = hd, z1 = hd + depth;
            for (float x = -hw + 0.1f; x < hw; x += 0.2f)
                Box(t, "Deck board", new Vector3(x, 0.24f, (z0 + z1) * 0.5f), new Vector3(0.185f, 0.04f, depth), Plank);
            Box(t, "Deck", new Vector3(0f, 0.12f, (z0 + z1) * 0.5f), new Vector3(w, 0.22f, depth), plankC, true);
            for (int i = 0; i < 2; i++) // (steps)
                Box(t, "Step", new Vector3(0f, 0.06f + i * 0.08f, z1 + 0.35f - i * 0.25f), new Vector3(doorWidth + 0.4f, 0.12f + i * 0.08f, 0.3f), plankC, true);
            float postTop = h - 0.65f; // (under the porch roof)
            foreach (float x in new[] { -hw + 0.12f, -doorWidth * 0.5f - 0.3f, doorWidth * 0.5f + 0.3f, hw - 0.12f })
            {
                Box(t, "Porch post", new Vector3(x, postTop * 0.5f + 0.2f, z1 - 0.1f), new Vector3(0.14f, postTop, 0.14f), plankC, true);
                // (a little bracket under the beam)
                var bracket = Box(t, "Bracket", new Vector3(x + (x < 0 ? 0.12f : -0.12f), postTop - 0.05f, z1 - 0.1f), new Vector3(0.3f, 0.06f, 0.06f), plankC);
                bracket.transform.localRotation = Quaternion.Euler(0f, 0f, x < 0 ? -40f : 40f);
            }
            Box(t, "Porch beam", new Vector3(0f, postTop + 0.12f, z1 - 0.1f), new Vector3(w, 0.16f, 0.16f), plankC);
            // Railings (either side of the steps): a top rail and balusters.
            for (int side = -1; side <= 1; side += 2)
            {
                float a = side * (doorWidth * 0.5f + 0.3f), b = side * (hw - 0.12f);
                float mid = (a + b) * 0.5f, len = Mathf.Abs(b - a);
                Box(t, "Rail", new Vector3(mid, 1.15f, z1 - 0.1f), new Vector3(len, 0.07f, 0.09f), plankC);
                Box(t, "Rail", new Vector3(mid, 0.4f, z1 - 0.1f), new Vector3(len, 0.05f, 0.07f), plankC);
                for (float x = Mathf.Min(a, b) + 0.15f; x < Mathf.Max(a, b) - 0.05f; x += 0.15f)
                    Box(t, "Baluster", new Vector3(x, 0.77f, z1 - 0.1f), new Vector3(0.04f, 0.74f, 0.04f), Plank);
            }
            // Its roof: a shallow slope from the wall out over the posts.
            float drop = 0.45f, run = depth + 0.3f;
            var roof = new GameObject("Porch roof").transform;
            roof.SetParent(t, false);
            roof.localPosition = new Vector3(0f, h - 0.05f - drop * 0.5f, hd + run * 0.5f);
            roof.localRotation = Quaternion.Euler(Mathf.Atan2(drop, run) * Mathf.Rad2Deg, 0f, 0f);
            Box(roof, "Porch roof deck", Vector3.zero, new Vector3(w + 0.3f, 0.05f, Mathf.Sqrt(run * run + drop * drop)), plankC);
            for (float z = -run * 0.5f + 0.1f; z < run * 0.5f; z += 0.22f)
                Box(roof, "Shingle row", new Vector3(0f, 0.04f, z), new Vector3(w + 0.3f, 0.025f, 0.25f), shingle);
            // A rocking chair, a little turned.
            RockingChair(t, new Vector3(hw - 1.1f, 0.26f, (z0 + z1) * 0.5f), -150f);
        }

        void RockingChair(Transform t, Vector3 at, float yaw)
        {
            var c = new GameObject("Rocking chair").transform;
            c.SetParent(t, false);
            c.localPosition = at;
            c.localRotation = Quaternion.Euler(0f, yaw, 0f);
            for (int side = -1; side <= 1; side += 2)
            {
                // The rocker: a curve of short pieces.
                for (int i = 0; i < 6; i++)
                {
                    float a0 = -0.5f + i / 6f, a1 = -0.5f + (i + 1) / 6f;
                    Vector3 p0 = new Vector3(side * 0.23f, 0.05f + a0 * a0 * 0.35f, a0 * 0.8f), p1 = new Vector3(side * 0.23f, 0.05f + a1 * a1 * 0.35f, a1 * 0.8f);
                    Rod(c, p0, p1, 0.02f, plankC);
                }
                Rod(c, new Vector3(side * 0.23f, 0.06f, 0.2f), new Vector3(side * 0.23f, 0.62f, 0.2f), 0.02f, plankC);
                Rod(c, new Vector3(side * 0.23f, 0.06f, -0.2f), new Vector3(side * 0.23f, 1.05f, -0.28f), 0.022f, plankC);
                Rod(c, new Vector3(side * 0.23f, 0.62f, 0.25f), new Vector3(side * 0.23f, 0.62f, -0.22f), 0.02f, plankC); // (arm)
            }
            Box(c, "Seat", new Vector3(0f, 0.42f, 0f), new Vector3(0.46f, 0.04f, 0.42f), plankB);
            for (int i = 0; i < 5; i++) Rod(c, new Vector3(-0.16f + i * 0.08f, 0.44f, -0.21f), new Vector3(-0.16f + i * 0.08f, 1.0f, -0.27f), 0.012f, plankC);
            Box(c, "Top rail", new Vector3(0f, 1.02f, -0.27f), new Vector3(0.48f, 0.06f, 0.04f), plankC);
        }

        void Table(Transform t, Vector3 at, bool cabin)
        {
            // Planked top on a frame and four square legs; a chair or two.
            for (int i = 0; i < 5; i++)
                Box(t, "Table top", at + new Vector3(0f, 0.75f, -0.28f + i * 0.14f), new Vector3(1.2f, 0.04f, 0.13f), plankB, i == 2);
            Box(t, "Apron", at + new Vector3(0f, 0.68f, 0f), new Vector3(1.05f, 0.1f, 0.6f), plankC);
            for (int i = 0; i < 4; i++)
                Box(t, "Table leg", at + new Vector3(i % 2 == 0 ? -0.52f : 0.52f, 0.36f, i < 2 ? -0.28f : 0.28f), new Vector3(0.07f, 0.72f, 0.07f), plankC);
            Chair(t, at + new Vector3(0f, 0f, 0.6f), 180f + R(-15f, 15f), false);
            if (cabin) Chair(t, at + new Vector3(-0.9f, 0f, -0.1f), 75f, true);
            // The shack's lantern stands at the back corner of the table (click it on and off).
            else LanternModel.Standing(t, at + new Vector3(0.46f, 0.77f, -0.2f), R(0f, 360f), tin, iron, made);
        }

        void Chair(Transform t, Vector3 at, float yaw, bool fallen)
        {
            var c = new GameObject("Chair").transform;
            c.SetParent(t, false);
            c.localPosition = at + (fallen ? Vector3.up * 0.22f : Vector3.zero);
            c.localRotation = Quaternion.Euler(fallen ? 0f : 0f, yaw, fallen ? 90f : 0f); // (knocked over on its side)
            Box(c, "Seat", new Vector3(0f, 0.45f, 0f), new Vector3(0.42f, 0.04f, 0.42f), plankB);
            for (int i = 0; i < 4; i++)
                Box(c, "Leg", new Vector3(i % 2 == 0 ? -0.18f : 0.18f, 0.22f, i < 2 ? -0.18f : 0.18f), new Vector3(0.04f, 0.45f, 0.04f), plankC);
            for (int side = -1; side <= 1; side += 2)
                Box(c, "Back post", new Vector3(side * 0.18f, 0.72f, -0.19f), new Vector3(0.04f, 0.52f, 0.04f), plankC);
            for (int i = 0; i < 3; i++)
                Box(c, "Back slat", new Vector3(0f, 0.6f + i * 0.13f, -0.19f), new Vector3(0.36f, 0.06f, 0.02f), plankB);
        }

        // A shelf on the wall with jars and cans on it.
        void Shelf(Transform t, Vector3 at, Vector3 along, int items)
        {
            var s = new GameObject("Shelf").transform;
            s.SetParent(t, false);
            s.localPosition = at;
            s.localRotation = Quaternion.LookRotation(Vector3.Cross(along, Vector3.up), Vector3.up);
            Box(s, "Shelf board", Vector3.zero, new Vector3(1.0f, 0.03f, 0.25f), plankB);
            for (int side = -1; side <= 1; side += 2)
            {
                var bracket = Box(s, "Bracket", new Vector3(side * 0.4f, -0.1f, -0.06f), new Vector3(0.03f, 0.2f, 0.03f), plankC);
                bracket.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
            }
            for (int i = 0; i < items; i++)
            {
                float x = -0.42f + i * (0.84f / Mathf.Max(1, items - 1)) + R(-0.03f, 0.03f);
                bool can = rng.Next(3) == 0;
                float hgt = can ? 0.11f : R(0.12f, 0.2f);
                Cyl(s, can ? "Can" : "Jar", new Vector3(x, 0.015f + hgt * 0.5f, R(-0.06f, 0.05f)), can ? 0.035f : R(0.035f, 0.05f), hgt, can ? iron : jar, Vector3.up);
            }
        }

        void Bed(Transform t, Vector3 at, float yaw, bool cot)
        {
            var b = new GameObject(cot ? "Cot" : "Bed").transform;
            b.SetParent(t, false);
            b.localPosition = at;
            b.localRotation = Quaternion.Euler(0f, yaw, 0f);
            float len = 1.95f, wid = cot ? 0.75f : 1.3f, top = cot ? 0.4f : 0.5f;
            Box(b, "Frame", new Vector3(0f, top - 0.08f, 0f), new Vector3(wid, 0.1f, len), cot ? iron : plankC, true);
            for (int i = 0; i < 4; i++)
                Box(b, "Leg", new Vector3((i % 2 == 0 ? -1 : 1) * (wid * 0.5f - 0.04f), (top - 0.1f) * 0.5f, (i < 2 ? -1 : 1) * (len * 0.5f - 0.04f)), new Vector3(0.06f, top - 0.1f, 0.06f), cot ? iron : plankC);
            if (!cot)
            {
                Box(b, "Headboard", new Vector3(0f, top + 0.3f, -len * 0.5f), new Vector3(wid + 0.06f, 0.75f, 0.06f), plankC);
                Box(b, "Footboard", new Vector3(0f, top + 0.1f, len * 0.5f), new Vector3(wid + 0.06f, 0.4f, 0.06f), plankC);
            }
            Box(b, "Mattress", new Vector3(0f, top + 0.05f, 0f), new Vector3(wid - 0.06f, 0.13f, len - 0.08f), fabric);
            Box(b, "Stain", new Vector3(R(-0.15f, 0.15f), top + 0.117f, R(-0.2f, 0.4f)), new Vector3(0.35f, 0.002f, 0.45f), Keep(RuntimeMaterials.Lit("Old stain", new Color(0.16f, 0.1f, 0.06f), 0.2f)));
            Box(b, "Pillow", new Vector3(0f, top + 0.15f, -len * 0.5f + 0.25f), new Vector3(wid * 0.6f, 0.08f, 0.3f), fabric);
            var cover = Box(b, "Blanket", new Vector3(0.05f, top + 0.13f, 0.35f), new Vector3(wid + 0.02f, 0.03f, len * 0.55f), blanket);
            cover.transform.localRotation = Quaternion.Euler(0f, R(-8f, 8f), 0f);
            Box(b, "Blanket (hanging)", new Vector3(wid * 0.5f + 0.01f, top - 0.05f, 0.35f), new Vector3(0.02f, 0.3f, len * 0.5f), blanket);
        }

        void ShackInside(Transform t, float hw, float hd, float h)
        {
            // A little pot-belly stove against the right wall, its pipe out through the tin.
            var stove = new Vector3(hw - 0.4f, 0f, -0.2f);
            Cyl(t, "Stove", stove + new Vector3(0f, 0.42f, 0f), 0.24f, 0.5f, iron, Vector3.up, true);
            Shape(PrimitiveType.Sphere, t, "Stove belly", stove + new Vector3(0f, 0.45f, 0f), new Vector3(0.56f, 0.4f, 0.56f), iron);
            Cyl(t, "Stove top", stove + new Vector3(0f, 0.7f, 0f), 0.2f, 0.04f, iron, Vector3.up);
            for (int i = 0; i < 3; i++)
            {
                float a = i / 3f * Mathf.PI * 2f;
                Rod(t, stove + new Vector3(Mathf.Cos(a) * 0.15f, 0.2f, Mathf.Sin(a) * 0.15f), stove + new Vector3(Mathf.Cos(a) * 0.22f, 0f, Mathf.Sin(a) * 0.22f), 0.02f, iron);
            }
            Cyl(t, "Stove pipe", stove + new Vector3(0f, (0.72f + h + 1.4f) * 0.5f, 0f), 0.06f, h + 1.4f - 0.72f, iron, Vector3.up);
            Cyl(t, "Pipe cap", stove + new Vector3(0f, h + 1.42f, 0f), 0.11f, 0.04f, iron, Vector3.up);
            // A rag hung over the plain window, half drawn; a rifle rack (empty) over the cot; a sack in the corner.
            var rag = Box(t, "Rag curtain", new Vector3(-hw + 0.1f, 1.45f, -0.2f), new Vector3(0.02f, 0.8f, 0.5f), Keep(RuntimeMaterials.Lit("Rag", new Color(0.26f, 0.22f, 0.15f), 0.05f)));
            rag.transform.localRotation = Quaternion.Euler(4f, 0f, 0f);
            foreach (float y in new[] { 1.55f, 1.85f })
                for (int side = -1; side <= 1; side += 2)
                    Box(t, "Rack peg", new Vector3(-hw + 0.1f, y, -hd + 1.15f + side * 0.45f), new Vector3(0.12f, 0.04f, 0.04f), plankC);
            Shape(PrimitiveType.Sphere, t, "Sack", new Vector3(-hw + 0.4f, 0.22f, hd - 1.1f), new Vector3(0.4f, 0.45f, 0.34f), Keep(RuntimeMaterials.Lit("Sacking", new Color(0.3f, 0.26f, 0.18f), 0.05f)));
            // Traps on a nail by the door.
            var trap = new Vector3(0.95f, 1.6f, hd - 0.09f);
            Cyl(t, "Trap", trap, 0.12f, 0.02f, tin, Vector3.forward);
            Rod(t, trap + Vector3.up * 0.12f, trap + Vector3.up * 0.26f, 0.006f, iron);
            Bed(t, new Vector3(-hw + 0.5f, 0.05f, -hd + 1.15f), 0f, true);
            Shelf(t, new Vector3(hw - 0.17f, 1.5f, 1.0f), Vector3.forward, 5);
            Shelf(t, new Vector3(0.6f, 1.6f, -hd + 0.17f), Vector3.right, 4);
            Cyl(t, "Bucket", new Vector3(-hw + 0.45f, 0.2f, hd - 0.6f), 0.15f, 0.3f, tin, Vector3.up, true);
            Cyl(t, "Barrel", new Vector3(hw - 0.45f, 0.48f, hd - 0.5f), 0.28f, 0.85f, plankB, Vector3.up, true);
            foreach (float y in new[] { 0.2f, 0.76f })
                Cyl(t, "Hoop", new Vector3(hw - 0.45f, 0.05f + y, hd - 0.5f), 0.29f, 0.04f, iron, Vector3.up);
            // An old calendar pinned up by the door.
            Box(t, "Calendar", new Vector3(-0.95f, 1.5f, hd - 0.075f), new Vector3(0.28f, 0.38f, 0.005f), paper);
        }

        // Round the shack: a lean-to woodshed down its left side (posts, a sloping tin roof, split logs stacked
        // under it), a chopping block out front with an axe in it and chips round it, a rain barrel under the
        // right eave, a skull of antlers over the door.
        void ShackOutside(Transform t, float hw, float hd, float h, float doorWidth, float doorHeight)
        {
            const float depth = 1.0f;
            float x0 = -hw, x1 = -hw - depth;
            float back = -hd + 0.3f, front = hd - 0.6f;
            foreach (float z in new[] { back, front })
                Box(t, "Lean-to post", new Vector3(x1 + 0.06f, (h - 0.6f) * 0.5f, z), new Vector3(0.1f, h - 0.6f, 0.1f), plankC, true);
            var roof = new GameObject("Lean-to roof").transform;
            roof.SetParent(t, false);
            float drop = 0.45f;
            roof.localPosition = new Vector3((x0 + x1) * 0.5f - 0.1f, h - 0.4f - drop * 0.5f, (back + front) * 0.5f);
            roof.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(drop, depth + 0.2f) * Mathf.Rad2Deg);
            Box(roof, "Lean-to tin", Vector3.zero, new Vector3(depth + 0.4f, 0.03f, front - back + 0.5f), tin);
            for (float z = -(front - back) * 0.5f - 0.2f; z < (front - back) * 0.5f + 0.25f; z += 0.16f)
                Box(roof, "Rib", new Vector3(0f, 0.02f, z), new Vector3(depth + 0.4f, 0.02f, 0.03f), tin);
            // The woodpile: split logs, ends out, stacked along the wall.
            for (int row = 0; row < 6; row++)
                for (float z = back + 0.2f; z < front - 0.15f; z += 0.17f)
                {
                    if (row >= 4 && rng.Next(3) == 0) continue; // (the top rows ragged)
                    Cyl(t, "Firewood", new Vector3(x0 - 0.45f + R(-0.04f, 0.04f), 0.09f + row * 0.15f, z + R(-0.02f, 0.02f)), R(0.065f, 0.085f), 0.7f, log, Vector3.right);
                }
            Box(t, "Woodpile", new Vector3(x0 - 0.45f, 0.5f, (back + front) * 0.5f), new Vector3(0.7f, 1.0f, front - back - 0.2f), plankC, true).GetComponent<Renderer>().enabled = false;
            // The chopping block, the axe in it, chips about.
            var block = new Vector3(hw + 0.2f, 0f, hd + 1.1f);
            Cyl(t, "Chopping block", block + new Vector3(0f, 0.25f, 0f), 0.28f, 0.5f, log, Vector3.up, true);
            Cyl(t, "Block top", block + new Vector3(0f, 0.505f, 0f), 0.27f, 0.01f, logEnd, Vector3.up);
            Rod(t, block + new Vector3(0.02f, 0.52f, 0f), block + new Vector3(0.3f, 1.1f, 0.15f), 0.02f, plankB);
            Box(t, "Axe head", block + new Vector3(0.02f, 0.53f, 0f), new Vector3(0.05f, 0.1f, 0.16f), iron).transform.localRotation = Quaternion.Euler(0f, 30f, -25f);
            for (int i = 0; i < 10; i++)
            {
                float a = R(0f, Mathf.PI * 2f), r = R(0.35f, 0.9f);
                var chip = Box(t, "Wood chip", block + new Vector3(Mathf.Cos(a) * r, 0.01f, Mathf.Sin(a) * r), new Vector3(R(0.04f, 0.09f), 0.01f, R(0.02f, 0.04f)), logEnd);
                chip.transform.localRotation = Quaternion.Euler(0f, R(0f, 180f), 0f);
            }
            // The rain barrel at the back right corner.
            var barrel = new Vector3(hw + 0.4f, 0f, -hd + 0.5f);
            Cyl(t, "Rain barrel", barrel + new Vector3(0f, 0.45f, 0f), 0.3f, 0.9f, plankB, Vector3.up, true);
            foreach (float y in new[] { 0.15f, 0.75f })
                Cyl(t, "Hoop", barrel + new Vector3(0f, y, 0f), 0.31f, 0.04f, iron, Vector3.up);
            Cyl(t, "Water", barrel + new Vector3(0f, 0.86f, 0f), 0.27f, 0.01f, Keep(RuntimeMaterials.Lit("Black water", new Color(0.01f, 0.012f, 0.012f), 0.95f)), Vector3.up);
            // Antlers over the door.
            Antlers(t, new Vector3(0f, doorHeight + 0.35f, hd + 0.04f), Vector3.forward);
        }

        // The cabin, in three rooms: the front room (the door, the stove, the table, a counter and basin, a rug and
        // the hanging lantern); through a doorway on the left, behind its own door, the bedroom (a bed, a
        // nightstand, a wardrobe, a bookshelf, a rug); and through an open doorway on the right, the storeroom (a
        // workbench under a pegboard of tools, shelves of jars, crates, sacks, a barrel, traps hung up). A plank
        // partition across the middle and another between the back rooms (solid: you can't walk through them).
        void CabinRooms(Transform t, float hw, float hd, float h)
        {
            // The partitions: across at the middle (doorways left and right), and down the middle behind it.
            Partition(t, new Vector3(0f, 0f, 0f), Vector3.right, hw * 2f, h, new[] { -BackDoorAt, BackDoorAt });
            Partition(t, new Vector3(0f, 0f, -hd * 0.5f), Vector3.forward, hd, h, new float[0]);
            // The bedroom's door (it swings in), on its own hinge.
            var hinge = new GameObject("Door") { layer = Layers.Props }.transform;
            hinge.SetParent(t, false);
            hinge.localPosition = new Vector3(-BackDoorAt - BackDoorWidth * 0.5f, 0f, -0.02f);
            for (int i = 0; i < 4; i++)
            {
                float pw = (BackDoorWidth - 0.04f) / 4f;
                Box(hinge, "Door plank", new Vector3(0.02f + pw * (i + 0.5f), BackDoorHeight * 0.5f, 0f), new Vector3(pw - 0.008f, BackDoorHeight - 0.03f, 0.045f), Plank, i == 1);
            }
            foreach (float y in new[] { 0.35f, BackDoorHeight - 0.35f })
                Box(hinge, "Brace", new Vector3(BackDoorWidth * 0.5f, y, 0.035f), new Vector3(BackDoorWidth - 0.1f, 0.1f, 0.025f), plankC);
            Cyl(hinge, "Knob", new Vector3(BackDoorWidth - 0.12f, 1.0f, -0.05f), 0.03f, 0.05f, iron, Vector3.forward);
            hinge.gameObject.AddComponent<Door>().ajar = 0.25f; // (left standing a little open: what's in there?)

            FrontRoom(t, hw, hd, h);
            Bedroom(t, hw, hd);
            Storeroom(t, hw, hd);
        }

        // A plank wall inside: boards standing side by side, broken by doorways (their middles along it, each
        // BackDoorWidth wide, BackDoorHeight high), with a frame round each and a solid run you can't walk through.
        void Partition(Transform t, Vector3 middle, Vector3 along, float length, float h, float[] doorways)
        {
            var wall = new GameObject("Partition").transform;
            wall.SetParent(t, false);
            wall.localPosition = middle;
            wall.localRotation = Quaternion.LookRotation(Vector3.Cross(along, Vector3.up), Vector3.up); // (its length along local x)
            bool Open(float a, float y)
            {
                foreach (float d in doorways) if (Mathf.Abs(a - d) < BackDoorWidth * 0.5f && y < BackDoorHeight) return true;
                return false;
            }
            for (float a = -length * 0.5f + 0.1f; a < length * 0.5f; a += 0.2f)
            {
                bool open = false;
                foreach (float d in doorways) if (Mathf.Abs(a - d) < BackDoorWidth * 0.5f + 0.05f) open = true;
                if (open)
                    Box(wall, "Board", new Vector3(a, (BackDoorHeight + h) * 0.5f, 0f), new Vector3(0.19f, h - BackDoorHeight, 0.05f), Plank);
                else
                    Box(wall, "Board", new Vector3(a, h * 0.5f, 0f), new Vector3(0.19f, h, 0.05f), Plank);
            }
            // (What you bump into: one block over each solid run.)
            Runs(length, a => !Open(a, 1f), (from, to) => Box(wall, "Wall", new Vector3((from + to) * 0.5f, h * 0.5f, 0f), new Vector3(to - from, h, 0.1f), plankC, true).GetComponent<Renderer>().enabled = false);
            foreach (float d in doorways)
            {
                for (int side = -1; side <= 1; side += 2)
                    Box(wall, "Door frame", new Vector3(d + side * (BackDoorWidth * 0.5f + 0.04f), BackDoorHeight * 0.5f, 0f), new Vector3(0.08f, BackDoorHeight, 0.12f), plankC);
                Box(wall, "Lintel", new Vector3(d, BackDoorHeight + 0.05f, 0f), new Vector3(BackDoorWidth + 0.2f, 0.1f, 0.13f), plankC);
            }
        }

        void FrontRoom(Transform t, float hw, float hd, float h)
        {
            Box(t, "Rug", new Vector3(-0.4f, 0.065f, hd * 0.5f), new Vector3(2.6f, 0.01f, 1.7f), rug);
            // A counter along the left wall by the partition, a basin let into it, a shelf over it.
            var counterAt = new Vector3(-hw + 0.4f, 0f, 0.85f);
            Box(t, "Counter", counterAt + new Vector3(0f, 0.43f, 0f), new Vector3(0.6f, 0.86f, 1.3f), plankC, true);
            Box(t, "Counter top", counterAt + new Vector3(0.02f, 0.88f, 0f), new Vector3(0.66f, 0.04f, 1.36f), plankB);
            Cyl(t, "Basin", counterAt + new Vector3(0.02f, 0.92f, 0.25f), 0.2f, 0.08f, tin, Vector3.up);
            Cyl(t, "Pump", counterAt + new Vector3(-0.2f, 1.1f, 0.25f), 0.03f, 0.4f, iron, Vector3.up);
            Box(t, "Cupboard door", counterAt + new Vector3(0.31f, 0.43f, -0.3f), new Vector3(0.02f, 0.65f, 0.55f), plankB);
            Shelf(t, new Vector3(-hw + 0.17f, 1.65f, 0.85f), Vector3.back, 6);
            // The stove by the left wall, its pipe up through the roof.
            var stoveAt = new Vector3(-hw + 0.65f, 0f, hd - 1.3f);
            Box(t, "Stove", stoveAt + new Vector3(0f, 0.45f, 0f), new Vector3(0.6f, 0.55f, 0.75f), iron, true);
            Box(t, "Stove top", stoveAt + new Vector3(0f, 0.74f, 0f), new Vector3(0.66f, 0.04f, 0.8f), iron);
            Box(t, "Stove door", stoveAt + new Vector3(0.31f, 0.45f, 0f), new Vector3(0.02f, 0.3f, 0.36f), iron);
            for (int i = 0; i < 4; i++)
                Box(t, "Stove leg", stoveAt + new Vector3(i % 2 == 0 ? -0.25f : 0.25f, 0.09f, i < 2 ? -0.3f : 0.3f), new Vector3(0.05f, 0.18f, 0.05f), iron);
            Cyl(t, "Stove pipe", stoveAt + new Vector3(0f, (0.76f + h + 2f) * 0.5f, 0f), 0.07f, h + 2f - 0.76f, iron, Vector3.up);
            Cyl(t, "Pipe cap", stoveAt + new Vector3(0f, h + 2.02f, 0f), 0.13f, 0.04f, iron, Vector3.up);
            // Split logs stacked by it, and a kettle on it.
            for (int i = 0; i < 7; i++)
                Cyl(t, "Firewood", stoveAt + new Vector3(0.1f + (i % 4) * 0.12f - 0.15f, 0.07f + (i / 4) * 0.12f, -0.75f), 0.055f, 0.45f, log, Vector3.right);
            Cyl(t, "Kettle", stoveAt + new Vector3(0.05f, 0.85f, 0.15f), 0.11f, 0.16f, iron, Vector3.up);
            // A ceiling beam across, and a lantern hanging from it on a chain (click it on and off).
            Box(t, "Beam", new Vector3(0f, h - 0.1f, hd * 0.5f), new Vector3(hw * 2f - 0.2f, 0.18f, 0.18f), plankC);
            LanternModel.Hanging(t, new Vector3(0.3f, h - 0.19f, hd * 0.5f), 0.4f, tin, iron, made, 6f, 3f);
            Cyl(t, "Lantern cap", new Vector3(0.3f, h - 0.6f, hd * 0.5f), 0.08f, 0.03f, iron, Vector3.up);
            Cyl(t, "Lantern base", new Vector3(0.3f, h - 0.83f, hd * 0.5f), 0.08f, 0.03f, iron, Vector3.up);
            // A coat hook by the door, an old coat on it; antlers over the table; a framed photo, crooked.
            Box(t, "Coat", new Vector3(1.4f, 1.3f, hd - 0.12f), new Vector3(0.45f, 0.8f, 0.08f), Keep(RuntimeMaterials.Lit("Coat", new Color(0.12f, 0.12f, 0.1f), 0.1f)));
            Antlers(t, new Vector3(hw - 0.12f, 2.2f, hd * 0.5f), Vector3.left);
            var photo = Box(t, "Photo", new Vector3(hw - 0.09f, 1.5f, 1.0f), new Vector3(0.02f, 0.3f, 0.24f), paper);
            photo.transform.localRotation = Quaternion.Euler(R(-12f, 12f), 0f, 0f);
            Box(t, "Photo frame", new Vector3(hw - 0.1f, 1.5f, 1.0f), new Vector3(0.015f, 0.34f, 0.28f), plankC).transform.localRotation = photo.transform.localRotation;
        }

        void Bedroom(Transform t, float hw, float hd)
        {
            Box(t, "Rug", new Vector3(-hw * 0.5f, 0.065f, -hd * 0.45f), new Vector3(1.6f, 0.01f, 1.2f), Keep(RuntimeMaterials.Lit("Bedroom rug", new Color(0.12f, 0.1f, 0.14f), 0.05f)));
            Bed(t, new Vector3(-hw + 1.05f, 0.05f, -hd + 0.8f), 90f, false);
            // The nightstand by the bed, a drawer left pulled out (things on top: see PlaceFittings).
            var ns = PlaceFittings.CabinNightstandSpot;
            Box(t, "Nightstand", ns + new Vector3(0f, 0.29f, 0f), new Vector3(0.46f, 0.58f, 0.4f), plankB, true);
            Box(t, "Drawer", ns + new Vector3(0f, 0.43f, 0.24f), new Vector3(0.38f, 0.12f, 0.2f), plankC);
            Cyl(t, "Candle", ns + new Vector3(0.15f, 0.64f, -0.1f), 0.022f, 0.12f, Keep(RuntimeMaterials.Lit("Candle", new Color(0.55f, 0.52f, 0.44f), 0.3f)), Vector3.up);
            // A wardrobe in the corner by the doorway, one door hanging open.
            var wardrobe = new Vector3(-hw + 0.38f, 0f, -0.75f);
            Box(t, "Wardrobe", wardrobe + new Vector3(0f, 1.0f, 0f), new Vector3(0.6f, 2.0f, 1.1f), plankC, true);
            Box(t, "Wardrobe door", wardrobe + new Vector3(0.31f, 1.0f, 0.27f), new Vector3(0.02f, 1.85f, 0.5f), plankB);
            var hanging = Box(t, "Wardrobe door", wardrobe + new Vector3(0.55f, 1.0f, -0.42f), new Vector3(0.02f, 1.85f, 0.5f), plankB);
            hanging.transform.localRotation = Quaternion.Euler(0f, 55f, 0f);
            // A bookshelf against the partition between the back rooms.
            Bookshelf(t, new Vector3(-0.35f, 0f, -hd * 0.45f), -90f);
            // A chair by the window, a shirt over its back.
            Chair(t, new Vector3(-1.1f, 0f, -0.75f), 200f, false); // (clear of the door's swing)
        }

        void Storeroom(Transform t, float hw, float hd)
        {
            // The workbench against the back wall, a vise on its end, a pegboard of tools over it (things on top: see
            // PlaceFittings).
            var wb = PlaceFittings.CabinWorkbenchSpot;
            Box(t, "Workbench top", wb + new Vector3(0f, 0.88f, 0f), new Vector3(1.8f, 0.07f, 0.65f), plankB, true);
            Box(t, "Workbench shelf", wb + new Vector3(0f, 0.25f, 0f), new Vector3(1.7f, 0.04f, 0.55f), plankC);
            for (int i = 0; i < 4; i++)
                Box(t, "Workbench leg", wb + new Vector3(i % 2 == 0 ? -0.82f : 0.82f, 0.43f, i < 2 ? -0.26f : 0.26f), new Vector3(0.08f, 0.86f, 0.08f), plankC);
            Box(t, "Vise", wb + new Vector3(0.8f, 0.98f, 0.28f), new Vector3(0.14f, 0.12f, 0.16f), iron);
            Box(t, "Pegboard", wb + new Vector3(0f, 1.55f, -0.36f), new Vector3(1.6f, 0.9f, 0.02f), plankC);
            Box(t, "Saw", wb + new Vector3(-0.45f, 1.6f, -0.34f), new Vector3(0.55f, 0.13f, 0.005f), tin);
            Box(t, "Hammer", wb + new Vector3(0.1f, 1.55f, -0.33f), new Vector3(0.03f, 0.32f, 0.03f), plankB);
            Box(t, "Hammer head", wb + new Vector3(0.1f, 1.7f, -0.33f), new Vector3(0.12f, 0.04f, 0.04f), iron);
            Rod(t, wb + new Vector3(0.4f, 1.3f, -0.33f), wb + new Vector3(0.55f, 1.8f, -0.33f), 0.012f, iron);
            Cyl(t, "Coil of rope", wb + new Vector3(0.62f, 1.45f, -0.31f), 0.14f, 0.05f, Keep(RuntimeMaterials.Lit("Rope", new Color(0.3f, 0.25f, 0.16f), 0.1f)), Vector3.forward);
            // Shelves of jars on the walls.
            Shelf(t, new Vector3(hw - 0.17f, 1.55f, -0.75f), Vector3.forward, 6);
            Shelf(t, new Vector3(0.17f, 1.6f, -hd * 0.45f), Vector3.back, 5);
            Shelf(t, new Vector3(0.17f, 1.1f, -hd * 0.45f), Vector3.back, 4);
            // Crates stacked in the corner, sacks slumped against them, a barrel.
            Box(t, "Crate", new Vector3(hw - 0.48f, 0.3f, -hd + 0.45f), new Vector3(0.6f, 0.6f, 0.6f), plankB, true);
            var top = Box(t, "Crate", new Vector3(hw - 0.5f, 0.8f, -hd + 0.47f), new Vector3(0.45f, 0.4f, 0.45f), plankC, true);
            top.transform.localRotation = Quaternion.Euler(0f, R(-20f, 20f), 0f);
            var sack = Keep(RuntimeMaterials.Lit("Sacking", new Color(0.3f, 0.26f, 0.18f), 0.05f));
            for (int i = 0; i < 2; i++)
            {
                var s = Shape(PrimitiveType.Sphere, t, "Sack", new Vector3(0.6f + i * 0.45f, 0.25f, -0.55f), new Vector3(0.42f, 0.5f, 0.36f), sack);
                s.transform.localRotation = Quaternion.Euler(R(-10f, 10f), R(0f, 180f), R(-15f, 15f));
            }
            Cyl(t, "Barrel", new Vector3(hw - 0.45f, 0.48f, -0.4f), 0.28f, 0.85f, plankB, Vector3.up, true);
            // Traps hung up on the wall by the doorway: rusted jaws.
            for (int i = 0; i < 2; i++)
            {
                var trap = new Vector3(BackDoorAt + 0.95f + i * 0.35f, 1.8f, -0.045f);
                Cyl(t, "Trap", trap, 0.13f, 0.02f, tin, Vector3.forward);
                Rod(t, trap + Vector3.up * 0.13f, trap + Vector3.up * 0.3f, 0.006f, iron);
            }
        }

        void Bookshelf(Transform t, Vector3 at, float yaw)
        {
            var books = new GameObject("Bookshelf").transform;
            books.SetParent(t, false);
            books.localPosition = at;
            books.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Box(books, "Case", new Vector3(0f, 0.95f, -0.1f), new Vector3(1.0f, 1.9f, 0.04f), plankC, true);
            for (int side = -1; side <= 1; side += 2) Box(books, "Side", new Vector3(side * 0.5f, 0.95f, 0.05f), new Vector3(0.04f, 1.9f, 0.32f), plankC);
            for (int shelf = 0; shelf < 4; shelf++)
            {
                float y = 0.1f + shelf * 0.5f;
                Box(books, "Shelf", new Vector3(0f, y, 0.05f), new Vector3(1.0f, 0.03f, 0.3f), plankB);
                float x = -0.45f;
                while (x < 0.4f && shelf < 3)
                {
                    float bw = R(0.03f, 0.07f), bh = R(0.2f, 0.32f);
                    if (rng.Next(6) == 0) { x += 0.08f; continue; } // (a gap)
                    var book = Box(books, "Book", new Vector3(x + bw * 0.5f, y + 0.015f + bh * 0.5f, 0.06f), new Vector3(bw, bh, R(0.18f, 0.24f)),
                        Keep(RuntimeMaterials.Lit("Book", Color.HSVToRGB(R(0f, 1f), R(0.3f, 0.6f), R(0.1f, 0.3f)), 0.2f)));
                    if (rng.Next(8) == 0) book.transform.localRotation = Quaternion.Euler(0f, 0f, 12f);
                    x += bw + 0.005f;
                }
            }
        }

        // A pair of antlers on a little plaque, on a wall (facing: out from it into the room).
        void Antlers(Transform t, Vector3 at, Vector3 facing)
        {
            var a = new GameObject("Antlers").transform;
            a.SetParent(t, false);
            a.localPosition = at;
            a.localRotation = Quaternion.LookRotation(facing);
            var bone = Keep(RuntimeMaterials.Lit("Antler", new Color(0.42f, 0.36f, 0.27f), 0.25f));
            Box(a, "Plaque", Vector3.zero, new Vector3(0.22f, 0.3f, 0.03f), plankB);
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 b = new Vector3(side * 0.05f, 0.05f, 0.05f), m = new Vector3(side * 0.3f, 0.3f, 0.12f), tip = new Vector3(side * 0.42f, 0.55f, 0.08f);
                Rod(a, b, m, 0.018f, bone);
                Rod(a, m, tip, 0.013f, bone);
                for (int k = 0; k < 3; k++)
                {
                    Vector3 from = Vector3.Lerp(b, tip, 0.35f + k * 0.2f);
                    Rod(a, from, from + new Vector3(side * 0.02f, 0.16f, 0.05f), 0.009f, bone);
                }
            }
        }

        static GameObject Shape(PrimitiveType type, Transform parent, string name, Vector3 at, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = Layers.Props;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        // ------------------------------------------------------------------ bits

        static GameObject Box(Transform parent, string name, Vector3 at, Vector3 size, Material m, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.layer = Layers.Props;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        static GameObject Cyl(Transform parent, string name, Vector3 at, float radius, float length, Material m, Vector3 axis, bool collider = false)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.layer = Layers.Props;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, axis);
            go.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }

        static void Rod(Transform parent, Vector3 from, Vector3 to, float radius, Material m)
        {
            Vector3 d = to - from;
            if (d.sqrMagnitude < 1e-6f) return;
            Cyl(parent, "Rod", (from + to) * 0.5f, radius, d.magnitude, m, d.normalized);
        }
    }
}
