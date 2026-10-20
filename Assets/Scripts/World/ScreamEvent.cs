using UnityEngine;

/// <summary>
/// A one-off scare: an anguished scream rings out from somewhere in the trees, in a random direction,
/// right as the last intro line fades out (however long the intro is). Runs with no intro (restarts)
/// get it screamTime seconds in instead. DarkForestGenerator adds this; GameOverScreen resets it.
/// </summary>
public class ScreamEvent : MonoBehaviour
{
    public static ScreamEvent Instance { get; private set; }

    [Tooltip("Seconds into the run before the scream, when there's no intro to wait for.")]
    public float screamTime = 50f;
    [Tooltip("How far away it sounds.")]
    public float minDistance = 25f;
    public float maxDistance = 40f;
    [Range(0f, 1f)] public float volume = 0.35f;
    [Tooltip("Leave empty to use the built-in synthesized scream.")]
    public AudioClip screamClip;

    float clock;
    bool done, sawIntro;
    Transform player;

    void Awake() => Instance = this;

    // Build the scream (with its reverb) up front, so it doesn't hitch the game the moment it's needed.
    void Start()
    {
        if (screamClip == null) _ = ProceduralAudio.Scream;
    }

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
        if (IntroText.Playing) { sawIntro = true; return; }
        if ((!sawIntro && clock < screamTime) || GameOverScreen.IsActive) return;

        done = true;
        Vector2 dir = Random.insideUnitCircle.normalized;
        Vector3 from = player.position + new Vector3(dir.x, 0f, dir.y) * Random.Range(minDistance, maxDistance) + Vector3.up * 1.6f;
        // Distant and muffled by the trees rather than loud: it falls off with distance like any sound.
        ProceduralAudio.PlayAt(screamClip != null ? screamClip : ProceduralAudio.Scream, from, volume, Random.Range(0.88f, 0.98f), 10f);
    }

    /// <summary>Lets the scream happen again in a new run.</summary>
    public void ResetEvent()
    {
        clock = 0f;
        done = false;
        sawIntro = false;
    }
}
