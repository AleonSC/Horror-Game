using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Makes the map loop: walk off one edge and you come out of the opposite edge, seamlessly.
///
/// How it works: the forest is one square tile whose terrain repeats at its edges. Every registered
/// object is drawn at whichever copy of its position is closest to the player, so the far side of the
/// map always appears just across the edge you're walking toward. When the player crosses an edge they
/// are moved to the opposite side, and everything around them moves with them, so nothing visibly
/// changes. Objects only jump when they're half a map away from the player, deep in the fog.
///
/// DarkForestGenerator sets this up when "Wrap World" is on.
/// </summary>
public class WorldWrap : MonoBehaviour
{
    static WorldWrap instance;

    /// <summary>True when the map loops.</summary>
    public static bool IsActive => instance != null && instance.isActiveAndEnabled;

    [Tooltip("Width/length of the looping square, usually the generator's area size.")]
    public float size = 320f;
    [Tooltip("Centre of the looping square.")]
    public Vector3 center;

    struct StaticItem
    {
        public Transform transform;
        public Vector3 canonical; // position inside the main square
    }

    readonly List<StaticItem> staticItems = new List<StaticItem>();
    readonly List<Transform> dynamicItems = new List<Transform>();
    FirstPersonController player;

    void Awake()
    {
        instance = this;
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Start()
    {
        // In case the forest was generated in edit mode and not regenerated on Play.
        if (staticItems.Count == 0)
        {
            var generator = FindAnyObjectByType<DarkForestGenerator>();
            if (generator != null) CollectFromGenerator(generator);
        }
    }

    /// <summary>
    /// Registers something that never moves on its own (a tree, rock, lamp...). It's redrawn at the copy of
    /// its position nearest the player.
    /// </summary>
    public static void RegisterStatic(Transform t)
    {
        if (instance == null || t == null) return;
        instance.staticItems.Add(new StaticItem { transform = t, canonical = Fold(t.position) });
    }

    /// <summary>
    /// Registers something that moves by itself (creatures, the entity). It's kept at the copy of its current
    /// position nearest the player, so it follows the player across the edges.
    /// </summary>
    public static void RegisterDynamic(Transform t)
    {
        if (instance == null || t == null) return;
        instance.dynamicItems.Add(t);
    }

    /// <summary>Registers the generator's trees, rocks, logs, grass, shacks, watchtower and campsites.</summary>
    public void CollectFromGenerator(DarkForestGenerator generator)
    {
        Transform root = generator.transform.Find(DarkForestGenerator.RootName);
        if (root == null) return;
        foreach (string group in new[] { "Trees", "Rocks", "Fallen Logs", "Grass", "Shacks", "Watchtower", "Campsites" })
        {
            Transform parent = root.Find(group);
            if (parent == null) continue;
            foreach (Transform child in parent) RegisterStatic(child);
        }
    }

    /// <summary>The copy of point p closest to reference (only x and z wrap).</summary>
    public static Vector3 NearestImage(Vector3 p, Vector3 reference)
    {
        if (instance == null) return p;
        float s = instance.size;
        p.x += s * Mathf.Round((reference.x - p.x) / s);
        p.z += s * Mathf.Round((reference.z - p.z) / s);
        return p;
    }

    /// <summary>Folds a point back into the main square.</summary>
    public static Vector3 Fold(Vector3 p)
    {
        if (instance == null) return p;
        return NearestImage(p, instance.center);
    }

    void LateUpdate()
    {
        if (player == null) player = FindAnyObjectByType<FirstPersonController>();
        if (player == null) return;

        // Crossed an edge: move the player to the matching spot on the other side.
        Vector3 pos = player.transform.position;
        Vector3 folded = Fold(pos);
        if ((folded - pos).sqrMagnitude > 0.0001f)
        {
            var cc = player.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false; // CharacterController overrides teleports while enabled
            player.transform.position = folded;
            if (cc != null) cc.enabled = true;
            pos = folded;
        }

        for (int i = staticItems.Count - 1; i >= 0; i--)
        {
            var item = staticItems[i];
            if (item.transform == null)
            {
                staticItems.RemoveAt(i);
                continue;
            }
            Vector3 image = NearestImage(item.canonical, pos);
            if ((item.transform.position - image).sqrMagnitude > 0.01f)
                item.transform.position = image;
        }

        for (int i = dynamicItems.Count - 1; i >= 0; i--)
        {
            Transform t = dynamicItems[i];
            if (t == null)
            {
                dynamicItems.RemoveAt(i);
                continue;
            }
            Vector3 image = NearestImage(t.position, pos);
            if ((t.position - image).sqrMagnitude > 0.01f)
                t.position = image;
        }
    }
}
