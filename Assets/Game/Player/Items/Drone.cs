using System.Collections.Generic;
using Game.Audio;
using Game.Core;
using Game.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace Game.Player
{
    /// <summary>
    /// The drone (section 6.3). Set down with F, it lifts off and you fly it from the remote: WASD, the mouse,
    /// Space and Ctrl to climb and sink, Shift for speed; click for its lamp, T for thermal. You see and hear only
    /// through it, standing where you are, blind. Its signal only reaches so far; its motors carry (the blind one
    /// hears them); its battery runs down and it drops when it's flat. F brings your eyes back and it hovers where
    /// you left it, motors running and battery slowly draining, until you fly it again (land it first to pick it up).
    /// </summary>
    public sealed class Drone : MonoBehaviour, IInteractable
    {
        public static Drone Current { get; private set; }
        /// <summary>Is someone flying it right now?</summary>
        public static bool Piloting => Current != null && Current.Flying;

        ItemSettings s;
        PlayerController player;
        Camera cam;
        AudioListener ears;
        HDAdditionalCameraData camData;
        Light lamp, beaconLight;
        MeshRenderer beacon;
        Transform model;
        readonly List<Transform> rotors = new List<Transform>();
        AudioSource motors;
        Vector3 velocity;
        float yaw, pitch, spin, noiseIn, settle;
        bool airborne;
        Mesh heatMesh;
        Material heatMat;
        System.IDisposable resetSub;

        public bool Flying { get; private set; }
        /// <summary>The frame it last started or stopped (F does both: one press mustn't do both).</summary>
        public static int SwitchedFrame { get; private set; } = -1;
        public bool Dead => Battery <= 0f;
        public float Battery { get; private set; }
        public bool LampOn { get; private set; }
        public bool Thermal { get; private set; }
        /// <summary>1 full signal, 0 lost (at the edge of its range).</summary>
        public float Signal { get; private set; } = 1f;
        public float Height { get; private set; }
        public float FromYou { get; private set; }
        /// <summary>Static over its picture at full signal.</summary>
        public float BaseStatic => s != null ? s.droneStatic : 0f;

        public string Prompt => "Pick up drone";
        public bool CanInteract => !Flying;
        public Vector3 FocusPoint => transform.position + Vector3.up * 0.05f;

        /// <summary>
        /// Where it would be set down: where you're looking, on the ground or something flat, within 4 m (or just
        /// in front of you). False if there's no room there (a trunk, a wall, you).
        /// </summary>
        public static bool PlacementSpot(PlayerController player, out Vector3 at)
        {
            var eye = player.Head;
            int solid = Layers.Mask(Layers.Ground) | Layers.Mask(Layers.Props) | Layers.Mask(Layers.Default);
            if (Physics.Raycast(eye.position, eye.forward, out var look, 4f, solid | Layers.Mask(Layers.Trees), QueryTriggerInteraction.Ignore) && look.normal.y > 0.6f)
                at = look.point;
            else
            {
                at = player.transform.position + Vector3.ProjectOnPlane(eye.forward, Vector3.up).normalized * 1.2f;
                if (Physics.Raycast(at + Vector3.up * 2f, Vector3.down, out var hit, 10f, solid, QueryTriggerInteraction.Ignore)) at = hit.point;
            }
            int blockers = Layers.Mask(Layers.Trees) | Layers.Mask(Layers.Props) | Layers.Mask(Layers.Default) | Layers.Mask(Layers.Entities);
            bool room = !Physics.CheckSphere(at + Vector3.up * 0.3f, 0.22f, blockers, QueryTriggerInteraction.Ignore);
            Vector3 fromYou = at - player.transform.position;
            fromYou.y = 0f;
            return room && fromYou.magnitude > 0.5f;
        }

        /// <summary>Set it down (here, or in front of the player) and start flying it.</summary>
        public static Drone Deploy(PlayerController player, ItemSettings s, float battery, GameObject art, Vector3? spot = null)
        {
            if (Current != null) Destroy(Current.gameObject);
            Vector3 at = spot ?? player.transform.position + player.transform.forward * 1.2f;
            if (spot == null && Physics.Raycast(at + Vector3.up * 2f, Vector3.down, out var hit, 10f, Layers.Mask(Layers.Ground), QueryTriggerInteraction.Ignore)) at = hit.point;
            var go = new GameObject("Drone") { layer = Layers.Props };
            go.transform.SetPositionAndRotation(at + Vector3.up * 0.02f, Quaternion.Euler(0f, player.transform.eulerAngles.y, 0f));
            var d = go.AddComponent<Drone>();
            d.Build(player, s, battery, art);
            Current = d;
            d.StartFlying();
            return d;
        }

        void Build(PlayerController p, ItemSettings settings, float battery, GameObject art)
        {
            player = p;
            s = settings;
            Battery = Mathf.Clamp01(battery);
            yaw = transform.eulerAngles.y;
            model = ItemModels.Make(ItemKind.Drone, art, transform, false).transform;
            model.localScale = Vector3.one * 1.6f;
            foreach (var t in model.GetComponentsInChildren<Transform>()) if (t.name == "Rotor") rotors.Add(t);
            var col = gameObject.AddComponent<SphereCollider>();
            col.radius = 0.25f;
            col.center = Vector3.up * 0.1f;
            col.isTrigger = true; // (to aim at and pick up)

            var camGo = new GameObject("Drone camera");
            camGo.transform.SetParent(transform, false);
            camGo.transform.localPosition = new Vector3(0f, 0.02f, 0.14f);
            cam = camGo.AddComponent<Camera>();
            var from = player.View;
            cam.fieldOfView = 78f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = from != null ? from.farClipPlane : 600f;
            cam.cullingMask = ~0; // (it sees everything, you included)
            cam.enabled = false;
            camData = camGo.AddComponent<HDAdditionalCameraData>();
            ears = camGo.AddComponent<AudioListener>();
            ears.enabled = false;

            // Its beacon: a red light on top, blinking while it's up, to be seen in the sky.
            beacon = Effects.Glow(model, new Vector3(0f, 0.085f, 0f), 0.35f, Color.black, 0.15f, 3f, "Drone beacon");
            var beaconGo = new GameObject("Drone beacon light");
            beaconGo.transform.SetParent(model, false);
            beaconGo.transform.localPosition = new Vector3(0f, 0.1f, 0f);
            beaconGo.AddHDLight(LightType.Point);
            beaconLight = beaconGo.GetComponent<Light>();
            beaconLight.color = new Color(1f, 0.08f, 0.04f);
            beaconLight.range = 3f;
            beaconLight.shadows = LightShadows.None;
            beaconLight.enabled = false;

            var lampGo = new GameObject("Drone lamp");
            lampGo.transform.SetParent(camGo.transform, false);
            var lampData = lampGo.AddHDLight(LightType.Spot);
            lamp = lampGo.GetComponent<Light>();
            lamp.spotAngle = 60f;
            lamp.enabled = false;
            lampData.volumetricDimmer = 0.15f;

            if (Services.TryGet<SoundPlayer>(out var sounds))
            {
                motors = gameObject.AddComponent<AudioSource>();
                motors.clip = sounds.Clip(Sound.Rotors);
                motors.loop = true;
                motors.spatialBlend = 1f;
                motors.maxDistance = 60f;
                motors.rolloffMode = AudioRolloffMode.Linear;
                motors.volume = 0f;
                motors.outputAudioMixerGroup = sounds.Group(MixerNames.World);
            }

            heatMesh = PrimitiveMesh(PrimitiveType.Capsule);
            float mul = Services.TryGet<LookController>(out var look) ? LookController.ExposureMultiplier(look.Config.settings.exposure) : 1f;
            heatMat = RuntimeMaterials.Haze("Heat", Color.white);
            RuntimeMaterials.SetHazeColor(heatMat, new Color(1f, 0.72f, 0.4f) * 1.4f / mul);
            heatMat.enableInstancing = true;
            if (Services.TryGet<EventBus>(out var events)) resetSub = events.Subscribe<RunStarted>(_ => Destroy(gameObject));
        }

        static Mesh PrimitiveMesh(PrimitiveType type)
        {
            var go = GameObject.CreatePrimitive(type);
            var mesh = go.GetComponent<MeshFilter>().sharedMesh;
            Destroy(go);
            return mesh;
        }

        void OnDestroy()
        {
            if (Flying) StopFlying();
            resetSub?.Dispose();
            if (heatMat != null) Destroy(heatMat);
            if (Current == this) Current = null;
        }

        // ------------------------------------------------------------------ flying

        public void StartFlying()
        {
            if (Flying || player == null) return;
            if (Dead)
            {
                player.Hotbar?.Say("Its battery is flat.");
                return;
            }
            if (Signal <= 0.02f)
            {
                player.Hotbar?.Say("No signal. Get closer to it.");
                return;
            }
            Flying = true;
            SwitchedFrame = Time.frameCount;
            airborne = true;
            player.Piloting = true;
            if (player.View != null)
            {
                player.View.enabled = false;
                var yours = player.View.GetComponent<AudioListener>();
                if (yours != null) yours.enabled = false;
            }
            cam.enabled = true;
            cam.tag = "MainCamera";
            ears.enabled = true;
            pitch = 10f;
            if (motors != null && !motors.isPlaying) motors.Play();
            velocity = Vector3.up * 1.5f; // (it lifts off)
        }

        /// <summary>Your eyes back; it hovers where it is (or stays put, if it's on the ground).</summary>
        public void StopFlying()
        {
            if (!Flying) return;
            Flying = false;
            SwitchedFrame = Time.frameCount;
            if (player != null)
            {
                player.Piloting = false;
                if (player.View != null)
                {
                    player.View.enabled = true;
                    var yours = player.View.GetComponent<AudioListener>();
                    if (yours != null) yours.enabled = true;
                }
            }
            cam.enabled = false;
            cam.tag = "Untagged";
            ears.enabled = false;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return; // (paused)
            if (Flying && (player == null || player.Held || !GameFlow.IsPlaying)) StopFlying(); // (something has you)

            Height = Physics.Raycast(transform.position + Vector3.up * 0.2f, Vector3.down, out var down, 500f, Layers.Mask(Layers.Ground), QueryTriggerInteraction.Ignore) ? down.distance - 0.2f : 0f;
            Vector3 toYou = player != null ? player.transform.position - transform.position : Vector3.zero;
            toYou.y = 0f;
            FromYou = toYou.magnitude;
            Signal = 1f - Mathf.Clamp01((FromYou - s.droneRange * 0.6f) / (s.droneRange * 0.4f));

            Vector3 wish = Vector3.zero;
            bool hovering = !Flying && airborne && !Dead;
            if (Flying || hovering)
            {
                Battery = Mathf.Max(0f, Battery - dt * (Flying ? 1f : s.droneHoverDrain) / Mathf.Max(1f, s.droneFlightTime));
                if (Dead)
                {
                    if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(Sound.Clunk, transform.position, MixerNames.World, 0.6f, 1.6f, 20f);
                    player.Hotbar?.Say("The drone's battery died.");
                    StopFlying();
                }
                else if (Flying) wish = Controls();

                noiseIn -= dt;
                if (noiseIn <= 0f && Services.TryGet<EventBus>(out var events))
                {
                    noiseIn = 1f;
                    events.Publish(new NoiseEmitted(transform.position, s.droneNoise, "drone"));
                }
            }

            if (Flying)
            {
                // Its signal stops at the edge of its range: it won't go further.
                if (Signal <= 0.02f)
                {
                    Vector3 back = toYou.normalized;
                    float outward = Vector3.Dot(wish, -back);
                    if (outward > 0f) wish += back * outward;
                    wish += back * 1.5f;
                }
                velocity = Vector3.Lerp(velocity, wish, 1f - Mathf.Exp(-3.5f * dt));
            }
            else if (airborne)
            {
                // Not flown: it holds where it is, bobbing a little (or drops, flat).
                velocity = Dead ? velocity + Physics.gravity * dt
                    : Vector3.Lerp(velocity, Vector3.up * (Mathf.Sin(Time.time * 1.3f) * 0.08f), 1f - Mathf.Exp(-2.5f * dt));
            }
            Move(dt);

            // Rotors, sound, tilt.
            float power = Flying || airborne && !Dead ? 1f : 0f;
            spin += dt * 2200f * power;
            foreach (var r in rotors) r.localRotation = Quaternion.Euler(0f, spin + r.GetSiblingIndex() * 40f, 0f);
            if (motors != null)
            {
                motors.volume = Mathf.MoveTowards(motors.volume, 0.7f * power, dt * 2f);
                motors.pitch = 1f + velocity.magnitude * 0.03f;
                if (motors.volume <= 0f && motors.isPlaying) motors.Stop();
            }
            Vector3 local = Quaternion.Euler(0f, -yaw, 0f) * velocity;
            model.localRotation = Quaternion.Slerp(model.localRotation, Quaternion.Euler(local.z * 3f, 0f, -local.x * 3f), 1f - Mathf.Exp(-6f * dt));
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            cam.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

            // The beacon: a short red blink every 1.2 s while it's up (powered and off the ground).
            bool up = (Flying || airborne) && !Dead;
            bool blink = up && Time.time % 1.2f < 0.12f;
            if (beacon != null) Effects.SetGlow(beacon, new Color(1f, 0.06f, 0.03f) * (blink ? 7f : 0f));
            if (beaconLight != null)
            {
                beaconLight.enabled = blink;
                beaconLight.intensity = 3f;
            }

            lamp.enabled = LampOn && !Dead;
            lamp.color = s.droneLampColor;
            lamp.intensity = s.droneLampIntensity;
            lamp.range = s.droneLampRange;
            if (Flying && Thermal) DrawHeat();
        }

        Vector3 Controls()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            bool menus = Services.TryGet<UiState>(out var ui) && ui.AnyOpen;
            if (keyboard == null || menus) return Vector3.zero;
            if (keyboard.fKey.wasPressedThisFrame && SwitchedFrame != Time.frameCount) { StopFlying(); return Vector3.zero; }
            if (keyboard.tKey.wasPressedThisFrame || (mouse != null && mouse.rightButton.wasPressedThisFrame)) SetThermal(!Thermal);
            if (mouse != null && mouse.leftButton.wasPressedThisFrame) LampOn = !LampOn;

            if (mouse != null && UnityEngine.Cursor.lockState == CursorLockMode.Locked)
            {
                float sensitivity = Services.TryGet<PlayerPreferences>(out var prefs) ? prefs.mouseSensitivity : 1f;
                Vector2 d = mouse.delta.ReadValue() * player.Settings.mouseSpeed * sensitivity;
                yaw += d.x;
                pitch = Mathf.Clamp(pitch - d.y, -60f, 85f);
            }
            float fwd = (keyboard.wKey.isPressed ? 1f : 0f) - (keyboard.sKey.isPressed ? 1f : 0f);
            float side = (keyboard.dKey.isPressed ? 1f : 0f) - (keyboard.aKey.isPressed ? 1f : 0f);
            float up = (keyboard.spaceKey.isPressed ? 1f : 0f) - (keyboard.leftCtrlKey.isPressed || keyboard.cKey.isPressed ? 1f : 0f);
            float speed = s.droneSpeed * (keyboard.leftShiftKey.isPressed ? s.droneBoost : 1f);
            Quaternion facing = Quaternion.Euler(0f, yaw, 0f);
            Vector3 flat = facing * new Vector3(side, 0f, fwd);
            if (flat.sqrMagnitude > 1f) flat.Normalize();
            Vector3 wish = flat * speed + Vector3.up * up * s.droneClimbSpeed;
            if (Height > s.droneMaxHeight && wish.y > 0f) wish.y = 0f;
            return wish;
        }

        // Kinematic: it slides along whatever it meets, and keeps a little off the ground.
        void Move(float dt)
        {
            Vector3 step = velocity * dt;
            int mask = ~(Layers.Mask(Layers.Effects) | Layers.Mask(Layers.PlayerBody) | Layers.Mask(Layers.Pickups) | Layers.Mask(Layers.Thermal));
            Vector3 centre = transform.position + Vector3.up * 0.1f;
            for (int i = 0; i < 2 && step.sqrMagnitude > 1e-8f; i++)
            {
                if (!Physics.SphereCast(centre, 0.22f, step.normalized, out var hit, step.magnitude, mask, QueryTriggerInteraction.Ignore)) break;
                step = Vector3.ProjectOnPlane(step, hit.normal);
                velocity = Vector3.ProjectOnPlane(velocity, hit.normal);
            }
            transform.position += step;

            if (Physics.Raycast(transform.position + Vector3.up * 0.5f, Vector3.down, out var ground, 2f, Layers.Mask(Layers.Ground) | Layers.Mask(Layers.Props), QueryTriggerInteraction.Ignore))
            {
                float floor = ground.point.y + 0.02f;
                if (transform.position.y < floor)
                {
                    transform.position = new Vector3(transform.position.x, floor, transform.position.z);
                    if (velocity.y < 0f)
                    {
                        if (airborne && !Flying)
                        {
                            // Down: motors off.
                            airborne = false;
                            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(Sound.Clunk, transform.position, MixerNames.World, Dead ? 0.6f : 0.25f, 1.5f, 15f);
                        }
                        velocity.y = 0f;
                    }
                }
            }
        }

        void SetThermal(bool on)
        {
            Thermal = on;
            ThermalView.Ensure();
            // (The thermal look is a volume on its own layer, which only this camera listens to while it's on.)
            camData.volumeLayerMask = on ? (LayerMask)(1 | Layers.Mask(Layers.Thermal)) : (LayerMask)1;
        }

        // Warm things glow: a soft shape drawn over each, seen only by the drone's camera.
        void DrawHeat()
        {
            foreach (var h in Heat.All)
            {
                if (!h.Visible) continue;
                Vector3 p = h.At.position + Vector3.up * h.Height * 0.5f;
                var m = Matrix4x4.TRS(p, Quaternion.identity, new Vector3(h.Radius * 2.4f, h.Height * 0.55f, h.Radius * 2.4f));
                Graphics.DrawMesh(heatMesh, m, heatMat, Layers.Thermal, cam, 0, null, ShadowCastingMode.Off, false);
            }
        }

        // ------------------------------------------------------------------ picking it up

        public void Interact()
        {
            if (Flying || player == null || player.Hotbar == null) return;
            var items = player.Hotbar.Items;
            int slot = -1;
            for (int i = 0; i < Inventory.SlotCount; i++)
                if (!items[i].Empty && items[i].Kind == ItemKind.Drone && items[i].Lit) { slot = i; break; }
            if (slot >= 0)
            {
                items.SetCharge(slot, Battery);
                items.SetLit(slot, false);
            }
            else if (!player.Hotbar.Give(ItemKind.Drone, 1, Mathf.Max(0.0001f, Battery))) return; // (no room)
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(Sound.Pickup, transform.position, MixerNames.World, 0.6f, 1f, 10f);
            Destroy(gameObject);
        }
    }

    /// <summary>The drone's thermal look: grey and blue, no fog (heat cuts through it), warm things glowing.</summary>
    static class ThermalView
    {
        static Volume volume;

        public static void Ensure()
        {
            if (volume != null) return;
            var go = new GameObject("Thermal look") { layer = Layers.Thermal };
            Object.DontDestroyOnLoad(go);
            volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 500f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            var fog = profile.Add<Fog>(true);
            fog.enabled.Override(false);
            var grade = profile.Add<ColorAdjustments>(true);
            grade.saturation.Override(-100f);
            grade.contrast.Override(35f);
            grade.postExposure.Override(1.5f);
            grade.colorFilter.Override(new Color(0.55f, 0.78f, 1f));
            volume.sharedProfile = profile;
        }
    }
}
