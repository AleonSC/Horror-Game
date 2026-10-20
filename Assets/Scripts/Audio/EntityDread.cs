using UnityEngine;

/// <summary>
/// The forest goes quiet when one of them is close. Within silenceDistance of a visible entity, the drone,
/// wind, distant noises and even your own footsteps fade almost to nothing (the other audio scripts read
/// Silence), and slow, heavy breathing starts up just behind your head.
/// DarkForestGenerator adds this automatically.
/// </summary>
public class EntityDread : MonoBehaviour
{
    /// <summary>0 normally, 1 when an entity is right on you. Other sounds duck by this.</summary>
    public static float Silence { get; private set; }

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
    }

    void OnDestroy() => Silence = 0f;

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 listener = cam.transform.position;

        float nearest = float.MaxValue;
        foreach (var entity in ShadowEntity.All)
            if (entity != null && entity.IsVisible)
                nearest = Mathf.Min(nearest, Vector3.Distance(entity.transform.position, listener));

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
