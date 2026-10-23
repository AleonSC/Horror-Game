using System.Collections.Generic;
using UnityEngine;

namespace Game.World.Build
{
    /// <summary>
    /// Turns a prefab (your own tree, bush, rock...) into parts the instanced renderer can draw: every mesh and
    /// sub-mesh with its material, placed as in the prefab. A LOD Group's levels become the levels of detail;
    /// otherwise every renderer is one level.
    /// </summary>
    public static class PrefabArt
    {
        public sealed class Result
        {
            public InstancedRenderer.Part[][] Lods;
            public float Height;   // from the root (the ground) to the top
        }

        public static Result Extract(GameObject prefab, MaterialFixer fixer)
        {
            if (prefab == null) return null;
            var root = prefab.transform;
            var group = prefab.GetComponentInChildren<LODGroup>();
            List<Renderer[]> levels = new List<Renderer[]>();
            if (group != null)
                foreach (var lod in group.GetLODs()) levels.Add(lod.renderers);
            else
                levels.Add(prefab.GetComponentsInChildren<MeshRenderer>());

            var result = new Result { Lods = new InstancedRenderer.Part[levels.Count][] };
            float top = 0f;
            for (int l = 0; l < levels.Count; l++)
            {
                var parts = new List<InstancedRenderer.Part>();
                foreach (var r in levels[l])
                {
                    if (r == null) continue;
                    var filter = r.GetComponent<MeshFilter>();
                    if (filter == null || filter.sharedMesh == null) continue;
                    var mesh = filter.sharedMesh;
                    Matrix4x4 local = root.worldToLocalMatrix * r.transform.localToWorldMatrix;
                    var mats = r.sharedMaterials;
                    for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    {
                        var mat = fixer.Fix(mats.Length > 0 ? mats[Mathf.Min(sub, mats.Length - 1)] : null);
                        if (mat == null) continue;
                        parts.Add(new InstancedRenderer.Part { Mesh = mesh, Submesh = sub, Material = mat, Local = local, HasLocal = local != Matrix4x4.identity });
                    }
                    if (l == 0)
                    {
                        var b = mesh.bounds;
                        for (int c = 0; c < 8; c++)
                        {
                            var corner = new Vector3((c & 1) == 0 ? b.min.x : b.max.x, (c & 2) == 0 ? b.min.y : b.max.y, (c & 4) == 0 ? b.min.z : b.max.z);
                            top = Mathf.Max(top, local.MultiplyPoint3x4(corner).y);
                        }
                    }
                }
                result.Lods[l] = parts.ToArray();
            }
            // A level with nothing in it (LOD Groups often end in "culled") draws the previous one.
            for (int l = 1; l < result.Lods.Length; l++)
                if (result.Lods[l].Length == 0) result.Lods[l] = result.Lods[l - 1];
            if (result.Lods.Length == 0 || result.Lods[0].Length == 0) return null;
            result.Height = Mathf.Max(0.1f, top);
            return result;
        }
    }
}
