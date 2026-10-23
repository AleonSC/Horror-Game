using UnityEditor;
using UnityEngine;

/// <summary>
/// Horror > Player Character > ...: the player's own model and animations (PlayerCharacter).
///   - Open Settings: the PlayerCharacter asset (Resources/PlayerCharacter), made if there isn't one: drag the
///     model into Model and the animation clips into their slots.
///   - Check: what the game will make of it (legacy clips, which can't be played; a humanoid model with no
///     avatar; missing Idle/Walk).
/// </summary>
public static class PlayerCharacterSetup
{
    const string AssetPath = "Assets/Scripts/Resources/PlayerCharacter.asset";

    [MenuItem("Horror/Player Character/Open Settings")]
    static void OpenSettings()
    {
        var character = AssetDatabase.LoadAssetAtPath<PlayerCharacter>(AssetPath);
        if (character == null)
        {
            character = ScriptableObject.CreateInstance<PlayerCharacter>();
            AssetDatabase.CreateAsset(character, AssetPath);
            AssetDatabase.SaveAssets();
        }
        Selection.activeObject = character;
        EditorGUIUtility.PingObject(character);
    }

    [MenuItem("Horror/Player Character/Check")]
    static void Check()
    {
        var c = AssetDatabase.LoadAssetAtPath<PlayerCharacter>(AssetPath);
        if (c == null || c.model == null)
        {
            EditorUtility.DisplayDialog("Player Character", "No model set yet. Use Horror > Player Character > Open Settings and drag the model into Model.", "OK");
            return;
        }
        var report = new System.Text.StringBuilder($"Player character '{c.model.name}':\n");
        var animator = c.model.GetComponentInChildren<Animator>(true);
        if (animator == null) report.AppendLine("  - no Animator on the model (one is added; fine for Generic clips)");
        else if (animator.avatar == null) report.AppendLine("  ! the model has no Avatar: set its Rig to Humanoid (or Generic) and Apply, so the clips can drive it");
        else report.AppendLine($"  - rig: {(animator.avatar.isHuman ? "Humanoid (the remote goes in its right hand)" : "Generic")}");
        if (c.idle == null && c.walk == null) report.AppendLine("  ! no Idle or Walk clip: it will just stand in its pose");
        foreach (var (name, clip) in new (string, AnimationClip)[] { ("Idle", c.idle), ("Walk", c.walk), ("Run", c.run), ("Crouch Idle", c.crouchIdle),
                     ("Crouch Walk", c.crouchWalk), ("Jump", c.jump), ("Winded", c.winded), ("Drone Control", c.droneControl), ("Seated", c.seated) })
        {
            if (clip == null) { report.AppendLine($"  - {name}: empty (falls back to a near one)"); continue; }
            string note = clip.legacy ? "  ! LEGACY: can't be played. Set the clip's model to Rig > Humanoid or Generic, not Legacy" : "";
            report.AppendLine($"  - {name}: {clip.name} ({clip.length:0.00}s){note}");
        }
        Debug.Log(report.ToString());
        EditorUtility.DisplayDialog("Player Character", "Checked. The details are in the Console.", "OK");
    }
}
