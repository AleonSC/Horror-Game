using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The print an instant camera spits out: a picture of exactly what you shot, flash and all. It slides out
/// of the slot under the lens, then drops, rocking and drifting side to side like a falling leaf, down to
/// the ground, where it lies as an ordinary physics object for the rest of the run (it can be knocked
/// about). The picture develops from murky brown to full colour over its first few seconds. Hotbar makes
/// these when you take a photo; GameOverScreen clears them for a new run.
/// </summary>
public class Polaroid : MonoBehaviour
{
    public static readonly List<Polaroid> All = new List<Polaroid>();
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() => All.Clear();

    const int PhotoSize = 256;
    // The card (metres): a classic square print with the wide strip at the bottom, a little larger than life.
    const float CardWidth = 0.12f, CardHeight = 0.145f, CardThickness = 0.0015f, PhotoSide = 0.104f;
    const float EjectTime = 0.45f, DevelopTime = 7f;

    RenderTexture picture;
    Material photoMaterial;
    Transform slot;
    float age;
    bool falling, resting;
    Vector3 velocity, drift;
    float swayPhase, swaySpeed, yaw;
    DarkForestGenerator generator;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    /// <summary>Removes every print (used when the run resets).</summary>
    public static void ClearAll()
    {
        foreach (var p in new List<Polaroid>(All)) if (p != null) Destroy(p.gameObject);
    }

    /// <summary>
    /// Takes a picture through a camera, as it is this very frame (so the flash lights it), and prints it out
    /// of the slot. Things closer than nearClip (your own hands and what's in them) aren't in it.
    /// </summary>
    public static Polaroid Take(Camera view, Transform printSlot, float nearClip = 0.7f)
    {
        var picture = new RenderTexture(PhotoSize, PhotoSize, 24) { name = "Polaroid" };
        picture.Create();

        // A second camera, matching the view, squared up, rendering just this frame into the picture.
        var shooter = new GameObject("Polaroid Shot").AddComponent<Camera>();
        shooter.CopyFrom(view);
        shooter.transform.SetPositionAndRotation(view.transform.position, view.transform.rotation);
        shooter.targetTexture = picture;
        shooter.aspect = 1f;
        shooter.nearClipPlane = Mathf.Max(view.nearClipPlane, nearClip);
        shooter.depth = view.depth - 1f;

        var print = new GameObject("Polaroid").AddComponent<Polaroid>();
        print.picture = picture;
        print.slot = printSlot;
        print.Build();
        print.StartCoroutine(print.DropCameraAfterFrame(shooter));
        return print;
    }

    IEnumerator DropCameraAfterFrame(Camera shooter)
    {
        yield return new WaitForEndOfFrame(); // it's rendered the picture
        if (shooter != null) Destroy(shooter.gameObject);
    }

    /// <summary>The print: a white card, the picture inset on its face nearer the top. Lies in its XY plane, face +Z.</summary>
    void Build()
    {
        var card = GameObject.CreatePrimitive(PrimitiveType.Cube);
        card.name = "Card";
        Destroy(card.GetComponent<Collider>());
        card.transform.SetParent(transform, false);
        card.transform.localScale = new Vector3(CardWidth, CardHeight, CardThickness);
        var cardRenderer = card.GetComponent<Renderer>();
        cardRenderer.sharedMaterial = MaterialFactory.Lit(new Color(0.9f, 0.88f, 0.83f), 0.25f);
        cardRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var photo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        photo.name = "Picture";
        Destroy(photo.GetComponent<Collider>());
        photo.transform.SetParent(transform, false);
        float border = (CardWidth - PhotoSide) * 0.5f;
        photo.transform.localPosition = new Vector3(0f, CardHeight * 0.5f - border - PhotoSide * 0.5f, CardThickness * 0.5f + 0.0004f);
        photo.transform.localRotation = Quaternion.Euler(0f, 180f, 0f); // (a quad faces -Z: turned to face out the front)
        photo.transform.localScale = new Vector3(PhotoSide, PhotoSide, 1f);
        photoMaterial = MaterialFactory.LitTextured(picture, null, new Color(0.06f, 0.05f, 0.04f), 0.35f);
        var photoRenderer = photo.GetComponent<Renderer>();
        photoRenderer.sharedMaterial = photoMaterial;
        photoRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Place();
        WorldWrap.RegisterDynamic(transform);
    }

