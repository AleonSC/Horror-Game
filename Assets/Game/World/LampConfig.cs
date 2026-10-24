using System;
using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>The lamps (section 6.4 of the plan), in Assets/Config/World/LampConfig.asset.</summary>
    [CreateAssetMenu(menuName = "Game/Config/Lamp", fileName = "LampConfig")]
    public sealed class LampConfig : GameConfig
    {
        public LampSettings settings = new LampSettings();
    }

    [Serializable]
    public sealed class LampSettings
    {
        [Header("Cranking")]
        [Range(0.01f, 0.5f), Tooltip("How much each click fills the meter.")]
        public float perClick = 0.06f;
        [Range(0f, 1f), Tooltip("How fast the meter drains once you stop (share per second).")]
        public float drainRate = 0.08f;
        [Range(0f, 5f), Tooltip("Seconds after the last click before it starts draining.")]
        public float drainDelay = 1.5f;

        [Header("Lit")]
        [Range(1f, 40f), Tooltip("Radius of the safe light round a lit lamp (metres).")]
        public float safeRadius = 13f;
        public Color lightColor = new Color(1f, 0.75f, 0.45f);
        [Range(0f, 200f), Tooltip("Brightness (candela).")]
        public float lightIntensity = 25f;
        [Range(1f, 60f)] public float lightRange = 13f;
        [Range(0f, 20f), Tooltip("The soft glow round a lit lamp's glass.")]
        public float glassHalo = 1.5f;
        [Range(0.2f, 8f), Tooltip("How wide that glow is (metres).")]
        public float glassHaloSize = 2.4f;

        [Header("Unlit")]
        [Tooltip("The blinking marker on top of a dark lamp.")]
        public Color markerColor = new Color(1f, 0.05f, 0.02f);
        [Range(0f, 20f)] public float markerGlow = 4f;
        [Range(0f, 20f), Tooltip("The soft red glow round the marker (seen through the fog).")]
        public float markerHalo = 1.2f;
        [Range(0.1f, 5f), Tooltip("Blinks per second.")]
        public float markerBlinkRate = 0.8f;

        public LampSettings Clone() => (LampSettings)MemberwiseClone();
    }
}
