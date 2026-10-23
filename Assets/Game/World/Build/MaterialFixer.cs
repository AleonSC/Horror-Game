using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.World.Build
{
    /// <summary>
    /// Makes imported materials work here (section 4.5 of the plan). A material whose shader has no HDRP pass
    /// (Standard, the old Tree Creator shaders: they show up pink) is swapped for an HDRP/Lit stand-in with its
    /// texture and colour; leaves are cut out and lit on both sides. Every material used for instancing is a
    /// copy with GPU instancing on, so the project's own assets are never changed.
    /// </summary>
    public sealed class MaterialFixer
    {
        readonly Dictionary<Material, Material> done = new Dictionary<Material, Material>();
        readonly List<Object> made;

        public MaterialFixer(List<Object> made) => this.made = made;

        public static bool WorksInHdrp(Material m) =>
            m != null && m.shader != null && (m.FindPass("GBuffer") >= 0 || m.FindPass("Forward") >= 0 || m.FindPass("ForwardOnly") >= 0);

        /// <summary>A copy of <paramref name="source"/> ready for instancing in HDRP (converted if it needs to be).</summary>
        public Material Fix(Material source)
        {
            if (source == null) return null;
            if (done.TryGetValue(source, out var fixedUp)) return fixedUp;

            Material m;
            if (WorksInHdrp(source)) m = new Material(source);
            else
            {
                m = new Material(Shader.Find("HDRP/Lit")) { name = source.name + " (HDRP)" };
                var tex = source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : source.mainTexture;
                if (tex != null) m.SetTexture("_BaseColorMap", tex);
                Color c = source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
                m.SetColor("_BaseColor", c);
                if (source.HasProperty("_BumpMap") && source.GetTexture("_BumpMap") != null) m.SetTexture("_NormalMap", source.GetTexture("_BumpMap"));
                m.SetFloat("_Smoothness", 0.15f);
                if (IsFoliage(source))
                {
                    HDMaterial.SetAlphaClipping(m, true);
                    HDMaterial.SetAlphaCutoff(m, source.HasProperty("_Cutoff") ? source.GetFloat("_Cutoff") : 0.4f);
                    m.SetFloat("_DoubleSidedEnable", 1f);
                    m.SetFloat("_DoubleSidedNormalMode", 2f); // none: both faces lit the same, so undersides aren't black
                }
                Debug.Log($"Converted '{source.name}' ({source.shader.name}) to an HDRP/Lit stand-in.");
            }
            m.enableInstancing = true;
            HDMaterial.ValidateMaterial(m);
            made.Add(m);
            done[source] = m;
            return m;
        }

        static bool IsFoliage(Material m)
        {
            string n = (m.name + " " + m.shader.name).ToLowerInvariant();
            return n.Contains("leaf") || n.Contains("leaves") || n.Contains("needle") || n.Contains("foliage") || n.Contains("grass") || n.Contains("cutout") || n.Contains("frond");
        }
    }
}
