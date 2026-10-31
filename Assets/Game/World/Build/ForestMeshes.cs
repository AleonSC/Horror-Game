using System.Collections.Generic;
using UnityEngine;

namespace Game.World.Build
{
    /// <summary>
    /// The forest's shapes, made in code: pines (drooping ragged tiers on a bare trunk), dead trees (forked bare
    /// limbs), bushes, grass clumps, twigs, rocks and logs. Trees come in levels of detail (0 full, 1 simpler,
    /// 2 a rough shape for far off in the fog). They read as silhouettes in fog; a tree pack can replace them.
    /// </summary>
    public static class ForestMeshes
    {
        /// <summary>
        /// A tall pine: a long bare trunk, then drooping tiers of boughs up to a point, with gaps of dark between
        /// them. Each tier is an arched skirt, and up close each bough carries a couple of hanging sprays of needles
        /// that rag the edges. height &lt;= 0: random.
        /// </summary>
        public static (MeshBuilder trunk, MeshBuilder needles) Pine(System.Random rng, float height = -1f, int lod = 0, float trunkRadius = -1f)
        {
            var trunk = new MeshBuilder();
            var needles = new MeshBuilder();
            if (height <= 0f) height = Range(rng, 14f, 22f);
            float radius = Range(rng, 0.22f, 0.36f);
            if (trunkRadius > 0f) radius = trunkRadius; // (the breakable tree: always the same)
            trunk.AddTube(Vector3.down * 0.3f, Vector3.up * height, radius * 1.25f, 0.03f, lod == 0 ? 8 : lod == 1 ? 6 : 4);

            float crownStart = height * Range(rng, 0.4f, 0.55f);
            // (Everything that shapes the tree is drawn the same way at every level of detail, so the far version
            // is the same tree with less in it, not another tree: the tiers' reach is a fixed profile sampled at
            // however many tiers there are, and the needles' own jitter comes from a stream of its own.)
            var profile = new float[12];
            for (int k = 0; k < profile.Length; k++) profile[k] = Range(rng, 2.2f, 3.1f);
            var detail = new System.Random(rng.Next());
            var rootRng = new System.Random(rng.Next());
            int tiers = lod == 0 ? 11 : lod == 1 ? 7 : 4;
            int segments = lod == 0 ? 13 : lod == 1 ? 9 : 6;
            int sprays = lod == 0 ? 2 : 0;
            for (int t = 0; t < tiers; t++)
            {
                float f = t / (float)(tiers - 1);              // 0 bottom tier, 1 top
                float y = Mathf.Lerp(crownStart, height - 0.6f, f);
                float p = f * (profile.Length - 1);
                int p0 = Mathf.Min(profile.Length - 2, Mathf.FloorToInt(p));
                float baseReach = Mathf.Lerp(profile[p0], profile[p0 + 1], p - p0);
                float reach = Mathf.Lerp(baseReach, 0.4f, Mathf.Pow(f, 0.85f));
                float droop = Mathf.Lerp(1.3f, 0.45f, f) * (lod == 2 ? 2f : 1f);
                AddSkirt(needles, detail, y, reach, droop, segments, lod < 2);
                if (sprays > 0) AddBoughs(needles, detail, y, reach, droop, segments, sprays);
            }
            AddRoots(trunk, rootRng, radius * 1.25f, lod == 0 ? 6 : lod == 1 ? 4 : 0, lod == 0 ? 5 : 4);
            return (trunk, needles);
        }

