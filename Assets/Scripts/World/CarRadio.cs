using UnityEngine;

/// <summary>
/// An old portable radio on the car's back seat. Click it and it crackles into life, loud: a scratchy waltz
/// on a dying station, carrying through the whole forest. The blind one hears it from anywhere and comes
/// for it, crashing through the trees; when it gets there it smashes it, and that's the end of the radio
/// (until the run restarts). It also gives out by itself after a while. Use it to pull the blind one away
/// from wherever you need to be. LampInteractor clicks it; BrokenCar puts one in.
/// </summary>
public class CarRadio : MonoBehaviour
{
    [Tooltip("Seconds it plays before its batteries die.")]
    public float playTime = 90f;
    [Tooltip("How close the blind one gets before it smashes the radio.")]
    public float smashDistance = 4.5f;
    [Range(0f, 1f)] public float volume = 1f;

    public bool IsOn { get; private set; }
    public bool IsBroken { get; private set; }
    public float Charge { get; private set; } = 1f;

    AudioSource broadcast, sfx;
    Renderer dial;
    Light dialLight;
    Material dialOn, dialOff;
    Transform body;

    public string Prompt =>
        IsBroken ? "The radio's smashed" :
        Charge <= 0f ? "The radio's batteries are dead" :
        IsOn ? "Click to turn off the radio" : "Click to turn on the radio (it's loud)";

    public void Toggle()
    {
        if (IsBroken || Charge <= 0f)
        {
            Play(ProceduralAudio.LampClick, 0.5f, 1.4f); // click... nothing
            return;
        }
        IsOn = !IsOn;
        Play(ProceduralAudio.LampClick, 0.7f, 1.6f);
        if (IsOn)
        {
            broadcast.time = Random.Range(0f, broadcast.clip.length * 0.9f);
            broadcast.Play();
        }
        else broadcast.Stop();
    }

    /// <summary>Whole again, off, fresh batteries (used when the run resets).</summary>
    public void ResetRadio()
    {
        IsOn = false;
        IsBroken = false;
        Charge = 1f;
        if (broadcast != null) broadcast.Stop();
        if (body != null) body.localRotation = Quaternion.identity;
    }

    void Update()
    {
        if (broadcast == null) return;
        bool on = IsOn && !IsBroken;
        if (on)
        {
            Charge -= Time.deltaTime / Mathf.Max(playTime, 0.01f);
            // Fading and warbling as the batteries go.
            broadcast.volume = volume * Mathf.Lerp(0.5f, 1f, Mathf.Clamp01(Charge / 0.3f)) * (1f - PortalDoor.RoomQuiet);
            broadcast.pitch = Charge < 0.15f ? Mathf.Lerp(0.8f, 1f, Charge / 0.15f) : 1f;
            if (Charge <= 0f)
            {
                Charge = 0f;
                IsOn = false;
                broadcast.Stop();
                Play(ProceduralAudio.LampPowerDown, 0.4f, 2f);
            }

            // The blind one comes for it, however far away it is.
            foreach (var blind in BlindEntity.All)
            {
                blind.Lure(transform.position);
                Vector3 d = blind.transform.position - WorldWrap.NearestImage(transform.position, blind.transform.position);
                d.y = 0f;
                if (d.magnitude < smashDistance) Smash();
            }
        }
        if (dial != null) dial.sharedMaterial = on ? dialOn : dialOff;
        if (dialLight != null) dialLight.enabled = on;
    }

    /// <summary>Crushed: a crunch, the sound cut dead, the radio knocked askew.</summary>
    public void Smash()
    {
        if (IsBroken) return;
        IsBroken = true;
        IsOn = false;
        broadcast.Stop();
        Play(ProceduralAudio.TreeSnap, 1f, 1.8f);
        Play(ProceduralAudio.LampSpark, 0.6f, 0.9f);
        if (body != null) body.localRotation = Quaternion.Euler(Random.Range(-25f, 25f), Random.Range(-30f, 30f), Random.Range(60f, 100f));
    }

    void Play(AudioClip clip, float vol, float pitch)
    {
        if (sfx == null || clip == null) return;
        sfx.pitch = pitch;
        sfx.PlayOneShot(clip, vol);
    }

