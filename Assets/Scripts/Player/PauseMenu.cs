using UnityEngine;

/// <summary>
/// Esc pauses the game: time and sound stop, the mouse is freed, and a menu offers Resume,
/// Restart (start the run over), Lighting (a live lighting test panel), mouse sensitivity and
/// volume sliders, and Quit.
/// Settings are remembered between sessions. DarkForestGenerator adds this automatically.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    const string SensitivityKey = "Horror.MouseSensitivity";
    const string VolumeKey = "Horror.Volume";

    FirstPersonController player;
    float sensitivity = 2f;
    float volume = 1f;
    GUIStyle titleStyle, buttonStyle, labelStyle;
    LightingSettings lighting;
    bool showLighting;

    void Start()
    {
        player = FindAnyObjectByType<FirstPersonController>();
        if (player != null) sensitivity = player.mouseSensitivity;
        sensitivity = PlayerPrefs.GetFloat(SensitivityKey, sensitivity);
        volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
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

        float unit = Mathf.Max(1f, Screen.height / 1080f);
        if (titleStyle == null)
        {
            titleStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            buttonStyle = new GUIStyle(GUI.skin.button);
            labelStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleLeft };
        }
        titleStyle.fontSize = Mathf.RoundToInt(44f * unit);
        titleStyle.normal.textColor = new Color(0.85f, 0.15f, 0.1f);
        buttonStyle.fontSize = Mathf.RoundToInt(20f * unit);
        labelStyle.fontSize = Mathf.RoundToInt(16f * unit);
        labelStyle.normal.textColor = new Color(0.85f, 0.82f, 0.76f);

        GUI.depth = -2000; // above everything, including the game over fade
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;

        float w = 360f * unit, h = 50f * unit, gap = 14f * unit;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height * 0.5f - 230f * unit;

        if (showLighting)
        {
            float panelWidth = Mathf.Min(Screen.width - 40f * unit, 760f * unit);
            var panel = new Rect((Screen.width - panelWidth) * 0.5f, 40f * unit, panelWidth, Screen.height - 140f * unit);
            lighting.DrawPanel(panel, unit);
            if (GUI.Button(new Rect((Screen.width - w) * 0.5f, Screen.height - 85f * unit, w, h), "Back", buttonStyle))
                showLighting = false;
            return;
        }

        GUI.Label(new Rect(0f, y, Screen.width, 70f * unit), "PAUSED", titleStyle);
        y += 100f * unit;

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

        GUI.Label(new Rect(x, y, w, 26f * unit), $"Mouse sensitivity: {sensitivity:0.0}", labelStyle);
        y += 28f * unit;
        float newSensitivity = GUI.HorizontalSlider(new Rect(x, y, w, 20f * unit), sensitivity, 0.2f, 6f);
        y += 34f * unit;

        GUI.Label(new Rect(x, y, w, 26f * unit), $"Volume: {Mathf.RoundToInt(volume * 100f)}%", labelStyle);
        y += 28f * unit;
        float newVolume = GUI.HorizontalSlider(new Rect(x, y, w, 20f * unit), volume, 0f, 1f);
        y += 40f * unit;

        if (!Mathf.Approximately(newSensitivity, sensitivity) || !Mathf.Approximately(newVolume, volume))
        {
            sensitivity = newSensitivity;
            volume = newVolume;
            ApplySettings();
        }

        if (GUI.Button(new Rect(x, y, w, h), "Quit", buttonStyle))
        {
            SetPaused(false);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
