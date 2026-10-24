using Game.Audio;
using Game.Core;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.World
{
    /// <summary>
    /// The watchtower's button (section 6.5): it lights the floodlights for 60 s, then needs 120 s to recharge.
    /// While they're on: every hunt resets, the area round the tower (45 m) is safe, the giant is driven back to
    /// the horizon and the flier away.
    /// </summary>
    public sealed class Watchtower : MonoBehaviour, IInteractable
    {
        public float onTime = 60f;
        public float rechargeTime = 120f;
        public float safeRadius = 45f;
        [Tooltip("Where the floodlights hang (this object's space); they shine out and down.")]
        public Vector3[] lightSpots = { new Vector3(-2f, 13.8f, -2f), new Vector3(2f, 13.8f, -2f), new Vector3(-2f, 13.8f, 2f), new Vector3(2f, 13.8f, 2f) };
        [Tooltip("The button, in this object's space.")]
        public Vector3 buttonSpot = new Vector3(1.5f, 13f, 1.5f);

        Light[] lights;
        SafeZone zone;
        float onLeft, rechargeLeft;
        Collider button;

        public string Prompt => onLeft > 0f ? "Lights on" : rechargeLeft > 0f ? "Recharging" : "Floodlights";
        public bool CanInteract => onLeft <= 0f && rechargeLeft <= 0f;
        public Vector3 FocusPoint => transform.TransformPoint(buttonSpot);
        public bool On => onLeft > 0f;

        void Start()
        {
            // A box to aim at, for the button.
            var b = new GameObject("Floodlight button") { layer = Layers.Props };
            b.transform.SetParent(transform, false);
            b.transform.localPosition = buttonSpot;
            var box = b.AddComponent<BoxCollider>();
            box.size = new Vector3(0.25f, 0.25f, 0.25f);
            box.isTrigger = true;
            button = box;
            var look = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(look.GetComponent<Collider>());
            look.transform.SetParent(b.transform, false);
            look.transform.localScale = Vector3.one * 0.18f;
            look.GetComponent<Renderer>().sharedMaterial = Game.Rendering.RuntimeMaterials.Lit("Button", new Color(0.4f, 0.05f, 0.03f), 0.5f);

            lights = new Light[lightSpots.Length];
            for (int i = 0; i < lightSpots.Length; i++)
            {
                var go = new GameObject("Floodlight");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = lightSpots[i];
                Vector3 outward = new Vector3(lightSpots[i].x, 0f, lightSpots[i].z).normalized;
                go.transform.localRotation = Quaternion.LookRotation(outward * 1f + Vector3.down * 0.55f);
                go.AddHDLight(LightType.Spot);
                var l = go.GetComponent<Light>();
                l.color = new Color(0.9f, 0.95f, 1f);
                l.spotAngle = 95f;
                l.innerSpotAngle = 60f;
                l.range = 70f;
                l.intensity = 9000f;
                l.shadows = LightShadows.None;
                l.enabled = false;
                lights[i] = l;
            }
            zone = new SafeZone("Watchtower floodlights", transform.position, safeRadius) { Active = false };
            if (Services.TryGet<SafeZones>(out var zones)) zones.Add(zone);
        }

        void OnDestroy()
        {
            if (Services.TryGet<SafeZones>(out var zones)) zones.Remove(zone);
        }

        public void Interact()
        {
            if (!CanInteract) return;
            onLeft = onTime;
            if (Services.TryGet<SoundPlayer>(out var sounds))
            {
                sounds.Play(Sound.Clunk, FocusPoint, MixerNames.World, 0.8f, 1.4f, 60f);
                sounds.Play(Sound.LampOn, FocusPoint, MixerNames.World, 1f, 0.8f, 120f);
            }
            if (Services.TryGet<EventBus>(out var events))
            {
                events.Publish(new FloodlightsLit(transform.position, onTime));
                events.Publish(new HuntsReset("the watchtower's floodlights"));
            }
        }

        void Update()
        {
            zone.Center = transform.position; // (the world wraps)
            if (onLeft > 0f)
            {
                onLeft -= Time.deltaTime;
                if (onLeft <= 0f)
                {
                    rechargeLeft = rechargeTime;
                    if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(Sound.Click, FocusPoint, MixerNames.World, 0.8f, 0.5f, 40f);
                }
            }
            else if (rechargeLeft > 0f) rechargeLeft -= Time.deltaTime;

            // The last few seconds, they flicker as they die.
            float level = onLeft <= 0f ? 0f : onLeft < 4f ? (Random.value < 0.25f ? 0.2f : 1f) : 1f;
            zone.Active = onLeft > 0f;
            foreach (var l in lights)
            {
                l.enabled = level > 0f;
                l.intensity = 9000f * level;
            }
        }
    }
}