        /// <summary>
        /// An oak: a stout trunk forking a third of the way up into crooked, spreading limbs, each with side branches,
        /// and a broad rounded canopy of leaf clumps at their ends: each clump a cluster of leaf cards (cut-out sprays
        /// of oak leaves, <see cref="ForestSurfaces.OakLeaves"/>) shaded as one rounded mass (their normals point out
        /// from the clump's middle). Far off, fewer and bigger cards. height &lt;= 0: random.
        /// </summary>
        public static (MeshBuilder trunk, MeshBuilder leaves) Oak(System.Random rng, float height = -1f, int lod = 0, float trunkRadius = -1f, bool roots = false)
        {
            var trunk = new MeshBuilder();
            var leaves = new MeshBuilder();
            if (height <= 0f) height = Range(rng, 11f, 16f);
            float radius = trunkRadius > 0f ? trunkRadius : Range(rng, 0.32f, 0.5f);
            int sides = lod == 0 ? 9 : lod == 1 ? 6 : 4;
            float fork = height * Range(rng, 0.3f, 0.38f);
            Vector3 lean = new Vector3(Range(rng, -0.4f, 0.4f), 0f, Range(rng, -0.4f, 0.4f));
            Vector3 forkAt = Vector3.up * fork + lean;
            trunk.AddTube(Vector3.down * 0.3f, forkAt, radius * 1.3f, radius * 0.95f, sides);
            float crownTop = height, crownRadius = height * Range(rng, 0.38f, 0.48f);

            var clumps = new List<(Vector3 at, float size)>();
            // (Every limb, branch and clump is drawn at every level of detail, so the crown's the same shape near
            // and far; only the tubes' sides and the leaf cards thin out. The cards and roots draw from their own
            // streams.)
            var leafRng = new System.Random(rng.Next());
            var rootRng = new System.Random(rng.Next());
            int limbs = rng.Next(4, 7);
            float turn = Range(rng, 0f, Mathf.PI * 2f);
            for (int i = 0; i < limbs; i++)
            {
                float a = turn + (i + Range(rng, -0.3f, 0.3f)) / limbs * Mathf.PI * 2f;
                Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float rise = Range(rng, 0.5f, 1.1f);
                float length = (crownTop - fork) * Range(rng, 0.65f, 0.9f);
                Vector3 start = forkAt + Vector3.down * Range(rng, 0f, 0.8f);
                // Two crooked lengths: out and up, then bending further out.
                Vector3 bend = start + (outward * 0.8f + Vector3.up * rise).normalized * length * 0.5f + RandomWobble(rng, 0.4f);
                Vector3 end = bend + (outward * 1.2f + Vector3.up * rise * 0.6f).normalized * length * 0.55f + RandomWobble(rng, 0.5f);
                float r0 = radius * Range(rng, 0.5f, 0.65f);
                trunk.AddTube(start, bend, r0, r0 * 0.65f, Mathf.Max(4, sides - 2));
                trunk.AddTube(bend, end, r0 * 0.65f, r0 * 0.2f, Mathf.Max(3, sides - 3));
                clumps.Add((end, Range(rng, 2.2f, 3f)));
                clumps.Add((bend + Vector3.up * 0.6f, Range(rng, 1.8f, 2.5f))); // (and along it, so the crown's full, not tufts on sticks)
                // Side branches off it, each ending in a clump (far off, the clumps without the branch).
                int side = rng.Next(2, 4);
                for (int k = 0; k < side; k++)
                {
                    float t = Range(rng, 0.35f, 0.85f);
                    Vector3 from = t < 0.5f ? Vector3.Lerp(start, bend, t * 2f) : Vector3.Lerp(bend, end, (t - 0.5f) * 2f);
                    Vector3 dir = (outward + new Vector3(Range(rng, -0.9f, 0.9f), Range(rng, 0.1f, 0.9f), Range(rng, -0.9f, 0.9f))).normalized;
                    // (Short, so its clump sits into the crown's mass rather than hanging out on its own like a pile of
                    // leaves floating off the rest; its branch drawn at every distance.)
                    Vector3 tip = from + dir * length * Range(rng, 0.18f, 0.3f);
                    float r = r0 * Mathf.Lerp(0.5f, 0.2f, t);
                    trunk.AddTube(from, tip, r, r * 0.2f, 3);
                    clumps.Add((tip, Range(rng, 1.8f, 2.5f)));
                }
            }
            // A dome over the middle and round its shoulders, so the crown's rounded and full, not a ring.
            Vector3 dome = forkAt + Vector3.up * (crownTop - fork) * 0.78f;
            clumps.Add((dome, crownRadius * 0.6f));
            clumps.Add((forkAt + Vector3.up * (crownTop - fork) * 0.5f, crownRadius * 0.5f));
            for (int i = 0; i < 4; i++)
            {
                float a = turn + (i + 0.5f) / 4f * Mathf.PI * 2f;
                clumps.Add((dome + new Vector3(Mathf.Cos(a), -0.35f, Mathf.Sin(a)) * crownRadius * 0.55f, crownRadius * 0.42f));
            }
            AddRoots(trunk, rootRng, radius * 1.3f, lod == 0 ? 6 : lod == 1 ? 4 : 0, Mathf.Max(3, sides - 4), roots);

            // The leaves: each clump a cluster of cards, the canopy's shading rounded (normals out from its middle).
            // (Far off: fewer, bigger cards. Each clump's cards come from its own stream, so the far ones are the
            // first few of the near ones, in the same places and turned the same way, only bigger and pulled in a
            // little so the clump stays the same size: it used to deal them all out afresh, and the crown's leaves
            // jumped about as you came nearer or went away.)
            int cards = lod == 0 ? 34 : lod == 1 ? 14 : 7;
            float cardSize = lod == 0 ? 1.4f : lod == 1 ? 2.3f : 3.4f;
            foreach (var (at, size) in clumps)
            {
                var clumpRng = new System.Random(leafRng.Next());
                float pull = Mathf.Clamp01((size - (cardSize - 1.4f) * 0.5f) / size);
                for (int c = 0; c < cards; c++)
                {
                    Vector3 offset = Random3(clumpRng) * size * Mathf.Sqrt((float)clumpRng.NextDouble());
                    Vector3 centre = at + offset * pull;
                    Vector3 normal = (offset + Vector3.up * size * 0.3f).normalized;
                    LeafCard(leaves, centre, normal, cardSize * Range(clumpRng, 0.8f, 1.25f), clumpRng);
                }
            }
            return (trunk, leaves);
        }

