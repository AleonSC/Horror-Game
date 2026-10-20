using UnityEngine;

/// <summary>
/// The player's hotbar: eight slots along the bottom of the screen. You start with just the flashlight in
/// the first; everything else goes into the next free slot as you find it (and its slot empties when it's
/// used up). Rearrange them by dragging in the inventory (Tab). Press a slot's number (or scroll the mouse
/// wheel) to hold what's in it, and F to use it.
///
/// - Batteries: picked-up batteries pile up in one slot. F puts one in the flashlight, topping it up by
///   batteryCharge, and goes back to the flashlight.
///
/// - Camera: found in the broken car's trunk, three shots. F takes a flash photo: every entity caught in
///   the frame (in range, nothing in the way) is driven off for banishTime seconds.
///
/// - Key fob: sometimes found at campsites and shacks. F presses its button: the broken car chirps and
///   flashes its lights, wherever it is, so you can find it. (Anything listening near the car hears it too.)
///
/// - Glowsticks: F cracks one and tosses it a few metres ahead, where it lies glowing yellow for minutes.
///   Only a light to find your way by (the entities ignore it). Ten in a bundle at each campsite.
///
/// - Flare gun: each one holds a single flare. Fire it into the sky and it bursts at the top of its arc,
///   wiping out every entity's hunt progress. Fired low, it's wasted. Found at campsites.
///
/// - Torch: lighting it uses some of the lighter's fuel. While lit it throws a wide circle of firelight
///   the entities won't enter or attack in. It burns down, and when it's gone it's gone for good.
/// - Lighter: flick it on for a small circle of safety. Its fuel runs out too.
///
/// Torches and lighters are found in the abandoned shacks; picking up another one stacks it.
/// Only the item in your hand works, so putting the torch or lighter away puts it out.
/// LampSpawner adds this to the player automatically.
/// </summary>
public class Hotbar : MonoBehaviour, SafeZones.ISource
{
    public enum Item { Flashlight, Torch, Lighter, FlareGun, Glowstick, Battery, Camera, KeyFob }
    const int SlotCount = 8;
    [Header("Camera")]
    [Tooltip("Shots on the camera when it's found.")]
    public int cameraShots = 3;
    [Tooltip("Seconds an entity caught in the flash stays gone.")]
    public float banishTime = 30f;
    [Tooltip("How far away the flash reaches things on the ground (the flier, up in the sky, a good deal further).")]
    public float flashRange = 40f;
    [Tooltip("Seconds for the flash to charge again between shots.")]
    public float flashRecharge = 1.6f;
    [Range(0f, 1f), Tooltip("How much of the flashlight each battery refills.")]
    public float batteryCharge = 0.1f;
    [Tooltip("Glowsticks in each bundle found at a campsite.")]
    public int glowsticksPerBundle = 10;

    [Header("Torch")]
    [Tooltip("Seconds a torch burns before it's used up for good.")]
    public float torchBurnTime = 120f;
    [Tooltip("Radius of the safe circle around a lit torch.")]
    public float torchRadius = 7f;
    [Range(0f, 1f), Tooltip("Share of the lighter's fuel used to light the torch.")]
    public float torchLightingCost = 0.1f;
    public Color torchColor = new Color(1f, 0.55f, 0.2f);

    [Header("Lighter")]
    [Tooltip("Seconds a lighter can stay lit before it's empty for good.")]
    public float lighterFuelTime = 60f;
    [Tooltip("Radius of the safe circle around a lit lighter.")]
    public float lighterRadius = 3.5f;
    public Color lighterColor = new Color(1f, 0.72f, 0.35f);

    [Header("HUD")]
    public bool showHotbar = true;

    public Item Selected { get; private set; } = Item.Flashlight;

    // What's in each hotbar slot: an Item, or -1 for an empty slot.
    readonly int[] slots = { 0, -1, -1, -1, -1, -1, -1, -1 };
    float selectedAt = -10f;
    public int TorchCount { get; private set; }
    public int LighterCount { get; private set; }
    /// <summary>Fuel left in the current torch, 0 to 1.</summary>
    public float TorchFuel { get; private set; } = 1f;
    /// <summary>Fuel left in the current lighter, 0 to 1.</summary>
    public float LighterFuel { get; private set; } = 1f;
    public bool TorchLit { get; private set; }
    public bool LighterLit { get; private set; }
    /// <summary>Flares left (one per flare gun picked up).</summary>
    public int FlareCount { get; private set; }
    public int GlowstickCount { get; private set; }
    public int BatteryCount { get; private set; }
    public int CameraShots { get; private set; }
    public bool HasKeyFob { get; private set; }
    float fobReady;
    Renderer fobLed;

    bool SafeZones.ISource.IsActive => TorchLit || LighterLit;
    Vector3 SafeZones.ISource.Center => transform.position;
    float SafeZones.ISource.Radius => TorchLit ? torchRadius : lighterRadius;

    Flashlight flashlight;
    Transform torchModel, lighterModel, torchFlame, lighterFlame;
    Light torchLight, lighterLight;
    AudioSource fireLoop, sfx;
    string message;
    float messageTimer;
    GUIStyle nameStyle, numberStyle, messageStyle;

    static readonly string[] ItemNames = { "Flashlight", "Torch", "Lighter", "Flare Gun", "Glowsticks", "Batteries", "Camera", "Key Fob" };
    Transform flareGunModel, muzzle, glowstickModel, batteryModel, cameraModel, cameraFlashPoint, fobModel;
    Light flashLight;
    float flashGlow, flashReady;

    void OnEnable() => SafeZones.Register(this);
    void OnDisable() => SafeZones.Unregister(this);

