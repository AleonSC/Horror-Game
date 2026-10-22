using UnityEngine;

/// <summary>
/// Tab opens the inventory: the forest dims behind a dark panel, your hotbar's eight slots laid out in a
/// grid on the left, and on the right a page about the one you pick: what it is, how many you have or how
/// much is left, and how to use it. Click a slot to read about it; drag it onto another slot to move it
/// there (swapping with whatever's in it), which changes its key; Hold puts it in your hand. Tab or Esc
/// closes it. The world doesn't stop while you look. Hotbar adds this.
/// </summary>
public class InventoryScreen : MonoBehaviour
{
    public static bool IsOpen { get; private set; }
    /// <summary>How big the whole screen is drawn (1 = the original size).</summary>
    const float SizeScale = 1.2f;
    /// <summary>True on the frame it closed (so the same Esc doesn't also open the pause menu).</summary>
    public static bool ClosedThisFrame => closedFrame == Time.frameCount;
    static int closedFrame = -1;
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() { closedFrame = -1; IsOpen = false; }

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
            how = "{key} to hold it, F to switch it on and off. It runs down; batteries top it up." },
        new Entry { item = Hotbar.Item.Torch, name = "Torch", accent = new Color(1f, 0.55f, 0.2f),
            lore = new[] { "A length of wood with a pitch-soaked rag bound round the head. It stinks of tar.",
                           "Fire keeps them back, a wide circle of it, for as long as it burns. When it's gone, it's gone." },
            how = "{key} to hold it, F to light it. Lighting it takes a little of the lighter's fuel." },
        new Entry { item = Hotbar.Item.Lighter, name = "Lighter", accent = new Color(1f, 0.72f, 0.35f),
            lore = new[] { "A cheap lighter, the paint chipped off its case.",
                           "A small flame makes a small circle they won't step into. It doesn't last." },
            how = "{key} to hold it, F to flick it on and off." },
        new Entry { item = Hotbar.Item.FlareGun, name = "Flare Gun", accent = new Color(0.95f, 0.35f, 0.1f),
            lore = new[] { "An orange plastic pistol with one flare in it. Somebody brought it out here expecting to need rescuing.",
                           "Fired high, the flare bursts over the trees and every one of them loses its nerve and has to start again." },
            how = "{key} to hold it, aim up into the sky, F to fire. Fired low, it's wasted." },
        new Entry { item = Hotbar.Item.Glowstick, name = "Glowsticks", accent = new Color(0.85f, 1f, 0.3f),
            lore = new[] { "A bundle of yellow chemical lights, the snap-and-shake kind.",
                           "They're only for seeing by: the things in the woods pay them no attention at all." },
            how = "{key} to hold them, F to crack one and toss it ahead." },
        new Entry { item = Hotbar.Item.Battery, name = "Batteries", accent = new Color(0.45f, 0.95f, 0.5f),
            lore = new[] { "Batteries, some still in their packets. Found in packs, on tables, in the glove box...",
                           "...and, somehow, dropped by the deer when they bolt. Best not to think about it." },
            how = "{key} to hold one, F to put it in the flashlight. Each tops it up a little." },
        new Entry { item = Hotbar.Item.Camera, name = "Camera", accent = new Color(0.8f, 0.85f, 0.95f),
            lore = new[] { "An old film camera with a flash gun on top, from the backpack in the car's trunk.",
                           "Whatever's caught in the flash can't bear it, and is gone for a while. Only three shots on the roll." },
            how = "{key} to hold it, F to take a photo. Anything in the frame, close enough, is driven off." },
        new Entry { item = Hotbar.Item.KeyFob, name = "Key Fob", accent = new Color(1f, 0.3f, 0.2f),
            lore = new[] { "The key to the broken-down car, on a ring with a plastic fob.",
                           "Press it and the car answers: two chirps and its lights flashing, wherever it is. So will anything else that's listening." },
            how = "{key} to hold it, F to press the button." },
        new Entry { item = Hotbar.Item.Map, name = "Map", accent = new Color(0.88f, 0.78f, 0.58f),
            lore = new[] { "A map someone drew of the trails, left up on the watchtower. Pencil, smudged, careful.",
                           "The lamps, the paths worn between them, and the tower. Whoever drew it never marked the door." },
            how = "{key} to hold it up and look at it." },
        new Entry { item = Hotbar.Item.Drone, name = "Drone", accent = new Color(0.7f, 0.85f, 0.95f),
            lore = new[] { "A toy-shop quadcopter with a camera and a little lamp, from a shelf in the cabin's store room.",
                           "Somebody wanted to see what was out there without walking into it. The buzz of it carries, though." },
            how = "{key} to hold it, F to set it down where you want to fly it from. Its slot becomes the remote." },
        new Entry { item = Hotbar.Item.Remote, name = "Remote", accent = new Color(0.55f, 0.95f, 0.75f),
            lore = new[] { "The drone's controller: two sticks, a stubby aerial, a screen to see through its camera.",
                           "While you're looking through it you're standing out here blind. Keep an ear on what's behind you." },
            how = "{key} to hold it, F to fly the drone from wherever you are, F while flying for thermal (twice the battery), E to let go (it hovers, or lands if it's low). Pick it back up where it lands." },
        new Entry { item = Hotbar.Item.Stick, name = "Stick of Truth", accent = new Color(1f, 0.8f, 0.45f),
            lore = new[] { "A stick. It knows things.", "(Dev tools: from the F9 window.)" },
            how = "{key} to hold it. Right click to choose what to spawn, left click to spawn it where you point. Double tap Space to fly." },
    };

    Hotbar hotbar;
    Flashlight flashlight;
    int selected = -1;            // the item whose page is showing (an Item), or -1
    int dragFrom = -1;            // the slot being dragged from, or -1
    bool dragging;
    Vector2 dragStart;
    float openTime;
    AudioSource sfx;
    GUIStyle title, slotName, slotCount, heading, sub, body, hint, button;

    void Awake()
    {
        Tuning.Apply(this); // saved tuning (Inspector changes made in Play are kept)
        hotbar = GetComponent<Hotbar>();
    }

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
        if (HorrorInput.InventoryPressed && !MainMenu.Showing && !DroneUnit.Piloting) SetOpen(!IsOpen);
        else if (IsOpen && HorrorInput.ReleaseCursorPressed) SetOpen(false);
    }

    void SetOpen(bool open)
    {
        IsOpen = open;
        dragFrom = -1;
        dragging = false;
        if (!open) closedFrame = Time.frameCount;
        openTime = Time.unscaledTime;
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;
        if (open && hotbar != null)
        {
            // Open on whatever's in your hand.
            selected = (int)hotbar.Selected;
        }
    }

    void OnGUI()
    {
        if (!IsOpen || hotbar == null) return;
        if (flashlight == null) flashlight = FindAnyObjectByType<Flashlight>();
        GUI.depth = -800;
        float unit = Mathf.Max(0.75f, Screen.height / 1080f) * SizeScale;
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

        // ---- The hotbar's slots, left: click to read, drag to move.
        const int columns = 4;
        float slot = 116f * unit, gap = 16f * unit;
        var grid = new Rect(panel.x, panel.y + 10f * unit, columns * slot + (columns - 1) * gap, 2 * slot + gap + 26f * unit);
        var ev = Event.current;
        Vector2 mouse = ev.mousePosition;
        int under = -1;
        for (int i = 0; i < Hotbar.SlotTotal; i++)
            if (SlotRect(grid, i, slot, gap, columns).Contains(mouse)) under = i;

        if (ev.type == EventType.MouseDown && ev.button == 0 && under >= 0)
        {
            if (hotbar.TryGetSlot(under, out Hotbar.Item picked))
            {
                selected = (int)picked;
                dragFrom = under;
                dragStart = mouse;
                dragging = false;
            }
            else selected = -1;
            ev.Use();
        }
        else if (ev.type == EventType.MouseDrag && dragFrom >= 0)
        {
            if (!dragging && (mouse - dragStart).sqrMagnitude > 25f) dragging = true;
            ev.Use();
        }
        else if (ev.type == EventType.MouseUp && ev.button == 0 && dragFrom >= 0)
        {
            if (dragging && under >= 0 && under != dragFrom)
            {
                hotbar.SwapSlots(dragFrom, under);
                Click(1.1f);
            }
            dragFrom = -1;
            dragging = false;
            ev.Use();
        }

        for (int i = 0; i < Hotbar.SlotTotal; i++)
        {
            var r = SlotRect(grid, i, slot, gap, columns);
            bool filled = hotbar.TryGetSlot(i, out Hotbar.Item item);
            bool lifted = dragging && i == dragFrom;
            bool target = dragging && i == under && i != dragFrom;
            bool reading = filled && (int)item == selected;
            hotbar.DrawSlot(r, i, filled && !lifted, item, filled && hotbar.Selected == item && !lifted, unit * 1.6f, a);
            if (reading && !lifted)
            {
                Color ac = Hotbar.AccentOf(item);
                Hotbar.Rounded(new Rect(r.x - 4f * unit, r.y - 4f * unit, r.width + 8f * unit, r.height + 8f * unit), new Color(ac.r, ac.g, ac.b, 0.6f * a), 15f * unit, 1.5f * unit);
            }
            if (target) Hotbar.Rounded(new Rect(r.x - 4f * unit, r.y - 4f * unit, r.width + 8f * unit, r.height + 8f * unit), new Color(1f, 0.85f, 0.55f, 0.9f * a), 15f * unit, 2.5f * unit);
            if (filled && !lifted)
                Text(new Rect(r.x, r.yMax + 2f * unit, r.width, 20f * unit), Hotbar.NameOf(item).ToUpperInvariant(), hint, a * (reading ? 0.95f : 0.55f));
        }
        Text(new Rect(grid.x, grid.yMax + 34f * unit, grid.width, 20f * unit), "CLICK TO READ  ·  DRAG TO MOVE", hint, a * 0.6f);

        // What's being dragged follows the pointer.
        if (dragging && dragFrom >= 0 && hotbar.TryGetSlot(dragFrom, out Hotbar.Item held))
        {
            var ghost = new Rect(mouse.x - slot * 0.4f, mouse.y - slot * 0.4f, slot * 0.8f, slot * 0.8f);
            Hotbar.Rounded(ghost, new Color(0.07f, 0.065f, 0.06f, 0.85f), 12f * unit);
            Hotbar.Rounded(ghost, Hotbar.AccentOf(held), 12f * unit, 2f * unit);
            Hotbar.DrawIcon(held, new Rect(ghost.x + ghost.width * 0.2f, ghost.y + ghost.height * 0.2f, ghost.width * 0.6f, ghost.height * 0.6f), Hotbar.AccentOf(held), 1f);
        }

        // ---- The page about the selected one, right.
        var page = new Rect(grid.xMax + 40f * unit, panel.y, panel.xMax - grid.xMax - 40f * unit, panel.height);
        Fill(new Rect(page.x - 20f * unit, page.y, 1f * unit, page.height * 0.85f), new Color(0.6f, 0.55f, 0.45f, 0.35f * a));
        int have = selected >= 0 ? hotbar.CountOf((Hotbar.Item)selected) : 0;
        float y = page.y;
        if (have <= 0)
        {
            Text(new Rect(page.x, y, page.width, 44f * unit), "E M P T Y", heading, a * 0.5f, TextAnchor.UpperLeft);
            y += 56f * unit;
            Text(new Rect(page.x, y, page.width, 200f * unit), "Nothing here yet. Whatever you find next goes in the first empty slot.", body, a * 0.6f, TextAnchor.UpperLeft);
        }
        else
        {
            var sel = Entries[Mathf.Clamp(selected, 0, Entries.Length - 1)];
            int slotIndex = hotbar.SlotOf(sel.item);
            string how = sel.how.Replace("{key}", slotIndex >= 0 ? (slotIndex + 1).ToString() : "?");
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
            float hh = sub.CalcHeight(new GUIContent(how), page.width);
            Text(new Rect(page.x, y, page.width, hh), how, sub, a * 0.85f, TextAnchor.UpperLeft);
            y += hh + 24f * unit;

            // Hold it.
            bool inHand = hotbar.Selected == sel.item;
            var holdRect = new Rect(page.x, y, 150f * unit, 38f * unit);
            bool overHold = holdRect.Contains(mouse) && !inHand;
            Hotbar.Rounded(holdRect, new Color(0f, 0f, 0f, 0.6f * a), 8f * unit);
            Hotbar.Rounded(holdRect, new Color(sel.accent.r, sel.accent.g, sel.accent.b, (inHand ? 0.3f : overHold ? 1f : 0.6f) * a), 8f * unit, Mathf.Max(1f, unit));
            Text(holdRect, inHand ? "IN HAND" : "HOLD", button, inHand ? a * 0.45f : a);
            if (overHold && ev.type == EventType.MouseDown && ev.button == 0)
            {
                hotbar.Select(sel.item);
                ev.Use();
            }
        }

        // How to close it.
        Text(new Rect(panel.x, panel.yMax - 10f * unit, panel.width, 24f * unit), "TAB  OR  ESC  TO  CLOSE", hint, a * 0.7f);
    }

    static Rect SlotRect(Rect grid, int i, float slot, float gap, int columns) =>
        new Rect(grid.x + (i % columns) * (slot + gap), grid.y + (i / columns) * (slot + gap + 26f * slot / 116f), slot, slot);

    void Click(float pitch)
    {
        if (sfx == null) sfx = ProceduralAudio.AddSource(gameObject, null, false, false, 0.5f);
        sfx.pitch = pitch;
        sfx.PlayOneShot(ProceduralAudio.Pickup, 0.35f);
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
            case Hotbar.Item.Map: return "The trails, the lamps, the tower";
            case Hotbar.Item.Drone: return DroneUnit.Dead ? "Battery dead" : $"Battery {Mathf.CeilToInt(DroneUnit.Charge * 100f)}% · set it down to fly it";
            case Hotbar.Item.Remote: return DroneUnit.Dead ? "The drone's battery is dead" : $"Drone battery {Mathf.CeilToInt(DroneUnit.Charge * 100f)}%";
            case Hotbar.Item.Stick: return "Dev tools";
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
