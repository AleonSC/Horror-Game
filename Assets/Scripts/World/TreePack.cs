using UnityEngine;

/// <summary>
/// The trees to use from a tree pack: an asset at Resources/TreePack (Horror > Tree Pack > Open Settings
/// makes it). Drag the pack's tree prefabs into Prefabs and DarkForestGenerator grows them, each set up
/// like the game's own trees (TreePack trees sway, fall, hide scours and stay out of buildings).
/// </summary>
[CreateAssetMenu(fileName = "TreePack", menuName = "Horror/Tree Pack")]
public class TreePack : ScriptableObject
{
    [Tooltip("The pack's tree prefabs. Their materials must be HDRP ones (pink means they aren't: convert them).")]
    public GameObject[] prefabs;
    [Range(0f, 1f), Tooltip("Share of the living trees taken from the pack (the rest are the game's own pines). 1: all of them.")]
    public float share = 1f;
    [Tooltip("Dead trees stay the game's own (the generator's Dead Tree Ratio of them).")]
    public bool keepProceduralDeadTrees = true;
    [Tooltip("Random size of a pack tree, as a multiple of the prefab's own.")]
    public Vector2 scale = new Vector2(0.8f, 1.3f);
    [Tooltip("Trunk thickness for a pack tree's collider (radius in metres, at the prefab's own size).")]
    public float trunkRadius = 0.3f;
    [Tooltip("Keep the prefab's own colliders (often its whole crown, which you'd walk into). Off: just the trunk collider.")]
    public bool keepPrefabColliders = false;

    public bool HasTrees
    {
        get
        {
            if (prefabs == null) return false;
            foreach (var p in prefabs) if (p != null) return true;
            return false;
        }
    }

    /// <summary>The pack in Resources (null if there isn't one).</summary>
    public static TreePack Load() => Resources.Load<TreePack>("TreePack");
}
