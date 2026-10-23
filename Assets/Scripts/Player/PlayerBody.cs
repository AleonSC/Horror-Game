using UnityEngine;

/// <summary>
/// The player's own body, simple (a hooded figure in a jacket and jeans): built from the "Player" model
/// (EnemyModel; the model editor's Player, or its simple starting model). You never see it through your
/// own eyes (it's on its own layer, which your camera leaves out), but every other camera does: from the
/// drone you can see yourself standing there, head bowed over the remote held out in front of you; and
/// through a rift or the dark room's door. It swings its arms and legs as you walk.
/// FirstPersonController adds this.
/// </summary>
public class PlayerBody : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Inspector changes made in Play are kept)

    /// <summary>The body's layer: left out of your own camera, seen by the others.</summary>
    public const int Layer = 29;

    [Tooltip("How far the arms and legs swing as you walk (degrees).")]
    public float walkSwing = 28f;
    [Tooltip("Strides per metre walked.")]
    public float strideRate = 0.9f;

    Transform root;
    EnemyModel.Built built;
    GameObject[] remote = new GameObject[0];
    Vector3 lastPos;
    float phase, swing, hold, bow;

    void Start()
    {
        lastPos = transform.position;
        Rebuild();
    }

    /// <summary>Builds (or rebuilds, after the Player model's saved) the body from the model.</summary>
    public void Rebuild()
    {
        if (root != null) Destroy(root.gameObject);
        var model = EnemyModel.ForGame("Player") ?? EnemyModel.Template("Player");
        root = new GameObject("Player Body").transform;
        root.SetParent(transform, false);
        var legs = MaterialFactory.Lit(new Color(0.12f, 0.13f, 0.18f), 0.2f); // jeans
        built = model.Build(root, 1f, part =>
            part == null ? legs : part.glow > 0f ? MaterialFactory.NoFog(part.color * part.glow) : MaterialFactory.Lit(part.color, 0.2f), true, false);

        var held = new System.Collections.Generic.List<GameObject>();
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            r.gameObject.layer = Layer;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; // (no stiff shadow of it under you)
            r.rayTracingMode = UnityEngine.Experimental.Rendering.RayTracingMode.Off;
            if (r.name.StartsWith("Remote")) held.Add(r.gameObject);
        }
        remote = held.ToArray();
    }

    void LateUpdate()
    {
        if (built == null) return;
        // Your own camera never sees it; the others (the drone's, the portal views') do.
        var main = Camera.main;
        foreach (var cam in Camera.allCameras)
        {
            int mask = cam == main ? cam.cullingMask & ~(1 << Layer) : cam.cullingMask | (1 << Layer);
            if (mask != cam.cullingMask) cam.cullingMask = mask;
        }

        float dt = Time.deltaTime;
        if (dt <= 0f) return;
        Vector3 moved = transform.position - lastPos;
        lastPos = transform.position;
        moved.y = 0f;
        float speed = moved.magnitude > 2f ? 0f : moved.magnitude / dt; // (a teleport isn't walking)

        // Walking: arms and legs swing in step; standing, they settle.
        phase += moved.magnitude * strideRate * Mathf.PI;
        swing = Mathf.MoveTowards(swing, Mathf.Clamp01(speed / 3f), dt * 4f);
        float s = Mathf.Sin(phase) * walkSwing * swing;

        // Flying the drone: the remote held out in front, head bowed over it.
        bool piloting = DroneUnit.Piloting;
        hold = Mathf.MoveTowards(hold, piloting ? 1f : 0f, dt * 3f);
        bow = Mathf.MoveTowards(bow, piloting ? 1f : 0f, dt * 2f);
        foreach (var go in remote) if (go.activeSelf != hold > 0.5f) go.SetActive(hold > 0.5f);

        built.hipLeft.localRotation = Quaternion.Euler(s, 0f, 0f);
        built.hipRight.localRotation = Quaternion.Euler(-s, 0f, 0f);
        built.armLeft.localRotation = Quaternion.Euler(Mathf.Lerp(-s * 0.8f, -60f, hold), 0f, Mathf.Lerp(-4f, 18f, hold));
        built.armRight.localRotation = Quaternion.Euler(Mathf.Lerp(s * 0.8f, -60f, hold), 0f, Mathf.Lerp(4f, -18f, hold));
        built.head.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 32f, bow) + Mathf.Sin(Time.time * 0.7f) * 1.5f, 0f, 0f);
    }
}
