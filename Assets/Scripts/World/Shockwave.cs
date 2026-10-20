using UnityEngine;

/// <summary>
/// A shockwave rolling out across the forest from a point (the giant landing): a ring travelling outward
/// over the ground. As it passes, trees and grass whip over away from it and sway back (WeatherSystem asks
/// LeanAt), the leaves on the ground blow up and away (GroundLeaves asks Front), and when it reaches the
/// player there's a rising roar, a deep boom and a violent shake. Only one at a time.
/// </summary>
public class Shockwave : MonoBehaviour
{
    static Shockwave current;

    public Vector3 origin;
    public float speed = 220f;
    [Tooltip("How far the front of the wave is spread (metres).")]
    public float width = 25f;
    [Tooltip("How far trees are thrown over as it passes (degrees).")]
    public float treeLean = 22f;

    float radius;
    bool roarStarted, hitPlayer;
    FirstPersonController player;
    AudioSource roar, boom;

    /// <summary>Sends a shockwave out from a point.</summary>
    public static void Begin(Vector3 origin, float speed)
    {
        Cancel();
        current = new GameObject("Shockwave").AddComponent<Shockwave>();
        current.origin = origin;
        current.speed = speed;
    }

    /// <summary>Stops any shockwave in progress (the run resets).</summary>
    public static void Cancel()
    {
        if (current != null) Destroy(current.gameObject);
        current = null;
    }

    /// <summary>
    /// How far something standing at a point is being thrown over right now (degrees), and the horizontal
    /// direction away from the wave. 0 when the wave isn't there.
    /// </summary>
    public static float LeanAt(Vector3 p, out Vector3 away)
    {
        away = Vector3.forward;
        if (current == null) return 0f;
        Vector3 o = WorldWrap.NearestImage(current.origin, p);
        Vector3 d = p - o;
        d.y = 0f;
        float dist = d.magnitude;
        away = dist > 0.01f ? d / dist : Vector3.forward;
        float w = current.width;
        float passed = current.radius - dist; // how far the front has gone beyond this point
        if (passed < -w * 1.5f) return 0f;
        if (passed < 0f) return current.treeLean * Mathf.SmoothStep(0f, 1f, 1f + passed / (w * 1.5f)); // the front arriving
        // Whipped over, then swinging back and forth, dying away.
        return current.treeLean * Mathf.Cos(passed / (w * 1.2f)) * Mathf.Exp(-passed / (w * 4f));
    }

    /// <summary>True if the front of the wave is passing a point right now, with the push away from it.</summary>
    public static bool Front(Vector3 p, out Vector3 push)
    {
        push = Vector3.zero;
        if (current == null) return false;
        Vector3 o = WorldWrap.NearestImage(current.origin, p);
        Vector3 d = p - o;
        d.y = 0f;
        float dist = d.magnitude;
        if (Mathf.Abs(current.radius - dist) > current.width) return false;
        push = dist > 0.01f ? d / dist : Vector3.forward;
        return true;
    }

    void Awake()
    {
        roar = ProceduralAudio.AddSource(gameObject, null, false, false, 1f);
        boom = ProceduralAudio.AddSource(gameObject, null, false, false, 1f);
    }

    void Update()
    {
        if (player == null) player = FindAnyObjectByType<FirstPersonController>();
        radius += speed * Time.deltaTime;
        if (player == null) return;

        Vector3 o = WorldWrap.NearestImage(origin, player.transform.position);
        Vector3 d = player.transform.position - o;
        d.y = 0f;
        float toPlayer = d.magnitude - radius; // metres until it reaches you
        float quiet = 1f - PortalDoor.RoomQuiet;

        // You hear it coming: a roar building over the last few seconds.
        if (!roarStarted && toPlayer < speed * 3f)
        {
            roarStarted = true;
            roar.pitch = 0.45f;
            roar.PlayOneShot(ProceduralAudio.DistantRumble, 1f * quiet);
        }
        if (roarStarted && !hitPlayer && toPlayer > 0f)
            player.AddCameraShake(Mathf.Lerp(1.2f, 0.1f, toPlayer / (speed * 3f)));

        // It hits.
        if (!hitPlayer && toPlayer <= 0f)
        {
            hitPlayer = true;
            boom.pitch = 0.5f;
            boom.PlayOneShot(ProceduralAudio.Thunder, 1f * quiet);
            boom.PlayOneShot(ProceduralAudio.GiantStep, 1f * quiet);
            LeafBurst.Spawn(new Bounds(player.transform.position + Vector3.up, new Vector3(30f, 2f, 30f)));
        }
        if (hitPlayer && toPlayer > -speed * 1.5f)
            player.AddCameraShake(Mathf.Lerp(6f, 0f, -toPlayer / (speed * 1.5f)));

        // Gone well past: done.
        if (toPlayer < -width * 20f && toPlayer < -speed * 2f)
        {
            if (current == this) current = null;
            Destroy(gameObject, 8f); // let the boom ring out
            enabled = false;
        }
    }

    void OnDestroy()
    {
        if (current == this) current = null;
    }
}
