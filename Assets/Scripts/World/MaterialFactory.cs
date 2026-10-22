using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Creates the game's materials in code, for HDRP (HDRP/Lit and HDRP/Unlit). Materials made at runtime
/// in HDRP must be "validated" after their settings change (HDMaterial.ValidateMaterial), which sets the
/// keywords and render states HDRP's shaders need; everything here does that, and SetDoubleSided and
/// SetNormalMapOffset cover the changes other scripts make.
/// </summary>
public static class MaterialFactory
{
    /// <summary>
    /// A shader from the reference materials kept in Resources/HorrorShaderRefs (HorrorBuildSetup makes them),
    /// which is what guarantees the shader is in a build at all; otherwise by name; and as a last resort the
    /// game's own fog-free unlit shader, so a missing shader can never stop the forest being built.
    /// </summary>
    static Shader FindShader(string reference, params string[] names)
    {
        var refMat = Resources.Load<Material>("HorrorShaderRefs/" + reference);
        if (refMat != null && refMat.shader != null && refMat.shader.isSupported) return refMat.shader;
        foreach (var name in names)
        {
            var shader = Shader.Find(name);
            if (shader != null && shader.isSupported) return shader;
        }
        Debug.LogWarning($"MaterialFactory: no '{reference}' shader in this build; using a plain fallback.");
        return Shader.Find("Horror/Unlit No Fog");
    }

    /// <summary>A rough, lit material (HDRP/Lit).</summary>
    public static Material Lit(Color color, float smoothness = 0.05f)
    {
        var mat = new Material(FindShader("Lit", "HDRP/Lit")) { enableInstancing = true };
        SetColor(mat, color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", 0f);
        Validate(mat);
        return mat;
    }

    /// <summary>A lit material with a colour texture (tiled by the mesh's UVs) and an optional normal map.</summary>
    public static Material LitTextured(Texture albedo, Texture normalMap, float smoothness = 0.05f) =>
        LitTextured(albedo, normalMap, Color.white, smoothness);

    /// <summary>
    /// A lit material that's see-through where its texture's alpha is low (worn paths over the ground).
    /// Doesn't cast shadows from the renderer's side; set that on the renderer.
    /// </summary>
    public static Material LitTransparent(Texture albedo, Color tint, float smoothness = 0.05f)
    {
        var mat = LitTextured(albedo, null, tint, smoothness);
        mat.enableInstancing = false;
        mat.SetFloat("_SurfaceType", 1f); // transparent
        mat.SetFloat("_BlendMode", 0f);   // alpha
        if (mat.HasProperty("_TransparentZWrite")) mat.SetFloat("_TransparentZWrite", 0f);
        Validate(mat);
        mat.renderQueue = (int)RenderQueue.Transparent - 50; // before other see-through things like smoke
        return mat;
    }

    /// <summary>As above, with the texture tinted by a colour.</summary>
    public static Material LitTextured(Texture albedo, Texture normalMap, Color tint, float smoothness = 0.05f)
    {
        var mat = Lit(tint, smoothness);
        mat.mainTexture = albedo;
        if (normalMap != null && mat.HasProperty("_NormalMap")) mat.SetTexture("_NormalMap", normalMap);
        Validate(mat);
        return mat;
    }

    /// <summary>An unlit material (HDRP/Unlit). Colours brighter than 1 glow when Bloom is enabled.</summary>
    public static Material Unlit(Color color)
    {
        var mat = new Material(FindShader("Unlit", "HDRP/Unlit"));
        SetColor(mat, color);
        Validate(mat);
        return mat;
    }

    /// <summary>
    /// A flat colour that ignores fog (Horror/Unlit No Fog), for things that must show through it from far away.
    /// Drawing with an early renderQueue and no depth writes puts it behind everything, like the sky.
    /// </summary>
    public static Material NoFog(Color color, int renderQueue = (int)RenderQueue.Geometry, bool writeDepth = true)
    {
        Shader shader = Shader.Find("Horror/Unlit No Fog");
        if (shader == null) return Unlit(color); // shader missing: fall back to fogged unlit
        var mat = new Material(shader);
        mat.SetColor("_Color", color);
        mat.SetFloat("_ZWrite", writeDepth ? 1f : 0f);
        mat.renderQueue = renderQueue;
        return mat;
    }

    /// <summary>Sets the main colour, whichever shader it is (HDRP Lit, HDRP Unlit or the game's own).</summary>
    public static void SetColor(Material mat, Color color)
    {
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_UnlitColor")) mat.SetColor("_UnlitColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
    }

    /// <summary>Makes an HDRP Lit material show (and light) both faces, for thin things like needle sprays.</summary>
    public static void SetDoubleSided(Material mat)
    {
        if (!mat.HasProperty("_DoubleSidedEnable")) return;
        mat.SetFloat("_DoubleSidedEnable", 1f);
        mat.doubleSidedGI = true;
        Validate(mat);
    }

    /// <summary>Scrolls a material's normal map (as well as its colour texture, which mainTextureOffset does).</summary>
    public static void SetNormalMapOffset(Material mat, Vector2 offset)
    {
        if (mat.HasProperty("_NormalMap")) mat.SetTextureOffset("_NormalMap", offset);
    }

    /// <summary>Sets the keywords and render states HDRP needs after a material's settings change.</summary>
    static void Validate(Material mat)
    {
        if (mat.shader != null && mat.shader.name.StartsWith("HDRP/")) HDMaterial.ValidateMaterial(mat);
    }
}