    void Start()
    {
        if (GetComponent<InventoryScreen>() == null) gameObject.AddComponent<InventoryScreen>();
        flashlight = GetComponentInChildren<Flashlight>();
        if (flashlight == null) flashlight = FindAnyObjectByType<Flashlight>();

        Transform hand = Camera.main != null ? Camera.main.transform : transform;
        BuildTorch(hand);
        BuildLighter(hand);
        BuildFlareGun(hand);
        glowstickModel = new GameObject("Held Glowstick").transform;
        glowstickModel.SetParent(hand, false);
        glowstickModel.localPosition = new Vector3(0.22f, -0.24f, 0.45f);
        glowstickModel.localRotation = Quaternion.Euler(30f, 0f, -20f);
        Glowstick.BuildModel(glowstickModel, false);
        glowstickModel.gameObject.SetActive(false);
        BuildBattery(hand);
        BuildCamera(hand);
        fobModel = new GameObject("Held Key Fob").transform;
        fobModel.SetParent(hand, false);
        fobModel.localPosition = new Vector3(0.16f, -0.18f, 0.32f);
        fobModel.localRotation = Quaternion.Euler(-55f, 10f, 0f);
        PropModels.KeyFob(fobModel, false, out fobLed).localScale = Vector3.one * 1.4f;
        fobModel.gameObject.SetActive(false);

        fireLoop = ProceduralAudio.AddSource(gameObject, ProceduralAudio.FireLoop, true, false, 0f);
        sfx = ProceduralAudio.AddSource(gameObject, null, false, false, 0.8f);
    }

    void Update()
    {
        SyncSlots();
        if (PauseMenu.IsPaused || InventoryScreen.IsOpen) return;
        int slot = HorrorInput.SlotPressed;
        if (slot > 0 && slots[slot - 1] >= 0) Select((Item)slots[slot - 1]);

        // Mouse wheel: down moves to the next item you have, up to the previous one.
        float scroll = HorrorInput.Scroll;
        if (Mathf.Abs(scroll) > 0.01f) Cycle(scroll < 0f ? 1 : -1);

        // The flashlight handles F itself while it's in hand.
        if (HorrorInput.FlashlightPressed && Selected != Item.Flashlight) Use();

        BurnFuel(Time.deltaTime);
        UpdateVisuals();
        messageTimer -= Time.deltaTime;
    }

    // ---------------------------------------------------------------- Items

    public void Select(Item item)
    {
        if (item == Selected) return;
        if (item == Item.Torch && TorchCount == 0) { Message("You don't have a torch"); return; }
        if (item == Item.Lighter && LighterCount == 0) { Message("You don't have a lighter"); return; }
        if (item == Item.FlareGun && FlareCount == 0) { Message("You don't have a flare gun"); return; }
        if (item == Item.Glowstick && GlowstickCount == 0) { Message("You don't have any glowsticks"); return; }
        if (item == Item.Battery && BatteryCount == 0) { Message("You don't have any batteries"); return; }
        if (item == Item.Camera && CameraShots == 0) { Message("You don't have a camera"); return; }
        if (item == Item.KeyFob && !HasKeyFob) { Message("You don't have a key fob"); return; }

        // Only the item in hand works: putting a flame away puts it out.
        TorchLit = false;
        LighterLit = false;

        Selected = item;
        selectedAt = Time.unscaledTime;
        if (flashlight != null) flashlight.SetEquipped(item == Item.Flashlight);
        Play(ProceduralAudio.Pickup, 0.3f, 1.3f);
    }

    /// <summary>Steps through the hotbar to the next (or previous) slot that has something in it, wrapping round.</summary>
    void Cycle(int direction)
    {
        int at = Mathf.Max(0, SlotOf(Selected));
        for (int step = 1; step < SlotCount; step++)
        {
            int i = (at + direction * step + SlotCount * 2) % SlotCount;
            if (slots[i] >= 0)
            {
                Select((Item)slots[i]);
                return;
            }
        }
    }

    // ---------------------------------------------------------------- Slots

    public static int SlotTotal => SlotCount;

    /// <summary>
    /// Keeps the slots in step with what you have: anything used up leaves its slot empty, and anything
    /// newly found goes into the first empty slot.
    /// </summary>
    void SyncSlots()
    {
        for (int i = 0; i < SlotCount; i++)
            if (slots[i] >= 0 && Count((Item)slots[i]) <= 0) slots[i] = -1;
        for (int it = 0; it < ItemNames.Length; it++)
        {
            if (Count((Item)it) <= 0 || SlotOf((Item)it) >= 0) continue;
            int free = System.Array.IndexOf(slots, -1);
            if (free < 0) return;
            slots[free] = it;
        }
    }

    /// <summary>Which slot an item is in (0-based), or -1.</summary>
    public int SlotOf(Item item) => System.Array.IndexOf(slots, (int)item);

    /// <summary>What's in a slot; false if it's empty.</summary>
    public bool TryGetSlot(int index, out Item item)
    {
        item = Item.Flashlight;
        if (index < 0 || index >= SlotCount || slots[index] < 0) return false;
        item = (Item)slots[index];
        return true;
    }

    /// <summary>Swaps two slots' contents (either can be empty): the inventory's drag and drop.</summary>
    public void SwapSlots(int a, int b)
    {
        if (a < 0 || b < 0 || a >= SlotCount || b >= SlotCount || a == b) return;
        (slots[a], slots[b]) = (slots[b], slots[a]);
    }

    /// <summary>The key that holds an item ("3"), syncing first so something just picked up has its slot.</summary>
    string Key(Item item)
    {
        SyncSlots();
        int i = SlotOf(item);
        return i >= 0 ? (i + 1).ToString() : "?";
    }

    /// <summary>How many of an item you have (1 for the flashlight, 0 or 1 for the key fob).</summary>
    public int CountOf(Item item) => Count(item);

