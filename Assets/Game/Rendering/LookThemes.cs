using System;
using UnityEngine;

namespace Game.Rendering
{
    /// <summary>
    /// Ready-made looks (section 4.3 of the plan). Choosing one copies its values into the LookConfig; tune from
    /// there. "Reference" (matching the owner's reference photo) is the default.
    /// </summary>
    public static class LookThemes
    {
        public const string Default = "Reference";

        public static readonly string[] Names =
        {
            "Reference", "Owner Dark", "Darker", "Pitch Black", "Blood Moon", "Foggy", "Sickly Green", "Dusk", "Bright (testing)",
        };

        /// <summary>The settings for a theme, or null if there's no theme of that name.</summary>
        public static LookSettings Get(string name)
        {
            var s = new LookSettings(); // the Reference values
            switch (name)
            {
                case "Reference":
                    return s;

                case "Owner Dark": // the owner's chosen lighting from the prototype: near-black fog, no vignette or grain
                    s.postExposure = 1.63f;
                    s.saturation = 40f;
                    s.moonColor = new Color(0.45f, 0.55f, 0.80f);
                    s.moonBrightness = 0.25f;
                    s.fogGlow = 0.55f;
                    s.mistThickness = 5f;
                    s.fogColor = new Color(0.02f, 0.025f, 0.035f);
                    s.fogDensity = 0.05f;
                    s.vignette = 0f;
                    s.grain = 0f;
                    s.mistColor = new Color(0.45f, 0.48f, 0.55f);
                    return s;

                case "Darker":
                    s.postExposure = -0.1f;
                    s.fogColor = new Color(0.09f, 0.10f, 0.12f);
                    s.ambient = new Color(0.035f, 0.04f, 0.055f);
                    s.moonBrightness = 0.15f;
                    s.mistColor = new Color(0.6f, 0.62f, 0.66f);
                    return s;

                case "Pitch Black":
                    s.postExposure = -0.4f;
                    s.fogColor = new Color(0.01f, 0.012f, 0.016f);
                    s.fogDensity = 0.07f;
                    s.ambient = new Color(0.01f, 0.012f, 0.016f);
                    s.moonBrightness = 0.03f;
                    s.mistColor = new Color(0.3f, 0.32f, 0.36f);
                    s.vignette = 0.45f;
                    return s;

                case "Blood Moon":
                    s.fogColor = new Color(0.16f, 0.05f, 0.045f);
                    s.ambient = new Color(0.08f, 0.03f, 0.03f);
                    s.moonColor = new Color(0.9f, 0.25f, 0.15f);
                    s.moonBrightness = 0.35f;
                    s.mistColor = new Color(0.75f, 0.45f, 0.42f);
                    s.saturation = 55f;
                    return s;

                case "Foggy":
                    s.fogDensity = 0.12f;
                    s.fogColor = new Color(0.2f, 0.21f, 0.23f);
                    s.mistThickness = 2.5f;
                    s.mistHeight = 2.2f;
                    s.mistPatchiness = 0.4f;
                    return s;

                case "Sickly Green":
                    s.fogColor = new Color(0.12f, 0.16f, 0.10f);
                    s.ambient = new Color(0.05f, 0.08f, 0.045f);
                    s.moonColor = new Color(0.55f, 0.8f, 0.45f);
                    s.mistColor = new Color(0.65f, 0.78f, 0.6f);
                    s.colorFilter = new Color(0.92f, 1f, 0.88f);
                    return s;

                case "Dusk":
                    s.fogColor = new Color(0.22f, 0.2f, 0.28f);
                    s.ambient = new Color(0.12f, 0.1f, 0.16f);
                    s.moonColor = new Color(0.95f, 0.6f, 0.45f);
                    s.moonBrightness = 0.8f;
                    s.moonHeight = 12f;
                    s.mistColor = new Color(0.85f, 0.78f, 0.82f);
                    return s;

                case "Bright (testing)":
                    s.postExposure = 4f;
                    s.fogDensity = 0.012f;
                    s.ambient = new Color(0.3f, 0.32f, 0.36f);
                    s.moonBrightness = 3f;
                    s.vignette = 0f;
                    s.grain = 0f;
                    return s;

                default:
                    return null;
            }
        }

        public static bool Exists(string name) => Array.IndexOf(Names, name) >= 0;
    }
}
