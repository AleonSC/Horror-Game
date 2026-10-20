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

    /// <summary>A lit material with a colour texture (tiled by the mesh's UVs) and an optional normal map.</summary>
    public static Material LitTextured(Texture albedo, Texture normalMap, float smoothness = 0.05f)
    {
        var mat = Lit(Color.white, smoothness);
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
        Shader shader = Shader.Find(UsingSrp ? "Universal Render Pipeline/Unlit" : "Unlit/Color");
        if (shader == null) shader = Shader.Find("Unlit/Color");

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
