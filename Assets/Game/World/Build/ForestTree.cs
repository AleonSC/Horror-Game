using System.Collections;
using System.Collections.Generic;
using Game.Audio;
using Game.Core;
using UnityEngine;

namespace Game.World.Build
{
    /// <summary>
    /// One tree of the forest, on its collider: which instanced kinds draw it, where, and how. Lets it be knocked
    /// down: the drawn copies are taken away and a real tree topples in their place.
    /// </summary>
    public sealed class ForestTree : MonoBehaviour
    {
        public int[] Kinds;
        public Vector2 Home;
        public Matrix4x4 Matrix;
        /// <summary>An owl sits in it (falling trees leave it alone).</summary>
        public bool HasOwl;
        bool down;

        public bool Down => down;

        /// <summary>
        /// Knock it over, falling away from <paramref name="pushedFrom"/>. It snaps off above a stump (which stays),
        /// cut into logs that topple together; the crown bursts into a flurry of needles as it hits the ground, and
        /// a few seconds later the logs come apart and roll. They lie a while, then sink away. False if it's
        /// already down.
        /// </summary>
        public bool KnockDown(InstancedRenderer forest, Vector3 pushedFrom, float shudder = 0f)
        {
            if (down || forest == null || HasOwl) return false;
            down = true;
            // Where the drawn copy is now (the world wraps: the collider's chunk may be at another copy of the map).
            Vector3 homeWorld = new Vector3(Matrix.m03, Matrix.m13, Matrix.m23);
            Vector3 offset = transform.position - homeWorld;
            offset.y = 0f;
            Vector3 at = homeWorld + offset;

            // Its parts, in the tree's own space (metres up from its foot): the wood, and the crown.
            var wood = new List<(Mesh mesh, int sub, Matrix4x4 m, Material mat)>();
            var crown = new Dictionary<Mesh, (Matrix4x4 m, Material[] mats, string name)>();
            var whole = new Dictionary<Mesh, (Matrix4x4 m, Material[] mats, string name)>();
            foreach (int k in Kinds)
            {
                forest.Remove(k, Home, Matrix);
                var kind = forest.GetKind(k);
                if (kind.Lods == null || kind.Lods.Length == 0) continue;
                foreach (var part in kind.Lods[0])
                {
                    if (part.Mesh == null) continue;
                    Matrix4x4 m = Matrix4x4.Translate(-homeWorld) * Matrix * (part.HasLocal ? part.Local : Matrix4x4.identity);
                    bool leafy = IsLeafy(kind.Name, part.Material);
                    if (!leafy) wood.Add((part.Mesh, part.Submesh, m, part.Material));
                    var into = leafy ? crown : whole;
                    if (!into.TryGetValue(part.Mesh, out var entry)) entry = (m, new Material[part.Mesh.subMeshCount], kind.Name);
                    if (part.Submesh < entry.mats.Length) entry.mats[part.Submesh] = part.Material;
                    into[part.Mesh] = entry;
                }
            }
            gameObject.SetActive(false); // (its collider: the stump has its own)

            Vector3 away = at - pushedFrom;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
            away.y = 0f;

            bool cuttable = wood.Count > 0;
            foreach (var w in wood) if (!w.mesh.isReadable) cuttable = false;
            List<TreeFall.Segment> segments = null;
            Transform root;
            Collider stump = null;
            if (cuttable) root = Cut(at, wood, out segments, out stump);
            else
            {
                // (Art whose meshes can't be read: it goes over whole, as it is.)
                root = new GameObject("Falling tree") { layer = Layers.Trees }.transform;
                root.position = at;
                foreach (var pair in whole) Show(root, pair.Key, pair.Value.m, pair.Value.mats, pair.Value.name, Vector3.zero);
            }
            var leaves = new List<Renderer>();
            Vector3 pivot = root.position - at;
            foreach (var pair in crown) leaves.Add(Show(root, pair.Key, pair.Value.m, pair.Value.mats, pair.Value.name, pivot));
            root.gameObject.AddComponent<TreeFall>().Begin(away.normalized, shudder, segments, leaves, stump);
            return true;
        }

        // Needles and leaves (they burst), rather than wood (it's cut into logs).
        static bool IsLeafy(string kindName, Material m)
        {
            string n = (kindName + " " + (m != null ? m.name : "")).ToLowerInvariant();
            if (n.Contains("crown") || n.Contains("leaf") || n.Contains("leaves") || n.Contains("needle") || n.Contains("foliage") || n.Contains("frond")) return true;
            return m != null && m.IsKeywordEnabled("_DOUBLESIDED_ON") && !n.Contains("bark") && !n.Contains("trunk") && !n.Contains("wood");
        }

        // A drawn part under the falling tree, placed as in the tree (less the pivot's offset).
        static Renderer Show(Transform root, Mesh mesh, Matrix4x4 m, Material[] mats, string name, Vector3 pivot)
        {
            for (int i = 0; i < mats.Length; i++) if (mats[i] == null) mats[i] = System.Array.Find(mats, x => x != null);
            var go = new GameObject(name) { layer = Layers.Trees };
            go.transform.SetParent(root, false);
            go.transform.localPosition = (Vector3)m.GetColumn(3) - pivot;
            go.transform.localRotation = m.rotation;
            go.transform.localScale = m.lossyScale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = mats;
            return r;
        }

        static Material freshWood;

