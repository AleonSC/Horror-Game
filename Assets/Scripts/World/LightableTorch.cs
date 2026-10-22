using UnityEngine;

/// <summary>
/// A fire fixed in place, unlit: a torch in an iron bracket on the inside wall of a shack, or a camp's
/// campfire. Click it with a lighter in your pocket and it catches (using a
/// little of the lighter's fuel): real fire, warm flickering light, the crackle of it, and a circle round it
/// the entities won't come into, like a lamp's. It burns for burnTime, then gutters out for good (until
/// the run restarts). Its light casts shadows, so it doesn't shine through walls. Campsite and Shack build
/// these; LampInteractor clicks them.
/// </summary>
public class LightableTorch : MonoBehaviour, SafeZones.ISource
{
    void Awake() => Tuning.Apply(this); // saved tuning (Inspector changes made in Play are kept)

    [Tooltip("Seconds it burns once lit, before it's spent.")]
    public float burnTime = 180f;
    [Tooltip("Radius of the circle of safety round it while it burns.")]
    public float safeRadius = 4.5f;
    [Range(0f, 1f), Tooltip("Share of the lighter's fuel it takes to light it.")]
    public float lightingCost = 0.05f;
    public Color fireColor = new Color(1f, 0.55f, 0.18f);
    [Tooltip("What it's called in the prompts (\"Light the ...\").")]
    public string noun = "torch";

    public bool IsLit { get; private set; }
    public bool BurntOut { get; private set; }
    /// <summary>How much it has left to burn, 1 (just lit) to 0.</summary>
    public float Fuel => fuel;
    float fuel = 1f;

    Fire fire;
    Light glow;
    AudioSource crackle, sfx;
    Transform flameAt;
    float baseIntensity = 2.4f;

    bool SafeZones.ISource.IsActive => IsLit;
    Vector3 SafeZones.ISource.Center => flameAt != null ? flameAt.position : transform.position;
    float SafeZones.ISource.Radius => safeRadius * Mathf.Lerp(0.6f, 1f, Mathf.Clamp01(fuel * 4f)); // shrinking as it gutters

    void OnEnable() => SafeZones.Register(this);
    void OnDisable() => SafeZones.Unregister(this);

    public string Prompt =>
        BurntOut ? $"The {noun} has burned out" :
        IsLit ? $"The {noun} is burning ({BurnLeft()})" :
        $"Light the {noun} (lighter)";

    string BurnLeft()
    {
        int seconds = Mathf.CeilToInt(fuel * burnTime);
        return seconds >= 60 ? $"{seconds / 60}:{seconds % 60:00}" : $"{seconds}s";
    }

    public bool CanLight => !IsLit && !BurntOut;

    /// <summary>Lights it from the player's lighter, if they have one with fuel in it.</summary>
    public void TryLight(Hotbar hotbar)
    {
        if (!CanLight) return;
        if (hotbar == null || !hotbar.UseLighterFlame(lightingCost))
        {
            if (hotbar != null) hotbar.Notify("You need a lighter to light it");
            Play(ProceduralAudio.FlashlightClick, 0.3f, 0.8f);
            return;
        }
        IsLit = true;
        fuel = 1f;
        fire.gameObject.SetActive(true);
        glow.enabled = true;
        Play(ProceduralAudio.LighterFlick, 0.7f, 1f);
        Play(ProceduralAudio.TorchIgnite, 0.9f, 1f);
        crackle.Play();
        Noise.Emit(transform.position, 6f);
    }

    /// <summary>Unlit and whole again (the run restarts).</summary>
    public void ResetTorch()
    {
        IsLit = false;
        BurntOut = false;
        fuel = 1f;
        if (fire != null) fire.gameObject.SetActive(false);
        if (glow != null) glow.enabled = false;
        if (crackle != null) crackle.Stop();
    }

