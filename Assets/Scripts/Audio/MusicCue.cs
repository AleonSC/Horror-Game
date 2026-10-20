using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Plays a procedurally synthesized music cue (an entity's theme). The samples are generated on a
/// background thread (Prepare, ahead of time, so there's no hitch when it's needed); the clip is made on
/// the main thread once they're ready. Play starts it from the top, in step with the moment it was asked
/// for even if it wasn't ready yet. While a cue plays the rest of the soundtrack ducks out (MusicManager
/// reads Duck), letting it back in over the cue's last few seconds. Volume follows the music volume in
/// the pause menu. One per cue name: BlindEntityTheme and FlierTheme use these.
/// </summary>
public class MusicCue : MonoBehaviour
{
    const int Rate = 44100;

    [Range(0f, 1f)] public float volume = 0.9f;
    [Tooltip("Seconds to fade out when it's stopped early (the run resets).")]
    public float fadeOutTime = 2f;

    static readonly Dictionary<string, MusicCue> cues = new Dictionary<string, MusicCue>();

    // Static state is cleared when Play starts, so nothing carries over from the last session if the
    // editor's Enter Play Mode settings skip reloading scripts (no domain reload).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => cues.Clear();

    /// <summary>How far the playing cues are ducking the soundtrack, 0 to 1.</summary>
    public static float Duck
    {
        get
        {
            float d = 0f;
            foreach (var cue in cues.Values) if (cue != null) d = Mathf.Max(d, cue.duck);
            return d;
        }
    }

    string id;
    System.Func<float[]> synthesize;
    float[] data;
    Task generating;
    AudioClip clip;
    AudioSource source;
    float requestedAt = -1f, fade = 1f, duck;
    bool stopping;

    /// <summary>The cue with this name, made if it doesn't exist yet (synthesize makes its samples, mono, 44.1kHz).</summary>
    public static MusicCue Get(string id, System.Func<float[]> synthesize, float volume = 0.9f)
    {
        if (cues.TryGetValue(id, out var existing) && existing != null) return existing;
        var cue = new GameObject(id).AddComponent<MusicCue>();
        cue.id = id;
        cue.synthesize = synthesize;
        cue.volume = volume;
        cues[id] = cue;
        return cue;
    }

    /// <summary>Starts generating it in the background, if it hasn't been already.</summary>
    public void Prepare()
    {
        if (generating != null || data != null) return;
        var make = synthesize;
        generating = Task.Run(() => data = make());
    }

    /// <summary>Plays it from the top (as soon as it's ready, in step with the moment it was asked for).</summary>
    public void Play()
    {
        Prepare();
        requestedAt = Time.time;
        stopping = false;
        fade = 1f;
        if (source.isPlaying) source.Stop();
    }

    /// <summary>Fades it out (its entity is gone).</summary>
    public void FadeOut()
    {
        if (source.isPlaying || requestedAt >= 0f) stopping = true;
    }

    void Awake()
    {
        source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        source.ignoreListenerVolume = true; // music: the sound effects slider doesn't touch it
        source.volume = 0f;
    }

    void OnDestroy()
    {
        if (id != null && cues.TryGetValue(id, out var c) && c == this) cues.Remove(id);
    }

    void Update()
    {
        if (clip == null && data != null)
        {
            clip = AudioClip.Create(id, data.Length, 1, Rate, false);
            clip.SetData(data, 0);
        }
        if (requestedAt >= 0f && clip != null && !source.isPlaying)
        {
            float late = Time.time - requestedAt;
            requestedAt = -1f;
            if (late < clip.length - 1f)
            {
                source.clip = clip;
                source.time = late;
                source.Play();
            }
        }

        if (stopping)
        {
            fade = Mathf.MoveTowards(fade, 0f, Time.unscaledDeltaTime / Mathf.Max(fadeOutTime, 0.01f));
            if (fade <= 0f)
            {
                source.Stop();
                stopping = false;
                requestedAt = -1f;
            }
        }

        bool playing = source.isPlaying || requestedAt >= 0f;
        var music = MusicManager.Instance;
        float musicVolume = music != null ? music.Volume : 0.6f;
        source.volume = PauseMenu.MasterVolume * musicVolume * volume * fade * (1f - PortalDoor.RoomQuiet);
        float tail = source.isPlaying && source.clip != null ? Mathf.Clamp01((source.clip.length - source.time) / 6f) : 1f;
        duck = playing ? fade * tail : 0f;
    }
}
