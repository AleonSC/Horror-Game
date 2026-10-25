using System.Collections.Generic;
using UnityEngine;

namespace Game.World.Build
{
    /// <summary>
    /// Cuts meshes into horizontal slabs (a felled tree's stump and logs): each triangle is clipped to every slab it
    /// crosses, so the pieces fit back together exactly. Where a cut crosses the trunk, it notes the points, so
    /// the open ends can be capped with fresh wood.
    /// </summary>
    public static class MeshSlicer
    {
        public struct Vert
        {
            public Vector3 P, N;
            public Vector2 UV;

            public static Vert Lerp(Vert a, Vert b, float t) => new Vert { P = Vector3.Lerp(a.P, b.P, t), N = Vector3.Lerp(a.N, b.N, t), UV = Vector2.Lerp(a.UV, b.UV, t) };
        }

        /// <summary>One piece's triangles, kept by material (a sub-mesh each).</summary>
        public sealed class Piece
        {
            sealed class Part
            {
                public readonly List<Vector3> P = new List<Vector3>(), N = new List<Vector3>();
                public readonly List<Vector2> UV = new List<Vector2>();
                public readonly List<int> T = new List<int>();
            }

            readonly List<Material> materials = new List<Material>();
            readonly List<Part> parts = new List<Part>();

            public bool Empty => parts.Count == 0;

            Part For(Material m)
            {
                int i = materials.IndexOf(m);
                if (i >= 0) return parts[i];
                materials.Add(m);
                var p = new Part();
                parts.Add(p);
                return p;
            }

            public void Add(Material m, Vert a, Vert b, Vert c)
            {
                var p = For(m);
                int i = p.P.Count;
                foreach (var v in new[] { a, b, c })
                {
                    p.P.Add(v.P);
                    p.N.Add(v.N);
                    p.UV.Add(v.UV);
                }
                p.T.Add(i);
                p.T.Add(i + 1);
                p.T.Add(i + 2);
            }

            /// <summary>The mesh, moved so <paramref name="origin"/> is its pivot, and its materials (one per sub-mesh).</summary>
            public Mesh ToMesh(string name, Vector3 origin, out Material[] mats)
            {
                var mesh = new Mesh { name = name };
                var p = new List<Vector3>();
                var n = new List<Vector3>();
                var uv = new List<Vector2>();
                foreach (var part in parts)
                {
                    foreach (var v in part.P) p.Add(v - origin);
                    n.AddRange(part.N);
                    uv.AddRange(part.UV);
                }
                if (p.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(p);
                mesh.SetNormals(n);
                mesh.SetUVs(0, uv);
                mesh.subMeshCount = parts.Count;
                int start = 0;
                for (int s = 0; s < parts.Count; s++)
                {
                    var tris = new List<int>(parts[s].T.Count);
                    foreach (int t in parts[s].T) tris.Add(t + start);
                    mesh.SetTriangles(tris, s);
                    start += parts[s].P.Count;
                }
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                mats = materials.ToArray();
                return mesh;
            }
        }

        /// <summary>
        /// Cut one sub-mesh, placed by <paramref name="toTree"/>, at the heights <paramref name="cuts"/> (rising) into
        /// pieces[0] (below the first cut), pieces[1] (between the first two) and so on. Where a cut crosses it, the
        /// points go into <paramref name="cutPoints"/>[cut] (if given).
        /// </summary>
        public static void Slice(Mesh mesh, int submesh, Matrix4x4 toTree, Material material, float[] cuts, Piece[] pieces, List<Vector3>[] cutPoints)
        {
            var vs = mesh.vertices;
            var ns = mesh.normals;
            var uvs = mesh.uv;
            var verts = new Vert[vs.Length];
            for (int i = 0; i < vs.Length; i++)
                verts[i] = new Vert
                {
                    P = toTree.MultiplyPoint3x4(vs[i]),
                    N = ns != null && ns.Length == vs.Length ? toTree.MultiplyVector(ns[i]).normalized : Vector3.up,
                    UV = uvs != null && uvs.Length == vs.Length ? uvs[i] : Vector2.zero,
                };
            var tris = mesh.GetTriangles(submesh);
            var poly = new List<Vert>(8);
            var clipped = new List<Vert>(8);
            for (int t = 0; t + 2 < tris.Length; t += 3)
            {
                Vert a = verts[tris[t]], b = verts[tris[t + 1]], c = verts[tris[t + 2]];
                float lo = Mathf.Min(a.P.y, Mathf.Min(b.P.y, c.P.y)), hi = Mathf.Max(a.P.y, Mathf.Max(b.P.y, c.P.y));
                // The cuts it crosses: the points where they do.
                if (cutPoints != null)
                    for (int k = 0; k < cuts.Length; k++)
                        if (lo < cuts[k] && hi > cuts[k])
                        {
                            Cross(a, b, cuts[k], cutPoints[k]);
                            Cross(b, c, cuts[k], cutPoints[k]);
                            Cross(c, a, cuts[k], cutPoints[k]);
                        }
                for (int i = 0; i < pieces.Length; i++)
                {
                    float from = i == 0 ? float.NegativeInfinity : cuts[i - 1];
                    float to = i == cuts.Length ? float.PositiveInfinity : cuts[i];
                    if (hi <= from || lo >= to) continue;
                    if (lo >= from && hi <= to)
                    {
                        pieces[i].Add(material, a, b, c);
                        continue;
                    }
                    poly.Clear();
                    poly.Add(a);
                    poly.Add(b);
                    poly.Add(c);
                    if (!float.IsInfinity(from)) { Clip(poly, clipped, from, true); Swap(ref poly, ref clipped); }
                    if (!float.IsInfinity(to)) { Clip(poly, clipped, to, false); Swap(ref poly, ref clipped); }
                    for (int k = 1; k + 1 < poly.Count; k++) pieces[i].Add(material, poly[0], poly[k], poly[k + 1]);
                }
            }
        }

