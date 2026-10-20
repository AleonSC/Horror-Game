using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Creates simple placeholder materials that work in both the Built-in render pipeline and URP.
/// </summary>
public static class MaterialFactory
{
    static bool UsingSrp => GraphicsSettings.currentRenderPipeline != null;

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

    /// <summary>A rough, lit material (Standard or URP Lit).</summary>
    public static Material Lit(Color color, float smoothness = 0.05f)
    {
        Shader shader = UsingSrp
            ? FindShader("Lit", "Universal Render Pipeline/Lit", "Universal Render Pipeline/Simple Lit")
            : FindShader("Lit", "Standard", "Diffuse");

        var mat = new Material(shader) { enableInstancing = true };
        SetColor(mat, color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
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
        if (mat.HasProperty("_Surface"))
        {
            // URP Lit
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", 0f);
            mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite", 0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        }
        else if (mat.HasProperty("_Mode"))
        {
            // Built-in Standard, "Fade" mode
            mat.SetFloat("_Mode", 2f);
            mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_ALPHABLEND_ON");
        }
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.renderQueue = (int)RenderQueue.Transparent - 50; // before other see-through things like smoke
        return mat;
    }

    /// <summary>
    /// A lit, alpha-tested material: solid where the texture's alpha is over cutoff, a hole elsewhere (leaves).
    /// Both faces drawn where the pipeline supports it.
    /// </summary>
    public static Material LitCutout(Texture albedo, Color tint, float cutoff = 0.5f, float smoothness = 0.05f)
    {
        var mat = LitTextured(albedo, null, tint, smoothness);
        if (mat.HasProperty("_AlphaClip"))
        {
            // URP Lit
            mat.SetFloat("_AlphaClip", 1f);
            mat.SetFloat("_Cutoff", cutoff);
            mat.EnableKeyword("_ALPHATEST_ON");
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", (float)CullMode.Off);
        }
        else if (mat.HasProperty("_Mode"))
        {
            // Built-in Standard, "Cutout" mode
            mat.SetFloat("_Mode", 1f);
            mat.SetFloat("_Cutoff", cutoff);
            mat.EnableKeyword("_ALPHATEST_ON");
        }
        mat.SetOverrideTag("RenderType", "TransparentCutout");
        mat.renderQueue = (int)RenderQueue.AlphaTest;
        return mat;
    }

    /// <summary>As above, with the texture tinted by a colour.</summary>
    public static Material LitTextured(Texture albedo, Texture normalMap, Color tint, float smoothness = 0.05f)
    {
        var mat = Lit(tint, smoothness);
        mat.mainTexture = albedo;
        if (normalMap != null && mat.HasProperty("_BumpMap"))
        {
            mat.SetTexture("_BumpMap", normalMap);
            mat.EnableKeyword("_NORMALMAP");
        }
        return mat;
    }

    /// <summary>An unlit material. Colors brighter than 1 glow when Bloom is enabled.</summary>
    public static Material Unlit(Color color)
    {
        Shader shader = UsingSrp
            ? FindShader("Unlit", "Universal Render Pipeline/Unlit", "Unlit/Color")
            : FindShader("Unlit", "Unlit/Color");

        var mat = new Material(shader);
        SetColor(mat, color);
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

    /// <summary>Sets the main color on either pipeline's shader.</summary>
    public static void SetColor(Material mat, Color color)
    {
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
    }
}
