using UnityEngine;

/// <summary>
/// Esc pauses the game: time and sound stop, the mouse is freed, and a menu offers Resume,
/// Restart (start the run over), Lighting (a live lighting test panel), mouse sensitivity, volume and
/// music volume sliders, and Quit.
/// Settings are remembered between sessions. DarkForestGenerator adds this automatically.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    const string SensitivityKey = "Horror.MouseSensitivity";
    const string VolumeKey = "Horror.Volume";
    const string MenuScaleKey = "Horror.MenuScale";

    FirstPersonController player;
    float sensitivity = 2f;
    float volume = 1f;
    [Tooltip("Size of the pause menu and lighting panel (1 = original size, up to 1.8). Players can change it in the menu.")]
    public float menuScale = 1.6f;
    GUIStyle titleStyle, buttonStyle, labelStyle;
    LightingSettings lighting;
    bool showLighting;

    void Start()
    {
        player = FindAnyObjectByType<FirstPersonController>();
        if (player != null) sensitivity = player.mouseSensitivity;
        sensitivity = PlayerPrefs.GetFloat(SensitivityKey, sensitivity);
        volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        menuScale = PlayerPrefs.GetFloat(MenuScaleKey, menuScale);
        ApplySettings();
        lighting = gameObject.AddComponent<LightingSettings>();
    }

    void OnDestroy()
    {
        if (IsPaused) SetPaused(false);
    }

    void Update()
    {
        if (HorrorInput.ReleaseCursorPressed) SetPaused(!IsPaused);
    }

    void SetPaused(bool paused)
    {
        IsPaused = paused;
        showLighting = false;
        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused;
        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = paused;
        if (!paused)
        {
            PlayerPrefs.SetFloat(SensitivityKey, sensitivity);
            PlayerPrefs.SetFloat(VolumeKey, volume);
            PlayerPrefs.SetFloat(MenuScaleKey, menuScale);
            PlayerPrefs.Save();
        }
    }

    void ApplySettings()
    {
        if (player == null) player = FindAnyObjectByType<FirstPersonController>();
        if (player != null) player.mouseSensitivity = sensitivity;
        AudioListener.volume = volume;
    }

    void OnGUI()
    {
        if (!IsPaused) return;

        // Scale everything (text, buttons, sliders) together: lay out on a virtual 1080p-high screen,
        // then let GUI.matrix blow it up by the menu size.
        float scale = Mathf.Max(0.5f, Screen.height / 1080f * menuScale);
        Matrix4x4 oldMatrix = GUI.matrix;
        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float sw = Screen.width / scale, sh = Screen.height / scale;

        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, fontSize = 44 };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 20 };
            labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft, fontSize = 16 };
        }
        titleStyle.normal.textColor = new Color(0.85f, 0.15f, 0.1f);
        labelStyle.normal.textColor = new Color(0.85f, 0.82f, 0.76f);

        GUI.depth = -2000; // above everything, including the game over fade
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(0f, 0f, sw, sh), Texture2D.whiteTexture);
        GUI.color = Color.white;

        const float w = 360f, h = 50f, gap = 14f;
        float x = (sw - w) * 0.5f;
        float y = Mathf.Max(10f, sh * 0.5f - 310f); // the menu is ~620 tall; keep it on screen

        if (showLighting)
        {
            float panelWidth = Mathf.Min(sw - 40f, 760f);
            var panel = new Rect((sw - panelWidth) * 0.5f, 30f, panelWidth, sh - 120f);
            lighting.DrawPanel(panel, 1f);
            if (GUI.Button(new Rect((sw - w) * 0.5f, sh - 75f, w, h), "Back", buttonStyle))
                showLighting = false;
            GUI.matrix = oldMatrix;
            return;
        }

        GUI.Label(new Rect(0f, y, sw, 70f), "PAUSED", titleStyle);
        y += 90f;

        if (GUI.Button(new Rect(x, y, w, h), "Resume", buttonStyle)) SetPaused(false);
        y += h + gap;

        if (GUI.Button(new Rect(x, y, w, h), "Restart", buttonStyle))
        {
            SetPaused(false);
            GameOverScreen.RestartRun();
        }
        y += h + gap;

        if (GUI.Button(new Rect(x, y, w, h), "Lighting", buttonStyle)) showLighting = true;
        y += h + gap * 2f;

        GUI.Label(new Rect(x, y, w, 26f), $"Mouse sensitivity: {sensitivity:0.0}", labelStyle);
        y += 28f;
        float newSensitivity = GUI.HorizontalSlider(new Rect(x, y, w, 20f), sensitivity, 0.2f, 6f);
        y += 34f;

        GUI.Label(new Rect(x, y, w, 26f), $"Volume: {Mathf.RoundToInt(volume * 100f)}%", labelStyle);
        y += 28f;
        float newVolume = GUI.HorizontalSlider(new Rect(x, y, w, 20f), volume, 0f, 1f);
        y += 34f;

        var music = MusicManager.Instance;
        if (music != null)
        {
            GUI.Label(new Rect(x, y, w, 26f), $"Music: {Mathf.RoundToInt(music.Volume * 100f)}%", labelStyle);
            y += 28f;
            float newMusic = GUI.HorizontalSlider(new Rect(x, y, w, 20f), music.Volume, 0f, 1f);
            if (!Mathf.Approximately(newMusic, music.Volume)) music.Volume = newMusic;
            y += 34f;
        }

        GUI.Label(new Rect(x, y, w, 26f), $"Menu size: {menuScale:0.0}x", labelStyle);
        y += 28f;
        float newScale = GUI.HorizontalSlider(new Rect(x, y, w, 20f), menuScale, 1f, 1.8f);
        y += 40f;

        if (!Mathf.Approximately(newSensitivity, sensitivity) || !Mathf.Approximately(newVolume, volume))
        {
            sensitivity = newSensitivity;
            volume = newVolume;
            ApplySettings();
        }
        menuScale = newScale;

        if (GUI.Button(new Rect(x, y, w, h), "Quit", buttonStyle))
        {
            SetPaused(false);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        GUI.matrix = oldMatrix;
    }
}
