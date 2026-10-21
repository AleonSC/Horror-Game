using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Merges the plain pieces of a model that move together into one mesh, so a creature or prop built from
/// dozens of primitives draws (and casts shadows) in a handful of calls instead of one per piece.
///
/// Only a group's direct children that are plain pieces are merged: nothing under them, and nothing on them
/// but a mesh with one material, its renderer and maybe a collider. Pieces with their own children (pivots
/// that move on their own: a limb, a door) or any script are left exactly as they are. Pieces with the
/// same shadow settings become one "Merged" child, one submesh per material. A piece's collider stays
/// behind on its own object, still solid, with just its mesh and renderer taken off; a piece without one is
/// removed. It's all done at once, so code that collects renderers straight after sees only the merged ones.
/// </summary>
public static class MeshMerge
{
    /// <summary>Merges the plain pieces directly under group.</summary>
    public static void Merge(Transform group)
    {
        var pieces = new List<MeshRenderer>();
        foreach (Transform child in group)
            if (IsPlainPiece(child, out var r)) pieces.Add(r);
        if (pieces.Count < 2) return;

        // One merged object per way of being drawn (shadows), each with a submesh per material.
        var looks = new List<(ShadowCastingMode shadows, bool receive)>();
        var byLook = new List<List<MeshRenderer>>();
        foreach (var r in pieces)
        {
            var look = (r.shadowCastingMode, r.receiveShadows);
            int i = looks.IndexOf(look);
            if (i < 0) { looks.Add(look); byLook.Add(new List<MeshRenderer>()); i = looks.Count - 1; }
            byLook[i].Add(r);
        }
        for (int i = 0; i < looks.Count; i++)
            if (byLook[i].Count >= 2) Build(group, byLook[i], looks[i].shadows, looks[i].receive);
    }

    /// <summary>Merges each group (null ones skipped).</summary>
    public static void MergeEach(params Transform[] groups)
    {
        foreach (var g in groups) if (g != null) Merge(g);
    }

    static void Build(Transform group, List<MeshRenderer> pieces, ShadowCastingMode shadows, bool receive)
    {
        var mats = new List<Material>();
        var byMat = new List<List<CombineInstance>>();
        int vertexCount = 0;
        Matrix4x4 toGroup = group.worldToLocalMatrix;
        foreach (var r in pieces)
        {
            var mesh = r.GetComponent<MeshFilter>().sharedMesh;
            int k = mats.IndexOf(r.sharedMaterial);
            if (k < 0) { mats.Add(r.sharedMaterial); byMat.Add(new List<CombineInstance>()); k = mats.Count - 1; }
            byMat[k].Add(new CombineInstance { mesh = mesh, subMeshIndex = 0, transform = toGroup * r.transform.localToWorldMatrix });
            vertexCount += mesh.vertexCount;
        }
        IndexFormat format = vertexCount > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;

        var parts = new CombineInstance[byMat.Count];
        for (int k = 0; k < byMat.Count; k++)
        {
            var part = new Mesh { indexFormat = format };
            part.CombineMeshes(byMat[k].ToArray(), true, true);
            parts[k] = new CombineInstance { mesh = part, transform = Matrix4x4.identity };
        }
        var merged = new Mesh { name = group.name + " (merged)", indexFormat = format };
        merged.CombineMeshes(parts, false, false);
        foreach (var p in parts) Object.DestroyImmediate(p.mesh);

        var go = new GameObject("Merged");
        go.layer = group.gameObject.layer;
        go.transform.SetParent(group, false);
        go.AddComponent<MeshFilter>().sharedMesh = merged;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterials = mats.ToArray();
        renderer.shadowCastingMode = shadows;
        renderer.receiveShadows = receive;

        foreach (var r in pieces)
        {
            if (r.GetComponent<Collider>() != null)
            {
                var filter = r.GetComponent<MeshFilter>();
                Object.DestroyImmediate(r);
                Object.DestroyImmediate(filter);
            }
            else Object.DestroyImmediate(r.gameObject);
        }
    }

    static bool IsPlainPiece(Transform t, out MeshRenderer renderer)
    {
        renderer = null;
        if (t.childCount > 0 || !t.gameObject.activeSelf) return false;
        var filter = t.GetComponent<MeshFilter>();
        renderer = t.GetComponent<MeshRenderer>();
        if (filter == null || renderer == null || !renderer.enabled || filter.sharedMesh == null || !filter.sharedMesh.isReadable) return false;
        if (filter.sharedMesh.subMeshCount != 1 || renderer.sharedMaterials.Length != 1 || renderer.sharedMaterial == null) return false;
        foreach (var c in t.GetComponents<Component>())
            if (!(c is Transform || c is MeshFilter || c is MeshRenderer || c is Collider)) return false;
        return true;
    }
}