    int Count(Item item) =>
        item == Item.Torch ? TorchCount :
        item == Item.Lighter ? LighterCount :
        item == Item.FlareGun ? FlareCount :
        item == Item.Glowstick ? GlowstickCount :
        item == Item.Battery ? BatteryCount :
        item == Item.Camera ? CameraShots :
        item == Item.KeyFob ? (HasKeyFob ? 1 : 0) : 1;

    void Use()
    {
        if (Selected == Item.Torch)
        {
            if (TorchLit)
            {
                TorchLit = false;
                Play(ProceduralAudio.BurnOut, 0.6f);
            }
            else if (LighterCount == 0)
            {
                Message("You need a lighter to light the torch");
            }
            else
            {
                Play(ProceduralAudio.LighterFlick, 0.7f);
                Play(ProceduralAudio.TorchIgnite, 0.8f);
                Noise.Emit(transform.position, 6f);
                TorchLit = true;
                SpendLighter(torchLightingCost);
            }
        }
        else if (Selected == Item.Lighter)
        {
            LighterLit = !LighterLit;
            Play(LighterLit ? ProceduralAudio.LighterFlick : ProceduralAudio.FlashlightClick, 0.7f);
        }
        else if (Selected == Item.FlareGun)
        {
            FireFlare();
        }
        else if (Selected == Item.Glowstick)
        {
            ThrowGlowstick();
        }
        else if (Selected == Item.Battery)
        {
            UseBattery();
        }
        else if (Selected == Item.Camera)
        {
            TakePhoto();
        }
        else if (Selected == Item.KeyFob)
        {
            PressFob();
        }
    }

    /// <summary>The fob's button: the car chirps and flashes its lights, wherever it is.</summary>
    void PressFob()
    {
        if (!HasKeyFob || Time.time < fobReady) return;
        fobReady = Time.time + 1.5f;
        Play(ProceduralAudio.FobClick, 0.6f);
        fobLedTimer = 0.25f;
        foreach (var car in BrokenCar.All) car.Locate();
    }

    float fobLedTimer;
    static Material fobLedOn, fobLedOff;

    /// <summary>
    /// A flash photo: a blinding burst of light, and every entity caught in the frame (close enough, nothing
    /// in the way) is driven off for a while. One of the camera's few shots, caught anything or not.
    /// </summary>
    void TakePhoto()
    {
        if (CameraShots <= 0 || Time.time < flashReady) return;
        var cam = Camera.main;
        if (cam == null) return;
        CameraShots--;
        flashReady = Time.time + flashRecharge;
        flashGlow = 1f;
        Play(ProceduralAudio.CameraFlash, 1f);
        Noise.Emit(transform.position, 4f);

        int caught = 0;
        foreach (var e in new System.Collections.Generic.List<ShadowEntity>(ShadowEntity.All))
            if (e.CanBeFlashed && InShot(cam, e.transform.position + Vector3.up * 1.4f, flashRange)) { e.Banish(banishTime); caught++; }
        foreach (var b in new System.Collections.Generic.List<BlindEntity>(BlindEntity.All))
            if (b.IsPresent && (InShot(cam, b.transform.position + Vector3.up * b.height * 0.85f, flashRange)
                                || InShot(cam, b.transform.position + Vector3.up * b.height * 0.45f, flashRange))) { b.Banish(banishTime); caught++; }
        foreach (var f in new System.Collections.Generic.List<FlyingEntity>(FlyingEntity.All))
            if (f.IsPresent && InShot(cam, f.transform.position, flashRange * 3f)) { f.Banish(banishTime); caught++; }

        string left = CameraShots == 1 ? "1 shot left" : $"{CameraShots} shots left";
        Message(caught > 0 ? $"Caught it in the flash. ({left})" : $"Nothing in the shot. ({left})");
        if (CameraShots == 0)
        {
            Message(caught > 0 ? "Caught it in the flash. That was the last shot." : "That was the camera's last shot.");
            Select(Item.Flashlight);
        }
    }

    /// <summary>True if a point is in the camera's frame, within range, with nothing solid in the way.</summary>
    static bool InShot(Camera cam, Vector3 point, float range)
    {
        Vector3 from = cam.transform.position;
        Vector3 to = point - from;
        float dist = to.magnitude;
        if (dist > range || dist < 0.1f) return false;
        Vector3 v = cam.WorldToViewportPoint(point);
        if (v.z <= 0f || v.x < -0.05f || v.x > 1.05f || v.y < -0.05f || v.y > 1.05f) return false;
        // Anything in the way? (Not counting what's right at the target: its own body, the ground it's on.)
        return !Physics.Raycast(from, to / dist, dist - 1.5f, ~0, QueryTriggerInteraction.Ignore)
               || !Physics.Raycast(from + cam.transform.up * 0.3f, (point + Vector3.up * 0.5f - from - cam.transform.up * 0.3f).normalized, dist - 1.5f, ~0, QueryTriggerInteraction.Ignore);
    }

    /// <summary>Puts a battery in the flashlight, then goes back to holding it.</summary>
    void UseBattery()
    {
        if (BatteryCount <= 0) return;
        if (flashlight == null || !flashlight.AddBattery(batteryCharge))
        {
            Message("Your flashlight is already full");
            return;
        }
        BatteryCount--;
        Play(ProceduralAudio.BatteryInsert, 0.9f);
        Message($"Flashlight +{Mathf.RoundToInt(batteryCharge * 100f)}% ({Mathf.RoundToInt(flashlight.Battery * 100f)}%)");
        Select(Item.Flashlight);
    }

