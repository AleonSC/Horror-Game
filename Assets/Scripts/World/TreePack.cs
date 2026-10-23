using UnityEngine;

/// <summary>
/// The trees to use from a tree pack: an asset at Resources/TreePack (Horror > Tree Pack > Open Settings
/// makes it). Drag the pack's tree prefabs into Prefabs and DarkForestGenerator grows them, each set up
/// like the game's own trees (TreePack trees sway, fall, hide scours and stay out of buildings).
/// </summary>
[CreateAssetMenu(fileName = "TreePack", menuName = "Horror/Tree Pack")]
public class TreePack : ScriptableObject
{
    [Tooltip("The pack's tree prefabs. Their materials must be HDRP ones (pink means they aren't: convert them).")]
    public GameObject[] prefabs;
    [Range(0f, 1f), Tooltip("Share of the living trees taken from the pack (the rest are the game's own pines). 1: all of them.")]
    public float share = 1f;
    [Tooltip("Dead trees stay the game's own (the generator's Dead Tree Ratio of them).")]
    public bool keepProceduralDeadTrees = true;
    [Tooltip("Random size of a pack tree, as a multiple of the prefab's own.")]
    public Vector2 scale = new Vector2(0.8f, 1.3f);
    [Tooltip("Trunk thickness for a pack tree's collider (radius in metres, at the prefab's own size).")]
    public float trunkRadius = 0.3f;
    [Tooltip("Keep the prefab's own colliders (often its whole crown, which you'd walk into). Off: just the trunk collider.")]
    public bool keepPrefabColliders = false;

    public bool HasTrees
    {
        get
        {
            if (prefabs == null) return false;
            foreach (var p in prefabs) if (p != null) return true;
            return false;
        }
    }

    /// <summary>The pack in Resources (null if there isn't one).</summary>
    public static TreePack Load() => Resources.Load<TreePack>("TreePack");

    // ---------------------------------------------------------------- Materials

    static readonly System.Collections.Generic.Dictionary<Material, Material> converted = new System.Collections.Generic.Dictionary<Material, Material>();
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() => converted.Clear();

    /// <summary>
    /// True if a material can't be drawn by HDRP (it shows pink): the old built-in pipeline's shaders, such as
    /// the Tree Creator's (Nature/Tree Creator Bark and Leaves, their Optimized versions), Standard, legacy ones.
    /// </summary>
    public static bool NeedsConverting(Material m)
    {
        if (m == null || m.shader == null) return false;
        string n = m.shader.name;
        if (n.StartsWith("HDRP/") || n.StartsWith("Horror/")) return false;
        return !m.shader.isSupported || n.StartsWith("Nature/") || n == "Standard" || n.StartsWith("Standard (")
            || n.StartsWith("Legacy Shaders/") || n.StartsWith("Mobile/") || n.StartsWith("Hidden/InternalErrorShader");
    }

    /// <summary>
    /// An HDRP Lit stand-in for an old material: its colour texture and tint; for leaves (a leaf shader, a
    /// cut-out, or "leaf" in its name), cut out where the texture's alpha is low, and lit on both faces. Each
    /// is made once and shared.
    /// </summary>
    public static Material ToHdrp(Material source)
    {
        if (!NeedsConverting(source)) return source;
        if (converted.TryGetValue(source, out var done) && done != null) return done;

        string shaderName = source.shader != null ? source.shader.name.ToLowerInvariant() : "";
        string name = source.name.ToLowerInvariant();
        bool leaf = shaderName.Contains("leaves") || shaderName.Contains("leaf") || shaderName.Contains("cutout")
                    || name.Contains("leaf") || name.Contains("leaves") || name.Contains("foliage") || name.Contains("needle");
        Texture albedo = source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
        Color tint = source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
        // A normal map only if it's a plain one (the Tree Creator's packed bump-and-gloss map isn't).
        Texture normal = source.HasProperty("_BumpMap") ? source.GetTexture("_BumpMap") : null;

        var mat = MaterialFactory.LitTextured(albedo, normal, tint, leaf ? 0.1f : 0.05f);
        mat.name = source.name + " (HDRP)";
        if (leaf)
        {
            float cutoff = source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : 0.4f;
            mat.SetFloat("_AlphaCutoffEnable", 1f);
            mat.SetFloat("_AlphaCutoff", Mathf.Clamp(cutoff, 0.05f, 0.95f));
            if (mat.HasProperty("_AlphaCutoffShadow")) mat.SetFloat("_AlphaCutoffShadow", Mathf.Clamp(cutoff, 0.05f, 0.95f));
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            MaterialFactory.SetDoubleSided(mat); // (also re-validates the material, setting the cut-out up)
        }
        converted[source] = mat;
        return mat;
    }

    /// <summary>Swaps every old material under a model for its HDRP stand-in.</summary>
    public static void ConvertMaterials(GameObject model)
    {
        foreach (var r in model.GetComponentsInChildren<Renderer>(true))
        {
            var mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                if (!NeedsConverting(mats[i])) continue;
                mats[i] = ToHdrp(mats[i]);
                changed = true;
            }
            if (changed) r.sharedMaterials = mats;
        }
    }
}
