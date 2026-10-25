using System;
using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>The wind and weather (section 6.13 of the plan), in Assets/Config/World/WeatherConfig.asset.</summary>
    [CreateAssetMenu(menuName = "Game/Config/Weather", fileName = "WeatherConfig")]
    public sealed class WeatherConfig : GameConfig
    {
        public WeatherSettings settings = new WeatherSettings();
    }

    [Serializable]
    public sealed class WeatherSettings
    {
        [Header("Wind")]
        [Range(0f, 1f), Tooltip("The steady sway, always there.")]
        public float breeze = 0.25f;
        [Range(0f, 2f), Tooltip("How hard the gusts push.")]
        public float gusts = 0.9f;
        [Range(0f, 40f), Tooltip("How fast gusts roll across the forest (metres a second).")]
        public float gustSpeed = 9f;
        [Range(10f, 300f), Tooltip("Metres between one gust and the next.")]
        public float gustSpacing = 70f;
        [Range(0f, 360f), Tooltip("Which way the wind blows (degrees).")]
        public float direction = 37f;
        [Range(0f, 1f), Tooltip("Each lamp lit makes the gusts this much stronger (0.05 = 5%).")]
        public float gustsPerLamp = 0.04f;

        [Header("Rain (grows at the 5th and 7th lamps)")]
        [Range(0f, 1f), Tooltip("How hard it rains from the start (0: dry until the 5th lamp).")]
        public float rainAtStart = 0.2f;
        [Range(0f, 1f)] public float rainAtFifthLamp = 0.55f;
        [Range(0f, 1f)] public float rainAtSeventhLamp = 1f;
        [Range(100, 8000), Tooltip("Drops round you at full rain.")]
        public int maxDrops = 3000;
        [Range(2f, 30f), Tooltip("How fast drops fall (metres a second).")]
        public float dropSpeed = 11f;
        [Range(0f, 1f), Tooltip("How visible the drops are.")]
        public float dropBrightness = 0.25f;

        [Header("Lightning")]
        [Range(0f, 1f), Tooltip("Share of strikes that show a bolt down through the sky (the rest flicker in the cloud).")]
        public float boltChance = 0.75f;
        [Range(0f, 200f), Tooltip("How bright the bolt itself is (the fog dims it with distance).")]
        public float boltBrightness = 30f;
        [Range(2f, 120f), Tooltip("A flash every this many seconds (shortest)...")]
        public float minLightning = 15f;
        [Range(2f, 200f), Tooltip("...and longest.")]
        public float maxLightning = 45f;
        [Range(0f, 50f), Tooltip("How bright a flash is (lux).")]
        public float lightningBrightness = 3f;
        [Range(100f, 5000f), Tooltip("Strikes are between this close...")]
        public float minLightningDistance = 400f;
        [Range(100f, 5000f), Tooltip("...and this far (the thunder comes later the further it is).")]
        public float maxLightningDistance = 2500f;

        [Header("Tornadoes (from the 8th lamp)")]
        [Range(1, 10)] public int tornadoesAtLamp = 8;
        [Range(0, 6)] public int tornadoes = 2;
        [Range(200f, 3000f), Tooltip("How far away they stay (metres).")]
        public float tornadoDistance = 900f;
        [Range(50f, 1500f)] public float tornadoHeight = 420f;

        public WeatherSettings Clone() => (WeatherSettings)MemberwiseClone();
    }
}
