using UnityEngine;

/// <summary>
/// Tab opens the inventory: the forest dims behind a dark panel, your things laid out in a grid on the
/// left (what you haven't found yet shown as an empty, unnamed slot), and on the right a page about the
/// one you pick: what it is, how many you have or how much is left, and how to use it. Click a slot to
/// read about it; Hold puts it in your hand. Tab or Esc closes it. The world doesn't stop while you look.
/// Hotbar adds this.
/// </summary>
public class InventoryScreen : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    /// <summary>True on the frame it closed (so the same Esc doesn't also open the pause menu).</summary>
    public static bool ClosedThisFrame => closedFrame == Time.frameCount;
    static int closedFrame = -1;

    struct Entry
    {
        public Hotbar.Item item;
        public string name, how;
        public string[] lore;
        public Color accent;
    }

    static readonly Entry[] Entries =
    {
        new Entry { item = Hotbar.Item.Flashlight, name = "Flashlight", accent = new Color(0.95f, 0.9f, 0.7f),
            lore = new[] { "A heavy steel torch, its lens scratched, the rubber grip worn smooth by somebody's hand.",
                           "They don't like being looked at with it. Hold it on one too long, though, and it stops being afraid." },
            how = "1 to hold it, F to switch it on and off. It runs down; batteries top it up." },
        new Entry { item = Hotbar.Item.Torch, name = "Torch", accent = new Color(1f, 0.55f, 0.2f),
            lore = new[] { "A length of wood with a pitch-soaked rag bound round the head. It stinks of tar.",
                           "Fire keeps them back, a wide circle of it, for as long as it burns. When it's gone, it's gone." },
            how = "2 to hold it, F to light it. Lighting it takes a little of the lighter's fuel." },
        new Entry { item = Hotbar.Item.Lighter, name = "Lighter", accent = new Color(1f, 0.72f, 0.35f),
            lore = new[] { "A cheap lighter, the paint chipped off its case.",
                           "A small flame makes a small circle they won't step into. It doesn't last." },
            how = "3 to hold it, F to flick it on and off." },
        new Entry { item = Hotbar.Item.FlareGun, name = "Flare Gun", accent = new Color(0.95f, 0.35f, 0.1f),
            lore = new[] { "An orange plastic pistol with one flare in it. Somebody brought it out here expecting to need rescuing.",
                           "Fired high, the flare bursts over the trees and every one of them loses its nerve and has to start again." },
            how = "4 to hold it, aim up into the sky, F to fire. Fired low, it's wasted." },
        new Entry { item = Hotbar.Item.Glowstick, name = "Glowsticks", accent = new Color(0.85f, 1f, 0.3f),
            lore = new[] { "A bundle of yellow chemical lights, the snap-and-shake kind.",
                           "They're only for seeing by: the things in the woods pay them no attention at all." },
            how = "5 to hold them, F to crack one and toss it ahead." },
        new Entry { item = Hotbar.Item.Battery, name = "Batteries", accent = new Color(0.45f, 0.95f, 0.5f),
            lore = new[] { "Batteries, some still in their packets. Found in packs, on tables, in the glove box...",
                           "...and, somehow, dropped by the deer when they bolt. Best not to think about it." },
            how = "6 to hold one, F to put it in the flashlight. Each tops it up a little." },
        new Entry { item = Hotbar.Item.Camera, name = "Camera", accent = new Color(0.8f, 0.85f, 0.95f),
            lore = new[] { "An old film camera with a flash gun on top, from the backpack in the car's trunk.",
                           "Whatever's caught in the flash can't bear it, and is gone for a while. Only three shots on the roll." },
            how = "7 to hold it, F to take a photo. Anything in the frame, close enough, is driven off." },
        new Entry { item = Hotbar.Item.KeyFob, name = "Key Fob", accent = new Color(1f, 0.3f, 0.2f),
            lore = new[] { "The key to the broken-down car, on a ring with a plastic fob.",
                           "Press it and the car answers: two chirps and its lights flashing, wherever it is. So will anything else that's listening." },
            how = "8 to hold it, F to press the button." },
    };

    Hotbar hotbar;
    Flashlight flashlight;
    int selected;
    float openTime;
    GUIStyle title, slotName, slotCount, heading, sub, body, hint, button;

    void Awake() => hotbar = GetComponent<Hotbar>();

    void OnDisable()
    {
        if (IsOpen) SetOpen(false);
    }

    void Update()
    {
        if (PauseMenu.IsPaused || GameOverScreen.IsActive)
        {
            if (IsOpen) SetOpen(false);
            return;
        }
        if (HorrorInput.InventoryPressed) SetOpen(!IsOpen);
        else if (IsOpen && HorrorInput.ReleaseCursorPressed) SetOpen(false);
    }

    void SetOpen(bool open)
    {
        IsOpen = open;
        if (!open) closedFrame = Time.frameCount;
        openTime = Time.unscaledTime;
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;
        if (open && hotbar != null)
        {
            // Open on whatever's in your hand.
            for (int i = 0; i < Entries.Length; i++) if (Entries[i].item == hotbar.Selected) selected = i;
        }
    }

    void OnGUI()
    {
        if (!IsOpen || hotbar == null) return;
        if (flashlight == null) flashlight = FindAnyObjectByType<Flashlight>();
        GUI.depth = -800;
        float unit = Mathf.Max(0.75f, Screen.height / 1080f);
        float a = Mathf.SmoothStep(0f, 1f, (Time.unscaledTime - openTime) / 0.25f);
        MakeStyles(unit);

        // The forest dims behind; a vignette darkens the edges.
        Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.62f * a));
        for (int i = 0; i < 8; i++)
        {
            float e = (8 - i) * 14f * unit;
            Fill(new Rect(0f, 0f, e, Screen.height), new Color(0f, 0f, 0f, 0.05f * a));
            Fill(new Rect(Screen.width - e, 0f, e, Screen.height), new Color(0f, 0f, 0f, 0.05f * a));
        }

        float w = Mathf.Min(1180f * unit, Screen.width - 60f * unit), h = 600f * unit;
        var panel = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f + (1f - a) * 20f * unit, w, h);

        // Title, with a rule.
        Text(new Rect(panel.x, panel.y - 64f * unit, panel.width, 40f * unit), Spaced("Inventory"), title, a);
        Rule(panel.center.x, panel.y - 20f * unit, 360f * unit, unit, a);

        // ---- The grid of slots, left.
        const int columns = 4;
        float slot = 116f * unit, gap = 14f * unit;
        var grid = new Rect(panel.x, panel.y, columns * slot + (columns - 1) * gap, 2 * slot + gap);
        for (int i = 0; i < Entries.Length; i++)
        {
            var e = Entries[i];
            var r = new Rect(grid.x + (i % columns) * (slot + gap), grid.y + (i / columns) * (slot + gap), slot, slot);
            int count = hotbar.CountOf(e.item);
            bool owned = count > 0;
            bool isSel = i == selected;
            bool over = r.Contains(Event.current.mousePosition);
            if (over && Event.current.type == EventType.MouseDown && Event.current.button == 0) { selected = i; Event.current.Use(); }

            Fill(r, new Color(0f, 0f, 0f, (isSel ? 0.75f : 0.55f) * a));
            Color frame = isSel ? new Color(e.accent.r, e.accent.g, e.accent.b, 0.95f) : new Color(0.6f, 0.58f, 0.52f, over ? 0.7f : 0.35f);
            Frame(r, new Color(frame.r, frame.g, frame.b, frame.a * a), (isSel ? 2f : 1f) * unit);
            if (isSel) Fill(new Rect(r.x, r.yMax - 3f * unit, r.width, 3f * unit), new Color(e.accent.r, e.accent.g, e.accent.b, 0.8f * a));

            // A coloured mark, the name, the count.
            Fill(new Rect(r.center.x - 4f * unit, r.y + 26f * unit, 8f * unit, 8f * unit), new Color(e.accent.r, e.accent.g, e.accent.b, (owned ? 0.9f : 0.15f) * a));
            Text(new Rect(r.x + 4f, r.y + 44f * unit, r.width - 8f, 30f * unit), owned ? e.name : "? ? ?", slotName, owned ? a : a * 0.35f);
            if (owned && e.item != Hotbar.Item.Flashlight && e.item != Hotbar.Item.KeyFob)
                Text(new Rect(r.x + 4f, r.yMax - 32f * unit, r.width - 8f, 22f * unit), "x" + count, slotCount, a * 0.85f);
            Text(new Rect(r.x + 6f * unit, r.y + 4f * unit, 30f * unit, 20f * unit), (i + 1).ToString(), hint, a * 0.6f);
            if (hotbar.Selected == e.item && owned) Text(new Rect(r.x, r.y + 4f * unit, r.width - 8f * unit, 20f * unit), "IN HAND", hint, a * 0.8f, TextAnchor.UpperRight);
        }

        // ---- The page about the selected one, right.
        var page = new Rect(grid.xMax + 40f * unit, panel.y, panel.xMax - grid.xMax - 40f * unit, panel.height);
        Fill(new Rect(page.x - 20f * unit, page.y, 1f * unit, page.height * 0.85f), new Color(0.6f, 0.55f, 0.45f, 0.35f * a));
        var sel = Entries[Mathf.Clamp(selected, 0, Entries.Length - 1)];
        int have = hotbar.CountOf(sel.item);
        float y = page.y;
        if (have <= 0)
        {
            Text(new Rect(page.x, y, page.width, 44f * unit), "? ? ?", heading, a * 0.5f, TextAnchor.UpperLeft);
            y += 56f * unit;
            Text(new Rect(page.x, y, page.width, 200f * unit), "You haven't found this yet. It's out there somewhere.", body, a * 0.6f, TextAnchor.UpperLeft);
        }
        else
        {
            Text(new Rect(page.x, y, page.width, 44f * unit), Spaced(sel.name), heading, a, TextAnchor.UpperLeft);
            y += 46f * unit;
            Fill(new Rect(page.x, y, 70f * unit, 2f * unit), new Color(sel.accent.r, sel.accent.g, sel.accent.b, 0.9f * a));
            y += 12f * unit;
            Text(new Rect(page.x, y, page.width, 24f * unit), Status(sel.item, have), sub, a, TextAnchor.UpperLeft);
            y += 40f * unit;
            foreach (var para in sel.lore)
            {
                float ph = body.CalcHeight(new GUIContent(para), page.width);
                Text(new Rect(page.x, y, page.width, ph), para, body, a, TextAnchor.UpperLeft);
                y += ph + 12f * unit;
            }
            y += 8f * unit;
            float hh = hint.CalcHeight(new GUIContent(sel.how), page.width);
            Text(new Rect(page.x, y, page.width, hh), sel.how, sub, a * 0.85f, TextAnchor.UpperLeft);
            y += hh + 24f * unit;

            // Hold it.
            bool inHand = hotbar.Selected == sel.item;
            var holdRect = new Rect(page.x, y, 150f * unit, 38f * unit);
            bool overHold = holdRect.Contains(Event.current.mousePosition) && !inHand;
            Fill(holdRect, new Color(0f, 0f, 0f, 0.6f * a));
            Frame(holdRect, new Color(sel.accent.r, sel.accent.g, sel.accent.b, (inHand ? 0.3f : overHold ? 1f : 0.6f) * a), Mathf.Max(1f, unit));
            Text(holdRect, inHand ? "IN HAND" : "HOLD", button, inHand ? a * 0.45f : a);
            if (overHold && Event.current.type == EventType.MouseDown && Event.current.button == 0)
            {
                hotbar.Select(sel.item);
                Event.current.Use();
            }
        }

        // How to close it.
        Text(new Rect(panel.x, panel.yMax - 10f * unit, panel.width, 24f * unit), "TAB  OR  ESC  TO  CLOSE", hint, a * 0.7f);
    }

    /// <summary>How many, or how much is left.</summary>
    string Status(Hotbar.Item item, int count)
    {
        switch (item)
        {
            case Hotbar.Item.Flashlight: return flashlight != null ? $"Battery {Mathf.RoundToInt(flashlight.Battery * 100f)}%" : "";
            case Hotbar.Item.Torch: return $"{count} {(count == 1 ? "torch" : "torches")} · this one {Mathf.RoundToInt(hotbar.TorchFuel * 100f)}% left" + (hotbar.TorchLit ? " · burning" : "");
            case Hotbar.Item.Lighter: return $"{count} {(count == 1 ? "lighter" : "lighters")} · fuel {Mathf.RoundToInt(hotbar.LighterFuel * 100f)}%" + (hotbar.LighterLit ? " · lit" : "");
            case Hotbar.Item.FlareGun: return count == 1 ? "1 flare" : $"{count} flares";
            case Hotbar.Item.Glowstick: return count == 1 ? "1 glowstick" : $"{count} glowsticks";
            case Hotbar.Item.Battery: return count == 1 ? "1 battery" : $"{count} batteries";
            case Hotbar.Item.Camera: return count == 1 ? "1 shot left" : $"{count} shots left";
            case Hotbar.Item.KeyFob: return "For the car";
        }
        return "";
    }

    // ---------------------------------------------------------------- Drawing

    void MakeStyles(float unit)
    {
        if (title == null)
        {
            title = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            slotName = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold, wordWrap = true };
            slotCount = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter };
            heading = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold };
            sub = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Italic, wordWrap = true };
            body = new GUIStyle(GUI.skin.label) { wordWrap = true };
            hint = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            button = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        }
        title.fontSize = Mathf.RoundToInt(30f * unit);
        slotName.fontSize = Mathf.RoundToInt(15f * unit);
        slotCount.fontSize = Mathf.RoundToInt(14f * unit);
        heading.fontSize = Mathf.RoundToInt(28f * unit);
        sub.fontSize = Mathf.RoundToInt(15f * unit);
        body.fontSize = Mathf.RoundToInt(17f * unit);
        hint.fontSize = Mathf.RoundToInt(11f * unit);
        button.fontSize = Mathf.RoundToInt(14f * unit);
    }

    static readonly Color Cream = new Color(0.93f, 0.87f, 0.74f);

    static void Text(Rect r, string text, GUIStyle style, float a, TextAnchor? anchor = null)
    {
        style.alignment = anchor ?? TextAnchor.MiddleCenter;
        style.normal.textColor = new Color(0f, 0f, 0f, 0.8f * a);
        GUI.Label(new Rect(r.x + 1.5f, r.y + 1.5f, r.width, r.height), text, style);
        style.normal.textColor = new Color(Cream.r, Cream.g, Cream.b, a);
        GUI.Label(r, text, style);
    }

    static string Spaced(string text)
    {
        var sb = new System.Text.StringBuilder();
        foreach (char ch in text.ToUpperInvariant())
        {
            if (sb.Length > 0) sb.Append(ch == ' ' ? "  " : " ");
            if (ch != ' ') sb.Append(ch);
        }
        return sb.ToString();
    }

    static void Rule(float cx, float y, float width, float unit, float a)
    {
        for (int i = 0; i < 20; i++)
        {
            float f = i / 20f;
            float edge = 1f - Mathf.Abs(f - 0.5f) * 2f;
            Fill(new Rect(cx - width * 0.5f + width * f, y, width / 20f + 0.5f, Mathf.Max(1f, unit)), new Color(1f, 0.72f, 0.36f, 0.7f * edge * a));
        }
    }

    static void Frame(Rect r, Color c, float t)
    {
        Fill(new Rect(r.x, r.y, r.width, t), c);
        Fill(new Rect(r.x, r.yMax - t, r.width, t), c);
        Fill(new Rect(r.x, r.y, t, r.height), c);
        Fill(new Rect(r.xMax - t, r.y, t, r.height), c);
    }

    static void Fill(Rect r, Color c)
    {
        Color old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }
}
