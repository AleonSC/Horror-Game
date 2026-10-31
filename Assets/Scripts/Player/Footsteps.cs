using UnityEngine;

/// <summary>
/// Very faint footsteps: a soft crunch every stride while walking, slightly louder when running,
/// barely there when crouching, and a thump on landing. LampSpawner adds this to the player.
/// Leave the clip list empty to use the built-in synthesized steps.
/// </summary>
[RequireComponent(typeof(FirstPersonController))]
public class Footsteps : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Save Tuning on the component's menu)

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

    [Header("Water")]
    [Tooltip("Volume of a wading step in deep water (half a metre or more); shallower is quieter.")]
    [Range(0f, 1f)] public float wadeVolume = 0.15f;
    [Range(0f, 1f)] public float puddleVolume = 0.07f;
    [Tooltip("Volume of the continuous swish of moving through deep water.")]
    [Range(0f, 1f)] public float wadeLoopVolume = 0.1f;

    FirstPersonController controller;
    AudioSource source, wadeLoop;
    float wadeLevel;
    Vector3 lastPosition;
    float distance, airTime;
    bool wasGrounded = true;
    int lastVariant = -1;

    void Start()
    {
        controller = GetComponent<FirstPersonController>();
        source = ProceduralAudio.AddSource(gameObject, null, false, false, 1f);
        wadeLoop = ProceduralAudio.AddSource(gameObject, ProceduralAudio.WadeLoop, true, false, 0f);
        lastPosition = transform.position;
    }

    /// <summary>The swish of water round your legs: as loud as you're moving through it, and as deep as it is.</summary>
    void UpdateWadeLoop(Vector3 delta)
    {
        float target = 0f;
        if (controller.enabled && controller.IsGrounded && Water.InWater(transform.position, out float depth) && depth > 0.12f)
        {
            float speed = delta.magnitude / Mathf.Max(Time.deltaTime, 0.0001f);
            float moving = Mathf.Clamp01(speed / 2.5f);
            target = wadeLoopVolume * moving * Mathf.Lerp(0.4f, 1f, Mathf.Clamp01(depth / 0.5f)) * (controller.IsCrouching ? 0.5f : 1f);
        }
        wadeLevel = Mathf.MoveTowards(wadeLevel, target, Time.deltaTime * (target > wadeLevel ? 1.5f : 0.8f));
        wadeLoop.volume = wadeLevel * (1f - 0.85f * EntityDread.Silence);
        wadeLoop.pitch = controller.IsRunning ? 1.15f : 1f;
        if (wadeLevel > 0.001f && !wadeLoop.isPlaying) { wadeLoop.time = Random.Range(0f, wadeLoop.clip.length * 0.9f); wadeLoop.Play(); }
        else if (wadeLevel <= 0.001f && wadeLoop.isPlaying) wadeLoop.Stop();
    }

    void Update()
    {
        Vector3 delta = transform.position - lastPosition;
        lastPosition = transform.position;
        delta.y = 0f;
        if (delta.magnitude > 2f) delta = Vector3.zero; // a teleport (map wrap or respawn), not a step
        UpdateWadeLoop(delta);

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

    int lastSplash = -1;

    void Step(float volume)
    {
        // In water: a splash, louder the deeper, and it carries.
        if (Water.InWater(transform.position, out float depth))
        {
            // Never the same splash twice in a row; a puddle slaps, deeper water is waded through.
            int splash = Random.Range(0, ProceduralAudio.WaterStepCount);
            if (splash == lastSplash) splash = (splash + 1) % ProceduralAudio.WaterStepCount;
            lastSplash = splash;
            bool wading = depth > 0.12f;
            // As loud as the step would be (walking, running, creeping), scaled to the water: a puddle's slap
            // well under a deep wade, which is louder the deeper, but never a roar.
            float pace = volume / Mathf.Max(walkVolume, 0.0001f); // 1 walking, more running, less crouching
            float loud = wading ? wadeVolume * Mathf.Lerp(0.55f, 1f, Mathf.Clamp01(depth / 0.5f)) : puddleVolume;
            source.pitch = Random.Range(0.93f, 1.07f) * (wading ? Mathf.Lerp(1f, 0.85f, Mathf.Clamp01(depth / 0.6f)) : 1f);
            source.PlayOneShot(wading ? ProceduralAudio.Wade(splash) : ProceduralAudio.PuddleStep(splash),
                Mathf.Clamp01(loud * Mathf.Clamp(pace, 0.4f, 1.6f)) * (1f - 0.85f * EntityDread.Silence));
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
