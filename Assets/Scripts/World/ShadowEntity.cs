using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A tall pitch-black silhouette that stalks the player. It dashes around a circle
/// (the "perimeter") centred on the player, pausing briefly between dashes and sometimes
/// standing still to watch. If the player gets too close it bolts away into the dark,
/// vanishes, and reappears somewhere else on the perimeter a few seconds later.
///
/// The body is unlit black, so the flashlight doesn't light it up: it shows as a solid
/// shadow against lit trees and ground. DarkForestGenerator spawns one automatically.
/// </summary>
public class ShadowEntity : MonoBehaviour
{
    [Tooltip("Who to stalk. Found automatically if left empty.")]
    public Transform target;

    [Header("Perimeter")]
    public float minRadius = 10f;
    public float maxRadius = 18f;
    [Tooltip("If the player gets this far away, the entity vanishes and relocates.")]
    public float relocateDistance = 30f;

    [Header("Dashing")]
    public float dashSpeed = 16f;
    [Tooltip("How far around the circle a single dash goes, in degrees.")]
    public float minDashAngle = 40f;
    public float maxDashAngle = 150f;
    public float minPauseTime = 0.3f;
    public float maxPauseTime = 1.5f;
    [Tooltip("Forward lean while dashing, in degrees.")]
    public float dashLean = 18f;

    [Header("Standing Still")]
    [Range(0f, 1f), Tooltip("Chance after each dash that it stops and stares instead of a short pause.")]
    public float standChance = 0.35f;
    public float minStandTime = 3f;
    public float maxStandTime = 9f;

    [Header("Too Close")]
    [Tooltip("When the player gets this close while it's still, it flees.")]
    public float tooCloseDistance = 6f;
    public float fleeSpeed = 28f;
    public float fleeDuration = 0.7f;
    public float minHideTime = 3f;
    public float maxHideTime = 8f;
    [Tooltip("Seconds before it first appears.")]
    public float initialHideTime = 5f;

    [Header("Look")]
    public float height = 2.4f;
    public bool glowingEyes = true;
    public Color eyeColor = new Color(0.6f, 0.05f, 0.05f);

    [Header("Audio (optional)")]
    public AudioClip dashSound;
    public AudioClip fleeSound;
    [Range(0f, 1f)] public float volume = 0.8f;

    public enum State { Hidden, Dashing, Pausing, Standing, Fleeing }
    public State CurrentState { get; private set; }

    Transform visual;
    Renderer[] renderers;
    AudioSource audioSource;
    DarkForestGenerator generator;

    float timer;
    float angle;       // degrees around the player
    float radius;
    float targetAngle;
    float targetRadius;
    Vector3 fleeDirection;

