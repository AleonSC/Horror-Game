using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

/// <summary>
/// The player's own body, simple (a hooded figure in a jacket and jeans): built from the "Player" model
/// (EnemyModel; the model editor's Player, or its simple starting model). You never see it through your
/// own eyes (it's on its own layer, which your camera leaves out), but every other camera does: from the
/// drone you can see yourself standing there, head bowed over the remote held out in front of you; and
/// through a rift or the dark room's door. It swings its arms and legs as you walk.
/// With a PlayerCharacter asset (Resources/PlayerCharacter) that has a model, that model is used instead,
/// animated from its clips (idle, walk, run, crouching, in the air, winded, flying the drone, seated),
/// blended by what you're doing (a PlayableGraph, so no Animator Controller is needed).
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
        controller = GetComponent<FirstPersonController>();
        Rebuild();
    }

    void OnDestroy() => DestroyGraph();

    /// <summary>Builds (or rebuilds, after the Player model's saved) the body from the model.</summary>
    public void Rebuild()
    {
        if (root != null) Destroy(root.gameObject);
        DestroyGraph();
        built = null;
        character = PlayerCharacter.Load();
        if (character != null && character.model != null)
        {
            BuildCharacter();
            return;
        }
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
        if (built == null && !graph.IsValid()) return;
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

        if (graph.IsValid())
        {
            Animate(speed, dt);
            return;
        }

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

    // ---------------------------------------------------------------- A character model (PlayerCharacter)

    enum Anim { Idle, Walk, Run, CrouchIdle, CrouchWalk, Jump, Winded, Drone, Seated }
    const int AnimCount = 9;

    PlayerCharacter character;
    FirstPersonController controller;
    PlayableGraph graph;
    AnimationMixerPlayable mixer;
    readonly AnimationClipPlayable[] clips = new AnimationClipPlayable[AnimCount];
    readonly bool[] loops = new bool[AnimCount];
    readonly float[] weights = new float[AnimCount];
    GameObject heldRemote;

    void DestroyGraph()
    {
        if (graph.IsValid()) graph.Destroy();
    }

    /// <summary>The clip for an animation, or the nearest one there is.</summary>
    AnimationClip ClipFor(Anim a)
    {
        var c = character;
        AnimationClip First(params AnimationClip[] options)
        {
            foreach (var o in options) if (o != null) return o;
            return null;
        }
        AnimationClip baseIdle = First(c.idle, c.walk, c.run, c.crouchIdle, c.crouchWalk);
        switch (a)
        {
            case Anim.Idle: return baseIdle;
            case Anim.Walk: return First(c.walk, c.run, baseIdle);
            case Anim.Run: return First(c.run, c.walk, baseIdle);
            case Anim.CrouchIdle: return First(c.crouchIdle, baseIdle);
            case Anim.CrouchWalk: return First(c.crouchWalk, c.walk, c.crouchIdle, baseIdle);
            case Anim.Jump: return First(c.jump, baseIdle);
            case Anim.Winded: return First(c.winded, baseIdle);
            case Anim.Drone: return First(c.droneControl, baseIdle);
            default: return First(c.seated, baseIdle);
        }
    }

    void BuildCharacter()
    {
        root = new GameObject("Player Body").transform;
        root.SetParent(transform, false);
        var model = Instantiate(character.model, root, false);
        model.name = character.model.name;
        model.transform.localRotation = Quaternion.Euler(0f, character.yawOffset, 0f);
        model.transform.localPosition = Vector3.zero;
        foreach (var col in model.GetComponentsInChildren<Collider>(true)) Destroy(col); // (it'd push you about)

        // Sized to stand height tall, feet on the ground.
        var renderers = model.GetComponentsInChildren<Renderer>(true);
        if (character.height > 0f && renderers.Length > 0)
        {
            Bounds b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            if (b.size.y > 0.01f) model.transform.localScale *= character.height / b.size.y;
            b = renderers[0].bounds;
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            model.transform.position += Vector3.up * (transform.position.y - b.min.y);
        }
        model.transform.localPosition += character.offset;

        foreach (var r in renderers)
        {
            r.gameObject.layer = Layer;
            r.shadowCastingMode = character.castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            r.rayTracingMode = UnityEngine.Experimental.Rendering.RayTracingMode.Off;
            if (r is SkinnedMeshRenderer skinned) skinned.updateWhenOffscreen = true; // (it's always off your own screen)
        }

        var animator = model.GetComponentInChildren<Animator>();
        if (animator == null) animator = model.AddComponent<Animator>();
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

        // The drone's remote, in the right hand (a humanoid rig's), shown while flying.
        Transform hand = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;
        heldRemote = BuildRemote(hand != null ? hand : root);

        if (ClipFor(Anim.Idle) == null) return; // (no animations: it just stands in its pose)
        graph = PlayableGraph.Create("Player Body");
        graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
        var output = AnimationPlayableOutput.Create(graph, "Body", animator);
        mixer = AnimationMixerPlayable.Create(graph, AnimCount);
        output.SetSourcePlayable(mixer);
        for (int i = 0; i < AnimCount; i++)
        {
            var anim = (Anim)i;
            var clip = ClipFor(anim);
            clips[i] = AnimationClipPlayable.Create(graph, clip);
            clips[i].SetApplyFootIK(true);
            loops[i] = anim != Anim.Jump;
            graph.Connect(clips[i], 0, mixer, i);
            weights[i] = anim == Anim.Idle ? 1f : 0f;
            mixer.SetInputWeight(i, weights[i]);
        }
        graph.Play();
    }

    /// <summary>A small remote with an aerial: in the hand (scaled back from the model's scale), or held out in front.</summary>
    GameObject BuildRemote(Transform parent)
    {
        var remoteRoot = new GameObject("Remote");
        remoteRoot.transform.SetParent(parent, false);
        if (parent == root) remoteRoot.transform.localPosition = new Vector3(0f, 1.1f, 0.35f);
        Vector3 s = parent.lossyScale;
        remoteRoot.transform.localScale = new Vector3(1f / Mathf.Max(s.x, 0.0001f), 1f / Mathf.Max(s.y, 0.0001f), 1f / Mathf.Max(s.z, 0.0001f));
        var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Destroy(body.GetComponent<Collider>());
        body.transform.SetParent(remoteRoot.transform, false);
        body.transform.localScale = new Vector3(0.14f, 0.03f, 0.09f);
        body.GetComponent<Renderer>().sharedMaterial = MaterialFactory.Lit(new Color(0.08f, 0.08f, 0.09f), 0.3f);
        var aerial = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Destroy(aerial.GetComponent<Collider>());
        aerial.transform.SetParent(remoteRoot.transform, false);
        aerial.transform.localPosition = new Vector3(0.05f, 0.06f, 0.03f);
        aerial.transform.localScale = new Vector3(0.008f, 0.06f, 0.008f);
        aerial.GetComponent<Renderer>().sharedMaterial = body.GetComponent<Renderer>().sharedMaterial;
        foreach (var r in remoteRoot.GetComponentsInChildren<Renderer>()) r.gameObject.layer = Layer;
        remoteRoot.SetActive(false);
        return remoteRoot;
    }

    /// <summary>Which animation fits what you're doing, blended toward it; walking clips play at your pace.</summary>
    void Animate(float speed, float dt)
    {
        bool moving = speed > 0.25f;
        Anim want;
        if (controller != null && controller.Seated) want = Anim.Seated;
        else if (DroneUnit.Piloting) want = Anim.Drone;
        else if (controller != null && !controller.IsGrounded) want = Anim.Jump;
        else if (controller != null && controller.IsCrouching) want = moving ? Anim.CrouchWalk : Anim.CrouchIdle;
        else if (moving) want = controller != null && controller.IsRunning ? Anim.Run : Anim.Walk;
        else want = controller != null && controller.Winded ? Anim.Winded : Anim.Idle;

        if (heldRemote != null && heldRemote.activeSelf != DroneUnit.Piloting) heldRemote.SetActive(DroneUnit.Piloting);

        float step = dt / Mathf.Max(character.blendTime, 0.01f);
        for (int i = 0; i < AnimCount; i++)
        {
            float before = weights[i];
            weights[i] = Mathf.MoveTowards(weights[i], i == (int)want ? 1f : 0f, step);
            mixer.SetInputWeight(i, weights[i]);
            var clip = clips[i];
            if (weights[i] <= 0f)
            {
                if (before > 0f) clip.SetTime(0); // (start from the top next time)
                continue;
            }
            // At your pace.
            var anim = (Anim)i;
            float rate = anim == Anim.Walk ? speed / Mathf.Max(character.walkClipSpeed, 0.1f)
                : anim == Anim.Run ? speed / Mathf.Max(character.runClipSpeed, 0.1f)
                : anim == Anim.CrouchWalk ? speed / Mathf.Max(character.crouchWalkClipSpeed, 0.1f) : 1f;
            clip.SetSpeed(Mathf.Clamp(rate, 0.5f, 2f));
            // Looping, whether or not the clip was imported to loop.
            float length = clip.GetAnimationClip() != null ? clip.GetAnimationClip().length : 0f;
            if (loops[i] && length > 0f && clip.GetTime() > length) clip.SetTime(clip.GetTime() % length);
        }
    }
}
