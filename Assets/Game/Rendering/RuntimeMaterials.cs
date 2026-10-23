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

        public static void SetGlow(Material m, Color glow)
        {
            if (m != null) m.SetColor("_EmissiveColor", glow);
        }
    }
}
