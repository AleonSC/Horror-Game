using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A lamp post that starts off. The player clicks it repeatedly to fill a meter (which slowly
/// drains if they stop). A full meter lights the lamp, making a safe zone that entities can't
/// enter or creep into. Every lamp lit also summons another ShadowEntity.
/// LampSpawner places these around the map; LampInteractor handles the clicking and HUD.
/// </summary>
public class Lamp : MonoBehaviour
{
    public static readonly List<Lamp> All = new List<Lamp>();

    [Header("Charging")]
    [Range(0.01f, 1f), Tooltip("How much each click fills the meter.")]
    public float chargePerClick = 0.06f;
    [Tooltip("Seconds without clicking before the meter starts draining.")]
    public float drainDelay = 1.5f;
    [Tooltip("Meter lost per second while draining.")]
    public float drainRate = 0.08f;

    [Header("When Lit")]
    [Tooltip("Radius of the safe zone.")]
    public float safeRadius = 10f;
    public float lightIntensity = 4f;
    public Color lightColor = new Color(1f, 0.75f, 0.45f);
    [Tooltip("Summon another entity each time a lamp is lit.")]
    public bool spawnEntityWhenLit = true;

    [Header("Audio (optional)")]
    public AudioClip clickSound;
    public AudioClip igniteSound;
    public AudioClip humLoop;

    public bool IsLit { get; private set; }
    /// <summary>Meter from 0 to 1.</summary>
    public float Charge { get; private set; }

    public static int LitCount
    {
        get
        {
            int count = 0;
            foreach (var lamp in All) if (lamp.IsLit) count++;
            return count;
        }
    }

    /// <summary>True if the point is inside any lit lamp's safe zone.</summary>
    public static bool IsInSafeZone(Vector3 point)
    {
        foreach (var lamp in All)
        {
            if (!lamp.IsLit) continue;
            Vector3 d = point - lamp.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < lamp.safeRadius * lamp.safeRadius) return true;
        }
        return false;
    }

    Light lampLight;
    Material bulbMat;
    AudioSource hum, sfx;
    float lastClickTime;
    float flash;

    const float PoleHeight = 3f;

    void Awake()
    {
        Build();
        hum = ProceduralAudio.AddSource(gameObject, humLoop != null ? humLoop : ProceduralAudio.LampHum, true, true, 0.35f, 2f, 20f);
        sfx = ProceduralAudio.AddSource(gameObject, null, false, true, 0.8f, 3f, 30f);
        SetBulb(0f);
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    /// <summary>One click's worth of charge. Lights the lamp when the meter is full.</summary>
    public void AddCharge()
    {
        if (IsLit) return;

        Charge = Mathf.Clamp01(Charge + chargePerClick);
        lastClickTime = Time.time;
        flash = 1f;

        sfx.pitch = Mathf.Lerp(0.8f, 1.6f, Charge);
        sfx.PlayOneShot(clickSound != null ? clickSound : ProceduralAudio.LampClick, 0.7f);

        if (Charge >= 1f) TurnOn();
    }

    void Update()
    {
        if (IsLit)
        {
            // Occasional flicker so it never feels completely safe.
            lampLight.intensity = Random.value < 0.01f ? lightIntensity * 0.4f : lightIntensity;
            return;
        }

        if (Charge > 0f && Time.time - lastClickTime > drainDelay)
            Charge = Mathf.Max(0f, Charge - drainRate * Time.deltaTime);

        // The bulb sputters on each click, brighter as the meter fills.
        flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 4f);
        float glow = flash * Charge;
        lampLight.enabled = glow > 0.01f;
        lampLight.intensity = lightIntensity * 0.6f * glow * Random.Range(0.6f, 1f);
        SetBulb(glow);
    }

    void TurnOn()
    {
        IsLit = true;
        Charge = 1f;
        lampLight.enabled = true;
        lampLight.intensity = lightIntensity;
        lampLight.range = safeRadius * 1.6f;
        SetBulb(1f);

        sfx.pitch = 1f;
        sfx.PlayOneShot(igniteSound != null ? igniteSound : ProceduralAudio.LampIgnite);
        hum.Play();

        if (spawnEntityWhenLit) ShadowEntity.Spawn();
    }

    /// <summary>Turns the lamp back off and empties the meter.</summary>
    public void ResetLamp()
    {
        IsLit = false;
        Charge = 0f;
        flash = 0f;
        lampLight.enabled = false;
        lampLight.range = 6f;
        SetBulb(0f);
        hum.Stop();
    }

    void SetBulb(float glow)
    {
        Color off = new Color(0.08f, 0.07f, 0.06f);
        MaterialFactory.SetColor(bulbMat, Color.Lerp(off, lightColor * 6f, glow));
    }

    void Build()
    {
        Material metal = MaterialFactory.Lit(new Color(0.12f, 0.12f, 0.13f), 0.3f);
        bulbMat = MaterialFactory.Unlit(Color.black);

        var pole = Part(PrimitiveType.Cylinder, metal, new Vector3(0f, PoleHeight * 0.5f, 0f), new Vector3(0.12f, PoleHeight * 0.5f, 0.12f), keepCollider: true);
        Part(PrimitiveType.Cylinder, metal, new Vector3(0f, 0.1f, 0f), new Vector3(0.45f, 0.1f, 0.45f));
        Part(PrimitiveType.Cube, metal, new Vector3(0f, PoleHeight + 0.15f, 0f), new Vector3(0.5f, 0.12f, 0.5f));
        Part(PrimitiveType.Cube, metal, new Vector3(0f, PoleHeight - 0.25f, 0f), new Vector3(0.35f, 0.06f, 0.35f));
        Part(PrimitiveType.Sphere, bulbMat, new Vector3(0f, PoleHeight - 0.05f, 0f), Vector3.one * 0.28f);
        pole.name = "Pole";

        // Generous invisible box so the lamp is easy to click on.
        var aim = gameObject.AddComponent<BoxCollider>();
        aim.isTrigger = true;
        aim.center = new Vector3(0f, PoleHeight * 0.55f, 0f);
        aim.size = new Vector3(1.2f, PoleHeight + 0.4f, 1.2f);

        lampLight = new GameObject("Lamp Light").AddComponent<Light>();
        lampLight.transform.SetParent(transform, false);
        lampLight.transform.localPosition = new Vector3(0f, PoleHeight - 0.2f, 0f);
        lampLight.type = LightType.Point;
        lampLight.color = lightColor;
        lampLight.range = 6f; // just the sputter while charging; widened when lit
        lampLight.shadows = LightShadows.None;
        lampLight.enabled = false;
    }

    GameObject Part(PrimitiveType type, Material mat, Vector3 pos, Vector3 scale, bool keepCollider = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!keepCollider) Destroy(go.GetComponent<Collider>());
        return go;
    }
}
