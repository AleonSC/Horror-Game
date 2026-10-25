using Game.Audio;
using Game.Core;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.World
{
    /// <summary>A little lantern (hanging in a tent, on a shack's table, from the cabin's beam): click it on or off. Warm and small, it lights the room round it.</summary>
    public sealed class Lantern : MonoBehaviour, IInteractable
    {
        public Renderer glass;
        [Tooltip("Where the flame is, from this object.")]
        public Vector3 flame = new Vector3(0f, -0.075f, 0f);
        public Color colour = new Color(1f, 0.72f, 0.42f);
        public float intensity = 1.6f, range = 3.2f;

        Light lamp;
        Renderer halo;
        bool on;

        public string Prompt => on ? "Turn off" : "Turn on";
        public bool CanInteract => true;
        public Vector3 FocusPoint => transform.TransformPoint(flame);

        void Start()
        {
            var go = new GameObject("Lantern light");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = flame;
            var data = go.AddHDLight(LightType.Point);
            if (data != null) data.volumetricDimmer = 0.2f;
            lamp = go.GetComponent<Light>();
            lamp.color = colour;
            lamp.range = range;
            lamp.intensity = intensity;
            lamp.shadows = LightShadows.Soft; // (it stays in the tent or the room, but for the doorway and windows)
            halo = Effects.Glow(transform, flame, 0.25f, Color.black, 0.5f, 3f, "Lantern glow");
            Set(false);
        }

        public void Interact()
        {
            if (lamp == null) return;
            Set(!on);
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(Sound.Click, transform.position, MixerNames.World, 0.5f, on ? 1.2f : 0.9f, 6f);
        }

        void Set(bool now)
        {
            on = now;
            lamp.enabled = on;
            if (halo != null) Effects.SetGlow(halo, on ? colour * 0.35f : Color.black);
            if (glass != null) RuntimeMaterials.SetGlow(glass.material, on ? colour * 2.5f : Color.black);
        }

        void Update()
        {
            if (!on || lamp == null) return;
            lamp.intensity = intensity * (0.95f + 0.05f * Mathf.PerlinNoise(Time.time * 8f, 1.3f));
        }
    }
}
