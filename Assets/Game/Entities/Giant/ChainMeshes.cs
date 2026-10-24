using System.Collections.Generic;
using UnityEngine;

namespace Game.Entities
{
    /// <summary>
    /// The giant's chains, made in code: heavy oval links that interlock (a run of them as one mesh), the open neck
    /// shackle hanging from the end of an empty chain, and the closed collar round the giant's own neck. All in
    /// units of the chain's thickness (a link is 1 wide), built once and shared.
    /// </summary>
    public static class ChainMeshes
    {
        // A link: an oval of rod, its long way along +Y, its ring in the XY plane, 1 wide and 1.7 long.
        const float Straight = 0.35f, Bend = 0.33f, Rod = 0.17f;
        /// <summary>How far along the chain each link is from the last (they interlock).</summary>
        public const float Pitch = 2f * (Straight + Bend) - 2f * Rod;
        /// <summary>Links in one <see cref="Run"/>.</summary>
        public const int LinksPerRun = 8;
        public static float RunLength => LinksPerRun * Pitch;

        static Mesh run, shackle, collar, stub;

        /// <summary>Eight links running up +Y from the origin, each turned a quarter from the last.</summary>
        public static Mesh Run => run != null ? run : run = Links(LinksPerRun, "Chain links");

        /// <summary>A few links, as broken off (hanging from the giant's collar after it falls).</summary>
        public static Mesh Stub => stub != null ? stub : stub = Links(3, "Broken chain");

        /// <summary>
        /// An open neck shackle, hanging: its top (where the chain holds it) at the origin, a ring about 2.2 across in
        /// the XY plane below, hinged at the top, its two halves swung apart at the bottom, each ending in a lock eye.
        /// </summary>
        public static Mesh OpenShackle
        {
            get
            {
                if (shackle != null) return shackle;
                var parts = new List<CombineInstance>();
                const float r = 1.1f, rod = 0.2f;
                var hinge = new Vector3(0f, -0.75f, 0f);
                Vector3 centre = hinge + Vector3.down * r;
                for (int side = -1; side <= 1; side += 2)
                {
                    // Half the ring, from the hinge round to the bottom, swung out about the hinge.
                    var half = new List<Vector3>();
                    for (int i = 0; i <= 14; i++)
                    {
                        float a = Mathf.PI * 0.5f + side * Mathf.PI * i / 14f;
                        half.Add(centre + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * r);
                    }
                    // (side -1 is the right half: it swings out to the right, and the left half to the left)
                    var swing = Matrix4x4.Translate(hinge) * Matrix4x4.Rotate(Quaternion.Euler(0f, 0f, -side * 38f)) * Matrix4x4.Translate(-hinge);
                    parts.Add(new CombineInstance { mesh = Tube(half, false, rod, Vector3.forward), transform = swing });
                    // The lock eye on its free end.
                    var eye = Ring(Vector3.zero, 0.28f, 0.1f, Vector3.right);
                    Vector3 end = half[half.Count - 1];
                    parts.Add(new CombineInstance { mesh = eye, transform = swing * Matrix4x4.Translate(end + new Vector3(0f, -0.2f, 0f)) });
                }
                // The hinge pin, and the ring the chain's last link holds.
                parts.Add(new CombineInstance { mesh = Tube(new List<Vector3> { hinge + Vector3.back * 0.35f, hinge + Vector3.forward * 0.35f }, false, 0.24f, Vector3.up), transform = Matrix4x4.identity });
                parts.Add(new CombineInstance { mesh = Ring(new Vector3(0f, -0.38f, 0f), 0.36f, 0.13f, Vector3.right), transform = Matrix4x4.identity });
                shackle = Combine(parts, "Open shackle");
                return shackle;
            }
        }

        /// <summary>
        /// The closed collar round a neck: a thick ring 1 across (its middle line) in the XZ plane, a bolt through
        /// its front, and an eye at the back (-Z) for the chain, its top at (0, 0.5, -1.25).
        /// </summary>
        public static Mesh Collar
        {
            get
            {
                if (collar != null) return collar;
                var parts = new List<CombineInstance>
                {
                    new CombineInstance { mesh = Ring(Vector3.zero, 1f, 0.24f, Vector3.up), transform = Matrix4x4.identity },
                    new CombineInstance { mesh = Ring(new Vector3(0f, 0.2f, -1.2f), 0.3f, 0.11f, Vector3.right), transform = Matrix4x4.identity },
                    new CombineInstance { mesh = Tube(new List<Vector3> { new Vector3(-0.3f, 0f, 1.05f), new Vector3(0.3f, 0f, 1.05f) }, false, 0.3f, Vector3.up), transform = Matrix4x4.identity },
                };
                collar = Combine(parts, "Collar");
                return collar;
            }
        }

