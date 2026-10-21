using UnityEditor;
using UnityEngine;

/// <summary>
/// Save Tuning / Clear Tuning on every component's right-click menu (see Tuning). Works in Play mode: the
/// values are written into Assets/Scripts/Resources/HorrorTuning.asset, which keeps them after Play stops.
/// </summary>
static class TuningMenu
{
    const string AssetPath = "Assets/Scripts/Resources/" + Tuning.ResourcePath + ".asset";

    static Tuning GetOrCreate()
    {
        var tuning = AssetDatabase.LoadAssetAtPath<Tuning>(AssetPath);
        if (tuning != null) return tuning;
        if (!AssetDatabase.IsValidFolder("Assets/Scripts/Resources")) AssetDatabase.CreateFolder("Assets/Scripts", "Resources");
        tuning = ScriptableObject.CreateInstance<Tuning>();
        AssetDatabase.CreateAsset(tuning, AssetPath);
        return tuning;
    }

    [MenuItem("CONTEXT/MonoBehaviour/Save Tuning (keeps these values)")]
    static void Save(MenuCommand command)
    {
        if (!(command.context is MonoBehaviour component)) return;
        var tuning = GetOrCreate();
        string type = component.GetType().FullName;
        var entry = tuning.Find(type);
        if (entry == null) { entry = new Tuning.Entry { type = type }; tuning.entries.Add(entry); }
        entry.json = Tuning.Capture(component);
        EditorUtility.SetDirty(tuning);
        AssetDatabase.SaveAssets();
        Debug.Log($"[Horror] Saved tuning for {component.GetType().Name}: every {component.GetType().Name} the game makes from now on starts with these values.");
    }

    [MenuItem("CONTEXT/MonoBehaviour/Clear Tuning (back to the code's values)")]
    static void Clear(MenuCommand command)
    {
        if (!(command.context is MonoBehaviour component)) return;
        var tuning = AssetDatabase.LoadAssetAtPath<Tuning>(AssetPath);
        if (tuning == null) return;
        if (tuning.entries.RemoveAll(e => e.type == component.GetType().FullName) > 0)
        {
            EditorUtility.SetDirty(tuning);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Horror] Cleared saved tuning for {component.GetType().Name}.");
        }
    }

    [MenuItem("Horror/Open Saved Tuning")]
    static void Open() => Selection.activeObject = GetOrCreate();
}
