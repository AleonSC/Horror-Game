using UnityEngine;

/// <summary>
/// The longer you're out there, the more of them there are: one more scour joins the hunt for every minute
/// of the run, up to ten minutes (on top of the one per lamp lit). Run time stops while the game's paused.
/// A restart starts the clock again. DarkForestGenerator adds this.
/// </summary>
public class ScourClock : MonoBehaviour
{
    [Tooltip("Seconds between each extra scour.")]
    public float interval = 60f;
    [Tooltip("How many extra scours the clock brings in, at most (one per interval).")]
    public int maxExtra = 10;

    static float runTime;
    static int added;

    /// <summary>Back to the start of a run (GameOverScreen's restart).</summary>
    public static void ResetRun()
    {
        runTime = 0f;
        added = 0;
    }

    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() => ResetRun();

    void Update()
    {
        if (GameOverScreen.IsActive || added >= maxExtra) return;
        runTime += Time.deltaTime;
        if (runTime < (added + 1) * interval) return;
        added++;
        ShadowEntity.Spawn(Random.Range(2f, 8f)); // comes in out of the dark a few seconds later
    }
}
