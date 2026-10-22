using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Debug: F1 steps through showing labels over everything (then each kind on its own: structures, lamps,
/// items, creatures; then off). It can be switched off altogether in the dev tools (Enabled). Labels over everything that spawns (except trees, boulders and animals; batteries are
/// small green dots and enemies red dots, with no labels), seen
/// through walls and fog from anywhere: name, state and distance. Things off screen or behind you are
/// pinned to the edge of the screen in their direction, with an arrow. A list down the left side gives
/// every one with its distance and compass bearing. DarkForestGenerator adds this automatically.
/// </summary>
public class SpawnDebugOverlay : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Inspector changes made in Play are kept)

    public bool visible;
    /// <summary>Whether F1 does anything at all (the dev tools switch it).</summary>
    public static bool Enabled = true;

    enum Kind { Structure, Lamp, Item, Creature }
    // 0 off, 1 everything, then each kind on its own.
    int mode;
    static readonly string[] ModeNames = { "OFF", "ALL", "STRUCTURES", "LAMPS", "ITEMS", "CREATURES" };
    [Tooltip("Text size at 1080p.")]
    public int fontSize = 15;

    struct Marker
    {
        public string label;
        public Vector3 position;
        public Color color;
        public bool dot; // drawn as a small dot, not a label (batteries, enemies)
        public Kind kind;
    }

    readonly List<Marker> markers = new List<Marker>();
    GUIStyle style, listStyle;
    PortalDoor door;

    static readonly Color StructureColor = new Color(0.55f, 0.85f, 1f);
    static readonly Color LampColor = new Color(1f, 0.8f, 0.35f);
    static readonly Color ItemColor = new Color(0.6f, 1f, 0.55f);
    static readonly Color BatteryColor = new Color(0.3f, 1f, 0.35f);
    static readonly Color EntityColor = new Color(1f, 0.3f, 0.25f);

    void Update()
    {
        if (!Enabled) { visible = false; mode = 0; return; }
        if (HorrorInput.DebugOverlayPressed)
        {
            mode = (mode + 1) % ModeNames.Length;
            visible = mode != 0;
        }
        else if (visible && mode == 0) mode = 1; // (switched on from the inspector)
        else if (!visible) mode = 0;
    }

    void Collect()
    {
        markers.Clear();
        kind = Kind.Structure;
        foreach (var shack in Shack.All) Add(shack.IsCabin ? "Cabin" : "Shack", shack.transform.position + Vector3.up * 3f, StructureColor);
        foreach (var camp in Campsite.All) Add("Campsite", camp.transform.position + Vector3.up * 1.5f, StructureColor);
        foreach (var tower in Watchtower.All) Add("Watchtower", tower.transform.position + Vector3.up * 4f, StructureColor);
        foreach (var car in BrokenCar.All)
            Add(car.IsDead ? "Car (battery dead)" : car.LightsOn ? $"Car (lights on, {Mathf.CeilToInt(car.Battery * 100f)}%)" : "Car", car.transform.position + Vector3.up * 2f, StructureColor);
        if (door == null) door = FindAnyObjectByType<PortalDoor>();
        if (door != null) Add(PortalDoor.PlayerInRoom ? "Lone Door (you're in the room)" : "Lone Door", door.transform.position + Vector3.up * 2.5f, StructureColor);

        kind = Kind.Lamp;
        foreach (var lamp in Lamp.All)
            Add(lamp.IsLit ? "Lamp (lit)" : lamp.IsCoolingDown ? "Lamp (cooling)" : "Lamp", lamp.transform.position + Vector3.up * 4f, LampColor);
        kind = Kind.Item;
        foreach (var item in ItemPickup.All)
            Add(Capitalise(item.DisplayName), item.transform.position + Vector3.up * 0.5f, ItemColor);
        foreach (var battery in BatteryPickup.All)
            Add("Battery", battery.transform.position + Vector3.up * 0.2f, BatteryColor, true);
        foreach (var stick in Glowstick.All)
            Add("Glowstick", stick.transform.position + Vector3.up * 0.3f, Glowstick.GlowColor);
        kind = Kind.Creature;
        foreach (var crawler in Crawler.All)
            if (crawler.gameObject.activeInHierarchy) Add("Crawler", crawler.transform.position + Vector3.up * 0.3f, EntityColor, true);
        foreach (var entity in ShadowEntity.All)
            if (entity.CurrentState != ShadowEntity.State.Hidden)
                Add("Entity", entity.transform.position + Vector3.up * 1.6f, EntityColor, true);
        foreach (var blind in BlindEntity.All)
            Add("Blind Entity", blind.transform.position + Vector3.up * 3f, EntityColor, true);
        foreach (var flier in FlyingEntity.All)
            Add("Flier", flier.transform.position, EntityColor, true);
        if (WatcherEvent.Instance != null && WatcherEvent.Instance.TryGetGiantHead(out var head))
            Add("The Watcher", head, EntityColor, true);
    }

    Kind kind; // what's being collected

    void Add(string label, Vector3 position, Color color, bool dot = false)
    {
        if (mode > 1 && (int)kind != mode - 2) return; // only the kind being shown
        // Wherever the map has looped round to, use the copy nearest the player.
        var cam = Camera.main;
        if (cam != null) position = WorldWrap.NearestImage(position, cam.transform.position);
        markers.Add(new Marker { label = label, position = position, color = color, dot = dot, kind = kind });
    }

    static string Capitalise(string s) => string.IsNullOrEmpty(s) ? s : char.ToUpper(s[0]) + s.Substring(1);

    void OnGUI()
    {
        if (!visible) return;
        var cam = Camera.main;
        if (cam == null) return;
        Collect();

        float unit = Mathf.Max(1f, Screen.height / 1080f);
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = false };
            listStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.UpperLeft, wordWrap = false };
        }
        style.fontSize = Mathf.RoundToInt(fontSize * unit);
        listStyle.fontSize = Mathf.RoundToInt((fontSize - 2) * unit);
        GUI.depth = -3000; // over everything

        Vector3 eye = cam.transform.position;
        float margin = 40f * unit;
        foreach (var m in markers)
        {
            if (m.dot)
            {
                DrawDot(cam, m, unit);
                continue;
            }
            float dist = Vector3.Distance(eye, m.position);
            string text = $"{m.label}  {Mathf.RoundToInt(dist)}m";
            Vector3 sp = cam.WorldToScreenPoint(m.position);
            bool behind = sp.z < 0f;
            if (behind) { sp.x = Screen.width - sp.x; sp.y = Screen.height - sp.y; }
            var pos = new Vector2(sp.x, Screen.height - sp.y);

            bool offScreen = behind || pos.x < margin || pos.x > Screen.width - margin || pos.y < margin || pos.y > Screen.height - margin;
            if (offScreen)
            {
                // Pin to the edge of the screen, in its direction from the centre.
                Vector2 center = new Vector2(Screen.width, Screen.height) * 0.5f;
                Vector2 dir = pos - center;
                if (behind && dir.sqrMagnitude < 1f) dir = Vector2.down;
                float sx = (Screen.width * 0.5f - margin) / Mathf.Max(Mathf.Abs(dir.x), 0.001f);
                float sy = (Screen.height * 0.5f - margin) / Mathf.Max(Mathf.Abs(dir.y), 0.001f);
                pos = center + dir * Mathf.Min(sx, sy);
                text = Arrow(dir) + " " + text;
            }

            Vector2 size = style.CalcSize(new GUIContent(text));
            var rect = new Rect(pos.x - size.x * 0.5f, pos.y - size.y * 0.5f, size.x, size.y);
            Color faded = offScreen ? new Color(m.color.r, m.color.g, m.color.b, 0.75f) : m.color;
            GUI.color = new Color(0f, 0f, 0f, 0.8f);
            GUI.Label(new Rect(rect.x + 1.5f, rect.y + 1.5f, rect.width, rect.height), text, style);
            GUI.color = faded;
            GUI.Label(rect, text, style);
        }

        // The full list, nearest first, with compass bearings (dots are left out of it).
        markers.RemoveAll(m => m.dot);
        markers.Sort((a, b) => (a.position - eye).sqrMagnitude.CompareTo((b.position - eye).sqrMagnitude));
        float y = 60f * unit;
        float line = listStyle.fontSize * 1.35f;
        int shown = Mathf.Min(markers.Count, Mathf.Max(1, Mathf.FloorToInt((Screen.height - y - 40f * unit) / line) - 2));
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(8f * unit, y - 6f * unit, 330f * unit, (shown + 2) * line + 12f * unit), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(16f * unit, y, 320f * unit, 30f * unit), $"SPAWNS: {ModeNames[mode]}  (F1 for next)", listStyle);
        y += listStyle.fontSize * 1.35f;
        for (int i = 0; i < shown; i++)
        {
            var m = markers[i];
            Vector3 d = m.position - eye;
            d.y = 0f;
            float bearing = (Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg + 360f) % 360f;
            GUI.color = m.color;
            GUI.Label(new Rect(16f * unit, y, 320f * unit, 30f * unit), $"{m.label} - {Mathf.RoundToInt(d.magnitude)}m {Compass(bearing)}", listStyle);
            y += line;
        }
        GUI.color = Color.white;
        if (shown < markers.Count) GUI.Label(new Rect(16f * unit, y, 320f * unit, 30f * unit), $"...and {markers.Count - shown} more further away", listStyle);
    }

    /// <summary>A small coloured dot over the thing, only when it's in front of you.</summary>
    static void DrawDot(Camera cam, Marker m, float unit)
    {
        Vector3 sp = cam.WorldToScreenPoint(m.position);
        if (sp.z < 0f) return;
        float size = (m.color == BatteryColor ? 6f : 9f) * unit;
        var r = new Rect(sp.x - size * 0.5f, Screen.height - sp.y - size * 0.5f, size, size);
        GUI.color = new Color(0f, 0f, 0f, 0.7f);
        GUI.DrawTexture(new Rect(r.x - 1f, r.y - 1f, r.width + 2f, r.height + 2f), Texture2D.whiteTexture);
        GUI.color = m.color;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    static string Compass(float bearing)
    {
        string[] names = { "N", "NE", "E", "SE", "S", "SW", "W", "NW" };
        return names[Mathf.RoundToInt(bearing / 45f) % 8];
    }

    static string Arrow(Vector2 dir)
    {
        // GUI space: +y is down.
        float a = Mathf.Atan2(-dir.y, dir.x) * Mathf.Rad2Deg;
        string[] arrows = { "→", "↗", "↑", "↖", "←", "↙", "↓", "↘" };
        int i = Mathf.RoundToInt(((a + 360f) % 360f) / 45f) % 8;
        return arrows[i];
    }
}
