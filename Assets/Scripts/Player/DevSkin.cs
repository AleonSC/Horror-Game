using UnityEngine;

/// <summary>
/// The dev tools' look (IMGUI): a dark, slightly warm panel with rounded cards, amber accents, slim sliders
/// and pill switches. Textures are made in code. Use() returns the skin to set as GUI.skin while drawing;
/// the extra styles (Title, Card, Section...) are for the dev tools' own layout. Rebuilt when the screen
/// size changes, so text scales with the screen.
/// </summary>
public static class DevSkin
{
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() { skin = null; unit = -1f; }

    public static readonly Color Accent = new Color(1f, 0.66f, 0.3f);
    public static readonly Color Text = new Color(0.9f, 0.87f, 0.82f);
    public static readonly Color Dim = new Color(0.62f, 0.6f, 0.57f);
    public static readonly Color Good = new Color(0.45f, 0.85f, 0.5f);
    public static readonly Color Warn = new Color(1f, 0.4f, 0.32f);

    static GUISkin skin;
    static float unit = -1f;

    public static GUIStyle Title, Subtitle, Section, Card, Panel, Header, Tab, TabOn, Hint, Value, SwitchOn, SwitchOff,
        Foldout, Small, Accented, Status;

    public static float Unit => unit;

    public static GUISkin Use(float scale)
    {
        if (skin != null && Mathf.Approximately(scale, unit)) return skin;
        unit = scale;
        Build();
        return skin;
    }

    static int Px(float v) => Mathf.RoundToInt(v * unit);

