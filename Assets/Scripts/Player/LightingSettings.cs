using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// The lighting panel, a developer tool (DevTools, F9, Lighting tab): pick a theme, then tweak the night live: moonlight,
/// ambient light, fog, the night sky, the flashlight, shadows, and the camera's picture (exposure, contrast,
/// saturation, vignette, film grain). Settings are saved between sessions. "Default" is the usual moonlit
/// night, "Darker" a much darker one where the flashlight matters more; "Original" puts back what the scene
/// started with. "Log Values" prints the current setup to the Console so a look you like can be made the default.
/// PauseMenu adds this (so the saved lighting is applied); DevTools draws its panel.
/// </summary>
public class LightingSettings : MonoBehaviour
{
    [System.Serializable]
    public class Theme
    {
        public string name;
        public Color moonColor;
        public float moonIntensity;
        public float moonHeight;      // degrees above the horizon
        public Color ambient;
        public Color fogColor;
        public float fogDensity;
        public Color flashlightColor;
    }

    public Theme[] themes =
    {
        new Theme { name = "Default", moonColor = new Color(0.45f, 0.55f, 0.8f), moonIntensity = 0.25f, moonHeight = 35f, ambient = new Color(0.06f, 0.07f, 0.09f), fogColor = new Color(0.02f, 0.025f, 0.035f), fogDensity = 0.05f, flashlightColor = new Color(1f, 0.95f, 0.85f) },
        new Theme { name = "Darker", moonColor = new Color(0.42f, 0.5f, 0.75f), moonIntensity = 0.07f, moonHeight = 35f, ambient = new Color(0.016f, 0.018f, 0.024f), fogColor = new Color(0.012f, 0.014f, 0.02f), fogDensity = 0.055f, flashlightColor = new Color(1f, 0.95f, 0.85f) },
        new Theme { name = "Pitch Black", moonColor = new Color(0.4f, 0.45f, 0.6f), moonIntensity = 0.02f, moonHeight = 30f, ambient = new Color(0.006f, 0.006f, 0.008f), fogColor = new Color(0.004f, 0.004f, 0.006f), fogDensity = 0.07f, flashlightColor = new Color(1f, 0.93f, 0.8f) },
        new Theme { name = "Blood Moon", moonColor = new Color(0.9f, 0.2f, 0.15f), moonIntensity = 0.18f, moonHeight = 25f, ambient = new Color(0.04f, 0.01f, 0.01f), fogColor = new Color(0.05f, 0.008f, 0.008f), fogDensity = 0.045f, flashlightColor = new Color(1f, 0.9f, 0.8f) },
        new Theme { name = "Foggy", moonColor = new Color(0.6f, 0.65f, 0.7f), moonIntensity = 0.1f, moonHeight = 40f, ambient = new Color(0.04f, 0.04f, 0.045f), fogColor = new Color(0.06f, 0.06f, 0.065f), fogDensity = 0.1f, flashlightColor = new Color(1f, 0.97f, 0.9f) },
        new Theme { name = "Sickly Green", moonColor = new Color(0.5f, 0.8f, 0.4f), moonIntensity = 0.12f, moonHeight = 35f, ambient = new Color(0.02f, 0.035f, 0.015f), fogColor = new Color(0.02f, 0.035f, 0.016f), fogDensity = 0.055f, flashlightColor = new Color(0.9f, 1f, 0.85f) },
        new Theme { name = "Dusk", moonColor = new Color(1f, 0.55f, 0.3f), moonIntensity = 0.6f, moonHeight = 8f, ambient = new Color(0.12f, 0.07f, 0.06f), fogColor = new Color(0.15f, 0.08f, 0.06f), fogDensity = 0.03f, flashlightColor = new Color(1f, 0.95f, 0.85f) },
        new Theme { name = "Bright (testing)", moonColor = new Color(0.7f, 0.75f, 0.9f), moonIntensity = 1f, moonHeight = 50f, ambient = new Color(0.25f, 0.27f, 0.3f), fogColor = new Color(0.05f, 0.06f, 0.08f), fogDensity = 0.015f, flashlightColor = Color.white },
    };

