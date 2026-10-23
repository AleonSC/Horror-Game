using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Toggleable flashlight (press F). Put this on the player's camera.
/// If no Light is assigned, a spot light is created as a child automatically.
/// The light lags slightly behind the camera for a hand-held feel.
///
/// It runs on a battery that drains while it's on. When the battery gets low the beam dims and
/// flickers; when it's empty the flashlight won't turn on. Batteries found around the map refill it.
/// The battery bar is drawn in the bottom left of the screen.
///
/// Like a real torch it's more than the beam: a wide, faint spill round it (so the edges of your view
/// aren't black), light bouncing back off whatever it's pointed at close by (tinted by it, lighting up
/// you and the space round you), and a beam that fades out gradually past Range instead of stopping dead.
/// </summary>
public class Flashlight : MonoBehaviour
{
    [Header("Light")]
    [Tooltip("Spot light to toggle. Leave empty to create one automatically.")]
    public Light flashlight;
    [Tooltip("On when the game starts? (Off: press F to turn it on.)")]
    public bool startOn = false;
    public float range = 24f;
    public float spotAngle = 31f;
    public float innerSpotAngle = 30f;
    public float intensity = 20.4f;
    public Color color = new Color(1f, 0.95f, 0.85f);
    public LightShadows shadows = LightShadows.Soft;
    [Tooltip("Extra brightness on top of Intensity (HDRP's lights fall off more than URP's did; the lighting panel's Brightness still sets Intensity).")]
    public float boost = 2f;

    [Tooltip("How far past Range the light really reaches (a multiple), so the beam dies away gradually instead of stopping dead at Range.")]
    public float rangeReach = 2.5f;

    [Header("Spill")]
    [Tooltip("The wide, faint glow round the beam, as a share of the beam's brightness. 0: none.")]
    [Range(0f, 0.5f)] public float spillStrength = 0.08f;
    [Tooltip("How wide the spill is (degrees).")]
    public float spillAngle = 85f;
    [Range(0f, 1f), Tooltip("How much the spill lights the fog (a share of Fog Glow). Low, or the near fog goes milky.")]
    public float spillFogGlow = 0.3f;

    [Header("Bounce")]
    [Tooltip("Light bouncing back off what the beam hits close by, lighting you and the space round you (tinted by the surface). 0: none.")]
    [Range(0f, 1f)] public float bounceStrength = 0.25f;
    [Tooltip("Beyond this the beam's too spread out for any bounce to show (metres).")]
    public float bounceDistance = 12f;
    [Tooltip("How far the bounced light reaches (metres).")]
    public float bounceRange = 7f;

    [Header("Up close")]
    [Tooltip("Pointed at something nearer than this (the ground at your feet), the beam dims, so close things don't blaze white.")]
    public float nearDimDistance = 4f;
    [Range(0f, 1f), Tooltip("How bright the beam is on something right in front of you, as a share of full.")]
    public float nearDimAmount = 0.35f;
    [Range(0f, 2f), Tooltip("How much the beam lights up the fog in the air (HDRP volumetrics). 1 is HDRP's normal.")]
    public float fogGlow = 0.22f;

    [Header("Edge")]
    [Tooltip("Soften the edge of the beam into a feathered fade instead of a hard circle.")]
    public bool featherEdge = true;
    [Range(0f, 1f), Tooltip("How soft the edge is: 0 a crisp ring, 1 a gentle fade from the centre outward.")]
    public float edgeFeather = 0f;
    [Tooltip("Position of the light relative to the camera (slightly right/down, like a hand).")]
    public Vector3 localOffset = new Vector3(0.25f, -0.2f, 0.2f);

    [Header("Sway")]
    [Tooltip("How quickly the beam catches up with where the camera is looking. Lower = more lag; 0 = no lag.")]
    public float swaySpeed = 5.5f;
    [Tooltip("How quickly the light's position catches up when you move (the hand trails a little). 0 = fixed.")]
    public float handLagSpeed = 14f;
    [Tooltip("Most the beam can trail behind the view, in degrees, so a fast spin never leaves you in the dark.")]
    public float maxLagAngle = 14f;

    [Header("Battery")]
    [Tooltip("Seconds a full battery lasts with the light on.")]
    public float batteryLife = 150f;
    [Range(0f, 1f), Tooltip("Below this charge the beam dims and starts flickering.")]
    public float lowBattery = 0.2f;
    public bool showBatteryBar = true;

    [Header("Audio")]
    [Tooltip("Leave empty to use the built-in click.")]
    public AudioClip toggleSound;
    [Range(0f, 1f)] public float toggleVolume = 0.6f;

