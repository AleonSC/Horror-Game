using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The main menu, shown when the game starts (once per Play, not on restarts), in the pause menu's style:
/// the title and START / QUIT on the left over the cinematic bars, and behind it a slow film of the forest
/// itself: shots drifting high over the treetops, craning down onto a camp or a lamp, closing in on a
/// grazing deer or an owl on its branch, panning slowly across the trees, creeping low through the mist. Each
/// holds for a while and dips to black into the next; the arrow at the bottom right skips ahead. No monster
/// is in any of them (none are out yet, and nothing looking up faces the giant hanging in the sky).
///
/// While it's up it has the player's own camera (so the mist, rain and sound go with the shots); the player,
/// the HUD, the intro and everything that comes for you wait for START. DarkForestGenerator adds this.
/// </summary>
public class MainMenu : MonoBehaviour
{
    /// <summary>True while the main menu is up: the game holds off until START.</summary>
    public static bool Showing { get; private set; }
    /// <summary>Whether it's been shown this Play (restarts don't show it again).</summary>
    public static bool ShownThisSession { get; private set; }
    internal static void ResetStatics() { Showing = false; ShownThisSession = false; }

    public string title = "SCARY GAME";
    [Tooltip("Seconds each shot holds before moving on by itself.")]
    public float shotLength = 11f;
    public float fadeTime = 0.6f;

    static readonly Color ItemColor = new Color(0.78f, 0.76f, 0.72f, 0.75f);
    static readonly Color HoverColor = new Color(1f, 0.98f, 0.94f, 1f);
    static readonly Color AccentColor = new Color(0.75f, 0.08f, 0.06f, 1f);
    const float VirtualWidth = 1600f, VirtualHeight = 900f;

    FirstPersonController player;
    Transform cam;
    Vector3 camLocalPos;
    Quaternion camLocalRot;
    DarkForestGenerator generator;
    Wildlife wildlife;
    AudioSource uiSound;

    // The film.
    System.Func<float, (Vector3 pos, Quaternion rot)> shot;
    float shotTime, shotDuration;
    int shotIndex = -1, kind = -1;
    float black = 1f;          // the dip to black between shots (and at the very start)
    bool skipping, starting;
    float startedAt;

    // Drawing.
    float s = 1f, vw = VirtualWidth, vh = VirtualHeight;
    int hovered = -1, lastHovered = -1;
    GUIStyle itemStyle, titleStyle, smallStyle;
    Rect S(Rect r) => new Rect(r.x * s, r.y * s, r.width * s, r.height * s);
    Vector2 Mouse => Event.current.mousePosition / s;

    void Awake()
    {
        Showing = true;
        ShownThisSession = true;
        uiSound = gameObject.AddComponent<AudioSource>();
        uiSound.playOnAwake = false;
        uiSound.spatialBlend = 0f;
        uiSound.ignoreListenerVolume = true;
    }

    void Start()
    {
        player = FindAnyObjectByType<FirstPersonController>();
        generator = FindAnyObjectByType<DarkForestGenerator>();
        wildlife = FindAnyObjectByType<Wildlife>();
        if (player != null)
        {
            player.enabled = false; // it waits (and so does its camera: the menu has it)
            var camera = player.GetComponentInChildren<Camera>();
            cam = camera != null ? camera.transform : null;
        }
        if (cam == null && Camera.main != null) cam = Camera.main.transform;
        if (cam != null) { camLocalPos = cam.localPosition; camLocalRot = cam.localRotation; }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        NextShot();
    }

    void OnDestroy()
    {
        if (Showing) Showing = false;
    }

    // ---------------------------------------------------------------- The film

    void LateUpdate()
    {
        if (cam == null) return;
        float dt = Time.unscaledDeltaTime;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        shotTime += dt;
        if (!starting)
        {
            // Into each shot from black, and back to black at its end (or when skipped).
            bool ending = skipping || shotTime > shotDuration - fadeTime;
            black = Mathf.MoveTowards(black, ending ? 1f : 0f, dt / Mathf.Max(fadeTime, 0.05f));
            if (ending && black >= 1f) NextShot();
        }
        else
        {
            black = Mathf.MoveTowards(black, 1f, dt / 0.9f);
            if (black >= 1f) Begin();
        }

        if (shot != null)
        {
            var (pos, rot) = shot(Mathf.Clamp01(shotTime / Mathf.Max(shotDuration, 0.01f)));
            cam.SetPositionAndRotation(pos, rot);
        }
    }

