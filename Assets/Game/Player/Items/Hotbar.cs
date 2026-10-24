using Game.Audio;
using Game.Core;
using Game.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.HighDefinition;

namespace Game.Player
{
    /// <summary>
    /// What the player carries and holds (section 6.3): the eight-slot <see cref="Inventory"/>, picking a slot
    /// (1-8 or the wheel), the model in the hand, and F to use what's held. A lit torch or open lighter is a safe
    /// circle round you. Putting it away puts it out. Lives on the player.
    /// </summary>
    public sealed class Hotbar : MonoBehaviour
    {
        PlayerController player;
        ItemsConfig config;
        Transform hand;
        GameObject model;
        Transform flame;
        ItemKind shown = ItemKind.None;
        Light handLight;
        Polaroids polaroids;
        HDAdditionalLightData handLightData;
        Light flash;
        float flashLeft;
        SafeZone handZone;
        System.IDisposable resetSub;
        string hint;
        float hintUntil;

        public Inventory Items { get; } = new Inventory();
        public ItemSettings Settings => config.settings;
        /// <summary>A short message for the HUD ("You need a lighter."), or null.</summary>
        public string Hint => Time.unscaledTime < hintUntil ? hint : null;
        /// <summary>When the selection or contents last changed (the HUD shows the hotbar for a moment).</summary>
        public float LastChanged { get; private set; } = -100f;

        void Awake()
        {
            player = GetComponent<PlayerController>();
            config = Services.TryGet<GameSetup>(out var setup) ? setup.Get<ItemsConfig>() : null;
            if (config == null)
            {
                Debug.LogWarning("No ItemsConfig in the GameSetup asset; using defaults.");
                config = ScriptableObject.CreateInstance<ItemsConfig>();
            }
            Items.Changed += () => LastChanged = Time.unscaledTime;
            handZone = new SafeZone("Held flame", transform.position, 0f) { Active = false };
            Services.Register(this);
        }

        void Start()
        {
            var eye = player.Head;
            hand = new GameObject("Hand").transform;
            hand.SetParent(eye, false);
            hand.localPosition = config.settings.handOffset;

            var lightGo = new GameObject("Hand light");
            lightGo.transform.SetParent(hand, false);
            lightGo.AddHDLight(LightType.Point);
            handLight = lightGo.GetComponent<Light>();
            handLightData = lightGo.GetComponent<HDAdditionalLightData>();
            handLight.shadows = LightShadows.Soft;
            handLight.enabled = false;

            var flashGo = new GameObject("Camera flash");
            flashGo.transform.SetParent(eye, false);
            flashGo.AddHDLight(LightType.Spot);
            flash = flashGo.GetComponent<Light>();
            flash.shadows = LightShadows.None;
            flash.spotAngle = 80f;
            flash.color = new Color(0.9f, 0.95f, 1f);
            flash.range = 45f;
            flash.enabled = false;

            if (Services.TryGet<SafeZones>(out var zones)) zones.Add(handZone);
            if (Services.TryGet<EventBus>(out var events)) resetSub = events.Subscribe<RunReset>(_ => OnRestart());
            ShowHeld();
        }

        void OnDestroy()
        {
            resetSub?.Dispose();
            if (Services.TryGet<SafeZones>(out var zones)) zones.Remove(handZone);
            Services.Unregister(this);
        }

        // ------------------------------------------------------------------ picking things up

        /// <summary>Put something in the hotbar. False (with a hint) if there's no room.</summary>
        public bool Give(ItemKind kind, int count = 1, float charge = -1f)
        {
            if (charge < 0f) charge = kind == ItemKind.Camera ? config.settings.cameraShots : 1f;
            if (!Items.Add(kind, count, charge))
            {
                Say("No room.");
                return false;
            }
            Play(Sound.Pickup, 0.7f, Random.Range(0.9f, 1.1f));
            if (Services.TryGet<EventBus>(out var events)) events.Publish(new ItemPickedUp(Inventory.NameOf(kind), count));
            return true;
        }

