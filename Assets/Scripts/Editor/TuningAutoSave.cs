using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Keeps whatever you change in the Inspector during Play. Every value you edit on one of the game's
/// components is saved into its tuning (see Tuning) a moment after you change it, and the asset is written
/// when Play stops; from then on every component of that kind the game makes starts with your values.
/// Only the values you actually touched are saved, so everything else still comes from the code.
/// (Horror > Open Saved Tuning shows them; a component's Clear Tuning forgets them.)
/// </summary>
[InitializeOnLoad]
static class TuningAutoSave
{
    static readonly Dictionary<MonoBehaviour, HashSet<string>> pending = new Dictionary<MonoBehaviour, HashSet<string>>();
    static double lastChange;

    static TuningAutoSave()
    {
        Undo.postprocessModifications += OnModified;
        EditorApplication.update += Update;
        EditorApplication.playModeStateChanged += OnPlayMode;
    }

    static UndoPropertyModification[] OnModified(UndoPropertyModification[] modifications)
    {
        if (!EditorApplication.isPlaying) return modifications;
        foreach (var m in modifications)
        {
            var change = m.currentValue;
            if (change == null || !(change.target is MonoBehaviour component)) continue;
            if (component.GetType().Assembly != typeof(Tuning).Assembly) continue; // (the game's own components only)
            string field = TopField(change.propertyPath);
            if (string.IsNullOrEmpty(field) || field.StartsWith("m_")) continue; // (Unity's own: enabled, script)
            if (!pending.TryGetValue(component, out var fields)) pending[component] = fields = new HashSet<string>();
            fields.Add(field);
            lastChange = EditorApplication.timeSinceStartup;
        }
        return modifications;
    }

    static void Update()
    {
        if (pending.Count > 0 && EditorApplication.timeSinceStartup - lastChange > 0.4) Flush();
    }

    static void OnPlayMode(PlayModeStateChange state)
    {
        if (state != PlayModeStateChange.ExitingPlayMode) return;
        Flush();
        AssetDatabase.SaveAssets();
    }

    /// <summary>Writes the changed values into the tuning asset.</summary>
    static void Flush()
    {
        if (pending.Count == 0) return;
        var tuning = TuningMenu.GetOrCreate();
        foreach (var pair in pending)
        {
            var component = pair.Key;
            if (component == null) continue;
            var now = Split(Tuning.Capture(component));
            string type = component.GetType().FullName;
            var entry = tuning.Find(type);
            if (entry == null) { entry = new Tuning.Entry { type = type, json = "{}" }; tuning.entries.Add(entry); }
            var saved = Split(entry.json);
            var kept = new List<string>();
            foreach (string field in pair.Value)
            {
                int i = now.keys.IndexOf(field);
                if (i < 0) continue; // (not a plain value: a reference to an object can't be kept)
                Set(saved, field, now.values[i]);
                kept.Add(field);
            }
            if (kept.Count == 0) continue;
            entry.json = Join(saved);
            Debug.Log($"[Horror] Kept {component.GetType().Name}: {string.Join(", ", kept)} (every {component.GetType().Name} starts with this from now on)");
        }
        pending.Clear();
        EditorUtility.SetDirty(tuning);
    }

    /// <summary>"eyeColor.r" or "list.Array.data[2]" is the field "eyeColor" or "list".</summary>
    static string TopField(string propertyPath)
    {
        if (string.IsNullOrEmpty(propertyPath)) return null;
        int dot = propertyPath.IndexOf('.');
        return dot < 0 ? propertyPath : propertyPath.Substring(0, dot);
    }

    // ---------------------------------------------------------------- A JSON object's top-level fields

    class Fields
    {
        public readonly List<string> keys = new List<string>();
        public readonly List<string> values = new List<string>(); // raw JSON
    }

    static void Set(Fields f, string key, string value)
    {
        int i = f.keys.IndexOf(key);
        if (i >= 0) f.values[i] = value;
        else { f.keys.Add(key); f.values.Add(value); }
    }

    static string Join(Fields f)
    {
        var sb = new StringBuilder("{");
        for (int i = 0; i < f.keys.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('"').Append(f.keys[i]).Append("\":").Append(f.values[i]);
        }
        return sb.Append('}').ToString();
    }

    /// <summary>Splits {"a":1,"b":{"x":2},"c":[3,4]} into its top-level keys and their raw values.</summary>
    static Fields Split(string json)
    {
        var f = new Fields();
        if (string.IsNullOrEmpty(json)) return f;
        int i = json.IndexOf('{');
        if (i < 0) return f;
        i++;
        while (i < json.Length)
        {
            while (i < json.Length && (char.IsWhiteSpace(json[i]) || json[i] == ',')) i++;
            if (i >= json.Length || json[i] == '}') break;
            if (json[i] != '"') break; // (not what JsonUtility writes)
            int keyEnd = SkipString(json, i);
            string key = json.Substring(i + 1, keyEnd - i - 2);
            i = json.IndexOf(':', keyEnd) + 1;
            while (i < json.Length && char.IsWhiteSpace(json[i])) i++;
            int start = i, depth = 0;
            while (i < json.Length)
            {
                char c = json[i];
                if (c == '"') { i = SkipString(json, i); continue; }
                if (c == '{' || c == '[') depth++;
                else if (c == '}' || c == ']')
                {
                    if (depth == 0) break;
                    depth--;
                }
                else if (c == ',' && depth == 0) break;
                i++;
            }
            f.keys.Add(key);
            f.values.Add(json.Substring(start, i - start).Trim());
        }
        return f;
    }

    /// <summary>From the opening quote of a JSON string, the index just past its closing quote.</summary>
    static int SkipString(string json, int i)
    {
        i++;
        while (i < json.Length && json[i] != '"') i += json[i] == '\\' ? 2 : 1;
        return i + 1;
    }
}
