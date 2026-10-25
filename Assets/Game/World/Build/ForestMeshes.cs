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
        public static (MeshBuilder trunk, MeshBuilder needles) Pine(System.Random rng, float height = -1f, int lod = 0)
        {
            var trunk = new MeshBuilder();
            var needles = new MeshBuilder();
            if (height <= 0f) height = Range(rng, 14f, 22f);
            float radius = Range(rng, 0.22f, 0.36f);
            trunk.AddTube(Vector3.down * 0.3f, Vector3.up * height, radius * 1.25f, 0.03f, lod == 0 ? 8 : lod == 1 ? 6 : 4);

            float crownStart = height * Range(rng, 0.4f, 0.55f);
            int tiers = lod == 0 ? rng.Next(9, 12) : lod == 1 ? 7 : 4;
            int segments = lod == 0 ? 13 : lod == 1 ? 9 : 6;
            int sprays = lod == 0 ? 2 : 0;
            for (int t = 0; t < tiers; t++)
            {
                float f = t / (float)(tiers - 1);              // 0 bottom tier, 1 top
                float y = Mathf.Lerp(crownStart, height - 0.6f, f);
                float reach = Mathf.Lerp(Range(rng, 2.2f, 3.1f), 0.4f, Mathf.Pow(f, 0.85f));
                float droop = Mathf.Lerp(1.3f, 0.45f, f) * (lod == 2 ? 2f : 1f);
                AddSkirt(needles, rng, y, reach, droop, segments, lod < 2);
                if (sprays > 0) AddBoughs(needles, rng, y, reach, droop, segments, sprays);
            }
            return (trunk, needles);
        }

        /// <summary>A dead tree: a bare trunk forking into a few crooked limbs. height &lt;= 0: random.</summary>
        public static MeshBuilder DeadTree(System.Random rng, float height = -1f, int lod = 0)
        {
            var b = new MeshBuilder();
            if (height <= 0f) height = Range(rng, 8f, 15f);
            float radius = Range(rng, 0.18f, 0.3f);
            Vector3 top = new Vector3(Range(rng, -0.6f, 0.6f), height, Range(rng, -0.6f, 0.6f));
            b.AddTube(Vector3.down * 0.3f, top, radius, radius * 0.25f, lod == 0 ? 7 : 4);

            int limbs = lod == 2 ? 3 : rng.Next(3, 7);
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
                if (lod > 0) continue;
                // A twig or two off each limb.
                for (int k = 0; k < 2; k++)
                {
                    Vector3 s = Vector3.Lerp(start, end, Range(rng, 0.4f, 0.8f));
                    Vector3 d = (dir + new Vector3(Range(rng, -0.8f, 0.8f), Range(rng, 0f, 0.6f), Range(rng, -0.8f, 0.8f))).normalized;
                    b.AddTube(s, s + d * length * 0.45f, r * 0.35f, 0.005f, 4);
                }
            }
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
