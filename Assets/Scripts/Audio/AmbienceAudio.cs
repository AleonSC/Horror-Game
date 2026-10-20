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
    [Header("Volume")]
    [Range(0f, 1f)] public float droneVolume = 0.3f;
    [Range(0f, 1f)] public float tensionVolume = 0.28f;
    [Range(0f, 1f)] public float randomNoiseVolume = 0.7f;

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
    float noiseTimer;

    void Start()
    {
        drone = StartLoop(droneClip != null ? droneClip : ProceduralAudio.Drone, droneVolume);
        tension = StartLoop(tensionClip != null ? tensionClip : ProceduralAudio.Tension, 0f);

        if (forestReverb)
        {
            var reverb = gameObject.AddComponent<AudioReverbZone>();
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
        float quiet = 1f - 0.9f * EntityDread.Silence;
        tension.volume = Mathf.MoveTowards(tension.volume, closeness * tensionVolume * quiet, Time.deltaTime * 0.5f);
        drone.pitch = Mathf.Lerp(1f, 0.92f, closeness);
        drone.volume = droneVolume * quiet;

        noiseTimer -= Time.deltaTime;
        if (noiseTimer <= 0f && EntityDread.Silence > 0.3f) noiseTimer = 2f; // nothing stirs while it's close
        if (noiseTimer <= 0f)
        {
            noiseTimer = Random.Range(minNoiseInterval, maxNoiseInterval);
            PlayRandomNoise(listener);
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
