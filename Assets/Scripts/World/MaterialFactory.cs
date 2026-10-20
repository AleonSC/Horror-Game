using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Creates simple placeholder materials that work in both the Built-in render pipeline and URP.
/// </summary>
public static class MaterialFactory
{
    static bool UsingSrp => GraphicsSettings.currentRenderPipeline != null;

    /// <summary>A rough, lit material (Standard or URP Lit).</summary>
    public static Material Lit(Color color, float smoothness = 0.05f)
    {
        Shader shader = UsingSrp ? Shader.Find("Universal Render Pipeline/Lit") : Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
        if (shader == null) shader = Shader.Find("Diffuse");

        var mat = new Material(shader) { enableInstancing = true };
        SetColor(mat, color);
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", smoothness);
        return mat;
    }

    /// <summary>An unlit material. Colors brighter than 1 glow when Bloom is enabled.</summary>
    public static Material Unlit(Color color)
    {
        Shader shader = Shader.Find(UsingSrp ? "Universal Render Pipeline/Unlit" : "Unlit/Color");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        var mat = new Material(shader);
        SetColor(mat, color);
        return mat;
    }

    /// <summary>
    /// A transparent particle material (rain, splashes). Lit particles only show where light
    /// hits them, e.g. in the flashlight beam.
    /// </summary>
    public static Material Particle(Color color, bool lit = false)
    {
        Shader shader = UsingSrp
            ? Shader.Find(lit ? "Universal Render Pipeline/Particles/Simple Lit" : "Universal Render Pipeline/Particles/Unlit")
            : Shader.Find(lit ? "Particles/Standard Surface" : "Legacy Shaders/Particles/Alpha Blended");
        if (shader == null) shader = Shader.Find(UsingSrp ? "Universal Render Pipeline/Particles/Unlit" : "Particles/Standard Unlit");

        var mat = new Material(shader);

        // Switch the shader from opaque to alpha-blended (property names are shared by URP and Standard particles).
        if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f); // URP
        if (mat.HasProperty("_Mode")) mat.SetFloat("_Mode", 2f);       // Built-in Standard particles: Fade
        if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
        if (mat.HasProperty("_SrcBlend")) mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
        if (mat.HasProperty("_DstBlend")) mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        if (mat.HasProperty("_ZWrite")) mat.SetFloat("_ZWrite", 0f);
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.renderQueue = (int)RenderQueue.Transparent;

        SetColor(mat, color);
        if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", color);
        return mat;
    }

    /// <summary>Sets the main color on either pipeline's shader.</summary>
    public static void SetColor(Material mat, Color color)
    {
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
    }
}
