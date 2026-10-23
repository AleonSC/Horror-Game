using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// The dev tools' Look tab: the fine detail of how the game looks, live, in Play: exposure and bloom, HDRP
/// fog and volumetrics, shadows and shading, the flashlight's finer points, the ground mist, the lamps and
/// the night sky. Each setting is a field on the game's own component (HdrpAtmosphere, Flashlight,
/// GroundMist, Lamp, NightSky), changed on every one of them in the scene.
///
/// Changes are saved between sessions (PlayerPrefs) and put back when the forest is built. Copy settings
/// puts everything (these and the Lighting tab's) on the clipboard as plain text, to paste to Claude to make
/// them the game's defaults; Paste settings reads that text back in. Reset puts back what the game started
/// with.
/// </summary>
public static class LookSettings
{
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics()
    {
        defaults.Clear();
        targets.Clear();
        targetsTime = -1f;
        savedApplied = false;
    }

    const string SaveKey = "Horror.Look.v1";

    /// <summary>One setting: a field on a component type, with a slider range.</summary>
    public class Knob
    {
        public string section, label, tip, format;
        public Type type;
        public string field;
        public float min, max;
        public FieldInfo info;
        public string Key => type.Name + "." + field;
    }

    public static readonly List<Knob> Knobs = new List<Knob>();
    public static readonly List<string> Sections = new List<string>();

    static readonly Dictionary<string, object> defaults = new Dictionary<string, object>();
    static readonly Dictionary<Type, Object[]> targets = new Dictionary<Type, Object[]>();
    static float targetsTime = -1f;
    static bool savedApplied;

