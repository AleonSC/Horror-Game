using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Inside somewhere: a shack, the cabin, a tent, or sitting in the car. The world outside drowns out:
/// the wind, rain, thunder, the forest's ambience and the giant fade to silence (OutsideQuiet, which those
/// sounds go by, as they do in the dark room), and everything else out there is muffled to a dull thump
/// (a low-pass filter on what you hear). Your own sounds, and those right there inside with you (a torch
/// on the wall, the lantern, a radio), stay clear. DarkForestGenerator adds this.
/// </summary>
public class Shelter : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Inspector changes made in Play are kept)

    /// <summary>0 outside, 1 inside a shelter (eased in and out).</summary>
    public static float Quiet { get; private set; }
    /// <summary>How far outside sounds should fade: inside a shelter, or in the dark room.</summary>
    public static float OutsideQuiet => Mathf.Max(Quiet, PortalDoor.RoomQuiet);
    /// <summary>True while the player's inside one.</summary>
    public static bool Inside { get; private set; }
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() { Quiet = 0f; Inside = false; }

    [Tooltip("Seconds for the outside to fade away as you go in...")]
    public float fadeIn = 0.6f;
    [Tooltip("...and to come back as you go out.")]
    public float fadeOut = 0.9f;
    [Tooltip("How muffled what's left outside is, inside (the low-pass cutoff, Hz: lower is duller).")]
    public float muffledCutoff = 380f;
    [Tooltip("Sounds within this far of you while you're inside are in there with you, and stay clear (metres).")]
    public float insideReach = 4f;

    FirstPersonController player;
    AudioLowPassFilter muffle;
    float scan;
    readonly List<AudioSource> sources = new List<AudioSource>();

    void Update()
    {
        if (player == null) player = FindAnyObjectByType<FirstPersonController>();
        Vector3 head = Camera.main != null ? Camera.main.transform.position : player != null ? player.transform.position + Vector3.up * 1.6f : Vector3.zero;
        Inside = player != null && IsInside(head);
        float wanted = Inside ? 1f : 0f;
        Quiet = Mathf.MoveTowards(Quiet, wanted, Time.unscaledDeltaTime / Mathf.Max(Inside ? fadeIn : fadeOut, 0.01f));

        // The muffle, on whatever's listening.
        var listener = FindListener();
        if (listener != null && (muffle == null || muffle.gameObject != listener.gameObject))
        {
            muffle = listener.GetComponent<AudioLowPassFilter>();
            if (muffle == null) muffle = listener.gameObject.AddComponent<AudioLowPassFilter>();
            muffle.lowpassResonanceQ = 1f;
        }
        if (muffle != null)
        {
            // Eased on a log scale, so it closes in smoothly rather than all at the end.
            float cutoff = Mathf.Exp(Mathf.Lerp(Mathf.Log(22000f), Mathf.Log(Mathf.Max(muffledCutoff, 50f)), Quiet));
            muffle.cutoffFrequency = cutoff;
            muffle.enabled = Quiet > 0.001f;
        }

        // About once a second (and right away on the way in): which sounds stay clear of the muffle.
        scan -= Time.unscaledDeltaTime;
        if (scan <= 0f || (Inside && Quiet < 0.05f))
        {
            scan = 1f;
            sources.Clear();
            sources.AddRange(FindObjectsByType<AudioSource>(FindObjectsSortMode.None));
            Transform you = player != null ? player.transform : null;
            foreach (var s in sources)
            {
                if (s == null) continue;
                bool flat = s.spatialBlend < 0.5f;                                   // music, the UI, your breathing
                bool yours = you != null && s.transform.IsChildOf(you);              // footsteps, the flashlight
                bool withYou = Inside && (s.transform.position - head).sqrMagnitude < insideReach * insideReach; // in here too
                s.bypassListenerEffects = flat || yours || withYou;
            }
        }
    }

    static AudioListener FindListener()
    {
        var cam = Camera.main;
        if (cam != null)
        {
            var l = cam.GetComponent<AudioListener>();
            if (l != null && l.enabled) return l;
        }
        return FindAnyObjectByType<AudioListener>();
    }

    /// <summary>True if a point (your head) is inside a shack, the cabin, a tent, or you're sitting in the car.</summary>
    bool IsInside(Vector3 head)
    {
        if (player != null && player.Seated) return true;
        foreach (var shack in Shack.All)
            if (shack != null && shack.Interior(head)) return true;
        foreach (var camp in Campsite.All)
            if (camp != null && camp.InTent(head)) return true;
        return false;
    }
}
