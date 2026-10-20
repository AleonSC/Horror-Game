using UnityEngine;

/// <summary>
/// A lighting test panel, opened from the pause menu: pick a theme, then tweak moonlight, ambient light,
/// fog, flashlight and shadows live. "Log Values" prints the current setup to the Console so a look you
/// like can be made the default. Nothing is saved; "Original" puts back what the scene started with.
/// PauseMenu adds this and draws its panel.
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
        new Theme { name = "Moonlit", moonColor = new Color(0.45f, 0.55f, 0.8f), moonIntensity = 0.25f, moonHeight = 35f, ambient = new Color(0.06f, 0.07f, 0.09f), fogColor = new Color(0.02f, 0.025f, 0.035f), fogDensity = 0.05f, flashlightColor = new Color(1f, 0.95f, 0.85f) },
        new Theme { name = "Pitch Black", moonColor = new Color(0.4f, 0.45f, 0.6f), moonIntensity = 0.03f, moonHeight = 30f, ambient = new Color(0.01f, 0.01f, 0.012f), fogColor = new Color(0.005f, 0.005f, 0.007f), fogDensity = 0.07f, flashlightColor = new Color(1f, 0.93f, 0.8f) },
        new Theme { name = "Blood Moon", moonColor = new Color(0.9f, 0.2f, 0.15f), moonIntensity = 0.35f, moonHeight = 25f, ambient = new Color(0.08f, 0.02f, 0.02f), fogColor = new Color(0.07f, 0.01f, 0.01f), fogDensity = 0.045f, flashlightColor = new Color(1f, 0.9f, 0.8f) },
        new Theme { name = "Foggy", moonColor = new Color(0.6f, 0.65f, 0.7f), moonIntensity = 0.2f, moonHeight = 40f, ambient = new Color(0.1f, 0.1f, 0.11f), fogColor = new Color(0.12f, 0.12f, 0.13f), fogDensity = 0.09f, flashlightColor = new Color(1f, 0.97f, 0.9f) },
        new Theme { name = "Sickly Green", moonColor = new Color(0.5f, 0.8f, 0.4f), moonIntensity = 0.25f, moonHeight = 35f, ambient = new Color(0.04f, 0.07f, 0.03f), fogColor = new Color(0.03f, 0.05f, 0.025f), fogDensity = 0.055f, flashlightColor = new Color(0.9f, 1f, 0.85f) },
        new Theme { name = "Dusk", moonColor = new Color(1f, 0.55f, 0.3f), moonIntensity = 0.6f, moonHeight = 8f, ambient = new Color(0.12f, 0.07f, 0.06f), fogColor = new Color(0.15f, 0.08f, 0.06f), fogDensity = 0.03f, flashlightColor = new Color(1f, 0.95f, 0.85f) },
        new Theme { name = "Bright (testing)", moonColor = new Color(0.7f, 0.75f, 0.9f), moonIntensity = 1f, moonHeight = 50f, ambient = new Color(0.25f, 0.27f, 0.3f), fogColor = new Color(0.05f, 0.06f, 0.08f), fogDensity = 0.015f, flashlightColor = Color.white },
    };

    // Live values (the sliders).
    float moonIntensity, moonHeight, ambientScale = 1f, fogDensity, flashlightIntensity, flashlightRange, flashlightAngle;
    Color moonColor, ambientColor, fogColor, flashlightColor;
    bool fogOn, flashlightShadows, extraShadows;
    int moonShadows; // 0 off, 1 hard, 2 soft
    int selectedTheme = -1;

    Theme original;
    bool captured;
    float lightRefresh;
    Vector2 scroll;
    GUIStyle headerStyle, labelStyle, buttonStyle, selectedStyle;

    static readonly string[] ShadowNames = { "Off", "Hard", "Soft" };

    void Update()
    {
        // New lamps / torches get the extra-shadows setting too.
        if (!captured || !extraShadows) return;
        lightRefresh -= Time.unscaledDeltaTime;
        if (lightRefresh <= 0f)
        {
            lightRefresh = 1f;
            ApplyExtraShadows();
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
        fogOn = RenderSettings.fog;
        moonShadows = moon == null ? 0 : moon.shadows == LightShadows.None ? 0 : moon.shadows == LightShadows.Hard ? 1 : 2;
        flashlightShadows = flashlight == null || flashlight.flashlight.shadows != LightShadows.None;
        flashlightIntensity = flashlight != null ? flashlight.intensity : 12f;
        flashlightRange = flashlight != null ? flashlight.flashlight.range : 45f;
        flashlightAngle = flashlight != null ? flashlight.flashlight.spotAngle : 60f;
        UseTheme(original);
        selectedTheme = -1;
        captured = true;
    }

    void UseTheme(Theme t)
    {
        moonColor = t.moonColor;
        moonIntensity = t.moonIntensity;
        moonHeight = t.moonHeight;
        ambientColor = t.ambient;
        ambientScale = 1f;
        fogColor = t.fogColor;
        fogDensity = t.fogDensity;
        flashlightColor = t.flashlightColor;
    }

    void Apply()
    {
        Light moon = RenderSettings.sun;
        if (moon != null)
        {
            moon.useColorTemperature = false;
            moon.color = moonColor;
            moon.intensity = moonIntensity;
            moon.shadows = moonShadows == 0 ? LightShadows.None : moonShadows == 1 ? LightShadows.Hard : LightShadows.Soft;
            Vector3 e = moon.transform.eulerAngles;
            moon.transform.rotation = Quaternion.Euler(moonHeight, e.y, 0f);
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor * ambientScale;
        RenderSettings.fog = fogOn;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;
        foreach (var cam in Camera.allCameras) cam.backgroundColor = fogColor;

        var flashlight = FindAnyObjectByType<Flashlight>();
        if (flashlight != null)
        {
            flashlight.intensity = flashlightIntensity;
            flashlight.color = flashlightColor;
            flashlight.flashlight.color = flashlightColor;
            flashlight.flashlight.range = flashlightRange;
            flashlight.flashlight.spotAngle = flashlightAngle;
            flashlight.flashlight.innerSpotAngle = flashlightAngle * 0.5f;
            flashlight.flashlight.shadows = flashlightShadows ? LightShadows.Soft : LightShadows.None;
        }

        ApplyExtraShadows();
    }

    /// <summary>Shadows on every point light (lamps, torch, lighter, floodlights are spots and keep theirs).</summary>
    void ApplyExtraShadows()
    {
        foreach (var light in Resources.FindObjectsOfTypeAll<Light>())
            if (light.gameObject.scene.IsValid() && light.type == LightType.Point)
                light.shadows = extraShadows ? LightShadows.Soft : LightShadows.None;
    }

    void LogValues()
    {
        string theme = selectedTheme >= 0 ? themes[selectedTheme].name : "Original";
        Debug.Log($"Lighting values (based on '{theme}'):\n" +
                  $"  moon colour {moonColor}, intensity {moonIntensity:0.00}, height {moonHeight:0}°, shadows {ShadowNames[moonShadows]}\n" +
                  $"  ambient {ambientColor * ambientScale}\n" +
                  $"  fog {(fogOn ? "on" : "off")}, colour {fogColor}, density {fogDensity:0.000}\n" +
                  $"  flashlight intensity {flashlightIntensity:0.0}, range {flashlightRange:0}, angle {flashlightAngle:0}°, colour {flashlightColor}, shadows {flashlightShadows}\n" +
                  $"  lamp / torch shadows {extraShadows}");
    }

    // ---------------------------------------------------------------- Panel (drawn by PauseMenu)

    public void DrawPanel(Rect area, float unit)
    {
        if (!captured) Capture();

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
        if (GUILayout.Button("Original", selectedTheme < 0 ? selectedStyle : buttonStyle)) { selectedTheme = -1; UseTheme(original); changed = true; }
        for (int i = 0; i < themes.Length; i++)
        {
            if (i == 3) { GUILayout.EndHorizontal(); GUILayout.BeginHorizontal(); }
            if (GUILayout.Button(themes[i].name, selectedTheme == i ? selectedStyle : buttonStyle)) { selectedTheme = i; UseTheme(themes[i]); changed = true; }
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(8f * unit);
        GUILayout.Label("Moon", headerStyle);
        changed |= Slider("Brightness", ref moonIntensity, 0f, 2f, "0.00");
        changed |= Slider("Height (degrees)", ref moonHeight, 2f, 90f, "0");
        changed |= Slider("Red", ref moonColor.r, 0f, 1f, "0.00");
        changed |= Slider("Green", ref moonColor.g, 0f, 1f, "0.00");
        changed |= Slider("Blue", ref moonColor.b, 0f, 1f, "0.00");
        GUILayout.Label("Shadows", labelStyle);
        int newShadows = GUILayout.Toolbar(moonShadows, ShadowNames, buttonStyle);
        if (newShadows != moonShadows) { moonShadows = newShadows; changed = true; }

        GUILayout.Space(8f * unit);
        GUILayout.Label("Ambient & fog", headerStyle);
        changed |= Slider("Ambient light", ref ambientScale, 0f, 5f, "0.00");
        bool newFog = GUILayout.Toggle(fogOn, " Fog", buttonStyle);
        if (newFog != fogOn) { fogOn = newFog; changed = true; }
        changed |= Slider("Fog density", ref fogDensity, 0f, 0.15f, "0.000");

        GUILayout.Space(8f * unit);
        GUILayout.Label("Flashlight", headerStyle);
        changed |= Slider("Brightness", ref flashlightIntensity, 0f, 40f, "0.0");
        changed |= Slider("Range", ref flashlightRange, 5f, 100f, "0");
        changed |= Slider("Beam width (degrees)", ref flashlightAngle, 15f, 120f, "0");
        bool newFlashShadows = GUILayout.Toggle(flashlightShadows, " Flashlight shadows", buttonStyle);
        if (newFlashShadows != flashlightShadows) { flashlightShadows = newFlashShadows; changed = true; }

        GUILayout.Space(8f * unit);
        GUILayout.Label("Other lights", headerStyle);
        bool newExtra = GUILayout.Toggle(extraShadows, " Shadows on lamps, torch & lighter (costly)", buttonStyle);
        if (newExtra != extraShadows) { extraShadows = newExtra; changed = true; }

        GUILayout.Space(10f * unit);
        if (GUILayout.Button("Log Values to Console", buttonStyle)) LogValues();

        GUILayout.EndScrollView();
        GUILayout.EndArea();

        if (changed) Apply();
    }

    bool Slider(string label, ref float value, float min, float max, string format)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label($"{label}: {value.ToString(format)}", labelStyle, GUILayout.Width(220f * Mathf.Max(1f, Screen.height / 1080f)));
        float v = GUILayout.HorizontalSlider(value, min, max);
        GUILayout.EndHorizontal();
        if (Mathf.Approximately(v, value)) return false;
        value = v;
        return true;
    }
}
