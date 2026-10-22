using UnityEngine;

/// <summary>
/// Stars and clouds: a big dome that follows the camera, drawn with the Horror/Night Sky shader.
/// Purely visual: it adds no light to the scene. Clouds drift with the wind (WeatherSystem) and
/// hide the stars behind them. It's pitch dark until the first lamp is lit; then the sky comes on, the stars
/// flickering into life one by one and the clouds rolling in after them (and it goes dark again for a new
/// run). DarkForestGenerator adds this automatically.
/// </summary>
public class NightSky : MonoBehaviour
{
    public static NightSky Instance { get; private set; }
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() => Instance = null;

    /// <summary>Lightning: 0 none, 1 full flash. Lightning sets this; the clouds light up toward FlashDirection.</summary>
    public float Flash { get; set; }
    public Vector3 FlashDirection { get; set; } = Vector3.up;
    [Tooltip("Radius of the sky dome. Keep it inside the camera's far clip distance.")]
    public float radius = 120f;
    [Range(0f, 2f)] public float starBrightness = 1f;
    [Range(0f, 1f)] public float cloudCover = 0.5f;
    public Color cloudColor = new Color(0.05f, 0.055f, 0.065f);
    [Tooltip("How fast clouds drift, before the wind speeds them up.")]
    public float cloudSpeed = 0.004f;
    [Tooltip("Seconds for the sky to come on once the first lamp is lit.")]
    public float revealTime = 6f;
    [Tooltip("Off: the sky's on from the start.")]
    public bool waitForFirstLamp = true;

    Transform dome;
    Material material;
    WeatherSystem weather;
    Vector2 cloudOffset;
    float reveal;
    bool revealed;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        Shader shader = Shader.Find("Horror/Night Sky");
        if (shader == null) { enabled = false; return; }
        material = new Material(shader);

        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "Night Sky";
        go.transform.SetParent(transform, false);
        Destroy(go.GetComponent<Collider>());
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        dome = go.transform;
        dome.localScale = Vector3.one * radius * 2f;
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null || dome == null) return;
        dome.position = cam.transform.position;

        if (weather == null) weather = FindAnyObjectByType<WeatherSystem>();
        Vector3 wind = weather != null ? weather.WindVector : Vector3.right;
        float speed = cloudSpeed * (1f + (weather != null ? weather.CurrentWind * 2f : 0f));
        cloudOffset += new Vector2(wind.x, wind.z).normalized * speed * Time.deltaTime;

        // Dark until the first lamp is lit, then on, with a low swell.
        bool on = !waitForFirstLamp || Lamp.TimesLit > 0;
        if (on && !revealed && reveal < 0.01f && waitForFirstLamp)
            ProceduralAudio.PlayAt(ProceduralAudio.DistantRumble, cam.transform.position + Vector3.up * 30f, 0.35f, 0.5f, 60f);
        revealed = on;
        reveal = Mathf.MoveTowards(reveal, on ? 1f : 0f, Time.deltaTime / (on ? Mathf.Max(revealTime, 0.01f) : 2f));
        material.SetFloat("_Reveal", reveal);

        material.SetFloat("_StarBrightness", starBrightness);
        material.SetFloat("_CloudCover", cloudCover);
        material.SetColor("_CloudColor", cloudColor);
        material.SetVector("_CloudOffset", cloudOffset);
        material.SetFloat("_Flash", Flash);
        material.SetVector("_FlashDir", FlashDirection);
    }
}
