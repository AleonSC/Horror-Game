using UnityEngine;

/// <summary>
/// The forest goes quiet when one of them is close. Within silenceDistance of a visible entity, the drone,
/// wind, distant noises and even your own footsteps fade almost to nothing (the other audio scripts read
/// Silence), and slow, heavy breathing starts up just behind your head.
///
/// And when a scour (or the blind one) is close enough to take you, your heartbeat takes over: it starts up and
/// quickens as it closes in, while everything else, music included, fades almost to nothing (Hush).
/// When you get away (or it's driven off), the world comes back slowly.
/// DarkForestGenerator adds this automatically.
/// </summary>
public class EntityDread : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Save Tuning on the component's menu)

    /// <summary>0 normally, 1 when an entity is right on you. Other sounds duck by this.</summary>
    public static float Silence { get; private set; }
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() { Silence = 0f; Hush = 0f; }

    /// <summary>0 normally, 1 when a scour is about to take you: every other sound (and the music) fades by this.</summary>
    public static float Hush { get; private set; }

    [Header("Heartbeat")]
    [Tooltip("Your heartbeat starts when a scour is this close...")]
    public float heartbeatDistance = 9f;
    [Tooltip("The blind one's bigger: the heartbeat starts when it's this close.")]
    public float blindHeartbeatDistance = 16f;
    [Tooltip("...and is pounding (and the rest of the world almost silent) by the time it's within reach.")]
    [Range(0f, 1f)] public float heartbeatVolume = 0.9f;
    [Tooltip("How much the rest of the world fades at worst (1 = to nothing).")]
    [Range(0f, 1f)] public float hushAmount = 0.92f;
    [Tooltip("Beats per minute as it starts, and at its worst.")]
    public float calmRate = 70f, panicRate = 165f;
    AudioSource heart;
    float peril, beatTimer;

    [Tooltip("The quiet starts when a visible entity is this close...")]
    public float silenceDistance = 7f;
    [Tooltip("...and is total by this distance.")]
    public float fullSilenceDistance = 3.5f;
    [Range(0f, 1f)] public float breathVolume = 0.55f;
    [Tooltip("How far behind the camera the breathing is.")]
    public float breathOffset = 0.35f;

    AudioSource breath;

    void Start()
    {
        var go = new GameObject("Breathing");
        go.transform.SetParent(transform, false);
        breath = ProceduralAudio.AddSource(go, ProceduralAudio.Breath, true, true, 0f, 0.4f, 6f);
        heart = ProceduralAudio.AddSource(gameObject, ProceduralAudio.Heartbeat, false, false, 1f);
        heart.ignoreListenerVolume = true; // it's the one sound that doesn't fade with the rest
    }

    void OnDestroy()
    {
        Silence = 0f;
        Hush = 0f;
        AudioListener.volume = PauseMenu.ListenerVolume;
    }

    /// <summary>
    /// How close a scour is to taking you, 0-1: from heartbeatDistance down to just outside its reach, and 1
    /// while one has you.
    /// </summary>
    float Peril(Vector3 listener, out bool taken)
    {
        taken = ShadowEntity.Capturing != null;
        if (taken) return 1f;
        float worst = 0f;
        foreach (var entity in ShadowEntity.All)
        {
            if (entity == null || !entity.IsVisible) continue;
            if (entity.CurrentState == ShadowEntity.State.Killing) { taken = true; return 1f; }
            Vector3 d = entity.transform.position - listener;
            d.y = 0f;
            worst = Mathf.Max(worst, Mathf.InverseLerp(heartbeatDistance, entity.killDistance + 0.3f, d.magnitude));
        }
        foreach (var blind in BlindEntity.All)
        {
            if (blind == null || !blind.IsPresent) continue;
            if (blind.IsTaking) { taken = true; return 1f; }
            Vector3 d = blind.transform.position - listener;
            d.y = 0f;
            worst = Mathf.Max(worst, Mathf.InverseLerp(blindHeartbeatDistance, blind.grabDistance + 0.5f, d.magnitude));
        }
        return worst;
    }

    void UpdateHeartbeat(Vector3 listener)
    {
        bool taken = false;
        float target = GameOverScreen.IsActive || PauseMenu.IsPaused ? 0f : Peril(listener, out taken);
        // Takes hold quickly, lets go slowly.
        peril = Mathf.MoveTowards(peril, target, Time.unscaledDeltaTime / (target > peril ? 0.8f : 2.5f));
        float eased = peril * peril * (3f - 2f * peril);
        // Once it has you, the taking's own sounds come back up over the pounding.
        float hushTarget = eased * hushAmount * (taken ? 0.3f : 1f);
        Hush = Mathf.MoveTowards(Hush, hushTarget, Time.unscaledDeltaTime / 0.5f);
        AudioListener.volume = PauseMenu.ListenerVolume * (1f - Hush);

        if (peril < 0.02f || PauseMenu.IsPaused) { beatTimer = 0f; return; }
        beatTimer -= Time.deltaTime;
        if (beatTimer > 0f) return;
        float rate = Mathf.Lerp(calmRate, panicRate, eased);
        beatTimer = 60f / Mathf.Max(rate, 1f);
        heart.pitch = Mathf.Lerp(0.95f, 1.08f, eased);
        heart.PlayOneShot(heart.clip, heartbeatVolume * PauseMenu.MasterVolume * Mathf.Lerp(0.35f, 1f, eased));
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 listener = cam.transform.position;

        float nearest = float.MaxValue;
        foreach (var entity in ShadowEntity.All)
            if (entity != null && entity.IsVisible)
                nearest = Mathf.Min(nearest, Vector3.Distance(entity.transform.position, listener));
        foreach (var blind in BlindEntity.All)
            if (blind != null) nearest = Mathf.Min(nearest, Vector3.Distance(blind.transform.position, listener));

        UpdateHeartbeat(listener);

        float target = Mathf.Clamp01(Mathf.InverseLerp(silenceDistance, fullSilenceDistance, nearest));
        if (GameOverScreen.IsActive) target = 0f;
        // Falls quiet quickly, comes back slowly.
        Silence = Mathf.MoveTowards(Silence, target, Time.deltaTime * (target > Silence ? 1.2f : 0.4f));

        // Breathing right behind you, wherever you turn.
        breath.transform.position = listener - cam.transform.forward * breathOffset + Vector3.up * 0.05f;
        breath.volume = breathVolume * Silence;
        if (Silence > 0.01f && !breath.isPlaying) breath.Play();
        else if (Silence <= 0.01f && breath.isPlaying) breath.Stop();
    }
}
