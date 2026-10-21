using UnityEngine;

/// <summary>
/// A random event: a tree near the player snaps at the base and comes crashing down (TreeFall does the
/// falling and splintering). The first falls about firstFallTime seconds into play, then another every
/// minInterval to maxInterval seconds, roughly away from the player, and only a tree in plain view (in
/// front of them, nothing in the way): if there isn't one it waits and tries again shortly. Call
/// Trigger() to set one off any time. Trees with an owl perched in them are left standing.
/// DarkForestGenerator adds this automatically.
/// </summary>
public class FallingTreeEvent : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Save Tuning on the component's menu)

    [Tooltip("Seconds into play before the first tree falls. Set below 0 to only fall when Trigger() is called.")]
    public float firstFallTime = 30f;
    [Tooltip("After that, another tree falls every this many seconds (picked at random between the two).")]
    public float minInterval = 45f;
    public float maxInterval = 90f;
    public float minDistance = 12f;
    public float maxDistance = 25f;
    [Tooltip("Prefer trees within this many degrees of where the player is looking, so they see it.")]
    public float viewAngle = 60f;
    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("How many shards of wood fly off when it splinters.")]
    public int shardCount = 14;
    [Tooltip("The pieces are frozen in place after this many seconds, even if they're still rocking.")]
    public float settleTime = 10f;

    DarkForestGenerator generator;
    FirstPersonController player;
    float clock, nextFall = -1f;

    void Update()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<FirstPersonController>();
            generator = FindAnyObjectByType<DarkForestGenerator>();
            if (player == null) return;
        }

        if (firstFallTime < 0f) return;
        if (nextFall < 0f) nextFall = firstFallTime;
        clock += Time.deltaTime;
        if (clock >= nextFall)
        {
            clock = 0f;
            // No suitable tree right now: try again shortly. Otherwise wait for the next one.
            nextFall = Trigger() ? Random.Range(minInterval, maxInterval) : 10f;
        }
    }

    /// <summary>Makes a tree near the player fall now. Returns false if there's no suitable tree.</summary>
    public bool Trigger()
    {
        if (generator == null || player == null) return false;
        Transform pivot = PickTree();
        if (pivot == null) return false;

        // Fall roughly away from the player, so it's a scare rather than a death.
        Vector3 away = pivot.position - player.transform.position;
        away.y = 0f;
        Vector3 fallDir = Quaternion.Euler(0f, Random.Range(-60f, 60f), 0f) * away.normalized;
        return TreeFall.Fell(pivot, fallDir, 0.7f, 0.25f, volume, shardCount, settleTime) != null;
    }

    Transform PickTree()
    {
        Vector3 eye = player.transform.position;
        Vector3 look = Camera.main != null ? Camera.main.transform.forward : player.transform.forward;
        look.y = 0f;

        Transform best = null;
        float bestScore = float.MaxValue;
        foreach (var pivot in generator.SwayPivots)
        {
            if (!TreeFall.CanFell(pivot)) continue;
            if (pivot.Find("Owl Perch") != null) continue; // leave the owls be

            Vector3 to = pivot.position - eye;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist < minDistance || dist > maxDistance) continue;

            // Only trees in plain view: in front of the player, with nothing in the way. (Out of sight, all
            // they'd get is the crack, with nothing to see.) Then the most central, with a bit of randomness.
            float offView = Vector3.Angle(look, to);
            if (offView > viewAngle || !InSight(eye, pivot)) continue;
            float score = offView + Random.Range(0f, 20f);
            if (score < bestScore)
            {
                bestScore = score;
                best = pivot;
            }
        }
        return best;
    }

    /// <summary>True if the player can see the trunk: a clear line to it, part way up.</summary>
    static bool InSight(Vector3 eye, Transform pivot)
    {
        Vector3 from = eye + Vector3.up * 1.6f;
        Vector3 to = pivot.position + Vector3.up * 2.5f;
        Vector3 d = to - from;
        float dist = d.magnitude;
        if (!Physics.Raycast(from, d / dist, out var hit, dist, ~0, QueryTriggerInteraction.Ignore)) return true;
        return hit.collider.transform == pivot.parent; // the first thing in the way is the tree itself
    }

}