        // One leaf card: a square of the leaf texture, turned at random (but never quite edge-on to the sky), its
        // normal the clump's.
        static void LeafCard(MeshBuilder b, Vector3 centre, Vector3 normal, float size, System.Random rng)
        {
            Vector3 face = (Random3(rng) + normal * 0.6f).normalized;
            Vector3 right = Vector3.Cross(face, Mathf.Abs(face.y) < 0.95f ? Vector3.up : Vector3.right).normalized;
            Vector3 up = Vector3.Cross(right, face);
            float spin = Range(rng, 0f, Mathf.PI * 2f);
            Vector3 u = (right * Mathf.Cos(spin) + up * Mathf.Sin(spin)) * size * 0.5f;
            Vector3 v = (-right * Mathf.Sin(spin) + up * Mathf.Cos(spin)) * size * 0.5f;
            int i0 = b.AddVertex(centre - u - v, new Vector2(0f, 0f), normal);
            int i1 = b.AddVertex(centre + u - v, new Vector2(1f, 0f), normal);
            int i2 = b.AddVertex(centre + u + v, new Vector2(1f, 1f), normal);
            int i3 = b.AddVertex(centre - u + v, new Vector2(0f, 1f), normal);
            b.AddTriangle(i0, i2, i1);
            b.AddTriangle(i0, i3, i2);
        }

        static Vector3 Random3(System.Random rng)
        {
            float z = Range(rng, -1f, 1f), a = Range(rng, 0f, Mathf.PI * 2f), r = Mathf.Sqrt(1f - z * z);
            return new Vector3(r * Mathf.Cos(a), z, r * Mathf.Sin(a));
        }

        static Vector3 RandomWobble(System.Random rng, float amount) => new Vector3(Range(rng, -amount, amount), Range(rng, -amount, amount) * 0.5f, Range(rng, -amount, amount));

        /// <summary>A dead tree: a bare trunk forking into a few crooked limbs. height &lt;= 0: random.</summary>
        public static MeshBuilder DeadTree(System.Random rng, float height = -1f, int lod = 0, bool roots = false)
        {
            var b = new MeshBuilder();
            if (height <= 0f) height = Range(rng, 8f, 15f);
            float radius = Range(rng, 0.18f, 0.3f);
            Vector3 top = new Vector3(Range(rng, -0.6f, 0.6f), height, Range(rng, -0.6f, 0.6f));
            b.AddTube(Vector3.down * 0.3f, top, radius, radius * 0.25f, lod == 0 ? 7 : 4);

            // (The same limbs at every level of detail: only the twigs, drawn the same way, are left off far away.)
            var rootRng = new System.Random(rng.Next());
            int limbs = rng.Next(3, 7);
            for (int i = 0; i < limbs; i++)
            {
                float along = Range(rng, 0.4f, 0.9f);
                Vector3 start = Vector3.Lerp(Vector3.zero, top, along);
                float yaw = Range(rng, 0f, Mathf.PI * 2f);
                Vector3 dir = new Vector3(Mathf.Cos(yaw), Range(rng, 0.4f, 1.1f), Mathf.Sin(yaw)).normalized;
                float length = Range(rng, 1.5f, 4f) * (1.2f - along * 0.5f);
                Vector3 end = start + dir * length;
                float r = radius * Mathf.Lerp(0.5f, 0.2f, along);
                b.AddTube(start, end, r, r * 0.2f, lod == 0 ? 5 : 3);
                // A twig or two off each limb (up close).
                for (int k = 0; k < 2; k++)
                {
                    Vector3 s = Vector3.Lerp(start, end, Range(rng, 0.4f, 0.8f));
                    Vector3 d = (dir + new Vector3(Range(rng, -0.8f, 0.8f), Range(rng, 0f, 0.6f), Range(rng, -0.8f, 0.8f))).normalized;
                    if (lod == 0) b.AddTube(s, s + d * length * 0.45f, r * 0.35f, 0.005f, 4);
                }
            }
            AddRoots(b, rootRng, radius, lod == 0 ? 5 : lod == 1 ? 3 : 0, lod == 0 ? 5 : 4, roots);
            return b;
        }

