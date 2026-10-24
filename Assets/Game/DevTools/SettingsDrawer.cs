using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Game.DevTools
{
    /// <summary>
    /// Draws a settings object's public fields as dev-window controls, grouped in folding sections by their
    /// [Header]s: sliders for numbers with a [Range], switches for bools, R/G/B sliders for colours. Each value
    /// that differs from its default gets a reset button. Returns true if anything changed.
    /// </summary>
    public static class SettingsDrawer
    {
        static readonly HashSet<string> collapsed = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => collapsed.Clear();

        public static bool Draw(string title, object settings, object defaults, string search)
        {
            if (settings == null) return false;
            bool changed = false;
            bool searching = !string.IsNullOrEmpty(search);
            string section = title;
            bool sectionOpen = true;
            bool sectionShown = false;

            foreach (var field in settings.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                var header = field.GetCustomAttribute<HeaderAttribute>();
                if (header != null)
                {
                    section = $"{title}: {header.header}";
                    sectionOpen = !collapsed.Contains(section);
                    sectionShown = false;
                }

                string label = Nicify(field.Name);
                if (searching && label.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0 &&
                    section.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                if (!sectionShown)
                {
                    sectionShown = true;
                    if (GUILayout.Button((sectionOpen || searching ? "▾ " : "▸ ") + section, DevStyle.SectionButton))
                    {
                        if (sectionOpen) collapsed.Add(section);
                        else collapsed.Remove(section);
                        sectionOpen = !sectionOpen;
                    }
                }
                if (!sectionOpen && !searching) continue;

                changed |= DrawField(field, label, settings, defaults);
            }
            return changed;
        }

        static bool DrawField(FieldInfo field, string label, object settings, object defaults)
        {
            object value = field.GetValue(settings);
            object initial = defaults != null ? field.GetValue(defaults) : null;
            object result = value;
            var tooltip = field.GetCustomAttribute<TooltipAttribute>()?.tooltip;

            GUILayout.BeginHorizontal();
            switch (value)
            {
                case float f:
                {
                    var range = field.GetCustomAttribute<RangeAttribute>();
                    GUILayout.Label(new GUIContent(label, tooltip), DevStyle.Label, GUILayout.Width(DevStyle.LabelWidth));
                    // (No [Range]: a slider from 0 to a few times its default, and you can type past it.)
                    float d = initial is float df ? df : f;
                    float lo = range != null ? range.min : Mathf.Min(0f, d * 3f);
                    float hi = range != null ? range.max : Mathf.Max(1f, d * 3f, f);
                    result = DevStyle.Slider(field.DeclaringType.Name + "." + field.Name, f, lo, hi);
                    break;
                }
                case int n:
                {
                    var range = field.GetCustomAttribute<RangeAttribute>();
                    GUILayout.Label(new GUIContent(label, tooltip), DevStyle.Label, GUILayout.Width(DevStyle.LabelWidth));
                    int d = initial is int di ? di : n;
                    float lo = range != null ? range.min : Mathf.Min(0, d * 3);
                    float hi = range != null ? range.max : Mathf.Max(10, d * 3, n);
                    result = Mathf.RoundToInt(DevStyle.Slider(field.DeclaringType.Name + "." + field.Name, n, lo, hi, true));
                    break;
                }
                case bool b:
                    GUILayout.EndHorizontal();
                    result = DevStyle.Switch(label, b);
                    GUILayout.BeginHorizontal();
                    break;
                case Color c:
                {
                    GUILayout.Label(new GUIContent(label, tooltip), DevStyle.Label, GUILayout.Width(DevStyle.LabelWidth));
                    var swatch = GUILayoutUtility.GetRect(26, 20, GUILayout.Width(26), GUILayout.Height(20));
                    if (Event.current.type == EventType.Repaint)
                    {
                        var old = GUI.color;
                        GUI.color = new Color(c.r, c.g, c.b, 1f);
                        GUI.DrawTexture(swatch, Texture2D.whiteTexture);
                        GUI.color = old;
                    }
                    GUILayout.Label($"{c.r:0.00} {c.g:0.00} {c.b:0.00}", DevStyle.MutedLabel);
                    GUILayout.EndHorizontal();
                    c.r = ChannelSlider("R", c.r, key: field.DeclaringType.Name + "." + field.Name);
                    c.g = ChannelSlider("G", c.g, key: field.DeclaringType.Name + "." + field.Name);
                    c.b = ChannelSlider("B", c.b, key: field.DeclaringType.Name + "." + field.Name);
                    GUILayout.BeginHorizontal();
                    result = c;
                    break;
                }
                case Vector3 v:
                    GUILayout.Label(new GUIContent(label, tooltip), DevStyle.Label, GUILayout.Width(DevStyle.LabelWidth));
                    GUILayout.Label($"{v.x:0.00}, {v.y:0.00}, {v.z:0.00}", DevStyle.MutedLabel);
                    GUILayout.EndHorizontal();
                    v.x = ChannelSlider("X", v.x, -1f, 1f, field.DeclaringType.Name + "." + field.Name);
                    v.y = ChannelSlider("Y", v.y, -1f, 1f, field.DeclaringType.Name + "." + field.Name);
                    v.z = ChannelSlider("Z", v.z, -1f, 1f, field.DeclaringType.Name + "." + field.Name);
                    GUILayout.BeginHorizontal();
                    result = v;
                    break;
                default:
                    GUILayout.Label($"{label}: {value}", DevStyle.MutedLabel);
                    break;
            }

            bool differs = initial != null && !Equals(result, initial);
            GUILayout.FlexibleSpace();
            if (differs && GUILayout.Button(new GUIContent("↺", "Back to the default"), DevStyle.SmallButton)) result = initial;
            GUILayout.EndHorizontal();

            if (Equals(result, value)) return false;
            field.SetValue(settings, result);
            return true;
        }

        static float ChannelSlider(string label, float value, float min = 0f, float max = 1f, string key = "")
        {
            GUILayout.BeginHorizontal();
            GUILayout.Space(16);
            GUILayout.Label(label, DevStyle.MutedLabel, GUILayout.Width(22));
            value = DevStyle.Slider(key + "." + label, value, min, max);
            GUILayout.EndHorizontal();
            return value;
        }

        /// <summary>"mistPatchSize" to "Mist patch size".</summary>
        public static string Nicify(string name)
        {
            var sb = new System.Text.StringBuilder(name.Length + 8);
            for (int i = 0; i < name.Length; i++)
            {
                char c = name[i];
                if (i == 0) sb.Append(char.ToUpperInvariant(c));
                else if (char.IsUpper(c)) sb.Append(' ').Append(char.ToLowerInvariant(c));
                else sb.Append(c);
            }
            return sb.ToString();
        }
    }
}
