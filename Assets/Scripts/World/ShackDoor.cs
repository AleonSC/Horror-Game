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
                audioSource.pitch = Random.Range(1.5f, 1.8f);
                audioSource.PlayOneShot(ProceduralAudio.DoorCreak, 0.5f);
                audioSource.PlayOneShot(ProceduralAudio.LampClick, 0.6f);
            }
            Noise.Emit(transform.position, 6f);
            return;
        }
        IsOpen = !IsOpen;
        if (linked != null) linked.IsOpen = IsOpen; // silently, it's the same door
        Noise.Emit(transform.position, 13f); // a creaking hinge carries
        if (audioSource != null)
        {
            audioSource.pitch = IsOpen ? Random.Range(0.9f, 1.05f) : Random.Range(0.75f, 0.9f);
            audioSource.PlayOneShot(ProceduralAudio.DoorCreak);
        }
    }

    /// <summary>Shuts the door instantly (used when the run resets).</summary>
    public void Close()
    {
        IsOpen = false;
        angle = 0f;
        transform.localRotation = Quaternion.identity;
    }

    void Update()
    {
        float target = IsOpen ? openAngle : 0f;
        if (Mathf.Approximately(angle, target)) return;
        angle = Mathf.MoveTowards(angle, target, swingSpeed * Time.deltaTime);
        transform.localRotation = Quaternion.Euler(0f, angle, 0f);
    }
}