    static void Build()
    {
        if (skin == null) skin = Object.Instantiate(GUI.skin);
        Font font = skin.font;

        var bgPanel = Rounded(new Color(0.055f, 0.05f, 0.05f, 0.96f), new Color(1f, 0.66f, 0.3f, 0.35f), 8);
        var bgCard = Rounded(new Color(0.1f, 0.09f, 0.085f, 0.95f), new Color(1f, 1f, 1f, 0.06f), 6);
        var bgHeader = Rounded(new Color(0.13f, 0.1f, 0.08f, 1f), new Color(1f, 0.66f, 0.3f, 0.25f), 6);
        var btn = Rounded(new Color(0.17f, 0.155f, 0.145f, 1f), new Color(1f, 1f, 1f, 0.08f), 5);
        var btnHover = Rounded(new Color(0.24f, 0.2f, 0.17f, 1f), new Color(1f, 0.66f, 0.3f, 0.4f), 5);
        var btnDown = Rounded(new Color(0.36f, 0.24f, 0.13f, 1f), new Color(1f, 0.66f, 0.3f, 0.7f), 5);
        var btnOn = Rounded(new Color(0.42f, 0.27f, 0.12f, 1f), new Color(1f, 0.7f, 0.35f, 0.9f), 5);
        var pillOn = Rounded(new Color(0.85f, 0.52f, 0.2f, 1f), new Color(1f, 0.8f, 0.5f, 1f), 7);
        var pillOff = Rounded(new Color(0.2f, 0.19f, 0.18f, 1f), new Color(1f, 1f, 1f, 0.12f), 7);
        var track = Track(new Color(0.24f, 0.22f, 0.2f, 1f), Px(14));
        var thumb = Rounded(Accent, new Color(1f, 0.85f, 0.65f, 1f), 8);
        var thumbHover = Rounded(new Color(1f, 0.78f, 0.5f), Color.white, 8);
        var field = Rounded(new Color(0.03f, 0.03f, 0.03f, 1f), new Color(1f, 1f, 1f, 0.12f), 4);
        var bar = Rounded(new Color(0.2f, 0.19f, 0.18f, 0.6f), new Color(0f, 0f, 0f, 0f), 3);
        var barThumb = Rounded(new Color(0.45f, 0.38f, 0.32f, 1f), new Color(0f, 0f, 0f, 0f), 3);

        skin.label = new GUIStyle(GUI.skin.label) { fontSize = Px(14), wordWrap = false };
        skin.label.normal.textColor = Text;

        skin.button = Box(btn, btnHover, btnDown, Px(14), Text);
        skin.button.onNormal.background = btnOn;
        skin.button.onHover.background = btnOn;
        skin.button.onActive.background = btnDown;
        skin.button.onNormal.textColor = skin.button.onHover.textColor = new Color(1f, 0.88f, 0.7f);
        skin.button.padding = new RectOffset(Px(10), Px(10), Px(5), Px(5));
        skin.button.margin = new RectOffset(Px(2), Px(2), Px(2), Px(2));

        skin.toggle = new GUIStyle(skin.button) { alignment = TextAnchor.MiddleLeft };

        skin.box = Box(bgCard, bgCard, bgCard, Px(14), Text);

        skin.textField = Box(field, field, field, Px(14), Text);
        skin.textField.alignment = TextAnchor.MiddleLeft;
        skin.textField.padding = new RectOffset(Px(8), Px(8), Px(4), Px(4));
        skin.textField.focused.background = field;
        skin.textField.focused.textColor = Color.white;
        skin.textArea = new GUIStyle(skin.textField) { wordWrap = true, alignment = TextAnchor.UpperLeft, fontSize = Px(12) };

        // The track is drawn at its own height (a thin line through the middle of a clear strip), so the
        // round thumb can sit on it.
        skin.horizontalSlider = new GUIStyle(GUI.skin.horizontalSlider)
        {
            fixedHeight = Px(14),
            border = new RectOffset(3, 3, 0, 0),
            margin = new RectOffset(Px(4), Px(4), Px(5), Px(5)),
            padding = new RectOffset(0, 0, 0, 0),
            overflow = new RectOffset(0, 0, 0, 0),
        };
        skin.horizontalSlider.normal.background = track;
        skin.horizontalSlider.hover.background = track;
        skin.horizontalSlider.active.background = track;
        skin.horizontalSliderThumb = new GUIStyle(GUI.skin.horizontalSliderThumb)
        {
            fixedWidth = Px(14),
            fixedHeight = Px(14),
            border = new RectOffset(0, 0, 0, 0),
            margin = new RectOffset(0, 0, 0, 0),
            overflow = new RectOffset(0, 0, 0, 0),
        };
        skin.horizontalSliderThumb.normal.background = thumb;
        skin.horizontalSliderThumb.hover.background = thumbHover;
        skin.horizontalSliderThumb.active.background = thumbHover;

        skin.verticalScrollbar = new GUIStyle(GUI.skin.verticalScrollbar) { fixedWidth = Px(8), border = new RectOffset(3, 3, 3, 3), margin = new RectOffset(Px(4), 0, 0, 0) };
        skin.verticalScrollbar.normal.background = bar;
        skin.verticalScrollbarThumb = new GUIStyle(GUI.skin.verticalScrollbarThumb) { fixedWidth = Px(8), border = new RectOffset(3, 3, 3, 3) };
        skin.verticalScrollbarThumb.normal.background = barThumb;
        skin.horizontalScrollbar = new GUIStyle(GUI.skin.horizontalScrollbar) { fixedHeight = 0f };
        skin.scrollView = new GUIStyle(GUI.skin.scrollView);

        Panel = Box(bgPanel, bgPanel, bgPanel, Px(14), Text);
        Panel.padding = new RectOffset(Px(16), Px(16), Px(12), Px(14));

        Card = Box(bgCard, bgCard, bgCard, Px(14), Text);
        Card.padding = new RectOffset(Px(12), Px(12), Px(10), Px(10));
        Card.margin = new RectOffset(0, Px(6), 0, Px(10));

        Header = Box(bgHeader, bgHeader, bgHeader, Px(14), Text);
        Header.padding = new RectOffset(Px(14), Px(14), Px(8), Px(8));

        Title = new GUIStyle(skin.label) { fontSize = Px(22), fontStyle = FontStyle.Bold };
        Title.normal.textColor = Accent;
        Subtitle = new GUIStyle(skin.label) { fontSize = Px(12) };
        Subtitle.normal.textColor = Dim;

        Section = new GUIStyle(skin.label) { fontSize = Px(15), fontStyle = FontStyle.Bold, margin = new RectOffset(0, 0, 0, Px(6)) };
        Section.normal.textColor = new Color(1f, 0.8f, 0.55f);

        Foldout = new GUIStyle(skin.button) { alignment = TextAnchor.MiddleLeft, fontSize = Px(15), fontStyle = FontStyle.Bold };
        Foldout.normal.background = bgHeader;
        Foldout.normal.textColor = new Color(1f, 0.8f, 0.55f);
        Foldout.hover.textColor = Color.white;
        Foldout.padding = new RectOffset(Px(12), Px(12), Px(7), Px(7));

        Tab = new GUIStyle(skin.button) { fontSize = Px(14), fontStyle = FontStyle.Bold, padding = new RectOffset(Px(14), Px(14), Px(7), Px(7)) };
        TabOn = new GUIStyle(Tab);
        TabOn.normal.background = TabOn.hover.background = btnOn;
        TabOn.normal.textColor = TabOn.hover.textColor = new Color(1f, 0.9f, 0.72f);

        Hint = new GUIStyle(skin.label) { fontSize = Px(12), wordWrap = true, fontStyle = FontStyle.Italic };
        Hint.normal.textColor = Dim;
        Small = new GUIStyle(skin.label) { fontSize = Px(12) };
        Small.normal.textColor = Dim;
        Value = new GUIStyle(skin.label) { fontSize = Px(13), alignment = TextAnchor.MiddleRight };
        Value.normal.textColor = new Color(1f, 0.85f, 0.62f);
        Accented = new GUIStyle(skin.label) { fontSize = Px(14), fontStyle = FontStyle.Bold };
        Accented.normal.textColor = Accent;
        Status = new GUIStyle(skin.label) { fontSize = Px(13), wordWrap = true };
        Status.normal.textColor = Good;

        SwitchOn = Box(pillOn, pillOn, pillOn, Px(12), new Color(0.12f, 0.06f, 0.02f));
        SwitchOn.fontStyle = FontStyle.Bold;
        SwitchOn.alignment = TextAnchor.MiddleCenter;
        SwitchOff = Box(pillOff, pillOff, pillOff, Px(12), Dim);
        SwitchOff.fontStyle = FontStyle.Bold;
        SwitchOff.alignment = TextAnchor.MiddleCenter;
        if (font != null) foreach (var s in new[] { Title, Subtitle, Section, Hint }) s.font = font;
    }