    /// <summary>Adds a picked-up battery to the battery slot.</summary>
    public void AddBattery()
    {
        BatteryCount++;
        Message(BatteryCount == 1
            ? $"Picked up a battery. Press {Key(Item.Battery)} to hold it, F to put it in the flashlight."
            : $"Picked up a battery ({BatteryCount})");
    }

    void ThrowGlowstick()
    {
        if (GlowstickCount <= 0) return;
        var cam = Camera.main;
        Transform aim = cam != null ? cam.transform : transform;
        Vector3 velocity = aim.forward * 7f + Vector3.up * 2f;
        Glowstick.Throw(glowstickModel.position, velocity);
        Play(ProceduralAudio.TwigSnap, 0.35f, 1.8f); // the crack
        Noise.Emit(transform.position, 6f);
        GlowstickCount--;
        if (GlowstickCount == 0) Select(Item.Flashlight);
    }

    void FireFlare()
    {
        if (FlareCount <= 0) return;
        var cam = Camera.main;
        Vector3 direction = cam != null ? cam.transform.forward : transform.forward;
        Flare.Fire(muzzle.position, direction, 30f);
        Play(ProceduralAudio.FlareShot, 1f);
        Noise.Emit(transform.position, 45f); // the whole forest hears that
        FlareCount--;

        if (direction.y < 0.4f) Message("Too low. Flares need to go up into the sky.");
        if (FlareCount == 0) Select(Item.Flashlight);
    }

    void BurnFuel(float dt)
    {
        if (TorchLit)
        {
            TorchFuel -= dt / Mathf.Max(torchBurnTime, 0.01f);
            if (TorchFuel <= 0f) TorchUsedUp();
        }
        if (LighterLit)
        {
            LighterFuel -= dt / Mathf.Max(lighterFuelTime, 0.01f);
            if (LighterFuel <= 0f) LighterUsedUp();
        }
    }

    void SpendLighter(float amount)
    {
        LighterFuel -= amount;
        if (LighterFuel <= 0f) LighterUsedUp();
    }

    void TorchUsedUp()
    {
        TorchLit = false;
        TorchCount--;
        Play(ProceduralAudio.BurnOut, 0.8f);
        if (TorchCount > 0)
        {
            TorchFuel = 1f;
            Message("Your torch burned out. You light another.");
            TorchLit = Selected == Item.Torch;
        }
        else
        {
            TorchFuel = 0f;
            Message("Your torch has burned out");
            if (Selected == Item.Torch) Select(Item.Flashlight);
        }
    }

    void LighterUsedUp()
    {
        LighterLit = false;
        LighterCount--;
        if (LighterCount > 0)
        {
            LighterFuel = 1f;
        }
        else
        {
            LighterFuel = 0f;
            Message("Your lighter is empty");
            if (Selected == Item.Lighter) Select(Item.Flashlight);
        }
    }

    /// <summary>Adds a picked-up torch or lighter. Extra ones stack.</summary>
    public void AddItem(Item item)
    {
        if (item == Item.Torch)
        {
            if (TorchCount == 0) TorchFuel = 1f;
            TorchCount++;
            Message($"Picked up a torch. Press {Key(Item.Torch)} to hold it, F to light it.");
        }
        else if (item == Item.Lighter)
        {
            if (LighterCount == 0) LighterFuel = 1f;
            LighterCount++;
            Message($"Picked up a lighter. Press {Key(Item.Lighter)} to hold it, F to flick it.");
        }
        else if (item == Item.FlareGun)
        {
            FlareCount++;
            Message($"Picked up a flare gun. Press {Key(Item.FlareGun)} to hold it, then aim up and press F to fire.");
        }
        else if (item == Item.KeyFob)
        {
            HasKeyFob = true;
            Message($"Picked up a car key fob. Press {Key(Item.KeyFob)} to hold it, F to press the button: the car will chirp and flash.");
        }
        else if (item == Item.Camera)
        {
            CameraShots += cameraShots;
            Message($"Picked up a camera ({cameraShots} shots). Press {Key(Item.Camera)} to hold it, F to take a flash photo: it drives them off for a while.");
        }
        else if (item == Item.Glowstick)
        {
            GlowstickCount += glowsticksPerBundle;
            Message($"Picked up {glowsticksPerBundle} glowsticks. Press {Key(Item.Glowstick)} to hold them, F to crack and throw one.");
        }
    }

    /// <summary>Empties the hotbar back to just the flashlight (used when the run resets).</summary>
    public void ResetInventory()
    {
        TorchLit = LighterLit = false;
        TorchCount = LighterCount = FlareCount = GlowstickCount = BatteryCount = CameraShots = 0;
        HasKeyFob = false;
        flashGlow = 0f;
        TorchFuel = LighterFuel = 1f;
        Selected = Item.Flashlight;
        for (int i = 0; i < SlotCount; i++) slots[i] = i == 0 ? (int)Item.Flashlight : -1;
        if (flashlight != null) flashlight.SetEquipped(true);
        messageTimer = 0f;
    }

    void Message(string text)
    {
        message = text;
        messageTimer = 3f;
    }

    void Play(AudioClip clip, float volume, float pitch = 1f)
    {
        if (sfx == null || clip == null) return;
        sfx.pitch = pitch;
        sfx.PlayOneShot(clip, volume);
    }

    // ---------------------------------------------------------------- Held models

