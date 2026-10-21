using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Developer tools, in a window toggled with F9 (the mouse is freed while it's open; you can still move).
/// Lamps: set how many lamps are lit right now, 0 to all of them. Going up lights the nearest unlit ones
/// for real (so everything that follows lamps being lit happens: entities, narration, the bird, the eye,
/// the searchlight...); going down puts the furthest lit ones back out and sets the count to match (anything
/// already spawned stays). The stick of truth (a dev item: spawn things, fly) and the F1 overlay can be
/// switched on and off here. Field of view (players always get 60; this isn't saved). Also: restart the run. The Lighting tab is the full lighting panel
/// (LightingSettings: themes, moon, fog, sky, flashlight, picture). DarkForestGenerator adds this.
/// </summary>
public class DevTools : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() => IsOpen = false;

    int lampTarget = -1;
    int tab; // 0 game, 1 lighting
    LightingSettings lighting;
    PauseMenu pause;
    Hotbar hotbar;
    Vector2 scroll;
    GUIStyle title, label, button, small;

    void Update()
    {
        if (!HorrorInput.DevToolsPressed) return;
        IsOpen = !IsOpen;
        if (IsOpen) lampTarget = Lamp.LitCount;
        Cursor.lockState = IsOpen ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = IsOpen;
    }

    void OnDisable()
    {
        if (IsOpen) IsOpen = false;
    }

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

    void OnGUI()
    {
        if (!IsOpen) return;
        GUI.depth = -900;
        float unit = Mathf.Max(1f, Screen.height / 1080f);
        if (title == null)
        {
            title = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            label = new GUIStyle(GUI.skin.label);
            button = new GUIStyle(GUI.skin.button);
            small = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Italic, wordWrap = true };
        }
        title.fontSize = Mathf.RoundToInt(20f * unit);
        label.fontSize = button.fontSize = Mathf.RoundToInt(15f * unit);
        small.fontSize = Mathf.RoundToInt(12f * unit);
        title.normal.textColor = new Color(1f, 0.75f, 0.4f);
        label.normal.textColor = small.normal.textColor = new Color(0.9f, 0.86f, 0.78f);

        var area = tab == 1
            ? new Rect(20f * unit, 20f * unit, Mathf.Min(620f * unit, Screen.width - 40f * unit), Screen.height - 40f * unit)
            : new Rect(20f * unit, 20f * unit, 380f * unit, 520f * unit);
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.DrawTexture(area, Texture2D.whiteTexture);
        GUI.color = Color.white;

        // Title and tabs.
        GUI.Label(new Rect(area.x + 14f * unit, area.y + 10f * unit, area.width, 30f * unit), "DEV TOOLS  (F9)", title);
        float tabWidth = 90f * unit, tabY = area.y + 44f * unit;
        string[] tabs = { "Game", "Lighting" };
        for (int i = 0; i < tabs.Length; i++)
        {
            var r = new Rect(area.x + 14f * unit + i * (tabWidth + 6f * unit), tabY, tabWidth, 28f * unit);
            GUI.color = tab == i ? new Color(1f, 0.8f, 0.5f) : Color.white;
            if (GUI.Button(r, tabs[i], button)) tab = i;
            GUI.color = Color.white;
        }
        var content = new Rect(area.x + 14f * unit, tabY + 38f * unit, area.width - 28f * unit, area.yMax - tabY - 48f * unit);

        if (tab == 1)
        {
            if (lighting == null) lighting = FindAnyObjectByType<LightingSettings>();
            if (lighting != null) lighting.DrawPanel(content, unit);
            else GUI.Label(content, "No LightingSettings in the scene.", label);
            return;
        }

        GUILayout.BeginArea(content);
        scroll = GUILayout.BeginScrollView(scroll);

        int total = Lamp.All.Count;
        if (lampTarget < 0) lampTarget = Lamp.LitCount;
        GUILayout.Label($"Lamps lit: {Lamp.LitCount} / {total}   (count {Lamp.TimesLit})", label);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("-", button, GUILayout.Width(40f * unit))) lampTarget = Mathf.Max(0, lampTarget - 1);
        lampTarget = Mathf.RoundToInt(GUILayout.HorizontalSlider(lampTarget, 0, total, GUILayout.ExpandWidth(true)));
        if (GUILayout.Button("+", button, GUILayout.Width(40f * unit))) lampTarget = Mathf.Min(total, lampTarget + 1);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button($"Set to {lampTarget}", button)) SetLampsLit(lampTarget);
        if (GUILayout.Button("All", button, GUILayout.Width(60f * unit))) { lampTarget = total; SetLampsLit(total); }
        if (GUILayout.Button("None", button, GUILayout.Width(60f * unit))) { lampTarget = 0; SetLampsLit(0); }
        GUILayout.EndHorizontal();
        GUILayout.Label("Lights the nearest dark lamps (or puts out the furthest lit ones). Everything tied to lamps follows: entities, narration, the bird, the eye, the searchlight.", small);

        GUILayout.Space(10f * unit);
        if (pause == null) pause = FindAnyObjectByType<PauseMenu>();
        if (pause != null)
        {
            GUILayout.Label($"Field of view: {Mathf.RoundToInt(pause.fieldOfView)}°  (players get 60, not saved)", label);
            GUILayout.BeginHorizontal();
            float fov = Mathf.Round(GUILayout.HorizontalSlider(pause.fieldOfView, 40f, 100f, GUILayout.ExpandWidth(true)));
            if (GUILayout.Button("60", button, GUILayout.Width(50f * unit))) fov = 60f;
            GUILayout.EndHorizontal();
            if (!Mathf.Approximately(fov, pause.fieldOfView)) pause.SetFieldOfView(fov);
            GUILayout.Space(10f * unit);
        }
        // The stick of truth, and the F1 overlay.
        if (hotbar == null) hotbar = FindAnyObjectByType<Hotbar>();
        if (hotbar != null)
        {
            bool stick = GUILayout.Toggle(hotbar.HasStick, "  Stick of truth (in your hotbar)", GUILayout.Height(24f * unit));
            if (stick != hotbar.HasStick) hotbar.SetStick(stick);
            GUILayout.Label("Right click with it to choose an item or creature, left click to spawn it where you point. Double tap Space to fly while holding it.", small);
        }
        bool overlay = GUILayout.Toggle(SpawnDebugOverlay.Enabled, "  F1 spawn overlay", GUILayout.Height(24f * unit));
        if (overlay != SpawnDebugOverlay.Enabled) SpawnDebugOverlay.Enabled = overlay;
        GUILayout.Label("F1 steps through: everything, then structures, lamps, items, creatures, then off.", small);
        GUILayout.Space(10f * unit);

        if (GUILayout.Button("Restart the run", button)) GameOverScreen.RestartRun();

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }
}