        /// <summary>A snack, eaten on the spot: more sprint time for the run, and your breath back.</summary>
        public void EatSnack()
        {
            player.Stamina.Bonus += config.settings.snackBonus;
            player.Stamina.Refill();
            Play(Sound.Eat, 0.8f, 1f);
            if (Services.TryGet<EventBus>(out var events)) events.Publish(new ItemPickedUp("Snack", 1));
        }

        public void Say(string text, float seconds = 2.5f)
        {
            hint = text;
            hintUntil = Time.unscaledTime + seconds;
        }

        // ------------------------------------------------------------------ every frame

        /// <summary>Called by the player each frame: the number keys and wheel pick a slot (unless the keys are taken).</summary>
        public void HandleSelection(bool keysTaken, bool mouseTaken)
        {
            var keyboard = Keyboard.current;
            if (!keysTaken && keyboard != null)
            {
                for (int i = 0; i < Inventory.SlotCount; i++)
                    if (keyboard[Key.Digit1 + i].wasPressedThisFrame) Items.Select(i);
            }
            var mouse = Mouse.current;
            if (!mouseTaken && mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;
                if (wheel > 0.1f) Items.Cycle(-1);
                else if (wheel < -0.1f) Items.Cycle(1);
            }
        }

        void Update()
        {
            hand.localPosition = config.settings.handOffset;
            if (shown != Items.HeldKind) ShowHeld();
            Burn(Time.deltaTime);
            UpdateLights();
            if (flashLeft > 0f)
            {
                flashLeft -= Time.deltaTime;
                flash.intensity = config.settings.cameraFlashIntensity * Mathf.Clamp01(flashLeft / 0.12f);
                flash.enabled = flashLeft > 0f;
            }
        }

        // A lit torch or open lighter uses itself up.
        void Burn(float dt)
        {
            int slot = Items.Selected;
            var held = Items[slot];
            if (held.Empty || !held.Lit || dt <= 0f) return;
            var s = config.settings;
            float time = held.Kind == ItemKind.Torch ? s.torchBurnTime : s.lighterFuelTime;
            float left = held.Charge - dt / Mathf.Max(0.01f, time);
            if (left > 0f)
            {
                Items.SetCharge(slot, left);
                return;
            }
            if (held.Kind == ItemKind.Torch)
            {
                Items.Clear(slot);
                Say("The torch burnt out.");
                BackToFlashlight();
            }
            else
            {
                Items.SetCharge(slot, 0f);
                Items.SetLit(slot, false);
                Say("The lighter's out of fuel.");
            }
        }

        void UpdateLights()
        {
            var s = config.settings;
            var held = Items.Held;
            bool torch = !held.Empty && held.Lit && held.Kind == ItemKind.Torch;
            bool lighter = !held.Empty && held.Lit && held.Kind == ItemKind.Lighter;

            handZone.Active = torch || lighter;
            handZone.Radius = torch ? s.torchSafeRadius : s.lighterSafeRadius;
            handZone.Center = transform.position;
            handZone.Name = torch ? "Torch" : "Lighter";

            handLight.enabled = torch || lighter;
            if (flame != null) flame.gameObject.SetActive(torch || lighter);
            if (!handLight.enabled) return;
            float flicker = 0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 7f, 0.7f) - 0.15f * Mathf.PerlinNoise(Time.time * 19f, 3.1f);
            handLight.color = torch ? s.torchColor : s.lighterColor;
            handLight.intensity = (torch ? s.torchIntensity : s.lighterIntensity) * flicker;
            handLight.range = torch ? s.torchRange : s.lighterRange;
            if (handLightData != null) handLightData.volumetricDimmer = torch ? s.torchFogGlow : s.lighterFogGlow;
            if (flame != null && flame.TryGetComponent<Renderer>(out var flameLook) && flameLook.sharedMaterial != null && flameLook.sharedMaterial.name == "Flame")
                RuntimeMaterials.SetGlow(flameLook.sharedMaterial, new Color(1f, 0.5f, 0.15f) * s.flameGlow * flicker);
            handLight.transform.position = flame != null ? flame.position : hand.position + hand.forward * 0.3f;
        }