    const string SaveKey = "Horror.Lighting.v2"; // v1 saves started from the too-dark default: ignored

    /// <summary>Everything the panel controls, as saved between sessions.</summary>
    [System.Serializable]
    class Values
    {
        public int theme = -1;
        public float moonIntensity, moonHeight = 35f, ambientScale = 1f, fogDensity, fogBrightness = 1f;
        public Color moonColor, ambientColor, fogColor, flashlightColor;
        public bool fogOn = true, flashlightShadows = true, extraShadows, flashlightFeather = true;
        public int moonShadows = 2; // 0 off, 1 hard, 2 soft
        public float flashlightIntensity = 12f, flashlightRange = 45f, flashlightAngle = 60f, flashlightSoftness = 0.6f;
        public float starBrightness = 1f, cloudCover = 0.5f;
        public float exposure, contrast, saturation, vignette, grain;
    }

    Values v = new Values();
    Theme original;
    bool captured;
    float lightRefresh;
    Vector2 scroll;
    float panelUnit = 1f;
    GUIStyle headerStyle, labelStyle, buttonStyle, selectedStyle;

    Volume volume;
    ColorAdjustments colorAdjust;
    Vignette vignetteFx;
    FilmGrain grainFx;

    static readonly string[] ShadowNames = { "Off", "Hard", "Soft" };

    void Start()
    {
        Capture();
        string saved = PlayerPrefs.GetString(SaveKey, "");
        if (!string.IsNullOrEmpty(saved))
        {
            try { JsonUtility.FromJsonOverwrite(saved, v); }
            catch { UseTheme(themes[0], 0); }
        }
        else UseTheme(themes[0], 0); // the intended dark look
        Apply(false);
    }

    void Update()
    {
        if (!captured) return;
        lightRefresh -= Time.unscaledDeltaTime;
        if (lightRefresh > 0f) return;
        lightRefresh = 1f;
        // New lamps / torches get the extra-shadows setting, and new cameras the picture settings.
        if (v.extraShadows) ApplyExtraShadows();
        EnablePostProcessing();
        // The sky's set every frame by NightSky itself, so keep it told.
        if (NightSky.Instance != null)
        {
            NightSky.Instance.starBrightness = v.starBrightness;
            NightSky.Instance.cloudCover = v.cloudCover;
        }
    }

    void Capture()
    {
        Light moon = RenderSettings.sun;
        Flashlight flashlight = FindAnyObjectByType<Flashlight>();

        original = new Theme
        {
            name = "Original",
            moonColor = moon != null ? moon.color : Color.white,
            moonIntensity = moon != null ? moon.intensity : 0f,
            moonHeight = moon != null ? moon.transform.eulerAngles.x : 35f,
            ambient = RenderSettings.ambientLight,
            fogColor = RenderSettings.fogColor,
            fogDensity = RenderSettings.fogDensity,
            flashlightColor = flashlight != null ? flashlight.color : Color.white,
        };
        v.fogOn = RenderSettings.fog;
        v.moonShadows = moon == null ? 0 : moon.shadows == LightShadows.None ? 0 : moon.shadows == LightShadows.Hard ? 1 : 2;
        v.flashlightShadows = flashlight == null || flashlight.flashlight.shadows != LightShadows.None;
        v.flashlightIntensity = flashlight != null ? flashlight.intensity : 12f;
        v.flashlightRange = flashlight != null ? flashlight.flashlight.range : 45f;
        v.flashlightAngle = flashlight != null ? flashlight.flashlight.spotAngle : 60f;
        v.flashlightFeather = flashlight == null || flashlight.featherEdge;
        v.flashlightSoftness = flashlight != null ? flashlight.edgeFeather : 0.6f;
        if (NightSky.Instance != null)
        {
            v.starBrightness = NightSky.Instance.starBrightness;
            v.cloudCover = NightSky.Instance.cloudCover;
        }
        UseTheme(original, -1);
        captured = true;
    }

