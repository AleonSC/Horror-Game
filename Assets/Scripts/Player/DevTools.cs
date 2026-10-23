using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Developer tools, in a window toggled with F9 (the mouse is freed while it's open; you can still move).
/// Three tabs:
///   - Game: invincibility (you become a safe spot: nothing can take you, and the scours circle round you
///     the way they circle a lamp), field of view, how many lamps are lit (going up lights the nearest dark
///     ones for real, so everything tied to lamps follows; going down puts the furthest out), the stick of
///     truth, the F1 overlay, the model editor (F10), the session recorder (F11), and restarting the run.
///   - Lighting: the lighting panel (LightingSettings: themes, moon, fog, sky, flashlight, picture).
///   - Look: the finer detail (LookSettings: exposure, bloom, volumetrics, shadows, the mist, lamps, sky),
///     with Copy settings to paste the whole look to Claude, and Paste settings to read it back.
/// The window's look is DevSkin. DarkForestGenerator adds this.
/// </summary>
public class DevTools : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Inspector changes made in Play are kept)

    public static bool IsOpen { get; private set; }

    /// <summary>
    /// Invincible (a dev tool): you're a safe spot, so nothing can take you, and scours prowl round you the
    /// way they prowl round a lit lamp. Not saved.
    /// </summary>
    public static bool Invincible
    {
        get => invincible;
        set
        {
            if (invincible == value) return;
            invincible = value;
            if (value) SafeZones.Register(SafeSpot); else SafeZones.Unregister(SafeSpot);
            SessionRecorder.Log("Dev", value ? "invincible on (a safe spot)" : "invincible off");
        }
    }
    static bool invincible;

    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics()
    {
        IsOpen = false;
        invincible = false;
        SafeSpot.player = null;
    }

    [Tooltip("While invincible: how far round you the safe spot reaches (the scours circle just outside it).")]
    public float safeSpotRadius = 4f;

    /// <summary>The safe spot that follows you while you're invincible.</summary>
    class PlayerSafeSpot : SafeZones.ISource
    {
        public Transform player;
        public float radius = 4f;
        public bool IsActive => invincible && player != null;
        public Vector3 Center => player != null ? player.position : Vector3.zero;
        public float Radius => radius;
    }
    static readonly PlayerSafeSpot SafeSpot = new PlayerSafeSpot();

    const string TabKey = "Horror.DevTab";
    static readonly string[] TabNames = { "Game", "Lighting", "Look" };

    int lampTarget = -1;
    int tab;
    LightingSettings lighting;
    PauseMenu pause;
    Hotbar hotbar;
    SessionRecorder recorder;
    FirstPersonController playerBody;
    Vector2 scroll, lookScroll;
    string filter = "";
    readonly HashSet<string> closedSections = new HashSet<string>();
    string status;
    float statusTime;
    bool statusBad;
    float saveTimer;

    IEnumerator Start()
    {
        tab = Mathf.Clamp(PlayerPrefs.GetInt(TabKey, 0), 0, TabNames.Length - 1);
        // The saved look goes back on once everything it touches has been built.
        yield return null;
        yield return new WaitForSeconds(0.5f);
        LookSettings.ApplySaved();
    }

    void Update()
    {
        if (playerBody == null) playerBody = FindAnyObjectByType<FirstPersonController>();
        SafeSpot.player = playerBody != null ? playerBody.transform : null;
        SafeSpot.radius = safeSpotRadius;

        saveTimer -= Time.unscaledDeltaTime;
        if (saveTimer <= 0f) { saveTimer = 2f; LookSettings.SaveIfChanged(); }

        if (!HorrorInput.DevToolsPressed) return;
        SetOpen(!IsOpen);
    }

    void SetOpen(bool open)
    {
        IsOpen = open;
        if (open) lampTarget = Lamp.LitCount;
        else LookSettings.SaveIfChanged();
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;
    }

    void OnDisable()
    {
        if (IsOpen) IsOpen = false;
        LookSettings.SaveIfChanged();
    }

    /// <summary>Closes the window (the model editor takes over the screen and the mouse).</summary>
    public static void Close() => IsOpen = false;

    /// <summary>Lights or puts out lamps until exactly this many are lit.</summary>
    static void SetLampsLit(int count)
    {
        var player = FindAnyObjectByType<FirstPersonController>();
        Vector3 at = player != null ? player.transform.position : Vector3.zero;
        var lamps = new List<Lamp>(Lamp.All);
        lamps.Sort((a, b) => Dist(a, at).CompareTo(Dist(b, at)));
        count = Mathf.Clamp(count, 0, lamps.Count);

        int lit = 0;
        foreach (var lamp in lamps) if (lamp.IsLit) lit++;
        // Up: the nearest dark ones first.
        foreach (var lamp in lamps)
        {
            if (lit >= count) break;
            if (lamp.IsLit) continue;
            lamp.ForceLight();
            lit++;
        }
        // Down: the furthest lit ones go out.
        for (int i = lamps.Count - 1; i >= 0 && lit > count; i--)
        {
            if (!lamps[i].IsLit) continue;
            lamps[i].ResetLamp();
            lit--;
        }
        Lamp.SetTimesLit(count); // the count everything goes by matches the lamps now lit
    }

    static float Dist(Lamp lamp, Vector3 from)
    {
        Vector3 d = WorldWrap.NearestImage(lamp.transform.position, from) - from;
        d.y = 0f;
        return d.sqrMagnitude;
    }

    void Say(string text, bool bad = false)
    {
        status = text;
        statusBad = bad;
        statusTime = Time.unscaledTime;
    }

    // ---------------------------------------------------------------- Window

    float u; // pixels per 1080p pixel

    void OnGUI()
    {
        u = Mathf.Max(1f, Screen.height / 1080f);
        GUI.skin = DevSkin.Use(u);
        GUI.depth = -900;

        if (!IsOpen)
        {
            if (invincible) Badge();
            return;
        }

        var area = new Rect(20f * u, 20f * u, Mathf.Min(620f * u, Screen.width - 40f * u), Screen.height - 40f * u);
        GUI.Box(area, GUIContent.none, DevSkin.Panel);

        // Header: title, keys, close.
        float x = area.x + 18f * u, w = area.width - 36f * u;
        GUI.Label(new Rect(x, area.y + 12f * u, w, 30f * u), "DEV TOOLS", DevSkin.Title);
        GUI.Label(new Rect(x, area.y + 42f * u, w, 18f * u), "F9 close   ·   F10 model editor   ·   F11 session recorder   ·   F1 spawn overlay", DevSkin.Subtitle);
        if (GUI.Button(new Rect(area.xMax - 48f * u, area.y + 14f * u, 30f * u, 28f * u), "X")) SetOpen(false);
        if (invincible)
            GUI.Label(new Rect(area.xMax - 190f * u, area.y + 16f * u, 136f * u, 24f * u), "INVINCIBLE", DevSkin.SwitchOn);
        DrawLine(new Rect(x, area.y + 66f * u, w, Mathf.Max(1f, 2f * u)), new Color(1f, 0.66f, 0.3f, 0.45f));

        // Tabs.
        float tx = x, ty = area.y + 76f * u;
        for (int i = 0; i < TabNames.Length; i++)
        {
            var style = tab == i ? DevSkin.TabOn : DevSkin.Tab;
            float tw = style.CalcSize(new GUIContent(TabNames[i])).x + 24f * u;
            if (GUI.Button(new Rect(tx, ty, tw, 32f * u), TabNames[i], style) && tab != i)
            {
                tab = i;
                PlayerPrefs.SetInt(TabKey, tab);
            }
            tx += tw + 6f * u;
        }

        float top = ty + 44f * u;
        float footer = tab == 2 ? 84f * u : 0f;
        var content = new Rect(x, top, w, area.yMax - 14f * u - footer - top);

        if (tab == 0) GameTab(content);
        else if (tab == 1) LightingTab(content);
        else
        {
            LookTab(content);
            LookFooter(new Rect(x, content.yMax + 8f * u, w, footer - 8f * u));
        }
    }

    /// <summary>A small badge in the corner while invincible and the window's shut.</summary>
    void Badge()
    {
        var r = new Rect(Screen.width - 170f * u, 16f * u, 150f * u, 26f * u);
        GUI.Label(r, "INVINCIBLE", DevSkin.SwitchOn);
    }

    static void DrawLine(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    // ---------------------------------------------------------------- Game

    void GameTab(Rect content)
    {
        GUILayout.BeginArea(content);
        scroll = GUILayout.BeginScrollView(scroll);

        // At a glance.
        Card("Right now");
        int lamps = Lamp.All.Count;
        Stats(("Lamps", $"{Lamp.LitCount}/{lamps}"), ("Scours", ShadowEntity.All.Count.ToString()), ("Blind ones", BlindEntity.All.Count.ToString()),
              ("Safe", SafeSpot.player != null && SafeZones.Contains(SafeSpot.player.position) ? "yes" : "no"));
        EndCard();

        Card("Player");
        bool inv = Switch("Invincible", invincible, "You become a safe spot: nothing can take you, and the scours circle round you like a lit lamp. Walk into one and it's driven off.");
        if (inv != invincible) Invincible = inv;
        if (invincible) safeSpotRadius = SliderRow("Safe spot size", safeSpotRadius, 1.5f, 12f, "0.0", "m");
        if (pause == null) pause = FindAnyObjectByType<PauseMenu>();
        if (pause != null)
        {
            GUILayout.BeginHorizontal();
            float fov = Mathf.Round(SliderRow("Field of view", pause.fieldOfView, 40f, 100f, "0", "°"));
            if (GUILayout.Button("60", GUILayout.Width(44f * u))) fov = 60f;
            GUILayout.EndHorizontal();
            if (!Mathf.Approximately(fov, pause.fieldOfView)) pause.SetFieldOfView(fov);
            Hint("Players always get 60. Not saved.");
        }
        EndCard();

        Card("Lamps");
        if (lampTarget < 0) lampTarget = Lamp.LitCount;
        GUILayout.BeginHorizontal();
        GUILayout.Label($"Lit {Lamp.LitCount} of {lamps}", DevSkin.Accented);
        GUILayout.FlexibleSpace();
        GUILayout.Label($"count {Lamp.TimesLit}", DevSkin.Small);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("-", GUILayout.Width(36f * u))) lampTarget = Mathf.Max(0, lampTarget - 1);
        lampTarget = Mathf.RoundToInt(GUILayout.HorizontalSlider(lampTarget, 0, lamps, GUILayout.ExpandWidth(true)));
        if (GUILayout.Button("+", GUILayout.Width(36f * u))) lampTarget = Mathf.Min(lamps, lampTarget + 1);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button($"Set to {lampTarget}")) SetLampsLit(lampTarget);
        if (GUILayout.Button("All", GUILayout.Width(70f * u))) { lampTarget = lamps; SetLampsLit(lamps); }
        if (GUILayout.Button("None", GUILayout.Width(70f * u))) { lampTarget = 0; SetLampsLit(0); }
        GUILayout.EndHorizontal();
        Hint("Lights the nearest dark lamps, or puts out the furthest lit ones. Everything tied to lamps follows: entities, the bird, the eye, the searchlight.");
        EndCard();

        Card("Tools");
        if (hotbar == null) hotbar = FindAnyObjectByType<Hotbar>();
        if (hotbar != null)
        {
            bool stick = Switch("Stick of truth", hotbar.HasStick, "In your hotbar. Right click to choose an item or creature, left click to spawn it where you point. Double tap Space to fly.");
            if (stick != hotbar.HasStick) hotbar.SetStick(stick);
        }
        bool overlay = Switch("Spawn overlay (F1)", SpawnDebugOverlay.Enabled, "F1 steps through: everything, structures, lamps, items, creatures, off.");
        if (overlay != SpawnDebugOverlay.Enabled) SpawnDebugOverlay.Enabled = overlay;
        GUILayout.Space(4f * u);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Model editor  (F10)")) ModelEditor.Open();
        if (recorder == null) recorder = FindAnyObjectByType<SessionRecorder>();
        if (recorder != null && GUILayout.Button("Session recorder  (F11)")) recorder.TogglePanel();
        GUILayout.EndHorizontal();
        EndCard();

        Card("Run");
        var old = GUI.color;
        GUI.color = new Color(1f, 0.6f, 0.55f);
        if (GUILayout.Button("Restart the run", GUILayout.Height(32f * u))) GameOverScreen.RestartRun();
        GUI.color = old;
        EndCard();

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // ---------------------------------------------------------------- Lighting

    void LightingTab(Rect content)
    {
        if (lighting == null) lighting = FindAnyObjectByType<LightingSettings>();
        if (lighting != null) lighting.DrawPanel(content, u);
        else GUI.Label(content, "No LightingSettings in the scene.");
    }

    // ---------------------------------------------------------------- Look

    void LookTab(Rect content)
    {
        GUILayout.BeginArea(content);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Find", DevSkin.Small, GUILayout.Width(36f * u));
        filter = GUILayout.TextField(filter ?? "", GUILayout.ExpandWidth(true));
        if (filter.Length > 0 && GUILayout.Button("Clear", GUILayout.Width(60f * u))) filter = "";
        bool allOpen = closedSections.Count == 0;
        if (GUILayout.Button(allOpen ? "Fold all" : "Open all", GUILayout.Width(84f * u)))
        {
            closedSections.Clear();
            if (allOpen) foreach (var s in LookSettings.Sections) closedSections.Add(s);
        }
        GUILayout.EndHorizontal();
        GUILayout.Space(6f * u);

        lookScroll = GUILayout.BeginScrollView(lookScroll);
        string f = filter.Trim().ToLowerInvariant();
        foreach (var section in LookSettings.Sections)
        {
            // The settings in this section that are here and match the search.
            var shown = new List<LookSettings.Knob>();
            int changed = 0;
            foreach (var k in LookSettings.Knobs)
            {
                if (k.section != section || !LookSettings.Available(k)) continue;
                if (f.Length > 0 && !k.label.ToLowerInvariant().Contains(f) && !section.ToLowerInvariant().Contains(f) && !k.Key.ToLowerInvariant().Contains(f)) continue;
                shown.Add(k);
                if (!LookSettings.IsDefault(k)) changed++;
            }
            if (shown.Count == 0) continue;

            bool open = f.Length > 0 || !closedSections.Contains(section);
            string head = (open ? "–   " : "+   ") + section + (changed > 0 ? $"    ({changed} changed)" : "");
            if (GUILayout.Button(head, DevSkin.Foldout))
            {
                if (closedSections.Contains(section)) closedSections.Remove(section);
                else closedSections.Add(section);
            }
            if (open)
            {
                GUILayout.BeginVertical(DevSkin.Card);
                foreach (var k in shown) KnobRow(k);
                GUILayout.EndVertical();
            }
            GUILayout.Space(4f * u);
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    void KnobRow(LookSettings.Knob k)
    {
        object value = LookSettings.Get(k);
        if (value == null) return;
        bool isDefault = LookSettings.IsDefault(k);
        var type = k.info.FieldType;

        GUILayout.BeginHorizontal();
        var label = new GUIContent(k.label, k.tip);
        GUILayout.Label(label, isDefault ? GUI.skin.label : DevSkin.Accented, GUILayout.Width(200f * u));
        object next = value;
        if (type == typeof(bool))
        {
            GUILayout.FlexibleSpace();
            bool b = (bool)value;
            if (GUILayout.Button(b ? "ON" : "OFF", b ? DevSkin.SwitchOn : DevSkin.SwitchOff, GUILayout.Width(56f * u), GUILayout.Height(22f * u))) next = !b;
        }
        else if (type == typeof(float))
        {
            float v = (float)value;
            float nv = GUILayout.HorizontalSlider(v, k.min, k.max, GUILayout.ExpandWidth(true));
            GUILayout.Label(v.ToString(k.format ?? "0.00"), DevSkin.Value, GUILayout.Width(64f * u));
            if (!Mathf.Approximately(nv, v)) next = nv;
        }
        else if (type == typeof(int))
        {
            int v = (int)value;
            if (k.format == "pow2")
            {
                // In steps of powers of two (shadow map sizes).
                float lv = Mathf.Log(Mathf.Max(v, 1), 2f);
                float nl = Mathf.Round(GUILayout.HorizontalSlider(lv, Mathf.Log(k.min, 2f), Mathf.Log(k.max, 2f), GUILayout.ExpandWidth(true)));
                GUILayout.Label(v.ToString(), DevSkin.Value, GUILayout.Width(64f * u));
                int nv = Mathf.RoundToInt(Mathf.Pow(2f, nl));
                if (nv != v) next = nv;
            }
            else
            {
                int nv = Mathf.RoundToInt(GUILayout.HorizontalSlider(v, k.min, k.max, GUILayout.ExpandWidth(true)));
                GUILayout.Label(v.ToString(), DevSkin.Value, GUILayout.Width(64f * u));
                if (nv != v) next = nv;
            }
        }
        else if (type == typeof(Color))
        {
            Color c = (Color)value;
            GUILayout.FlexibleSpace();
            var swatch = GUILayoutUtility.GetRect(64f * u, 20f * u, GUILayout.Width(64f * u), GUILayout.Height(20f * u));
            float most = Mathf.Max(0.0001f, c.maxColorComponent);
            var shown = c.maxColorComponent > 0f && k.max < 1f ? c / most : c; // (very dark colours shown brightened)
            shown.a = 1f;
            DrawLine(swatch, shown);
        }
        ResetButton(k, isDefault);
        GUILayout.EndHorizontal();

        if (type == typeof(Color))
        {
            Color c = (Color)value, n = c;
            n.r = ColourRow("R", c.r, k.max);
            n.g = ColourRow("G", c.g, k.max);
            n.b = ColourRow("B", c.b, k.max);
            if (n != c) next = n;
        }

        if (!Equals(next, value)) LookSettings.Set(k, next);
    }

    float ColourRow(string channel, float v, float max)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Space(24f * u);
        GUILayout.Label(channel, DevSkin.Small, GUILayout.Width(20f * u));
        float nv = GUILayout.HorizontalSlider(v, 0f, max, GUILayout.ExpandWidth(true));
        GUILayout.Label(v.ToString(max < 0.5f ? "0.000" : "0.00"), DevSkin.Value, GUILayout.Width(64f * u));
        GUILayout.Space(58f * u);
        GUILayout.EndHorizontal();
        return nv;
    }

    void ResetButton(LookSettings.Knob k, bool isDefault)
    {
        if (isDefault) { GUILayout.Space(58f * u); return; }
        if (GUILayout.Button(new GUIContent("Reset", "Back to the game's default"), GUILayout.Width(54f * u))) LookSettings.Reset(k);
    }

    void LookFooter(Rect r)
    {
        GUILayout.BeginArea(r);
        GUILayout.BeginHorizontal();
        if (lighting == null) lighting = FindAnyObjectByType<LightingSettings>();
        if (GUILayout.Button("Copy settings", GUILayout.Height(32f * u)))
        {
            GUIUtility.systemCopyBuffer = LookSettings.CopyText(lighting);
            Say("Copied: the Look and Lighting settings are on the clipboard. Paste them to Claude to make them the defaults.");
        }
        if (GUILayout.Button("Paste settings", GUILayout.Height(32f * u)))
        {
            int n = LookSettings.ReadText(GUIUtility.systemCopyBuffer ?? "", lighting, out string problem);
            if (n == 0) Say("Nothing to paste: copy settings text first.", true);
            else Say($"Pasted {n} settings." + (problem != null ? " (Skipped some: " + problem + ")" : ""), problem != null);
        }
        if (GUILayout.Button("Reset all", GUILayout.Height(32f * u), GUILayout.Width(110f * u)))
        {
            LookSettings.ResetAll();
            Say("The Look tab is back to the game's defaults (the Lighting tab has its own themes).");
        }
        GUILayout.EndHorizontal();

        // What just happened, or else a hover tip, or else what the tab is for.
        string line;
        GUIStyle style = DevSkin.Hint;
        if (status != null && Time.unscaledTime - statusTime < 6f)
        {
            line = status;
            style = new GUIStyle(DevSkin.Status);
            style.normal.textColor = statusBad ? DevSkin.Warn : DevSkin.Good;
        }
        else if (!string.IsNullOrEmpty(GUI.tooltip)) line = GUI.tooltip;
        else line = "Saved as you go. Hover a setting for what it does. Changed ones show in amber.";
        GUILayout.Label(line, style);
        GUILayout.EndArea();
    }

    // ---------------------------------------------------------------- Layout helpers

    void Card(string heading)
    {
        GUILayout.BeginVertical(DevSkin.Card);
        GUILayout.Label(heading, DevSkin.Section);
    }

    static void EndCard()
    {
        GUILayout.EndVertical();
    }

    static void Hint(string text) => GUILayout.Label(text, DevSkin.Hint);

    bool Switch(string label, bool value, string hint = null)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label);
        GUILayout.FlexibleSpace();
        if (GUILayout.Button(value ? "ON" : "OFF", value ? DevSkin.SwitchOn : DevSkin.SwitchOff, GUILayout.Width(56f * u), GUILayout.Height(22f * u)))
            value = !value;
        GUILayout.EndHorizontal();
        if (hint != null) Hint(hint);
        GUILayout.Space(4f * u);
        return value;
    }

    float SliderRow(string label, float value, float min, float max, string format, string unitText = "")
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, GUILayout.Width(150f * u));
        float nv = GUILayout.HorizontalSlider(value, min, max, GUILayout.ExpandWidth(true));
        GUILayout.Label(value.ToString(format) + unitText, DevSkin.Value, GUILayout.Width(56f * u));
        GUILayout.EndHorizontal();
        return nv;
    }

    void Stats(params (string name, string value)[] items)
    {
        GUILayout.BeginHorizontal();
        foreach (var (name, value) in items)
        {
            GUILayout.BeginVertical(GUILayout.ExpandWidth(true));
            GUILayout.Label(value, DevSkin.Title);
            GUILayout.Label(name, DevSkin.Small);
            GUILayout.EndVertical();
        }
        GUILayout.EndHorizontal();
    }
}