        // ------------------------------------------------------------------ the hand

        void ShowHeld()
        {
            var kind = Items.HeldKind;
            // Whatever was lit before goes out when it's put away.
            for (int i = 0; i < Inventory.SlotCount; i++)
                if (i != Items.Selected && Items[i].Lit) Items.SetLit(i, false);
            if (player.Flashlight != null) player.Flashlight.Stowed = kind != ItemKind.Flashlight;

            if (model != null) Destroy(model);
            model = null;
            flame = null;
            shown = kind;
            if (kind == ItemKind.None || hand == null) return;

            // The flashlight model sits on the beam (which trails the view a little); everything else in the hand.
            Transform parent = kind == ItemKind.Flashlight && player.Flashlight != null ? player.Flashlight.Beam : hand;
            model = ItemModels.Make(kind, config.art.Held(kind), parent, true);
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = kind == ItemKind.Torch ? Quaternion.Euler(-35f, 0f, 0f) : Quaternion.identity;
            flame = FindChild(model.transform, "Flame");
            foreach (var t in model.GetComponentsInChildren<Transform>()) t.gameObject.layer = Layers.Effects;
        }

        static Transform FindChild(Transform root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.Contains(name)) return t;
            return null;
        }

        // ------------------------------------------------------------------ F: use what's held

        public void Use()
        {
            int slot = Items.Selected;
            var held = Items[slot];
            if (held.Empty) return;
            var s = config.settings;
            switch (held.Kind)
            {
                case ItemKind.Flashlight:
                    player.Flashlight?.Toggle();
                    break;

                case ItemKind.Batteries:
                    var light = player.Flashlight;
                    if (light == null) break;
                    if (light.Battery.Charge >= 0.999f)
                    {
                        Say("The flashlight's already full.");
                        break;
                    }
                    light.Battery.Add(s.batteryCharge);
                    Items.Take(slot);
                    BackToFlashlight();
                    Play(Sound.Click, 0.7f, 0.8f);
                    Used(held.Kind, $"battery in ({light.Battery.Charge * 100f:0}%)");
                    break;

                case ItemKind.Torch:
                    if (held.Lit)
                    {
                        Items.SetLit(slot, false);
                        Used(held.Kind, "put out");
                        break;
                    }
                    int lighter = FindLighterWithFuel(s.torchLightCost);
                    if (lighter < 0)
                    {
                        Say(Items.Find(ItemKind.Lighter) >= 0 ? "The lighter hasn't enough fuel." : "You need a lighter to light it.");
                        Play(Sound.Click, 0.4f, 0.6f);
                        break;
                    }
                    Items.SetCharge(lighter, Items[lighter].Charge - s.torchLightCost);
                    Items.SetLit(slot, true);
                    Play(Sound.Lighter, 0.6f, 1f);
                    Play(Sound.Ignite, 0.9f, 0.9f);
                    Used(held.Kind, "lit");
                    break;

                case ItemKind.Lighter:
                    if (!held.Lit && held.Charge <= 0f)
                    {
                        Say("The lighter's out of fuel.");
                        Play(Sound.Lighter, 0.4f, 1.2f);
                        break;
                    }
                    Items.SetLit(slot, !held.Lit);
                    Play(Sound.Lighter, 0.6f, held.Lit ? 1.4f : 1f);
                    if (!held.Lit) Play(Sound.Ignite, 0.4f, 1.5f);
                    break;

                case ItemKind.FlareGun:
                    Flare.Fire(s, hand.position + hand.forward * 0.2f, player.Head.forward);
                    Items.Clear(slot);
                    BackToFlashlight();
                    Play(Sound.FlareShot, 1f, 1f);
                    Noise(40f, "flare gun");
                    Used(held.Kind, "fired");
                    break;

                case ItemKind.Glowsticks:
                    ThrownGlowstick.Throw(s, config.art.glowstick, hand.position + hand.forward * 0.15f, player.Head.forward);
                    Items.Take(slot);
                    BackToFlashlight();
                    Play(Sound.Crack, 0.7f, Random.Range(0.9f, 1.15f));
                    Used(held.Kind, "tossed one");
                    break;

                case ItemKind.Camera:
                    TakePhoto(slot, held);
                    break;

                case ItemKind.Map:
                    Play(Sound.Click, 0.25f, 0.6f);
                    if (Services.TryGet<EventBus>(out var mapEvents)) mapEvents.Publish(new MapToggled());
                    break;

                case ItemKind.KeyFob:
                    Play(Sound.Click, 0.5f, 1.6f);
                    if (Services.TryGet<EventBus>(out var events)) events.Publish(new KeyFobPressed());
                    break;
            }
        }

        // Used the last of what was held: back to the flashlight, rather than an empty hand.
        void BackToFlashlight()
        {
            if (!Items[Items.Selected].Empty) return;
            int flashlight = Items.Find(ItemKind.Flashlight);
            if (flashlight >= 0) Items.Select(flashlight);
        }

        int FindLighterWithFuel(float need)
        {
            for (int i = 0; i < Inventory.SlotCount; i++)
                if (!Items[i].Empty && Items[i].Kind == ItemKind.Lighter && Items[i].Charge >= need) return i;
            return -1;
        }

        void TakePhoto(int slot, ItemStack held)
        {
            var s = config.settings;
            if (held.Charge < 1f)
            {
                Say("No film left.");
                Play(Sound.Click, 0.4f, 0.7f);
                return;
            }
            flashLeft = 0.15f;
            flash.enabled = true;
            Play(Sound.Shutter, 0.9f, 1f);
            var eye = player.Head;
            if (Services.TryGet<EventBus>(out var events))
                events.Publish(new FlashBurst(eye.position, eye.forward, s.cameraRange, s.cameraHalfAngle, s.cameraRepelTime));
            // The shot itself, lit by the flash: a polaroid comes out.
            if (polaroids == null && !TryGetComponent(out polaroids)) polaroids = gameObject.AddComponent<Polaroids>();
            polaroids.Take(player.View, s);
            float left = held.Charge - 1f;
            if (left < 1f)
            {
                Items.Clear(slot);
                Say("That was the last of the film.");
                BackToFlashlight();
            }
            else Items.SetCharge(slot, left);
            Used(ItemKind.Camera, left >= 1f ? $"photo ({left:0} left)" : "photo (the last)");
        }

        void Used(ItemKind kind, string what)
        {
            if (Services.TryGet<EventBus>(out var events)) events.Publish(new ItemUsed(Inventory.NameOf(kind), what));
        }

        void Noise(float radius, string source)
        {
            if (Services.TryGet<EventBus>(out var events)) events.Publish(new NoiseEmitted(transform.position, radius, source));
        }

        void Play(Sound sound, float volume, float pitch)
        {
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(sound, transform.position + Vector3.up * 1.4f, MixerNames.Player, volume, pitch, 10f);
        }

        // ------------------------------------------------------------------ restart

        // Back to the start: just the flashlight (switched on, full), no snack bonus, nothing glowing.
        void OnRestart()
        {
            Items.Reset();
            player.Stamina.Bonus = 0f;
            ThrownGlowstick.ClearAll();
            if (player.Flashlight != null)
            {
                player.Flashlight.Battery.Add(1f);
                player.Flashlight.SwitchOn();
            }
            ShowHeld();
        }
    }
}