    static LookSettings()
    {
        string s = "Exposure & glow";
        Add(s, typeof(HdrpAtmosphere), "exposure", -5f, 3f, "0.00", "Fixed exposure. Lower is brighter.");
        Add(s, typeof(HdrpAtmosphere), "noTonemapping", 0f, 1f, null, "Tonemapping off (as in URP). On, HDRP's ACES crushes the dark end.");
        Add(s, typeof(HdrpAtmosphere), "bloomOverride", 0f, 1f, null, "Use the bloom below instead of the project's own.");
        Add(s, typeof(HdrpAtmosphere), "bloomIntensity", 0f, 1f, "0.00", "Glow round bright things (eyes, embers, bulbs).");

        s = "Fog & volumetrics";
        Add(s, typeof(HdrpAtmosphere), "fogDistanceScale", 0.1f, 4f, "0.00", "Higher is thinner fog (the Lighting tab's density works on top).");
        Add(s, typeof(HdrpAtmosphere), "volumetricAlbedo", 0f, 1f, "0.00", "How much light shows in the air: beams and glows.");
        Add(s, typeof(HdrpAtmosphere), "volumetricDistance", 20f, 400f, "0", "How far out the lit fog reaches.");
        Add(s, typeof(HdrpAtmosphere), "fogAnisotropy", -1f, 1f, "0.00", "Above 0: the fog glows most looking toward a light.");
        Add(s, typeof(HdrpAtmosphere), "lampFogGlow", 0f, 2f, "0.00", "How much lamps and torches light the fog round them.");
        Add(s, typeof(Flashlight), "fogGlow", 0f, 2f, "0.00", "How much the flashlight lights the fog in the air.");
        Add(s, typeof(HdrpAtmosphere), "beamOverShapes", 0f, 3f, "0.00", "The beam's glow drawn over the giant and far shapes.");

        s = "Shadows & shading";
        Add(s, typeof(HdrpAtmosphere), "shadowDistance", 20f, 500f, "0", "How far out things cast shadows (metres).");
        Add(s, typeof(HdrpAtmosphere), "moonShadowResolution", 256f, 4096f, "pow2", "The moon's shadow map: sharper and costlier up.");
        Add(s, typeof(HdrpAtmosphere), "moonShadowStrength", 0f, 1f, "0.00", "How dark the moon's shadows are.");
        Add(s, typeof(HdrpAtmosphere), "flashlightShadowResolution", 256f, 4096f, "pow2", "The flashlight's shadow map.");
        Add(s, typeof(HdrpAtmosphere), "flashlightShadowStrength", 0f, 1f, "0.00", "How dark the flashlight's shadows are.");
        Add(s, typeof(HdrpAtmosphere), "contactShadows", 0f, 1f, null, "Small sharp shadows where things touch.");
        Add(s, typeof(HdrpAtmosphere), "contactShadowLength", 0f, 1f, "0.00", "How far contact shadows reach.");
        Add(s, typeof(HdrpAtmosphere), "ambientOcclusionStrength", 0f, 4f, "0.00", "Darkening in creases and under things (screen space). 0 is off.");

        s = "Flashlight";
        Add(s, typeof(Flashlight), "boost", 0f, 6f, "0.00", "Multiplies the Lighting tab's flashlight brightness.");
        Add(s, typeof(Flashlight), "nearDimDistance", 0f, 12f, "0.0", "Pointed at something closer than this, the beam dims.");
        Add(s, typeof(Flashlight), "nearDimAmount", 0f, 1f, "0.00", "How bright it is right up close (share of full).");
        Add(s, typeof(Flashlight), "swaySpeed", 0f, 15f, "0.0", "How quickly the beam follows your view.");
        Add(s, typeof(Flashlight), "maxLagAngle", 0f, 30f, "0", "How far the beam can trail behind where you look.");

        s = "Ground mist";
        Add(s, typeof(GroundMist), "density", 0f, 2f, "0.00", "How thick the mist on the ground is.");
        Add(s, typeof(GroundMist), "mistHeight", 0.1f, 4f, "0.00", "How high it rises (metres).");
        Add(s, typeof(GroundMist), "mistColor", 0f, 0.3f, null, "Its colour.");
        Add(s, typeof(GroundMist), "patchiness", 0f, 1f, "0.00", "0 even, 1 in clumps with clear gaps.");
        Add(s, typeof(GroundMist), "noiseScale", 0.005f, 0.3f, "0.000", "The size of its swirls (smaller is bigger swirls).");
        Add(s, typeof(GroundMist), "patchScale", 0.002f, 0.08f, "0.000", "The size of the patches.");
        Add(s, typeof(GroundMist), "driftSpeed", 0f, 2f, "0.00", "How fast it drifts.");
        Add(s, typeof(GroundMist), "maxDistance", 10f, 150f, "0", "How far out it's drawn.");
        Add(s, typeof(GroundMist), "clearAmount", 0f, 1f, "0.00", "How much it clears round you.");
        Add(s, typeof(GroundMist), "clearNear", 0f, 10f, "0.0", "Clear within this of you...");
        Add(s, typeof(GroundMist), "clearFar", 0f, 30f, "0.0", "...back to full by this far.");
        Add(s, typeof(GroundMist), "lampClearAmount", 0f, 1f, "0.00", "How much it clears round lit lamps.");
        Add(s, typeof(GroundMist), "lampClearNear", 0f, 20f, "0.0", "Clear within this of a lamp...");
        Add(s, typeof(GroundMist), "lampClearFar", 0f, 40f, "0.0", "...back to full by this far.");
        Add(s, typeof(GroundMist), "flashlightStrength", 0f, 0.3f, "0.000", "How much the flashlight lights the mist.");
        Add(s, typeof(GroundMist), "lampStrength", 0f, 0.3f, "0.000", "How much lamps light the mist.");

        s = "Lamps";
        Add(s, typeof(Lamp), "lightIntensity", 0f, 20f, "0.00", "How bright a lit lamp is.");
        Add(s, typeof(Lamp), "lightColor", 0f, 1f, null, "The colour of its light.");
        Add(s, typeof(Lamp), "markerColor", 0f, 1f, null, "The colour of the marker over an unlit lamp.");

        s = "Night sky";
        Add(s, typeof(NightSky), "cloudColor", 0f, 0.3f, null, "The clouds' colour.");
        Add(s, typeof(NightSky), "cloudSpeed", 0f, 0.03f, "0.0000", "How fast the clouds move.");
    }

