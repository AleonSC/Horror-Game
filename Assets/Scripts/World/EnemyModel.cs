using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// An enemy's look, as simple shapes (spheres, capsules, boxes, cylinders) placed on a few joints: the body
/// (the ground under its feet), the head, each arm and each hip. Made and saved with the in-game model
/// editor (ModelEditor, F10), as a JSON file per enemy that can be shared and read back.
///
/// Numbers are in metres for an enemy of its usual height (height): each part's position and rotation are
/// relative to the joint it's on (+z is forward, +y up), and the joints relative to the ground under it.
/// The scour builds itself from its model when one is saved with useInGame on (its legs, mouth, eye glow
/// and twitching are still its own, placed at the model's joints); the others' models are designs, for
/// now, to be made into the real thing.
///
/// Saved to Assets/Scripts/Resources/EnemyModels/[name].json from the editor (so it's in the project and
/// the build), or to the game's save folder in a build.
/// </summary>
[System.Serializable]
public class EnemyModel
{
    public string enemy = "Scour";
    [Tooltip("The height these numbers are for, in metres.")]
    public float height = 2.4f;
    [Tooltip("Use this model in the game (where the enemy supports it: the scour, for now).")]
    public bool useInGame = true;
    public string notes = "";

    [Header("Joints (from the ground under it)")]
    public Vector3 head = new Vector3(0f, 2.12f, 0.24f);
    public Vector3 armLeft = new Vector3(-0.26f, 1.9f, 0.02f), armRight = new Vector3(0.26f, 1.9f, 0.02f);
    public Vector3 hipLeft = new Vector3(-0.11f, 1.12f, -0.02f), hipRight = new Vector3(0.11f, 1.12f, -0.02f);
    [Tooltip("Where its mouth opens, from the head joint.")]
    public Vector3 mouth = new Vector3(0f, -0.005f, 0.11f);

    [Header("Legs (dragged, from the hips)")]
    public bool legs = true;
    public float thigh = 0.68f, shin = 0.7f, legThickness = 0.06f;

    public List<ModelPart> parts = new List<ModelPart>();

    /// <summary>The enemies the editor knows, with the height each is usually.</summary>
    public static readonly string[] Enemies = { "Scour", "Blind One", "Crawler", "Flier", "Giant" };
    public static readonly string[] Shapes = { "Sphere", "Capsule", "Cube", "Cylinder" };
    public static readonly string[] Joints = { "Body", "Head", "ArmL", "ArmR" };

    public EnemyModel Clone() => JsonUtility.FromJson<EnemyModel>(JsonUtility.ToJson(this));

    // ---------------------------------------------------------------- Saving and loading

    static readonly Dictionary<string, EnemyModel> cache = new Dictionary<string, EnemyModel>();
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() => cache.Clear();

    static string FileName(string enemy) => enemy.Replace(" ", "") + ".json";
    static string ProjectFolder => Path.Combine(Application.dataPath, "Scripts/Resources/EnemyModels");
    static string SaveFolder => Path.Combine(Application.persistentDataPath, "EnemyModels");

    /// <summary>Where Save writes (in the project in the editor, the game's save folder in a build).</summary>
    public static string SavePath(string enemy) => Path.Combine(Application.isEditor ? ProjectFolder : SaveFolder, FileName(enemy));

    /// <summary>The saved model for this enemy, or null if there isn't one.</summary>
    public static EnemyModel Load(string enemy)
    {
        if (cache.TryGetValue(enemy, out var cached)) return cached?.Clone();
        EnemyModel model = null;
        try
        {
            string path = SavePath(enemy);
            string json = File.Exists(path) ? File.ReadAllText(path) : null;
            if (json == null)
            {
                var asset = Resources.Load<TextAsset>("EnemyModels/" + Path.GetFileNameWithoutExtension(FileName(enemy)));
                if (asset != null) json = asset.text;
            }
            if (!string.IsNullOrEmpty(json)) model = JsonUtility.FromJson<EnemyModel>(json);
        }
        catch (System.Exception e) { Debug.LogWarning($"[Horror] Couldn't read the {enemy} model: {e.Message}"); }
        cache[enemy] = model;
        return model?.Clone();
    }

