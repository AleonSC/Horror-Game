using UnityEngine;

namespace Game.DevTools
{
    /// <summary>
    /// The dev window's look: a dark rounded panel, amber accents and pill switches. Textures are made in code
    /// once, the first time the window is drawn.
    /// </summary>
    public static class DevStyle
    {
        public static readonly Color Panel = new Color(0.08f, 0.085f, 0.1f, 0.94f);
        public static readonly Color Raised = new Color(0.15f, 0.155f, 0.18f, 1f);
        public static readonly Color Amber = new Color(1f, 0.7f, 0.25f, 1f);
        public static readonly Color Text = new Color(0.88f, 0.88f, 0.9f, 1f);
        public static readonly Color Muted = new Color(0.55f, 0.56f, 0.6f, 1f);
        static readonly Color SwitchOff = new Color(0.28f, 0.29f, 0.33f, 1f);

        static bool ready;
        static Texture2D pill, knob;

        public static GUIStyle PanelStyle, Title, Label, MutedLabel, Tab, TabActive, Button, Section, Mono, SectionButton, SmallButton, TextField, SliderTrack, SliderThumb, NumberField;

        /// <summary>How wide labels are beside sliders (a share of the window, set by the window each frame).</summary>
        public static float LabelWidth = 200f;

        // Styles are rebuilt each Play session (they'd survive it when domain reload is off).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => ready = false;

        /// <summary>Build the styles. Call from OnGUI (GUI styles can't be made outside it).</summary>
        public static void Ensure()
        {
            if (ready && pill != null) return;
            ready = true;

            pill = RoundedTexture(36, 18, 9, Color.white);
            knob = RoundedTexture(14, 14, 7, Color.white);
            var panel = RoundedTexture(24, 24, 8, Panel);
            var raised = RoundedTexture(16, 16, 5, Raised);
            var raisedHover = RoundedTexture(16, 16, 5, Color.Lerp(Raised, Amber, 0.18f));
            var amber = RoundedTexture(16, 16, 5, Amber);

            PanelStyle = new GUIStyle { normal = { background = panel }, border = new RectOffset(8, 8, 8, 8), padding = new RectOffset(14, 14, 12, 12) };
            Title = new GUIStyle { fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = Amber }, margin = new RectOffset(0, 0, 0, 8) };
            Label = new GUIStyle { fontSize = 17, normal = { textColor = Text }, wordWrap = true, margin = new RectOffset(0, 0, 3, 3) };
            MutedLabel = new GUIStyle(Label) { normal = { textColor = Muted } };
            Section = new GUIStyle(Label) { fontStyle = FontStyle.Bold, normal = { textColor = Amber }, margin = new RectOffset(0, 0, 12, 4) };
            Mono = new GUIStyle(Label) { fontSize = 15, wordWrap = false, richText = false };
            Button = new GUIStyle
            {
                fontSize = 17, alignment = TextAnchor.MiddleCenter, fixedHeight = 34,
                normal = { background = raised, textColor = Text },
                hover = { background = raisedHover, textColor = Text },
                active = { background = amber, textColor = Color.black },
                border = new RectOffset(5, 5, 5, 5), padding = new RectOffset(10, 10, 4, 4), margin = new RectOffset(2, 2, 3, 3),
            };
            Tab = new GUIStyle(Button) { normal = { background = null, textColor = Muted }, hover = { background = raised, textColor = Text } };
            TabActive = new GUIStyle(Button) { normal = { background = raised, textColor = Amber }, hover = { background = raised, textColor = Amber } };
            SectionButton = new GUIStyle(Section) { hover = { textColor = Text }, active = { textColor = Text } };
            SmallButton = new GUIStyle(Button) { fixedWidth = 34, fixedHeight = 28, padding = new RectOffset(0, 0, 0, 0) };
            var track = RoundedTexture(12, 8, 4, new Color(0.22f, 0.225f, 0.26f, 1f));
            SliderTrack = new GUIStyle { normal = { background = track }, border = new RectOffset(4, 4, 4, 4), fixedHeight = 8, margin = new RectOffset(6, 6, 13, 13) };
            SliderThumb = new GUIStyle { normal = { background = RoundedTexture(18, 18, 9, Amber) }, hover = { background = RoundedTexture(18, 18, 9, Color.white) }, active = { background = RoundedTexture(18, 18, 9, Color.white) }, fixedWidth = 18, overflow = new RectOffset(0, 0, 5, 5) }; // (18 high over the 8 high track)
            TextField = new GUIStyle(Button) { alignment = TextAnchor.MiddleLeft, normal = { background = raised, textColor = Text }, hover = { background = raised, textColor = Text }, focused = { background = raisedHover, textColor = Text } };
            NumberField = new GUIStyle(TextField) { fontSize = 16, fixedHeight = 28, alignment = TextAnchor.MiddleRight, padding = new RectOffset(4, 6, 2, 2) };
        }

