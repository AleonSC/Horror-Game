using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Runs the weather (section 6.13): sets the wind from WeatherConfig every frame (so changes show at once),
    /// growing gustier as lamps are lit. Only in the forest.
    /// </summary>
    public sealed class Weather : MonoBehaviour
    {
        WeatherConfig config;

        public WeatherSettings Settings => config.settings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (!Services.TryGet<Game.World.Build.WorldBuilder>(out _)) return;
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponent<Weather>() == null)
                root.gameObject.AddComponent<Weather>();
        }

        void Awake()
        {
            config = Services.Get<GameSetup>().Get<WeatherConfig>();
            if (config == null)
            {
                Debug.LogWarning("No WeatherConfig in the GameSetup asset; using defaults.");
                config = ScriptableObject.CreateInstance<WeatherConfig>();
            }
            Services.Register(this);
        }

        void OnDestroy() => Services.Unregister(this);

        int LampsLit => Services.TryGet<GameRun>(out var run) ? run.LampsLit : 0;

        void Update()
        {
            var s = config.settings;
            if (!Services.TryGet<Wind>(out var wind)) return;
            float a = s.direction * Mathf.Deg2Rad;
            wind.Direction = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
            wind.Breeze = s.breeze;
            wind.Gusts = s.gusts * (1f + s.gustsPerLamp * LampsLit);
            wind.GustSpeed = s.gustSpeed;
            wind.GustSpacing = s.gustSpacing;
        }
    }
}