    /// <summary>On to the next shot: a different kind from the last, round the forest.</summary>
    void NextShot()
    {
        skipping = false;
        shotTime = 0f;
        shotDuration = shotLength;
        shotIndex++;
        // Kinds: 0 high drift, 1 crane down, 2 deer, 3 owl, 4 pan, 5 low creep through the mist.
        int[] order = { 0, 2, 4, 3, 1, 5 };
        for (int attempt = 0; attempt < order.Length; attempt++)
        {
            int k = order[(shotIndex + attempt) % order.Length];
            if (k == kind && order.Length > 1) continue;
            shot = MakeShot(k);
            if (shot != null) { kind = k; return; }
        }
        kind = 0;
        shot = MakeShot(0);
    }

    Vector3 Home => player != null ? player.transform.position : Vector3.zero;

    float Ground(Vector3 p) => generator != null ? generator.GetGroundHeight(p.x, p.z) : p.y;

    /// <summary>The direction the giant hangs in (flat), so shots looking up can face away from it.</summary>
    Vector3 GiantBearing()
    {
        var watcher = WatcherEvent.Instance;
        if (watcher != null && watcher.TryGetHangingBearing(out Vector3 b) && b.sqrMagnitude > 0.01f) return b.normalized;
        return Vector3.forward;
    }

    /// <summary>A flat direction well away from the giant's (so nothing low-angle has it in frame).</summary>
    Vector3 AwayFromGiant(float spread = 50f)
    {
        Vector3 away = -GiantBearing();
        return Quaternion.Euler(0f, Random.Range(-spread, spread), 0f) * away;
    }

    System.Func<float, (Vector3, Quaternion)> MakeShot(int k)
    {
        Vector3 home = Home;
        switch (k)
        {
            case 0:
            {
                // High over the treetops, looking down, drifting slowly across and turning a little.
                Vector3 c = home + new Vector3(Random.Range(-25f, 25f), 0f, Random.Range(-25f, 25f));
                Vector3 across = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                float yaw = Random.Range(0f, 360f), height = Random.Range(34f, 46f);
                return t =>
                {
                    Vector3 p = c + across * Mathf.Lerp(-9f, 9f, t);
                    p.y = Ground(c) + height;
                    return (p, Quaternion.Euler(Mathf.Lerp(72f, 64f, t), yaw + t * 12f, 0f));
                };
            }
            case 1:
            {
                // Craning down onto something: a camp, a lamp or a shack, from high to just above it.
                Transform target = Pick(Campsite.All.ConvertAll(x => x != null ? x.transform : null), Lamp.All.ConvertAll(x => x != null ? x.transform : null), Shack.All.ConvertAll(x => x != null ? x.transform : null));
                if (target == null) return null;
                Vector3 focus = target.position + Vector3.up * 1f;
                Vector3 from = AwayFromGiant(); // the camera's on the giant's far side, looking away from it
                Vector3 side = Vector3.Cross(Vector3.up, from);
                return t =>
                {
                    float e = Mathf.SmoothStep(0f, 1f, t);
                    Vector3 p = focus - from * Mathf.Lerp(14f, 8f, e) + side * Mathf.Lerp(-3f, 3f, t) + Vector3.up * Mathf.Lerp(22f, 4f, e);
                    p.y = Mathf.Max(p.y, Ground(p) + 1.5f);
                    return (p, Quaternion.LookRotation(focus - p));
                };
            }
            case 2:
            {
                // Close on a deer, circling slowly round it as it grazes.
                if (wildlife == null || !wildlife.TryGetDeer(home, out Transform deer)) return null;
                float start = Mathf.Atan2(-GiantBearing().x, -GiantBearing().z) * Mathf.Rad2Deg + Random.Range(-40f, 40f);
                return t =>
                {
                    if (deer == null) return (cam.position, cam.rotation);
                    Vector3 focus = deer.position + Vector3.up * 0.95f;
                    // The camera stands on the giant's side, so looking at the deer looks away from it.
                    Vector3 offset = Quaternion.Euler(0f, start + 180f + t * 35f, 0f) * Vector3.forward * 3.6f;
                    Vector3 p = focus + offset;
                    p.y = Mathf.Max(Ground(p) + 0.9f, focus.y - 0.1f);
                    return (p, Quaternion.LookRotation(focus - p));
                };
            }
            case 3:
            {
                // An owl on its branch, slowly pushing in on it.
                if (wildlife == null || !wildlife.TryGetOwl(home, out Transform owl)) return null;
                Vector3 back = GiantBearing();
                Vector3 side = Vector3.Cross(Vector3.up, back);
                float lean = Random.Range(-0.6f, 0.6f);
                return t =>
                {
                    if (owl == null) return (cam.position, cam.rotation);
                    Vector3 focus = owl.position;
                    Vector3 dir = (back + side * lean).normalized;
                    Vector3 p = focus + dir * Mathf.Lerp(2.8f, 1.6f, Mathf.SmoothStep(0f, 1f, t)) + Vector3.down * 0.35f;
                    return (p, Quaternion.LookRotation(focus - p));
                };
            }
            case 4:
            {
                // Standing among the trees, turning slowly across them (away from the giant).
                Vector3 p = home + new Vector3(Random.Range(-30f, 30f), 0f, Random.Range(-30f, 30f));
                p.y = Ground(p) + 1.7f;
                Vector3 away = -GiantBearing();
                float centre = Mathf.Atan2(away.x, away.z) * Mathf.Rad2Deg;
                float dir = Random.value < 0.5f ? -1f : 1f;
                return t => (p, Quaternion.Euler(4f, centre + dir * Mathf.Lerp(-30f, 30f, Mathf.SmoothStep(0f, 1f, t)), 0f));
            }
            case 5:
            {
                // Low down, creeping forward through the grass and the mist.
                Vector3 forward = AwayFromGiant(40f);
                Vector3 start = home + new Vector3(Random.Range(-20f, 20f), 0f, Random.Range(-20f, 20f));
                return t =>
                {
                    Vector3 p = start + forward * Mathf.Lerp(0f, 5f, t);
                    p.y = Ground(p) + 0.4f;
                    return (p, Quaternion.LookRotation(forward + Vector3.down * 0.04f));
                };
            }
        }
        return null;
    }

