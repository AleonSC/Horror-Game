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
/// - Drone: in the cabin's store room. F sets it down on the ground ahead, and its slot becomes the remote:
///   F with the remote flies it from wherever you are (DroneUnit). Picking it back up makes it the drone again.
///
/// Torches and lighters are found in the abandoned shacks; picking up another one stacks it.
/// Only the item in your hand works, so putting the torch or lighter away puts it out.
/// LampSpawner adds this to the player automatically.
/// </summary>
public class Hotbar : MonoBehaviour, SafeZones.ISource
{
    void Awake() => Tuning.Apply(this); // saved tuning (Save Tuning on the component's menu)

    public enum Item { Flashlight, Torch, Lighter, FlareGun, Glowstick, Battery, Camera, KeyFob, Map, Drone, Remote, Stick }
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
    /// <summary>The map from the top of the watchtower: hold it to look at it.</summary>
    public bool HasMap { get; private set; }
    /// <summary>Carrying the drone (to set down somewhere)...</summary>
    public bool HasDrone { get; private set; }
    /// <summary>...or, once it's set down, holding its remote.</summary>
    public bool HasRemote { get; private set; }
    /// <summary>The stick of truth (dev tools): spawns things where you click, and lets you fly.</summary>
    public bool HasStick { get; private set; }
    /// <summary>True while the stick's spawn menu is open (the mouse is free).</summary>
    public static bool StickMenuOpen { get; private set; }
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

    static readonly string[] ItemNames = { "Flashlight", "Torch", "Lighter", "Flare Gun", "Glowsticks", "Batteries", "Camera", "Key Fob", "Map", "Drone", "Remote", "Stick of Truth" };
    Transform flareGunModel, muzzle, glowstickModel, batteryModel, cameraModel, cameraFlashPoint, cameraPrintSlot, fobModel, droneModel, remoteModel, stickModel, remoteDot, remoteYou;
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
        BuildDrone(hand);
        BuildStick(hand);