        static string editing, editingText;

        /// <summary>
        /// A big slider with a box beside it to type an exact value (it may go past the slider's ends). Returns the
        /// new value. <paramref name="key"/> names it, so a half-typed number survives the frame.
        /// </summary>
        public static float Slider(string key, float value, float min, float max, bool whole = false)
        {
            Ensure();
            float v = GUILayout.HorizontalSlider(Mathf.Clamp(value, min, max), min, max, SliderTrack, SliderThumb, GUILayout.ExpandWidth(true), GUILayout.MinWidth(110));
            if (!Mathf.Approximately(v, Mathf.Clamp(value, min, max))) value = whole ? Mathf.Round(v) : v;

            string name = "num:" + key;
            GUI.SetNextControlName(name);
            bool focused = GUI.GetNameOfFocusedControl() == name;
            string shown = focused && editing == name ? editingText : value.ToString(whole ? "0" : "0.###");
            string typed = GUILayout.TextField(shown, NumberField, GUILayout.Width(84));
            if (focused)
            {
                editing = name;
                editingText = typed;
                if (typed != shown && float.TryParse(typed, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                    value = whole ? Mathf.Round(parsed) : parsed;
            }
            else if (editing == name) editing = null;
            return value;
        }

        /// <summary>A label with a pill switch on the right. Returns the new value.</summary>
        public static bool Switch(string label, bool value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, Label);
            var r = GUILayoutUtility.GetRect(36, 18, GUILayout.Width(36), GUILayout.Height(18));
            GUILayout.EndHorizontal();

            var e = Event.current;
            if (e.type == EventType.MouseDown && e.button == 0 && r.Contains(e.mousePosition))
            {
                value = !value;
                GUI.changed = true;
                e.Use();
            }
            if (e.type == EventType.Repaint)
            {
                var old = GUI.color;
                GUI.color = value ? Amber : SwitchOff;
                GUI.DrawTexture(r, pill);
                GUI.color = value ? Color.black : Text;
                GUI.DrawTexture(new Rect(value ? r.xMax - 16 : r.x + 2, r.y + 2, 14, 14), knob);
                GUI.color = old;
            }
            return value;
        }

        /// <summary>A row of buttons where one is selected. Returns the selected index.</summary>
        public static int Choice(int selected, params string[] options)
        {
            GUILayout.BeginHorizontal();
            for (int i = 0; i < options.Length; i++)
                if (GUILayout.Button(options[i], i == selected ? TabActive : Tab)) selected = i;
            GUILayout.EndHorizontal();
            return selected;
        }

        public static void Row(string label, string value)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(label, MutedLabel, GUILayout.Width(170));
            GUILayout.Label(value, Label);
            GUILayout.EndHorizontal();
        }

        // A rounded rectangle with soft (anti-aliased) corners.
        static Texture2D RoundedTexture(int width, int height, float radius, Color color)
        {
            var tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                hideFlags = HideFlags.HideAndDontSave,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
            };
            var pixels = new Color[width * height];
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                float px = x + 0.5f, py = y + 0.5f;
                float cx = Mathf.Clamp(px, radius, width - radius);
                float cy = Mathf.Clamp(py, radius, height - radius);
                float distance = Mathf.Sqrt((px - cx) * (px - cx) + (py - cy) * (py - cy));
                float coverage = Mathf.Clamp01(radius - distance + 0.5f);
                var c = color;
                c.a *= coverage;
                pixels[y * width + x] = c;
            }
            tex.SetPixels(pixels);
            tex.Apply(false, true);
            return tex;
        }
    }
}