    static Transform Pick(params List<Transform>[] lists)
    {
        var all = new List<Transform>();
        foreach (var list in lists) foreach (var t in list) if (t != null) all.Add(t);
        return all.Count > 0 ? all[Random.Range(0, all.Count)] : null;
    }

    /// <summary>START: the camera back to the player, and the game begins (the intro takes it from the dark).</summary>
    void Begin()
    {
        Showing = false;
        if (cam != null) { cam.localPosition = camLocalPos; cam.localRotation = camLocalRot; }
        if (player != null) player.enabled = true;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        Destroy(gameObject);
    }

    // ---------------------------------------------------------------- Drawing

    void OnGUI()
    {
        GUI.depth = -3000;
        float sw = Screen.width, sh = Screen.height;
        s = Mathf.Max(0.4f, Mathf.Min(sw / VirtualWidth, sh / VirtualHeight));
        vw = sw / s;
        vh = sh / s;
        EnsureStyles();

        // The film's dips to black.
        if (black > 0f) Fill(new Rect(0f, 0f, sw, sh), new Color(0f, 0f, 0f, black));

        // The cinematic bars, and the dark falling across from the left where the menu sits.
        float bar = sh * 0.13f;
        Fill(new Rect(0f, 0f, sw, bar), Color.black);
        Fill(new Rect(0f, sh - bar, sw, bar), Color.black);
        for (int i = 0; i < 24; i++)
        {
            float x0 = sw * 0.55f * i / 24f, x1 = sw * 0.55f * (i + 1) / 24f;
            Fill(new Rect(x0, bar, x1 - x0 + 1f, sh - bar * 2f), new Color(0f, 0f, 0f, 0.55f * (1f - i / 24f)));
        }
        if (starting) { Fill(new Rect(0f, 0f, sw, sh), new Color(0f, 0f, 0f, black)); return; }

        float left = Mathf.Max(70f, vw * 0.07f);
        float top = vh * 0.13f + 70f;
        hovered = -1;

        // The title: big, spaced, with a red rule under it.
        string spaced = string.Join(" ", title.ToUpperInvariant().ToCharArray()).Replace("   ", "     ");
        GUI.Label(S(new Rect(left + 3f, top + 3f, 1200f, 90f)), $"<color=#000000AA>{spaced}</color>", titleStyle);
        titleStyle.normal.textColor = HoverColor;
        GUI.Label(S(new Rect(left, top, 1200f, 90f)), spaced, titleStyle);
        Fill(S(new Rect(left, top + 92f, 260f, 3f)), AccentColor);

        float y = top + 150f;
        if (Item(0, "START", left, y)) { starting = true; Tick(0.5f, 1.2f); }
        y += 80f;
        if (Item(1, "QUIT", left, y))
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // The next-shot arrow, bottom right.
        var arrow = new Rect(vw - 150f, vh - vh * 0.13f - 70f, 110f, 46f);
        bool over = arrow.Contains(Mouse);
        if (over) hovered = 10;
        Color c = over ? HoverColor : ItemColor;
        smallStyle.normal.textColor = c;
        GUI.Label(S(new Rect(arrow.x, arrow.y + 8f, 70f, 30f)), "NEXT", smallStyle);
        DrawArrow(S(new Rect(arrow.x + 74f, arrow.y + 10f, 26f, 26f)), over ? AccentColor : c);
        if (over && Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            skipping = true;
            Tick(0.35f, 1.5f);
            Event.current.Use();
        }

        if (Event.current.type == EventType.Repaint && hovered != lastHovered)
        {
            if (hovered >= 0) Tick(0.2f, 1.6f);
            lastHovered = hovered;
        }
    }