    void UpdateVisuals()
    {
        float time = Time.time;

        torchModel.gameObject.SetActive(Selected == Item.Torch);
        torchFlame.gameObject.SetActive(TorchLit);
        torchLight.enabled = TorchLit;
        if (TorchLit)
        {
            // Sputters and shrinks as it burns down.
            float low = Mathf.Clamp01(1f - TorchFuel / 0.15f);
            float flicker = 0.8f + 0.4f * Mathf.PerlinNoise(time * 9f, 0.3f) - low * Random.Range(0f, 0.5f);
            torchLight.intensity = 3f * flicker;
            torchFire.strength = Mathf.Lerp(1f, 0.45f, low); // shrinking as it burns down (the fire burns upward by itself)
        }

        flareGunModel.gameObject.SetActive(Selected == Item.FlareGun);
        glowstickModel.gameObject.SetActive(Selected == Item.Glowstick);
        batteryModel.gameObject.SetActive(Selected == Item.Battery);
        cameraModel.gameObject.SetActive(Selected == Item.Camera);
        fobModel.gameObject.SetActive(Selected == Item.KeyFob);
        // The fob's little LED lights while the button's pressed.
        fobLedTimer -= Time.deltaTime;
        if (fobLed != null)
        {
            if (fobLedOn == null) { fobLedOn = MaterialFactory.NoFog(new Color(1f, 0.1f, 0.05f) * 3f); fobLedOff = fobLed.sharedMaterial; }
            fobLed.sharedMaterial = fobLedTimer > 0f ? fobLedOn : fobLedOff;
        }
        // The flash: a blinding burst, gone in a moment.
        flashGlow = Mathf.MoveTowards(flashGlow, 0f, Time.deltaTime * 4f);
        flashLight.enabled = flashGlow > 0f;
        flashLight.intensity = 60f * flashGlow * flashGlow;
        if (cameraFlashPoint != null) flashLight.transform.position = cameraFlashPoint.position;
        lighterModel.gameObject.SetActive(Selected == Item.Lighter);
        lighterFlame.gameObject.SetActive(LighterLit);
        // The lid flips open when it's lit.
        if (lighterLid != null)
            lighterLid.localRotation = Quaternion.Slerp(lighterLid.localRotation, Quaternion.Euler(0f, 0f, LighterLit ? -115f : 0f), 1f - Mathf.Exp(-25f * Time.deltaTime));
        lighterLight.enabled = LighterLit;
        if (LighterLit)
        {
            lighterLight.intensity = 1.4f * (0.85f + 0.3f * Mathf.PerlinNoise(time * 10f, 5.1f));
            lighterFire.strength = 0.85f + 0.15f * Mathf.PerlinNoise(time * 14f, 2.9f);
        }

        float fireVolume = TorchLit ? 0.5f : LighterLit ? 0.12f : 0f;
        fireLoop.pitch = TorchLit ? 1f : 1.6f;
        fireLoop.volume = Mathf.MoveTowards(fireLoop.volume, fireVolume, Time.deltaTime * 2f);
        if (fireVolume > 0f && !fireLoop.isPlaying) fireLoop.Play();
        else if (fireLoop.volume <= 0f && fireLoop.isPlaying) fireLoop.Stop();
    }

    Fire torchFire, lighterFire;
    Transform lighterLid;

    void BuildTorch(Transform hand)
    {
        torchModel = new GameObject("Held Torch").transform;
        torchModel.SetParent(hand, false);
        torchModel.localPosition = new Vector3(0.3f, -0.62f, 0.62f);
        torchModel.localRotation = Quaternion.Euler(22f, 0f, -12f);
        PropModels.Torch(torchModel, false, out Transform flameAt);
        torchFlame = new GameObject("Flame").transform;
        torchFlame.SetParent(flameAt, false);
        torchFire = Fire.Create(torchFlame, Vector3.zero, 0.42f, new Color(1f, 0.55f, 0.15f), 8, 3);
        torchLight = new GameObject("Torch Light").AddComponent<Light>();
        torchLight.transform.SetParent(torchFlame, false);
        torchLight.transform.localPosition = new Vector3(0f, 0.15f, 0f);
        torchLight.type = LightType.Point;
        torchLight.color = torchColor;
        torchLight.range = torchRadius * 1.5f;
        torchLight.shadows = LightShadows.None;
        torchLight.enabled = false;
        torchModel.gameObject.SetActive(false);
    }

    void BuildLighter(Transform hand)
    {
        lighterModel = new GameObject("Held Lighter").transform;
        lighterModel.SetParent(hand, false);
        lighterModel.localPosition = new Vector3(0.18f, -0.25f, 0.4f);
        lighterModel.localRotation = Quaternion.Euler(8f, -20f, 0f);
        var model = PropModels.Lighter(lighterModel, false, out lighterLid, out Transform flameAt);
        model.localScale = Vector3.one * 1.3f;
        lighterFlame = new GameObject("Flame").transform;
        lighterFlame.SetParent(flameAt, false);
        lighterFire = Fire.Create(lighterFlame, Vector3.zero, 0.05f, new Color(1f, 0.62f, 0.25f), 0, 0);
        lighterLight = new GameObject("Lighter Light").AddComponent<Light>();
        lighterLight.transform.SetParent(lighterFlame, false);
        lighterLight.transform.localPosition = new Vector3(0f, 0.05f, 0f);
        lighterLight.type = LightType.Point;
        lighterLight.color = lighterColor;
        lighterLight.range = lighterRadius * 1.5f;
        lighterLight.shadows = LightShadows.None;
        lighterLight.enabled = false;
        lighterModel.gameObject.SetActive(false);
    }

    void BuildCamera(Transform hand)
    {
        cameraModel = new GameObject("Held Camera").transform;
        cameraModel.SetParent(hand, false);
        cameraModel.localPosition = new Vector3(0.18f, -0.2f, 0.4f);
        cameraModel.localRotation = Quaternion.Euler(-3f, -6f, 0f);
        var model = PropModels.FilmCamera(cameraModel, false, out cameraFlashPoint);
        model.localScale = Vector3.one * 1.3f;
        cameraModel.gameObject.SetActive(false);

        flashLight = new GameObject("Camera Flash").AddComponent<Light>();
        flashLight.transform.SetParent(hand, false);
        flashLight.type = LightType.Point;
        flashLight.color = new Color(0.95f, 0.97f, 1f);
        flashLight.range = 45f;
        flashLight.shadows = LightShadows.None;
        flashLight.enabled = false;
    }