    public bool IsOn => flashlight != null && flashlight.enabled;
    /// <summary>Battery charge from 0 (empty) to 1 (full).</summary>
    public float Battery { get; private set; } = 1f;
    public bool IsFull => Battery >= 0.999f;
    /// <summary>False while another hotbar item is in hand; the flashlight is off and F does nothing.</summary>
    public bool Equipped { get; private set; } = true;

    bool wasOnBeforeUnequip;

    AudioSource audioSource;
    float flickerTimer;
    float flickerLevel = 1f;
    GUIStyle percentStyle;

    void Awake()
    {
        Tuning.Apply(this); // saved tuning (Save Tuning on the component's menu), before anything uses it
        if (flashlight == null)
        {
            var go = new GameObject("Flashlight Light");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = localOffset;
            flashlight = go.AddComponent<Light>();
            flashlight.type = LightType.Spot;
        }

        flashlight.type = LightType.Spot;
        flashlight.range = range;
        flashlight.spotAngle = spotAngle;
        flashlight.innerSpotAngle = innerSpotAngle;
        ApplyFeather();
        flashlight.intensity = intensity * boost;
        flashlight.color = color;
        flashlight.shadows = shadows;
        flashlight.enabled = startOn;
        BuildSpillAndBounce();

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
    }

    bool appliedFeather;
    float appliedFeatherAmount = -1f;

    /// <summary>
    /// Feathers the beam's edge: widens the falloff between the bright core and the rim, and lays a soft radial
    /// cookie over it (smoothstep from the centre to the edge, with a faint brighter ring like a real reflector),
    /// so the circle of light melts into the dark. Rebuilt whenever the settings change, even during play.
    /// </summary>
    void ApplyFeather()
    {
        if (flashlight == null) return;
        if (appliedFeather == featherEdge && Mathf.Abs(appliedFeatherAmount - edgeFeather) < 0.01f) return;
        appliedFeather = featherEdge;
        appliedFeatherAmount = edgeFeather;

        if (!featherEdge)
        {
            flashlight.innerSpotAngle = innerSpotAngle;
            flashlight.cookie = null;
            return;
        }
        flashlight.innerSpotAngle = spotAngle * 0.95f; // (the cookie does the falloff: a real beam's shape)
        flashlight.cookie = MakeFeatherCookie(edgeFeather);
    }

