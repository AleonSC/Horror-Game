using System.Collections.Generic;
using Game.Core;
using Game.World.Generation;
using UnityEngine;

namespace Game.World.Build
{
    /// <summary>
    /// The ground as 32 m chunks (1 m between vertices, normals from the heightfield so chunks meet without a
    /// seam), each with its collider and the trodden earth of the trails and lamp patches on it; and the ponds' water.
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
            float uvTile = plan.Size / Mathf.Round(plan.Size / ForestSurfaces.FloorTile);
            float patchTile = plan.Size / Mathf.Round(plan.Size / PatchTile);

            for (int cz = 0; cz < across; cz++)
            for (int cx = 0; cx < across; cx++)
            {
                var origin = new Vector3(cx * ChunkSize, 0f, cz * ChunkSize);
                var go = new GameObject($"Ground {cx},{cz}") { layer = Layers.Ground };
                go.transform.SetParent(root, false);
                go.transform.position = origin;

                var mesh = GroundMesh(plan, origin, uvTile, patchTile);
                made.Add(mesh);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = ground;
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
                go.AddComponent<PanoramaGround>().mapSize = plan.Size;

                var trailMesh = trailParts[cz * across + cx];
                if (trailMesh != null)
                {
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

        static Mesh GroundMesh(WorldPlan plan, Vector3 origin, float uvTile, float patchTile)
        {
            int n = Mathf.RoundToInt(ChunkSize);
            var verts = new Vector3[(n + 1) * (n + 1)];
            var normals = new Vector3[verts.Length];
            var uvs = new Vector2[verts.Length];
            var patchUvs = new Vector2[verts.Length];
            var h = plan.Height;
            for (int z = 0; z <= n; z++)
            for (int x = 0; x <= n; x++)
            {
                float wx = origin.x + x, wz = origin.z + z;
                int i = z * (n + 1) + x;
                verts[i] = new Vector3(x, h.Sample(wx, wz), z);
                normals[i] = new Vector3(h.Sample(wx - 1f, wz) - h.Sample(wx + 1f, wz), 2f, h.Sample(wx, wz - 1f) - h.Sample(wx, wz + 1f)).normalized;
                uvs[i] = new Vector2(wx / uvTile, wz / uvTile);
                patchUvs[i] = new Vector2(wx / patchTile, wz / patchTile);
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
            var mesh = new Mesh { name = $"Ground {origin.x},{origin.z}", vertices = verts, normals = normals, uv = uvs, uv2 = patchUvs, triangles = tris };
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        // The trails' earth is drawn out to this far past a trail's edge, fading out over the last of it.
        public const float TrailFade = 0.4f;
        // ...solid to this far inside the edge.
        public const float TrailSolidInside = 0.15f;
        // The overlay's grid spacing (metres): fine enough to follow the edges.
        const float TrailStep = 0.25f;
        // The earth's texture repeats every this many metres.
        public const float TrailRepeat = 3f;

        // Inside a patch, the earth eases from the trail's mapping into its own over this many metres.
        const float PatchBlend = 0.5f;

        /// <summary>How often the forest floor's big patches (its detail map, on UV1) repeat (metres).</summary>
        const float PatchTile = 23f;

        /// <summary>The trails' earth: from the centre (0) to the edge of the fade (1), the share where it's solid.</summary>
        public static float TrailSolidShare(float trailWidth) => Mathf.Clamp01((trailWidth * 0.5f - TrailSolidInside) / (trailWidth * 0.5f + TrailFade));

        // All the worn trails and the lamps' patches as one surface per chunk, so where trails meet, cross or run
        // into a patch they join up as one: a fine grid over the chunk, each point knowing how far it is from the
        // nearest trail (or patch edge), and which, with a little noise so the edges wander. U is that distance
        // (0.5 on the middle line, 0 at the edge of the fade: the texture's alpha); V runs along whatever it's
        // nearest (a trail's length, round a patch's edge), so the earth never smears out where a trail runs one
        // way or another; UV1 is the ground itself (for the fine grain). Only the cells with earth in them are kept.
        static Mesh[] SplitTrails(WorldPlan plan, int across)
        {
            var meshes = new Mesh[across * across];
            if (plan.Trails.Count == 0 && plan.Patches.Count == 0) return meshes;
            float half = plan.Trails.Count > 0 ? plan.Trails[0].Width * 0.5f : 0.7f;
            float outer = half + TrailFade;
            int n = Mathf.RoundToInt(ChunkSize / TrailStep);
            int stride = n + 1;
            var dist = new float[across * across][];
            var owners = new int[across * across][];

            // What a point can be nearest: a trail's stretch (owner >= 0, with how far along its trail it starts),
            // or a patch (owner -1 - its index).
            var segA = new List<Vector2>();
            var segB = new List<Vector2>();
            var segAlong = new List<float>();
            for (int ti = 0; ti < plan.Trails.Count; ti++)
            {
                var trail = plan.Trails[ti];
                float along = ti * 7.31f; // (each trail its own stretch of the texture)
                for (int i = 0; i < trail.Points.Count - 1; i++)
                {
                    Vector2 a = trail.Points[i];
                    Vector2 b = a + Wrap.Delta(a, trail.Points[i + 1], plan.Size);
                    if (trail.Worn[i] && trail.Worn[i + 1])
                    {
                        segA.Add(a); segB.Add(b); segAlong.Add(along);
                    }
                    along += (b - a).magnitude;
                }
            }

            int Chunk(int cx, int cz)
            {
                cx = ((cx % across) + across) % across;
                cz = ((cz % across) + across) % across;
                int i = cz * across + cx;
                if (dist[i] == null)
                {
                    dist[i] = new float[stride * stride];
                    owners[i] = new int[stride * stride];
                    for (int k = 0; k < dist[i].Length; k++) dist[i][k] = float.MaxValue;
                }
                return i;
            }
            // Stamp a shape's distance into every chunk its reach overlaps (unwrapped: a chunk index past the
            // map's edge is the chunk on the other side).
            void Stamp(Vector2 min, Vector2 max, int owner, System.Func<Vector2, float> distanceAt)
            {
                min -= Vector2.one * outer;
                max += Vector2.one * outer;
                for (int cz = Mathf.FloorToInt(min.y / ChunkSize); cz <= Mathf.FloorToInt(max.y / ChunkSize); cz++)
                for (int cx = Mathf.FloorToInt(min.x / ChunkSize); cx <= Mathf.FloorToInt(max.x / ChunkSize); cx++)
                {
                    int ci = Chunk(cx, cz);
                    var field = dist[ci];
                    var own = owners[ci];
                    var origin = new Vector2(cx * ChunkSize, cz * ChunkSize);
                    int x0 = Mathf.Clamp(Mathf.FloorToInt((min.x - origin.x) / TrailStep), 0, n), x1 = Mathf.Clamp(Mathf.CeilToInt((max.x - origin.x) / TrailStep), 0, n);
                    int z0 = Mathf.Clamp(Mathf.FloorToInt((min.y - origin.y) / TrailStep), 0, n), z1 = Mathf.Clamp(Mathf.CeilToInt((max.y - origin.y) / TrailStep), 0, n);
                    for (int z = z0; z <= z1; z++)
                    for (int x = x0; x <= x1; x++)
                    {
                        float d = distanceAt(origin + new Vector2(x * TrailStep, z * TrailStep));
                        int k = z * stride + x;
                        if (d < field[k]) { field[k] = d; own[k] = owner; }
                    }
                }
            }

            for (int s = 0; s < segA.Count; s++)
            {
                Vector2 a = segA[s], b = segB[s];
                Stamp(Vector2.Min(a, b), Vector2.Max(a, b), s, p => Segment(p, a, b) - half);
            }
            // A patch: inside its ragged edge counts as on a trail (its edge where a trail's would be).
            for (int pi = 0; pi < plan.Patches.Count; pi++)
            {
                var patch = plan.Patches[pi];
                Vector2 c = patch.Centre;
                float reach = patch.Radius * 1.25f;
                Stamp(c - Vector2.one * reach, c + Vector2.one * reach, -1 - pi, p =>
                {
                    Vector2 d = p - c;
                    return d.magnitude - patch.RadiusAt(Mathf.Atan2(d.y, d.x));
                });
            }

            // (Inside a patch: how far U goes over the solid earth, just short of where it starts to fade, and
            // the metres it takes to get there: the texture's own scale, a little drawn out.)
            float solid = 0.9f * TrailSolidShare(half * 2f);
            float patchSpan = Mathf.Max(0.3f, solid * outer * 1.5f);
            // Directions over the open middle of a patch, bent just enough to repeat a whole number of times
            // across the map, so they meet themselves at its edge.
            float Periodic(float c, float repeat) => Mathf.Max(1f, Mathf.Round(c * plan.Size / repeat)) * repeat / plan.Size;
            Vector2 alongDir = new Vector2(Periodic(0.83f, TrailRepeat), Periodic(0.56f, TrailRepeat));
            Vector2 sideDir = new Vector2(-Periodic(0.56f, patchSpan * 2f), Periodic(0.83f, patchSpan * 2f));
            int edgeSeed = plan.Seed * 13 + 5;
            int fine = Mathf.Max(1, Mathf.RoundToInt(plan.Size / 0.8f)), coarse = Mathf.Max(1, Mathf.RoundToInt(plan.Size / 3f));
            var h = plan.Height;
            for (int c = 0; c < dist.Length; c++)
            {
                var field = dist[c];
                if (field == null) continue;
                var own = owners[c];
                int cx = c % across, cz = c / across;
                var origin = new Vector2(cx * ChunkSize, cz * ChunkSize);
                // Signed distance from the trail's edge, plus wander: 0 at the edge, -half on the middle line.
                var edge = new float[field.Length];
                for (int z = 0; z <= n; z++)
                for (int x = 0; x <= n; x++)
                {
                    int k = z * stride + x;
                    if (field[k] > TrailFade + 0.5f) { edge[k] = float.MaxValue; continue; }
                    float wx = origin.x + x * TrailStep, wz = origin.y + z * TrailStep;
                    float wander = (PeriodicNoise.Sample(wx, wz, plan.Size, coarse, edgeSeed) - 0.5f) * 0.45f
                                 + (PeriodicNoise.Sample(wx, wz, plan.Size, fine, edgeSeed + 1) - 0.5f) * 0.2f;
                    edge[k] = field[k] + wander;
                }

                // A point's UV as one owner has it. (inner: the open middle of a patch, mapped flat over the ground.)
                Vector2 Uv(int k, int owner, bool inner, float wx, float wz)
                {
                    float fromMiddle = edge[k] + half; // (below 0: deeper in than a trail's middle line, inside a patch)
                    float u = 0.5f - 0.5f * Mathf.Clamp01(Mathf.Max(0f, fromMiddle) / outer);
                    var p = new Vector2(wx, wz);
                    if (owner >= 0)
                    {
                        // Along the trail: how far its nearest point is from the trail's start (carried on past
                        // the stretch's ends, so a trail's rounded end doesn't fan one line of the texture out).
                        Vector2 a = segA[owner], d = segB[owner] - a;
                        Vector2 q = a + Wrap.Delta(a, p, plan.Size);
                        float len = d.magnitude;
                        float t = len > 1e-4f ? Vector2.Dot(q - a, d) / len : 0f;
                        return new Vector2(u, (segAlong[owner] + t) / TrailRepeat);
                    }
                    var patch = plan.Patches[-1 - owner];
                    if (inner)
                    {
                        // U runs to and fro over the solid earth crosswise to V (wandering a little, so the turns
                        // aren't straight lines), easing in from the middle line.
                        float side = Vector2.Dot(p, sideDir) + (PeriodicNoise.Sample(wx, wz, plan.Size, coarse, edgeSeed + 2) - 0.5f) * 1.2f;
                        float into = Mathf.Clamp01(-fromMiddle / PatchBlend);
                        u = 0.5f - 0.5f * solid * Mathf.PingPong(side / patchSpan, 1f) * into;
                        return new Vector2(u, Vector2.Dot(p, alongDir) / TrailRepeat);
                    }
                    // Round the patch's edge, a whole number of repeats the way round.
                    Vector2 r = Wrap.Delta(patch.Centre, p, plan.Size);
                    float turns = Mathf.Max(1f, Mathf.Round(2f * Mathf.PI * patch.Radius / TrailRepeat));
                    return new Vector2(u, (Mathf.Atan2(r.y, r.x) / (2f * Mathf.PI) + 0.5f) * turns + patch.Seed * 0.137f);
                }

                var verts = new List<Vector3>();
                var normals = new List<Vector3>();
                var uv0 = new List<Vector2>();
                var uv1 = new List<Vector2>();
                var tris = new List<int>();
                var index = new Dictionary<(int k, int owner, bool inner, int shift), int>();
                var corner = new int[4];
                var cornerUv = new Vector2[4];
                for (int z = 0; z < n; z++)
                for (int x = 0; x < n; x++)
                {
                    int k = z * stride + x;
                    corner[0] = k; corner[1] = k + 1; corner[2] = k + stride; corner[3] = k + stride + 1;
                    // The cell's earth is all mapped by whatever its deepest corner is nearest, so its UVs agree.
                    int deep = corner[0];
                    for (int i = 1; i < 4; i++) if (edge[corner[i]] < edge[deep]) deep = corner[i];
                    if (edge[deep] >= TrailFade) continue; // (no earth in this cell)
                    int owner = own[deep];
                    bool inner = owner < 0 && edge[deep] + half < 0f;
                    for (int i = 0; i < 4; i++)
                    {
                        int ck = corner[i];
                        cornerUv[i] = Uv(ck, owner, inner, origin.x + (ck % stride) * TrailStep, origin.y + (ck / stride) * TrailStep);
                    }
                    for (int i = 0; i < 4; i++)
                    {
                        // (V repeats, so a corner past a wrap — round a patch, over the map's edge — is brought
                        // back alongside the others rather than the texture squeezed between them.)
                        int shift = Mathf.RoundToInt(cornerUv[0].y - cornerUv[i].y);
                        var key = (corner[i], owner, inner, shift);
                        if (!index.TryGetValue(key, out int vi))
                        {
                            int ck = corner[i];
                            float lx = (ck % stride) * TrailStep, lz = (ck / stride) * TrailStep;
                            float wx = origin.x + lx, wz = origin.y + lz;
                            vi = verts.Count;
                            index[key] = vi;
                            verts.Add(new Vector3(lx, GroundAt(plan, wx, wz) + 0.025f, lz));
                            normals.Add(new Vector3(h.Sample(wx - 1f, wz) - h.Sample(wx + 1f, wz), 2f, h.Sample(wx, wz - 1f) - h.Sample(wx, wz + 1f)).normalized);
                            uv0.Add(cornerUv[i] + new Vector2(0f, shift));
                            uv1.Add(new Vector2(wx, wz) / 1.6f);
                        }
                        corner[i] = vi;
                    }
                    // (The same split as the ground's own triangles, clockwise from above.)
                    tris.Add(corner[0]); tris.Add(corner[2]); tris.Add(corner[1]);
                    tris.Add(corner[1]); tris.Add(corner[2]); tris.Add(corner[3]);
                }
                if (tris.Count == 0) continue;
                var mesh = new Mesh { name = $"Trail {cx},{cz}" };
                if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(verts);
                mesh.SetNormals(normals); // (from the ground's own slope, so cells mapped apart still light as one)
                mesh.SetUVs(0, uv0);
                mesh.SetUVs(1, uv1);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateTangents();
                mesh.RecalculateBounds();
                meshes[c] = mesh;
            }
            return meshes;
        }

        static float Segment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 d = b - a;
            float t = d.sqrMagnitude > 1e-6f ? Mathf.Clamp01(Vector2.Dot(p - a, d) / d.sqrMagnitude) : 0f;
            return (p - (a + d * t)).magnitude;
        }

        // The ground's height here as its mesh has it (straight between the 1 m vertices, split the same way), so
        // the trails lie just on it rather than dipping under it between vertices.
        public static float GroundAt(WorldPlan plan, float wx, float wz)
        {
            float x0 = Mathf.Floor(wx), z0 = Mathf.Floor(wz);
            float fx = wx - x0, fz = wz - z0;
            var h = plan.Height;
            float h00 = h.Sample(x0, z0), h10 = h.Sample(x0 + 1f, z0), h01 = h.Sample(x0, z0 + 1f), h11 = h.Sample(x0 + 1f, z0 + 1f);
            // Triangles (0,0)-(0,1)-(1,0) and (1,0)-(0,1)-(1,1): the diagonal runs from (1,0) to (0,1).
            if (fx + fz <= 1f) return h00 + (h10 - h00) * fx + (h01 - h00) * fz;
            return h11 + (h01 - h11) * (1f - fx) + (h10 - h11) * (1f - fz);
        }

        /// <summary>
        /// A pond's water: a flat grid at its level covering only the water the bowl actually holds (cells whose
        /// ground dips under the level, flooded out from the middle), so it ends at the shore and never hangs out
        /// over the bank where the land falls away beyond the rim. Where it overlaps the bank the ground hides it.
        /// Local to the pond's middle, at its water level.
        /// </summary>
        public static GameObject Water(WorldPlan plan, Pond pond, Material water, List<Object> made)
        {
            const float cell = 0.5f;
            float extent = pond.Radius + 1.5f;
            int n = Mathf.CeilToInt(extent * 2f / cell);
            float origin = -n * cell * 0.5f;

            var wet = new bool[(n + 1) * (n + 1)];
            for (int z = 0; z <= n; z++)
            for (int x = 0; x <= n; x++)
            {
                float lx = origin + x * cell, lz = origin + z * cell;
                wet[z * (n + 1) + x] = lx * lx + lz * lz < extent * extent
                    && GroundAt(plan, pond.Centre.x + lx, pond.Centre.y + lz) < pond.Water + 0.02f;
            }
            bool CellWet(int x, int z) =>
                wet[z * (n + 1) + x] || wet[z * (n + 1) + x + 1] || wet[(z + 1) * (n + 1) + x] || wet[(z + 1) * (n + 1) + x + 1];

            // Flood out from the middle, so a separate hollow nearby isn't filled at this level.
            var keep = new bool[n * n];
            var queue = new Queue<Vector2Int>();
            int mid = n / 2;
            if (CellWet(mid, mid)) { keep[mid * n + mid] = true; queue.Enqueue(new Vector2Int(mid, mid)); }
            var steps = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            while (queue.Count > 0)
            {
                var c = queue.Dequeue();
                foreach (var step in steps)
                {
                    var q = c + step;
                    if (q.x < 0 || q.y < 0 || q.x >= n || q.y >= n || keep[q.y * n + q.x] || !CellWet(q.x, q.y)) continue;
                    keep[q.y * n + q.x] = true;
                    queue.Enqueue(q);
                }
            }

            var verts = new List<Vector3>();
            var uvs = new List<Vector2>();
            var tris = new List<int>();
            var index = new int[(n + 1) * (n + 1)];
            for (int i = 0; i < index.Length; i++) index[i] = -1;
            int Vertex(int x, int z)
            {
                int k = z * (n + 1) + x;
                if (index[k] >= 0) return index[k];
                var v = new Vector3(origin + x * cell, 0f, origin + z * cell);
                index[k] = verts.Count;
                verts.Add(v);
                uvs.Add(new Vector2(v.x, v.z) * 0.25f); // ripples the same size whatever the pond
                return index[k];
            }
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                if (!keep[z * n + x]) continue;
                int a = Vertex(x, z), b = Vertex(x + 1, z), c = Vertex(x, z + 1), d = Vertex(x + 1, z + 1);
                // Clockwise seen from above (Unity's front faces), split like the ground.
                tris.Add(a); tris.Add(c); tris.Add(b);
                tris.Add(b); tris.Add(c); tris.Add(d);
            }

            var mesh = new Mesh { name = "Pond", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.SetVertices(verts);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            var normals = new Vector3[verts.Count];
            for (int i = 0; i < normals.Length; i++) normals[i] = Vector3.up;
            mesh.normals = normals;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            made.Add(mesh);
            var go = new GameObject("Pond") { layer = Layers.Default };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = water;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }

        /// <summary>
        /// A puddle: a small grid lying just on the ground as its mesh has it, rotated and stretched; the material's
        /// texture puts the puddle's shape on it. Local to the puddle's middle, at the ground there.
        /// </summary>
        public static GameObject PuddleQuad(WorldPlan plan, Puddle puddle, Material material, List<Object> made, out float groundY)
        {
            const int n = 6;
            var c = puddle.Centre;
            groundY = GroundAt(plan, c.x, c.y);
            var rot = Quaternion.Euler(0f, puddle.Angle, 0f);
            var verts = new Vector3[(n + 1) * (n + 1)];
            var uvs = new Vector2[verts.Length];
            var normals = new Vector3[verts.Length];
            var tris = new int[n * n * 6];
            for (int z = 0, i = 0; z <= n; z++)
            for (int x = 0; x <= n; x++, i++)
            {
                float u = x / (float)n, v = z / (float)n;
                Vector3 local = rot * new Vector3((u * 2f - 1f) * puddle.HalfSize.x, 0f, (v * 2f - 1f) * puddle.HalfSize.y);
                local.y = GroundAt(plan, c.x + local.x, c.y + local.z) - groundY + 0.025f;
                verts[i] = local;
                uvs[i] = new Vector2(u, v);
                normals[i] = Vector3.up;
            }
            for (int z = 0, t = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                int i = z * (n + 1) + x;
                tris[t++] = i; tris[t++] = i + n + 1; tris[t++] = i + 1;
                tris[t++] = i + 1; tris[t++] = i + n + 1; tris[t++] = i + n + 2;
            }
            var mesh = new Mesh { name = "Puddle", vertices = verts, uv = uvs, normals = normals, triangles = tris };
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            made.Add(mesh);
            var go = new GameObject("Puddle") { layer = Layers.Default };
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
