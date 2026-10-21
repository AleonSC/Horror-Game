using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Leave the flashlight on and moths find it. Over about a minute of it being on they gather, a few at
/// first, then a fluttering cloud round the beam just in front of you; once they've all come they start
/// to wear the battery down, a little at a time. Switch the light off and they drift away over five seconds;
/// switch it back on before they've gone and they're straight back. Once they've gone, they have to find
/// it all over again. Pale, papery things, lit by the beam itself; you hear them too. LampSpawner adds this
/// to the player.
/// </summary>
public class FlashlightMoths : MonoBehaviour
{
    void Awake() => Tuning.Apply(this);

    [Tooltip("Seconds of the light being on for all of them to gather.")]
    public float gatherTime = 60f;
    [Tooltip("Seconds off for them to drift away.")]
    public float disperseTime = 5f;
    [Tooltip("Once they've all gathered: battery lost per tick, and seconds between ticks.")]
    public float drainPerTick = 0.01f, tickEvery = 3f;
    public int maxMoths = 36;
    [Range(0f, 1f)] public float volume = 0.25f;

    class Moth
    {
        public Transform t;
        public Transform[] wings = new Transform[2];
        public float seed, flapRate, joinAt;
        public Vector3 orbit;
    }

    readonly List<Moth> moths = new List<Moth>();
    Flashlight flashlight;
    Hotbar hotbar;
    AudioSource flutter;
    float gathered, offFor, tick;
    bool told;

    void Start()
    {
        flashlight = GetComponentInChildren<Flashlight>();
        if (flashlight == null) flashlight = FindAnyObjectByType<Flashlight>();
        hotbar = GetComponent<Hotbar>();
        flutter = ProceduralAudio.AddSource(gameObject, ProceduralAudio.MothFlutter, true, false, 0f);

        var body = MaterialFactory.Lit(new Color(0.45f, 0.4f, 0.32f), 0.05f);
        var wing = MaterialFactory.Lit(new Color(0.72f, 0.68f, 0.58f), 0.05f);
        var root = new GameObject("Moths").transform;
        for (int i = 0; i < maxMoths; i++)
        {
            var m = new Moth { seed = Random.Range(0f, 100f), flapRate = Random.Range(18f, 30f), joinAt = (i + Random.value) / maxMoths };
            m.t = new GameObject("Moth").transform;
            m.t.SetParent(root, false);
            Part(PrimitiveType.Capsule, body, m.t, Vector3.zero, new Vector3(0.006f, 0.011f, 0.006f), Quaternion.Euler(90f, 0f, 0f));
            for (int side = 0; side < 2; side++)
            {
                var hinge = new GameObject("Wing").transform;
                hinge.SetParent(m.t, false);
                Part(PrimitiveType.Quad, wing, hinge, new Vector3((side == 0 ? -1f : 1f) * 0.011f, 0f, 0f), new Vector3(0.022f, 0.016f, 1f), Quaternion.Euler(90f, 0f, 0f));
                m.wings[side] = hinge;
            }
            m.t.gameObject.SetActive(false);
            moths.Add(m);
        }
    }

    static void Part(PrimitiveType type, Material mat, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    void Update()
    {
        if (flashlight == null || PauseMenu.IsPaused) return;
        float dt = Time.deltaTime;
        bool on = flashlight.IsOn && !PortalDoor.PlayerInRoom;

        if (on)
        {
            offFor = 0f;
            gathered = Mathf.Min(1f, gathered + dt / Mathf.Max(gatherTime * Difficulty.MothGather, 1f));
            // All here: they wear the battery down, a tick at a time.
            if (gathered >= 1f)
            {
                if (!told && hotbar != null) { told = true; hotbar.Notify("Moths are swarming your light. Turn it off a moment to shake them."); }
                tick += dt;
                if (tick >= tickEvery) { tick = 0f; flashlight.Drain(drainPerTick); }
            }
        }
        else
        {
            tick = 0f;
            offFor += dt;
            if (offFor >= disperseTime) gathered = 0f; // gone: they'll have to find it again
        }

        // How many are here right now: as gathered, thinning away over the seconds it's off.
        float present = gathered * (on ? 1f : Mathf.Clamp01(1f - offFor / Mathf.Max(disperseTime, 0.1f)));
        UpdateSwarm(present, on);

        float target = volume * Mathf.Sqrt(present) * (1f - EntityDread.Hush);
        flutter.volume = Mathf.MoveTowards(flutter.volume, target, dt * 0.5f);
        if (flutter.volume > 0.001f && !flutter.isPlaying) flutter.Play();
        else if (flutter.volume <= 0.001f && flutter.isPlaying) flutter.Stop();
    }

    /// <summary>The moths in front of you: a cloud weaving round the beam, the drifting ones flying off.</summary>
    void UpdateSwarm(float present, bool on)
    {
        var cam = Camera.main;
        if (cam == null) return;
        Transform beam = flashlight.flashlight != null ? flashlight.flashlight.transform : cam.transform;
        float t = Time.time;
        foreach (var m in moths)
        {
            bool here = m.joinAt < present;
            if (m.t.gameObject.activeSelf != here) m.t.gameObject.SetActive(here);
            if (!here) continue;

            // Weaving through the beam a metre or two ahead of the lens, darting about.
            float along = 0.6f + 1.4f * Mathf.PerlinNoise(m.seed, t * 0.3f);
            float spread = 0.15f + 0.35f * (along - 0.6f);
            Vector3 wander = new Vector3(Mathf.PerlinNoise(m.seed + 3f, t * 0.9f) - 0.5f, Mathf.PerlinNoise(m.seed + 7f, t * 0.9f) - 0.5f, 0f) * 2f * spread;
            Vector3 dart = new Vector3(Mathf.Sin(t * 7f + m.seed), Mathf.Cos(t * 6.3f + m.seed * 1.7f), 0f) * 0.03f;
            Vector3 target = beam.position + beam.rotation * (Vector3.forward * along + wander + dart);
            // Drifting away: off out of where the beam was, slowly.
            if (!on) target += (beam.rotation * new Vector3(m.seed % 2f - 1f, 0.6f, 0.5f)) * (1.5f * Mathf.Clamp01(1f - present));
            m.t.position = Vector3.Lerp(m.t.position == Vector3.zero ? target : m.t.position, target, 1f - Mathf.Exp(-8f * Time.deltaTime));
            m.t.rotation = Quaternion.LookRotation(target - m.t.position + beam.forward * 0.01f, Vector3.up);
            float flap = Mathf.Sin(t * m.flapRate + m.seed) * 60f;
            m.wings[0].localRotation = Quaternion.Euler(0f, 0f, flap);
            m.wings[1].localRotation = Quaternion.Euler(0f, 0f, -flap);
        }
    }
}
