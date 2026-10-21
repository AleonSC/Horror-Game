using UnityEngine;

/// <summary>
/// Toggleable flashlight (press F). Put this on the player's camera.
/// If no Light is assigned, a spot light is created as a child automatically.
/// The light lags slightly behind the camera for a hand-held feel.
///
/// It runs on a battery that drains while it's on. When the battery gets low the beam dims and
/// flickers; when it's empty the flashlight won't turn on. Batteries found around the map refill it.
/// The battery bar is drawn in the bottom left of the screen.
/// </summary>
public class Flashlight : MonoBehaviour
{
    [Header("Light")]
    [Tooltip("Spot light to toggle. Leave empty to create one automatically.")]
    public Light flashlight;
    [Tooltip("On when the game starts? (Off: press F to turn it on.)")]
    public bool startOn = false;
    public float range = 45f;
    public float spotAngle = 60f;
    public float innerSpotAngle = 30f;
    public float intensity = 12f;
    public Color color = new Color(1f, 0.95f, 0.85f);
    public LightShadows shadows = LightShadows.Soft;

    [Header("Edge")]
    [Tooltip("Soften the edge of the beam into a feathered fade instead of a hard circle.")]
    public bool featherEdge = true;
    [Range(0f, 1f), Tooltip("How soft the edge is: 0 a crisp ring, 1 a gentle fade from the centre outward.")]
    public float edgeFeather = 0.6f;
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
        flashlight.intensity = intensity;
        flashlight.color = color;
        flashlight.shadows = shadows;
        flashlight.enabled = startOn;

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
        flashlight.innerSpotAngle = Mathf.Lerp(spotAngle * 0.9f, spotAngle * 0.05f, edgeFeather);
        flashlight.cookie = MakeFeatherCookie(edgeFeather);
    }

    static Texture2D MakeFeatherCookie(float feather)
    {
        const int n = 128;
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
                float v = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(inner, 0.98f, r));
                v *= 1f + 0.12f * Mathf.Exp(-Mathf.Pow((r - inner * 0.9f) / 0.08f, 2f)); // a faint reflector ring
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
        if (PauseMenu.IsPaused) return;
        if (HorrorInput.FlashlightPressed && Equipped && !InventoryScreen.IsOpen)
            Toggle();

        if (IsOn) UpdateBattery();
    }

    void UpdateBattery()
    {
        Battery = Mathf.Max(0f, Battery - Time.deltaTime / Mathf.Max(batteryLife, 0.01f));
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

        flashlight.intensity = intensity * Mathf.Lerp(1f, 0.45f, low) * flickerLevel;
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
            flashlight.intensity = intensity;
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
        if (!showBatteryBar || GameOverScreen.IsActive) return;

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
    }

    static void DrawRect(Rect r, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }
}
