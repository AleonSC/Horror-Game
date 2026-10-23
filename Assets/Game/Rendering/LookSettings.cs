using System;
using UnityEngine;

namespace Game.Rendering
{
    /// <summary>
    /// Every number behind the night's look (section 4 of the plan). Colours are as they appear on screen,
    /// before Post Exposure: the controller converts them for HDRP. The dev window's Look tab shows these
    /// fields grouped by their headers.
    /// </summary>
    [Serializable]
    public sealed class LookSettings
    {
        [Header("Exposure")]
        [Range(-6f, 6f), Tooltip("Fixed exposure (EV100). Lower is brighter. -1.9 makes lights read as intended.")]
        public float exposure = -1.9f;
        [Tooltip("Neutral tonemapping instead of none (none keeps the dark end; ACES would crush it).")]
        public bool neutralTonemapping;

        [Header("Picture")]
        [Range(-3f, 5f), Tooltip("Brightens or darkens the whole picture after lighting.")]
        public float postExposure = 1.63f;
        [Range(-100f, 100f)] public float contrast = 35f;
        [Range(-100f, 100f)] public float saturation = 40f;
        [Tooltip("Tints the whole picture (white: no tint).")]
        public Color colorFilter = Color.white;
        [Range(0f, 1f), Tooltip("Darkened corners.")]
        public float vignette = 0.3f;
        [Range(0f, 1f), Tooltip("Film grain.")]
        public float grain = 0.2f;

        [Header("Fog")]
        [Tooltip("The fog's colour: pale and brighter than the shadows, so trees stand dark against it.")]
        public Color fogColor = new Color(0.16f, 0.17f, 0.20f);
        [Range(0.001f, 0.3f), Tooltip("How thick the fog is. 0.06 hides things about 15 m away.")]
        public float fogDensity = 0.06f;
        [Tooltip("Lights shine through the fog (beams, glows). Off is the classic look.")]
        public bool volumetricFog = true;
        [Range(0f, 1f), Tooltip("How strongly lights show up in the fog.")]
        public float fogGlow = 0.55f;
        [Range(-1f, 1f), Tooltip("Above 0 the fog glows most looking toward a light (halos round beams).")]
        public float fogAnisotropy = 0.2f;
        [Range(20f, 1000f), Tooltip("How far out lights show in the fog (metres). Never less than the camera sees, so there's no hard line where it gives way to plain fog.")]
        public float fogReach = 160f;
        [Range(0f, 2f), Tooltip("How much lamps and other round lights glow in the fog. Low, or every lamp blazes.")]
        public float lampFogGlow = 0.3f;

        [Header("Ambient and sky")]
        [Tooltip("The faint light everywhere, and the sky's colour behind the fog.")]
        public Color ambient = new Color(0.06f, 0.07f, 0.09f);
        [Range(0f, 4f)] public float ambientStrength = 1f;

        [Header("Moon")]
        public Color moonColor = new Color(0.45f, 0.55f, 0.80f);
        [Range(0f, 5f), Tooltip("Moonlight brightness (lux).")]
        public float moonBrightness = 0.25f;
        [Range(5f, 90f), Tooltip("How high the moon is (degrees).")]
        public float moonHeight = 35f;
        [Range(0f, 360f), Tooltip("Which way the moonlight comes from (degrees round).")]
        public float moonDirection = 210f;
        public bool moonShadows = true;
        [Range(0f, 10f), Tooltip("How soft the moon's shadows are.")]
        public float moonShadowSoftness = 3f;

        [Header("Ground mist")]
        public bool mist = true;
        [Tooltip("The mist's colour.")]
        public Color mistColor = new Color(0.80f, 0.82f, 0.86f);
        [Range(0.5f, 60f), Tooltip("How thick the mist is: the distance you can see through it (metres). Lower is thicker.")]
        public float mistThickness = 5f;
        [Range(0.3f, 6f), Tooltip("How high the mist reaches (metres).")]
        public float mistHeight = 1.4f;
        [Range(10f, 120f), Tooltip("How far round you the mist reaches (metres).")]
        public float mistReach = 45f;
        [Range(2f, 60f), Tooltip("Size of the mist's patches (metres).")]
        public float mistPatchSize = 14f;
        [Range(0f, 1f), Tooltip("How patchy the mist is: 0 even, 1 broken up.")]
        public float mistPatchiness = 0.65f;
        [Range(0f, 3f), Tooltip("How fast the mist drifts (metres a second).")]
        public float mistDrift = 0.25f;

        public LookSettings Clone() => (LookSettings)MemberwiseClone();
    }
}
