using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

/// <summary>
/// Esc pauses the game: time and sound stop, the mouse is freed, and a cinematic menu appears: black
/// letterbox bars slide in, the game stays visible behind, and plain left-aligned items (Return to Game,
/// Restart, Options, Quit) light up under the pointer. Options has mouse sensitivity, master volume,
/// sound effects and music sliders, and motion blur (on/off and strength). (The lighting
/// panel is a developer tool now, in DevTools.) The menu is laid out on a virtual 1600x900 screen and
/// scaled to fit whatever the real one is, its text drawn at the scaled size so it stays sharp.
/// Settings are remembered between sessions. DarkForestGenerator adds this automatically.
/// </summary>
public class PauseMenu : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Inspector changes made in Play are kept)

    public static bool IsPaused { get; private set; }
    /// <summary>The sound effects volume from the settings (the listener's volume, before anything ducks it).</summary>
    public static float ListenerVolume { get; private set; } = 1f;
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() => IsPaused = false;

    const string SensitivityKey = "Horror.MouseSensitivity";
    const string VolumeKey = "Horror.Volume";
    const string EffectsKey = "Horror.EffectsVolume";

    /// <summary>The master volume (everything, music included). MusicManager scales by this.</summary>
    public static float MasterVolume { get; private set; } = 1f;
    const string MotionBlurKey = "Horror.MotionBlur", MotionBlurStrengthKey = "Horror.MotionBlurStrength";

    FirstPersonController player;
    float sensitivity = 2f;
    float volume = 1f;          // master
    float effectsVolume = 1f;   // every sound but the music
    [Tooltip("Vertical field of view (degrees). Wide angles stretch the edges of the view: looking down a slope, trees there seem to lean outward.")]
    public float fieldOfView = 60f;
    [Tooltip("Motion blur as the view turns and moves (off by default; players can turn it on in Options).")]
    public bool motionBlur;
    [Range(0f, 1f)] public float motionBlurStrength = 0.4f;
    Volume motionBlurVolume;
    MotionBlur motionBlurFx;

    void Start()
    {
        player = FindAnyObjectByType<FirstPersonController>();
        if (player != null) sensitivity = player.mouseSensitivity;
        sensitivity = PlayerPrefs.GetFloat(SensitivityKey, sensitivity);
        volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        effectsVolume = PlayerPrefs.GetFloat(EffectsKey, 1f);
        // (Field of view isn't a player setting: always the default, changed only from the dev tools.)
        motionBlur = PlayerPrefs.GetInt(MotionBlurKey, motionBlur ? 1 : 0) == 1;
        motionBlurStrength = PlayerPrefs.GetFloat(MotionBlurStrengthKey, motionBlurStrength);
        ApplySettings();
        gameObject.AddComponent<LightingSettings>(); // applies the saved lighting; DevTools shows its panel
    }

    void OnDestroy()
    {
        if (IsPaused) SetPaused(false);
    }

    void Update()
    {
        if (MainMenu.Showing) return; // (no pausing the main menu)
        // (Esc closes the inventory first, if it's open.)
        if (HorrorInput.ReleaseCursorPressed && !InventoryScreen.IsOpen && !InventoryScreen.ClosedThisFrame) SetPaused(!IsPaused);
        UpdatePauseSong();
    }

    // ---------------------------------------------------------------- The pause song

    AudioSource song;
    AudioEchoFilter songEcho;
    float songLevel, songEchoMix;

    /// <summary>
    /// While paused, a creepy, warped old song plays as if from a radio somewhere. Unpause and it doesn't
    /// stop dead: it fades away into its own echo.
    /// </summary>
    void UpdatePauseSong()
    {
        if (song == null)
        {
            // On its own object: the echo filter would otherwise also sit on the menu's clipless UI source.
            var songObject = new GameObject("Pause Song");
            songObject.transform.SetParent(transform, false);
            song = ProceduralAudio.AddSource(songObject, ProceduralAudio.PauseSong, true, false, 0f);
            song.ignoreListenerPause = true;  // it plays while everything else is paused
            song.ignoreListenerVolume = true; // music: the music volume sets it
            songEcho = songObject.AddComponent<AudioEchoFilter>();
            songEcho.delay = 420f;
            songEcho.decayRatio = 0.6f;
            songEcho.wetMix = 0f;
            songEcho.dryMix = 1f;
        }
        float dt = Time.unscaledDeltaTime;
        if (IsPaused)
        {
            if (!song.isPlaying)
            {
                song.time = Random.Range(0f, song.clip.length * 0.9f);
                song.Play();
            }
            songLevel = Mathf.MoveTowards(songLevel, 1f, dt / 1.2f);
            songEchoMix = Mathf.MoveTowards(songEchoMix, 0.15f, dt);
        }
        else
        {
            // Fading out, the dry sound going first and the echo lingering after it.
            songLevel = Mathf.MoveTowards(songLevel, 0f, dt / 3f);
            songEchoMix = Mathf.MoveTowards(songEchoMix, 1f, dt * 1.5f);
            if (songLevel <= 0f && song.isPlaying) song.Stop();
        }
        var music = MusicManager.Instance;
        float musicVolume = music != null ? music.Volume : 0.6f;
        song.volume = MasterVolume * musicVolume * 0.9f * Mathf.SmoothStep(0f, 1f, songLevel);
        songEcho.wetMix = songEchoMix * 0.8f;
        songEcho.dryMix = Mathf.Lerp(1f, 0.25f, songEchoMix);
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
            PlayerPrefs.SetInt(MotionBlurKey, motionBlur ? 1 : 0);
            PlayerPrefs.SetFloat(MotionBlurStrengthKey, motionBlurStrength);
            PlayerPrefs.Save();
        }
    }

    /// <summary>Dev tools: sets the field of view (degrees) for this session; it isn't saved.</summary>
    public void SetFieldOfView(float degrees)
    {
        fieldOfView = Mathf.Clamp(degrees, 30f, 120f);
        ApplySettings();
    }

    void ApplySettings()
    {
        if (player == null) player = FindAnyObjectByType<FirstPersonController>();
        if (player != null) player.mouseSensitivity = sensitivity;
        // The listener volume carries the sound effects; the music ignores it and uses MasterVolume itself.
        MasterVolume = volume;
        ListenerVolume = volume * effectsVolume;
        AudioListener.volume = ListenerVolume * (1f - EntityDread.Hush); // (kept up to date by EntityDread while one is on you)
        // Not while the dream hallway has the view stretched: it picks this up when it lets go.
        var cam = player != null ? player.GetComponentInChildren<Camera>() : Camera.main;
        if (cam != null && (player == null || player.DreamStretch <= 0f)) cam.fieldOfView = fieldOfView;
        ApplyMotionBlur(cam);
    }

    /// <summary>Motion blur on a global volume (turning, moving, and things rushing past), on or off and how strong.</summary>
    void ApplyMotionBlur(Camera cam)
    {
        if (motionBlurVolume == null)
        {
            motionBlurVolume = new GameObject("Motion Blur").AddComponent<Volume>();
            motionBlurVolume.transform.SetParent(transform, false);
            motionBlurVolume.isGlobal = true;
            motionBlurVolume.priority = 95f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            motionBlurVolume.profile = profile;
            motionBlurFx = profile.Add<MotionBlur>(true);
        }
        motionBlurFx.intensity.Override(motionBlur ? motionBlurStrength : 0f);
        motionBlurVolume.enabled = motionBlur && motionBlurStrength > 0f;
    }

    // ---------------------------------------------------------------- Drawing

    enum Page { Main, Options }
    Page page = Page.Main;
    float openedAt;
    int hovered = -1, lastHovered = -1;
    GUIStyle itemStyle, smallStyle, valueStyle;
    AudioSource uiSound;
    int draggingSlider = -1;

    static readonly Color ItemColor = new Color(0.78f, 0.76f, 0.72f, 0.75f);
    static readonly Color HoverColor = new Color(1f, 0.98f, 0.94f, 1f);
    static readonly Color AccentColor = new Color(0.75f, 0.08f, 0.06f, 1f);

    // Everything is laid out on a virtual screen this size, then scaled to fit the real one (keeping its
    // proportions): s is virtual units to pixels. Text is drawn at the scaled font size, not stretched.
    const float VirtualWidth = 1600f, VirtualHeight = 900f;
    float s = 1f, vw = VirtualWidth, vh = VirtualHeight;
    Rect S(Rect r) => new Rect(r.x * s, r.y * s, r.width * s, r.height * s);
    Vector2 Mouse => Event.current.mousePosition / s;
    void VFill(Rect r, Color c) => Fill(S(r), c);

    void OnGUI()
    {
        if (!IsPaused) return;
        if (Event.current.type == EventType.Layout && openedAt <= 0f) openedAt = Time.unscaledTime;
        GUI.depth = -2000; // above everything, including the game over fade
        float sw = Screen.width, sh = Screen.height;
        s = Mathf.Max(0.4f, Mathf.Min(sw / VirtualWidth, sh / VirtualHeight));
        vw = sw / s;
        vh = sh / s;

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

        EnsureStyles();
        GUI.color = new Color(1f, 1f, 1f, open);

        float left = Mathf.Max(70f, vw * 0.07f);
        float top = vh * 0.13f + 80f;
        hovered = -1;

        if (page == Page.Main) DrawMain(left, top);
        else DrawOptions(left, top);

        // A soft tick when the pointer moves onto a new item.
        if (Event.current.type == EventType.Repaint && hovered != lastHovered)
        {
            if (hovered >= 0) Tick(0.25f, 1.6f);
            lastHovered = hovered;
        }

        GUI.color = Color.white;
    }

    void DrawMain(float x, float y)
    {
        const float step = 50f;
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
        y -= 20f; // a little more room: there's more on this page
        GUI.Label(S(new Rect(x, y - 46f, 600f, 36f)), "OPTIONS", smallStyle);
        const float step = 56f;
        const float width = 380f;

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

        if (Item(12, motionBlur ? "MOTION BLUR: ON" : "MOTION BLUR: OFF", x, y))
        {
            motionBlur = !motionBlur;
            ApplySettings();
        }
        y += 48f;
        if (motionBlur)
        {
            float newBlur = Slider(6, "MOTION BLUR STRENGTH", $"{Mathf.RoundToInt(motionBlurStrength * 100f)}%", x, y, width, motionBlurStrength, 0f, 1f);
            if (!Mathf.Approximately(newBlur, motionBlurStrength)) { motionBlurStrength = newBlur; ApplySettings(); }
            y += step;
        }

        if (!Mathf.Approximately(newSensitivity, sensitivity) || !Mathf.Approximately(newVolume, volume) || !Mathf.Approximately(newEffects, effectsVolume))
        {
            sensitivity = newSensitivity;
            volume = newVolume;
            effectsVolume = newEffects;
            ApplySettings();
        }

        bool classic = HdrpAtmosphere.ClassicLook;
        if (Item(13, classic ? "GRAPHICS: CLASSIC" : "GRAPHICS: ENHANCED", x, y)) HdrpAtmosphere.ClassicLook = !classic;
        y += 48f;
        if (!classic)
        {
            if (HdrpAtmosphere.RayTracingAvailable)
            {
                bool rt = HdrpAtmosphere.RayTracingWanted;
                if (Item(14, rt ? "RAY TRACING: ON" : "RAY TRACING: OFF", x, y)) HdrpAtmosphere.RayTracingWanted = !rt;
                y += 48f;
            }
            else
            {
                // Shown anyway, with why, so it's clear it isn't just missing.
                GUI.Label(S(new Rect(x, y, 900f, 30f)), "RAY TRACING: NOT AVAILABLE", smallStyle);
                GUI.Label(S(new Rect(x, y + 26f, 900f, 30f)), HdrpAtmosphere.RayTracingProblem, valueStyle);
                y += 62f;
            }
        }

        y += 24f;
        if (Item(11, "BACK", x, y)) page = Page.Main;
    }

    /// <summary>A menu entry: plain text that brightens, gains a red mark and slides right under the pointer.</summary>
    bool Item(int id, string text, float x, float y)
    {
        Vector2 size = itemStyle.CalcSize(new GUIContent(text)) / s;
        var hit = new Rect(x - 20f, y - 4f, size.x + 60f, size.y + 8f);
        bool over = hit.Contains(Mouse);
        if (over) hovered = id;

        float a = GUI.color.a;
        float shift = over ? 10f : 0f;
        if (over) VFill(new Rect(x - 14f, y + size.y * 0.5f - 1.5f, 6f, 3f), new Color(AccentColor.r, AccentColor.g, AccentColor.b, a));
        itemStyle.normal.textColor = over ? HoverColor : ItemColor;
        GUI.Label(S(new Rect(x + shift + 1.5f, y + 1.5f, size.x + 20f, size.y)), $"<color=#000000AA>{text}</color>", itemStyle);
        GUI.Label(S(new Rect(x + shift, y, size.x + 20f, size.y)), text, itemStyle);

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
        Vector2 mouse = Mouse;
        bool over = new Rect(x - 10f, y - 2f, width + 120f, 50f).Contains(mouse);
        if (over) hovered = 100 + id;

        smallStyle.normal.textColor = over || draggingSlider == id ? HoverColor : ItemColor;
        GUI.Label(S(labelRect), label, smallStyle);
        valueStyle.normal.textColor = smallStyle.normal.textColor;
        GUI.Label(S(new Rect(x + width + 16f, y + 24f, 110f, 28f)), value, valueStyle);

        float t = Mathf.InverseLerp(min, max, current);
        float a = GUI.color.a;
        VFill(new Rect(track.x, track.y + 6f, track.width, 2f), new Color(1f, 1f, 1f, 0.2f * a));
        VFill(new Rect(track.x, track.y + 6f, track.width * t, 2f), new Color(HoverColor.r, HoverColor.g, HoverColor.b, 0.85f * a));
        VFill(new Rect(track.x + track.width * t - 2f, track.y, 4f, 14f), new Color(AccentColor.r, AccentColor.g, AccentColor.b, a));

        var e = Event.current;
        var grab = new Rect(track.x - 8f, track.y - 8f, track.width + 16f, track.height + 16f);
        if (e.type == EventType.MouseDown && e.button == 0 && grab.Contains(mouse)) { draggingSlider = id; e.Use(); }
        if (draggingSlider == id && (e.type == EventType.MouseDrag || e.type == EventType.MouseDown))
        {
            current = Mathf.Lerp(min, max, Mathf.Clamp01((mouse.x - track.x) / track.width));
            e.Use();
        }
        if (draggingSlider == id && e.type == EventType.MouseUp) { draggingSlider = -1; e.Use(); Tick(0.3f, 1.3f); }
        return current;
    }

    void EnsureStyles()
    {
        if (itemStyle == null)
        {
            itemStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft, richText = true, wordWrap = false };
            smallStyle = new GUIStyle(itemStyle);
            valueStyle = new GUIStyle(smallStyle) { fontStyle = FontStyle.Normal };
        }
        // Sized to the screen every frame, so the text is drawn sharp at the size it's shown.
        itemStyle.fontSize = Mathf.Max(8, Mathf.RoundToInt(30f * s));
        smallStyle.fontSize = valueStyle.fontSize = Mathf.Max(7, Mathf.RoundToInt(21f * s));
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
