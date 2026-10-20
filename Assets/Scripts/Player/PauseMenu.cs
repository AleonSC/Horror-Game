using UnityEngine;

/// <summary>
/// Esc pauses the game: time and sound stop, the mouse is freed, and a cinematic menu appears: black
/// letterbox bars slide in, the game stays visible behind, and plain left-aligned items (Return to Game,
/// Restart, Options, Quit) light up under the pointer. Options has mouse sensitivity, master volume,
/// sound effects, music and menu size sliders, and the Lighting test panel.
/// Settings are remembered between sessions. DarkForestGenerator adds this automatically.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    public static bool IsPaused { get; private set; }

    const string SensitivityKey = "Horror.MouseSensitivity";
    const string VolumeKey = "Horror.Volume";
    const string EffectsKey = "Horror.EffectsVolume";

    /// <summary>The master volume (everything, music included). MusicManager scales by this.</summary>
    public static float MasterVolume { get; private set; } = 1f;
    const string MenuScaleKey = "Horror.MenuScale";
    const string FovKey = "Horror.FieldOfView";

    FirstPersonController player;
    float sensitivity = 2f;
    float volume = 1f;          // master
    float effectsVolume = 1f;   // every sound but the music
    [Tooltip("Vertical field of view (degrees). Wide angles stretch the edges of the view: looking down a slope, trees there seem to lean outward.")]
    public float fieldOfView = 60f;
    [Tooltip("Size of the pause menu and lighting panel (1 = original size, up to 1.8). Players can change it in the menu.")]
    public float menuScale = 1.6f;
    LightingSettings lighting;

    void Start()
    {
        player = FindAnyObjectByType<FirstPersonController>();
        if (player != null) sensitivity = player.mouseSensitivity;
        sensitivity = PlayerPrefs.GetFloat(SensitivityKey, sensitivity);
        volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        effectsVolume = PlayerPrefs.GetFloat(EffectsKey, 1f);
        menuScale = PlayerPrefs.GetFloat(MenuScaleKey, menuScale);
        fieldOfView = PlayerPrefs.GetFloat(FovKey, fieldOfView);
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
        page = Page.Main;
        openedAt = 0f;
        draggingSlider = -1;
        Time.timeScale = paused ? 0f : 1f;
        AudioListener.pause = paused;
        Cursor.lockState = paused ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = paused;
        if (!paused)
        {
            PlayerPrefs.SetFloat(SensitivityKey, sensitivity);
            PlayerPrefs.SetFloat(VolumeKey, volume);
            PlayerPrefs.SetFloat(EffectsKey, effectsVolume);
            PlayerPrefs.SetFloat(MenuScaleKey, menuScale);
            PlayerPrefs.SetFloat(FovKey, fieldOfView);
            PlayerPrefs.Save();
        }
    }

    void ApplySettings()
    {
        if (player == null) player = FindAnyObjectByType<FirstPersonController>();
        if (player != null) player.mouseSensitivity = sensitivity;
        // The listener volume carries the sound effects; the music ignores it and uses MasterVolume itself.
        MasterVolume = volume;
        AudioListener.volume = volume * effectsVolume;
        // Not while the dream hallway has the view stretched: it picks this up when it lets go.
        var cam = player != null ? player.GetComponentInChildren<Camera>() : Camera.main;
        if (cam != null && (player == null || player.DreamStretch <= 0f)) cam.fieldOfView = fieldOfView;
    }

    // ---------------------------------------------------------------- Drawing

    enum Page { Main, Options, Lighting }
    Page page = Page.Main;
    float openedAt;
    int hovered = -1, lastHovered = -1;
    GUIStyle itemStyle, smallStyle, valueStyle;
    AudioSource uiSound;
    int draggingSlider = -1;

    static readonly Color ItemColor = new Color(0.78f, 0.76f, 0.72f, 0.75f);
    static readonly Color HoverColor = new Color(1f, 0.98f, 0.94f, 1f);
    static readonly Color AccentColor = new Color(0.75f, 0.08f, 0.06f, 1f);

    void OnGUI()
    {
        if (!IsPaused) return;
        if (Event.current.type == EventType.Layout && openedAt <= 0f) openedAt = Time.unscaledTime;

        // Lay everything out on a virtual 1080p-high screen, scaled by the menu size.
        float scale = Mathf.Max(0.5f, Screen.height / 1080f * menuScale);
        Matrix4x4 oldMatrix = GUI.matrix;
        GUI.depth = -2000; // above everything, including the game over fade
        float sw = Screen.width, sh = Screen.height;

        // Cinematic: black bars slide in top and bottom; the game stays visible between them, dimmed toward
        // the left where the menu sits.
        float open = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - openedAt) / 0.35f));
        float bar = sh * 0.13f * open;
        Fill(new Rect(0f, 0f, sw, bar), Color.black);
        Fill(new Rect(0f, sh - bar, sw, bar), Color.black);
        for (int i = 0; i < 24; i++)
        {
            float x0 = sw * 0.55f * i / 24f, x1 = sw * 0.55f * (i + 1) / 24f;
            Fill(new Rect(x0, bar, x1 - x0 + 1f, sh - bar * 2f), new Color(0f, 0f, 0f, 0.55f * open * (1f - i / 24f)));
        }
        Fill(new Rect(0f, bar, sw, sh - bar * 2f), new Color(0f, 0f, 0f, 0.15f * open));

        GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
        float vw = sw / scale, vh = sh / scale;
        EnsureStyles();
        GUI.color = new Color(1f, 1f, 1f, open);

        float left = Mathf.Max(60f, vw * 0.07f);
        float top = vh * 0.13f + 70f;
        hovered = -1;

        if (page == Page.Main) DrawMain(left, top);
        else if (page == Page.Options) DrawOptions(left, top);
        else
        {
            float panelWidth = Mathf.Min(vw - left * 2f, 760f);
            lighting.DrawPanel(new Rect(left, vh * 0.13f + 20f, panelWidth, vh * 0.74f - 90f), 1f);
            if (Item(0, "BACK", left, vh * 0.87f - 60f)) page = Page.Options;
        }

        // A soft tick when the pointer moves onto a new item.
        if (Event.current.type == EventType.Repaint && hovered != lastHovered)
        {
            if (hovered >= 0) Tick(0.25f, 1.6f);
            lastHovered = hovered;
        }

        GUI.color = Color.white;
        GUI.matrix = oldMatrix;
    }

    void DrawMain(float x, float y)
    {
        const float step = 46f;
        if (Item(0, "RETURN TO GAME", x, y)) { SetPaused(false); return; }
        y += step;
        if (Item(1, "RESTART", x, y))
        {
            SetPaused(false);
            GameOverScreen.RestartRun();
            return;
        }
        y += step;
        if (Item(2, "OPTIONS", x, y)) page = Page.Options;
        y += step * 1.6f;
        if (Item(3, "QUIT TO DESKTOP", x, y))
        {
            SetPaused(false);
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }

    void DrawOptions(float x, float y)
    {
        GUI.Label(new Rect(x, y - 52f, 600f, 40f), "OPTIONS", smallStyle);
        const float step = 58f;
        const float width = 360f;

        float newSensitivity = Slider(0, "MOUSE SENSITIVITY", $"{sensitivity:0.0}", x, y, width, sensitivity, 0.2f, 6f); y += step;
        float newVolume = Slider(1, "MASTER VOLUME", $"{Mathf.RoundToInt(volume * 100f)}%", x, y, width, volume, 0f, 1f); y += step;
        float newEffects = Slider(2, "SOUND EFFECTS", $"{Mathf.RoundToInt(effectsVolume * 100f)}%", x, y, width, effectsVolume, 0f, 1f); y += step;
        var music = MusicManager.Instance;
        if (music != null)
        {
            float newMusic = Slider(3, "MUSIC", $"{Mathf.RoundToInt(music.Volume * 100f)}%", x, y, width, music.Volume, 0f, 1f);
            if (!Mathf.Approximately(newMusic, music.Volume)) music.Volume = newMusic;
            y += step;
        }
        float newFov = Mathf.Round(Slider(5, "FIELD OF VIEW", $"{Mathf.RoundToInt(fieldOfView)}°", x, y, width, fieldOfView, 50f, 90f)); y += step;
        menuScale = Slider(4, "MENU SIZE", $"{menuScale:0.0}x", x, y, width, menuScale, 1f, 1.8f); y += step;

        if (!Mathf.Approximately(newSensitivity, sensitivity) || !Mathf.Approximately(newVolume, volume) || !Mathf.Approximately(newEffects, effectsVolume)
            || !Mathf.Approximately(newFov, fieldOfView))
        {
            sensitivity = newSensitivity;
            volume = newVolume;
            effectsVolume = newEffects;
            fieldOfView = newFov;
            ApplySettings();
        }

        y += 10f;
        if (Item(10, "LIGHTING", x, y)) page = Page.Lighting;
        y += 46f * 1.5f;
        if (Item(11, "BACK", x, y)) page = Page.Main;
    }

    /// <summary>A menu entry: plain text that brightens, gains a red mark and slides right under the pointer.</summary>
    bool Item(int id, string text, float x, float y)
    {
        Vector2 size = itemStyle.CalcSize(new GUIContent(text));
        var hit = new Rect(x - 20f, y - 4f, size.x + 60f, size.y + 8f);
        bool over = hit.Contains(Event.current.mousePosition);
        if (over) hovered = id;

        float a = GUI.color.a;
        float shift = over ? 10f : 0f;
        if (over) Fill(new Rect(x - 14f, y + size.y * 0.5f - 1.5f, 6f, 3f), new Color(AccentColor.r, AccentColor.g, AccentColor.b, a));
        itemStyle.normal.textColor = over ? HoverColor : ItemColor;
        GUI.Label(new Rect(x + shift + 1.5f, y + 1.5f, size.x + 20f, size.y), $"<color=#000000AA>{text}</color>", itemStyle);
        GUI.Label(new Rect(x + shift, y, size.x + 20f, size.y), text, itemStyle);

        if (over && Event.current.type == EventType.MouseUp && Event.current.button == 0)
        {
            Event.current.Use();
            Tick(0.5f, 1f);
            return true;
        }
        return false;
    }

    /// <summary>A label and a thin line slider beneath it, dragged with the mouse.</summary>
    float Slider(int id, string label, string value, float x, float y, float width, float current, float min, float max)
    {
        var track = new Rect(x, y + 32f, width, 14f);
        var labelRect = new Rect(x, y, width, 28f);
        bool over = new Rect(x - 10f, y - 2f, width + 120f, 50f).Contains(Event.current.mousePosition);
        if (over) hovered = 100 + id;

        smallStyle.normal.textColor = over || draggingSlider == id ? HoverColor : ItemColor;
        GUI.Label(labelRect, label, smallStyle);
        valueStyle.normal.textColor = smallStyle.normal.textColor;
        GUI.Label(new Rect(x + width + 16f, y + 24f, 100f, 28f), value, valueStyle);

        float t = Mathf.InverseLerp(min, max, current);
        float a = GUI.color.a;
        Fill(new Rect(track.x, track.y + 6f, track.width, 2f), new Color(1f, 1f, 1f, 0.2f * a));
        Fill(new Rect(track.x, track.y + 6f, track.width * t, 2f), new Color(HoverColor.r, HoverColor.g, HoverColor.b, 0.85f * a));
        Fill(new Rect(track.x + track.width * t - 2f, track.y, 4f, 14f), new Color(AccentColor.r, AccentColor.g, AccentColor.b, a));

        var e = Event.current;
        var grab = new Rect(track.x - 8f, track.y - 8f, track.width + 16f, track.height + 16f);
        if (e.type == EventType.MouseDown && e.button == 0 && grab.Contains(e.mousePosition)) { draggingSlider = id; e.Use(); }
        if (draggingSlider == id && (e.type == EventType.MouseDrag || e.type == EventType.MouseDown))
        {
            current = Mathf.Lerp(min, max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width));
            e.Use();
        }
        if (draggingSlider == id && e.type == EventType.MouseUp) { draggingSlider = -1; e.Use(); Tick(0.3f, 1.3f); }
        return current;
    }

    void EnsureStyles()
    {
        if (itemStyle != null) return;
        itemStyle = new GUIStyle(GUI.skin.label) { fontSize = 26, fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft, richText = true, wordWrap = false };
        smallStyle = new GUIStyle(itemStyle) { fontSize = 19 };
        valueStyle = new GUIStyle(smallStyle) { fontStyle = FontStyle.Normal };
    }

    static void Fill(Rect r, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    /// <summary>A quiet UI tick that plays even though the game's sound is paused.</summary>
    void Tick(float volumeScale, float pitch)
    {
        if (uiSound == null)
        {
            uiSound = ProceduralAudio.AddSource(gameObject, null, false, false, 1f);
            uiSound.ignoreListenerPause = true;
        }
        uiSound.pitch = pitch;
        uiSound.PlayOneShot(ProceduralAudio.FlashlightClick, 0.35f * volumeScale);
    }
}