    /// <summary>
    /// A boxy transistor radio (origin at its base): a cabinet with a speaker grille, a tuning dial that glows
    /// when it's on, two knobs, a carry handle and a telescopic aerial.
    /// </summary>
    public void Build()
    {
        var cabinet = MaterialFactory.Lit(new Color(0.22f, 0.13f, 0.07f), 0.35f);
        var face = MaterialFactory.Lit(new Color(0.55f, 0.5f, 0.4f), 0.3f);
        var grille = MaterialFactory.Lit(new Color(0.08f, 0.08f, 0.08f), 0.4f);
        var chrome = MaterialFactory.Lit(new Color(0.45f, 0.45f, 0.45f), 0.75f);
        dialOff = MaterialFactory.Lit(new Color(0.35f, 0.3f, 0.18f), 0.6f);
        dialOn = MaterialFactory.NoFog(new Color(1f, 0.7f, 0.3f) * 1.6f);

        body = new GameObject("Radio Body").transform;
        body.SetParent(transform, false);
        const float w = 0.34f, h = 0.2f, d = 0.1f;
        Part(PrimitiveType.Cube, cabinet, body, new Vector3(0f, h * 0.5f, 0f), new Vector3(w, h, d));
        Part(PrimitiveType.Cube, face, body, new Vector3(0f, h * 0.5f, d * 0.5f + 0.003f), new Vector3(w - 0.03f, h - 0.03f, 0.006f));
        // Speaker grille: horizontal slats.
        for (int i = 0; i < 6; i++)
            Part(PrimitiveType.Cube, grille, body, new Vector3(-0.06f, 0.04f + i * 0.022f, d * 0.5f + 0.007f), new Vector3(0.17f, 0.01f, 0.004f));
        var dialPart = Part(PrimitiveType.Cube, dialOff, body, new Vector3(0.1f, 0.14f, d * 0.5f + 0.007f), new Vector3(0.1f, 0.035f, 0.004f));
        dial = dialPart.GetComponent<Renderer>();
        foreach (float y in new[] { 0.06f, 0.095f })
        {
            var knob = Part(PrimitiveType.Cylinder, chrome, body, new Vector3(0.1f, y, d * 0.5f + 0.012f), new Vector3(0.028f, 0.008f, 0.028f));
            knob.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }
        // Carry handle and aerial.
        foreach (float x in new[] { -0.12f, 0.12f })
            Part(PrimitiveType.Cube, chrome, body, new Vector3(x, h + 0.02f, 0f), new Vector3(0.012f, 0.04f, 0.012f));
        Part(PrimitiveType.Cube, chrome, body, new Vector3(0f, h + 0.04f, 0f), new Vector3(0.25f, 0.012f, 0.012f));
        var aerial = Part(PrimitiveType.Cylinder, chrome, body, new Vector3(-0.15f, h + 0.18f, 0f), new Vector3(0.006f, 0.18f, 0.006f));
        aerial.localRotation = Quaternion.Euler(0f, 0f, 25f);

        dialLight = new GameObject("Dial Glow").AddComponent<Light>();
        dialLight.transform.SetParent(body, false);
        dialLight.transform.localPosition = new Vector3(0.1f, 0.14f, 0.2f);
        dialLight.type = LightType.Point;
        dialLight.color = new Color(1f, 0.7f, 0.35f);
        dialLight.range = 1.2f;
        dialLight.intensity = 0.8f;
        dialLight.shadows = LightShadows.None;
        dialLight.enabled = false;

        // Something to click.
        var aim = gameObject.AddComponent<BoxCollider>();
        aim.isTrigger = true;
        aim.center = new Vector3(0f, 0.12f, 0f);
        aim.size = new Vector3(0.5f, 0.35f, 0.3f);

        // Loud, and it carries a long way.
        broadcast = ProceduralAudio.AddSource(gameObject, ProceduralAudio.RadioBroadcast, true, true, volume, 4f, 160f);
        broadcast.rolloffMode = AudioRolloffMode.Linear;
        sfx = ProceduralAudio.AddSource(gameObject, null, false, true, 0.8f, 2f, 25f);
    }

    static Transform Part(PrimitiveType type, Material mat, Transform parent, Vector3 pos, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        var col = go.GetComponent<Collider>();
        if (Application.isPlaying) Destroy(col);
        else DestroyImmediate(col);
        return go.transform;
    }
}
