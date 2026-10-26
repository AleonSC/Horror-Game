using Game.Audio;
using Game.Core;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.World
{
    /// <summary>
    /// The watchtower's floodlights (section 6.5), worked by a lever on the switch box inside by the door: throw it
    /// down and they come on, up and they go off. They run on a charge (60 s of light) that drains while they're on
    /// and slowly fills again while they're off (120 s from empty); they need a quarter of it to come on, and when
    /// it runs out they flicker and die and the lever kicks back up. While they're on: the area round the tower
    /// (45 m) is safe, the giant is driven back to the horizon and the flier away, and (no more than every 20 s)
    /// every hunt resets.
    /// </summary>
    public sealed class Watchtower : MonoBehaviour, IInteractable
    {
        [Tooltip("Seconds of light in a full charge.")]
        public float onTime = 60f;
        [Tooltip("Seconds for the charge to fill again from empty, while they're off.")]
        public float rechargeTime = 120f;
        [Range(0f, 1f), Tooltip("They need this much of the charge to come on.")]
        public float minCharge = 0.25f;
        public float safeRadius = 45f;
        [Tooltip("How bright each floodlight is (candela).")]
        public float brightness = 700f;
        [Tooltip("How far they reach (metres).")]
        public float reach = 80f;
        [Tooltip("How much they light the fog (0-1): high and the whole area turns to white haze you can't see into.")]
        public float fogGlow = 0.035f;
        [Tooltip("The soft shafts of light under them (0: none). Low, so you can see into the light from outside it.")]
        public float beamGlow = 0.06f;
        [Tooltip("The glare of each lamp's face, seen from out in the forest.")]
        public float lensGlow = 6f;
        [Tooltip("Where the floodlights hang (this object's space); they shine out and down.")]
        public Vector3[] lightSpots = { new Vector3(-2f, 13.8f, -2f), new Vector3(2f, 13.8f, -2f), new Vector3(-2f, 13.8f, 2f), new Vector3(2f, 13.8f, 2f) };
        [Tooltip("The lever's pivot, in this object's space (it sticks out along +X, the wall behind it).")]
        public Vector3 buttonSpot = new Vector3(1.5f, 13f, 1.5f);

        static readonly Color LightColor = new Color(0.9f, 0.95f, 1f);
        const float ResetEvery = 20f;

        Light[] lights;
        Renderer[] shafts, lenses;
        HDAdditionalLightData[] lightData;
        SafeZone zone;
        Transform lever;
        Renderer indicator;
        Material indicatorMat;
        bool on;
        float charge = 1f, throwAngle = Off, lastReset = -999f;

        const float Off = -30f, OnAngle = -150f; // (the lever's angle about the wall: up and out, down and out)

        public string Prompt => on ? $"Switch the floodlights off ({Mathf.CeilToInt(charge * onTime)}s left)"
            : charge >= minCharge ? "Switch the floodlights on"
            : $"Floodlights recharging ({Mathf.FloorToInt(charge * 100f)}%)";
        public bool CanInteract => on || charge >= minCharge;
        public Vector3 FocusPoint => lever != null ? lever.TransformPoint(new Vector3(0f, 0.2f, 0f)) : transform.TransformPoint(buttonSpot);
        public bool On => on;

        void Start()
        {
            BuildLever();

            lights = new Light[lightSpots.Length];
            shafts = new Renderer[lightSpots.Length];
            lenses = new Renderer[lightSpots.Length];
            lightData = new HDAdditionalLightData[lightSpots.Length];
            for (int i = 0; i < lightSpots.Length; i++)
            {
                var go = new GameObject("Floodlight");
                go.transform.SetParent(transform, false);
                go.transform.localPosition = lightSpots[i];
                Vector3 outward = new Vector3(lightSpots[i].x, 0f, lightSpots[i].z).normalized;
                go.transform.localRotation = Quaternion.LookRotation(outward * 1f + Vector3.down * 0.55f);
                var data = go.AddHDLight(LightType.Spot);
                data.volumetricDimmer = fogGlow;
                lightData[i] = data;
                var l = go.GetComponent<Light>();
                l.color = LightColor;
                l.spotAngle = 80f;
                l.innerSpotAngle = 45f;
                l.range = reach;
                l.intensity = brightness;
                l.shadows = LightShadows.None;
                l.enabled = false;
                lights[i] = l;
                shafts[i] = Effects.Beam(go.transform, 34f, 22f, Color.black, 0.6f, 0.4f, "Floodlight shaft");
                if (shafts[i] != null) shafts[i].enabled = false;
                // The lamp's face, blazing (seen from anywhere it faces, through the fog a little).
                lenses[i] = Effects.Glow(go.transform, new Vector3(0f, 0f, 0.03f), 1.1f, Color.black, 0.35f, 3f, "Floodlight glare");
                if (lenses[i] != null) lenses[i].enabled = false;
            }
            zone = new SafeZone("Watchtower floodlights", transform.position, safeRadius) { Active = false };
            if (Services.TryGet<SafeZones>(out var zones)) zones.Add(zone);
        }

        // A knife-switch lever on the switch box: a hub on the wall, a steel arm, a black grip; and a small lamp
        // over it, green when there's charge enough, amber while it fills.
        void BuildLever()
        {
            var root = new GameObject("Floodlight lever") { layer = Layers.Props }.transform;
            root.SetParent(transform, false);
            root.localPosition = buttonSpot;
            var box = root.gameObject.AddComponent<BoxCollider>(); // (to aim at)
            box.center = new Vector3(0.08f, 0f, 0f);
            box.size = new Vector3(0.22f, 0.5f, 0.22f);
            box.isTrigger = true;

            var steel = RuntimeMaterials.Lit("Lever steel", new Color(0.32f, 0.32f, 0.33f), 0.55f);
            var grip = RuntimeMaterials.Lit("Lever grip", new Color(0.03f, 0.03f, 0.03f), 0.3f);
            var hub = Part(PrimitiveType.Cylinder, root, new Vector3(0.02f, 0f, 0f), new Vector3(0.06f, 0.02f, 0.06f), steel);
            hub.localRotation = Quaternion.Euler(0f, 0f, 90f);

            lever = new GameObject("Lever").transform;
            lever.SetParent(root, false);
            lever.localPosition = new Vector3(0.04f, 0f, 0f);
            Part(PrimitiveType.Cube, lever, new Vector3(0f, 0.11f, 0f), new Vector3(0.022f, 0.22f, 0.022f), steel);
            Part(PrimitiveType.Cylinder, lever, new Vector3(0f, 0.25f, 0f), new Vector3(0.04f, 0.05f, 0.04f), grip);
            Part(PrimitiveType.Sphere, lever, new Vector3(0f, 0.3f, 0f), Vector3.one * 0.045f, grip);

            indicatorMat = RuntimeMaterials.LitGlow("Floodlight charge lamp", new Color(0.05f, 0.05f, 0.05f), Color.black);
            var lamp = Part(PrimitiveType.Sphere, root, new Vector3(0.01f, 0.26f, 0.09f), Vector3.one * 0.025f, indicatorMat);
            indicator = lamp.GetComponent<Renderer>();
            SetLever(true);
        }

        static Transform Part(PrimitiveType type, Transform parent, Vector3 at, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.layer = Layers.Props;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        void OnDestroy()
        {
            if (Services.TryGet<SafeZones>(out var zones)) zones.Remove(zone);
            if (indicatorMat != null) Destroy(indicatorMat);
        }

        public void Interact()
        {
            if (!CanInteract) return;
            if (on) SwitchOff(false);
            else SwitchOn();
        }

        void SwitchOn()
        {
            on = true;
            if (Services.TryGet<SoundPlayer>(out var sounds))
            {
                sounds.Play(Sound.Clunk, FocusPoint, MixerNames.World, 0.9f, 0.9f, 60f);
                sounds.Play(Sound.LampOn, FocusPoint, MixerNames.World, 1f, 0.8f, 120f);
            }
            if (Services.TryGet<EventBus>(out var events))
            {
                events.Publish(new FloodlightsLit(transform.position, charge * onTime));
                if (Time.time - lastReset >= ResetEvery)
                {
                    lastReset = Time.time;
                    events.Publish(new HuntsReset("the watchtower's floodlights"));
                }
            }
        }

        void SwitchOff(bool ranOut)
        {
            on = false;
            if (Services.TryGet<SoundPlayer>(out var sounds))
            {
                sounds.Play(Sound.Clunk, FocusPoint, MixerNames.World, ranOut ? 0.6f : 0.9f, 1.15f, 40f);
                if (ranOut) sounds.Play(Sound.Click, FocusPoint, MixerNames.World, 0.8f, 0.5f, 40f);
            }
        }

        void SetLever(bool snap)
        {
            float want = on ? OnAngle : Off;
            throwAngle = snap ? want : Mathf.MoveTowards(throwAngle, want, Time.deltaTime * 900f);
            if (lever != null) lever.localRotation = Quaternion.Euler(0f, 0f, throwAngle);
        }

        void Update()
        {
            zone.Center = transform.position; // (the world wraps)
            float dt = Time.deltaTime;
            if (on)
            {
                charge = Mathf.Max(0f, charge - dt / Mathf.Max(1f, onTime));
                if (charge <= 0f) SwitchOff(true); // (flat: they die, and the lever kicks back up)
            }
            else charge = Mathf.Min(1f, charge + dt / Mathf.Max(1f, rechargeTime));
            SetLever(false);

            if (indicatorMat != null)
                RuntimeMaterials.SetGlow(indicatorMat, charge >= minCharge ? new Color(0.2f, 1f, 0.3f) * 2f : new Color(1f, 0.55f, 0.1f) * (1.2f + Mathf.Sin(Time.time * 4f)));

            // The last few seconds of the charge, they flicker as they die.
            float left = charge * onTime;
            float level = !on ? 0f : left < 4f ? (Random.value < 0.25f ? 0.2f : 1f) : 1f;
            zone.Active = on;
            for (int i = 0; i < lights.Length; i++)
            {
                lights[i].enabled = level > 0f;
                lights[i].intensity = brightness * level;
                lights[i].range = reach;
                lightData[i].volumetricDimmer = fogGlow;
                if (shafts[i] != null)
                {
                    shafts[i].enabled = level > 0f && beamGlow > 0f;
                    Effects.SetGlow(shafts[i], LightColor * (beamGlow * level));
                }
                if (lenses[i] != null)
                {
                    lenses[i].enabled = level > 0f;
                    Effects.SetGlow(lenses[i], LightColor * (lensGlow * level));
                }
            }
        }
    }
}
