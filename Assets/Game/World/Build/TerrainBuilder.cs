using System.Collections.Generic;
using Game.Core;
using Game.World.Generation;
using UnityEngine;

namespace Game.World.Build
{
    /// <summary>
    /// The ground as 32 m chunks (1 m between vertices, normals from the heightfield so chunks meet without a
    /// seam), each with its collider and the worn stretches of trail on it; and the ponds' water.
    /// </summary>
    public static class TerrainBuilder
    {
        public const float ChunkSize = 32f;

        /// <summary>Make every chunk under <paramref name="root"/>. Returns them (each at its home place).</summary>
        public static List<Transform> BuildChunks(WorldPlan plan, Transform root, Material ground, Material trail, List<Object> made)
        {
            var chunks = new List<Transform>();
            int across = Mathf.CeilToInt(plan.Size / ChunkSize);
            var trailParts = SplitTrails(plan, across);
            // The texture repeats a whole number of times across the map, so it wraps without a seam.
            float uvTile = plan.Size / Mathf.Round(plan.Size / 3f);

            for (int cz = 0; cz < across; cz++)
            for (int cx = 0; cx < across; cx++)
            {
                var origin = new Vector3(cx * ChunkSize, 0f, cz * ChunkSize);
                var go = new GameObject($"Ground {cx},{cz}") { layer = Layers.Ground };
                go.transform.SetParent(root, false);
                go.transform.position = origin;

                var mesh = GroundMesh(plan, origin, uvTile);
                made.Add(mesh);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = ground;
                go.AddComponent<MeshCollider>().sharedMesh = mesh;

                var parts = trailParts[cz * across + cx];
                if (parts != null && parts.VertexCount > 0)
                {
                    var trailMesh = parts.ToMesh($"Trail {cx},{cz}");
                    made.Add(trailMesh);
                    var t = new GameObject("Trail") { layer = Layers.Ground };
                    t.transform.SetParent(go.transform, false);
                    t.AddComponent<MeshFilter>().sharedMesh = trailMesh;
                    var r = t.AddComponent<MeshRenderer>();
                    r.sharedMaterial = trail;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                chunks.Add(go.transform);
            }
            return chunks;
        }

        static Mesh GroundMesh(WorldPlan plan, Vector3 origin, float uvTile)
        {
            int n = Mathf.RoundToInt(ChunkSize);
            var verts = new Vector3[(n + 1) * (n + 1)];
            var normals = new Vector3[verts.Length];
            var uvs = new Vector2[verts.Length];
            var h = plan.Height;
            for (int z = 0; z <= n; z++)
            for (int x = 0; x <= n; x++)
            {
                float wx = origin.x + x, wz = origin.z + z;
                int i = z * (n + 1) + x;
                verts[i] = new Vector3(x, h.Sample(wx, wz), z);
                normals[i] = new Vector3(h.Sample(wx - 1f, wz) - h.Sample(wx + 1f, wz), 2f, h.Sample(wx, wz - 1f) - h.Sample(wx, wz + 1f)).normalized;
                uvs[i] = new Vector2(wx / uvTile, wz / uvTile);
            }
            var tris = new int[n * n * 6];
            int t = 0;
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int a = z * (n + 1) + x;
                tris[t++] = a; tris[t++] = a + n + 1; tris[t++] = a + 1;
                tris[t++] = a + 1; tris[t++] = a + n + 1; tris[t++] = a + n + 2;
            }
            var mesh = new Mesh { name = $"Ground {origin.x},{origin.z}", vertices = verts, normals = normals, uv = uvs, triangles = tris };
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        // Each worn stretch of trail as a ribbon of packed earth just above the ground, cut up by chunk.
        static MeshBuilder[] SplitTrails(WorldPlan plan, int across)
        {
            var parts = new MeshBuilder[across * across];
            foreach (var trail in plan.Trails)
            {
                float half = trail.Width * 0.5f;
                for (int i = 0; i < trail.Points.Count - 1; i++)
                {
                    if (!trail.Worn[i] || !trail.Worn[i + 1]) continue;
                    Vector2 a = trail.Points[i];
                    Vector2 b = a + Wrap.Delta(a, trail.Points[i + 1], plan.Size);
                    Vector2 mid = Wrap.Into((a + b) * 0.5f, plan.Size);
                    int cx = Mathf.Clamp(Mathf.FloorToInt(mid.x / ChunkSize), 0, across - 1);
                    int cz = Mathf.Clamp(Mathf.FloorToInt(mid.y / ChunkSize), 0, across - 1);
                    var origin = new Vector2(cx * ChunkSize, cz * ChunkSize);
                    // Local to the chunk, the short way round (so a stretch over the map's edge stays whole).
                    Vector2 la = Wrap.Delta(origin, a, plan.Size), lb = la + (b - a);
                    Vector2 dir = (lb - la).sqrMagnitude > 1e-4f ? (lb - la).normalized : Vector2.right;
                    Vector2 side = new Vector2(-dir.y, dir.x) * half;
                    var p = parts[cz * across + cx] ??= new MeshBuilder();
                    int v0 = p.AddVertex(Lift(plan, origin, la - side), new Vector2(0f, i));
                    int v1 = p.AddVertex(Lift(plan, origin, la + side), new Vector2(1f, i));
                    int v2 = p.AddVertex(Lift(plan, origin, lb - side), new Vector2(0f, i + 1));
                    int v3 = p.AddVertex(Lift(plan, origin, lb + side), new Vector2(1f, i + 1));
                    // Face up whichever way the stretch runs.
                    if (Vector3.Cross(Lift(plan, origin, lb - side) - Lift(plan, origin, la - side), Lift(plan, origin, la + side) - Lift(plan, origin, la - side)).y > 0f)
                    {
                        p.AddTriangle(v0, v2, v1);
                        p.AddTriangle(v1, v2, v3);
                    }
                    else
                    {
                        p.AddTriangle(v0, v1, v2);
                        p.AddTriangle(v1, v3, v2);
                    }
                }
            }
            // The lamps' patches: a ragged disc each, in rings so it follows the ground.
            foreach (var patch in plan.Patches)
            {
                const int segments = 28, rings = 3;
                int cx = Mathf.Clamp(Mathf.FloorToInt(patch.Centre.x / ChunkSize), 0, across - 1);
                int cz = Mathf.Clamp(Mathf.FloorToInt(patch.Centre.y / ChunkSize), 0, across - 1);
                var origin = new Vector2(cx * ChunkSize, cz * ChunkSize);
                Vector2 c = Wrap.Delta(origin, patch.Centre, plan.Size);
                var p = parts[cz * across + cx] ??= new MeshBuilder();
                int centre = p.AddVertex(Lift(plan, origin, c, 0.035f), c * 0.5f);
                int first = -1;
                for (int r = 1; r <= rings; r++)
                {
                    for (int i = 0; i < segments; i++)
                    {
                        float a = i / (float)segments * Mathf.PI * 2f;
                        Vector2 at = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * patch.RadiusAt(a) * (r / (float)rings);
                        int v = p.AddVertex(Lift(plan, origin, at, 0.035f), at * 0.5f);
                        if (r == 1 && i == 0) first = v;
                    }
                }
                // (Clockwise seen from above: Unity's front faces.)
                for (int i = 0; i < segments; i++) p.AddTriangle(centre, first + (i + 1) % segments, first + i);
                for (int r = 1; r < rings; r++)
                {
                    int inner = first + (r - 1) * segments, outer = first + r * segments;
                    for (int i = 0; i < segments; i++)
                    {
                        int j = (i + 1) % segments;
                        p.AddTriangle(inner + i, inner + j, outer + i);
                        p.AddTriangle(inner + j, outer + j, outer + i);
                    }
                }
            }
            return parts;
        }

        static Vector3 Lift(WorldPlan plan, Vector2 origin, Vector2 local, float above)
        {
            Vector2 w = origin + local;
            return new Vector3(local.x, plan.Height.Sample(w.x, w.y) + above, local.y);
        }

        static Vector3 Lift(WorldPlan plan, Vector2 origin, Vector2 local)
        {
            Vector2 w = origin + local;
            return new Vector3(local.x, plan.Height.Sample(w.x, w.y) + 0.03f, local.y);
        }

        /// <summary>A pond's water: a disc at its level, a little wider than the pond so its edge is under the bank.</summary>
        public static GameObject Water(Pond pond, Material water, List<Object> made)
        {
            const int segments = 40;
            var verts = new Vector3[segments + 1];
            var tris = new int[segments * 3];
            float r = pond.Radius + 1.5f;
            for (int i = 0; i < segments; i++)
            {
                float a = i / (float)segments * Mathf.PI * 2f;
                verts[i + 1] = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
                // Clockwise seen from above (Unity's front faces).
                tris[i * 3] = 0;
                tris[i * 3 + 1] = (i + 1) % segments + 1;
                tris[i * 3 + 2] = i + 1;
            }
            var mesh = new Mesh { name = "Pond", vertices = verts, triangles = tris };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            made.Add(mesh);
            var go = new GameObject("Pond") { layer = Layers.Default };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = water;
            return go;
        }
    }
}
