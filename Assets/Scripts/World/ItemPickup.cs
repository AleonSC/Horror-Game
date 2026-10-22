using UnityEngine;

/// <summary>
/// An item lying in the world (a torch or a lighter) that goes into the player's hotbar when clicked.
/// Shack places these on its table; LampInteractor handles the clicking.
/// </summary>
public class ItemPickup : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Inspector changes made in Play are kept)

    public static readonly System.Collections.Generic.List<ItemPickup> All = new System.Collections.Generic.List<ItemPickup>();
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() => All.Clear();

    public Hotbar.Item item;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    // (By Start the model's been built, so the glow can fit itself round it.)
    void Start()
    {
        if (GetComponent<PickupGlow>() == null) gameObject.AddComponent<PickupGlow>();
    }

    public string DisplayName =>
        item == Hotbar.Item.Torch ? "torch" :
        item == Hotbar.Item.Lighter ? "lighter" :
        item == Hotbar.Item.FlareGun ? "flare gun" :
        item == Hotbar.Item.Glowstick ? "glowsticks" :
        item == Hotbar.Item.Camera ? "camera" :
        item == Hotbar.Item.KeyFob ? "car key fob" :
        item == Hotbar.Item.Map ? "map" :
        item == Hotbar.Item.Drone ? "drone" : "item";

    /// <summary>Puts the item in the hotbar and removes it from the world.</summary>
    public void PickUp(Hotbar hotbar)
    {
        if (hotbar == null) return;
        if (!hotbar.CanTake(item)) { hotbar.NoRoom(DisplayName); return; } // (left where it is)
        hotbar.AddItem(item);
        ProceduralAudio.PlayAt(ProceduralAudio.Pickup, transform.position, 0.8f, 1f, 2f);
        Destroy(gameObject);
    }

    /// <summary>Builds a torch (PropModels.Torch) lying on its side.</summary>
    public static ItemPickup CreateTorch(Transform parent, Vector3 localPos, float yaw)
    {
        var pickup = Create("Torch Pickup", Hotbar.Item.Torch, parent, localPos, yaw);
        var model = PropModels.Torch(pickup.transform, true, out _);
        model.localRotation = Quaternion.Euler(0f, 0f, -90f); // lying along +x, the head to the right
        model.localPosition = new Vector3(-0.25f, 0.035f, 0f);
        return pickup;
    }

    /// <summary>Builds a flare gun (PropModels.FlareGun) lying on its side, label up.</summary>
    public static ItemPickup CreateFlareGun(Transform parent, Vector3 localPos, float yaw)
    {
        var pickup = Create("Flare Gun Pickup", Hotbar.Item.FlareGun, parent, localPos, yaw);
        var model = PropModels.FlareGun(pickup.transform, true, out _);
        // On its side: the gun's right flank up, the barrel resting on the ground.
        model.localRotation = Quaternion.Euler(0f, 0f, 90f);
        model.localPosition = new Vector3(0.05f, 0.022f, -0.04f);
        return pickup;
    }

    /// <summary>Builds an old film camera with a flash gun (PropModels.FilmCamera), lying flat.</summary>
    public static ItemPickup CreateCamera(Transform parent, Vector3 localPos, float yaw)
    {
        var pickup = Create("Camera Pickup", Hotbar.Item.Camera, parent, localPos, yaw);
        PropModels.FilmCamera(pickup.transform, true, out _);
        return pickup;
    }

    /// <summary>A hand-drawn map of the trails, folded in three, lying open a little.</summary>
    public static ItemPickup CreateMap(Transform parent, Vector3 localPos, float yaw)
    {
        var pickup = Create("Map Pickup", Hotbar.Item.Map, parent, localPos, yaw);
        var paper = MaterialFactory.Lit(new Color(0.78f, 0.7f, 0.54f), 0.05f);
        var ink = MaterialFactory.Lit(new Color(0.18f, 0.12f, 0.08f), 0.05f);
        for (int i = 0; i < 3; i++)
        {
            // Three panels, the outer two tilted up off the fold.
            var panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(panel.GetComponent<Collider>());
            panel.transform.SetParent(pickup.transform, false);
            float x = (i - 1) * 0.1f;
            panel.transform.localPosition = new Vector3(x, 0.004f + (i == 1 ? 0f : 0.008f), 0f);
            panel.transform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 6f : i == 2 ? -6f : 0f);
            panel.transform.localScale = new Vector3(0.1f, 0.002f, 0.21f);
            panel.GetComponent<Renderer>().sharedMaterial = paper;
        }
        for (int i = 0; i < 4; i++)
        {
            // Pencilled trails across it.
            var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.Destroy(line.GetComponent<Collider>());
            line.transform.SetParent(pickup.transform, false);
            line.transform.localPosition = new Vector3(Random.Range(-0.08f, 0.08f), 0.0065f, Random.Range(-0.08f, 0.08f));
            line.transform.localRotation = Quaternion.Euler(0f, Random.Range(0f, 180f), 0f);
            line.transform.localScale = new Vector3(0.09f, 0.0005f, 0.003f);
            line.GetComponent<Renderer>().sharedMaterial = ink;
        }
        return pickup;
    }

    /// <summary>The remote-control drone, sitting on its landing legs (DroneUnit.BuildModel).</summary>
    public static ItemPickup CreateDrone(Transform parent, Vector3 localPos, float yaw)
    {
        var pickup = Create("Drone Pickup", Hotbar.Item.Drone, parent, localPos, yaw);
        var body = new GameObject("Drone").transform;
        body.SetParent(pickup.transform, false);
        body.localPosition = Vector3.up * 0.09f;
        DroneUnit.BuildModel(body, null, out Light lamp);
        lamp.enabled = false;
        return pickup;
    }

    /// <summary>Builds a car key fob on its ring (PropModels.KeyFob).</summary>
    public static ItemPickup CreateKeyFob(Transform parent, Vector3 localPos, float yaw)
    {
        var pickup = Create("Key Fob Pickup", Hotbar.Item.KeyFob, parent, localPos, yaw);
        PropModels.KeyFob(pickup.transform, true, out Renderer led).localScale = Vector3.one * 1.4f; // (a bit bigger, to be found)
        pickup.gameObject.AddComponent<FobBeacon>().led = led;
        return pickup;
    }

    /// <summary>Builds a bundle of yellow glowsticks held together with a band.</summary>
    public static ItemPickup CreateGlowsticks(Transform parent, Vector3 localPos, float yaw)
    {
        var pickup = Create("Glowsticks Pickup", Hotbar.Item.Glowstick, parent, localPos, yaw);
        var yellow = MaterialFactory.Lit(Glowstick.GlowColor * 0.6f, 0.6f);
        var band = MaterialFactory.Lit(new Color(0.08f, 0.08f, 0.08f), 0.2f);
        for (int i = 0; i < 5; i++)
        {
            float x = (i - 2) * 0.024f, y = 0.012f + (i % 2) * 0.016f;
            Visual(PrimitiveType.Capsule, yellow, pickup.transform, new Vector3(x, y, 0f), new Vector3(0.022f, 0.08f, 0.022f), new Vector3(90f, 0f, 0f));
        }
        Visual(PrimitiveType.Cube, band, pickup.transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.13f, 0.045f, 0.02f), Vector3.zero);
        return pickup;
    }

    /// <summary>Builds a lighter (PropModels.Lighter) standing shut.</summary>
    public static ItemPickup CreateLighter(Transform parent, Vector3 localPos, float yaw)
    {
        var pickup = Create("Lighter Pickup", Hotbar.Item.Lighter, parent, localPos, yaw);
        PropModels.Lighter(pickup.transform, true, out _, out _);
        return pickup;
    }

    static ItemPickup Create(string name, Hotbar.Item item, Transform parent, Vector3 localPos, float yaw)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var pickup = go.AddComponent<ItemPickup>();
        pickup.item = item;

        // Generous invisible box so small items are easy to click.
        var aim = go.AddComponent<BoxCollider>();
        aim.isTrigger = true;
        aim.center = new Vector3(0.05f, 0.15f, 0f);
        aim.size = new Vector3(0.65f, 0.35f, 0.5f);
        return pickup;
    }

    static void Visual(PrimitiveType type, Material mat, Transform parent, Vector3 pos, Vector3 scale, Vector3 euler)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        var col = go.GetComponent<Collider>();
        if (Application.isPlaying) Destroy(col);
        else DestroyImmediate(col);
    }
}

