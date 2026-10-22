using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Saved settings for the game's components, kept in an asset (Resources/HorrorTuning) so they survive
/// leaving Play mode, applied whenever the game makes one of those components.
///
/// Nearly everything in this game is added by code at runtime, so values tweaked in the Inspector during
/// Play would be lost when it stops, and its defaults live in code. So any value you change in the
/// Inspector during Play is kept (TuningAutoSave), and every one of the game's components puts its saved
/// values on as it wakes up (code that sets a value straight after still wins). Save Tuning on a
/// component's right-click menu keeps all its values at once; Clear Tuning forgets them again. Tuning is
/// per kind of component: a value kept from one lamp goes on every lamp. References to other objects (lights, clips, materials) aren't
/// saved, since they can't carry over from one session to another; only plain values (numbers, colours,
/// toggles, curves) are.
/// </summary>
public class Tuning : ScriptableObject
{
    [System.Serializable]
    public class Entry
    {
        public string type;
        [TextArea(3, 20)] public string json;
    }

    public List<Entry> entries = new List<Entry>();

    public const string ResourcePath = "HorrorTuning";
    static Tuning loaded;
    static bool looked;

    /// <summary>The project's saved tuning (null if there isn't any yet).</summary>
    public static Tuning Current
    {
        get
        {
            if (loaded == null && !looked)
            {
                looked = true;
                loaded = Resources.Load<Tuning>(ResourcePath);
            }
            return loaded;
        }
    }

    /// <summary>Forgets the cached asset (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics()
    {
        loaded = null;
        looked = false;
    }

    /// <summary>Puts any saved tuning for this kind of component onto it. Components call this as they wake.</summary>
    public static void Apply(MonoBehaviour component)
    {
        var tuning = Current;
        if (tuning == null || component == null) return;
        var entry = tuning.Find(component.GetType().FullName);
        if (entry == null || string.IsNullOrEmpty(entry.json)) return;
        try { JsonUtility.FromJsonOverwrite(entry.json, component); }
        catch (System.Exception e) { Debug.LogWarning($"[Horror] Couldn't apply saved tuning to {component.GetType().Name}: {e.Message}"); }
    }

    public Entry Find(string type) => entries.Find(e => e.type == type);

    /// <summary>
    /// A component's plain values as JSON, with references to other objects taken out (they're saved by an
    /// id that means nothing in another session).
    /// </summary>
    public static string Capture(MonoBehaviour component)
    {
        string json = JsonUtility.ToJson(component);
        // "field":{"instanceID":123}  and  "field":[{"instanceID":1},{"instanceID":2}]
        json = System.Text.RegularExpressions.Regex.Replace(json, "\"[^\"]+\":\\{\"instanceID\":-?\\d+\\},?", "");
        json = System.Text.RegularExpressions.Regex.Replace(json, "\"[^\"]+\":\\[(\\{\"instanceID\":-?\\d+\\},?)*\\],?", "");
        json = System.Text.RegularExpressions.Regex.Replace(json, ",\\s*\\}", "}");
        return json;
    }
}
