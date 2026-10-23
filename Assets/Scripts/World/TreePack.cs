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

    [Header("Look (old materials the game converts; changes show live in Play and are kept)")]
    [Tooltip("Brightens the leaves (their colour times this). Canopies seen from below are mostly in their own shade.")]
    public float leafBrightness = 1.6f;
    public float barkBrightness = 1f;
    [Tooltip("Leaves lit the same on both faces, so ones seen from underneath aren't black (thin leaves let light through).")]
    public bool leavesLitBothSides = true;

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
    static readonly System.Collections.Generic.Dictionary<Material, (Color tint, bool leaf)> convertedLook = new System.Collections.Generic.Dictionary<Material, (Color, bool)>();
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() { converted.Clear(); convertedLook.Clear(); }

    /// <summary>The look settings on every converted material (after converting, and live as they're changed).</summary>
    static void ApplyLook(Material mat, Color tint, bool leaf)
    {
        var pack = Load();
        float bright = pack == null ? (leaf ? 1.6f : 1f) : leaf ? pack.leafBrightness : pack.barkBrightness;
        Color c = tint * Mathf.Max(0f, bright);
        c.a = tint.a; // (the alpha decides the cut-out: kept)
        MaterialFactory.SetColor(mat, c);
        if (leaf && mat.HasProperty("_DoubleSidedNormalMode"))
        {
            // None: the back face lit as the front (Flip or Mirror leave the undersides of a canopy black).
            mat.SetFloat("_DoubleSidedNormalMode", pack == null || pack.leavesLitBothSides ? 2f : 1f);
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(mat);
        }
    }

    void OnValidate()
    {
        if (!Application.isPlaying) return;
        foreach (var kv in convertedLook)
            if (kv.Key != null) ApplyLook(kv.Key, kv.Value.tint, kv.Value.leaf);
    }

    /// <summary>
    /// True if a material can't be drawn by HDRP (it shows pink): its shader has no HDRP pass (GBuffer,
    /// Forward or ForwardOnly). That covers the old built-in pipeline's shaders: the Tree Creator's
    /// (Hidden/Nature/Tree Creator ... Optimized), Standard, legacy and mobile ones, and URP's.
    /// </summary>
    public static bool NeedsConverting(Material m)
    {
        if (m == null || m.shader == null) return false;
        var shader = m.shader;
        if (shader.name.StartsWith("HDRP/") || shader.name.StartsWith("Horror/")) return false;
        if (!shader.isSupported) return true;
        var lightMode = new UnityEngine.Rendering.ShaderTagId("LightMode");
        for (int pass = 0; pass < shader.passCount; pass++)
        {
            string mode = shader.FindPassTagValue(pass, lightMode).name;
            if (mode == "GBuffer" || mode == "Forward" || mode == "ForwardOnly") return false;
        }
        return true;
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
        convertedLook[mat] = (tint, leaf);
        ApplyLook(mat, tint, leaf);
        Debug.Log($"[Horror] Tree pack: '{source.name}' ({(source.shader != null ? source.shader.name : "no shader")}) drawn with an HDRP stand-in{(leaf ? " (leaves: cut out, two-sided)" : "")}, texture {(albedo != null ? albedo.name : "none")}.");
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
