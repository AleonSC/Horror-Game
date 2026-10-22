using UnityEngine;

/// <summary>
/// A creaky hinged door. Click it (via LampInteractor) to swing it open or shut.
/// Sits on the hinge pivot; the door panel is a child.
/// </summary>
public class ShackDoor : MonoBehaviour
{
    [Tooltip("How far the door swings open, in degrees.")]
    public float openAngle = -105f;
    [Tooltip("Degrees per second.")]
    public float swingSpeed = 120f;
    [Tooltip("Local axis it swings about: up for a door, sideways for a lid or a flap.")]
    public Vector3 axis = Vector3.up;
    [Tooltip("What it's called in the prompt (\"Open the ...\", with E).")]
    public string noun = "door";
    /// <summary>What it sounds like: a wooden door, a car's metal door (or trunk), a plastic glove box, or a tent's canvas flap.</summary>
    public enum Kind { Wood, Car, Plastic, Canvas }
    public Kind kind = Kind.Wood;
    bool shutPending; // swinging shut: the bang comes as it lands

    public bool IsOpen { get; private set; }

    /// <summary>How far open it is right now, 0 shut to 1 fully open.</summary>
    public float OpenAmount => Mathf.Approximately(openAngle, 0f) ? 0f : Mathf.Clamp01(angle / openAngle);

    /// <summary>Locked doors only rattle. lockedMessage is what the player is told when they look at it.</summary>
    [HideInInspector] public bool locked;
    [HideInInspector] public string lockedMessage = "It's locked";

    /// <summary>Another door that opens and shuts along with this one (the lone door and its twin in the dark room).</summary>
    [HideInInspector] public ShackDoor linked;

    float angle;
    AudioSource audioSource;

    void Start()
    {
        audioSource = ProceduralAudio.AddSource(gameObject, null, false, true, 0.8f, 2f, 25f);
    }

    public void Toggle()
    {
        if (locked && !IsOpen)
        {
            // The handle turns, the door shudders in its frame, and doesn't give.
            if (audioSource != null)
            {
                audioSource.pitch = Random.Range(0.92f, 1.08f);
                audioSource.PlayOneShot(kind == Kind.Wood ? ProceduralAudio.DoorRattle : ProceduralAudio.GloveBoxShut, 0.8f);
            }
            Noise.Emit(transform.position, 6f);
            return;
        }
        IsOpen = !IsOpen;
        if (linked != null) linked.IsOpen = IsOpen; // silently, it's the same door
        Noise.Emit(transform.position, 13f); // a creaking hinge carries
        if (audioSource != null)
        {
            audioSource.pitch = Random.Range(0.92f, 1.06f);
            if (kind == Kind.Canvas)
                audioSource.PlayOneShot(ProceduralAudio.BushRustle, 0.7f); // a rustle and flap of canvas, either way
            else if (IsOpen)
                audioSource.PlayOneShot(kind == Kind.Wood ? ProceduralAudio.DoorOpen : kind == Kind.Car ? ProceduralAudio.CarDoorOpen : ProceduralAudio.GloveBoxOpen);
            else if (kind == Kind.Wood)
                audioSource.PlayOneShot(ProceduralAudio.DoorClose, 0.8f);
        }
        shutPending = !IsOpen;
        if (!IsOpen && (kind == Kind.Wood || kind == Kind.Car)) DoorKnocks.DoorClosed(transform.position); // now and then, something answers
    }

    /// <summary>Sets it open or shut at once, with no swing and no sound (for how it's left when built).</summary>
    public void SetOpenInstant(bool open)
    {
        IsOpen = open;
        shutPending = false;
        angle = open ? openAngle : 0f;
        transform.localRotation = Quaternion.AngleAxis(angle, axis);
    }

    /// <summary>Shuts the door instantly (used when the run resets).</summary>
    public void Close()
    {
        IsOpen = false;
        shutPending = false;
        angle = 0f;
        transform.localRotation = Quaternion.identity;
    }

    Collider[] solids;

    /// <summary>
    /// Open, the door's out of the way: it doesn't block the doorway or catch you as you go past (it's only
    /// solid shut, or nearly). Its colliders become triggers rather than going off, so it can still be looked
    /// at and shut again.
    /// </summary>
    void UpdateSolid()
    {
        if (solids == null)
        {
            var list = new System.Collections.Generic.List<Collider>();
            foreach (var c in GetComponentsInChildren<Collider>()) if (!c.isTrigger) list.Add(c);
            solids = list.ToArray();
        }
        bool solid = OpenAmount < 0.15f;
        foreach (var c in solids)
        {
            if (c == null || c.isTrigger == !solid) continue;
            if (c is MeshCollider mesh && !mesh.convex) continue; // (a concave mesh can't be a trigger)
            c.isTrigger = !solid;
        }
    }

    void Update()
    {
        UpdateSolid();
        float target = IsOpen ? openAngle : 0f;
        if (Mathf.Approximately(angle, target)) return;
        angle = Mathf.MoveTowards(angle, target, swingSpeed * Time.deltaTime);
        // Landing in its frame.
        if (shutPending && !IsOpen && Mathf.Approximately(angle, 0f))
        {
            shutPending = false;
            if (audioSource != null && kind != Kind.Canvas)
            {
                audioSource.pitch = Random.Range(0.94f, 1.06f);
                audioSource.PlayOneShot(kind == Kind.Wood ? ProceduralAudio.DoorShut : kind == Kind.Car ? ProceduralAudio.CarDoorShut : ProceduralAudio.GloveBoxShut);
            }
        }
        transform.localRotation = Quaternion.AngleAxis(angle, axis);
    }
}
