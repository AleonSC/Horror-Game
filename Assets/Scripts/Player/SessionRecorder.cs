using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

/// <summary>
/// Records what happens in a session, to copy and paste for analysis. It writes a line for every change
/// that matters (each scour, numbered as it first appears, and each of its changes of state; the blind
/// one, the crawlers, the fliers and the giant likewise; what the blind one hears and where it came from;
/// lamps lit and how long each took to reach; safe zones, seats, the drone, the dark room; being caught;
/// running out of breath; storms, falling trees, lightning...) and keeps running totals: time running,
/// walking with full stamina, walking or standing while getting your breath back, standing still,
/// crouched, in safe zones, flying the drone, and how far you went.
///
/// F11 shows the recorder: Copy (everything: the totals, then every line), Copy totals, Save (a text file:
/// in the project's SessionLogs folder in the editor, the game's save folder in a build), Clear. It's also
/// saved by itself when Play stops. Anything can add a line with SessionRecorder.Log.
/// DarkForestGenerator adds this.
/// </summary>
public class SessionRecorder : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Inspector changes made in Play are kept)

    [Tooltip("Seconds between status lines (where you are, your stamina, battery, the nearest scour).")]
    public float statusEvery = 30f;
    [Tooltip("Lines kept at most (the oldest go first).")]
    public int maxLines = 20000;

    // ---------------------------------------------------------------- Logging

    static readonly List<string> lines = new List<string>();
    static float clock; // game time this session (paused time doesn't count)
    static float runStart;
    static int run = 1;
    static readonly Dictionary<string, int> counts = new Dictionary<string, int>();
    static int limit = 20000;

    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics()
    {
        lines.Clear();
        counts.Clear();
        clock = runStart = 0f;
        run = 1;
        scourIds.Clear();
        nextScour = 1;
    }

    /// <summary>Adds a line: [time] [category] text. Anything can call this.</summary>
    public static void Log(string category, string text)
    {
        lines.Add($"[{Stamp(clock)} | run {run} {Stamp(clock - runStart)}] {category,-8} {text}");
        if (lines.Count > limit) lines.RemoveRange(0, lines.Count - limit);
        Count(category);
    }

    /// <summary>Counts something for the totals (without a line).</summary>
    public static void Count(string what, int by = 1)
    {
        counts.TryGetValue(what, out int n);
        counts[what] = n + by;
    }

    static string Stamp(float t)
    {
        t = Mathf.Max(0f, t);
        int m = Mathf.FloorToInt(t / 60f);
        return $"{m:00}:{t - m * 60f:00.0}";
    }

    static string Where(Vector3 p) => $"({p.x:0},{p.z:0})";

    // ---------------------------------------------------------------- Watching

    static readonly Dictionary<ShadowEntity, int> scourIds = new Dictionary<ShadowEntity, int>();
    static int nextScour = 1;
    readonly Dictionary<ShadowEntity, ShadowEntity.State> scourState = new Dictionary<ShadowEntity, ShadowEntity.State>();
    readonly Dictionary<Object, string> otherState = new Dictionary<Object, string>();
    readonly Dictionary<Object, int> otherIds = new Dictionary<Object, int>();
    readonly Dictionary<string, int> nextOther = new Dictionary<string, int>();

    FirstPersonController player;
    Flashlight flashlight;
    Hotbar hotbar;
    Vector3 lastPos;
    bool havePos;

    // Totals.
    float tRunning, tWalkFull, tWalkRecovering, tStillFull, tStillRecovering, tCrouched, tSafe, tSeated, tPiloting, tRoom, tWinded, tFlashlight;
    float distance, distanceSinceLamp, timeSinceLamp;
    int windedTimes, deaths;
    readonly List<string> lampSplits = new List<string>();

    // Last seen, to log changes.
    bool wasRunning, wasWinded, wasSafe, wasSeated, wasPiloting, wasRoom, wasGameOver, wasStorm, wasFull = true;
    float runStartedAt, windedAt, recoverStartedAt;
    int lastLamps, lastHotbarItem = -1;
    string lastGiant = "", lastBattery = "";
    float statusTimer;

    void Start()
    {
        limit = Mathf.Max(100, maxLines);
        Log("session", $"Started {System.DateTime.Now:yyyy-MM-dd HH:mm}. Difficulty and settings as saved.");
    }

    /// <summary>Shows or hides the recorder's panel (as F11 does).</summary>
    public void TogglePanel() => showPanel = !showPanel;

    void Update()
    {
        float dt = Time.deltaTime;
        if (HorrorInput.RecorderPressed) showPanel = !showPanel;
        if (MainMenu.Showing || dt <= 0f) return;
        clock += dt;
        timeSinceLamp += dt;

        if (player == null)
        {
            player = FindAnyObjectByType<FirstPersonController>();
            flashlight = FindAnyObjectByType<Flashlight>();
            hotbar = FindAnyObjectByType<Hotbar>();
            if (player == null) return;
        }

        WatchPlayer(dt);
        WatchLamps();
        WatchScours();
        WatchOthers();
        WatchWorld();

        statusTimer -= dt;
        if (statusTimer <= 0f)
        {
            statusTimer = statusEvery;
            Status();
        }
    }

    void WatchPlayer(float dt)
    {
        Vector3 p = player.transform.position;
        Vector3 step = p - lastPos;
        step.y = 0f;
        float moved = havePos && step.magnitude < 3f ? step.magnitude : 0f; // (a teleport isn't walking)
        lastPos = p;
        havePos = true;
        distance += moved;
        distanceSinceLamp += moved;
        bool moving = moved / dt > 0.4f;

        bool running = player.IsRunning && moving;
        bool full = player.Stamina >= 0.999f;
        if (running) tRunning += dt;
        else if (moving) { if (full) tWalkFull += dt; else tWalkRecovering += dt; }
        else { if (full) tStillFull += dt; else tStillRecovering += dt; }
        if (player.IsCrouching) tCrouched += dt;
        if (player.Winded) tWinded += dt;
        if (flashlight != null && flashlight.IsOn) tFlashlight += dt;

        if (running != wasRunning)
        {
            if (running) { runStartedAt = clock; Log("player", $"Started running (stamina {player.Stamina:P0})."); }
            else Log("player", $"Stopped running after {clock - runStartedAt:0.0}s (stamina {player.Stamina:P0}).");
            wasRunning = running;
        }
        if (player.Winded != wasWinded)
        {
            if (player.Winded) { windedTimes++; windedAt = clock; Log("player", "Out of breath (winded)."); }
            else Log("player", $"Got enough breath back after {clock - windedAt:0.0}s.");
            wasWinded = player.Winded;
        }
        if (full != wasFull)
        {
            if (!full) recoverStartedAt = clock;
            else Log("player", $"Stamina full again after {clock - recoverStartedAt:0.0}s.");
            wasFull = full;
        }

        bool safe = SafeZones.Contains(p);
        if (safe) tSafe += dt;
        if (safe != wasSafe) { Log("player", safe ? $"Entered a safe zone at {Where(p)}." : $"Left the safe zone at {Where(p)}."); wasSafe = safe; }
        if (player.Seated) tSeated += dt;
        if (player.Seated != wasSeated) { Log("player", player.Seated ? "Sat down (car)." : "Stood up."); wasSeated = player.Seated; }
        if (DroneUnit.Piloting) tPiloting += dt;
        if (DroneUnit.Piloting != wasPiloting)
        {
            Log("drone", DroneUnit.Piloting ? $"Started flying the drone (battery {DroneUnit.Charge:P0})." : $"Stopped flying the drone (battery {DroneUnit.Charge:P0}).");
            wasPiloting = DroneUnit.Piloting;
        }
        if (PortalDoor.PlayerInRoom) tRoom += dt;
        if (PortalDoor.PlayerInRoom != wasRoom) { Log("player", PortalDoor.PlayerInRoom ? "Went into the dark room." : "Came out of the dark room."); wasRoom = PortalDoor.PlayerInRoom; }

        if (hotbar != null && (int)hotbar.Selected != lastHotbarItem)
        {
            if (lastHotbarItem >= 0) Log("item", $"Took out: {hotbar.Selected}.");
            lastHotbarItem = (int)hotbar.Selected;
        }
        if (flashlight != null)
        {
            string battery = flashlight.Battery <= 0f ? "empty" : flashlight.Battery < 0.2f ? "low" : "ok";
            if (battery != lastBattery)
            {
                if (lastBattery != "") Log("item", $"Flashlight battery {battery} ({flashlight.Battery:P0}).");
                lastBattery = battery;
            }
        }

        bool over = GameOverScreen.IsActive;
        if (over != wasGameOver)
        {
            if (over)
            {
                deaths++;
                string by = ShadowEntity.Capturing != null ? ScourName(ShadowEntity.Capturing) : Crawler.Holding != null ? "a crawler" : TakenBy();
                Log("CAUGHT", $"Caught by {by} at {Where(p)}, {Lamp.TimesLit} lamps lit, {clock - runStart:0}s into the run.");
            }
            else
            {
                run++;
                runStart = clock;
                timeSinceLamp = distanceSinceLamp = 0f;
                Log("session", $"Run {run} starts.");
            }
            wasGameOver = over;
        }
    }

    string TakenBy()
    {
        foreach (var b in BlindEntity.All) if (b != null && b.StateName == "Taking") return "the blind one";
        return "something";
    }

    void WatchLamps()
    {
        int lit = Lamp.TimesLit;
        if (lit > lastLamps)
        {
            var lamp = Lamp.LastLit;
            string at = lamp != null ? Where(lamp.transform.position) : "";
            string split = $"lamp {lit}: {timeSinceLamp:0}s, {distanceSinceLamp:0}m";
            lampSplits.Add(split);
            Log("LAMP", $"Lamp {lit} lit {at} after {timeSinceLamp:0.0}s and {distanceSinceLamp:0}m since the last ({clock - runStart:0}s into the run).");
            timeSinceLamp = distanceSinceLamp = 0f;
        }
        else if (lit < lastLamps) Log("LAMP", $"Lamps lit back to {lit}.");
        lastLamps = lit;
    }

    static string ScourName(ShadowEntity s)
    {
        if (!scourIds.TryGetValue(s, out int id)) scourIds[s] = id = nextScour++;
        return $"Scour {id}";
    }

    void WatchScours()
    {
        Vector3 you = player.transform.position;
        foreach (var s in ShadowEntity.All)
        {
            if (s == null) continue;
            bool known = scourState.TryGetValue(s, out var before);
            var now = s.CurrentState;
            string name = ScourName(s);
            float d = Vector3.Distance(WorldWrap.NearestImage(s.transform.position, you), you);
            if (!known)
            {
                Log("scour", $"{name} exists (now {now}). {ShadowEntity.All.Count} scours in all.");
                Count("scours");
            }
            else if (now != before)
            {
                string what = Describe(before, now);
                Log("scour", $"{name}: {before} -> {now}{what}, {d:0}m away (hunt {s.HuntProgress:P0}).");
            }
            scourState[s] = now;
        }
        // Gone for good.
        var gone = new List<ShadowEntity>();
        foreach (var kv in scourState) if (kv.Key == null) gone.Add(kv.Key);
        foreach (var s in gone) { scourState.Remove(s); Log("scour", "A scour was removed."); }
    }

    static string Describe(ShadowEntity.State before, ShadowEntity.State now)
    {
        if (now == ShadowEntity.State.Retreating) { Count("scours dismissed"); return " (dismissed: dragged back through its hole)"; }
        if (now == ShadowEntity.State.Crescendo) return " (being faced down)";
        if (now == ShadowEntity.State.Charging) { Count("scour charges"); return " (charging: flashed at)"; }
        if (now == ShadowEntity.State.Killing) { Count("scour catches"); return " (caught you)"; }
        if (now == ShadowEntity.State.Watching) return " (out, watching)";
        if (now == ShadowEntity.State.Hidden && before != ShadowEntity.State.Retreating) return " (gone)";
        return "";
    }

    void WatchOthers()
    {
        foreach (var b in BlindEntity.All) Track(b, "Blind One", b != null ? b.StateName : null);
        foreach (var c in Crawler.All) Track(c, "Crawler", c != null ? c.StateName : null);
        foreach (var f in FlyingEntity.All) Track(f, "Flier", f != null ? f.StateName : null);

        var w = WatcherEvent.Instance;
        if (w != null)
        {
            string g = w.PhaseName + (w.SearchlightsOn ? " (searchlights on)" : "");
            if (g != lastGiant) { if (lastGiant != "") Log("giant", $"Giant: {lastGiant} -> {g}."); lastGiant = g; }
        }
    }

    void Track(Component c, string kind, string state)
    {
        if (c == null || state == null) return;
        if (!otherIds.TryGetValue(c, out int id))
        {
            nextOther.TryGetValue(kind, out int n);
            otherIds[c] = id = n + 1;
            nextOther[kind] = id;
            Log(kind.ToLowerInvariant().Replace(" ", ""), $"{kind} {id} appears at {Where(c.transform.position)} ({state}).");
            Count(kind + "s");
        }
        otherState.TryGetValue(c, out string before);
        if (before != null && before != state)
        {
            float d = Vector3.Distance(WorldWrap.NearestImage(c.transform.position, player.transform.position), player.transform.position);
            Log(kind.ToLowerInvariant().Replace(" ", ""), $"{kind} {id}: {before} -> {state}, {d:0}m away.");
        }
        otherState[c] = state;
    }

    void WatchWorld()
    {
        bool storm = RainDrops.Storm > 0.5f;
        if (storm != wasStorm) { Log("weather", storm ? "A storm is building." : "The storm is easing."); wasStorm = storm; }
    }

    void Status()
    {
        Vector3 p = player.transform.position;
        float nearest = float.MaxValue;
        int visible = 0;
        foreach (var s in ShadowEntity.All)
        {
            if (s == null) continue;
            if (s.IsVisible) visible++;
            if (s.CurrentState != ShadowEntity.State.Hidden)
                nearest = Mathf.Min(nearest, Vector3.Distance(WorldWrap.NearestImage(s.transform.position, p), p));
        }
        string scour = nearest < float.MaxValue ? $"nearest scour {nearest:0}m" : "no scour out";
        string battery = flashlight != null ? $"battery {flashlight.Battery:P0}" : "";
        Log("status", $"At {Where(p)}, stamina {player.Stamina:P0}, {battery}, lamps {Lamp.TimesLit}, {visible} scours visible, {scour}.");
    }

    // ---------------------------------------------------------------- The report

    string Totals()
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== SESSION TOTALS ===");
        sb.AppendLine($"Played {Stamp(clock)}, {run} run(s), caught {deaths} time(s). Distance {distance:0}m.");
        sb.AppendLine($"Running {Stamp(tRunning)} | walking at full stamina {Stamp(tWalkFull)} | walking while getting breath back {Stamp(tWalkRecovering)}");
        sb.AppendLine($"Standing still at full stamina {Stamp(tStillFull)} | standing still getting breath back {Stamp(tStillRecovering)} | winded {Stamp(tWinded)} ({windedTimes} times)");
        sb.AppendLine($"Crouched {Stamp(tCrouched)} | in safe zones {Stamp(tSafe)} | seated {Stamp(tSeated)} | flying the drone {Stamp(tPiloting)} | in the dark room {Stamp(tRoom)} | flashlight on {Stamp(tFlashlight)}");
        sb.AppendLine("Lamps: " + (lampSplits.Count > 0 ? string.Join(" | ", lampSplits) : "none lit"));
        if (counts.Count > 0)
        {
            var keys = new List<string>(counts.Keys);
            keys.Sort();
            var parts = new List<string>();
            foreach (var k in keys) parts.Add($"{k} {counts[k]}");
            sb.AppendLine("Counts: " + string.Join(", ", parts));
        }
        return sb.ToString();
    }

    string Everything()
    {
        var sb = new StringBuilder(Totals());
        sb.AppendLine();
        sb.AppendLine("=== EVENTS ===");
        foreach (var line in lines) sb.AppendLine(line);
        return sb.ToString();
    }

    string Save()
    {
        string folder = Application.isEditor ? Path.Combine(Path.GetDirectoryName(Application.dataPath), "SessionLogs")
                                             : Path.Combine(Application.persistentDataPath, "SessionLogs");
        Directory.CreateDirectory(folder);
        string path = Path.Combine(folder, $"session_{System.DateTime.Now:yyyy-MM-dd_HH-mm-ss}.txt");
        File.WriteAllText(path, Everything());
        return path;
    }

    void OnApplicationQuit()
    {
        if (lines.Count > 1)
        {
            try { Debug.Log("[Horror] Session log saved: " + Save()); }
            catch (System.Exception e) { Debug.LogWarning("[Horror] Couldn't save the session log: " + e.Message); }
        }
    }

    // ---------------------------------------------------------------- The panel (F11)

    bool showPanel;
    string note = "";
    float noteUntil;
    GUIStyle style, button;

    void OnGUI()
    {
        if (!showPanel) return;
        GUI.depth = -940;
        float u = Mathf.Max(0.75f, Screen.height / 1080f);
        if (style == null)
        {
            style = new GUIStyle(GUI.skin.label) { wordWrap = true };
            button = new GUIStyle(GUI.skin.button);
        }
        style.fontSize = Mathf.RoundToInt(13f * u);
        button.fontSize = Mathf.RoundToInt(13f * u);
        style.normal.textColor = new Color(0.9f, 0.86f, 0.78f);

        var area = new Rect(Screen.width - 520f * u, 20f * u, 500f * u, 330f * u);
        var before = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.85f);
        GUI.DrawTexture(area, Texture2D.whiteTexture);
        GUI.color = before;

        GUILayout.BeginArea(new Rect(area.x + 10f * u, area.y + 8f * u, area.width - 20f * u, area.height - 16f * u));
        GUILayout.Label($"SESSION RECORDER  (F11)   {Stamp(clock)}, {lines.Count} lines", style);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Copy everything", button)) { GUIUtility.systemCopyBuffer = Everything(); Note("Copied the whole log: paste it to Claude."); }
        if (GUILayout.Button("Copy totals", button)) { GUIUtility.systemCopyBuffer = Totals(); Note("Copied the totals."); }
        if (GUILayout.Button("Save", button))
        {
            try { Note("Saved: " + Save()); }
            catch (System.Exception e) { Note("Couldn't save: " + e.Message); }
        }
        if (GUILayout.Button("Clear", button)) { lines.Clear(); Note("Cleared the lines (the totals carry on)."); }
        GUILayout.EndHorizontal();
        if (Time.unscaledTime < noteUntil) GUILayout.Label(note, style);
        GUILayout.Space(4f * u);
        // The last few lines.
        var recent = new StringBuilder();
        for (int i = Mathf.Max(0, lines.Count - 12); i < lines.Count; i++) recent.AppendLine(lines[i]);
        GUILayout.Label(recent.ToString(), style);
        GUILayout.EndArea();
    }

    void Note(string text)
    {
        note = text;
        noteUntil = Time.unscaledTime + 5f;
    }
}