        /// <summary>A lumpy rock about a metre across, sunk a little.</summary>
        public static MeshBuilder Rock(System.Random rng)
        {
            var b = new MeshBuilder();
            const int rings = 6, segments = 9;
            float seed = Range(rng, 0f, 100f);
            int[,] index = new int[rings + 1, segments + 1];
            for (int r = 0; r <= rings; r++)
            {
                float v = r / (float)rings;
                float phi = v * Mathf.PI;
                for (int s = 0; s <= segments; s++)
                {
                    float u = s / (float)segments;
                    float theta = u * Mathf.PI * 2f;
                    Vector3 n = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta));
                    float lump = 0.75f + 0.45f * Mathf.PerlinNoise(seed + n.x * 1.7f + n.y, seed + n.z * 1.7f);
                    Vector3 p = new Vector3(n.x * lump, n.y * lump * 0.6f, n.z * lump) * 0.5f;
                    index[r, s] = b.AddVertex(p - Vector3.up * 0.12f, new Vector2(u, v));
                }
            }
            for (int r = 0; r < rings; r++)
            for (int s = 0; s < segments; s++)
            {
                b.AddTriangle(index[r, s], index[r, s + 1], index[r + 1, s]);
                b.AddTriangle(index[r + 1, s], index[r, s + 1], index[r + 1, s + 1]);
            }
            return b;
        }

        /// <summary>A clump of grass: thin blades leaning out from a point. Both sides drawn (double-sided material).</summary>
        public static MeshBuilder GrassClump(System.Random rng, float height)
        {
            var b = new MeshBuilder();
            int blades = rng.Next(7, 14);
            for (int i = 0; i < blades; i++)
            {
                float yaw = Range(rng, 0f, Mathf.PI * 2f);
                float lean = Range(rng, 0.1f, 0.5f);
                float h = height * Range(rng, 0.5f, 1.1f);
                float w = Range(rng, 0.015f, 0.035f);
                Vector3 outward = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                Vector3 across = new Vector3(-outward.z, 0f, outward.x) * w;
                Vector3 root = outward * Range(rng, 0f, 0.12f);
                Vector3 mid = root + Vector3.up * h * 0.55f + outward * h * lean * 0.3f;
                Vector3 tip = root + Vector3.up * h + outward * h * lean;
                int a = b.AddVertex(root - across, new Vector2(0f, 0f));
                int c = b.AddVertex(root + across, new Vector2(1f, 0f));
                int d = b.AddVertex(mid - across * 0.7f, new Vector2(0f, 0.55f));
                int e = b.AddVertex(mid + across * 0.7f, new Vector2(1f, 0.55f));
                int t = b.AddVertex(tip, new Vector2(0.5f, 1f));
                b.AddTriangle(a, d, c);
                b.AddTriangle(c, d, e);
                b.AddTriangle(d, t, e);
            }
            return b;
        }

        /// <summary>
        /// A leafy bush about a metre across (kind 0 low and wide, 1 upright, 2 leaning over): thin stems out of the
        /// ground, each ending in a clump of real leaves, pointed ovals folded along their midribs and drooping a
        /// little, all facing outward. (Draw it double-sided.)
        /// </summary>
        public static MeshBuilder Bush(System.Random rng, int kind)
        {
            var b = new MeshBuilder();
            int clumps = rng.Next(7, 12);
            for (int i = 0; i < clumps; i++)
            {
                float a = Range(rng, 0f, Mathf.PI * 2f);
                float r = Range(rng, 0.05f, kind == 1 ? 0.22f : 0.45f);
                float y = kind == 1 ? Range(rng, 0.3f, 1.15f) : Range(rng, 0.2f, 0.6f);
                Vector3 c = new Vector3(Mathf.Cos(a) * r, y, Mathf.Sin(a) * r);
                if (kind == 2) c += new Vector3(0f, 0f, y * 0.6f); // leaning over
                float size = Range(rng, 0.2f, 0.32f);
                // Its stem, from near the middle of the bush's foot.
                var foot = new Vector3(Range(rng, -0.08f, 0.08f), 0f, Range(rng, -0.08f, 0.08f));
                b.AddTube(foot, c, 0.012f, 0.005f, 3);
                // Its leaves, all round it, facing out.
                int leaves = rng.Next(18, 28);
                for (int k = 0; k < leaves; k++)
                {
                    Vector3 dir = new Vector3(Range(rng, -1f, 1f), Range(rng, -0.35f, 1f), Range(rng, -1f, 1f));
                    if (dir.sqrMagnitude < 0.01f) dir = Vector3.up;
                    dir.Normalize();
                    Vector3 at = c + dir * size * Range(rng, 0.35f, 0.85f);
                    Vector3 point = (dir + Vector3.down * Range(0.1f, 0.45f, rng)).normalized; // (drooping a little)
                    Leaf(b, at, point, dir, Range(rng, 0.09f, 0.15f), rng);
                }
            }
            return b;
        }

        static float Range(float min, float max, System.Random rng) => Range(rng, min, max);

        // ------------------------------------------------------------------ the larger undergrowth

        static Vector3 RandomDir(System.Random rng, float yMin, float yMax)
        {
            var d = new Vector3(Range(rng, -1f, 1f), Range(rng, yMin, yMax), Range(rng, -1f, 1f));
            return d.sqrMagnitude < 0.01f ? Vector3.up : d.normalized;
        }

        static Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t) => a * ((1f - t) * (1f - t)) + b * (2f * (1f - t) * t) + c * (t * t);

        // A stem along a curve, in short tubes.
        static void Cane(MeshBuilder m, Vector3 a, Vector3 b, Vector3 c, float r0, float r1, int segments, int sides)
        {
            Vector3 prev = a;
            for (int k = 1; k <= segments; k++)
            {
                float t = k / (float)segments;
                Vector3 q = Bezier(a, b, c, t);
                m.AddTube(prev, q, Mathf.Lerp(r0, r1, (k - 1f) / segments), Mathf.Lerp(r0, r1, t), sides);
                prev = q;
            }
        }

        // A little flat spray (a thin quad hanging a touch), for seed heads and florets.
        static void Spray(MeshBuilder m, Vector3 at, Vector3 dir, float length, float width)
        {
            Vector3 s = Vector3.Cross(dir, Vector3.up);
            if (s.sqrMagnitude < 1e-4f) s = Vector3.right;
            s = s.normalized * width;
            Vector3 e = at + dir * length + Vector3.down * length * 0.1f;
            int a = m.AddVertex(at - s, new Vector2(0f, 0f)), b = m.AddVertex(at + s, new Vector2(1f, 0f));
            int c = m.AddVertex(e + s * 0.4f, new Vector2(1f, 1f)), d = m.AddVertex(e - s * 0.4f, new Vector2(0f, 1f));
            m.AddTriangle(a, b, c);
            m.AddTriangle(a, c, d);
        }

        /// <summary>
        /// The laurel mound (about 2.4 m tall and 3 m across): a dozen and a half stems out of one root, branching
        /// again, each ending in a clump of big, glossy, dark leaves; a solid rounded shrub nothing shows through.
        /// Stems and leaves apart (they take different materials).
        /// </summary>
        public static (MeshBuilder stems, MeshBuilder leaves) Laurel(System.Random rng)
        {
            var stems = new MeshBuilder();
            var leaves = new MeshBuilder();
            int count = rng.Next(15, 19);
            for (int s = 0; s < count; s++)
            {
                float a = Range(rng, 0f, Mathf.PI * 2f);
                var foot = new Vector3(Range(rng, -0.25f, 0.25f), 0f, Range(rng, -0.25f, 0.25f));
                var mid = new Vector3(Mathf.Cos(a) * Range(rng, 0.3f, 0.9f), Range(rng, 0.6f, 1.2f), Mathf.Sin(a) * Range(rng, 0.3f, 0.9f));
                stems.AddTube(foot, mid, 0.04f, 0.025f, 4);
                for (int k = 0; k < 3; k++)
                {
                    float b = a + Range(rng, -1f, 1f);
                    var end = mid + new Vector3(Mathf.Cos(b) * Range(rng, 0.3f, 0.8f), Range(rng, 0.2f, 1.1f), Mathf.Sin(b) * Range(rng, 0.3f, 0.8f));
                    end.y = Mathf.Min(end.y, 2.3f);
                    stems.AddTube(mid, end, 0.022f, 0.01f, 3);
                    for (int l = 0; l < 20; l++)
                    {
                        var d = RandomDir(rng, -0.4f, 1f);
                        Leaf(leaves, end + d * Range(rng, 0.05f, 0.38f), (d + Vector3.down * Range(rng, 0.1f, 0.4f)).normalized, d, Range(rng, 0.16f, 0.26f), rng);
                    }
                }
            }
            return (stems, leaves);
        }

        /// <summary>
        /// The dead thicket (about 2.5 m tall): a shrub that died standing, grey and leafless, its stems forking
        /// again and again into fine twigs that claw upward; you can just see through it.
        /// </summary>
        public static MeshBuilder DeadThicket(System.Random rng)
        {
            var m = new MeshBuilder();
            void Branch(Vector3 from, Vector3 dir, float len, float r, int depth)
            {
                Vector3 to = from + dir * len;
                m.AddTube(from, to, r, r * 0.6f, depth > 2 ? 3 : 4);
                if (depth >= 4) return;
                int n = depth < 2 ? 3 : 2;
                for (int k = 0; k < n; k++)
                    Branch(to, (dir + new Vector3(Range(rng, -0.8f, 0.8f), Range(rng, 0.1f, 0.6f), Range(rng, -0.8f, 0.8f))).normalized, len * Range(rng, 0.55f, 0.75f), r * 0.6f, depth + 1);
            }
            int stems = rng.Next(5, 8);
            for (int s = 0; s < stems; s++)
            {
                float a = Range(rng, 0f, Mathf.PI * 2f);
                Branch(new Vector3(Range(rng, -0.2f, 0.2f), -0.05f, Range(rng, -0.2f, 0.2f)), new Vector3(Mathf.Cos(a) * 0.4f, 1f, Mathf.Sin(a) * 0.4f).normalized, Range(rng, 0.75f, 1f), 0.045f, 0);
            }
            return m;
        }

        /// <summary>
        /// A clump of meadow grass a metre tall: a couple of dozen blades, the taller ones carrying drooping seed
        /// heads. (Scale it to height.)
        /// </summary>
        public static MeshBuilder Meadow(System.Random rng)
        {
            var m = new MeshBuilder();
            int blades = rng.Next(18, 26);
            for (int i = 0; i < blades; i++)
            {
                float yaw = Range(rng, 0f, Mathf.PI * 2f), lean = Range(rng, 0.1f, 0.35f), h = Range(rng, 0.65f, 1f), w = Range(rng, 0.01f, 0.018f);
                var outward = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw));
                var across = new Vector3(-outward.z, 0f, outward.x) * w;
                var root = outward * Range(rng, 0f, 0.18f);
                var mid = root + Vector3.up * h * 0.55f + outward * h * lean * 0.3f;
                var tip = root + Vector3.up * h + outward * h * lean;
                int a = m.AddVertex(root - across, new Vector2(0f, 0f)), c = m.AddVertex(root + across, new Vector2(1f, 0f));
                int d = m.AddVertex(mid - across * 0.7f, new Vector2(0f, 0.55f)), e = m.AddVertex(mid + across * 0.7f, new Vector2(1f, 0.55f));
                int t = m.AddVertex(tip, new Vector2(0.5f, 1f));
                m.AddTriangle(a, d, c);
                m.AddTriangle(c, d, e);
                m.AddTriangle(d, t, e);
                if (h > 0.85f && rng.NextDouble() < 0.5)
                    for (int k = 0; k < 4; k++)
                        Spray(m, tip + new Vector3(Range(rng, -0.02f, 0.02f), -k * 0.035f, Range(rng, -0.02f, 0.02f)), new Vector3(Range(rng, -0.3f, 0.3f), -1f, Range(rng, -0.3f, 0.3f)).normalized, 0.06f, 0.012f);
            }
            return m;
        }

        /// <summary>
        /// A weedy patch about a metre and a half across, for the edges of the lamps' clearings: grass, a few small
        /// ferns, and two or three tall stems of cow parsley, their flat white flower heads at head height. The
        /// green and the flowers apart (different materials).
        /// </summary>
        public static (MeshBuilder green, MeshBuilder flowers) Weeds(System.Random rng)
        {
            var green = new MeshBuilder();
            var flowers = new MeshBuilder();
            green.Append(GrassClump(rng, 0.7f), Matrix4x4.identity);
            green.Append(GrassClump(rng, 0.55f), Matrix4x4.Translate(new Vector3(0.4f, 0f, 0.25f)));
            green.Append(GrassClump(rng, 0.6f), Matrix4x4.Translate(new Vector3(-0.35f, 0f, -0.3f)));
            // Small ferns: a few fronds out of a crown, their leaflets in pairs.
            int ferns = rng.Next(1, 3);
            for (int f = 0; f < ferns; f++)
            {
                var crown = new Vector3(Range(rng, -0.5f, 0.5f), 0.03f, Range(rng, -0.5f, 0.5f));
                for (int k = 0; k < 4; k++)
                {
                    float a = k / 4f * Mathf.PI * 2f + Range(rng, -0.3f, 0.3f), len = Range(rng, 0.35f, 0.55f);
                    var dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                    Vector3 tip = crown + dir * len * 0.85f + Vector3.up * len * 0.28f, ctrl = crown + dir * len * 0.3f + Vector3.up * len * 1.2f;
                    Cane(green, crown, ctrl, tip, 0.006f, 0.002f, 4, 3);
                    var side = Vector3.Cross(dir, Vector3.up).normalized;
                    for (int j = 2; j < 10; j++)
                    {
                        float t = j / 10f;
                        Vector3 p = Bezier(crown, ctrl, tip, t);
                        float size = len * (0.16f * Mathf.Sin(Mathf.PI * Mathf.Min(1f, t * 1.1f)) + 0.02f);
                        Leaf(green, p, (side + Vector3.down * 0.15f).normalized, Vector3.up, size, rng);
                        Leaf(green, p, (-side + Vector3.down * 0.15f).normalized, Vector3.up, size, rng);
                    }
                }
            }
            // Cow parsley: tall stems, a flat umbel of tiny florets on rays at the top.
            int stalks = rng.Next(2, 4);
            for (int u = 0; u < stalks; u++)
            {
                var foot = new Vector3(Range(rng, -0.45f, 0.45f), 0f, Range(rng, -0.45f, 0.45f));
                float h = Range(rng, 1.25f, 1.6f);
                var top = foot + new Vector3(Range(rng, -0.12f, 0.12f), h, Range(rng, -0.12f, 0.12f));
                Cane(green, foot, Vector3.Lerp(foot, top, 0.5f) + new Vector3(Range(rng, -0.05f, 0.05f), 0f, 0f), top, 0.012f, 0.006f, 4, 3);
                for (int r = 0; r < 8; r++)
                {
                    float a = r / 8f * Mathf.PI * 2f;
                    var ray = top + new Vector3(Mathf.Cos(a) * 0.13f, 0.04f, Mathf.Sin(a) * 0.13f);
                    green.AddTube(top, ray, 0.003f, 0.002f, 3);
                    for (int q = 0; q < 5; q++)
                        Spray(flowers, ray + new Vector3(Range(rng, -0.035f, 0.035f), Range(rng, 0f, 0.015f), Range(rng, -0.035f, 0.035f)), Vector3.up, 0.016f, 0.013f);
                }
            }
            return (green, flowers);
        }

        // One leaf from its base, pointing along point, its upper face turned toward up: a pointed oval, folded a
        // touch along the midrib, its tip curling down.
        static void Leaf(MeshBuilder b, Vector3 at, Vector3 point, Vector3 up, float length, System.Random rng)
        {
            Vector3 side = Vector3.Cross(point, up);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.Cross(point, Vector3.forward);
            side.Normalize();
            Vector3 face = Vector3.Cross(side, point).normalized;
            float w = length * Range(rng, 0.32f, 0.45f);
            Vector3 mid = at + point * (length * 0.45f);
            Vector3 tip = at + point * length - face * (length * 0.12f);
            Vector3 ridge = mid + face * (length * 0.05f);
            int v0 = b.AddVertex(at, new Vector2(0.5f, 0f));
            int vl = b.AddVertex(mid - side * w * 0.5f - face * (length * 0.02f), new Vector2(0f, 0.45f));
            int vr = b.AddVertex(mid + side * w * 0.5f - face * (length * 0.02f), new Vector2(1f, 0.45f));
            int vm = b.AddVertex(ridge, new Vector2(0.5f, 0.45f));
            int vt = b.AddVertex(tip, new Vector2(0.5f, 1f));
            b.AddTriangle(v0, vm, vl);
            b.AddTriangle(vl, vm, vt);
            b.AddTriangle(v0, vr, vm);
            b.AddTriangle(vm, vr, vt);
        }

        /// <summary>A fallen log a metre long (along z) and half a metre thick; scale it to length.</summary>
        public static MeshBuilder Log(System.Random rng)
        {
            var b = new MeshBuilder();
            float r = Range(rng, 0.2f, 0.3f);
            b.AddTube(new Vector3(0f, r * 0.8f, -0.5f), new Vector3(0f, r * 0.8f, 0.5f), r, r * Range(rng, 0.75f, 0.95f), 7, capTop: true);
            b.AddTube(new Vector3(0f, r * 0.8f, 0.5f), new Vector3(0f, r * 0.8f, -0.5f), r * 0.8f, r, 7, capTop: true);
            return b;
        }

        /// <summary>A tangle of bare twigs and bramble stems.</summary>
        public static MeshBuilder Twigs(System.Random rng)
        {
            var b = new MeshBuilder();
            int stems = rng.Next(5, 10);
            for (int i = 0; i < stems; i++)
            {
                float yaw = Range(rng, 0f, Mathf.PI * 2f);
                Vector3 dir = new Vector3(Mathf.Cos(yaw) * Range(rng, 0.2f, 0.8f), 1f, Mathf.Sin(yaw) * Range(rng, 0.2f, 0.8f)).normalized;
                Vector3 start = new Vector3(Range(rng, -0.15f, 0.15f), 0f, Range(rng, -0.15f, 0.15f));
                Vector3 bend = start + dir * Range(rng, 0.3f, 0.6f);
                Vector3 end = bend + (dir + new Vector3(Range(rng, -0.6f, 0.6f), -0.2f, Range(rng, -0.6f, 0.6f))).normalized * Range(rng, 0.2f, 0.5f);
                b.AddTube(start, bend, 0.008f, 0.006f, 3);
                b.AddTube(bend, end, 0.006f, 0.002f, 3);
            }
            return b;
        }

        // Roots flaring out from the foot of the trunk into the ground: each from up the trunk a little, out and down
        // in two crooked lengths, tapering, diving well under the soil at the end (so a little unevenness in the
        // ground doesn't leave a tip in the air). Not drawn (draw false), it still takes the same turns of the
        // random numbers, so the rest of the tree comes out the same with roots or without.
        static void AddRoots(MeshBuilder b, System.Random rng, float trunkRadius, int count, int sides, bool draw = true)
        {
            float turn = Range(rng, 0f, Mathf.PI * 2f);
            for (int i = 0; i < count; i++)
            {
                float a = turn + (i + Range(rng, -0.25f, 0.25f)) / Mathf.Max(1, count) * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 side = new Vector3(-dir.z, 0f, dir.x);
                float reach = Range(rng, 0.5f, 1.1f) + trunkRadius;
                Vector3 from = dir * trunkRadius * 0.55f + Vector3.up * Range(0.3f, 0.55f, rng);
                Vector3 bend = dir * (trunkRadius + reach * 0.35f) + side * Range(rng, -0.12f, 0.12f) + Vector3.down * 0.02f;
                Vector3 end = dir * (trunkRadius + reach) + side * Range(rng, -0.25f, 0.25f) + Vector3.down * 0.4f;
                float r = trunkRadius * Range(rng, 0.38f, 0.55f);
                if (!draw) continue;
                b.AddTube(from, bend, r, r * 0.6f, sides);
                b.AddTube(bend, end, r * 0.6f, 0.02f, sides);
            }
        }

        // One tier of boughs: a ragged skirt from the trunk out and down. Arched: it rises a little from the trunk
        // before it droops, so it has body rather than being a flat cone.
        static void AddSkirt(MeshBuilder b, System.Random rng, float y, float reach, float droop, int segments, bool arched)
        {
            int centre = b.AddVertex(new Vector3(0f, y + 0.25f, 0f), new Vector2(0.5f, 1f));
            int mids = b.VertexCount;
            var angles = new float[segments];
            for (int s = 0; s < segments; s++) angles[s] = (s + Range(rng, -0.3f, 0.3f)) / segments * Mathf.PI * 2f;
            if (arched)
                for (int s = 0; s < segments; s++)
                {
                    float r = reach * Range(rng, 0.4f, 0.5f);
                    b.AddVertex(new Vector3(Mathf.Cos(angles[s]) * r, y + 0.05f - droop * Range(rng, 0.05f, 0.2f), Mathf.Sin(angles[s]) * r), new Vector2(s / (float)segments, 0.5f));
                }
            int first = b.VertexCount;
            for (int s = 0; s < segments; s++)
            {
                float r = reach * Range(rng, 0.65f, 1.15f);
                float d = droop * Range(rng, 0.7f, 1.3f);
                b.AddVertex(new Vector3(Mathf.Cos(angles[s]) * r, y - d, Mathf.Sin(angles[s]) * r), new Vector2(s / (float)segments, 0f));
            }
            for (int s = 0; s < segments; s++)
            {
                int n = (s + 1) % segments;
                if (!arched)
                {
                    b.AddTriangle(centre, first + n, first + s);
                    continue;
                }
                b.AddTriangle(centre, mids + n, mids + s);
                b.AddTriangle(mids + s, mids + n, first + s);
                b.AddTriangle(mids + n, first + n, first + s);
            }
        }

        // The boughs of a tier, each hung with sprays of needles: flat, drooping fans out along it and past its tip,
        // so the tier's edge is ragged and full rather than a clean rim.
        static void AddBoughs(MeshBuilder b, System.Random rng, float y, float reach, float droop, int boughs, int sprays)
        {
            for (int i = 0; i < boughs; i++)
            {
                float a = (i + Range(rng, -0.4f, 0.4f)) / boughs * Mathf.PI * 2f;
                Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                float r = reach * Range(rng, 0.85f, 1.2f);
                for (int k = 0; k < sprays; k++)
                {
                    float along = sprays == 1 ? 0.8f : Mathf.Lerp(0.35f, 1f, k / (float)(sprays - 1));
                    Vector3 at = outward * (r * along) + Vector3.up * (y + 0.1f - droop * along * along);
                    float yaw = Range(rng, -0.7f, 0.7f);
                    Vector3 point = (Quaternion.AngleAxis(yaw * Mathf.Rad2Deg, Vector3.up) * outward + Vector3.down * Range(rng, 0.25f, 0.7f)).normalized;
                    float length = reach * Range(rng, 0.3f, 0.45f) * Mathf.Lerp(1f, 0.7f, along);
                    Spray(b, at, point, length, length * Range(rng, 0.45f, 0.65f), rng);
                }
            }
        }

        // A spray of needles: a fan that widens from its base and droops at the tip, a little cupped.
        static void Spray(MeshBuilder b, Vector3 at, Vector3 point, float length, float width, System.Random rng)
        {
            Vector3 side = Vector3.Cross(point, Vector3.up);
            if (side.sqrMagnitude < 1e-4f) side = Vector3.right;
            side = (side.normalized + Vector3.up * Range(rng, -0.25f, 0.25f)).normalized;
            Vector3 face = Vector3.Cross(side, point).normalized;
            Vector3 mid = at + point * (length * 0.55f);
            Vector3 tip = at + point * length + Vector3.down * (length * 0.25f);
            int v0 = b.AddVertex(at, new Vector2(0.5f, 0f));
            int vl = b.AddVertex(mid - side * width * 0.5f - face * (width * 0.12f), new Vector2(0f, 0.55f));
            int vm = b.AddVertex(mid + face * (width * 0.06f), new Vector2(0.5f, 0.55f));
            int vr = b.AddVertex(mid + side * width * 0.5f - face * (width * 0.12f), new Vector2(1f, 0.55f));
            int vt = b.AddVertex(tip, new Vector2(0.5f, 1f));
            b.AddTriangle(v0, vm, vl);
            b.AddTriangle(vl, vm, vt);
            b.AddTriangle(v0, vr, vm);
            b.AddTriangle(vm, vr, vt);
        }

        static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
    }
}