    /// <summary>The model the game should build this enemy from: saved, with useInGame on; otherwise null.</summary>
    public static EnemyModel ForGame(string enemy)
    {
        var model = Load(enemy);
        return model != null && model.useInGame && model.parts.Count > 0 ? model : null;
    }

    /// <summary>Saves it (and returns where), so it's kept, used in the game, and can be shared.</summary>
    public string Save()
    {
        string path = SavePath(enemy);
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        File.WriteAllText(path, JsonUtility.ToJson(this, true));
        cache[enemy] = Clone();
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
        return path;
    }

    /// <summary>Forgets the saved model (the enemy goes back to its own look).</summary>
    public static void Delete(string enemy)
    {
        string path = SavePath(enemy);
        if (File.Exists(path)) File.Delete(path);
        if (File.Exists(path + ".meta")) File.Delete(path + ".meta");
        cache[enemy] = null;
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif
    }

    // ---------------------------------------------------------------- Building it

    /// <summary>What Build made: the joints, the parts (in model order) and the eyes.</summary>
    public class Built
    {
        public Transform root, head, armLeft, armRight, hipLeft, hipRight;
        public readonly List<GameObject> parts = new List<GameObject>();
        public readonly List<Transform> eyes = new List<Transform>();
    }

    /// <summary>
    /// Builds the model under root, scaled by scale (1 = its own height). material gives each part its
    /// material (and the legs theirs, asked for with null). With previewLegs the legs are drawn hanging from the hips (the game drags its own).
    /// Colliders are kept only with keepColliders (the editor uses them to pick parts).
    /// </summary>
    public Built Build(Transform root, float scale, System.Func<ModelPart, Material> material, bool previewLegs, bool keepColliders)
    {
        var b = new Built { root = root };
        b.head = Joint(root, "Head", head * scale);
        b.armLeft = Joint(root, "Arm L", armLeft * scale);
        b.armRight = Joint(root, "Arm R", armRight * scale);
        b.hipLeft = Joint(root, "Hip L", hipLeft * scale);
        b.hipRight = Joint(root, "Hip R", hipRight * scale);

        foreach (var part in parts)
        {
            var go = GameObject.CreatePrimitive(ShapeOf(part.shape));
            go.name = string.IsNullOrEmpty(part.name) ? part.shape : part.name;
            go.transform.SetParent(JointOf(b, part.attach), false);
            go.transform.localPosition = part.position * scale;
            go.transform.localRotation = Quaternion.Euler(part.rotation);
            go.transform.localScale = part.scale * scale;
            go.GetComponent<Renderer>().sharedMaterial = material(part);
            if (!keepColliders) DestroyCollider(go);
            b.parts.Add(go);
            if (part.eye) b.eyes.Add(go.transform);
        }

        if (previewLegs && legs)
        {
            var legMat = material(null);
            foreach (var hip in new[] { b.hipLeft, b.hipRight })
            {
                // Standing: the knee jutting a little forward, the foot under the hip.
                float h = Mathf.Max(hip.localPosition.y, 0.05f * scale);
                float a = thigh * scale, c = shin * scale;
                float along = (a * a - c * c + h * h) / (2f * h);
                float forward = Mathf.Sqrt(Mathf.Max(a * a - along * along, 0f));
                Vector3 knee = new Vector3(0f, -along, forward), foot = new Vector3(0f, -h, 0f);
                Limb(hip, legMat, Vector3.zero, knee, legThickness * scale);
                Limb(hip, legMat, knee, foot, legThickness * 0.7f * scale);
            }
        }
        return b;
    }

    static Transform JointOf(Built b, string attach)
    {
        switch (attach)
        {
            case "Head": return b.head;
            case "ArmL": return b.armLeft;
            case "ArmR": return b.armRight;
            default: return b.root;
        }
    }

    static Transform Joint(Transform parent, string name, Vector3 at)
    {
        var t = new GameObject(name).transform;
        t.SetParent(parent, false);
        t.localPosition = at;
        return t;
    }

