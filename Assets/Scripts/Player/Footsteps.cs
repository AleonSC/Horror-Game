using UnityEngine;

/// <summary>
/// Very faint footsteps: a soft crunch every stride while walking, slightly louder when running,
/// barely there when crouching, and a thump on landing. LampSpawner adds this to the player.
/// Leave the clip list empty to use the built-in synthesized steps.
/// </summary>
[RequireComponent(typeof(FirstPersonController))]
public class Footsteps : MonoBehaviour
{
    [Header("Volume")]
    [Range(0f, 1f)] public float walkVolume = 0.06f;
    [Range(0f, 1f)] public float runVolume = 0.1f;
    [Range(0f, 1f)] public float crouchVolume = 0.03f;
    [Range(0f, 1f)] public float landVolume = 0.12f;

    [Header("Stride (metres between steps)")]
    public float walkStride = 0.8f;
    public float runStride = 1.3f;
    public float crouchStride = 0.6f;

    [Header("Clips (optional)")]
    public AudioClip[] stepClips;

    FirstPersonController controller;
    AudioSource source;
    Vector3 lastPosition;
    float distance, airTime;
    bool wasGrounded = true;
    int lastVariant = -1;

    void Start()
    {
        controller = GetComponent<FirstPersonController>();
        source = ProceduralAudio.AddSource(gameObject, null, false, false, 1f);
        lastPosition = transform.position;
    }

    void Update()
    {
        Vector3 delta = transform.position - lastPosition;
        lastPosition = transform.position;
        delta.y = 0f;
        if (delta.magnitude > 2f) delta = Vector3.zero; // a teleport (map wrap or respawn), not a step

        if (!controller.enabled) return;

        bool grounded = controller.IsGrounded;
        if (grounded)
        {
            if (!wasGrounded && airTime > 0.3f)
            {
                Step(landVolume);
                Noise.Emit(transform.position, 11f); // a thump on landing carries
            }
            airTime = 0f;
            // In the dream hallway you run without getting anywhere, but the footsteps keep coming.
            distance += Mathf.Lerp(delta.magnitude, controller.CurrentSpeed * Time.deltaTime, controller.DreamStretch);
        }
        else
        {
            airTime += Time.deltaTime;
        }
        wasGrounded = grounded;

        float stride = controller.IsCrouching ? crouchStride : controller.IsRunning ? runStride : walkStride;
        if (distance >= stride)
        {
            distance = 0f;
            Step(controller.IsCrouching ? crouchVolume : controller.IsRunning ? runVolume : walkVolume);
            // How far each step can be heard: crouching barely at all, running a long way.
            Noise.Emit(transform.position, controller.IsCrouching ? 1.5f : controller.IsRunning ? 17f : 7f);
        }
    }

    void Step(float volume)
    {
        // In water: a splash, louder the deeper, and it carries.
        if (Water.InWater(transform.position, out float depth))
        {
            source.pitch = Random.Range(0.85f, 1.15f);
            source.PlayOneShot(ProceduralAudio.Splash, Mathf.Clamp01(volume * 2.5f + depth) * (1f - 0.85f * EntityDread.Silence));
            Noise.Emit(transform.position, depth > 0.1f ? 13f : 9f);
            return;
        }

        AudioClip clip;
        if (stepClips != null && stepClips.Length > 0)
        {
            clip = stepClips[Random.Range(0, stepClips.Length)];
        }
        else
        {
            // Never the same variant twice in a row.
            int variant = Random.Range(0, 4);
            if (variant == lastVariant) variant = (variant + 1) % 4;
            lastVariant = variant;
            clip = ProceduralAudio.Footstep(variant);
        }

        source.pitch = Random.Range(0.9f, 1.1f);
        // Even your steps go quiet near it; in the dark room's silence they're all there is, ringing off the walls.
        source.PlayOneShot(clip, volume * (1f - 0.85f * EntityDread.Silence) * (1f + 1.5f * PortalDoor.RoomQuiet));
    }
}
