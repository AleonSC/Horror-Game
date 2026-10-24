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

        /// <summary>
        /// A flat colour that the fog doesn't touch (transparent, fog off): something vast and far away that should
        /// stay a clear silhouette through any fog (the giant, its chains, the eye). Set the colour already hazed.
        /// </summary>
        public static Material Haze(string name, Color color)
        {
            var m = Unlit(name, color, true);
            HDMaterial.SetSurfaceType(m, true);
            m.SetFloat("_EnableFogOnTransparent", 0f);
            HDMaterial.ValidateMaterial(m);
            return m;
        }

        /// <summary>Set a haze material's colour, always fully opaque (dividing a colour by the exposure would thin its alpha too).</summary>
        public static void SetHazeColor(Material m, Color c)
        {
            if (m == null) return;
            c.a = 1f;
            m.SetColor("_UnlitColor", c);
        }

        /// <summary>
        /// A silhouette that darkens what's behind it instead of covering it (transparent, fog off, black): something
        /// vast and far away (the giant, the flier, a tornado). Behind it is the fog, so it reads the same as a hazed
        /// colour; but the fog in front, lit by the flashlight, is behind it on screen too, so the beam's glow still
        /// shows through instead of being cut out. The depth prepass keeps its own parts from darkening each other.
        /// Set how dark with <see cref="SetSilhouetteShade"/>.
        /// </summary>
        public static Material Silhouette(string name)
        {
            var m = Unlit(name, new Color(0f, 0f, 0f, 0.5f), true);
            HDMaterial.SetSurfaceType(m, true);
            m.SetFloat("_EnableFogOnTransparent", 0f);
            m.SetFloat("_TransparentDepthPrepassEnable", 1f);
            HDMaterial.ValidateMaterial(m);
            return m;
        }

        /// <summary>What's behind it is kept by <paramref name="shade"/>: 0 black, 1 not there (the fog colour times this).</summary>
        public static void SetSilhouetteShade(Material m, float shade)
        {
            if (m != null) m.SetColor("_UnlitColor", new Color(0f, 0f, 0f, 1f - Mathf.Clamp01(shade)));
        }

        /// <summary>
        /// Part solid, part darkening: <paramref name="through"/> 0 is a flat <paramref name="fog"/> x shade (a crisp
        /// silhouette, but it covers the flashlight's glow), 1 darkens what's behind by shade (the glow shows through,
        /// but so does all the lit fog, which softens it). <paramref name="fog"/> is the fog as it shows on screen.
        /// </summary>
        public static void SetSilhouette(Material m, float shade, Color fog, float through)
        {
            if (m == null) return;
            shade = Mathf.Clamp01(shade);
            through = Mathf.Clamp01(through);
            float a = Mathf.Max(0.001f, 1f - through * shade);
            Color c = fog * (shade * (1f - through) / a);
            c.a = a;
            m.SetColor("_UnlitColor", c);
        }

        /// <summary>Draw this transparent material after others at the same spot (higher: later).</summary>
        public static void DrawAfter(Material m, int priority)
        {
            m.SetFloat("_TransparentSortPriority", priority);
            HDMaterial.ValidateMaterial(m);
        }

        public static void SetGlow(Material m, Color glow)
        {
            if (m == null) return;
            if (m.HasProperty("_GlowColor")) m.SetColor("_GlowColor", glow); // (the game's own effect shaders)
            else m.SetColor("_EmissiveColor", glow);
        }
    }
}
