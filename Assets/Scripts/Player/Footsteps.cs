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
            if (!wasGrounded && airTime > 0.3f) Step(landVolume);
            airTime = 0f;
            distance += delta.magnitude;
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
        }
    }

    void Step(float volume)
    {
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
        source.PlayOneShot(clip, volume * (1f - 0.85f * EntityDread.Silence)); // even your steps go quiet near it
    }
}