/// <summary>
/// Makes a dropped key fob findable: its little red LED blinks every couple of seconds, bright enough to see
/// through the dark and the fog and throwing a faint red glow on the ground round it, and every so often,
/// when you're near, it gives a tiny electronic double beep (a low battery warning) you can follow.
/// </summary>
public class FobBeacon : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Inspector changes made in Play are kept)

    public Renderer led;
    public float blinkEvery = 2f, blinkLength = 0.18f;
    [Tooltip("It beeps every so often when the player is within this distance.")]
    public float beepRange = 18f;
    public float beepEvery = 9f;
    [Range(0f, 1f)] public float beepVolume = 0.25f;

    Material on, off;
    Light glow;
    AudioSource source;
    float clock, beepTimer;
    Transform player;

    void Start()
    {
        clock = Random.Range(0f, blinkEvery);
        beepTimer = Random.Range(2f, beepEvery);
        if (led != null)
        {
            off = led.sharedMaterial;
            on = MaterialFactory.NoFog(new Color(1f, 0.08f, 0.04f) * 4f); // ignores fog: a red pinprick across the camp
        }
        glow = new GameObject("Fob Glow").AddComponent<Light>();
        glow.transform.SetParent(led != null ? led.transform : transform, false);
        glow.transform.position = (led != null ? led.transform.position : transform.position) + Vector3.up * 0.05f;
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.1f, 0.05f);
        glow.range = 1.6f;
        glow.intensity = 0f;
        glow.shadows = LightShadows.None;
        source = ProceduralAudio.AddSource(gameObject, ProceduralAudio.FobClick, false, true, beepVolume, 1.5f, beepRange);
    }

    void Update()
    {
        clock += Time.deltaTime;
        bool lit = Mathf.Repeat(clock, blinkEvery) < blinkLength;
        if (led != null && on != null) led.sharedMaterial = lit ? on : off;
        if (glow != null) glow.intensity = lit ? 1.2f : 0f;

        if (player == null)
        {
            var controller = FindAnyObjectByType<FirstPersonController>();
            if (controller != null) player = controller.transform;
        }
        beepTimer -= Time.deltaTime;
        if (beepTimer <= 0f)
        {
            beepTimer = beepEvery * Random.Range(0.8f, 1.2f);
            if (player != null && (player.position - transform.position).sqrMagnitude < beepRange * beepRange && source != null)
            {
                // Two tiny high beeps.
                source.pitch = 2.2f;
                source.PlayOneShot(ProceduralAudio.FobClick, beepVolume);
                Invoke(nameof(SecondBeep), 0.14f);
            }
        }
    }

    void SecondBeep()
    {
        if (source != null) source.PlayOneShot(ProceduralAudio.FobClick, beepVolume);
    }

    void OnDestroy()
    {
        if (on != null) Destroy(on);
    }
}