    static GUIStyle Box(Texture2D normal, Texture2D hover, Texture2D active, int fontSize, Color text)
    {
        var s = new GUIStyle(GUI.skin.box)
        {
            border = new RectOffset(8, 8, 8, 8),
            fontSize = fontSize,
            alignment = TextAnchor.MiddleCenter,
            wordWrap = false,
        };
        s.normal.background = normal;
        s.hover.background = hover;
        s.active.background = active;
        s.focused.background = normal;
        s.onNormal.background = normal;
        s.normal.textColor = s.hover.textColor = s.active.textColor = s.focused.textColor = text;
        return s;
    }

    /// <summary>A slider track: a clear strip this many pixels high with a rounded line through its middle.</summary>
    static Texture2D Track(Color color, int height)
    {
        height = Mathf.Max(height, 6);
        var tex = new Texture2D(7, height, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
        float mid = height * 0.5f, half = Mathf.Max(2f, height * 0.16f);
        for (int y = 0; y < height; y++)
            for (int x = 0; x < 7; x++)
            {
                float edgeX = Mathf.Min(x + 0.5f, 7 - (x + 0.5f)); // fades in at the ends
                float a = Mathf.Clamp01(half - Mathf.Abs(y + 0.5f - mid) + 0.5f) * Mathf.Clamp01(edgeX * 1.5f);
                Color c = color;
                c.a *= a;
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
        return tex;
    }

    /// <summary>A rounded rectangle (for 9-slicing with a border of 8): fill, a one pixel edge, soft corners.</summary>
    static Texture2D Rounded(Color fill, Color edge, int radius)
    {
        const int size = 17;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, hideFlags = HideFlags.DontSave };
        float r = Mathf.Clamp(radius, 1, 8);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                // Distance outside the rounded rectangle's inner box (negative inside).
                float cx = Mathf.Max(Mathf.Max(r - (x + 0.5f), 0f), Mathf.Max((x + 0.5f) - (size - r), 0f));
                float cy = Mathf.Max(Mathf.Max(r - (y + 0.5f), 0f), Mathf.Max((y + 0.5f) - (size - r), 0f));
                float d = Mathf.Sqrt(cx * cx + cy * cy) - r; // <= 0 inside
                float inside = Mathf.Clamp01(0.5f - d);
                float rim = Mathf.Clamp01(1f - Mathf.Abs(d + 0.75f));
                Color c = Color.Lerp(fill, edge, rim * edge.a);
                c.a = Mathf.Max(fill.a, edge.a * rim) * inside;
                tex.SetPixel(x, y, c);
            }
        tex.Apply();
        return tex;
    }
}