    void Update()
    {
        if (!IsLit) return;
        fuel -= Time.deltaTime / Mathf.Max(burnTime, 1f);
        float t = Time.time + transform.position.x;
        float strength = Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(fuel * 4f)); // dying down over its last quarter
        fire.strength = strength * (0.9f + 0.1f * Mathf.PerlinNoise(t * 9f, 1.7f));
        glow.intensity = baseIntensity * strength * (0.8f + 0.35f * Mathf.PerlinNoise(t * 8f, 4.2f));
        crackle.volume = 0.45f * strength * (1f - PortalDoor.RoomQuiet);
        if (fuel > 0f) return;

        // Spent: it gutters out, leaving the charred head.
        IsLit = false;
        BurntOut = true;
        fire.gameObject.SetActive(false);
        glow.enabled = false;
        crackle.Stop();
        Play(ProceduralAudio.BurnOut, 0.7f, 1f);
    }

    void Play(AudioClip clip, float volume, float pitch)
    {
        if (sfx == null || clip == null) return;
        sfx.pitch = pitch;
        sfx.PlayOneShot(clip, volume);
    }

    // ---------------------------------------------------------------- Building

    /// <summary>
    /// A campfire's fire, laid in the pit (pivot at the middle of the pit, on the ash): a big fire that burns
    /// for burnTime (ten minutes by default) with a wide circle of light and safety round it.
    /// </summary>
    public static LightableTorch Campfire(Transform parent, Vector3 localPos, float burnSeconds = 600f)
    {
        var torch = Make(parent, "Campfire Fire", localPos, Quaternion.identity);
        torch.noun = "campfire";
        torch.burnTime = burnSeconds;
        torch.safeRadius = 7f;
        torch.lightingCost = 0.08f;
        torch.baseIntensity = 4f;
        torch.Finish(torch.transform, 1.1f, new Vector3(0f, 0.35f, 0f), new Vector3(1.3f, 0.8f, 1.3f), 0.55f, 18f);
        return torch;
    }

    /// <summary>A tall torch staked in the ground (pivot on the ground), standing about head height.</summary>
    public static LightableTorch Standing(Transform parent, Vector3 localPos, float yaw)
    {
        var torch = Make(parent, "Standing Torch", localPos, Quaternion.Euler(0f, yaw, 0f));
        var wood = MaterialFactory.LitTextured(ProceduralTextures.Bark, ProceduralTextures.BarkNormal, new Color(0.45f, 0.35f, 0.26f), 0.08f);
        var stone = MaterialFactory.Lit(new Color(0.2f, 0.19f, 0.18f), 0.1f);
        var t = torch.transform;
        // A rough pole driven into the ground, a few stones packed round its foot.
        var pole = Part(PrimitiveType.Cylinder, wood, t, new Vector3(0f, 0.6f, 0f), new Vector3(0.07f, 0.75f, 0.07f), Vector3.zero, true);
        pole.name = "Pole";
        for (int i = 0; i < 5; i++)
        {
            float a = i / 5f * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
            Part(PrimitiveType.Sphere, stone, t, new Vector3(Mathf.Cos(a) * 0.12f, 0.03f, Mathf.Sin(a) * 0.12f), new Vector3(0.12f, 0.08f, 0.1f) * Random.Range(0.8f, 1.2f), new Vector3(0f, Random.Range(0f, 360f), 0f), false);
        }
        // The torch itself lashed to the top.
        var model = PropModels.Torch(t, true, out Transform head);
        model.localPosition = new Vector3(0f, 1.2f, 0f);
        model.localScale = Vector3.one * 1.5f;
        torch.Finish(head, 0.55f, new Vector3(0f, 1.95f, 0f), new Vector3(0.4f, 1.0f, 0.4f)); // round the torch at the top
        return torch;
    }

    /// <summary>A torch in an iron bracket on a wall (pivot on the wall face, +z out from the wall).</summary>
    public static LightableTorch WallMounted(Transform parent, Vector3 localPos, Quaternion rotation)
    {
        var torch = Make(parent, "Wall Torch", localPos, rotation);
        torch.safeRadius = 4f;
        var iron = MaterialFactory.Lit(new Color(0.07f, 0.065f, 0.06f), 0.45f);
        var t = torch.transform;
        // A back plate nailed to the wall, an arm out, and a ring the torch sits in.
        Part(PrimitiveType.Cube, iron, t, new Vector3(0f, 0f, 0.012f), new Vector3(0.09f, 0.22f, 0.02f), Vector3.zero, false);
        foreach (float y in new[] { -0.08f, 0.08f })
            Part(PrimitiveType.Sphere, iron, t, new Vector3(0f, y, 0.025f), Vector3.one * 0.02f, Vector3.zero, false);
        Part(PrimitiveType.Cube, iron, t, new Vector3(0f, -0.06f, 0.1f), new Vector3(0.025f, 0.025f, 0.18f), new Vector3(-20f, 0f, 0f), false);
        Part(PrimitiveType.Cylinder, iron, t, new Vector3(0f, -0.03f, 0.2f), new Vector3(0.09f, 0.012f, 0.09f), new Vector3(-25f, 0f, 0f), false);
        Part(PrimitiveType.Cylinder, iron, t, new Vector3(0f, 0.08f, 0.24f), new Vector3(0.085f, 0.01f, 0.085f), new Vector3(-25f, 0f, 0f), false);
        // The torch, leaning out from the wall.
        var model = PropModels.Torch(t, true, out Transform head);
        model.localPosition = new Vector3(0f, -0.25f, 0.13f);
        model.localRotation = Quaternion.Euler(25f, 0f, 0f);
        model.localScale = Vector3.one * 1.1f;
        torch.Finish(head, 0.42f, new Vector3(0f, 0.15f, 0.25f), new Vector3(0.35f, 0.7f, 0.4f));
        return torch;
    }

    static LightableTorch Make(Transform parent, string name, Vector3 localPos, Quaternion rotation)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = rotation;
        return go.AddComponent<LightableTorch>();
    }

    /// <summary>The fire (unlit), its light and sounds, and an easy-to-click box round it.</summary>
    void Finish(Transform head, float fireSize, Vector3 aimCenter, Vector3 aimSize, float lightHeight = 0.2f, float lightRange = 0f)
    {
        flameAt = new GameObject("Flame").transform;
        flameAt.SetParent(head, false);
        fire = Fire.Create(flameAt, Vector3.zero, fireSize, fireColor, 8, 3);
        fire.gameObject.SetActive(false);

        glow = new GameObject("Torch Light").AddComponent<Light>();
        glow.transform.SetParent(flameAt, false);
        glow.transform.localPosition = Vector3.up * lightHeight;
        glow.type = LightType.Point;
        glow.color = new Color(1f, 0.62f, 0.3f);
        glow.range = lightRange > 0f ? lightRange : safeRadius * 1.8f;
        glow.intensity = baseIntensity;
        // Shadows, so it lights the room it's in and not through the walls.
        glow.shadows = LightShadows.Soft;
        glow.shadowBias = 0.03f;
        glow.shadowNormalBias = 0f; // (any more and thin planks let the light through)
        glow.shadowNearPlane = 0.1f;
        glow.enabled = false;

        crackle = ProceduralAudio.AddSource(flameAt.gameObject, ProceduralAudio.FireLoop, true, true, 0f, 2f, 18f);
        sfx = ProceduralAudio.AddSource(gameObject, null, false, true, 0.8f, 2f, 25f);

        var aim = gameObject.AddComponent<BoxCollider>();
        aim.isTrigger = true;
        aim.center = aimCenter;
        aim.size = aimSize;
    }

    static Transform Part(PrimitiveType type, Material mat, Transform parent, Vector3 pos, Vector3 scale, Vector3 euler, bool keepCollider)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!keepCollider) Destroy(go.GetComponent<Collider>());
        return go.transform;
    }
}
