using System.Collections.Generic;
using Game.Audio;
using Game.Core;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.World
{
    /// <summary>
    /// Runs the weather (section 6.13): the wind (gustier as lamps are lit), rain that grows at the 5th and 7th lamps,
    /// lightning every so often with thunder after it (later the further away it struck), and tornadoes on the horizon
    /// from the 8th lamp. Settings come from WeatherConfig every frame, so changes show at once. Only in the forest.
    /// </summary>
    public sealed class Weather : MonoBehaviour
    {
        WeatherConfig config;
        Rain rain;
        AudioSource rainSound, tornadoSound;
        Light flash;
        LightningBolt bolt;
        bool boltShown;
        float nextLightning, flashLeft, thunderAt = -1f, thunderVolume, rainAmount;
        readonly List<Tornado> tornadoes = new List<Tornado>();
        System.IDisposable resetSub;

        public WeatherSettings Settings => config.settings;
        public float RainAmount => rainAmount;

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
            rain = new Rain(Mathf.Max(100, config.settings.maxDrops));

            var flashGo = new GameObject("Lightning");
            flashGo.transform.SetParent(transform, false);
            flashGo.AddHDLight(LightType.Directional);
            flash = flashGo.GetComponent<Light>();
            flash.color = new Color(0.8f, 0.85f, 1f);
            flash.shadows = LightShadows.None;
            flash.enabled = false;
            bolt = LightningBolt.Create(transform);
            nextLightning = Time.time + Random.Range(config.settings.minLightning, config.settings.maxLightning);
            Services.Register(this);
        }

        void Start()
        {
            if (Services.TryGet<SoundPlayer>(out var sounds))
            {
                rainSound = Loop(sounds, Sound.RainLoop);
                tornadoSound = Loop(sounds, Sound.WindLoop);
                Muffled.On(rainSound);   // (rain drumming on the roof instead, through it)
                Muffled.On(tornadoSound);
                tornadoSound.pitch = 0.45f;
            }
            if (Services.TryGet<EventBus>(out var events)) resetSub = events.Subscribe<RunReset>(_ => ClearTornadoes());
        }

        void OnDestroy()
        {
            resetSub?.Dispose();
            rain?.Destroy();
            Services.Unregister(this);
        }

        AudioSource Loop(SoundPlayer sounds, Sound sound)
        {
            var go = new GameObject(sound.ToString());
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.clip = sounds.Clip(sound);
            src.loop = true;
            src.volume = 0f;
            src.spatialBlend = 0f;
            src.outputAudioMixerGroup = sounds.Group(MixerNames.Outside);
            if (src.clip != null) src.Play();
            return src;
        }

        int LampsLit => Services.TryGet<GameRun>(out var run) ? run.LampsLit : 0;

        void Update()
        {
            var s = config.settings;
            float dt = Time.deltaTime;
            var wind = Services.TryGet<Wind>(out var w) ? w : null;
            int lamps = LampsLit;
            if (wind != null)
            {
                float a = s.direction * Mathf.Deg2Rad;
                wind.Direction = new Vector2(Mathf.Sin(a), Mathf.Cos(a));
                wind.Breeze = s.breeze;
                wind.Gusts = s.gusts * (1f + s.gustsPerLamp * lamps);
                wind.GustSpeed = s.gustSpeed;
                wind.GustSpacing = s.gustSpacing;
            }

            var cam = Camera.main;
            bool outside = !PlayerPlace.InDarkRoom;
            float mul = Services.TryGet<LookController>(out var look) ? LookController.ExposureMultiplier(look.Config.settings.exposure) : 1f;

            // Rain: heavier at the 5th and 7th lamps, easing in over a while.
            float wantRain = !outside ? 0f : lamps >= 7 ? s.rainAtSeventhLamp : lamps >= 5 ? s.rainAtFifthLamp : s.rainAtStart;
            rainAmount = Mathf.MoveTowards(rainAmount, wantRain, dt / 20f);
            rain.Update(cam, rainAmount, s, wind, mul);
            // (a drizzle hardly heard, a downpour clearly; under a roof, quieter still as well as muffled)
            float rainVolume = Mathf.Pow(rainAmount, 1.6f) * s.rainVolume * (1f - 0.6f * SoundPlayer.Muffle);
            if (rainSound != null) rainSound.volume = Mathf.Lerp(rainSound.volume, rainVolume, 1f - Mathf.Exp(-dt));

            Lightning(dt, s, outside);
            Tornadoes(dt, s, lamps, cam, look, mul, outside);
        }

        // A flash somewhere far off (the whole forest lit for a moment), and its thunder later.
        void Lightning(float dt, WeatherSettings s, bool outside)
        {
            if (outside && Time.time >= nextLightning)
            {
                nextLightning = Time.time + Random.Range(s.minLightning, Mathf.Max(s.minLightning, s.maxLightning));
                float d = Random.Range(s.minLightningDistance, Mathf.Max(s.minLightningDistance, s.maxLightningDistance));
                flashLeft = Random.Range(0.25f, 0.6f);
                float bearing = Random.Range(0f, 360f);
                // (the flash lights the forest from where the bolt is)
                flash.transform.rotation = Quaternion.Euler(Random.Range(25f, 70f), bearing + 180f, 0f);
                // Most strikes show a bolt down through the sky; some are only a flicker in the cloud.
                boltShown = Random.value < s.boltChance && Camera.main != null;
                if (boltShown) bolt.Strike(Camera.main.transform.position, bearing, d, s.boltBrightness);
                thunderAt = Time.time + d / 343f;
                thunderVolume = Mathf.Lerp(1f, 0.35f, Mathf.InverseLerp(s.minLightningDistance, s.maxLightningDistance, d));
            }
            if (flashLeft > 0f)
            {
                flashLeft -= dt;
                // Two or three flickers, not one smooth pulse.
                float f = Mathf.PerlinNoise(Time.time * 30f, 0.4f) > 0.45f ? 1f : 0.15f;
                flash.enabled = flashLeft > 0f;
                flash.intensity = s.lightningBrightness * f;
                if (boltShown) bolt.Level(f);
            }
            else
            {
                flash.enabled = false;
                if (boltShown) { bolt.Hide(); boltShown = false; }
            }
            if (thunderAt > 0f && Time.time >= thunderAt)
            {
                thunderAt = -1f;
                if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play2D(Sound.Thunder, MixerNames.Outside, thunderVolume, Random.Range(0.75f, 1f));
            }
        }

        void Tornadoes(float dt, WeatherSettings s, int lamps, Camera cam, LookController look, float mul, bool outside)
        {
            if (lamps >= s.tornadoesAtLamp && tornadoes.Count == 0 && s.tornadoes > 0)
                for (int i = 0; i < s.tornadoes; i++)
                    tornadoes.Add(Tornado.Create(transform, Random.Range(0f, 360f), s.tornadoDistance * Random.Range(0.8f, 1.3f), s.tornadoHeight * Random.Range(0.8f, 1.2f), i));
            if (tornadoSound != null) tornadoSound.volume = Mathf.Lerp(tornadoSound.volume, tornadoes.Count > 0 && outside ? 0.35f : 0f, 1f - Mathf.Exp(-0.5f * dt));
            if (cam == null || look == null) return;
            foreach (var t in tornadoes)
            {
                t.gameObject.SetActive(outside);
                if (outside) t.Tick(cam.transform.position, 0.6f, dt);
            }
        }

        void ClearTornadoes()
        {
            foreach (var t in tornadoes) if (t != null) Destroy(t.gameObject);
            tornadoes.Clear();
            rainAmount = 0f;
        }
    }
}