    /// <summary>A menu entry, like the pause menu's: brightens, gains a red mark and slides right under the pointer.</summary>
    bool Item(int id, string text, float x, float y)
    {
        Vector2 size = itemStyle.CalcSize(new GUIContent(text)) / s;
        var hit = new Rect(x - 20f, y - 4f, size.x + 60f, size.y + 8f);
        bool over = hit.Contains(Mouse);
        if (over) hovered = id;
        float shift = over ? 10f : 0f;
        if (over) Fill(S(new Rect(x - 14f, y + size.y * 0.5f - 1.5f, 6f, 3f)), AccentColor);
        itemStyle.normal.textColor = over ? HoverColor : ItemColor;
        GUI.Label(S(new Rect(x + shift + 1.5f, y + 1.5f, size.x + 20f, size.y)), $"<color=#000000AA>{text}</color>", itemStyle);
        GUI.Label(S(new Rect(x + shift, y, size.x + 20f, size.y)), text, itemStyle);
        if (over && Event.current.type == EventType.MouseDown && Event.current.button == 0)
        {
            Event.current.Use();
            return true;
        }
        return false;
    }

    /// <summary>A plain right-pointing chevron.</summary>
    static void DrawArrow(Rect r, Color c)
    {
        float t = Mathf.Max(2f, r.height * 0.14f);
        int steps = 12;
        for (int i = 0; i < steps; i++)
        {
            float f = i / (float)(steps - 1);
            float x = r.x + r.width * 0.3f + r.width * 0.4f * (1f - Mathf.Abs(f - 0.5f) * 2f);
            Fill(new Rect(x, r.y + r.height * f - t * 0.5f, t, t), c);
        }
    }

    void EnsureStyles()
    {
        if (itemStyle == null)
        {
            itemStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.UpperLeft, richText = true, wordWrap = false };
            titleStyle = new GUIStyle(itemStyle);
            smallStyle = new GUIStyle(itemStyle);
        }
        itemStyle.fontSize = Mathf.Max(8, Mathf.RoundToInt(34f * s));
        titleStyle.fontSize = Mathf.Max(12, Mathf.RoundToInt(74f * s));
        smallStyle.fontSize = Mathf.Max(7, Mathf.RoundToInt(21f * s));
    }

    static void Fill(Rect r, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    void Tick(float volume, float pitch)
    {
        if (uiSound == null) return;
        uiSound.pitch = pitch;
        uiSound.PlayOneShot(ProceduralAudio.FlashlightClick, volume * PauseMenu.MasterVolume);
    }
}
