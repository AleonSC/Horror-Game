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
    /// An unlit, see-through textured material, either alpha-blended or additive (glows, light beams).
    /// Double-sided, doesn't write depth. renderQueue lets things like the moon draw behind everything.
    /// </summary>
    public static Material Transparent(Texture texture, Color color, bool additive, int renderQueue = (int)RenderQueue.Transparent)
    {
        Material mat;
        if (UsingSrp)
        {
            mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            float src = (float)BlendMode.SrcAlpha;
            float dst = additive ? (float)BlendMode.One : (float)BlendMode.OneMinusSrcAlpha;
            mat.SetFloat("_Surface", 1f);
            mat.SetFloat("_Blend", additive ? 2f : 0f);
            mat.SetFloat("_SrcBlend", src);
            mat.SetFloat("_DstBlend", dst);
            if (mat.HasProperty("_SrcBlendAlpha")) mat.SetFloat("_SrcBlendAlpha", src);
            if (mat.HasProperty("_DstBlendAlpha")) mat.SetFloat("_DstBlendAlpha", dst);
            mat.SetFloat("_ZWrite", 0f);
            if (mat.HasProperty("_Cull")) mat.SetFloat("_Cull", 0f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
        else
        {
            Shader shader = Shader.Find(additive ? "Legacy Shaders/Particles/Additive" : "Legacy Shaders/Particles/Alpha Blended");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");
            mat = new Material(shader);
            color *= 0.5f; // these shaders double the tint
        }

        mat.mainTexture = texture;
        SetColor(mat, color);
        if (mat.HasProperty("_TintColor")) mat.SetColor("_TintColor", color);
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
