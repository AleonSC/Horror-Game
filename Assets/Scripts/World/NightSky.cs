using UnityEngine;

/// <summary>
/// Stars and clouds: a big dome that follows the camera, drawn with the Horror/Night Sky shader.
/// Purely visual: it adds no light to the scene. Clouds drift with the wind (WeatherSystem) and
/// hide the stars behind them. DarkForestGenerator adds this automatically.
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

    Transform dome;
    Material material;
    WeatherSystem weather;
    Vector2 cloudOffset;

    void Awake()
    {
        Tuning.Apply(this); // saved tuning (Inspector changes made in Play are kept)
        Instance = this;
    }

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

        material.SetFloat("_StarBrightness", starBrightness);
        material.SetFloat("_CloudCover", cloudCover);
        material.SetColor("_CloudColor", cloudColor);
        material.SetVector("_CloudOffset", cloudOffset);
        material.SetFloat("_Flash", Flash);
        material.SetVector("_FlashDir", FlashDirection);
    }
}
