using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.Rendering
{
    /// <summary>
    /// HDRP materials made while the game runs (creatures, portals), always validated so HDRP sets their
    /// keywords. (Builds will need these shader variants kept: see section 4.5 of the plan.)
    /// </summary>
    public static class RuntimeMaterials
    {
        /// <summary>A flat colour, not lit (the scour's black body).</summary>
        public static Material Unlit(string name, Color color, bool doubleSided = false)
        {
            var m = new Material(Shader.Find("HDRP/Unlit")) { name = name };
            m.SetColor("_UnlitColor", color);
            if (doubleSided) m.SetFloat("_DoubleSidedEnable", 1f);
            HDMaterial.ValidateMaterial(m);
            return m;
        }

        /// <summary>Glows by itself; <paramref name="glow"/> is HDR. Change it later with <see cref="SetGlow"/>.</summary>
        public static Material Glow(string name, Color glow, bool doubleSided = false)
        {
            var m = Unlit(name, Color.black, doubleSided);
            HDMaterial.SetEmissiveColor(m, glow);
            HDMaterial.ValidateMaterial(m);
            return m;
        }

        /// <summary>A lit material (the scour's fangs, or its optional dark lit body).</summary>
        public static Material Lit(string name, Color color, float smoothness, bool doubleSided = false)
        {
            var m = new Material(Shader.Find("HDRP/Lit")) { name = name };
            m.SetColor("_BaseColor", color);
            m.SetFloat("_Smoothness", smoothness);
            m.SetFloat("_Metallic", 0f);
            if (doubleSided)
            {
                m.SetFloat("_DoubleSidedEnable", 1f);
                m.SetFloat("_DoubleSidedNormalMode", 1f);
            }
            HDMaterial.ValidateMaterial(m);
            return m;
        }

        /// <summary>A lit material with a texture, tiled, ready for GPU instancing (the forest's ground, plants, rocks).</summary>
        public static Material LitTextured(string name, Color color, float smoothness, Texture2D texture, Vector2 tiling, bool doubleSided = false)
        {
            var m = Lit(name, color, smoothness, doubleSided);
            if (texture != null)
            {
                m.SetTexture("_BaseColorMap", texture);
                m.SetTextureScale("_BaseColorMap", tiling);
            }
            m.enableInstancing = true;
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

        public static void SetGlow(Material m, Color glow)
        {
            if (m != null) m.SetColor("_EmissiveColor", glow);
        }
    }
}
