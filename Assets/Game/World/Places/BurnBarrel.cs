using Game.Audio;
using Game.Core;
using Game.Player;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.World
{
    /// <summary>
    /// An old oil drum made a burn barrel, half full of split wood and rubbish (its pivot: the middle of its foot).
    /// Click it with a lighter on you and it catches: flames lick up out of its open top, the holes punched round
    /// its foot glow orange, it throws a warm, shifting light and a small safe circle round it, crackles, and smoke
    /// rises off it. It burns down over a few minutes to embers, and once burnt out it's spent.
    /// </summary>
    public sealed class BurnBarrel : MonoBehaviour, IInteractable
    {
        [Tooltip("Seconds it burns.")]
        public float burnTime = 300f;
        [Tooltip("Seconds it takes over its end to die down to embers.")]
        public float dieDown = 45f;
        [Tooltip("Lighter fuel used to light it (a share of a full lighter).")]
        public float lighterFuel = 0.05f;
        [Tooltip("Radius of the safe circle round it while it burns (metres).")]
        public float safeRadius = 4f;
        public Color fireColor = new Color(1f, 0.48f, 0.18f);
        [Range(0f, 100f)] public float intensity = 0.8f;
        [Tooltip("How bright the flames themselves are (keep it low: they bloom).")]
        public float flameBrightness = 0.45f;
        [Tooltip("How bright the air holes and the embers glow.")]
        public float emberGlow = 0.25f;
        [Range(1f, 30f)] public float range = 6f;
        [Tooltip("How tall the top of the barrel is (metres): the flames come up out of it.")]
        public float top = 0.88f;
        [Tooltip("The holes round its foot (they glow while it burns), and what's inside (it chars).")]
        public Renderer[] vents = new Renderer[0];
        public Renderer[] fuel = new Renderer[0];
        public Material charred;

        Light glow;
        MeshRenderer[] flames;
        Vector3[] flameSizes;
        Renderer embers;
        Material ventGlow;
        AudioSource crackle;
        SafeZone zone;
        float left, burnt;
        bool burning, spent;

        public string Prompt => "Light the barrel";
        public bool CanInteract => !burning && !spent;
        public Vector3 FocusPoint => transform.position + Vector3.up * top;

        public void Interact()
        {
            if (!CanInteract || !Services.TryGet<PlayerController>(out var player) || player.Hotbar == null) return;
            if (!player.Hotbar.StrikeLighter(lighterFuel)) return;
            Light();
            player.Hotbar.Say("The barrel catches.", 2f);
        }

        void Light()
        {
            burning = true;
            left = Mathf.Max(5f, burnTime);
            burnt = 0f;
            float y = top - 0.05f;
            var at = new[] { new Vector3(0f, y, 0f), new Vector3(0.1f, y, 0.06f), new Vector3(-0.09f, y, 0.07f), new Vector3(0.03f, y, -0.11f) };
            flameSizes = new[] { new Vector3(0.4f, 0.75f, 0f), new Vector3(0.26f, 0.45f, 0f), new Vector3(0.24f, 0.5f, 0f), new Vector3(0.22f, 0.4f, 0f) };
            flames = new MeshRenderer[at.Length];
            for (int i = 0; i < at.Length; i++) flames[i] = Effects.Flame(transform, at[i], flameSizes[i].x, flameSizes[i].y, Color.white * flameBrightness, "Barrel flame");
            embers = Effects.Glow(transform, Vector3.up * (top + 0.05f), 0.6f, fireColor * 0.05f, 0.3f, 2.5f, "Barrel glow");

            var lightGo = new GameObject("Barrel light");
            lightGo.transform.SetParent(transform, false);
            lightGo.transform.localPosition = Vector3.up * (top + 0.35f);
            lightGo.AddHDLight(LightType.Point);
            glow = lightGo.GetComponent<Light>();
            glow.color = fireColor;
            glow.range = range;
            glow.shadows = LightShadows.Soft;

            // The holes round its foot: their dark insides glow.
            ventGlow = RuntimeMaterials.Glow("Barrel vents", Color.black);
            foreach (var v in vents) if (v != null) v.sharedMaterial = ventGlow;

            zone = new SafeZone("Burn barrel", transform.position, safeRadius);
            if (Services.TryGet<SafeZones>(out var zones)) zones.Add(zone);

            var smoke = new GameObject("Smoke") { layer = Layers.Effects };
            smoke.transform.SetParent(transform, false);
            smoke.transform.localPosition = Vector3.up * (top + 0.3f);
            smoke.AddComponent<CampSmoke>();

            if (Services.TryGet<SoundPlayer>(out var sounds))
            {
                sounds.Play(Sound.Ignite, transform.position + Vector3.up * top, MixerNames.World, 1f, 0.75f, 20f);
                crackle = gameObject.AddComponent<AudioSource>();
                crackle.clip = sounds.Clip(Sound.FireLoop);
                crackle.loop = true;
                crackle.spatialBlend = 1f;
                crackle.rolloffMode = AudioRolloffMode.Logarithmic;
                crackle.minDistance = 1.2f;
                crackle.maxDistance = 25f;
                crackle.dopplerLevel = 0f;
                crackle.outputAudioMixerGroup = sounds.Group(MixerNames.World);
                crackle.time = Random.Range(0f, 7f);
                crackle.Play();
            }
            if (Services.TryGet<EventBus>(out var events)) events.Publish(new NoiseEmitted(transform.position, 5f, "a fire lit"));
        }

        void Update()
        {
            if (zone != null) zone.Center = transform.position; // (the world wraps: it moves)
            if (!burning) return;
            float dt = Time.deltaTime;
            left -= dt;
            burnt += dt;
            float catching = Mathf.SmoothStep(0.15f, 1f, Mathf.Clamp01(burnt / 5f));
            float dying = dieDown > 0f ? Mathf.Clamp01(left / dieDown) : (left > 0f ? 1f : 0f);
            float level = catching * Mathf.SmoothStep(0f, 1f, dying);
            float flicker = 0.85f + 0.25f * Mathf.PerlinNoise(Time.time * 6f, 0.3f) - 0.12f * Mathf.PerlinNoise(Time.time * 17f, 2.7f);
            glow.intensity = intensity * level * flicker;
            glow.enabled = level > 0.001f;
            // (The light dances, as off flames.)
            glow.transform.localPosition = new Vector3((Mathf.PerlinNoise(Time.time * 2f, 5f) - 0.5f) * 0.12f, top + 0.35f, (Mathf.PerlinNoise(Time.time * 2f, 9f) - 0.5f) * 0.12f);
            if (zone != null) zone.Radius = safeRadius * Mathf.Sqrt(level);
            for (int i = 0; i < flames.Length; i++)
            {
                if (flames[i] == null) continue;
                float lick = 0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 3f + i * 7.1f, i);
                flames[i].transform.localScale = new Vector3(flameSizes[i].x * Mathf.Lerp(0.5f, 1f, level), flameSizes[i].y * Mathf.Max(0.05f, level) * lick, flameSizes[i].x);
                flames[i].enabled = level > 0.08f;
            }
            if (embers != null) Effects.SetGlow(embers, fireColor * (0.02f + 0.06f * level) * flicker);
            RuntimeMaterials.SetGlow(ventGlow, fireColor * emberGlow * (0.15f + 0.85f * level) * flicker);
            if (crackle != null) crackle.volume = Mathf.Lerp(0.12f, 0.8f, level);
            if (charred != null && burnt > burnTime * 0.3f)
                foreach (var f in fuel) if (f != null && f.sharedMaterial != charred) f.sharedMaterial = charred;
            if (left <= 0f) BurnOut();
        }

        void BurnOut()
        {
            burning = false;
            spent = true;
            if (glow != null) glow.enabled = false;
            foreach (var f in flames) if (f != null) f.enabled = false;
            if (embers != null) Effects.SetGlow(embers, Color.black);
            if (ventGlow != null) RuntimeMaterials.SetGlow(ventGlow, Color.black);
            if (crackle != null) crackle.Stop();
            if (zone != null && Services.TryGet<SafeZones>(out var zones)) zones.Remove(zone);
            zone = null;
        }

        void OnDestroy()
        {
            if (zone != null && Services.TryGet<SafeZones>(out var zones)) zones.Remove(zone);
            if (ventGlow != null) Destroy(ventGlow);
        }
    }
}