    /// <summary>Where it is while it's still coming out of the slot: face up, sliding forward out of it.</summary>
    void Place()
    {
        if (slot == null) return;
        float out01 = Mathf.Clamp01(age / EjectTime);
        // Lying along the slot, face up, picture end first, the wide strip coming out last.
        transform.rotation = Quaternion.LookRotation(slot.up, slot.forward);
        transform.position = slot.position + slot.forward * (CardHeight * (out01 - 0.5f)) + slot.up * -0.004f;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;

        // Developing: murky brown to the full picture.
        float develop = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((age - 0.5f) / DevelopTime));
        if (photoMaterial != null && develop < 1.01f)
            MaterialFactory.SetColor(photoMaterial, Color.Lerp(new Color(0.06f, 0.05f, 0.04f), Color.white, develop));

        if (resting) return;
        if (!falling)
        {
            Place();
            if (age >= EjectTime || slot == null) Release();
            return;
        }
        Flutter(dt);
    }

    /// <summary>Out of the slot: off it goes, with a little push forward.</summary>
    void Release()
    {
        falling = true;
        Vector3 forward = slot != null ? slot.forward : transform.up;
        velocity = forward * 0.7f + Vector3.up * 0.2f;
        drift = Vector3.Cross(Vector3.up, forward).normalized;
        if (drift.sqrMagnitude < 0.01f) drift = Vector3.right;
        swayPhase = Random.Range(0f, Mathf.PI * 2f);
        swaySpeed = Random.Range(2.3f, 3.1f);
        yaw = Quaternion.LookRotation(Vector3.ProjectOnPlane(forward, Vector3.up).normalized + Vector3.forward * 0.001f).eulerAngles.y;
    }

    /// <summary>
    /// Falling like a leaf: it sinks slowly, swinging from side to side and tipping into each swing,
    /// turning a little as it goes, until it touches something; then it's an ordinary physics object.
    /// </summary>
    void Flutter(float dt)
    {
        swayPhase += swaySpeed * dt;
        float swing = Mathf.Sin(swayPhase);
        // Its own push dies away into the slow side-to-side fall.
        velocity = Vector3.Lerp(velocity, drift * (swing * 0.9f) + Vector3.down * (0.45f + 0.25f * Mathf.Abs(swing)), 1f - Mathf.Exp(-3f * dt));
        yaw += 25f * Mathf.Cos(swayPhase * 0.5f) * dt;
        Vector3 step = velocity * dt;

        // Touching down (or onto anything), it settles there.
        if (Physics.Raycast(transform.position + Vector3.up * 0.02f, step.normalized, out var hit, step.magnitude + 0.03f, ~0, QueryTriggerInteraction.Ignore))
        {
            Land(hit.point + hit.normal * 0.004f);
            return;
        }
        if (generator == null) generator = FindAnyObjectByType<DarkForestGenerator>();
        float ground = generator != null ? generator.GetGroundHeight(transform.position.x, transform.position.z) : float.MinValue;
        if (transform.position.y + step.y <= ground + 0.004f || age > 30f)
        {
            Land(new Vector3(transform.position.x, Mathf.Max(ground, transform.position.y + step.y) + 0.004f, transform.position.z));
            return;
        }

        transform.position += step;
        // Face up, tipped into the swing (the edge going up leading), and turning slowly.
        float tip = Mathf.Cos(swayPhase) * 38f;
        transform.rotation = Quaternion.AngleAxis(tip, Quaternion.Euler(0f, yaw, 0f) * Vector3.forward) * Quaternion.Euler(-90f, yaw, 0f);
    }

    void Land(Vector3 at)
    {
        falling = false;
        resting = true;
        transform.position = at;
        var box = gameObject.AddComponent<BoxCollider>();
        box.size = new Vector3(CardWidth, CardHeight, CardThickness * 3f);
        var body = gameObject.AddComponent<Rigidbody>();
        body.mass = 0.02f;
        body.linearDamping = 2f;
        body.angularDamping = 2f;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        body.linearVelocity = velocity * 0.3f;
    }

    void OnDestroy()
    {
        if (picture != null) picture.Release();
        if (picture != null) Destroy(picture);
        if (photoMaterial != null) Destroy(photoMaterial);
    }
}