    static void Limb(Transform parent, Material mat, Vector3 from, Vector3 to, float thickness)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.name = "Leg";
        DestroyCollider(go);
        go.transform.SetParent(parent, false);
        Vector3 d = to - from;
        go.transform.localPosition = (from + to) * 0.5f;
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d);
        go.transform.localScale = new Vector3(thickness, d.magnitude * 0.5f, thickness);
        go.GetComponent<Renderer>().sharedMaterial = mat;
    }

    public static PrimitiveType ShapeOf(string shape)
    {
        switch (shape)
        {
            case "Capsule": return PrimitiveType.Capsule;
            case "Cube": return PrimitiveType.Cube;
            case "Cylinder": return PrimitiveType.Cylinder;
            default: return PrimitiveType.Sphere;
        }
    }

    static void DestroyCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col == null) return;
        if (Application.isPlaying) Object.Destroy(col);
        else Object.DestroyImmediate(col);
    }

    // ---------------------------------------------------------------- Starting points

    /// <summary>A simple starting model for this enemy, roughly its shape, to edit from.</summary>
    public static EnemyModel Template(string enemy)
    {
        switch (enemy)
        {
            case "Blind One": return BlindOne();
            case "Crawler": return Crawler();
            case "Flier": return Flier();
            case "Giant": return Giant();
            default: return Scour();
        }
    }

    static ModelPart P(string name, string shape, string attach, Vector3 pos, Vector3 rot, Vector3 size, Color color, float glow = 0f, bool eye = false) =>
        new ModelPart { name = name, shape = shape, attach = attach, position = pos, rotation = rot, scale = size, color = color, glow = glow, eye = eye };

    /// <summary>A scour, simplified: a hunched, too-thin black figure, long arms, a small tall head, two red eyes.</summary>
    static EnemyModel Scour()
    {
        var black = Color.black;
        var red = new Color(1f, 0.08f, 0.05f);
        var m = new EnemyModel { enemy = "Scour", height = 2.4f };
        m.parts.Add(P("Pelvis", "Sphere", "Body", new Vector3(0f, 1.12f, -0.02f), Vector3.zero, new Vector3(0.24f, 0.13f, 0.15f), black));
        m.parts.Add(P("Spine", "Capsule", "Body", new Vector3(0f, 1.42f, -0.08f), new Vector3(-10f, 0f, 0f), new Vector3(0.1f, 0.3f, 0.1f), black));
        m.parts.Add(P("Chest", "Capsule", "Body", new Vector3(0f, 1.66f, -0.02f), new Vector3(18f, 0f, 0f), new Vector3(0.24f, 0.22f, 0.13f), black));
        m.parts.Add(P("Shoulders", "Capsule", "Body", new Vector3(0f, 1.9f, 0.02f), new Vector3(0f, 0f, 90f), new Vector3(0.07f, 0.28f, 0.07f), black));
        m.parts.Add(P("Neck", "Cylinder", "Body", new Vector3(0f, 2.03f, 0.18f), new Vector3(35f, 0f, 0f), new Vector3(0.05f, 0.12f, 0.05f), black));
        m.parts.Add(P("Skull", "Sphere", "Head", new Vector3(0f, 0.1f, 0.02f), Vector3.zero, new Vector3(0.2f, 0.3f, 0.22f), black));
        m.parts.Add(P("Jaw", "Sphere", "Head", new Vector3(0f, -0.05f, 0.07f), new Vector3(20f, 0f, 0f), new Vector3(0.12f, 0.1f, 0.16f), black));
        m.parts.Add(P("Eye L", "Sphere", "Head", new Vector3(-0.05f, 0.12f, 0.125f), Vector3.zero, new Vector3(0.035f, 0.025f, 0.02f), red, 6f, true));
        m.parts.Add(P("Eye R", "Sphere", "Head", new Vector3(0.05f, 0.12f, 0.125f), Vector3.zero, new Vector3(0.035f, 0.025f, 0.02f), red, 6f, true));
        foreach (var (attach, side) in new[] { ("ArmL", -1f), ("ArmR", 1f) })
        {
            m.parts.Add(P("Upper arm", "Cylinder", attach, new Vector3(side * 0.06f, -0.3f, -0.08f), new Vector3(15f, 0f, side * -10f), new Vector3(0.045f, 0.3f, 0.045f), black));
            m.parts.Add(P("Forearm", "Cylinder", attach, new Vector3(side * 0.09f, -0.91f, -0.03f), new Vector3(-22f, 0f, side * 5f), new Vector3(0.035f, 0.32f, 0.035f), black));
            m.parts.Add(P("Hand", "Capsule", attach, new Vector3(side * 0.06f, -1.38f, 0.14f), new Vector3(-20f, 0f, 0f), new Vector3(0.06f, 0.16f, 0.03f), black));
        }
        return m;
    }

    /// <summary>The blind one, simplified: huge, pale, eyeless, a long gaping head.</summary>
    static EnemyModel BlindOne()
    {
        var skin = new Color(0.55f, 0.5f, 0.45f);
        var m = new EnemyModel
        {
            enemy = "Blind One", height = 6.5f,
            head = new Vector3(0f, 5.9f, 0.5f), armLeft = new Vector3(-0.8f, 5.1f, 0.1f), armRight = new Vector3(0.8f, 5.1f, 0.1f),
            hipLeft = new Vector3(-0.35f, 3.1f, 0f), hipRight = new Vector3(0.35f, 3.1f, 0f), mouth = new Vector3(0f, -0.3f, 0.45f),
            thigh = 1.7f, shin = 1.7f, legThickness = 0.3f,
        };
        m.parts.Add(P("Torso", "Capsule", "Body", new Vector3(0f, 4.2f, 0f), new Vector3(10f, 0f, 0f), new Vector3(1.1f, 1.3f, 0.7f), skin));
        m.parts.Add(P("Head", "Capsule", "Head", new Vector3(0f, 0.2f, 0.2f), new Vector3(60f, 0f, 0f), new Vector3(0.6f, 0.55f, 0.55f), skin));
        m.parts.Add(P("Mouth", "Sphere", "Head", new Vector3(0f, -0.3f, 0.45f), Vector3.zero, new Vector3(0.35f, 0.25f, 0.2f), new Color(0.05f, 0f, 0f)));
        foreach (var (attach, side) in new[] { ("ArmL", -1f), ("ArmR", 1f) })
            m.parts.Add(P("Arm", "Capsule", attach, new Vector3(side * 0.2f, -1.4f, 0.2f), new Vector3(10f, 0f, side * -8f), new Vector3(0.28f, 1.5f, 0.28f), skin));
        return m;
    }

    /// <summary>The crawler, simplified: a head on four long neck-arms.</summary>
    static EnemyModel Crawler()
    {
        var skin = new Color(0.32f, 0.28f, 0.26f);
        var m = new EnemyModel { enemy = "Crawler", height = 1f, legs = false, head = new Vector3(0f, 0.55f, 0f), mouth = new Vector3(0f, -0.05f, 0.18f) };
        m.parts.Add(P("Head", "Sphere", "Head", Vector3.zero, Vector3.zero, new Vector3(0.38f, 0.32f, 0.42f), skin));
        m.parts.Add(P("Eye L", "Sphere", "Head", new Vector3(-0.08f, 0.06f, 0.18f), Vector3.zero, new Vector3(0.05f, 0.05f, 0.03f), new Color(0.85f, 0.9f, 0.75f), 2.2f, true));
        m.parts.Add(P("Eye R", "Sphere", "Head", new Vector3(0.08f, 0.06f, 0.18f), Vector3.zero, new Vector3(0.05f, 0.05f, 0.03f), new Color(0.85f, 0.9f, 0.75f), 2.2f, true));
        for (int i = 0; i < 4; i++)
        {
            float yaw = 45f + i * 90f;
            Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * Vector3.forward;
            m.parts.Add(P("Limb", "Cylinder", "Body", new Vector3(dir.x * 0.35f, 0.3f, dir.z * 0.35f), new Vector3(0f, yaw, 0f) + new Vector3(50f, 0f, 0f), new Vector3(0.06f, 0.4f, 0.06f), skin));
        }
        return m;
    }

    /// <summary>The flier, simplified: a dark body with broad wings and a searchlight eye.</summary>
    static EnemyModel Flier()
    {
        var dark = new Color(0.02f, 0.02f, 0.025f);
        var m = new EnemyModel { enemy = "Flier", height = 2f, legs = false, head = new Vector3(0f, 1f, 0.6f) };
        m.parts.Add(P("Body", "Capsule", "Body", new Vector3(0f, 1f, 0f), new Vector3(90f, 0f, 0f), new Vector3(0.4f, 0.6f, 0.35f), dark));
        m.parts.Add(P("Wing L", "Cube", "Body", new Vector3(-1.1f, 1.05f, 0f), new Vector3(0f, 0f, 8f), new Vector3(1.8f, 0.03f, 0.8f), dark));
        m.parts.Add(P("Wing R", "Cube", "Body", new Vector3(1.1f, 1.05f, 0f), new Vector3(0f, 0f, -8f), new Vector3(1.8f, 0.03f, 0.8f), dark));
        m.parts.Add(P("Head", "Sphere", "Head", Vector3.zero, Vector3.zero, new Vector3(0.3f, 0.28f, 0.34f), dark));
        m.parts.Add(P("Eye", "Sphere", "Head", new Vector3(0f, 0f, 0.15f), Vector3.zero, new Vector3(0.1f, 0.1f, 0.05f), new Color(1f, 0.9f, 0.7f), 6f, true));
        return m;
    }

    /// <summary>The giant, simplified: a towering dark figure with burning eyes.</summary>
    static EnemyModel Giant()
    {
        var dark = new Color(0.01f, 0.005f, 0.005f);
        var m = new EnemyModel
        {
            enemy = "Giant", height = 40f,
            head = new Vector3(0f, 36f, 1.5f), armLeft = new Vector3(-5f, 31f, 0f), armRight = new Vector3(5f, 31f, 0f),
            hipLeft = new Vector3(-2f, 19f, 0f), hipRight = new Vector3(2f, 19f, 0f), mouth = new Vector3(0f, -1.5f, 2f),
            thigh = 10f, shin = 10f, legThickness = 2f,
        };
        m.parts.Add(P("Torso", "Capsule", "Body", new Vector3(0f, 26f, 0f), new Vector3(8f, 0f, 0f), new Vector3(8f, 8f, 5f), dark));
        m.parts.Add(P("Head", "Sphere", "Head", new Vector3(0f, 1f, 0f), Vector3.zero, new Vector3(4f, 5f, 4.5f), dark));
        m.parts.Add(P("Eye L", "Sphere", "Head", new Vector3(-1f, 1.6f, 2f), Vector3.zero, new Vector3(0.8f, 0.6f, 0.4f), new Color(1f, 0.05f, 0.01f), 4f, true));
        m.parts.Add(P("Eye R", "Sphere", "Head", new Vector3(1f, 1.6f, 2f), Vector3.zero, new Vector3(0.8f, 0.6f, 0.4f), new Color(1f, 0.05f, 0.01f), 4f, true));
        foreach (var (attach, side) in new[] { ("ArmL", -1f), ("ArmR", 1f) })
            m.parts.Add(P("Arm", "Capsule", attach, new Vector3(side * 0.8f, -9f, 0f), new Vector3(0f, 0f, side * -6f), new Vector3(2f, 9f, 2f), dark));
        return m;
    }
}

/// <summary>One shape in an EnemyModel (see there).</summary>
[System.Serializable]
public class ModelPart
{
    public string name = "Part";
    [Tooltip("Sphere, Capsule, Cube or Cylinder.")]
    public string shape = "Sphere";
    [Tooltip("What it's on: Body, Head, ArmL or ArmR. It moves with that.")]
    public string attach = "Body";
    [Tooltip("An eye: glows, and (on the scour) widens, flares and burns blue like its eyes do.")]
    public bool eye;
    public Vector3 position;
    public Vector3 rotation;
    public Vector3 scale = new Vector3(0.2f, 0.2f, 0.2f);
    public Color color = Color.black;
    [Tooltip("Above 0, it glows (unaffected by fog), this bright.")]
    public float glow;
}
