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

        public WeatherSettings Clone() => (WeatherSettings)MemberwiseClone();
    }
}
