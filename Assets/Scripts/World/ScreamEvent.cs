using UnityEngine;

/// <summary>
/// A one-off scare: a while into each run, a loud, anguished scream rings out from somewhere
/// in the trees, in a random direction. DarkForestGenerator adds this; GameOverScreen resets it.
/// </summary>
public class ScreamEvent : MonoBehaviour
{
    public static ScreamEvent Instance { get; private set; }

    [Tooltip("Seconds into the run before the scream.")]
    public float screamTime = 50f;
    [Tooltip("How far away it sounds.")]
    public float minDistance = 25f;
    public float maxDistance = 40f;
    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("Leave empty to use the built-in synthesized scream.")]
    public AudioClip screamClip;

    float clock;
    bool done;
    Transform player;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        if (done) return;
        if (player == null)
        {
            var controller = FindAnyObjectByType<FirstPersonController>();
            if (controller == null) return;
            player = controller.transform;
        }

        clock += Time.deltaTime; // stops while paused
        if (clock < screamTime || GameOverScreen.IsActive) return;

        done = true;
        Vector2 dir = Random.insideUnitCircle.normalized;
        Vector3 from = player.position + new Vector3(dir.x, 0f, dir.y) * Random.Range(minDistance, maxDistance) + Vector3.up * 1.6f;
        // A large minimum distance keeps it loud even though it's far off, while still clearly coming from one side.
        ProceduralAudio.PlayAt(screamClip != null ? screamClip : ProceduralAudio.Scream, from, volume, Random.Range(0.92f, 1.05f), 30f);
    }

    /// <summary>Lets the scream happen again in a new run.</summary>
    public void ResetEvent()
    {
        clock = 0f;
        done = false;
    }
}
