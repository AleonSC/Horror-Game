using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Horror > Tree Pack > ...: gets a tree pack ready for the game.
///   - Open Settings: the TreePack asset (Resources/TreePack), made if there isn't one: drag the prefabs in.
///   - Allow Felling: ticks Read/Write on the models behind the pack's tree prefabs, so the
///     game can cut the trees up when they fall (TreeFall). Without it pack trees just never fall.
///   - Check: lists each prefab with what the game will make of it (materials that aren't HDRP, which show
///     pink; missing Read/Write; size; levels of detail).
/// </summary>
public static class TreePackSetup
{
    const string AssetPath = "Assets/Scripts/Resources/TreePack.asset";

    [MenuItem("Horror/Tree Pack/Open Settings")]
    static void OpenSettings()
    {
        var pack = AssetDatabase.LoadAssetAtPath<TreePack>(AssetPath);
        if (pack == null)
        {
            pack = ScriptableObject.CreateInstance<TreePack>();
            AssetDatabase.CreateAsset(pack, AssetPath);
            AssetDatabase.SaveAssets();
        }
        Selection.activeObject = pack;
        EditorGUIUtility.PingObject(pack);
    }

    static GameObject[] Prefabs()
    {
        var pack = AssetDatabase.LoadAssetAtPath<TreePack>(AssetPath);
        if (pack == null || !pack.HasTrees)
        {
            EditorUtility.DisplayDialog("Tree Pack", "No trees in the tree pack yet. Use Horror > Tree Pack > Open Settings and drag the pack's tree prefabs into Prefabs first.", "OK");
            return null;
        }
        return pack.prefabs;
    }

    [MenuItem("Horror/Tree Pack/Allow Felling (Read-Write meshes)")]
    static void AllowFelling()
    {
        var prefabs = Prefabs();
        if (prefabs == null) return;
        var paths = new HashSet<string>();
        foreach (var prefab in prefabs)
        {
            if (prefab == null) continue;
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                if (filter.sharedMesh == null || filter.sharedMesh.isReadable) continue;
                string path = AssetDatabase.GetAssetPath(filter.sharedMesh);
                if (!string.IsNullOrEmpty(path)) paths.Add(path);
            }
        }
        int changed = 0;
        foreach (var path in paths)
        {
            if (AssetImporter.GetAtPath(path) is ModelImporter importer && !importer.isReadable)
            {
                importer.isReadable = true;
                importer.SaveAndReimport();
                changed++;
            }
        }
        string note = paths.Count > changed ? $"\n\n{paths.Count - changed} mesh file(s) aren't models (e.g. .asset or SpeedTree files) and couldn't be changed here: tick Read/Write on them by hand if they have the option." : "";
        EditorUtility.DisplayDialog("Tree Pack", changed > 0 ? $"Read/Write ticked on {changed} model(s). Pack trees can now be felled." + note
            : "Every mesh was already readable." + note, "OK");
    }

    [MenuItem("Horror/Tree Pack/Check")]
    static void Check()
    {
        var prefabs = Prefabs();
        if (prefabs == null) return;
        var report = new System.Text.StringBuilder("Tree pack check:\n");
        foreach (var prefab in prefabs)
        {
            if (prefab == null) { report.AppendLine("- (empty slot)"); continue; }
            var problems = new List<string>();
            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            var bounds = new Bounds();
            bool first = true;
            foreach (var r in renderers)
            {
                if (first) { bounds = r.bounds; first = false; } else bounds.Encapsulate(r.bounds);
                foreach (var m in r.sharedMaterials)
                    if (m != null && m.shader != null && !m.shader.name.StartsWith("HDRP/") && !m.shader.name.Contains("HDRP") && !m.shader.name.Contains("Nature/SpeedTree"))
                        problems.Add($"material '{m.name}' uses '{m.shader.name}' (not HDRP: it'll be pink; convert it)");
            }
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
                if (filter.sharedMesh != null && !filter.sharedMesh.isReadable) { problems.Add("meshes without Read/Write (it won't fall: use Allow Felling)"); break; }
            int lods = prefab.GetComponentInChildren<LODGroup>(true) is LODGroup g ? g.lodCount : 0;
            int colliders = prefab.GetComponentsInChildren<Collider>(true).Length;
            report.AppendLine($"- {prefab.name}: {bounds.size.y:0.0} m tall, {renderers.Length} renderer(s), {(lods > 0 ? lods + " LODs" : "no LODs")}, {colliders} collider(s) (replaced by a trunk collider)");
            foreach (var p in new HashSet<string>(problems)) report.AppendLine("    ! " + p);
        }
        Debug.Log(report.ToString());
        EditorUtility.DisplayDialog("Tree Pack", "Checked " + prefabs.Length + " prefab(s). The details are in the Console.", "OK");
    }
}