    void BuildBattery(Transform hand)
    {
        var body = MaterialFactory.Lit(new Color(0.08f, 0.08f, 0.09f), 0.4f);
        var label = MaterialFactory.Lit(new Color(0.75f, 0.45f, 0.1f), 0.5f);
        var metal = MaterialFactory.Lit(new Color(0.6f, 0.6f, 0.62f), 0.8f);
        batteryModel = new GameObject("Held Battery").transform;
        batteryModel.SetParent(hand, false);
        batteryModel.localPosition = new Vector3(0.2f, -0.2f, 0.42f);
        batteryModel.localRotation = Quaternion.Euler(-70f, 15f, 0f);
        Part(PrimitiveType.Cylinder, body, batteryModel, Vector3.zero, new Vector3(0.034f, 0.03f, 0.034f));
        Part(PrimitiveType.Cylinder, label, batteryModel, new Vector3(0f, 0.008f, 0f), new Vector3(0.0345f, 0.016f, 0.0345f));
        Part(PrimitiveType.Cylinder, metal, batteryModel, new Vector3(0f, 0.033f, 0f), new Vector3(0.012f, 0.004f, 0.012f));
        batteryModel.gameObject.SetActive(false);
    }

    void BuildFlareGun(Transform hand)
    {
        flareGunModel = new GameObject("Held Flare Gun").transform;
        flareGunModel.SetParent(hand, false);
        flareGunModel.localPosition = new Vector3(0.22f, -0.22f, 0.45f);
        flareGunModel.localRotation = Quaternion.Euler(-4f, -4f, 0f);
        var model = PropModels.FlareGun(flareGunModel, false, out muzzle);
        model.localPosition = new Vector3(0f, -0.06f, -0.03f); // barrel where the old one was
        model.localScale = Vector3.one * 1.15f;

        flareGunModel.gameObject.SetActive(false);
    }

    static void Part(PrimitiveType type, Material mat, Transform parent, Vector3 pos, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        Destroy(go.GetComponent<Collider>());
    }

    // ---------------------------------------------------------------- HUD

    static readonly Color[] Accents =
    {
        new Color(0.95f, 0.9f, 0.7f),  // flashlight
        new Color(1f, 0.55f, 0.2f),    // torch
        new Color(1f, 0.72f, 0.35f),   // lighter
        new Color(0.95f, 0.35f, 0.1f), // flare gun
        new Color(0.85f, 1f, 0.3f),    // glowsticks
        new Color(0.45f, 0.95f, 0.5f), // batteries
        new Color(0.8f, 0.85f, 0.95f), // camera
        new Color(1f, 0.3f, 0.2f),     // key fob
    };
    static readonly Color Cream = new Color(0.93f, 0.87f, 0.74f);

    public static string NameOf(Item item) => ItemNames[(int)item];
    public static Color AccentOf(Item item) => Accents[(int)item];

    /// <summary>True while the item is burning or shining.</summary>
    public bool IsLit(Item item) =>
        (item == Item.Torch && TorchLit) || (item == Item.Lighter && LighterLit) || (item == Item.Flashlight && flashlight != null && flashlight.IsOn);

    /// <summary>How full a meter the item has (battery, fuel, shots left), or -1 if it has none.</summary>
    public float MeterOf(Item item) =>
        item == Item.Flashlight ? (flashlight != null ? flashlight.Battery : 1f) :
        item == Item.Torch ? TorchFuel :
        item == Item.Lighter ? LighterFuel :
        item == Item.Camera ? CameraShots / (float)Mathf.Max(cameraShots, 1) : -1f;

    void OnGUI()
    {
        if (GameOverScreen.IsActive) return;
        if (flashGlow > 0f) DrawRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(1f, 1f, 1f, flashGlow * flashGlow * 0.9f)); // the flash in your own eyes
        if (!showHotbar || InventoryScreen.IsOpen) return; // the inventory shows the slots itself

        float unit = Mathf.Max(0.6f, Screen.height / 1080f);
        float size = 70f * unit, gap = 8f * unit, pad = 10f * unit, margin = 26f * unit;
        float width = SlotCount * size + (SlotCount - 1) * gap;
        float x0 = (Screen.width - width) * 0.5f;
        float y = Screen.height - size - margin - pad;

        EnsureStyles(unit);

        // The tray: a dark, rounded band behind the slots with a thin gilt line along its top.
        var tray = new Rect(x0 - pad, y - pad, width + pad * 2f, size + pad * 2f);
        Rounded(new Rect(tray.x, tray.y + 3f * unit, tray.width, tray.height), new Color(0f, 0f, 0f, 0.35f), 16f * unit); // shadow
        Rounded(tray, new Color(0.03f, 0.028f, 0.025f, 0.62f), 16f * unit);
        for (int i = 0; i < 24; i++)
        {
            float f = i / 24f, edge = 1f - Mathf.Abs(f - 0.5f) * 2f;
            DrawRect(new Rect(tray.x + 18f * unit + (tray.width - 36f * unit) * f, tray.y, (tray.width - 36f * unit) / 24f + 0.5f, Mathf.Max(1f, unit)), new Color(1f, 0.72f, 0.36f, 0.55f * edge));
        }

        for (int i = 0; i < SlotCount; i++)
        {
            var slot = new Rect(x0 + i * (size + gap), y, size, size);
            bool filled = TryGetSlot(i, out Item item);
            DrawSlot(slot, i, filled, item, filled && Selected == item, unit);
        }