    /// <summary>
    /// The beam's shape, like a real flashlight's: a hot, bright centre, a softer spill round it falling away
    /// to a feathered edge (feather: how soft), faint rings from the reflector, and the slight unevenness of a
    /// cheap lens.
    /// </summary>
    static Texture2D MakeFeatherCookie(float feather)
    {
        const int n = 256;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false, true)
        {
            name = "Flashlight Feather", wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear,
        };
        var px = new Color32[n * n];
        float inner = Mathf.Lerp(0.85f, 0.15f, feather); // where the fade starts, as a fraction of the radius
        for (int y = 0; y < n; y++)
        {
            for (int x = 0; x < n; x++)
            {
                float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                float r = Mathf.Sqrt(dx * dx + dy * dy);
                float spill = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner, 0.98f, r));
                float hot = Mathf.Exp(-Mathf.Pow(r / 0.28f, 2f));                      // the hot centre
                float v = spill * 0.42f + hot * 0.58f;
                v *= 1f + 0.07f * Mathf.Sin(r * 46f) * Mathf.SmoothStep(0.1f, 0.4f, r);  // reflector rings
                v *= 1f - 0.12f * Mathf.Exp(-Mathf.Pow((r - 0.2f) / 0.025f, 2f));    // a darker ring round the hot spot
                v *= 0.94f + 0.06f * Mathf.PerlinNoise(dx * 6f + 3.1f, dy * 6f + 7.7f); // the lens
                v = Mathf.Clamp01(v);
                byte b = (byte)Mathf.RoundToInt(v * 255f);
                px[y * n + x] = new Color32(b, b, b, b);
            }
        }
        tex.SetPixels32(px);
        tex.Apply(false, true);
        return tex;
    }

    void Update()
    {
        ApplyFeather(); // live: picks up changes to the edge settings during play
        if (PauseMenu.IsPaused || MainMenu.Showing) return; // (no switching it on behind the main menu)
        if (HorrorInput.FlashlightPressed && Equipped && !InventoryScreen.IsOpen)
            Toggle();

        if (IsOn) UpdateBattery();
        UpdateSpillAndBounce(); // (always: they follow the beam on and off, however it's switched)
    }

    void UpdateBattery()
    {
        Battery = Mathf.Max(0f, Battery - Time.deltaTime / Mathf.Max(batteryLife * Difficulty.BatteryLife, 0.01f));
        if (Battery <= 0f)
        {
            flashlight.enabled = false;
            PlayClick(0.7f);
            return;
        }

        // 0 while healthy, rising to 1 as the battery dies.
        float low = lowBattery > 0f ? Mathf.Clamp01(1f - Battery / lowBattery) : 0f;

        if (flickerTimer > 0f)
        {
            flickerTimer -= Time.deltaTime;
            if (flickerTimer <= 0f) flickerLevel = 1f;
        }
        else if (low > 0f && Random.value < Mathf.Lerp(0.5f, 6f, low) * Time.deltaTime)
        {
            flickerTimer = Random.Range(0.03f, Mathf.Lerp(0.08f, 0.3f, low));
            flickerLevel = Random.Range(0f, 0.5f);
        }

        Probe();
        flashlight.intensity = intensity * boost * Mathf.Lerp(1f, 0.45f, low) * flickerLevel * NearDim();
        flashlight.range = range * Mathf.Max(1f, rangeReach);
        var hd = flashlight.GetComponent<HDAdditionalLightData>();
        if (hd != null) hd.volumetricDimmer = fogGlow;
    }

    // ---------------------------------------------------------------- Spill and bounce

    Light spill, bounce;
    HDAdditionalLightData spillHd, bounceHd;
    bool probeHit;
    RaycastHit probe;
    float bounceLevel;
    Color bounceTint = Color.white;
    Collider tintFrom;

    /// <summary>The spill (a wide faint spot on the beam) and the bounce (a soft point light where it lands).</summary>
    void BuildSpillAndBounce()
    {
        spill = new GameObject("Flashlight Spill").AddComponent<Light>();
        spill.transform.SetParent(flashlight.transform, false);
        spill.type = LightType.Spot;
        spill.shadows = LightShadows.None;
        spill.enabled = false;
        spillHd = spill.gameObject.AddComponent<HDAdditionalLightData>();

        bounce = new GameObject("Flashlight Bounce").AddComponent<Light>();
        bounce.transform.SetParent(transform, false);
        bounce.type = LightType.Point;
        bounce.shadows = LightShadows.None;
        bounce.enabled = false;
        bounceHd = bounce.gameObject.AddComponent<HDAdditionalLightData>();
    }

    /// <summary>What the beam is pointed at (one ray a frame, for the bounce and the up-close dimming).</summary>
    void Probe()
    {
        Transform t = flashlight.transform;
        float reach = Mathf.Max(nearDimDistance, bounceDistance);
        probeHit = reach > 0f && Physics.Raycast(t.position, t.forward, out probe, reach, ~0, QueryTriggerInteraction.Ignore);
    }

    void UpdateSpillAndBounce()
    {
        if (spill == null || bounce == null) return;
        bool on = flashlight.enabled;
        float beam = flashlight.intensity; // (with the battery, flicker and up-close dimming in it)

        // Spill: the same light, much wider and fainter, fading smoothly from the middle out.
        spill.enabled = on && spillStrength > 0f;
        if (spill.enabled)
        {
            spill.spotAngle = Mathf.Clamp(spillAngle, flashlight.spotAngle, 170f);
            spill.innerSpotAngle = Mathf.Min(flashlight.spotAngle * 0.5f, spill.spotAngle * 0.3f);
            spill.color = flashlight.color;
            spill.intensity = beam * spillStrength;
            spill.range = flashlight.range * 0.6f; // (it spreads out: it doesn't carry as far)
            if (spillHd != null) spillHd.volumetricDimmer = fogGlow * spillFogGlow;
        }

        // Bounce: where the beam lands, a soft glow comes back toward you, tinted by what it hit. (The light a
        // beam throws back is about the same whatever the distance: further off, the spot it lights is
        // bigger and dimmer. It's the distance back to you that makes a far wall's bounce fade, so the glow
        // sits between the wall and you, and fades out toward bounceDistance.)
        float target = 0f;
        if (on && bounceStrength > 0f && probeHit)
        {
            float fade = 1f - Mathf.SmoothStep(0f, 1f, probe.distance / Mathf.Max(bounceDistance, 0.1f));
            target = beam * bounceStrength * 0.1f * fade;
            if (probe.collider != tintFrom)
            {
                tintFrom = probe.collider;
                bounceTint = SurfaceTint(probe.collider);
            }
            // Off the surface (no hot spot on it) and partway back toward you.
            Vector3 back = (flashlight.transform.position - probe.point).normalized * Mathf.Min(0.6f, probe.distance * 0.4f);
            Vector3 at = probe.point + probe.normal * 0.5f + back;
            bool far = (bounce.transform.position - at).sqrMagnitude > 9f;
            bounce.transform.position = far || bounceLevel <= 0.001f ? at : Vector3.Lerp(bounce.transform.position, at, 1f - Mathf.Exp(-12f * Time.deltaTime));
        }
        bounceLevel = Mathf.MoveTowards(bounceLevel, target, Time.deltaTime * Mathf.Max(target, bounceLevel, 0.5f) * 6f);
        bounce.enabled = bounceLevel > 0.001f;
        if (bounce.enabled)
        {
            bounce.intensity = bounceLevel;
            bounce.range = bounceRange;
            bounce.color = Color.Lerp(flashlight.color, flashlight.color * bounceTint, 0.7f);
            if (bounceHd != null) bounceHd.volumetricDimmer = 0f; // (no glowing ball of fog where it lands)
        }
    }

    /// <summary>The colour of what the beam hit (its material's colour, brightened to full: only the hue matters), or white.</summary>
    static Color SurfaceTint(Collider c)
    {
        var r = c.GetComponent<Renderer>();
        if (r == null) r = c.GetComponentInChildren<Renderer>();
        var mat = r != null ? r.sharedMaterial : null;
        if (mat == null) return Color.white;
        Color col = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor") : mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
        float most = Mathf.Max(col.maxColorComponent, 0.0001f);
        col /= most;
        col.a = 1f;
        return Color.Lerp(Color.white, col, 0.6f);
    }

    float nearDim = 1f;

    /// <summary>
    /// Dimmer when it's pointed at something close (the way your eyes would squint): full beyond
    /// nearDimDistance, down to nearDimAmount right in front of you. Smoothed, so it doesn't jump.
    /// </summary>
    float NearDim()
    {
        float target = 1f;
        if (nearDimDistance > 0f && probeHit && probe.distance < nearDimDistance)
            target = Mathf.Lerp(nearDimAmount, 1f, Mathf.SmoothStep(0f, 1f, probe.distance / nearDimDistance));
        nearDim = Mathf.MoveTowards(nearDim, target, Time.deltaTime * 2.5f);
        return nearDim;
    }

    void LateUpdate()
    {
        // Smoothly follow the camera's rotation so the beam drags a little when turning.
        if (flashlight == null || flashlight.transform == transform) return;
        Transform t = flashlight.transform;
        float dt = Time.deltaTime;

        // The beam trails the view, then catches up, but never by more than maxLagAngle.
        Quaternion rot = swaySpeed > 0f
            ? Quaternion.Slerp(t.rotation, transform.rotation, 1f - Mathf.Exp(-swaySpeed * dt))
            : transform.rotation;
        float behind = Quaternion.Angle(rot, transform.rotation);
        if (behind > maxLagAngle) rot = Quaternion.RotateTowards(transform.rotation, rot, maxLagAngle);
        t.rotation = rot;

        // And the hand holding it trails a touch when you move.
        Vector3 held = transform.TransformPoint(localOffset);
        bool teleported = (t.position - held).sqrMagnitude > 4f; // the map looped, or through the door
        t.position = handLagSpeed > 0f && !teleported ? Vector3.Lerp(t.position, held, 1f - Mathf.Exp(-handLagSpeed * dt)) : held;
    }

    /// <summary>How many times the player has clicked it on or off (entities watch for frantic flashing).</summary>
    public int ToggleCount { get; private set; }

    public void Toggle()
    {
        ToggleCount++;
        SetOn(!IsOn);
    }

    public void SetOn(bool on)
    {
        if (flashlight == null) return;

        if (on && Battery <= 0f)
        {
            PlayClick(0.7f); // dead: just a click in the dark
            return;
        }

        flashlight.enabled = on;
        flickerTimer = 0f;
        flickerLevel = 1f;
        PlayClick(1f);
        Noise.Emit(transform.position, 3f); // a click, close by
    }

    /// <summary>Puts the flashlight away (off) or takes it back out (back on if it was on before).</summary>
    public void SetEquipped(bool equipped)
    {
        if (equipped == Equipped || flashlight == null) return;
        Equipped = equipped;
        if (!equipped)
        {
            wasOnBeforeUnequip = flashlight.enabled;
            flashlight.enabled = false;
        }
        else if (wasOnBeforeUnequip && Battery > 0f)
            flashlight.enabled = true; // back on as it was, silently (switching items makes no sound)
    }

    /// <summary>Adds charge (0-1). Returns false if the battery was already full.</summary>
    public bool AddBattery(float amount)
    {
        if (IsFull) return false;
        Battery = Mathf.Clamp01(Battery + amount);
        return true;
    }

    /// <summary>Takes charge out of the battery (0-1): facing a scour down costs some.</summary>
    public void Drain(float amount) => Battery = Mathf.Clamp01(Battery - amount);

    /// <summary>Tops the battery up to at least this much (after being caught).</summary>
    public void EnsureCharge(float atLeast) => Battery = Mathf.Max(Battery, Mathf.Clamp01(atLeast));

    /// <summary>Full battery and back to the starting on/off state (used when the run resets).</summary>
    public void ResetBattery()
    {
        Battery = 1f;
        Equipped = true;
        flickerTimer = 0f;
        flickerLevel = 1f;
        if (flashlight != null)
        {
            flashlight.intensity = intensity * boost;
            flashlight.enabled = startOn;
        }
    }

    void PlayClick(float pitch)
    {
        audioSource.pitch = pitch;
        audioSource.PlayOneShot(toggleSound != null ? toggleSound : ProceduralAudio.FlashlightClick, toggleVolume);
    }

    // ---------------------------------------------------------------- Battery bar

    void OnGUI()
    {
        if (!showBatteryBar || GameOverScreen.IsActive || MainMenu.Showing) return;

        float unit = Mathf.Max(1f, Screen.height / 1080f);
        float w = 150f * unit, h = 34f * unit, margin = 24f * unit;
        var body = new Rect(margin, Screen.height - margin - h, w, h);
        var tip = new Rect(body.xMax, body.y + h * 0.3f, 6f * unit, h * 0.4f);

        bool low = Battery < lowBattery;
        bool blinkOff = Battery < lowBattery * 0.5f && Mathf.Repeat(Time.time, 0.8f) < 0.3f;
        Color frame = low ? new Color(0.9f, 0.3f, 0.2f, 0.9f) : new Color(0.9f, 0.88f, 0.8f, 0.85f);
        Color fill = low ? new Color(0.9f, 0.25f, 0.15f) : new Color(0.95f, 0.9f, 0.7f);
        float border = 2f * unit, pad = 3f * unit;

        // Battery outline
        DrawRect(new Rect(body.x, body.y, body.width, border), frame);
        DrawRect(new Rect(body.x, body.yMax - border, body.width, border), frame);
        DrawRect(new Rect(body.x, body.y, border, body.height), frame);
        DrawRect(new Rect(body.xMax - border, body.y, border, body.height), frame);
        DrawRect(tip, frame);

        // Charge
        var inner = new Rect(body.x + border + pad, body.y + border + pad,
            body.width - 2f * (border + pad), body.height - 2f * (border + pad));
        DrawRect(inner, new Color(0f, 0f, 0f, 0.5f));
        if (!blinkOff)
            DrawRect(new Rect(inner.x, inner.y, inner.width * Battery, inner.height), fill);

        if (percentStyle == null)
            percentStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontStyle = FontStyle.Bold };
        percentStyle.fontSize = Mathf.RoundToInt(18f * unit);
        percentStyle.normal.textColor = frame;
        GUI.Label(new Rect(tip.xMax + 10f * unit, body.y, 80f * unit, body.height),
            Mathf.CeilToInt(Battery * 100f) + "%", percentStyle);

        // Stamina: a thin bar just above the battery, quiet when you're fresh, reddening as you tire.
        if (player == null) player = FindAnyObjectByType<FirstPersonController>();
        if (player != null)
        {
            float stamina = player.Stamina;
            staminaShown = Mathf.MoveTowards(staminaShown, stamina >= 0.999f ? 0.35f : 1f, Time.unscaledDeltaTime * 2f);
            // (Longer with each snack eaten: there's more of it.)
            var bar = new Rect(body.x, body.y - 12f * unit, body.width * (1f + player.StaminaBonus), 6f * unit);
            DrawRect(bar, new Color(0f, 0f, 0f, 0.5f * staminaShown));
            Color tint = player.Winded ? new Color(0.9f, 0.3f, 0.2f) : Color.Lerp(new Color(0.9f, 0.45f, 0.25f), new Color(0.75f, 0.9f, 0.75f), Mathf.InverseLerp(0.15f, 0.6f, stamina));
            tint.a = 0.9f * staminaShown;
            DrawRect(new Rect(bar.x + unit, bar.y + unit, (bar.width - 2f * unit) * stamina, bar.height - 2f * unit), tint);
        }
    }

    FirstPersonController player;
    float staminaShown = 1f;

    static void DrawRect(Rect r, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }
}
