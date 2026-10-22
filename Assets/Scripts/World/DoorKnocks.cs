using UnityEngine;

/// <summary>
/// Now and then, when you shut a door, something answers it: a moment later, three slow knocks on wood,
/// from somewhere off in the trees where there's no door. Not every time (chance, and a long wait before it
/// can happen again). ShackDoor calls DoorClosed.
/// </summary>
public class DoorKnocks : MonoBehaviour
{
    /// <summary>Chance that shutting a door gets an answer.</summary>
    public static float chance = 0.3f;
    /// <summary>Seconds before it can happen again.</summary>
    public static float cooldown = 120f;
    static float nextAllowed;
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() => nextAllowed = 0f;

    Vector3 from;
    float delay;

    /// <summary>A door's just been shut by the player at this spot.</summary>
    public static void DoorClosed(Vector3 at)
    {
        if (Time.time < nextAllowed || MainMenu.Showing || GameOverScreen.IsActive || PortalDoor.PlayerInRoom) return;
        if (Random.value > chance) return;
        nextAllowed = Time.time + cooldown;
        // Somewhere off in the dark, a good way off: not where you can see.
        var cam = Camera.main;
        Vector3 forward = cam != null ? cam.transform.forward : Vector3.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
        Vector3 dir = Quaternion.Euler(0f, Random.Range(100f, 260f), 0f) * forward; // behind you, mostly
        var go = new GameObject("Door Knocks");
        var knocks = go.AddComponent<DoorKnocks>();
        knocks.from = at + dir * Random.Range(30f, 55f) + Vector3.up * 1.2f;
        knocks.delay = Random.Range(1.8f, 4f);
    }

    void Update()
    {
        delay -= Time.deltaTime;
        if (delay > 0f) return;
        if (!GameOverScreen.IsActive)
            ProceduralAudio.PlayAt(ProceduralAudio.Knocks, from, 0.9f * (1f - PortalDoor.RoomQuiet), Random.Range(0.92f, 1.04f), 18f);
        Destroy(gameObject);
    }
}