    void UseTheme(Theme t, int index)
    {
        v.theme = index;
        v.moonColor = t.moonColor;
        v.moonIntensity = t.moonIntensity;
        v.moonHeight = t.moonHeight;
        v.ambientColor = t.ambient;
        v.ambientScale = 1f;
        v.fogColor = t.fogColor;
        v.fogBrightness = 1f;
        v.fogDensity = t.fogDensity;
        v.flashlightColor = t.flashlightColor;
    }

    void Apply(bool save = true)
    {
        Light moon = RenderSettings.sun;
        if (moon != null)
        {
            moon.useColorTemperature = false;
            moon.color = v.moonColor;
            moon.intensity = v.moonIntensity;
            moon.shadows = v.moonShadows == 0 ? LightShadows.None : v.moonShadows == 1 ? LightShadows.Hard : LightShadows.Soft;
            Vector3 e = moon.transform.eulerAngles;
            moon.transform.rotation = Quaternion.Euler(v.moonHeight, e.y, 0f);
        }

        Color fog = v.fogColor * v.fogBrightness;
        fog.a = 1f;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = v.ambientColor * v.ambientScale;
        RenderSettings.fog = v.fogOn;
        RenderSettings.fogColor = fog;
        RenderSettings.fogDensity = v.fogDensity;
        foreach (var cam in Camera.allCameras) cam.backgroundColor = fog;

        var flashlight = FindAnyObjectByType<Flashlight>();
        if (flashlight != null)
        {
            flashlight.intensity = v.flashlightIntensity;
            flashlight.color = v.flashlightColor;
            flashlight.flashlight.color = v.flashlightColor;
            flashlight.flashlight.range = v.flashlightRange;
            flashlight.flashlight.spotAngle = v.flashlightAngle;
            if (!v.flashlightFeather) flashlight.flashlight.innerSpotAngle = v.flashlightAngle * 0.5f;
            flashlight.featherEdge = v.flashlightFeather;
            flashlight.edgeFeather = v.flashlightSoftness;
            flashlight.flashlight.shadows = v.flashlightShadows ? LightShadows.Soft : LightShadows.None;
        }

        if (NightSky.Instance != null)
        {
            NightSky.Instance.starBrightness = v.starBrightness;
            NightSky.Instance.cloudCover = v.cloudCover;
        }

        ApplyExtraShadows();
        ApplyPicture();

        if (save)
        {
            PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(v));
            PlayerPrefs.Save();
        }
    }

    /// <summary>The camera's picture: exposure, contrast, saturation, vignette and film grain, on a global volume.</summary>
    void ApplyPicture()
    {
        if (volume == null)
        {
            volume = new GameObject("Lighting Volume").AddComponent<Volume>();
            volume.transform.SetParent(transform, false);
            volume.isGlobal = true;
            volume.priority = 100f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            volume.profile = profile;
            colorAdjust = profile.Add<ColorAdjustments>(true);
            vignetteFx = profile.Add<Vignette>(true);
            grainFx = profile.Add<FilmGrain>(true);
        }
        colorAdjust.postExposure.Override(v.exposure);
        colorAdjust.contrast.Override(v.contrast);
        colorAdjust.saturation.Override(v.saturation);
        vignetteFx.intensity.Override(v.vignette);
        vignetteFx.smoothness.Override(0.45f);
        vignetteFx.color.Override(Color.black);
        grainFx.intensity.Override(v.grain);
        grainFx.response.Override(0.8f);
        EnablePostProcessing();
    }

    static void EnablePostProcessing()
    {
        foreach (var cam in Camera.allCameras)
        {
            if (cam.targetTexture != null) continue; // the door's view cameras are copied from the main one
            var data = cam.GetUniversalAdditionalCameraData();
            if (data != null && !data.renderPostProcessing) data.renderPostProcessing = true;
        }
    }

    /// <summary>Shadows on every point light (lamps, torch, lighter, floodlights are spots and keep theirs).</summary>
    void ApplyExtraShadows()
    {
        foreach (var light in Resources.FindObjectsOfTypeAll<Light>())
            if (light.gameObject.scene.IsValid() && light.type == LightType.Point)
                light.shadows = v.extraShadows ? LightShadows.Soft : LightShadows.None;
    }

    void LogValues()
    {
        string theme = v.theme >= 0 ? themes[v.theme].name : "Original";
        Debug.Log($"Lighting values (based on '{theme}'):\n" +
                  $"  moon colour {v.moonColor}, intensity {v.moonIntensity:0.000}, height {v.moonHeight:0}°, shadows {ShadowNames[v.moonShadows]}\n" +
                  $"  ambient {v.ambientColor * v.ambientScale}\n" +
                  $"  fog {(v.fogOn ? "on" : "off")}, colour {v.fogColor * v.fogBrightness}, density {v.fogDensity:0.000}\n" +
                  $"  sky stars {v.starBrightness:0.00}, clouds {v.cloudCover:0.00}\n" +
                  $"  flashlight intensity {v.flashlightIntensity:0.0}, range {v.flashlightRange:0}, angle {v.flashlightAngle:0}°, colour {v.flashlightColor}, shadows {v.flashlightShadows}, soft edge {v.flashlightFeather} ({v.flashlightSoftness:0.00})\n" +
                  $"  picture exposure {v.exposure:0.00}, contrast {v.contrast:0}, saturation {v.saturation:0}, vignette {v.vignette:0.00}, grain {v.grain:0.00}\n" +
                  $"  lamp / torch shadows {v.extraShadows}");
    }

    // ---------------------------------------------------------------- Panel (drawn by DevTools)

    public void DrawPanel(Rect area, float unit)
    {
        if (!captured) Capture();
        panelUnit = unit;

        if (headerStyle == null)
        {
            headerStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            labelStyle = new GUIStyle(GUI.skin.label);
            buttonStyle = new GUIStyle(GUI.skin.button);
            selectedStyle = new GUIStyle(GUI.skin.button) { fontStyle = FontStyle.Bold };
        }
        headerStyle.fontSize = Mathf.RoundToInt(18f * unit);
        headerStyle.normal.textColor = new Color(0.95f, 0.75f, 0.45f);
        labelStyle.fontSize = Mathf.RoundToInt(14f * unit);
        labelStyle.normal.textColor = new Color(0.85f, 0.82f, 0.76f);
        buttonStyle.fontSize = selectedStyle.fontSize = Mathf.RoundToInt(14f * unit);
        selectedStyle.normal.textColor = new Color(1f, 0.8f, 0.4f);

        GUILayout.BeginArea(area);
        scroll = GUILayout.BeginScrollView(scroll);
        bool changed = false;

        GUILayout.Label("Theme", headerStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Original", v.theme < 0 ? selectedStyle : buttonStyle)) { UseTheme(original, -1); changed = true; }
        for (int i = 0; i < themes.Length; i++)
        {
            if (i == 3 || i == 7) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
            if (GUILayout.Button(themes[i].name, v.theme == i ? selectedStyle : buttonStyle)) { UseTheme(themes[i], i); changed = true; }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(8f * unit);
        GUILayout.Label("Picture", headerStyle);
        changed |= Slider("Exposure", ref v.exposure, -3f, 2f, "0.00");
        changed |= Slider("Contrast", ref v.contrast, -50f, 60f, "0");
        changed |= Slider("Saturation", ref v.saturation, -100f, 40f, "0");
        changed |= Slider("Vignette", ref v.vignette, 0f, 0.7f, "0.00");
        changed |= Slider("Film grain", ref v.grain, 0f, 1f, "0.00");

        GUILayout.Space(8f * unit);
        GUILayout.Label("Moon", headerStyle);
        changed |= Slider("Brightness", ref v.moonIntensity, 0f, 1f, "0.000");
        changed |= Slider("Height (degrees)", ref v.moonHeight, 2f, 90f, "0");
        changed |= Slider("Red", ref v.moonColor.r, 0f, 1f, "0.00");
        changed |= Slider("Green", ref v.moonColor.g, 0f, 1f, "0.00");
        changed |= Slider("Blue", ref v.moonColor.b, 0f, 1f, "0.00");
        GUILayout.Label("Shadows", labelStyle);
        int newShadows = GUILayout.Toolbar(v.moonShadows, ShadowNames, buttonStyle);
        if (newShadows != v.moonShadows) { v.moonShadows = newShadows; changed = true; }

        GUILayout.Space(8f * unit);
        GUILayout.Label("Ambient, fog & sky", headerStyle);
        changed |= Slider("Ambient light", ref v.ambientScale, 0f, 5f, "0.00");
        bool newFog = GUILayout.Toggle(v.fogOn, " Fog", buttonStyle);
        if (newFog != v.fogOn) { v.fogOn = newFog; changed = true; }
        changed |= Slider("Fog density", ref v.fogDensity, 0f, 0.15f, "0.000");
        changed |= Slider("Fog brightness", ref v.fogBrightness, 0f, 4f, "0.00");
        changed |= Slider("Stars", ref v.starBrightness, 0f, 2f, "0.00");
        changed |= Slider("Clouds", ref v.cloudCover, 0f, 1f, "0.00");

        GUILayout.Space(8f * unit);
        GUILayout.Label("Flashlight", headerStyle);
        changed |= Slider("Brightness", ref v.flashlightIntensity, 0f, 40f, "0.0");
        changed |= Slider("Range", ref v.flashlightRange, 5f, 100f, "0");
        changed |= Slider("Beam width (degrees)", ref v.flashlightAngle, 15f, 120f, "0");
        bool newFeather = GUILayout.Toggle(v.flashlightFeather, " Soft beam edge", buttonStyle);
        if (newFeather != v.flashlightFeather) { v.flashlightFeather = newFeather; changed = true; }
        if (v.flashlightFeather) changed |= Slider("Edge softness", ref v.flashlightSoftness, 0f, 1f, "0.00");
        bool newFlashShadows = GUILayout.Toggle(v.flashlightShadows, " Flashlight shadows", buttonStyle);
        if (newFlashShadows != v.flashlightShadows) { v.flashlightShadows = newFlashShadows; changed = true; }

        GUILayout.Space(8f * unit);
        GUILayout.Label("Other lights", headerStyle);
        bool newExtra = GUILayout.Toggle(v.extraShadows, " Shadows on lamps, torch & lighter (costly)", buttonStyle);
        if (newExtra != v.extraShadows) { v.extraShadows = newExtra; changed = true; }

        GUILayout.Space(10f * unit);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Reset Picture", buttonStyle))
        {
            v.exposure = v.contrast = v.saturation = 0f;
            v.vignette = 0f;
            v.grain = 0f;
            changed = true;
        }
        if (GUILayout.Button("Log Values to Console", buttonStyle)) LogValues();
        GUILayout.EndHorizontal();

        GUILayout.EndScrollView();
        GUILayout.EndArea();

        if (changed) Apply();
    }

    bool Slider(string label, ref float value, float min, float max, string format)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label($"{label}: {value.ToString(format)}", labelStyle, GUILayout.Width(220f * panelUnit));
        float nv = GUILayout.HorizontalSlider(value, min, max);
        GUILayout.EndHorizontal();
        if (Mathf.Approximately(nv, value)) return false;
        value = nv;
        return true;
    }
}