        static void Swap(ref List<Vert> a, ref List<Vert> b)
        {
            var t = a;
            a = b;
            b = t;
        }

        static void Cross(Vert a, Vert b, float y, List<Vector3> into)
        {
            if ((a.P.y - y) * (b.P.y - y) >= 0f) return;
            into.Add(Vector3.Lerp(a.P, b.P, (y - a.P.y) / (b.P.y - a.P.y)));
        }

        // Keep the part of the polygon above (or below) height y.
        static void Clip(List<Vert> poly, List<Vert> into, float y, bool above)
        {
            into.Clear();
            for (int i = 0; i < poly.Count; i++)
            {
                Vert a = poly[i], b = poly[(i + 1) % poly.Count];
                bool aIn = above ? a.P.y >= y : a.P.y <= y;
                bool bIn = above ? b.P.y >= y : b.P.y <= y;
                if (aIn) into.Add(a);
                if (aIn != bIn) into.Add(Vert.Lerp(a, b, (y - a.P.y) / (b.P.y - a.P.y)));
            }
        }

        /// <summary>
        /// Where the trunk is at a cut, from the points where the cut crossed the wood: the middle and the radius
        /// (most of the points are the trunk's ring; a limb crossing near it doesn't throw it far off).
        /// </summary>
        public static bool TrunkAt(List<Vector3> points, float y, out Vector3 centre, out float radius) =>
            TrunkAt(points, y, Vector3.zero, float.MaxValue, out centre, out radius);

        /// <summary>
        /// The trunk where a cut crosses it, looking only near <paramref name="guess"/> (within
        /// <paramref name="reach"/>, across): a branch the cut also goes through, off to one side, isn't taken for
        /// part of the trunk (which would drag its middle off and swell it).
        /// </summary>
        public static bool TrunkAt(List<Vector3> points, float y, Vector3 guess, float reach, out Vector3 centre, out float radius)
        {
            centre = new Vector3(guess.x, y, guess.z);
            radius = 0f;
            if (points == null || points.Count < 3) return false;
            if (reach < float.MaxValue)
            {
                var near = new List<Vector3>();
                foreach (var p in points)
                    if (new Vector2(p.x - guess.x, p.z - guess.z).sqrMagnitude <= reach * reach) near.Add(p);
                if (near.Count >= 3) points = near;
            }
            // Start from the middle of them, then keep to the ones near the middle.
            Vector3 c = Vector3.zero;
            foreach (var p in points) c += p;
            c /= points.Count;
            var d = new List<float>(points.Count);
            for (int pass = 0; pass < 2; pass++)
            {
                d.Clear();
                foreach (var p in points) d.Add(new Vector2(p.x - c.x, p.z - c.z).magnitude);
                d.Sort();
                float near = d[d.Count / 2] * 1.6f;
                Vector3 sum = Vector3.zero;
                int n = 0;
                foreach (var p in points)
                    if (new Vector2(p.x - c.x, p.z - c.z).magnitude <= near) { sum += p; n++; }
                if (n > 0) c = sum / n;
            }
            d.Clear();
            foreach (var p in points) d.Add(new Vector2(p.x - c.x, p.z - c.z).magnitude);
            d.Sort();
            centre = new Vector3(c.x, y, c.z);
            radius = d[d.Count / 2] * 1.04f;
            if (reach < float.MaxValue) radius = Mathf.Min(radius, reach);
            return radius > 0.01f;
        }

        /// <summary>
        /// A cap of fresh wood over a cut end: a disc at height y, its middle torn up (or down) into splinters by
        /// <paramref name="jag"/> (0 a clean saw cut).
        /// </summary>
        public static void Cap(Piece piece, Material wood, Vector3 centre, float radius, bool up, float jag, System.Random rng)
        {
            const int sides = 12;
            float s = up ? 1f : -1f;
            Vector3 n = Vector3.up * s;
            Vert V(Vector3 p, Vector2 uv) => new Vert { P = p, N = n, UV = uv };
            var outer = new Vert[sides];
            var inner = new Vert[sides];
            for (int i = 0; i < sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                outer[i] = V(centre + dir * radius * 0.98f, new Vector2(0.5f + dir.x * 0.5f, 0.5f + dir.z * 0.5f));
                float lift = jag * radius * (float)rng.NextDouble() * (rng.NextDouble() < 0.5 ? 1.6f : 0.4f) * s;
                inner[i] = V(centre + dir * radius * 0.55f + Vector3.up * lift, new Vector2(0.5f + dir.x * 0.27f, 0.5f + dir.z * 0.27f));
            }
            var mid = V(centre + Vector3.up * (jag * radius * 0.8f * (float)rng.NextDouble() * s), new Vector2(0.5f, 0.5f));
            for (int i = 0; i < sides; i++)
            {
                int j = (i + 1) % sides;
                piece.Add(wood, mid, inner[j], inner[i]);
                piece.Add(wood, inner[i], inner[j], outer[i]);
                piece.Add(wood, inner[j], outer[j], outer[i]);
            }
        }
    }
}
