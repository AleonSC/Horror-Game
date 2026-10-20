using UnityEngine;

/// <summary>
/// The player's hotbar: 1 = flashlight, 2 = torch, 3 = lighter, 4 = flare gun, 5 = glowsticks. Press a
/// number (or scroll the mouse wheel) to hold that item, and F to use it.
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
    public enum Item { Flashlight, Torch, Lighter, FlareGun, Glowstick }
    const int SlotCount = 5;
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

    static readonly string[] ItemNames = { "Flashlight", "Torch", "Lighter", "Flare Gun", "Glowsticks" };
    Transform flareGunModel, muzzle, glowstickModel;

    void OnEnable() => SafeZones.Register(this);
    void OnDisable() => SafeZones.Unregister(this);

    void Start()
    {
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

        fireLoop = ProceduralAudio.AddSource(gameObject, ProceduralAudio.FireLoop, true, false, 0f);
        sfx = ProceduralAudio.AddSource(gameObject, null, false, false, 0.8f);
    }

    void Update()
    {
        if (PauseMenu.IsPaused) return;
        int slot = HorrorInput.SlotPressed;
        if (slot > 0) Select((Item)(slot - 1));

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

        // Only the item in hand works: putting a flame away puts it out.
        TorchLit = false;
        LighterLit = false;

        Selected = item;
        if (flashlight != null) flashlight.SetEquipped(item == Item.Flashlight);
        Play(ProceduralAudio.Pickup, 0.3f, 1.3f);
    }

    /// <summary>Steps through the hotbar to the next (or previous) slot that has something in it, wrapping round.</summary>
    void Cycle(int direction)
    {
        for (int step = 1; step < SlotCount; step++)
        {
            var item = (Item)(((int)Selected + direction * step + SlotCount * 2) % SlotCount);
            if (Count(item) > 0)
            {
                Select(item);
                return;
            }
        }
    }

    int Count(Item item) =>
        item == Item.Torch ? TorchCount :
        item == Item.Lighter ? LighterCount :
        item == Item.FlareGun ? FlareCount :
        item == Item.Glowstick ? GlowstickCount : 1;

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
            Message("Picked up a torch. Press 2 to hold it, F to light it.");
        }
        else if (item == Item.Lighter)
        {
            if (LighterCount == 0) LighterFuel = 1f;
            LighterCount++;
            Message("Picked up a lighter. Press 3 to hold it, F to flick it.");
        }
        else if (item == Item.FlareGun)
        {
            FlareCount++;
            Message("Picked up a flare gun. Press 4 to hold it, then aim up and press F to fire.");
        }
        else if (item == Item.Glowstick)
        {
            GlowstickCount += glowsticksPerBundle;
            Message($"Picked up {glowsticksPerBundle} glowsticks. Press 5 to hold them, F to crack and throw one.");
        }
    }

    /// <summary>Empties the hotbar back to just the flashlight (used when the run resets).</summary>
    public void ResetInventory()
    {
        TorchLit = LighterLit = false;
        TorchCount = LighterCount = FlareCount = GlowstickCount = 0;
        TorchFuel = LighterFuel = 1f;
        Selected = Item.Flashlight;
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
            torchFlame.rotation = Quaternion.identity; // flames burn upward however the torch is held
            torchFlame.localScale = Vector3.one * Mathf.Lerp(1f, 0.5f, low) * (0.85f + 0.3f * Mathf.PerlinNoise(time * 12f, 1.7f));
        }

        flareGunModel.gameObject.SetActive(Selected == Item.FlareGun);
        glowstickModel.gameObject.SetActive(Selected == Item.Glowstick);
        lighterModel.gameObject.SetActive(Selected == Item.Lighter);
        lighterFlame.gameObject.SetActive(LighterLit);
        lighterLight.enabled = LighterLit;
        if (LighterLit)
        {
            lighterLight.intensity = 1.4f * (0.85f + 0.3f * Mathf.PerlinNoise(time * 10f, 5.1f));
            lighterFlame.rotation = Quaternion.identity;
            lighterFlame.localScale = Vector3.one * (0.85f + 0.3f * Mathf.PerlinNoise(time * 14f, 2.9f));
        }

        float fireVolume = TorchLit ? 0.5f : LighterLit ? 0.12f : 0f;
        fireLoop.pitch = TorchLit ? 1f : 1.6f;
        fireLoop.volume = Mathf.MoveTowards(fireLoop.volume, fireVolume, Time.deltaTime * 2f);
        if (fireVolume > 0f && !fireLoop.isPlaying) fireLoop.Play();
        else if (fireLoop.volume <= 0f && fireLoop.isPlaying) fireLoop.Stop();
    }

    void BuildTorch(Transform hand)
    {
        var wood = MaterialFactory.Lit(new Color(0.22f, 0.14f, 0.08f), 0.1f);
        var cloth = MaterialFactory.Lit(new Color(0.12f, 0.1f, 0.07f), 0.05f);

        torchModel = new GameObject("Held Torch").transform;
        torchModel.SetParent(hand, false);
        torchModel.localPosition = new Vector3(0.32f, -0.42f, 0.6f);
        torchModel.localRotation = Quaternion.Euler(25f, 0f, -10f);
        Part(PrimitiveType.Cylinder, wood, torchModel, Vector3.zero, new Vector3(0.035f, 0.25f, 0.035f));
        Part(PrimitiveType.Cylinder, cloth, torchModel, new Vector3(0f, 0.27f, 0f), new Vector3(0.07f, 0.07f, 0.07f));

        torchFlame = new GameObject("Flame").transform;
        torchFlame.SetParent(torchModel, false);
        torchFlame.localPosition = new Vector3(0f, 0.36f, 0f);
        Part(PrimitiveType.Sphere, MaterialFactory.Unlit(torchColor * 4f), torchFlame, new Vector3(0f, 0.06f, 0f), new Vector3(0.12f, 0.22f, 0.12f));
        Part(PrimitiveType.Sphere, MaterialFactory.Unlit(new Color(1f, 0.85f, 0.4f) * 6f), torchFlame, new Vector3(0f, 0.04f, 0f), new Vector3(0.07f, 0.13f, 0.07f));

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
        var body = MaterialFactory.Lit(new Color(0.45f, 0.08f, 0.06f), 0.6f);
        var metal = MaterialFactory.Lit(new Color(0.6f, 0.6f, 0.62f), 0.8f);

        lighterModel = new GameObject("Held Lighter").transform;
        lighterModel.SetParent(hand, false);
        lighterModel.localPosition = new Vector3(0.2f, -0.24f, 0.42f);
        lighterModel.localRotation = Quaternion.Euler(10f, -15f, 0f);
        Part(PrimitiveType.Cube, body, lighterModel, Vector3.zero, new Vector3(0.04f, 0.06f, 0.015f));
        Part(PrimitiveType.Cube, metal, lighterModel, new Vector3(0f, 0.038f, 0f), new Vector3(0.04f, 0.016f, 0.015f));

        lighterFlame = new GameObject("Flame").transform;
        lighterFlame.SetParent(lighterModel, false);
        lighterFlame.localPosition = new Vector3(0f, 0.05f, 0f);
        Part(PrimitiveType.Sphere, MaterialFactory.Unlit(lighterColor * 4f), lighterFlame, new Vector3(0f, 0.02f, 0f), new Vector3(0.02f, 0.05f, 0.02f));
        Part(PrimitiveType.Sphere, MaterialFactory.Unlit(new Color(0.5f, 0.7f, 1f) * 3f), lighterFlame, new Vector3(0f, 0.005f, 0f), new Vector3(0.01f, 0.015f, 0.01f));

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

    void OnGUI()
    {
        if (!showHotbar || GameOverScreen.IsActive) return;

        float unit = Mathf.Max(1f, Screen.height / 1080f);
        float size = 76f * unit, gap = 10f * unit, margin = 24f * unit;
        float x0 = (Screen.width - (SlotCount * size + (SlotCount - 1) * gap)) * 0.5f;
        float y = Screen.height - size - margin;

        if (nameStyle == null)
        {
            nameStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            numberStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft };
            messageStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
        }
        nameStyle.fontSize = Mathf.RoundToInt(14f * unit);
        numberStyle.fontSize = Mathf.RoundToInt(13f * unit);
        messageStyle.fontSize = Mathf.RoundToInt(17f * unit);

        for (int i = 0; i < SlotCount; i++)
        {
            var item = (Item)i;
            var slot = new Rect(x0 + i * (size + gap), y, size, size);
            int count = item == Item.Torch ? TorchCount : item == Item.Lighter ? LighterCount : item == Item.FlareGun ? FlareCount
                : item == Item.Glowstick ? GlowstickCount : 1;
            bool owned = count > 0;
            bool selected = Selected == item;
            bool lit = (item == Item.Torch && TorchLit) || (item == Item.Lighter && LighterLit) || (item == Item.Flashlight && flashlight != null && flashlight.IsOn);

            DrawRect(slot, new Color(0f, 0f, 0f, selected ? 0.7f : 0.5f));
            Color frame = selected ? new Color(1f, 0.8f, 0.5f, 0.95f) : new Color(0.6f, 0.6f, 0.6f, 0.5f);
            DrawFrame(slot, frame, (selected ? 3f : 1f) * unit);

            Color text = !owned ? new Color(0.5f, 0.5f, 0.5f, 0.5f) : lit ? new Color(1f, 0.75f, 0.4f) : new Color(0.9f, 0.88f, 0.82f);
            numberStyle.normal.textColor = new Color(0.8f, 0.8f, 0.8f, 0.8f);
            GUI.Label(new Rect(slot.x + 5f * unit, slot.y + 3f * unit, 20f * unit, 20f * unit), (i + 1).ToString(), numberStyle);
            nameStyle.normal.textColor = text;
            GUI.Label(new Rect(slot.x, slot.y - 4f * unit, slot.width, slot.height), owned ? ItemNames[i] : "-", nameStyle);

            if (count > 1)
            {
                numberStyle.normal.textColor = text;
                GUI.Label(new Rect(slot.xMax - 26f * unit, slot.y + 3f * unit, 24f * unit, 20f * unit), "x" + count, numberStyle);
            }

            if (owned)
            {
                float fill = item == Item.Torch ? TorchFuel : item == Item.Lighter ? LighterFuel : item == Item.FlareGun || item == Item.Glowstick ? 1f : (flashlight != null ? flashlight.Battery : 1f);
                var bar = new Rect(slot.x + 8f * unit, slot.yMax - 14f * unit, slot.width - 16f * unit, 5f * unit);
                DrawRect(bar, new Color(0.15f, 0.15f, 0.15f, 0.9f));
                Color barColor = fill < 0.2f ? new Color(0.9f, 0.3f, 0.2f) : item == Item.Flashlight ? new Color(0.95f, 0.9f, 0.7f)
                    : item == Item.Glowstick ? Glowstick.GlowColor : new Color(1f, 0.6f, 0.25f);
                DrawRect(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(fill), bar.height), barColor);
            }
        }

        if (messageTimer > 0f && !string.IsNullOrEmpty(message))
        {
            float alpha = Mathf.Clamp01(messageTimer);
            messageStyle.normal.textColor = new Color(0.95f, 0.9f, 0.8f, alpha);
            GUI.Label(new Rect(0f, y - 40f * unit, Screen.width, 30f * unit), message, messageStyle);
        }
    }

    static void DrawFrame(Rect r, Color c, float t)
    {
        DrawRect(new Rect(r.x, r.y, r.width, t), c);
        DrawRect(new Rect(r.x, r.yMax - t, r.width, t), c);
        DrawRect(new Rect(r.x, r.y, t, r.height), c);
        DrawRect(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    static void DrawRect(Rect r, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }
}