        static Mesh Links(int count, string name)
        {
            var link = new List<Vector3>();
            for (int i = 0; i < 24; i++)
            {
                float a = i / 24f * Mathf.PI * 2f;
                float y = (a < Mathf.PI ? Straight : -Straight) + Mathf.Sin(a) * Bend;
                link.Add(new Vector3(Mathf.Cos(a) * Bend, y, 0f));
            }
            var linkMesh = Tube(link, true, Rod, Vector3.forward);
            var parts = new List<CombineInstance>();
            for (int i = 0; i < count; i++)
            {
                var m = Matrix4x4.TRS(new Vector3(0f, Straight + Bend + Rod + i * Pitch, 0f), Quaternion.Euler(0f, i % 2 == 0 ? 0f : 90f, 0f), Vector3.one);
                parts.Add(new CombineInstance { mesh = linkMesh, transform = m });
            }
            var mesh = Combine(parts, name);
            Object.Destroy(linkMesh);
            return mesh;
        }

        static Mesh Ring(Vector3 centre, float radius, float rod, Vector3 axis)
        {
            Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized, v = Vector3.Cross(axis, u);
            var path = new List<Vector3>();
            for (int i = 0; i < 20; i++)
            {
                float a = i / 20f * Mathf.PI * 2f;
                path.Add(centre + (u * Mathf.Cos(a) + v * Mathf.Sin(a)) * radius);
            }
            return Tube(path, true, rod, axis);
        }

        // A round rod swept along a path (closed: back round to the start). The path lies roughly in the plane
        // across <paramref name="planeNormal"/>, which keeps the rod's twist steady.
        static Mesh Tube(List<Vector3> path, bool closed, float radius, Vector3 planeNormal)
        {
            const int sides = 8;
            int n = path.Count;
            var verts = new List<Vector3>();
            var tris = new List<int>();
            for (int i = 0; i < n; i++)
            {
                Vector3 prev = path[closed ? (i - 1 + n) % n : Mathf.Max(0, i - 1)], next = path[closed ? (i + 1) % n : Mathf.Min(n - 1, i + 1)];
                Vector3 t = (next - prev).normalized;
                Vector3 a = Vector3.Cross(t, planeNormal);
                if (a.sqrMagnitude < 1e-4f) a = Vector3.Cross(t, Vector3.right);
                a.Normalize();
                Vector3 b = Vector3.Cross(t, a);
                for (int k = 0; k < sides; k++)
                {
                    float ang = k / (float)sides * Mathf.PI * 2f;
                    verts.Add(path[i] + (a * Mathf.Cos(ang) + b * Mathf.Sin(ang)) * radius);
                }
            }
            int rings = closed ? n : n - 1;
            for (int i = 0; i < rings; i++)
            {
                int i2 = (i + 1) % n;
                for (int k = 0; k < sides; k++)
                {
                    int k2 = (k + 1) % sides;
                    int p0 = i * sides + k, p1 = i * sides + k2, q0 = i2 * sides + k, q1 = i2 * sides + k2;
                    tris.Add(p0); tris.Add(q0); tris.Add(p1);
                    tris.Add(p1); tris.Add(q0); tris.Add(q1);
                }
            }
            if (!closed)
                for (int end = 0; end < 2; end++)
                {
                    int ring = end == 0 ? 0 : n - 1, centre = verts.Count;
                    verts.Add(path[ring]);
                    for (int k = 0; k < sides; k++)
                    {
                        int k2 = (k + 1) % sides;
                        if (end == 0) { tris.Add(centre); tris.Add(ring * sides + k); tris.Add(ring * sides + k2); }
                        else { tris.Add(centre); tris.Add(ring * sides + k2); tris.Add(ring * sides + k); }
                    }
                }
            var mesh = new Mesh { name = "Chain part" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        static Mesh Combine(List<CombineInstance> parts, string name)
        {
            var mesh = new Mesh { name = name };
            mesh.CombineMeshes(parts.ToArray(), true, true);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
