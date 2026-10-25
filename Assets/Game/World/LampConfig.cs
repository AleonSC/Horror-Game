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

        [Range(0.5f, 20f), Tooltip("Whole turns of the crank from empty to full (it turns with the meter, and back as it drains).")]
        public float crankTurns = 4f;

        [Header("Lit")]
        [Range(1f, 40f), Tooltip("Radius of the safe light round a lit lamp (metres).")]
        public float safeRadius = 13f;
        public Color lightColor = new Color(1f, 0.75f, 0.45f);
        [Range(0f, 200f), Tooltip("Brightness (candela).")]
        public float lightIntensity = 14f;
        [Range(1f, 60f)] public float lightRange = 13f;
        [Range(0f, 20f), Tooltip("The soft glow round a lit lamp's glass.")]
        public float glassHalo = 0.35f;
        [Range(0.2f, 8f), Tooltip("How wide that glow is (metres).")]
        public float glassHaloSize = 1.4f;

        [Header("Campfires")]
        [Range(10f, 3600f), Tooltip("How long a campfire burns once you light it (seconds).")]
        public float campfireTime = 600f;
        [Range(0f, 300f), Tooltip("Over its last this-many seconds it dies down, and its safe circle shrinks with it.")]
        public float campfireDieDown = 60f;
        [Range(0f, 30f), Tooltip("Radius of the safe circle round a burning campfire (metres).")]
        public float campfireSafeRadius = 8f;
        [Range(0f, 1f), Tooltip("Lighting it uses this much of the lighter's fuel.")]
        public float campfireLighterFuel = 0.05f;
        public Color campfireColor = new Color(1f, 0.5f, 0.2f);
        [Range(0f, 200f)] public float campfireIntensity = 10f;
        [Range(1f, 40f)] public float campfireRange = 12f;
        [Range(0f, 1f), Tooltip("How much the fire lights the fog round it.")]
        public float campfireFogGlow = 0.25f;

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