        // The pale wood of a cut or broken end.
        static Material FreshWood
        {
            get
            {
                if (freshWood == null) freshWood = Game.Rendering.RuntimeMaterials.Lit("Fresh wood", new Color(0.5f, 0.38f, 0.25f), 0.12f, true);
                return freshWood;
            }
        }

        // Cut the wood into a stump (left standing, beside the chunk's other trees) and logs (under the returned
        // root, which sits on top of the stump: the tree pivots there as it goes over).
        Transform Cut(Vector3 at, List<(Mesh mesh, int sub, Matrix4x4 m, Material mat)> wood, out List<TreeFall.Segment> segments, out Collider stumpCollider)
        {
            var rng = new System.Random(Home.GetHashCode());
            float top = 0f;
            foreach (var w in wood)
            {
                var b = w.mesh.bounds;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = b.center + Vector3.Scale(b.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                    top = Mathf.Max(top, w.m.MultiplyPoint3x4(corner).y);
                }
            }
            top = Mathf.Max(top, 2f);
            float stump = Mathf.Clamp(top * 0.04f, 0.35f, 0.8f) * (0.85f + (float)rng.NextDouble() * 0.3f);
            int logs = Mathf.Clamp(Mathf.RoundToInt((top - stump) / 3.2f), 3, 6);
            var cuts = new float[logs];
            cuts[0] = stump;
            for (int i = 1; i < logs; i++)
                cuts[i] = Mathf.Lerp(stump, top, (i + ((float)rng.NextDouble() - 0.5f) * 0.4f) / logs);
            var pieces = new MeshSlicer.Piece[logs + 1];
            var points = new List<Vector3>[logs];
            for (int i = 0; i < pieces.Length; i++) pieces[i] = new MeshSlicer.Piece();
            for (int i = 0; i < logs; i++) points[i] = new List<Vector3>();
            foreach (var w in wood)
                if (w.sub < w.mesh.subMeshCount) MeshSlicer.Slice(w.mesh, w.sub, w.m, w.mat, cuts, pieces, points);

            // Cap the cut ends with fresh wood: torn and splintered where it snapped off the stump, cleaner between logs.
            var centres = new Vector3[logs];
            var radii = new float[logs];
            for (int k = 0; k < logs; k++)
            {
                if (!MeshSlicer.TrunkAt(points[k], cuts[k], out centres[k], out radii[k]))
                {
                    centres[k] = new Vector3(0f, cuts[k], 0f);
                    radii[k] = 0.1f;
                    continue;
                }
                float jag = k == 0 ? 0.45f : 0.12f;
                MeshSlicer.Cap(pieces[k], FreshWood, centres[k], radii[k], true, jag, rng);
                MeshSlicer.Cap(pieces[k + 1], FreshWood, centres[k], radii[k], false, jag, rng);
            }

            // The stump: stays, in this tree's chunk (so it wraps with it).
            var stumpGo = new GameObject("Stump") { layer = Layers.Props };
            stumpGo.transform.position = at;
            stumpGo.transform.SetParent(transform.parent, true);
            var stumpMesh = pieces[0].ToMesh("Stump", Vector3.zero, out var stumpMats);
            stumpGo.AddComponent<MeshFilter>().sharedMesh = stumpMesh;
            stumpGo.AddComponent<MeshRenderer>().sharedMaterials = stumpMats;
            var stumpCol = stumpGo.AddComponent<CapsuleCollider>();
            stumpCol.radius = Mathf.Max(0.1f, radii[0]);
            stumpCol.height = Mathf.Max(stump, stumpCol.radius * 2f + 0.01f);
            stumpCol.center = new Vector3(centres[0].x, stump * 0.5f, centres[0].z);
            stumpGo.AddComponent<MeshCleanup>().Mesh = stumpMesh;
            stumpCollider = stumpCol;

            // The logs, under a root on top of the stump.
            var root = new GameObject("Falling tree") { layer = Layers.Trees }.transform;
            Vector3 pivot = centres[0];
            root.position = at + pivot;
            segments = new List<TreeFall.Segment>();
            for (int i = 1; i < pieces.Length; i++)
            {
                if (pieces[i].Empty) continue;
                Vector3 low = centres[i - 1];
                Vector3 high = i < logs ? centres[i] : new Vector3(low.x, top, low.z);
                Vector3 mid = (low + high) * 0.5f;
                var log = pieces[i].ToMesh($"Log {i}", mid, out var mats);
                var go = new GameObject($"Log {i}") { layer = Layers.Trees };
                go.transform.SetParent(root, false);
                go.transform.localPosition = mid - pivot;
                go.AddComponent<MeshFilter>().sharedMesh = log;
                go.AddComponent<MeshRenderer>().sharedMaterials = mats;
                go.AddComponent<MeshCleanup>().Mesh = log;
                float r = radii[i - 1] > 0f ? radii[i - 1] : 0.2f;
                segments.Add(new TreeFall.Segment { Piece = go.transform, Radius = Mathf.Max(0.06f, i < logs ? (r + radii[i]) * 0.5f : r * 0.6f), Length = (high - low).magnitude });
            }
            return root;
        }
    }

    /// <summary>Frees a mesh made for one object (a stump, a log) when the object goes.</summary>
    public sealed class MeshCleanup : MonoBehaviour
    {
        public Mesh Mesh;

        void OnDestroy()
        {
            if (Mesh != null) Destroy(Mesh);
        }
    }
}
