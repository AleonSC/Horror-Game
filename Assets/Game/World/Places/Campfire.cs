using Game.Audio;
using Game.Core;
using Game.Player;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.World
{
    /// <summary>
    /// A campsite's fire, laid ready but cold (section 6.6): click it with a lighter on you and it catches, burns
    /// for ten minutes (LampConfig > Campfires) and makes a safe circle of light round it, flames licking up
    /// through the logs, crackling, smoke rising off it. Over its last minute it dies down, the circle shrinking
    /// with it, to glowing embers and then nothing: the logs are burnt through, so it lights only once.
    /// </summary>
    public sealed class Campfire : MonoBehaviour, IInteractable
    {
        [Tooltip("The laid logs: they char as it burns.")]
        public Renderer[] logs = new Renderer[0];
        [Tooltip("What the logs look like burnt.")]
        public Material charred;

        LampSettings s;
        SafeZone zone;
        Light glow;
        HDAdditionalLightData glowData;
        MeshRenderer[] flames;
        Vector3[] flameSizes;
        Renderer embers;
        GameObject smoke;
        AudioSource crackle;
        float left, burnt;
        bool burning, spent;

        public bool Burning => burning;
        public string Prompt => "Light the fire";
        public bool CanInteract => !burning && !spent;
        public Vector3 FocusPoint => transform.position + Vector3.up * 0.3f;

        void Awake()
        {
            var config = Services.TryGet<GameSetup>(out var setup) ? setup.Get<LampConfig>() : null;
            s = config != null ? config.settings : new LampSettings();
        }

        public void Interact()
        {
            if (!CanInteract || !Services.TryGet<PlayerController>(out var player) || player.Hotbar == null) return;
            if (!player.Hotbar.StrikeLighter(s.campfireLighterFuel)) return;
            Light();
            player.Hotbar.Say("The fire catches.", 2f);
        }

        void Light()
        {
            burning = true;
            left = Mathf.Max(1f, s.campfireTime);
            burnt = 0f;

            // Flames: a few tongues of different heights through the logs, and one tall in the middle.
            var at = new[] { new Vector3(0f, 0.05f, 0f), new Vector3(0.12f, 0.03f, 0.05f), new Vector3(-0.1f, 0.03f, 0.08f), new Vector3(0.02f, 0.03f, -0.12f), new Vector3(-0.06f, 0.1f, -0.04f) };
            var size = new[] { new Vector3(0.45f, 1f, 0f), new Vector3(0.3f, 0.6f, 0f), new Vector3(0.3f, 0.55f, 0f), new Vector3(0.28f, 0.5f, 0f), new Vector3(0.22f, 0.75f, 0f) };
            flames = new MeshRenderer[at.Length];
            flameSizes = size;
            for (int i = 0; i < at.Length; i++)
                flames[i] = Effects.Flame(transform, at[i], size[i].x, size[i].y, Color.white * 2.2f, "Campfire flame");
            embers = Effects.Glow(transform, Vector3.up * 0.12f, 1.1f, s.campfireColor * 0.3f, 0.6f, 2.5f, "Campfire glow");

            var lightGo = new GameObject("Campfire light");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = Vector3.up * 0.55f;
            glowData = lightGo.AddHDLight(LightType.Point);
            glow = lightGo.GetComponent<Light>();
            glow.color = s.campfireColor;
            glow.range = s.campfireRange;
            glow.shadows = LightShadows.Soft;
            if (glowData != null) glowData.volumetricDimmer = s.campfireFogGlow;

            zone = new SafeZone("Campfire", transform.position, s.campfireSafeRadius);
            if (Services.TryGet<SafeZones>(out var zones)) zones.Add(zone);

            smoke = new GameObject("Smoke") { layer = Layers.Effects };
            smoke.transform.SetParent(transform, false);
            smoke.transform.localPosition = Vector3.up * 0.4f;
            smoke.AddComponent<CampSmoke>();

            if (Services.TryGet<SoundPlayer>(out var sounds))
            {
                sounds.Play(Sound.Ignite, transform.position + Vector3.up * 0.3f, MixerNames.World, 1f, 0.7f, 25f);
                crackle = gameObject.AddComponent<AudioSource>();
                crackle.clip = sounds.Clip(Sound.FireLoop);
                crackle.loop = true;
                crackle.spatialBlend = 1f;
                crackle.rolloffMode = AudioRolloffMode.Logarithmic;
                crackle.minDistance = 1.5f;
                crackle.maxDistance = 30f;
                crackle.dopplerLevel = 0f;
                crackle.outputAudioMixerGroup = sounds.Group(MixerNames.World);
                crackle.time = Random.Range(0f, 7f);
                crackle.Play();
            }
            if (Services.TryGet<EventBus>(out var events)) events.Publish(new NoiseEmitted(transform.position, 6f, "a campfire lit"));
        }

        void Update()
        {
            if (zone != null) zone.Center = transform.position; // (the world wraps: it moves)
            if (!burning) return;
            float dt = Time.deltaTime;
            left -= dt;
            burnt += dt;
            // Catching over the first few seconds; dying down over the last minute.
            float catching = Mathf.SmoothStep(0.15f, 1f, Mathf.Clamp01(burnt / 6f));
            float dying = s.campfireDieDown > 0f ? Mathf.Clamp01(left / s.campfireDieDown) : (left > 0f ? 1f : 0f);
            float level = catching * Mathf.SmoothStep(0f, 1f, dying);
            float flicker = 0.85f + 0.25f * Mathf.PerlinNoise(Time.time * 6f, 0.3f) - 0.12f * Mathf.PerlinNoise(Time.time * 17f, 2.7f);

            glow.intensity = s.campfireIntensity * level * flicker;
            glow.enabled = level > 0.001f;
            if (zone != null) zone.Radius = s.campfireSafeRadius * Mathf.Sqrt(level);
            for (int i = 0; i < flames.Length; i++)
            {
                if (flames[i] == null) continue;
                float lick = 0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 3f + i * 7.1f, i);
                float h = flameSizes[i].y * Mathf.Max(0.05f, level) * lick;
                flames[i].transform.localScale = new Vector3(flameSizes[i].x * Mathf.Lerp(0.5f, 1f, level), h, flameSizes[i].x);
                flames[i].enabled = level > 0.08f;
            }
            if (embers != null) Effects.SetGlow(embers, s.campfireColor * (0.12f + 0.25f * level) * flicker);
            if (crackle != null) crackle.volume = Mathf.Lerp(0.15f, 0.9f, level);
            // The logs blacken as they burn.
            if (charred != null && burnt > s.campfireTime * 0.25f)
                foreach (var log in logs)
                    if (log != null && log.sharedMaterial != charred) log.sharedMaterial = charred;

            if (left <= 0f) BurnOut();
        }

        // Out: the flames and light gone, the circle gone; the embers glow a while, then fade; the smoke thins away.
        void BurnOut()
        {
            burning = false;
            spent = true;
            if (Services.TryGet<SafeZones>(out var zones)) zones.Remove(zone);
            zone = null;
            foreach (var f in flames) if (f != null) Destroy(f.gameObject);
            if (glow != null) Destroy(glow.gameObject);
            if (crackle != null) Destroy(crackle);
            StartCoroutine(Cooling());
        }

        System.Collections.IEnumerator Cooling()
        {
            for (float t = 0f; t < 90f; t += Time.deltaTime)
            {
                if (embers != null) Effects.SetGlow(embers, s.campfireColor * 0.12f * (1f - t / 90f));
                yield return null;
            }
            if (embers != null) Destroy(embers.gameObject);
            if (smoke != null) Destroy(smoke);
        }

        void OnDestroy()
        {
            if (zone != null && Services.TryGet<SafeZones>(out var zones)) zones.Remove(zone);
        }
    }
}
