using UnityEngine;

/// <summary>
/// Background audio for the forest: a dull droning bed, a dissonant tension layer
/// that swells when an entity is close, and random noises (twig snaps, distant moans, rustling)
/// from somewhere out in the dark. Wind sounds live in WeatherSystem.
/// DarkForestGenerator adds this automatically.
/// Leave the clip slots empty to use the built-in synthesized sounds.
/// </summary>
public class AmbienceAudio : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Save Tuning on the component's menu)

    [Header("Volume")]
    [Range(0f, 1f)] public float droneVolume = 0.3f;
    [Range(0f, 1f)] public float tensionVolume = 0.28f;
    [Range(0f, 1f)] public float randomNoiseVolume = 0.7f;

    [Range(0f, 1f)] public float chorusVolume = 0.14f;

    [Header("Night Chorus")]
    [Tooltip("Crickets and frogs fall silent this close to the blind one...")]
    public float chorusSilentDistance = 30f;
    [Tooltip("...and are back to full voice this far from it.")]
    public float chorusFullDistance = 50f;

    [Header("Random Noises")]
    public float minNoiseInterval = 6f;
    public float maxNoiseInterval = 16f;
    public float minNoiseDistance = 8f;
    public float maxNoiseDistance = 30f;

    [Header("Tension")]
    [Tooltip("Visible entities closer than this make the tension layer swell and the drone sag in pitch.")]
    public float tensionDistance = 20f;

    [Header("Reverb")]
    public bool forestReverb = true;

    [Header("Clip Overrides (optional)")]
    public AudioClip droneClip;
    public AudioClip tensionClip;
    public AudioClip[] randomNoiseClips;

    AudioSource drone, tension;
    // The chorus all round you: a few voices, each out in its own direction, each falling quiet on its own
    // as the blind one comes near it, so the silence spreads through the woods ahead of it.
    readonly AudioSource[] chorus = new AudioSource[4];
    readonly float[] chorusLevel = { 1f, 1f, 1f, 1f };
    AudioReverbZone reverb;
    float noiseTimer;

    void Start()
    {
        drone = StartLoop(droneClip != null ? droneClip : ProceduralAudio.Drone, droneVolume);
        tension = StartLoop(tensionClip != null ? tensionClip : ProceduralAudio.Tension, 0f);
        for (int i = 0; i < chorus.Length; i++)
        {
            var voice = new GameObject("Night Chorus");
            voice.transform.SetParent(transform, false);
            chorus[i] = ProceduralAudio.AddSource(voice, ProceduralAudio.NightChorus, true, true, 0f, 6f, 60f);
            chorus[i].pitch = Random.Range(0.94f, 1.06f);
            chorus[i].time = Random.Range(0f, chorus[i].clip.length * 0.95f);
            chorus[i].Play();
        }

        if (forestReverb)
        {
            reverb = new GameObject("Reverb").AddComponent<AudioReverbZone>();
            reverb.transform.SetParent(transform, false);
            reverb.reverbPreset = AudioReverbPreset.Forest;
            reverb.minDistance = 500f;
            reverb.maxDistance = 1000f;
        }

        noiseTimer = Random.Range(minNoiseInterval, maxNoiseInterval);
    }

    AudioSource StartLoop(AudioClip clip, float volume)
    {
        var src = ProceduralAudio.AddSource(gameObject, clip, true, false, volume);
        src.time = Random.Range(0f, clip.length * 0.95f); // don't start every layer at the same point
        src.Play();
        return src;
    }

    void Update()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 listener = cam.transform.position;

        // Swell the tension layer as the nearest visible entity gets closer.
        float nearest = float.MaxValue;
        foreach (var entity in ShadowEntity.All)
            if (entity.IsVisible)
                nearest = Mathf.Min(nearest, Vector3.Distance(entity.transform.position, listener));

        float closeness = Mathf.Clamp01(Mathf.InverseLerp(tensionDistance, 3f, nearest));
        // Right up close it all drops away (EntityDread): the silence is the warning.
        // ...and in the dark room behind the lone door, the whole forest fades away.
        float quiet = (1f - 0.9f * EntityDread.Silence) * (1f - Shelter.OutsideQuiet);
        if (reverb != null)
        {
            // In the room: a vast, echoing hall. Out here: the woods.
            reverb.transform.position = listener;
            var preset = PortalDoor.PlayerInRoom ? AudioReverbPreset.Hangar : AudioReverbPreset.Forest;
            if (reverb.reverbPreset != preset) reverb.reverbPreset = preset;
        }
        tension.volume = Mathf.MoveTowards(tension.volume, closeness * tensionVolume * quiet, Time.deltaTime * 0.5f);
        drone.pitch = Mathf.Lerp(1f, 0.92f, closeness);
        drone.volume = droneVolume * quiet;

        UpdateChorus(listener, quiet);

        noiseTimer -= Time.deltaTime;
        if (noiseTimer <= 0f && (EntityDread.Silence > 0.3f || Shelter.OutsideQuiet > 0.05f)) noiseTimer = 2f; // nothing stirs while it's close, or in the room
        if (noiseTimer <= 0f)
        {
            noiseTimer = Random.Range(minNoiseInterval, maxNoiseInterval);
            PlayRandomNoise(listener);
        }
    }

    void UpdateChorus(Vector3 listener, float quiet)
    {
        for (int i = 0; i < chorus.Length; i++)
        {
            if (chorus[i] == null) continue;
            float a = (i + 0.5f) / chorus.Length * Mathf.PI * 2f;
            Vector3 at = listener + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 14f;
            chorus[i].transform.position = at;

            // Near the blind one, everything goes still: quickly as it comes, slowly back once it's gone.
            float nearest = float.MaxValue;
            foreach (var blind in BlindEntity.All)
                if (blind != null && blind.isActiveAndEnabled)
                    nearest = Mathf.Min(nearest, Vector3.Distance(WorldWrap.NearestImage(blind.transform.position, at), at));
            float target = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(chorusSilentDistance, chorusFullDistance, nearest));
            chorusLevel[i] = Mathf.MoveTowards(chorusLevel[i], target, Time.deltaTime / (target < chorusLevel[i] ? 1.5f : 10f));
            chorus[i].volume = chorusVolume * chorusLevel[i] * quiet;
        }
    }

    void PlayRandomNoise(Vector3 listener)
    {
        AudioClip clip;
        if (randomNoiseClips != null && randomNoiseClips.Length > 0)
        {
            clip = randomNoiseClips[Random.Range(0, randomNoiseClips.Length)];
        }
        else
        {
            float roll = Random.value;
            clip = roll < 0.5f ? ProceduralAudio.TwigSnap : roll < 0.75f ? ProceduralAudio.Moan : ProceduralAudio.Creep;
        }

        Vector2 dir = Random.insideUnitCircle.normalized;
        Vector3 pos = listener + new Vector3(dir.x, 0f, dir.y) * Random.Range(minNoiseDistance, maxNoiseDistance);
        ProceduralAudio.PlayAt(clip, pos, randomNoiseVolume, Random.Range(0.85f, 1.15f));
    }
}
