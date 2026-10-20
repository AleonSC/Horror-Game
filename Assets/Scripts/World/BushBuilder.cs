using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Builds leafy broadleaf bushes out of leaf cards: quads each carrying a spray of pointed leaves
/// (ProceduralTextures.LeafCluster, alpha-cutout), heaped into clumps on a skeleton of stems, lit as one
/// soft rounded mass. Three kinds, each varied every time:
/// - Mound: a low, wide, dense dome of leaves right down to the ground;
/// - Upright: a little tree of a bush, a short trunk with the leaves in rough tiers, taller than wide;
/// - Leaning: several thin pale stems splaying up from the ground and leaning one way, a top-heavy crown.
/// About 1m across at scale 1. Submesh 0 is the leaves; submesh 1 (when there is one) the stems.
/// DarkForestGenerator uses these for its bushes.
/// </summary>
public static class BushBuilder
{
    public enum Shape { Mound, Upright, Leaning }

    struct Clump
    {
        public Vector3 center;
        public float radius;
        public int cards;
    }

    public static Mesh Build(Shape shape, System.Random rng)
    {
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        Vector3 InSphere() { Vector3 v; do v = new Vector3(R(-1f, 1f), R(-1f, 1f), R(-1f, 1f)); while (v.sqrMagnitude > 1f); return v; }

        var stems = new List<(List<Vector3> points, float radius)>();
        var clumps = new List<Clump>();
        Vector3 core; // the middle of the leaf mass: leaves are lit as if it were one rounded shape round here

        if (shape == Shape.Mound)
        {
            float w = R(1.0f, 1.4f), h = R(0.7f, 0.95f);
            core = new Vector3(0f, h * 0.38f, 0f);
            int count = rng.Next(15, 20);
            for (int i = 0; i < count; i++)
            {
                // Over the outside of a squashed dome, right down to the ground.
                Vector3 d = InSphere().normalized;
                d.y = Mathf.Abs(d.y) * 0.9f + 0.05f;
                float f = R(0.55f, 0.9f);
                clumps.Add(new Clump { center = new Vector3(d.x * w * 0.5f * f, d.y * h * f, d.z * w * 0.5f * f), radius = R(0.24f, 0.34f), cards = 7 });
            }
        }
        else if (shape == Shape.Upright)
        {
            float w = R(0.8f, 1.1f), h = R(1.2f, 1.6f);
            core = new Vector3(0f, h * 0.6f, 0f);
            float fork = h * R(0.25f, 0.35f);
            stems.Add((new List<Vector3> { Vector3.zero, new Vector3(R(-0.03f, 0.03f), fork, R(-0.03f, 0.03f)) }, 0.035f));
            int branches = rng.Next(4, 7);
            for (int i = 0; i < branches; i++)
            {
                float a = i / (float)branches * Mathf.PI * 2f + R(-0.4f, 0.4f);
                Vector3 outward = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 mid = new Vector3(0f, fork, 0f) + outward * w * 0.18f + Vector3.up * h * 0.25f;
                Vector3 tip = new Vector3(0f, fork, 0f) + outward * w * R(0.3f, 0.5f) + Vector3.up * h * R(0.45f, 0.7f);
                stems.Add((new List<Vector3> { new Vector3(0f, fork, 0f), mid, tip }, 0.018f));
            }
            // The leaves in rough tiers, ragged at the edges, a few sprays sticking out.
            float[] tiers = { 0.42f, 0.66f, 0.88f };
            foreach (float tier in tiers)
            {
                int ring = rng.Next(5, 8);
                float spreadOut = Mathf.Lerp(0.42f, 0.28f, (tier - 0.42f) / 0.46f) * w;
                for (int i = 0; i < ring; i++)
                {
                    float a = i / (float)ring * Mathf.PI * 2f + R(-0.5f, 0.5f);
                    Vector3 c = new Vector3(Mathf.Cos(a) * spreadOut * R(0.5f, 1.1f), h * tier + R(-0.08f, 0.08f), Mathf.Sin(a) * spreadOut * R(0.5f, 1.1f));
                    clumps.Add(new Clump { center = c, radius = R(0.2f, 0.28f), cards = 6 });
                }
            }
            for (int i = 0; i < 3; i++)
            {
                float a = R(0f, Mathf.PI * 2f);
                clumps.Add(new Clump { center = new Vector3(Mathf.Cos(a) * w * 0.55f, h * R(0.6f, 0.95f), Mathf.Sin(a) * w * 0.55f), radius = 0.16f, cards = 3 });
            }
        }
        else
        {
            float w = R(0.9f, 1.2f), h = R(1.3f, 1.7f);
            float leanAngle = R(0f, Mathf.PI * 2f);
            Vector3 lean = new Vector3(Mathf.Cos(leanAngle), 0f, Mathf.Sin(leanAngle)) * h * R(0.2f, 0.35f);
            Vector3 crown = Vector3.up * h * 0.72f + lean;
            core = crown;
            // Thin stems from almost the same spot, splaying, all leaning the same way.
            int count = rng.Next(3, 6);
            for (int i = 0; i < count; i++)
            {
                float a = i / (float)count * Mathf.PI * 2f + R(-0.5f, 0.5f);
                Vector3 splay = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 baseAt = splay * 0.03f;
                Vector3 mid = splay * w * 0.12f + Vector3.up * h * 0.4f + lean * 0.4f;
                Vector3 tip = crown + splay * w * R(0.15f, 0.3f) + Vector3.up * h * R(-0.05f, 0.15f);
                stems.Add((new List<Vector3> { baseAt, mid, tip }, 0.016f));
            }
            // A top-heavy, lopsided crown, and a few lower sprays along the stems.
            int crownClumps = rng.Next(12, 16);
            for (int i = 0; i < crownClumps; i++)
            {
                Vector3 d = InSphere();
                clumps.Add(new Clump { center = crown + new Vector3(d.x * w * 0.5f, d.y * h * 0.25f, d.z * w * 0.5f) + lean * 0.2f * d.x, radius = R(0.22f, 0.3f), cards = 7 });
            }
            for (int i = 0; i < 3; i++)
            {
                var s = stems[rng.Next(stems.Count)].points;
                Vector3 at = Vector3.Lerp(s[1], s[2], R(0.2f, 0.6f));
                clumps.Add(new Clump { center = at, radius = 0.17f, cards = 4 });
            }
        }

        // ---- Leaf cards.
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var leafTris = new List<int>();
        foreach (var clump in clumps)
        {
            for (int k = 0; k < clump.cards; k++)
            {
                Vector3 pos = clump.center + Vector3.Scale(InSphere(), new Vector3(1f, 0.8f, 1f)) * clump.radius;
                Vector3 outward = pos - core;
                outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.up;
                // The spray points out and a little up from the middle of the bush, every one at its own angle.
                Vector3 up = (outward + Vector3.up * 0.5f + InSphere() * 0.6f).normalized;
                Vector3 face = Vector3.Cross(up, InSphere());
                if (face.sqrMagnitude < 0.01f) face = Vector3.Cross(up, Mathf.Abs(up.z) < 0.9f ? Vector3.forward : Vector3.right);
                face.Normalize();
                Vector3 right = Vector3.Cross(up, face).normalized;
                float size = R(0.36f, 0.5f);
                Vector3 bottom = pos - up * size * 0.2f;
                Vector3[] corners =
                {
                    bottom - right * size * 0.5f, bottom + right * size * 0.5f,
                    bottom + right * size * 0.5f + up * size, bottom - right * size * 0.5f + up * size,
                };
                Vector2[] cornerUvs = { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(0f, 1f) };
                for (int side = 0; side < 2; side++) // both faces (some pipelines won't draw the back)
                {
                    int start = verts.Count;
                    for (int c = 0; c < 4; c++)
                    {
                        verts.Add(corners[c]);
                        uvs.Add(cornerUvs[c]);
                        // Lit as one soft rounded mass, not as flat cards.
                        Vector3 n = (corners[c] - core).normalized * 0.8f + Vector3.up * 0.35f;
                        normals.Add(n.normalized);
                    }
                    if (side == 0) leafTris.AddRange(new[] { start, start + 2, start + 1, start, start + 3, start + 2 });
                    else leafTris.AddRange(new[] { start, start + 1, start + 2, start, start + 2, start + 3 });
                }
            }
        }

        // ---- Stems: thin tapering five-sided tubes.
        var stemTris = new List<int>();
        foreach (var (points, radius) in stems)
        {
            for (int i = 0; i < points.Count - 1; i++)
            {
                Vector3 a = points[i], b = points[i + 1];
                Vector3 dir = (b - a).normalized;
                Vector3 side1 = Vector3.Cross(dir, Mathf.Abs(dir.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                Vector3 side2 = Vector3.Cross(dir, side1);
                float r0 = radius * (1f - i / (float)points.Count * 0.6f), r1 = radius * (1f - (i + 1) / (float)points.Count * 0.6f);
                int start = verts.Count;
                const int around = 5;
                for (int j = 0; j <= around; j++)
                {
                    float ang = j / (float)around * Mathf.PI * 2f;
                    Vector3 n = side1 * Mathf.Cos(ang) + side2 * Mathf.Sin(ang);
                    verts.Add(a + n * r0); normals.Add(n); uvs.Add(new Vector2(j / (float)around, 0f));
                    verts.Add(b + n * r1); normals.Add(n); uvs.Add(new Vector2(j / (float)around, (b - a).magnitude * 3f));
                }
                for (int j = 0; j < around; j++)
                {
                    int q = start + j * 2;
                    stemTris.AddRange(new[] { q, q + 2, q + 1, q + 2, q + 3, q + 1 }); // wound to face outward
                }
            }
        }

        var mesh = new Mesh { name = shape + " Bush" };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = stemTris.Count > 0 ? 2 : 1;
        mesh.SetTriangles(leafTris, 0);
        if (stemTris.Count > 0) mesh.SetTriangles(stemTris, 1);
        mesh.RecalculateBounds();
        return mesh;
    }
}