    void Awake()
    {
        BuildSilhouette();

        if (dashSound != null || fleeSound != null)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.maxDistance = 40f;
        }
    }

    void Start()
    {
        generator = FindAnyObjectByType<DarkForestGenerator>();
        Hide(initialHideTime);
    }

    void Update()
    {
        if (target == null)
        {
            var player = FindAnyObjectByType<FirstPersonController>();
            if (player == null) return;
            target = player.transform;
        }

        float dist = HorizontalDistanceToTarget();

        switch (CurrentState)
        {
            case State.Hidden:
                timer -= Time.deltaTime;
                if (timer <= 0f) Reappear();
                break;

            case State.Dashing:
                UpdateDash();
                break;

            case State.Pausing:
            case State.Standing:
                FacePlayer();
                SetLean(0f);
                if (dist < tooCloseDistance) { StartFlee(); break; }
                if (dist > relocateDistance) { Hide(Random.Range(minHideTime, maxHideTime)); break; }
                timer -= Time.deltaTime;
                if (timer <= 0f) StartDash();
                break;

            case State.Fleeing:
                transform.position += fleeDirection * (fleeSpeed * Time.deltaTime);
                SnapToGround();
                SetLean(dashLean * 1.5f);
                timer -= Time.deltaTime;
                if (timer <= 0f) Hide(Random.Range(minHideTime, maxHideTime));
                break;
        }
    }

    // ---------------------------------------------------------------- States

    void StartDash()
    {
        // Re-measure from where we are now, since the player has moved since the last dash.
        Vector3 offset = transform.position - target.position;
        angle = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
        radius = new Vector2(offset.x, offset.z).magnitude;

        float sign = Random.value < 0.5f ? -1f : 1f;
        targetAngle = angle + sign * Random.Range(minDashAngle, maxDashAngle);
        targetRadius = Random.Range(minRadius, maxRadius);

        CurrentState = State.Dashing;
        PlaySound(dashSound);
    }

    void UpdateDash()
    {
        Vector3 previous = transform.position;

        float angularSpeed = dashSpeed / Mathf.Max(radius, 1f) * Mathf.Rad2Deg;
        angle = Mathf.MoveTowards(angle, targetAngle, angularSpeed * Time.deltaTime);
        radius = Mathf.MoveTowards(radius, targetRadius, dashSpeed * 0.5f * Time.deltaTime);

        transform.position = PointOnPerimeter(angle, radius);

        Vector3 moved = transform.position - previous;
        moved.y = 0f;
        if (moved.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(moved);
        SetLean(dashLean);

        if (Mathf.Approximately(angle, targetAngle) && Mathf.Abs(radius - targetRadius) < 0.05f)
        {
            if (Random.value < standChance)
            {
                CurrentState = State.Standing;
                timer = Random.Range(minStandTime, maxStandTime);
            }
            else
            {
                CurrentState = State.Pausing;
                timer = Random.Range(minPauseTime, maxPauseTime);
            }
        }
    }

    void StartFlee()
    {
        fleeDirection = transform.position - target.position;
        fleeDirection.y = 0f;
        fleeDirection = fleeDirection.sqrMagnitude > 0.001f ? fleeDirection.normalized : transform.forward;
        // Veer off to one side so it doesn't just back away in a straight line.
        fleeDirection = Quaternion.Euler(0f, Random.Range(-35f, 35f), 0f) * fleeDirection;

        transform.rotation = Quaternion.LookRotation(fleeDirection);

        CurrentState = State.Fleeing;
        timer = fleeDuration;
        PlaySound(fleeSound != null ? fleeSound : dashSound);
    }

    void Hide(float duration)
    {
        CurrentState = State.Hidden;
        timer = duration;
        SetVisible(false);
    }

    void Reappear()
    {
        angle = Random.Range(0f, 360f);
        radius = Random.Range(minRadius, maxRadius);
        transform.position = PointOnPerimeter(angle, radius);
        FacePlayer();
        SetVisible(true);

        bool stand = Random.value < standChance;
        CurrentState = stand ? State.Standing : State.Pausing;
        timer = stand ? Random.Range(minStandTime, maxStandTime) : Random.Range(minPauseTime, maxPauseTime);
    }

    // ---------------------------------------------------------------- Helpers

    float HorizontalDistanceToTarget()
    {
        Vector3 d = transform.position - target.position;
        d.y = 0f;
        return d.magnitude;
    }

    Vector3 PointOnPerimeter(float degrees, float r)
    {
        float rad = degrees * Mathf.Deg2Rad;
        Vector3 p = target.position + new Vector3(Mathf.Sin(rad), 0f, Mathf.Cos(rad)) * r;
        p.y = GroundHeight(p);
        return p;
    }

    void SnapToGround()
    {
        Vector3 p = transform.position;
        p.y = GroundHeight(p);
        transform.position = p;
    }

    float GroundHeight(Vector3 p)
    {
        if (generator != null) return generator.GetGroundHeight(p.x, p.z);
        if (Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out var hit, 100f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return target != null ? target.position.y : p.y;
    }

    void FacePlayer()
    {
        Vector3 look = target.position - transform.position;
        look.y = 0f;
        if (look.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(look);
    }

    void SetLean(float degrees)
    {
        Quaternion goal = Quaternion.Euler(degrees, 0f, 0f);
        visual.localRotation = Quaternion.Slerp(visual.localRotation, goal, 1f - Mathf.Exp(-12f * Time.deltaTime));
    }

    void SetVisible(bool visible)
    {
        foreach (var r in renderers) r.enabled = visible;
    }

    void PlaySound(AudioClip clip)
    {
        if (audioSource != null && clip != null) audioSource.PlayOneShot(clip, volume);
    }

    // ---------------------------------------------------------------- Body

    void BuildSilhouette()
    {
        visual = new GameObject("Silhouette").transform;
        visual.SetParent(transform, false);

        Material black = MakeUnlitMaterial(Color.black);
        float s = height / 2.4f; // proportions below are for a 2.4m figure

        // Long thin legs
        AddPart(PrimitiveType.Cylinder, black, new Vector3(-0.12f, 0.55f, 0f), new Vector3(0.12f, 0.55f, 0.12f), 0f, s);
        AddPart(PrimitiveType.Cylinder, black, new Vector3(0.12f, 0.55f, 0f), new Vector3(0.12f, 0.55f, 0.12f), 0f, s);
        // Narrow torso
        AddPart(PrimitiveType.Capsule, black, new Vector3(0f, 1.55f, 0f), new Vector3(0.42f, 0.5f, 0.25f), 0f, s);
        // Thin neck and small head, tilted slightly
        AddPart(PrimitiveType.Cylinder, black, new Vector3(0f, 2.05f, 0.02f), new Vector3(0.08f, 0.1f, 0.08f), 0f, s);
        var head = AddPart(PrimitiveType.Sphere, black, new Vector3(0f, 2.22f, 0.04f), new Vector3(0.26f, 0.32f, 0.26f), 0f, s);
        head.localRotation = Quaternion.Euler(0f, 0f, 12f);
        // Arms that hang down past the knees
        AddPart(PrimitiveType.Cylinder, black, new Vector3(-0.3f, 1.25f, 0f), new Vector3(0.07f, 0.68f, 0.07f), -6f, s);
        AddPart(PrimitiveType.Cylinder, black, new Vector3(0.3f, 1.25f, 0f), new Vector3(0.07f, 0.68f, 0.07f), 6f, s);

        if (glowingEyes)
        {
            Material eyes = MakeUnlitMaterial(eyeColor);
            var left = AddPart(PrimitiveType.Sphere, eyes, new Vector3(-0.055f, 2.25f, 0.15f), Vector3.one * 0.035f, 0f, s);
            var right = AddPart(PrimitiveType.Sphere, eyes, new Vector3(0.055f, 2.25f, 0.15f), Vector3.one * 0.035f, 0f, s);
            left.SetParent(head, true);
            right.SetParent(head, true);
        }

        renderers = GetComponentsInChildren<Renderer>();
    }

    Transform AddPart(PrimitiveType type, Material mat, Vector3 pos, Vector3 scale, float roll, float s)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = type.ToString();
        go.transform.SetParent(visual, false);
        go.transform.localPosition = pos * s;
        go.transform.localScale = scale * s;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
        go.GetComponent<Renderer>().sharedMaterial = mat;

        // It's a ghost: no collisions with the player or trees.
        var col = go.GetComponent<Collider>();
        if (Application.isPlaying) Destroy(col);
        else DestroyImmediate(col);
        return go.transform;
    }

    static Material MakeUnlitMaterial(Color color)
    {
        bool usingSrp = GraphicsSettings.currentRenderPipeline != null;
        Shader shader = Shader.Find(usingSrp ? "Universal Render Pipeline/Unlit" : "Unlit/Color");
        if (shader == null) shader = Shader.Find("Unlit/Color");

        var mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
        return mat;
    }
}
