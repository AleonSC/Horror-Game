using System.Collections.Generic;
using Game.World.Generation;
using UnityEngine;

namespace Game.World.Build
{
    /// <summary>
    /// Draws the forest's many copies (trees, grass, bushes, rocks, logs) with GPU instancing, no GameObject each
    /// (section 2.6). Copies are kept per 32 m chunk; every frame only chunks in range and in view are drawn,
    /// each at whichever copy of it (the map wraps) is nearest the camera, in a level of detail by distance.
    /// </summary>
    public sealed class InstancedRenderer
    {
        /// <summary>One mesh (or one sub-mesh of it) with its material, placed relative to the copy.</summary>
        public struct Part
        {
            public Mesh Mesh;
            public int Submesh;
            public Material Material;
            public Matrix4x4 Local;
            public bool HasLocal;   // false: Local is the identity (most parts)
        }

        public sealed class Kind
        {
            public string Name;
            public Part[][] Lods;        // per level of detail (most detailed first), the parts to draw
            public float[] LodDistances; // switch to the next LOD past each of these (metres)
            public float MaxDistance;    // not drawn past this (the fog hides it anyway)
            public bool Shadows;
            public int Layer;

            /// <summary>A kind made of one mesh per LOD, all in one material (the built-in shapes).</summary>
            public static Kind Simple(string name, Mesh[] lods, Material material, float[] lodDistances, float maxDistance, bool shadows, int layer)
            {
                var parts = new Part[lods.Length][];
                for (int i = 0; i < lods.Length; i++) parts[i] = new[] { new Part { Mesh = lods[i], Material = material } };
                return new Kind { Name = name, Lods = parts, LodDistances = lodDistances, MaxDistance = maxDistance, Shadows = shadows, Layer = layer };
            }
        }

        sealed class Gather
        {
            public Matrix4x4[] Matrices = new Matrix4x4[256];
            public int Count;

            public void Add(in Matrix4x4 m)
            {
                if (Count == Matrices.Length) System.Array.Resize(ref Matrices, Count * 2);
                Matrices[Count++] = m;
            }
        }

        readonly float mapSize, chunkSize;
        readonly int across;
        readonly List<Kind> kinds = new List<Kind>();
        readonly List<List<Matrix4x4>[]> homes = new List<List<Matrix4x4>[]>(); // per kind: per chunk
        readonly List<Gather[]> gathers = new List<Gather[]>();               // per kind: per LOD
        readonly Plane[] planes = new Plane[6];
        readonly Matrix4x4[] batch = new Matrix4x4[InstancedDraw.MaxPerCall];
        readonly float minHeight, maxHeight;
        float farthest;

        public InstancedRenderer(float mapSize, float chunkSize, float minHeight, float maxHeight)
        {
            this.mapSize = mapSize;
            this.chunkSize = chunkSize;
            across = Mathf.CeilToInt(mapSize / chunkSize);
            this.minHeight = minHeight;
            this.maxHeight = maxHeight;
        }

        public int InstanceCount { get; private set; }
        public int DrawCalls { get; private set; }
        public int DrawnInstances { get; private set; }

        public int AddKind(Kind kind)
        {
            kinds.Add(kind);
            var perChunk = new List<Matrix4x4>[across * across];
            homes.Add(perChunk);
            var g = new Gather[kind.Lods.Length];
            for (int i = 0; i < g.Length; i++) g[i] = new Gather();
            gathers.Add(g);
            farthest = Mathf.Max(farthest, kind.MaxDistance);
            return kinds.Count - 1;
        }

        /// <summary>Add a copy at its place on the map (x, z in 0..mapSize).</summary>
        public void Add(int kind, Vector2 home, Matrix4x4 matrix)
        {
            int c = ChunkIndex(home);
            var list = homes[kind][c] ??= new List<Matrix4x4>();
            list.Add(matrix);
            InstanceCount++;
        }

        public Kind GetKind(int kind) => kinds[kind];

        /// <summary>Take a copy away again (a tree knocked down). False if it isn't there.</summary>
        public bool Remove(int kind, Vector2 home, Matrix4x4 matrix)
        {
            var list = homes[kind][ChunkIndex(home)];
            if (list == null) return false;
            int i = list.IndexOf(matrix);
            if (i < 0) return false;
            list.RemoveAt(i);
            InstanceCount--;
            return true;
        }

        int ChunkIndex(Vector2 p)
        {
            int cx = Mathf.Clamp(Mathf.FloorToInt(Wrap.Into(p.x, mapSize) / chunkSize), 0, across - 1);
            int cz = Mathf.Clamp(Mathf.FloorToInt(Wrap.Into(p.y, mapSize) / chunkSize), 0, across - 1);
            return cz * across + cx;
        }

        public void Render(Camera cam)
        {
            if (cam == null) return;
            GeometryUtility.CalculateFrustumPlanes(cam, planes);
            Vector3 eye = cam.transform.position;
            var eye2 = new Vector2(eye.x, eye.z);
            foreach (var g in gathers) foreach (var lod in g) lod.Count = 0;

            float half = chunkSize * 0.5f;
            for (int cz = 0; cz < across; cz++)
            for (int cx = 0; cx < across; cx++)
            {
                var centre = new Vector2(cx * chunkSize + half, cz * chunkSize + half);
                Vector2 offset = Wrap.NearestCopyOffset(centre, eye2, mapSize);
                Vector2 c = centre + offset;
                float dist = Mathf.Max(0f, Vector2.Distance(c, eye2) - half * 1.414f);
                if (dist > farthest) continue;
                var box = new Bounds(new Vector3(c.x, (minHeight + maxHeight) * 0.5f + 15f, c.y), new Vector3(chunkSize + 8f, maxHeight - minHeight + 60f, chunkSize + 8f));
                if (!GeometryUtility.TestPlanesAABB(planes, box)) continue;

                int chunk = cz * across + cx;
                for (int k = 0; k < kinds.Count; k++)
                {
                    var list = homes[k][chunk];
                    if (list == null) continue;
                    var kind = kinds[k];
                    if (dist > kind.MaxDistance) continue;
                    int lod = 0;
                    while (lod < kind.LodDistances.Length && lod < kind.Lods.Length - 1 && dist > kind.LodDistances[lod]) lod++;
                    var gather = gathers[k][lod];
                    for (int i = 0; i < list.Count; i++)
                    {
                        var m = list[i];
                        m.m03 += offset.x;
                        m.m23 += offset.y;
                        gather.Add(m);
                    }
                }
            }

            DrawCalls = 0;
            DrawnInstances = 0;
            var bounds = new Bounds(eye, Vector3.one * (farthest * 2f + 100f));
            for (int k = 0; k < kinds.Count; k++)
            {
                var kind = kinds[k];
                for (int lod = 0; lod < kind.Lods.Length; lod++)
                {
                    var g = gathers[k][lod];
                    if (g.Count == 0) continue;
                    DrawnInstances += g.Count;
                    foreach (var part in kind.Lods[lod])
                    {
                        for (int start = 0; start < g.Count; start += InstancedDraw.MaxPerCall)
                        {
                            int n = Mathf.Min(InstancedDraw.MaxPerCall, g.Count - start);
                            if (part.HasLocal)
                                for (int i = 0; i < n; i++) batch[i] = g.Matrices[start + i] * part.Local;
                            else
                                System.Array.Copy(g.Matrices, start, batch, 0, n);
                            InstancedDraw.Draw(part.Mesh, part.Submesh, part.Material, batch, n, bounds, kind.Layer, kind.Shadows);
                            DrawCalls++;
                        }
                    }
                }
            }
        }
    }
}
