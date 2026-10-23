using Game.Audio;
using Game.Core;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.World
{
    /// <summary>
    /// A lamp post (section 6.4): click to crank its meter up; stop and it drains. Full, it lights for good: a
    /// warm safe light, and one more lamp counted (escalation listens for LampLit). While dark, a red marker
    /// on top blinks. A restart puts it out again.
    /// </summary>
    public sealed class Lamp : MonoBehaviour, IProgressInteractable
    {
        [Tooltip("This lamp's number (0-9).")]
        public int index;
        [Tooltip("The warm light that comes on when it's lit.")]
        public Light lampLight;
        [Tooltip("The red marker's renderer (blinks while dark).")]
        public Renderer marker;
        [Tooltip("Where the crank is: what the crosshair aims for.")]
        public Transform crank;
        [Tooltip("The glass that glows when lit.")]
        public Renderer glass;

        LampConfig config;
        SafeZone zone;
        float meter;
        float lastClick = -10f;
        float litLevel;
        Material markerMat, glassMat;
        Color glassGlow;
        System.IDisposable resetSub;

        public bool Lit { get; private set; }
        public float Progress => Lit ? 1f : meter;
        public string Prompt => "Crank";
        public bool CanInteract => !Lit;
        public Vector3 FocusPoint => crank != null ? crank.position : transform.position + Vector3.up * 1.1f;

        void Awake()
        {
            config = Services.TryGet<GameSetup>(out var setup) ? setup.Get<LampConfig>() : null;
            if (config == null) config = ScriptableObject.CreateInstance<LampConfig>();
            if (marker != null) markerMat = marker.material;
            if (glass != null)
            {
                glassMat = glass.material;
                glassGlow = glassMat.HasProperty("_EmissiveColor") ? glassMat.GetColor("_EmissiveColor") : Color.white;
            }
            zone = new SafeZone($"Lamp {index}", transform.position, config.settings.safeRadius) { Active = false };
        }

        void Start()
        {
            if (Services.TryGet<SafeZones>(out var zones)) zones.Add(zone);
            if (Services.TryGet<EventBus>(out var events)) resetSub = events.Subscribe<RunReset>(_ => PutOut());
            if (lampLight != null && lampLight.GetComponent<FogGlowLight>() == null) lampLight.gameObject.AddComponent<FogGlowLight>();
            Apply(0f);
        }

        void OnDestroy()
        {
            resetSub?.Dispose();
            if (Services.TryGet<SafeZones>(out var zones)) zones.Remove(zone);
        }

        public void Interact()
        {
            if (Lit) return;
            var s = config.settings;
            meter = Mathf.Min(1f, meter + s.perClick * DifficultyCrankSpeed());
            lastClick = Time.time;
            if (Services.TryGet<SoundPlayer>(out var sounds))
                sounds.Play(Sound.Click, FocusPoint, MixerNames.World, 0.6f, Mathf.Lerp(0.8f, 1.3f, meter), 12f);
            if (meter >= 1f) TurnOn();
        }

        // (Difficulty scales cranking speed; it arrives in milestone 4.)
        static float DifficultyCrankSpeed() => 1f;

        void TurnOn()
        {
            Lit = true;
            meter = 1f;
            zone.Center = transform.position;
            zone.Radius = config.settings.safeRadius;
            zone.Active = true;

            int timesLit = 1;
            if (Services.TryGet<GameRun>(out var run)) timesLit = run.RecordLampLit();
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(Sound.LampOn, transform.position + Vector3.up * 3f, MixerNames.World, 1f, 1f, 60f);
            if (Services.TryGet<EventBus>(out var events)) events.Publish(new LampLit(index, timesLit, transform.position));
        }

        void PutOut()
        {
            Lit = false;
            meter = 0f;
            litLevel = 0f;
            zone.Active = false;
        }

        void Update()
        {
            var s = config.settings;
            if (!Lit && meter > 0f && Time.time - lastClick > s.drainDelay)
                meter = Mathf.Max(0f, meter - s.drainRate * Time.deltaTime);
            litLevel = Mathf.MoveTowards(litLevel, Lit ? 1f : meter * 0.15f, Time.deltaTime * (Lit ? 0.8f : 2f));
            Apply(Time.time);
        }

        void Apply(float time)
        {
            var s = config.settings;
            if (lampLight != null)
            {
                lampLight.enabled = litLevel > 0.001f;
                lampLight.color = s.lightColor;
                lampLight.intensity = s.lightIntensity * litLevel;
                lampLight.range = s.lightRange;
            }
            if (glassMat != null) glassMat.SetColor("_EmissiveColor", glassGlow * litLevel);

            if (markerMat != null)
            {
                // A slow pulse, only while dark.
                float blink = Lit ? 0f : Mathf.Pow(0.5f + 0.5f * Mathf.Sin(time * s.markerBlinkRate * Mathf.PI * 2f), 3f);
                markerMat.SetColor("_EmissiveColor", s.markerColor * s.markerGlow * blink);
            }
        }
    }
}
