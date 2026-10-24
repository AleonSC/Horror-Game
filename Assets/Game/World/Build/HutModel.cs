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
            var windows = new List<(int wall, float at, bool boarded)> { (0, 0f, false), (1, 0f, !cabin) };
            if (cabin) { windows.Add((2, -2.4f, false)); windows.Add((2, 2.4f, false)); }
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
            if (cabin) CabinInside(t, hw, hd, h);
            else ShackInside(t, hw, hd);
        }

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

        void ShackInside(Transform t, float hw, float hd)
        {
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

        void CabinInside(Transform t, float hw, float hd, float h)
        {
            Box(t, "Rug", new Vector3(0f, 0.065f, -0.3f), new Vector3(2.4f, 0.01f, 1.7f), rug);
            Bed(t, new Vector3(-hw + 1.1f, 0.05f, -hd + 1.15f), 90f, false);
            // The stove by the left wall, its pipe up through the roof.
            var stoveAt = new Vector3(-hw + 0.65f, 0f, 1.1f);
            Box(t, "Stove", stoveAt + new Vector3(0f, 0.45f, 0f), new Vector3(0.6f, 0.55f, 0.75f), iron, true);
            Box(t, "Stove top", stoveAt + new Vector3(0f, 0.74f, 0f), new Vector3(0.66f, 0.04f, 0.8f), iron);
            Box(t, "Stove door", stoveAt + new Vector3(0.31f, 0.45f, 0f), new Vector3(0.02f, 0.3f, 0.36f), iron);
            for (int i = 0; i < 4; i++)
                Box(t, "Stove leg", stoveAt + new Vector3(i % 2 == 0 ? -0.25f : 0.25f, 0.09f, i < 2 ? -0.3f : 0.3f), new Vector3(0.05f, 0.18f, 0.05f), iron);
            Cyl(t, "Stove pipe", stoveAt + new Vector3(0f, (0.76f + h + 2f) * 0.5f, 0f), 0.07f, h + 2f - 0.76f, iron, Vector3.up);
            Cyl(t, "Pipe cap", stoveAt + new Vector3(0f, h + 2.02f, 0f), 0.13f, 0.04f, iron, Vector3.up);
            // A bookshelf against the right wall.
            var books = new GameObject("Bookshelf").transform;
            books.SetParent(t, false);
            books.localPosition = new Vector3(hw - 0.35f, 0f, 1.3f);
            books.localRotation = Quaternion.Euler(0f, -90f, 0f);
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
            Shelf(t, new Vector3(-hw + 0.17f, 1.6f, -1.6f), Vector3.back, 6); // (over the bed, clear of the window)
            Shelf(t, new Vector3(1.8f, 1.6f, -hd + 0.17f), Vector3.right, 5);
            // A ceiling beam across, and a lantern hanging from it (long out).
            Box(t, "Beam", new Vector3(0f, h - 0.1f, -0.3f), new Vector3(hw * 2f - 0.2f, 0.18f, 0.18f), plankC);
            Rod(t, new Vector3(0.3f, h - 0.19f, -0.3f), new Vector3(0.3f, h - 0.6f, -0.3f), 0.006f, iron);
            Cyl(t, "Lantern", new Vector3(0.3f, h - 0.72f, -0.3f), 0.07f, 0.2f, jar, Vector3.up);
            Cyl(t, "Lantern cap", new Vector3(0.3f, h - 0.6f, -0.3f), 0.08f, 0.03f, iron, Vector3.up);
            Cyl(t, "Lantern base", new Vector3(0.3f, h - 0.83f, -0.3f), 0.08f, 0.03f, iron, Vector3.up);
            // A coat hook by the door, an old coat on it.
            Box(t, "Coat", new Vector3(1.4f, 1.3f, hd - 0.12f), new Vector3(0.45f, 0.8f, 0.08f), Keep(RuntimeMaterials.Lit("Coat", new Color(0.12f, 0.12f, 0.1f), 0.1f)));
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