        fireLoop = ProceduralAudio.AddSource(gameObject, ProceduralAudio.FireLoop, true, false, 0f);
        sfx = ProceduralAudio.AddSource(gameObject, null, false, false, 0.8f);
    }

    void Update()
    {
        SyncSlots();
        if (PauseMenu.IsPaused || InventoryScreen.IsOpen || MainMenu.Showing) return;
        UpdateStick();
        if (DroneUnit.Piloting)
        {
            // Flying the drone: the keys and the mouse are its (F lets go, which it handles itself).
            BurnFuel(Time.deltaTime);
            UpdateVisuals();
            messageTimer -= Time.deltaTime;
            return;
        }
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
        if (item == Item.Map && !HasMap) { Message("You don't have a map"); return; }
        if (item == Item.Drone && !HasDrone) { Message("You don't have the drone"); return; }
        if (item == Item.Remote && !HasRemote) { Message("You don't have the remote"); return; }
        if (item == Item.Stick && !HasStick) return;

        // Only the item in hand works: putting a flame away puts it out.
        TorchLit = false;
        LighterLit = false;

        Selected = item;
        selectedAt = Time.unscaledTime;
        if (flashlight != null) flashlight.SetEquipped(item == Item.Flashlight); // (silently: no sound for switching)
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
        item == Item.KeyFob ? (HasKeyFob ? 1 : 0) :
        item == Item.Map ? (HasMap ? 1 : 0) :
        item == Item.Drone ? (HasDrone ? 1 : 0) :
        item == Item.Remote ? (HasRemote ? 1 : 0) :
        item == Item.Stick ? (HasStick ? 1 : 0) : 1;

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
        else if (Selected == Item.Drone)
        {
            SetDownDrone();
        }
        else if (Selected == Item.Remote)
        {
            if (DroneUnit.Deployed == null) Message("The drone's gone");
            else DroneUnit.Deployed.StartPiloting(GetComponent<FirstPersonController>());
        }
    }

    /// <summary>Sets the drone down on the ground a couple of metres ahead; its slot becomes the remote.</summary>
    void SetDownDrone()
    {
        if (!HasDrone || !TryDroneSpot(out Vector3 spot, out float facing))
        {
            if (HasDrone) Message("Look at clear ground nearby to set the drone down");
            return;
        }
        DroneUnit.Place(spot, facing);
        Play(ProceduralAudio.Pickup, 0.6f, 0.8f);
        HasDrone = false;
        HasRemote = true;
        SwapItem(Item.Drone, Item.Remote);
        Selected = Item.Flashlight; // (so selecting the remote below counts as a change)
        Select(Item.Remote);
        Message("Drone set down. With the remote in hand, F flies it from wherever you are.");
    }

    /// <summary>
    /// Where the drone would go: ground you're looking at, close by, flat enough, with room round it (not in a
    /// trunk, a wall, a rock). False if there's nowhere like that where you're looking.
    /// </summary>
    bool TryDroneSpot(out Vector3 spot, out float facing)
    {
        spot = Vector3.zero;
        facing = 0f;
        var cam = Camera.main;
        if (cam == null) return false;
        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.01f ? forward.normalized : transform.forward;
        facing = Quaternion.LookRotation(forward).eulerAngles.y;
        if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, 4f, ~0, QueryTriggerInteraction.Ignore)) return false;
        if (hit.normal.y < 0.7f) return false; // too steep (or a wall)
        spot = hit.point;
        // Room for it: nothing solid in a box its size, just above where it'd sit.
        var mask = ~0;
        if (Physics.CheckBox(spot + Vector3.up * 0.3f, new Vector3(0.25f, 0.12f, 0.25f), Quaternion.Euler(0f, facing, 0f), mask, QueryTriggerInteraction.Ignore))
            return false;
        // Not where you're standing.
        Vector3 off = spot - transform.position;
        off.y = 0f;
        return off.magnitude > 0.5f;
    }

    Transform droneGhost;

    /// <summary>Holding the drone: a see-through drone shows where it'd go, if there's somewhere it can.</summary>
    void UpdateDroneGhost()
    {
        bool holding = Selected == Item.Drone && HasDrone && !DroneUnit.Piloting;
        Vector3 spot = Vector3.zero;
        float facing = 0f;
        bool valid = holding && TryDroneSpot(out spot, out facing);
        droneGhostValid = valid;
        if (!valid)
        {
            if (droneGhost != null) droneGhost.gameObject.SetActive(false);
            return;
        }
        if (droneGhost == null)
        {
            droneGhost = new GameObject("Drone Ghost").transform;
            var body = new GameObject("Body").transform;
            body.SetParent(droneGhost, false);
            body.localPosition = Vector3.up * 0.09f;
            DroneUnit.BuildModel(body, null, out Light lamp);
            Destroy(lamp.gameObject);
            var ghostMat = MaterialFactory.LitTransparent(Texture2D.whiteTexture, new Color(0.6f, 0.9f, 1f, 0.28f), 0.6f);
            foreach (var r in droneGhost.GetComponentsInChildren<Renderer>()) r.sharedMaterial = ghostMat;
        }
        droneGhost.gameObject.SetActive(true);
        droneGhost.SetPositionAndRotation(spot + Vector3.up * (0.01f + 0.015f * Mathf.Sin(Time.time * 4f)), Quaternion.Euler(0f, facing, 0f));
    }
    bool droneGhostValid;

    /// <summary>
    /// The remote's little screen tracks the drone: a dot where it is from you (ahead is up the screen),
    /// nearer the edge the further off it is, blinking; pinned to the edge, past what the screen covers.
    /// </summary>
    void UpdateRemoteScreen()
    {
        if (remoteDot == null) return;
        var drone = DroneUnit.Deployed;
        var cam = Camera.main;
        bool show = drone != null && cam != null && Mathf.Repeat(Time.time, 0.8f) < 0.6f;
        remoteDot.gameObject.SetActive(show);
        if (!show) return;
        Vector3 you = cam.transform.position;
        Vector3 to = WorldWrap.NearestImage(drone.transform.position, you) - you;
        to.y = 0f;
        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f) forward = transform.forward;
        Vector3 local = Quaternion.Inverse(Quaternion.LookRotation(forward.normalized)) * to;
        float dist = local.magnitude;
        Vector2 dir = dist > 0.01f ? new Vector2(local.x, local.z) / dist : Vector2.zero;
        float reach = dist / (dist + 25f); // close in near the middle, the far end squeezed toward the edge
        const float halfW = 0.027f, halfH = 0.0155f;
        Vector2 spot = dir * reach;
        spot = new Vector2(spot.x * halfW, spot.y * halfH);
        remoteDot.localPosition = new Vector3(spot.x, 0.0149f, 0.022f + spot.y);
    }

    /// <summary>Puts one item in another's slot (the drone becoming its remote, and back).</summary>
    void SwapItem(Item from, Item to)
    {
        int i = SlotOf(from);
        if (i >= 0 && SlotOf(to) < 0) slots[i] = (int)to;
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
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics()
    {
        StaticReset.Drop(ref fobLedOn);
        fobLedOff = null; // (the fob model's own material, not ours to destroy)
        StickMenuOpen = false;
    }

    // ---------------------------------------------------------------- The stick of truth (dev tools)

    /// <summary>Gives the player the stick of truth, or takes it away.</summary>
    public void SetStick(bool has)
    {
        if (has == HasStick) return;
        HasStick = has;
        if (has)
        {
            SyncSlots();
            Select(Item.Stick);
            Message("The stick of truth. Right click: pick what to spawn. Left click: spawn it there. Double tap Space: fly.");
        }
        else
        {
            if (Selected == Item.Stick) Select(Item.Flashlight);
            CloseStickMenu();
            var p = GetComponent<FirstPersonController>();
            if (p != null) p.FreeFlight = false;
        }
    }

    static readonly string[] StickItems = { "Torch", "Lighter", "Flare Gun", "Glowsticks", "Battery", "Camera", "Key Fob", "Map", "Drone" };
    static readonly string[] StickMobs = { "Scour", "Blind One", "Crawler", "Flier" };
    int stickChoice; // an index into StickItems, then StickMobs after them
    float lastSpaceTap = -10f;
    Vector2 stickScroll;

    string StickChoiceName => stickChoice < StickItems.Length ? StickItems[stickChoice] : StickMobs[stickChoice - StickItems.Length];

    void UpdateStick()
    {
        var player = GetComponent<FirstPersonController>();
        bool holding = HasStick && Selected == Item.Stick && !DroneUnit.Piloting;
        if (!holding)
        {
            if (StickMenuOpen) CloseStickMenu();
            if (player != null && player.FreeFlight) player.FreeFlight = false; // (only while it's in hand)
            return;
        }
        // Double tap Space: free flight on or off.
        if (HorrorInput.JumpPressed && !StickMenuOpen)
        {
            if (Time.unscaledTime - lastSpaceTap < 0.3f)
            {
                if (player != null)
                {
                    player.FreeFlight = !player.FreeFlight;
                    Message(player.FreeFlight ? "Free flight: on (Space up, Ctrl down, Shift fast)" : "Free flight: off");
                }
                lastSpaceTap = -10f;
            }
            else lastSpaceTap = Time.unscaledTime;
        }
        if (HorrorInput.RightClickPressed)
        {
            if (StickMenuOpen) CloseStickMenu();
            else
            {
                StickMenuOpen = true;
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
        else if (HorrorInput.ClickPressed && !StickMenuOpen && !DevTools.IsOpen && Cursor.lockState == CursorLockMode.Locked)
            StickSpawn();
    }

    void CloseStickMenu()
    {
        if (!StickMenuOpen) return;
        StickMenuOpen = false;
        if (!DevTools.IsOpen && !PauseMenu.IsPaused)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    /// <summary>Spawns the chosen thing where the stick is pointing.</summary>
    void StickSpawn()
    {
        var cam = Camera.main;
        if (cam == null) return;
        if (!Physics.Raycast(cam.transform.position, cam.transform.forward, out var hit, 300f, ~0, QueryTriggerInteraction.Ignore))
        {
            Message("Nothing there to put it on");
            return;
        }
        Vector3 at = hit.point;
        float yaw = Random.Range(0f, 360f);
        Transform made = null;
        switch (StickChoiceName)
        {
            case "Torch": made = ItemPickup.CreateTorch(null, at, yaw).transform; break;
            case "Lighter": made = ItemPickup.CreateLighter(null, at, yaw).transform; break;
            case "Flare Gun": made = ItemPickup.CreateFlareGun(null, at, yaw).transform; break;
            case "Glowsticks": made = ItemPickup.CreateGlowsticks(null, at, yaw).transform; break;
            case "Battery": made = BatterySpawner.Create(null, at, yaw).transform; break;
            case "Camera": made = ItemPickup.CreateCamera(null, at, yaw).transform; break;
            case "Key Fob": made = ItemPickup.CreateKeyFob(null, at, yaw).transform; break;
            case "Map": made = ItemPickup.CreateMap(null, at, yaw).transform; break;
            case "Drone": made = ItemPickup.CreateDrone(null, at, yaw).transform; break;
            case "Scour": ShadowEntity.SpawnAt(at); break;
            case "Blind One": BlindEntity.SpawnAt(at); break;
            case "Crawler":
                var crawler = new GameObject("Crawler").AddComponent<Crawler>();
                crawler.Settle(at);
                break;
            case "Flier": FlyingEntity.Spawn(GetComponent<FirstPersonController>()); break;
        }
        if (made != null) WorldWrap.RegisterDynamic(made);
        Play(ProceduralAudio.LampSpark, 0.4f, 1.6f);
        Message($"Spawned: {StickChoiceName}");
    }

    /// <summary>The stick's menu: everything it can spawn, items on the left, creatures on the right.</summary>
    void DrawStickMenu(float unit)
    {
        float w = 460f * unit, h = 420f * unit;
        var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
        GUI.depth = -950;
        DrawRect(panel, new Color(0.02f, 0.02f, 0.02f, 0.9f));
        var title = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(20f * unit) };
        title.normal.textColor = new Color(1f, 0.8f, 0.45f);
        var head = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(15f * unit) };
        head.normal.textColor = new Color(0.9f, 0.86f, 0.78f);
        var button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(15f * unit) };
        GUI.Label(new Rect(panel.x, panel.y + 8f * unit, w, 30f * unit), "STICK OF TRUTH", title);
        float colW = (w - 48f * unit) * 0.5f, top = panel.y + 48f * unit, rowH = 32f * unit;
        GUI.Label(new Rect(panel.x + 16f * unit, top, colW, 24f * unit), "Items", head);
        GUI.Label(new Rect(panel.x + 32f * unit + colW, top, colW, 24f * unit), "Mobs", head);
        for (int i = 0; i < StickItems.Length + StickMobs.Length; i++)
        {
            bool mob = i >= StickItems.Length;
            int row = mob ? i - StickItems.Length : i;
            var r = new Rect(panel.x + (mob ? 32f * unit + colW : 16f * unit), top + 28f * unit + row * (rowH + 4f * unit), colW, rowH);
            GUI.color = stickChoice == i ? new Color(1f, 0.8f, 0.45f) : Color.white;
            if (GUI.Button(r, mob ? StickMobs[row] : StickItems[row], button))
            {
                stickChoice = i;
                CloseStickMenu();
            }
        }
        GUI.color = Color.white;
        GUI.Label(new Rect(panel.x, panel.yMax - 30f * unit, w, 24f * unit), "Pick one, then left click to spawn it. Right click to close.", new GUIStyle(head) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Italic, fontSize = Mathf.RoundToInt(12f * unit) });
    }

    /// <summary>A gnarled stick held out, its tip glowing faintly.</summary>
    void BuildStick(Transform hand)
    {
        stickModel = new GameObject("Held Stick").transform;
        stickModel.SetParent(hand, false);
        stickModel.localPosition = new Vector3(0.2f, -0.25f, 0.45f);
        stickModel.localRotation = Quaternion.Euler(55f, 0f, -15f);
        var wood = MaterialFactory.LitTextured(ProceduralTextures.Bark, ProceduralTextures.BarkNormal, new Color(0.55f, 0.42f, 0.3f), 0.05f);
        var tip = MaterialFactory.NoFog(new Color(1f, 0.85f, 0.4f) * 2.5f);
        HeldPart(PrimitiveType.Cylinder, wood, stickModel, new Vector3(0f, 0.15f, 0f), new Vector3(0.025f, 0.3f, 0.025f), Vector3.zero);
        HeldPart(PrimitiveType.Cylinder, wood, stickModel, new Vector3(0.02f, 0.3f, 0f), new Vector3(0.015f, 0.06f, 0.015f), new Vector3(0f, 0f, -30f)); // a twig off it
        HeldPart(PrimitiveType.Sphere, tip, stickModel, new Vector3(0f, 0.46f, 0f), Vector3.one * 0.03f, Vector3.zero);
        stickModel.gameObject.SetActive(false);
    }

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
        Polaroid.Take(cam, cameraPrintSlot); // a print of the shot slides out, and drifts down

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

    /// <summary>
    /// Uses the lighter on something else (a torch on a wall or at a camp): true if you have one with fuel
    /// left, spending that much of it.
    /// </summary>
    public bool UseLighterFlame(float cost)
    {
        if (LighterCount <= 0 || LighterFuel <= 0f) return false;
        SpendLighter(cost);
        return true;
    }

    /// <summary>Shows a short message above the hotbar.</summary>
    public void Notify(string text) => Message(text);

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
        else if (item == Item.Drone)
        {
            bool wasRemote = HasRemote;
            HasDrone = true;
            HasRemote = false;
            SwapItem(Item.Remote, Item.Drone);
            if (Selected == Item.Remote) Select(Item.Flashlight);
            Message(wasRemote ? "Picked the drone back up." : $"Picked up a drone. Press {Key(Item.Drone)} to hold it, F to set it down where you want to fly it from.");
        }
        else if (item == Item.Map)
        {
            HasMap = true;
            Message($"Picked up a map of the trails. Press {Key(Item.Map)} to look at it.");
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
        HasMap = false;
        HasDrone = HasRemote = false;
        DroneUnit.ClearAll();
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
        messageAt = Time.unscaledTime;
    }

    float messageAt = -10f;
    [Tooltip("Seconds the hotbar stays up after you switch items (or pick something up) before it fades away.")]
    public float hotbarShowTime = 2.5f;

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
        droneModel.gameObject.SetActive(Selected == Item.Drone);
        remoteModel.gameObject.SetActive(Selected == Item.Remote);
        if (Selected == Item.Remote) UpdateRemoteScreen();
        UpdateDroneGhost();
        stickModel.gameObject.SetActive(Selected == Item.Stick);
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

    /// <summary>The drone held out in front of you, and its remote held in both hands.</summary>
    void BuildDrone(Transform hand)
    {
        droneModel = new GameObject("Held Drone").transform;
        droneModel.SetParent(hand, false);
        droneModel.localPosition = new Vector3(0.05f, -0.26f, 0.55f);
        droneModel.localRotation = Quaternion.Euler(10f, -10f, 0f);
        DroneUnit.BuildModel(droneModel, null, out Light lamp);
        lamp.enabled = false;
        droneModel.gameObject.SetActive(false);

        remoteModel = new GameObject("Held Remote").transform;
        remoteModel.SetParent(hand, false);
        remoteModel.localPosition = new Vector3(0f, -0.22f, 0.36f);
        remoteModel.localRotation = Quaternion.Euler(-40f, 0f, 0f);
        var casing = MaterialFactory.Lit(new Color(0.12f, 0.12f, 0.13f), 0.35f);
        var grip = MaterialFactory.Lit(new Color(0.05f, 0.05f, 0.05f), 0.15f);
        var screen = MaterialFactory.NoFog(new Color(0.2f, 0.45f, 0.4f) * 0.8f);
        HeldPart(PrimitiveType.Cube, casing, remoteModel, Vector3.zero, new Vector3(0.18f, 0.025f, 0.09f), Vector3.zero);
        foreach (float x in new[] { -0.075f, 0.075f })
        {
            HeldPart(PrimitiveType.Sphere, grip, remoteModel, new Vector3(x, -0.01f, -0.03f), new Vector3(0.05f, 0.04f, 0.08f), Vector3.zero); // the grips
            HeldPart(PrimitiveType.Cylinder, grip, remoteModel, new Vector3(x * 0.6f, 0.02f, 0.0f), new Vector3(0.012f, 0.012f, 0.012f), Vector3.zero); // the sticks
            HeldPart(PrimitiveType.Sphere, grip, remoteModel, new Vector3(x * 0.6f, 0.032f, 0.0f), Vector3.one * 0.016f, Vector3.zero);
        }
        HeldPart(PrimitiveType.Cube, screen, remoteModel, new Vector3(0f, 0.0135f, 0.022f), new Vector3(0.06f, 0.002f, 0.035f), Vector3.zero);
        // On the screen: you, a faint mark in the middle, and the drone, a bright dot where it is from you.
        remoteYou = new GameObject("Screen You").transform;
        remoteYou.SetParent(remoteModel, false);
        remoteYou.localPosition = new Vector3(0f, 0.0148f, 0.022f);
        HeldPart(PrimitiveType.Cube, MaterialFactory.NoFog(new Color(0.5f, 0.9f, 0.8f) * 0.9f), remoteYou, Vector3.zero, new Vector3(0.003f, 0.0005f, 0.003f), Vector3.zero);
        remoteDot = new GameObject("Screen Drone").transform;
        remoteDot.SetParent(remoteModel, false);
        HeldPart(PrimitiveType.Cube, MaterialFactory.NoFog(new Color(1f, 0.2f, 0.12f) * 3f), remoteDot, Vector3.zero, new Vector3(0.004f, 0.0006f, 0.004f), Vector3.zero);
        HeldPart(PrimitiveType.Cylinder, grip, remoteModel, new Vector3(0.06f, 0.01f, 0.07f), new Vector3(0.006f, 0.05f, 0.006f), new Vector3(70f, 0f, 0f)); // the aerial
        remoteModel.gameObject.SetActive(false);
    }

    static void HeldPart(PrimitiveType type, Material mat, Transform parent, Vector3 pos, Vector3 scale, Vector3 euler)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void BuildCamera(Transform hand)
    {
        cameraModel = new GameObject("Held Camera").transform;
        cameraModel.SetParent(hand, false);
        cameraModel.localPosition = new Vector3(0.18f, -0.2f, 0.4f);
        cameraModel.localRotation = Quaternion.Euler(-3f, -6f, 0f);
        var model = PropModels.FilmCamera(cameraModel, false, out cameraFlashPoint, out cameraPrintSlot);
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
        new Color(0.88f, 0.78f, 0.58f), // map
        new Color(0.7f, 0.85f, 0.95f),  // drone
        new Color(0.55f, 0.95f, 0.75f), // remote
        new Color(1f, 0.8f, 0.45f),     // stick
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
        if (GameOverScreen.IsActive || MainMenu.Showing) return;
        if (flashGlow > 0f) DrawRect(new Rect(0f, 0f, Screen.width, Screen.height), new Color(1f, 1f, 1f, flashGlow * flashGlow * 0.9f)); // the flash in your own eyes
        // Holding the map: it's up in front of you.
        if (Selected == Item.Map && HasMap && !InventoryScreen.IsOpen) TrailMap.Draw(Mathf.Clamp01((Time.unscaledTime - selectedAt) / 0.25f));
        if (StickMenuOpen && !InventoryScreen.IsOpen) DrawStickMenu(Mathf.Max(0.6f, Screen.height / 1080f));
        // Holding the drone over somewhere it can go: how to set it down.
        if (droneGhostValid && !InventoryScreen.IsOpen && !PauseMenu.IsPaused)
        {
            float u = Mathf.Max(0.6f, Screen.height / 1080f);
            EnsureStyles(u);
            Shadowed(new Rect(0f, Screen.height * 0.58f, Screen.width, 30f * u), "F  —  set the drone down here", messageStyle, new Color(0.75f, 0.92f, 1f), 0.95f);
        }
        if (!showHotbar || InventoryScreen.IsOpen) return; // the inventory shows the slots itself

        // It shows while you're switching items (or just picked something up), then fades away.
        float idle = Time.unscaledTime - Mathf.Max(selectedAt, messageAt);
        float shown = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(hotbarShowTime, hotbarShowTime + 0.8f, idle));

        float unit = Mathf.Max(0.6f, Screen.height / 1080f);
        float size = 70f * unit, gap = 8f * unit, pad = 10f * unit, margin = 26f * unit;
        float width = SlotCount * size + (SlotCount - 1) * gap;
        float x0 = (Screen.width - width) * 0.5f;
        float y = Screen.height - size - margin - pad;

        EnsureStyles(unit);

        // The tray: a dark, rounded band behind the slots with a thin gilt line along its top.
        var tray = new Rect(x0 - pad, y - pad, width + pad * 2f, size + pad * 2f);
        if (shown > 0.002f)
        {
            Rounded(new Rect(tray.x, tray.y + 3f * unit, tray.width, tray.height), new Color(0f, 0f, 0f, 0.35f * shown), 16f * unit); // shadow
            Rounded(tray, new Color(0.03f, 0.028f, 0.025f, 0.62f * shown), 16f * unit);
            for (int i = 0; i < 24; i++)
            {
                float f = i / 24f, edge = 1f - Mathf.Abs(f - 0.5f) * 2f;
                DrawRect(new Rect(tray.x + 18f * unit + (tray.width - 36f * unit) * f, tray.y, (tray.width - 36f * unit) / 24f + 0.5f, Mathf.Max(1f, unit)), new Color(1f, 0.72f, 0.36f, 0.55f * edge * shown));
            }

            for (int i = 0; i < SlotCount; i++)
            {
                var slot = new Rect(x0 + i * (size + gap), y, size, size);
                bool filled = TryGetSlot(i, out Item item);
                DrawSlot(slot, i, filled, item, filled && Selected == item, unit, shown);
            }
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
            case Item.Map:
                // A folded sheet: three panels, a trail wandering across it, a dot for a lamp.
                Shape(r, 0.12f, 0.2f, 0.25f, 0.6f, main, 0.02f);
                Shape(r, 0.37f, 0.24f, 0.25f, 0.56f, new Color(c.r * 0.8f, c.g * 0.8f, c.b * 0.8f, alpha), 0.02f);
                Shape(r, 0.62f, 0.2f, 0.25f, 0.6f, main, 0.02f);
                Shape(r, 0.2f, 0.55f, 0.3f, 0.05f, dark, 0.02f);
                Shape(r, 0.45f, 0.42f, 0.3f, 0.05f, dark, 0.02f);
                Shape(r, 0.68f, 0.33f, 0.1f, 0.1f, warm, 0.05f);
                break;
            case Item.Drone:
                // From above: a body, four arms in an X, a propeller disc at each end.
                GUIUtility.RotateAroundPivot(45f, r.center);
                Shape(r, 0.15f, 0.46f, 0.7f, 0.08f, dark, 0.03f);
                Shape(r, 0.46f, 0.15f, 0.08f, 0.7f, dark, 0.03f);
                GUI.matrix = old;
                foreach (var (px, py) in new[] { (0.12f, 0.12f), (0.6f, 0.12f), (0.12f, 0.6f), (0.6f, 0.6f) })
                    Shape(r, px, py, 0.28f, 0.28f, main, 0.14f, 0.035f);
                Shape(r, 0.37f, 0.33f, 0.26f, 0.34f, main, 0.1f);
                Shape(r, 0.46f, 0.6f, 0.08f, 0.06f, hot, 0.03f);
                break;
            case Item.Remote:
                // A controller: the case, two stick knobs, the aerial, a little screen.
                Shape(r, 0.62f, 0.08f, 0.05f, 0.3f, dark, 0.02f);
                Shape(r, 0.1f, 0.34f, 0.8f, 0.36f, main, 0.1f);
                Shape(r, 0.08f, 0.5f, 0.24f, 0.32f, main, 0.12f);
                Shape(r, 0.68f, 0.5f, 0.24f, 0.32f, main, 0.12f);
                Shape(r, 0.22f, 0.42f, 0.14f, 0.14f, dark, 0.07f);
                Shape(r, 0.64f, 0.42f, 0.14f, 0.14f, dark, 0.07f);
                Shape(r, 0.42f, 0.4f, 0.16f, 0.1f, new Color(0.3f, 0.8f, 0.7f, alpha), 0.02f);
                break;
            case Item.Stick:
                GUIUtility.RotateAroundPivot(40f, r.center);
                Shape(r, 0.45f, 0.18f, 0.1f, 0.74f, dark, 0.05f);
                Shape(r, 0.5f, 0.36f, 0.18f, 0.06f, dark, 0.03f);
                Shape(r, 0.4f, 0.04f, 0.2f, 0.2f, hot, 0.1f);
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