    static void Add(string section, Type type, string field, float min, float max, string format, string tip)
    {
        var info = type.GetField(field, BindingFlags.Public | BindingFlags.Instance);
        if (info == null) { Debug.LogWarning($"LookSettings: no field {type.Name}.{field}"); return; }
        if (!Sections.Contains(section)) Sections.Add(section);
        Knobs.Add(new Knob
        {
            section = section, type = type, field = field, info = info, min = min, max = max, format = format, tip = tip,
            label = Nice(field),
        });
    }

    /// <summary>"lampClearNear" as "Lamp clear near".</summary>
    static string Nice(string field)
    {
        var sb = new StringBuilder();
        for (int i = 0; i < field.Length; i++)
        {
            char c = field[i];
            if (i == 0) sb.Append(char.ToUpperInvariant(c));
            else if (char.IsUpper(c)) sb.Append(' ').Append(char.ToLowerInvariant(c));
            else sb.Append(c);
        }
        return sb.ToString();
    }

    // ---------------------------------------------------------------- Values

    /// <summary>Every live component of this type (refreshed about once a second).</summary>
    public static Object[] Targets(Type type)
    {
        if (Time.unscaledTime - targetsTime > 1f)
        {
            targetsTime = Time.unscaledTime;
            targets.Clear();
        }
        if (!targets.TryGetValue(type, out var found))
        {
            found = Object.FindObjectsByType(type, FindObjectsSortMode.None);
            targets[type] = found;
            // What the game started with, the first time each is seen (before anything saved is put back).
            if (found.Length > 0)
                foreach (var k in Knobs)
                    if (k.type == type && !defaults.ContainsKey(k.Key)) defaults[k.Key] = k.info.GetValue(found[0]);
        }
        return found;
    }

    public static bool Available(Knob k) => Targets(k.type).Length > 0;

    public static object Get(Knob k)
    {
        var t = Targets(k.type);
        return t.Length > 0 && t[0] != null ? k.info.GetValue(t[0]) : null;
    }

    public static void Set(Knob k, object value)
    {
        foreach (var t in Targets(k.type))
        {
            if (t == null) continue;
            k.info.SetValue(t, value);
            if (t is Lamp lamp) lamp.RefreshSettings();
        }
        dirty = true;
    }

    public static bool IsDefault(Knob k)
    {
        if (!defaults.TryGetValue(k.Key, out var d)) return true;
        return Format(Get(k)) == Format(d);
    }

    public static void Reset(Knob k)
    {
        if (defaults.TryGetValue(k.Key, out var d)) Set(k, d);
    }

    public static void ResetAll()
    {
        foreach (var k in Knobs) Reset(k);
        PlayerPrefs.DeleteKey(SaveKey);
        PlayerPrefs.Save();
        dirty = false;
    }

    static bool dirty;

    /// <summary>Saves if anything changed (the dev tools call this every so often, and on closing).</summary>
    public static void SaveIfChanged()
    {
        if (!dirty) return;
        dirty = false;
        PlayerPrefs.SetString(SaveKey, ChangedText());
        PlayerPrefs.Save();
    }

    /// <summary>Puts back what was saved last session (once, after the forest is built).</summary>
    public static void ApplySaved()
    {
        if (savedApplied) return;
        savedApplied = true;
        foreach (var k in Knobs) Targets(k.type); // (so what the game starts with is known before)
        string saved = PlayerPrefs.GetString(SaveKey, "");
        if (!string.IsNullOrEmpty(saved)) ReadText(saved, null, out _);
        dirty = false;
    }

    // ---------------------------------------------------------------- Text (copy & paste)

    static string Format(object value)
    {
        var inv = CultureInfo.InvariantCulture;
        switch (value)
        {
            case null: return "";
            case float f: return f.ToString("0.#####", inv);
            case int i: return i.ToString(inv);
            case bool b: return b ? "true" : "false";
            case Color c: return string.Format(inv, "{0:0.####}, {1:0.####}, {2:0.####}, {3:0.####}", c.r, c.g, c.b, c.a);
            default: return value.ToString();
        }
    }