        // The name of what's in hand, for a moment after picking it up.
        float since = Time.unscaledTime - selectedAt;
        float nameAlpha = Mathf.Clamp01(2.2f - since) * Mathf.Clamp01(since * 6f + 0.3f);
        if (nameAlpha > 0f)
        {
            var nameRect = new Rect(0f, tray.y - 34f * unit, Screen.width, 26f * unit);
            Shadowed(nameRect, Spaced(NameOf(Selected)), nameStyle, Color.Lerp(Cream, AccentOf(Selected), 0.3f), nameAlpha);
        }

        if (messageTimer > 0f && !string.IsNullOrEmpty(message))
        {
            float alpha = Mathf.Clamp01(messageTimer);
            Shadowed(new Rect(0f, tray.y - 70f * unit, Screen.width, 30f * unit), message, messageStyle, new Color(0.95f, 0.9f, 0.8f), alpha);
        }
    }

    /// <summary>
    /// One slot: a rounded dark tile with the item's picture, its key number, how many, and a thin meter. The
    /// one in hand is lifted, framed in its colour and glowing. Also used by the inventory.
    /// </summary>
    public void DrawSlot(Rect slot, int index, bool filled, Item item, bool inHand, float unit, float alpha = 1f)
    {
        EnsureStyles(unit);
        Color accent = filled ? AccentOf(item) : Cream;
        if (inHand)
        {
            slot.y -= 6f * unit;
            for (int g = 3; g >= 1; g--) // soft glow
            {
                float grow = g * 4f * unit;
                Rounded(new Rect(slot.x - grow, slot.y - grow, slot.width + grow * 2f, slot.height + grow * 2f), new Color(accent.r, accent.g, accent.b, 0.07f * alpha), 12f * unit + grow);
            }
        }
        Rounded(slot, new Color(0.07f, 0.065f, 0.06f, (inHand ? 0.92f : 0.78f) * alpha), 11f * unit);
        Rounded(new Rect(slot.x, slot.y, slot.width, slot.height * 0.5f), new Color(1f, 1f, 1f, 0.035f * alpha), 11f * unit); // a little sheen
        Rounded(slot, inHand ? new Color(accent.r, accent.g, accent.b, 0.95f * alpha) : new Color(Cream.r, Cream.g, Cream.b, 0.16f * alpha), 11f * unit, (inHand ? 2.5f : 1.2f) * unit);

        Shadowed(new Rect(slot.x + 7f * unit, slot.y + 4f * unit, 20f * unit, 16f * unit), (index + 1).ToString(), numberStyle,
            Cream, (inHand ? 0.9f : 0.45f) * alpha, TextAnchor.UpperLeft);
        if (!filled) return;

        bool lit = IsLit(item);
        var icon = new Rect(slot.x + slot.width * 0.2f, slot.y + slot.height * 0.14f, slot.width * 0.6f, slot.width * 0.6f);
        Color tint = lit ? Color.Lerp(accent, Color.white, 0.3f) : Color.Lerp(Cream, accent, inHand ? 0.55f : 0.3f);
        DrawIcon(item, icon, tint, (inHand || lit ? 1f : 0.8f) * alpha);

        int count = Count(item);
        if (count > 1)
            Shadowed(new Rect(slot.x, slot.yMax - 22f * unit, slot.width - 7f * unit, 18f * unit), "\u00d7" + count, numberStyle, Cream, 0.95f * alpha, TextAnchor.UpperRight);

        float meter = MeterOf(item);
        if (meter >= 0f)
        {
            var bar = new Rect(slot.x + 10f * unit, slot.yMax - 7f * unit, slot.width - 20f * unit, 3f * unit);
            Rounded(bar, new Color(1f, 1f, 1f, 0.12f * alpha), bar.height * 0.5f);
            Color barColor = meter < 0.2f ? new Color(0.95f, 0.3f, 0.2f) : accent;
            Rounded(new Rect(bar.x, bar.y, Mathf.Max(bar.height, bar.width * Mathf.Clamp01(meter)), bar.height), new Color(barColor.r, barColor.g, barColor.b, 0.9f * alpha), bar.height * 0.5f);
        }
    }

    void EnsureStyles(float unit)
    {
        if (nameStyle == null)
        {
            nameStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            numberStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, fontStyle = FontStyle.Bold };
            messageStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
        }
        nameStyle.fontSize = Mathf.RoundToInt(17f * unit);
        numberStyle.fontSize = Mathf.RoundToInt(12f * unit);
        messageStyle.fontSize = Mathf.RoundToInt(17f * unit);
    }

    // ---------------------------------------------------------------- Icons

    /// <summary>A simple flat picture of an item, drawn from rounded shapes, fitted to a square.</summary>
    public static void DrawIcon(Item item, Rect r, Color c, float alpha)
    {
        Color main = new Color(c.r, c.g, c.b, alpha);
        Color dark = new Color(c.r * 0.45f, c.g * 0.45f, c.b * 0.45f, alpha);
        Color warm = new Color(1f, 0.6f, 0.2f, alpha);
        Color hot = new Color(1f, 0.92f, 0.65f, alpha);
        Matrix4x4 old = GUI.matrix;
        switch (item)
        {
            case Item.Flashlight:
                GUIUtility.RotateAroundPivot(-30f, r.center);
                Shape(r, 0.06f, 0.42f, 0.5f, 0.17f, main, 0.04f);
                Shape(r, 0.54f, 0.33f, 0.2f, 0.35f, main, 0.05f);
                Shape(r, 0.74f, 0.36f, 0.05f, 0.29f, hot, 0.02f);
                Shape(r, 0.26f, 0.385f, 0.1f, 0.05f, dark, 0.02f);
                Shape(r, 0.08f, 0.45f, 0.14f, 0.11f, dark, 0.03f);
                break;
            case Item.Torch:
                GUIUtility.RotateAroundPivot(20f, r.center);
                Shape(r, 0.45f, 0.4f, 0.1f, 0.58f, dark, 0.04f);
                Shape(r, 0.4f, 0.33f, 0.2f, 0.15f, main, 0.04f);
                Shape(r, 0.35f, 0.02f, 0.3f, 0.36f, warm, 0.15f);
                Shape(r, 0.42f, 0.13f, 0.16f, 0.2f, hot, 0.08f);
                break;
            case Item.Lighter:
                Shape(r, 0.47f, 0.06f, 0.14f, 0.26f, warm, 0.07f);
                Shape(r, 0.505f, 0.15f, 0.07f, 0.14f, hot, 0.035f);
                Shape(r, 0.32f, 0.36f, 0.36f, 0.56f, main, 0.06f);
                Shape(r, 0.32f, 0.36f, 0.36f, 0.12f, dark, 0.04f);
                Shape(r, 0.34f, 0.3f, 0.1f, 0.08f, dark, 0.04f);
                break;
            case Item.FlareGun:
                Shape(r, 0.12f, 0.3f, 0.66f, 0.2f, main, 0.05f);
                Shape(r, 0.74f, 0.27f, 0.1f, 0.26f, main, 0.03f);
                Shape(r, 0.16f, 0.44f, 0.18f, 0.4f, main, 0.05f);
                Shape(r, 0.33f, 0.48f, 0.16f, 0.17f, main, 0.07f, 0.035f);
                Shape(r, 0.8f, 0.33f, 0.04f, 0.14f, dark, 0.02f);
                break;
            case Item.Glowstick:
                GUIUtility.RotateAroundPivot(35f, r.center);
                Shape(r, 0.31f, 0.04f, 0.38f, 0.92f, new Color(c.r, c.g, c.b, 0.18f * alpha), 0.19f);
                Shape(r, 0.4f, 0.12f, 0.2f, 0.76f, main, 0.1f);
                Shape(r, 0.45f, 0.18f, 0.06f, 0.6f, new Color(1f, 1f, 1f, 0.5f * alpha), 0.03f);
                Shape(r, 0.41f, 0.08f, 0.18f, 0.09f, dark, 0.04f);
                break;
            case Item.Battery:
                GUIUtility.RotateAroundPivot(-20f, r.center);
                Shape(r, 0.14f, 0.33f, 0.62f, 0.34f, main, 0.05f);
                Shape(r, 0.76f, 0.43f, 0.08f, 0.14f, main, 0.02f);
                Shape(r, 0.14f, 0.33f, 0.22f, 0.34f, dark, 0.05f);
                Shape(r, 0.47f, 0.44f, 0.12f, 0.03f, dark, 0.01f); // a plus sign
                Shape(r, 0.515f, 0.395f, 0.03f, 0.12f, dark, 0.01f);
                break;
            case Item.Camera:
                Shape(r, 0.1f, 0.32f, 0.8f, 0.48f, main, 0.07f);
                Shape(r, 0.34f, 0.2f, 0.26f, 0.16f, main, 0.04f);
                Shape(r, 0.68f, 0.24f, 0.16f, 0.1f, hot, 0.03f);
                Shape(r, 0.33f, 0.37f, 0.34f, 0.34f, dark, 0.17f);
                Shape(r, 0.4f, 0.44f, 0.2f, 0.2f, main, 0.1f);
                Shape(r, 0.46f, 0.47f, 0.06f, 0.06f, hot, 0.03f);
                break;
            case Item.KeyFob:
                Shape(r, 0.1f, 0.06f, 0.34f, 0.34f, main, 0.17f, 0.05f);
                GUIUtility.RotateAroundPivot(-25f, r.center);
                Shape(r, 0.36f, 0.3f, 0.36f, 0.6f, main, 0.14f);
                Shape(r, 0.46f, 0.42f, 0.16f, 0.16f, new Color(0.9f, 0.18f, 0.12f, alpha), 0.08f);
                Shape(r, 0.47f, 0.66f, 0.14f, 0.1f, dark, 0.05f);
                break;
        }
        GUI.matrix = old;
    }

    /// <summary>A rounded shape in an icon's square: position and size as fractions of it.</summary>
    static void Shape(Rect r, float x, float y, float w, float h, Color c, float radius, float border = 0f)
    {
        var rect = new Rect(r.x + x * r.width, r.y + y * r.height, w * r.width, h * r.height);
        Rounded(rect, c, radius * r.width, border * r.width);
    }

    /// <summary>A filled (or, with a border width, outlined) rounded rectangle.</summary>
    public static void Rounded(Rect r, Color c, float radius, float border = 0f)
    {
        radius = Mathf.Min(radius, Mathf.Min(r.width, r.height) * 0.5f);
        GUI.DrawTexture(r, Texture2D.whiteTexture, ScaleMode.StretchToFill, true, 0f, c, border, radius);
    }

    static void Shadowed(Rect r, string text, GUIStyle style, Color c, float a, TextAnchor anchor = TextAnchor.MiddleCenter)
    {
        style.alignment = anchor;
        style.normal.textColor = new Color(0f, 0f, 0f, 0.75f * a);
        GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, style);
        style.normal.textColor = new Color(c.r, c.g, c.b, a);
        GUI.Label(r, text, style);
    }

    static string Spaced(string text)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char ch in text.ToUpperInvariant())
        {
            if (sb.Length > 0) sb.Append(ch == ' ' ? "   " : " ");
            if (ch != ' ') sb.Append(ch);
        }
        return sb.ToString();
    }

    static void DrawRect(Rect r, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }
}
