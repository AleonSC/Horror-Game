using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.Editor
{
    /// <summary>HDRP materials made from code, always validated so HDRP sets their keywords (section 4.5).</summary>
    public static class EditorMaterials
    {
        public static Material Lit(string name, Color color, float smoothness = 0.15f, bool doubleSided = false, Texture2D texture = null, Vector2? tiling = null)
        {
            var m = new Material(Shader.Find("HDRP/Lit")) { name = name };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            if (texture != null)
            {
                m.SetTexture("_BaseColorMap", texture);
                m.SetTextureScale("_BaseColorMap", tiling ?? Vector2.one);
            }
            if (doubleSided)
            {
                m.SetFloat("_DoubleSidedEnable", 1f);
                m.SetFloat("_DoubleSidedNormalMode", 1f); // mirror: both faces lit, so undersides aren't black
            }
            HDMaterial.ValidateMaterial(m);
            return m;
        }

        /// <summary>Flat colour, not lit (the scour's body).</summary>
        public static Material Unlit(string name, Color color)
        {
            var m = new Material(Shader.Find("HDRP/Unlit")) { name = name };
            m.SetColor("_UnlitColor", color);
            HDMaterial.ValidateMaterial(m);
            return m;
        }

        /// <summary>Glows by itself (eyes, bulbs). <paramref name="glow"/> is HDR: above 1 is brighter.</summary>
        public static Material Glow(string name, Color glow)
        {
            var m = new Material(Shader.Find("HDRP/Unlit")) { name = name };
            m.SetColor("_UnlitColor", Color.black);
            HDMaterial.SetEmissiveColor(m, glow);
            HDMaterial.ValidateMaterial(m);
            return m;
        }

        /// <summary>A lit material that also glows (a lamp's glass).</summary>
        public static Material LitGlow(string name, Color color, Color glow)
        {
            var m = Lit(name, color, 0.6f);
            HDMaterial.SetEmissiveColor(m, glow);
            HDMaterial.ValidateMaterial(m);
            return m;
        }
    }
}