    static bool TryParse(Type type, string text, out object value)
    {
        var inv = CultureInfo.InvariantCulture;
        value = null;
        text = text.Trim();
        if (type == typeof(float) && float.TryParse(text, NumberStyles.Float, inv, out float f)) value = f;
        else if (type == typeof(int) && float.TryParse(text, NumberStyles.Float, inv, out float n)) value = Mathf.RoundToInt(n);
        else if (type == typeof(bool) && bool.TryParse(text, out bool b)) value = b;
        else if (type == typeof(Color))
        {
            var parts = text.Trim('(', ')', ' ').Replace("RGBA", "").Split(',');
            if (parts.Length >= 3)
            {
                var c = new float[4] { 0f, 0f, 0f, 1f };
                for (int i = 0; i < parts.Length && i < 4; i++)
                    if (!float.TryParse(parts[i].Trim('(', ')', ' '), NumberStyles.Float, inv, out c[i])) return false;
                value = new Color(c[0], c[1], c[2], c[3]);
            }
        }
        return value != null;
    }

    /// <summary>Only the settings that differ from what the game started with (what's saved).</summary>
    static string ChangedText()
    {
        var sb = new StringBuilder();
        foreach (var k in Knobs)
            if (Available(k) && !IsDefault(k)) sb.Append(k.Key).Append(" = ").Append(Format(Get(k))).Append('\n');
        return sb.ToString();
    }

    /// <summary>Everything, for the clipboard: the Look tab's settings (changed ones marked) and the Lighting tab's.</summary>
    public static string CopyText(LightingSettings lighting)
    {
        var sb = new StringBuilder();
        sb.Append("# Scary Game look settings (dev tools > Look > Copy settings)\n");
        sb.Append("# Lines marked * differ from the game's defaults.\n");
        sb.Append("ClassicLook = ").Append(HdrpAtmosphere.ClassicLook ? "true" : "false").Append('\n');
        string section = null;
        foreach (var k in Knobs)
        {
            if (!Available(k)) continue;
            if (k.section != section) { section = k.section; sb.Append("\n# ").Append(section).Append('\n'); }
            bool changed = !IsDefault(k);
            sb.Append(k.Key).Append(" = ").Append(Format(Get(k)));
            if (changed && defaults.TryGetValue(k.Key, out var d)) sb.Append("   * (default ").Append(Format(d)).Append(')');
            sb.Append('\n');
        }
        if (lighting != null) sb.Append("\n# Lighting tab\nLighting = ").Append(lighting.ValuesJson()).Append('\n');
        return sb.ToString();
    }

    /// <summary>Reads settings text (as copied); returns how many settings it set.</summary>
    public static int ReadText(string text, LightingSettings lighting, out string problem)
    {
        problem = null;
        int count = 0;
        var byKey = new Dictionary<string, Knob>();
        foreach (var k in Knobs) byKey[k.Key] = k;
        foreach (var raw in text.Replace("\r", "").Split('\n'))
        {
            string line = raw.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;
            int eq = line.IndexOf('=');
            if (eq <= 0) continue;
            string key = line.Substring(0, eq).Trim();
            string value = line.Substring(eq + 1).Trim();
            int star = value.IndexOf("   *", StringComparison.Ordinal);
            if (star >= 0) value = value.Substring(0, star).Trim();

            if (key == "Lighting")
            {
                if (lighting != null) { lighting.LoadValuesJson(value); count++; }
                continue;
            }
            if (key == "ClassicLook")
            {
                if (bool.TryParse(value, out bool classic)) { HdrpAtmosphere.ClassicLook = classic; count++; }
                continue;
            }
            if (!byKey.TryGetValue(key, out var knob)) { problem = "didn't know " + key; continue; }
            if (!TryParse(knob.info.FieldType, value, out var parsed)) { problem = "couldn't read " + key; continue; }
            Set(knob, parsed);
            count++;
        }
        return count;
    }
}
