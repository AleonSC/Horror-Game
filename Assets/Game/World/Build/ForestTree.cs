using System.Collections;
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
        /// Knock it over, falling away from <paramref name="pushedFrom"/>: it topples, thuds down, lies a while, then
        /// sinks away. False if it's already down.
        /// </summary>
        public bool KnockDown(InstancedRenderer forest, Vector3 pushedFrom, float shudder = 0f)
        {
            if (down || forest == null) return false;
            down = true;
            // Where the drawn copy is now (the world wraps: the collider's chunk may be at another copy of the map).
            Vector3 homeWorld = new Vector3(Matrix.m03, Matrix.m13, Matrix.m23);
            Vector3 offset = transform.position - homeWorld;
            offset.y = 0f;

            var tree = new GameObject("Falling tree") { layer = Layers.Trees };
            tree.transform.position = homeWorld + offset;
            var pivot = tree.transform;
            // One renderer per mesh, with each part's material in its sub-mesh's slot.
            var byMesh = new System.Collections.Generic.Dictionary<Mesh, (Matrix4x4 local, Material[] mats, string name)>();
            foreach (int k in Kinds)
            {
                forest.Remove(k, Home, Matrix);
                var kind = forest.GetKind(k);
                if (kind.Lods == null || kind.Lods.Length == 0) continue;
                foreach (var part in kind.Lods[0])
                {
                    if (part.Mesh == null) continue;
                    if (!byMesh.TryGetValue(part.Mesh, out var entry))
                        entry = (part.HasLocal ? part.Local : Matrix4x4.identity, new Material[part.Mesh.subMeshCount], kind.Name);
                    if (part.Submesh < entry.mats.Length) entry.mats[part.Submesh] = part.Material;
                    byMesh[part.Mesh] = entry;
                }
            }
            foreach (var pair in byMesh)
            {
                var (local, mats, name) = pair.Value;
                for (int i = 0; i < mats.Length; i++) if (mats[i] == null) mats[i] = mats[0] ?? System.Array.Find(mats, x => x != null);
                var go = new GameObject(name) { layer = Layers.Trees };
                go.transform.SetParent(pivot, false);
                Matrix4x4 m = Matrix4x4.Translate(-homeWorld) * Matrix * local;
                go.transform.localPosition = m.GetColumn(3);
                go.transform.localRotation = m.rotation;
                go.transform.localScale = m.lossyScale;
                go.AddComponent<MeshFilter>().sharedMesh = pair.Key;
                go.AddComponent<MeshRenderer>().sharedMaterials = mats;
            }
            gameObject.SetActive(false); // (its collider: nothing bumps into the stump's ghost)

            Vector3 away = tree.transform.position - pushedFrom;
            away.y = 0f;
            if (away.sqrMagnitude < 0.01f) away = Random.insideUnitSphere;
            away.y = 0f;
            tree.AddComponent<TreeFall>().Begin(away.normalized, shudder);
            return true;
        }
    }
}
