using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Makes builds work. The game builds every material in code, from shaders found by name
/// (MaterialFactory). Unity leaves any shader out of a build unless some asset in the build uses it,
/// so in a build those lookups failed, generation broke part way, and you got an empty scene.
///
/// This keeps a small set of reference materials in Resources/HorrorShaderRefs, one per shader and
/// keyword combination the game uses (HDRP lit, lit with a normal map, double-sided lit, see-through lit,
/// unlit), so the shaders and those variants are always built in. It also keeps fog in the build (the fog is only switched on
/// at runtime, so Unity would otherwise strip it), and makes sure the open scene is in the build list.
/// Runs whenever scripts reload and again just before every build; also Horror > Prepare For Build.
/// </summary>
[InitializeOnLoad]
public class HorrorBuildSetup : IPreprocessBuildWithReport
{
    const string Folder = "Assets/Scripts/Resources/HorrorShaderRefs";

    static HorrorBuildSetup()
    {
        EditorApplication.delayCall += () => EnsureShaderReferences(false);
    }

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        EnsureShaderReferences(true);
        KeepFogInBuilds();
        EnsureSceneInBuild();
    }

    [MenuItem("Horror/Prepare For Build")]
    static void PrepareForBuild()
    {
        EnsureShaderReferences(true);
        KeepFogInBuilds();
        EnsureSceneInBuild();
        Debug.Log("Horror: ready to build (shaders referenced, fog kept, scene in the build list).");
    }

    // ---------------------------------------------------------------- Shaders

    static void EnsureShaderReferences(bool log)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        Shader lit = Shader.Find("HDRP/Lit");
        Shader unlit = Shader.Find("HDRP/Unlit");

        if (!AssetDatabase.IsValidFolder(Folder))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Scripts/Resources")) AssetDatabase.CreateFolder("Assets/Scripts", "Resources");
            AssetDatabase.CreateFolder("Assets/Scripts/Resources", "HorrorShaderRefs");
        }

        bool changed = false;
        changed |= Ensure("Lit", lit, m => { m.enableInstancing = true; });
        changed |= Ensure("LitNormalMap", lit, m =>
        {
            m.enableInstancing = true;
            m.EnableKeyword("_NORMALMAP");
            m.EnableKeyword("_NORMALMAP_TANGENT_SPACE");
        });
        changed |= Ensure("LitDoubleSided", lit, m =>
        {
            m.enableInstancing = true;
            m.SetFloat("_DoubleSidedEnable", 1f);
        });
        changed |= Ensure("LitCutout", lit, m => // (tree pack leaves: TreePack.ToHdrp)
        {
            m.enableInstancing = true;
            m.SetFloat("_DoubleSidedEnable", 1f);
            m.SetFloat("_AlphaCutoffEnable", 1f);
            m.renderQueue = (int)RenderQueue.AlphaTest;
        });
        changed |= Ensure("LitTransparent", lit, m =>
        {
            m.SetFloat("_SurfaceType", 1f);
            m.SetFloat("_BlendMode", 0f);
            m.renderQueue = (int)RenderQueue.Transparent - 50;
        });
        changed |= Ensure("Unlit", unlit, m => { });
        changed |= Ensure("Decal", Shader.Find("HDRP/Decal"), m => { });

        if (changed)
        {
            AssetDatabase.SaveAssets();
            if (log) Debug.Log("Horror: updated the shader reference materials in " + Folder);
        }
    }

    /// <summary>Creates (or re-points) one reference material. True if anything changed.</summary>
    static bool Ensure(string name, Shader shader, System.Action<Material> setup)
    {
        if (shader == null) return false;
        string path = Folder + "/" + name + ".mat";
        var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null && existing.shader == shader) return false;

        if (existing != null)
        {
            existing.shader = shader;
            setup(existing);
            UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(existing);
            EditorUtility.SetDirty(existing);
            return true;
        }
        var mat = new Material(shader) { name = name };
        setup(mat);
        UnityEngine.Rendering.HighDefinition.HDMaterial.ValidateMaterial(mat);
        AssetDatabase.CreateAsset(mat, path);
        return true;
    }

    // ---------------------------------------------------------------- Fog

    /// <summary>Fog is switched on at runtime, so keep every fog mode's shader variants in the build.</summary>
    static void KeepFogInBuilds()
    {
        var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset");
        if (assets == null || assets.Length == 0) return;
        var settings = new SerializedObject(assets[0]);
        bool changed = false;
        changed |= SetInt(settings, "m_FogStripping", 1); // custom
        changed |= SetBool(settings, "m_FogKeepLinear", true);
        changed |= SetBool(settings, "m_FogKeepExp", true);
        changed |= SetBool(settings, "m_FogKeepExp2", true);
        if (changed)
        {
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("Horror: set Graphics > Shader Stripping > Fog Modes to keep all fog modes.");
        }
    }

    static bool SetInt(SerializedObject so, string prop, int value)
    {
        var p = so.FindProperty(prop);
        if (p == null || p.intValue == value) return false;
        p.intValue = value;
        return true;
    }

    static bool SetBool(SerializedObject so, string prop, bool value)
    {
        var p = so.FindProperty(prop);
        if (p == null || p.boolValue == value) return false;
        p.boolValue = value;
        return true;
    }

    // ---------------------------------------------------------------- Scenes

    /// <summary>If no scene is ticked in Build Settings, adds the open one (the game builds itself in it).</summary>
    static void EnsureSceneInBuild()
    {
        foreach (var s in EditorBuildSettings.scenes)
            if (s.enabled) return;

        var active = EditorSceneManager.GetActiveScene();
        if (string.IsNullOrEmpty(active.path))
        {
            Debug.LogWarning("Horror: no scenes are in File > Build Settings and the open scene isn't saved. Save it and add it.");
            return;
        }
        var list = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes) { new EditorBuildSettingsScene(active.path, true) };
        EditorBuildSettings.scenes = list.ToArray();
        Debug.Log("Horror: added " + active.path + " to the build's scene list.");
    }
}
