using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A shockwave rolling out across the forest from a point: a ring travelling outward over the ground. As it
/// passes, trees and grass whip over away from it and sway back (WeatherSystem asks LeanAt), the leaves on
/// the ground blow up and away (GroundLeaves asks Front), and when it reaches the player the ground heaves
/// under them. A full-strength one (the giant landing) comes with a rising roar and a deep boom; weaker ones
/// (each of its footsteps) are just the heave and the trees. Several can be rolling out at once.
/// </summary>
public class Shockwave : MonoBehaviour
{
    static readonly List<Shockwave> all = new List<Shockwave>();

    // Static state is cleared when Play starts, so nothing carries over from the last session if the
    // editor's Enter Play Mode settings skip reloading scripts (no domain reload).
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() => all.Clear();

    public Vector3 origin;
    public float speed = 220f;
    [Tooltip("How far the front of the wave is spread (metres).")]
    public float width = 25f;
    [Tooltip("How far trees are thrown over as it passes (degrees), at full strength.")]
    public float treeLean = 22f;
    [Range(0f, 1f), Tooltip("1 for the landing; footsteps send out weaker ones.")]
    public float strength = 1f;

    float radius;
    bool roarStarted, hitPlayer;
    FirstPersonController player;
    AudioSource roar, boom;

    /// <summary>Sends a shockwave out from a point. Strength 1 is the full thing; less is smaller and quiet.</summary>
    public static Shockwave Begin(Vector3 origin, float speed, float strength = 1f)
    {
        var wave = new GameObject(strength >= 0.99f ? "Shockwave" : "Footstep Shockwave").AddComponent<Shockwave>();
        wave.origin = origin;
        wave.speed = speed;
        wave.strength = Mathf.Clamp01(strength);
        wave.width = Mathf.Lerp(14f, 25f, wave.strength);
        all.Add(wave);
        return wave;
    }

    /// <summary>Stops every shockwave in progress (the run resets).</summary>
    public static void Cancel()
    {
        foreach (var wave in all) if (wave != null) Destroy(wave.gameObject);
        all.Clear();
    }

    /// <summary>
    /// How far something standing at a point is being thrown over right now (degrees, all the waves passing
    /// it together), and the horizontal direction away from the strongest. 0 when no wave is there.
    /// </summary>
    public static float LeanAt(Vector3 p, out Vector3 away)
    {
        away = Vector3.forward;
        float total = 0f, strongest = 0f;
        foreach (var wave in all)
        {
            if (wave == null || !wave.enabled) continue;
            float lean = wave.Lean(p, out Vector3 dir);
            total += lean;
            if (Mathf.Abs(lean) > strongest) { strongest = Mathf.Abs(lean); away = dir; }
        }
        return total;
    }

    float Lean(Vector3 p, out Vector3 away)
    {
        Vector3 o = WorldWrap.NearestImage(origin, p);
        Vector3 d = p - o;
        d.y = 0f;
        float dist = d.magnitude;
        away = dist > 0.01f ? d / dist : Vector3.forward;
        float w = width, lean = treeLean * strength;
        float passed = radius - dist; // how far the front has gone beyond this point
        if (passed < -w * 1.5f) return 0f;
        if (passed < 0f) return lean * Mathf.SmoothStep(0f, 1f, 1f + passed / (w * 1.5f)); // the front arriving
        // Whipped over, then swinging back and forth, dying away.
        return lean * Mathf.Cos(passed / (w * 1.2f)) * Mathf.Exp(-passed / (w * 4f));
    }

    /// <summary>True if a wave's front is passing a point right now, with the push away from it and its strength.</summary>
    public static bool Front(Vector3 p, out Vector3 push, out float power)
    {
        push = Vector3.zero;
        power = 0f;
        foreach (var wave in all)
        {
            if (wave == null || !wave.enabled) continue;
            Vector3 o = WorldWrap.NearestImage(wave.origin, p);
            Vector3 d = p - o;
            d.y = 0f;
            float dist = d.magnitude;
            if (Mathf.Abs(wave.radius - dist) > wave.width || wave.strength <= power) continue;
            push = dist > 0.01f ? d / dist : Vector3.forward;
            power = wave.strength;
        }
        return power > 0f;
    }

    void Awake()
    {
        Tuning.Apply(this); // saved tuning (Inspector changes made in Play are kept)
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
        bool full = strength > 0.6f;

        // You hear it coming: a roar building over the last few seconds (the big one only).
        if (full && !roarStarted && toPlayer < speed * 3f)
        {
            roarStarted = true;
            roar.pitch = 0.45f;
            roar.PlayOneShot(ProceduralAudio.DistantRumble, 1f * quiet);
        }
        if (full && roarStarted && !hitPlayer && toPlayer > 0f)
            player.AddCameraShake(Mathf.Lerp(1.2f, 0.1f, toPlayer / (speed * 3f)));

        // It hits.
        if (!hitPlayer && toPlayer <= 0f)
        {
            hitPlayer = true;
            if (full)
            {
                boom.pitch = 0.5f;
                boom.PlayOneShot(ProceduralAudio.Thunder, 1f * quiet);
                boom.PlayOneShot(ProceduralAudio.GiantStep, 1f * quiet);
                LeafBurst.Spawn(new Bounds(player.transform.position + Vector3.up, new Vector3(30f, 2f, 30f)));
            }
            else
            {
                boom.pitch = Random.Range(1.4f, 1.7f);
                boom.PlayOneShot(ProceduralAudio.GiantStep, 0.35f * strength * quiet); // a low thump through the ground
            }
        }
        float after = speed * (full ? 1.5f : 0.6f);
        if (hitPlayer && toPlayer > -after)
            player.AddCameraShake(Mathf.Lerp(6f * strength, 0f, -toPlayer / after));

        // Gone well past: done.
        if (toPlayer < -width * 20f && toPlayer < -speed * 2f)
        {
            all.Remove(this);
            Destroy(gameObject, full ? 8f : 2f); // let the boom ring out
            enabled = false;
        }
    }

    void OnDestroy() => all.Remove(this);
}
