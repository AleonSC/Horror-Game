using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A lamp post that starts off. The player clicks it repeatedly to fill a meter (which slowly
/// drains if they stop). A full meter lights the lamp, making a safe zone that entities can't
/// enter or creep into. Every lamp lit also summons another ShadowEntity.
///
/// A lit lamp burns through its energy. It flickers more and more as the energy runs low, then
/// goes out and needs to recharge before it can be lit again, crackling with faint blue sparks meanwhile.
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

    [Header("Energy")]
    [Tooltip("Seconds a lit lamp lasts before going out.")]
    public float energyDuration = 90f;
    [Range(0f, 1f), Tooltip("Below this much energy the light starts flickering, more as it gets lower.")]
    public float flickerBelow = 0.35f;
    [Tooltip("Seconds after going out before the lamp can be lit again.")]
    public float cooldownTime = 30f;
    [Tooltip("Colour of the little electrical sparks while the lamp recharges.")]
    public Color sparkColor = new Color(0.45f, 0.65f, 1f);
    [Tooltip("Average sparks per second while recharging.")]
    public float sparksPerSecond = 1.2f;

    [Header("Audio (optional)")]
    public AudioClip clickSound;
    public AudioClip igniteSound;
    public AudioClip humLoop;
    public AudioClip powerDownSound;
    public AudioClip flickerSound;

    public bool IsLit { get; private set; }
    /// <summary>Meter from 0 to 1.</summary>
    public float Charge { get; private set; }
    /// <summary>Remaining energy from 1 (just lit) to 0 (goes out).</summary>
    public float Energy { get; private set; }
    public float CooldownRemaining { get; private set; }
    public bool IsCoolingDown => CooldownRemaining > 0f;
    /// <summary>Cooldown progress from 0 (just went out) to 1 (ready).</summary>
    public float CooldownProgress => cooldownTime > 0f ? 1f - CooldownRemaining / cooldownTime : 1f;

    /// <summary>How many times any lamp has been turned on this run.</summary>
    public static int TimesLit { get; private set; }
    public static void ResetTimesLit() => TimesLit = 0;

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
    public static bool IsInSafeZone(Vector3 point) => IsInSafeZone(point, out _);

    /// <summary>True if the point is inside any lit lamp's safe zone, and which lamp.</summary>
    public static bool IsInSafeZone(Vector3 point, out Lamp lamp)
    {
        foreach (var l in All)
        {
            if (!l.IsLit) continue;
            Vector3 d = point - l.transform.position;
            d.y = 0f;
            if (d.sqrMagnitude < l.safeRadius * l.safeRadius)
            {
                lamp = l;
                return true;
            }
        }
        lamp = null;
        return false;
    }

    Light lampLight;
    Light sparkLight;
    Transform sparkRoot;
    Material sparkMat;
    float sparkTimer;
    Material bulbMat;
    AudioSource hum, sfx;
    float lastClickTime;
    float flash;
    float flickerTimer;
    float flickerLevel = 1f;

    const float PoleHeight = 3f;
    const float HumVolume = 0.2f;

    /// <summary>World position of the bulb (moths gather here).</summary>
    public Vector3 BulbPosition => transform.position + Vector3.up * (PoleHeight - 0.05f);

    void Awake()
    {
        Build();
        hum = ProceduralAudio.AddSource(gameObject, humLoop != null ? humLoop : ProceduralAudio.LampHum, true, true, HumVolume, 2f, 20f);
        sfx = ProceduralAudio.AddSource(gameObject, null, false, true, 0.8f, 3f, 30f);
        SetBulb(0f);
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    /// <summary>One click's worth of charge. Lights the lamp when the meter is full.</summary>
    public void AddCharge()
    {
        if (IsLit || IsCoolingDown) return;

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
            UpdateLit();
            return;
        }

        if (IsCoolingDown)
        {
            CooldownRemaining = Mathf.Max(0f, CooldownRemaining - Time.deltaTime);
            UpdateSparks();
            if (!IsCoolingDown) EndSpark();
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

    void UpdateLit()
    {
        Energy = Mathf.Max(0f, Energy - Time.deltaTime / Mathf.Max(energyDuration, 0.01f));
        if (Energy <= 0f)
        {
            TurnOff();
            return;
        }

        // 0 while healthy, rising to 1 as the energy runs out.
        float low = flickerBelow > 0f ? Mathf.Clamp01(1f - Energy / flickerBelow) : 0f;

        if (flickerTimer > 0f)
        {
            flickerTimer -= Time.deltaTime;
            if (flickerTimer <= 0f) flickerLevel = 1f;
        }
        else
        {
            // Flickers per second: rare while healthy, constant sputtering near the end.
            float rate = Mathf.Lerp(0.3f, 9f, low * low);
            if (Random.value < rate * Time.deltaTime)
            {
                flickerTimer = Random.Range(0.04f, Mathf.Lerp(0.1f, 0.35f, low));
                flickerLevel = Random.Range(0f, Mathf.Lerp(0.6f, 0.25f, low));
                if (low > 0f) sfx.PlayOneShot(flickerSound != null ? flickerSound : ProceduralAudio.LampSpark, 0.3f + 0.4f * low);
            }
        }

        float level = flickerLevel * Mathf.Lerp(1f, 0.7f, low); // also dims overall near the end
        lampLight.intensity = lightIntensity * level;
        SetBulb(level);
        hum.volume = HumVolume * level;
    }

    /// <summary>While recharging, little blue sparks crackle around the lamp head now and then.</summary>
    void UpdateSparks()
    {
        if (sparkTimer > 0f)
        {
            sparkTimer -= Time.deltaTime;
            // Jitter the arcs and light while the spark lasts.
            sparkLight.intensity = Random.Range(0.6f, 1.6f);
            foreach (Transform arc in sparkRoot)
                arc.localRotation = Random.rotation;
            if (sparkTimer <= 0f) EndSpark();
            return;
        }

        MaterialFactory.SetColor(bulbMat, Color.Lerp(BulbOff, sparkColor * 0.4f, 0.15f)); // faint blue tinge

        if (Random.value < sparksPerSecond * Time.deltaTime)
        {
            sparkTimer = Random.Range(0.05f, 0.15f);
            sparkRoot.localPosition = new Vector3(Random.Range(-0.2f, 0.2f), PoleHeight - Random.Range(0f, 0.25f), Random.Range(-0.2f, 0.2f));
            sparkRoot.gameObject.SetActive(true);
            sparkLight.enabled = true;
            MaterialFactory.SetColor(bulbMat, sparkColor * 1.5f);
            sfx.pitch = Random.Range(1.3f, 1.8f);
            sfx.PlayOneShot(flickerSound != null ? flickerSound : ProceduralAudio.LampSpark, 0.15f);
        }
    }

    void EndSpark()
    {
        sparkTimer = 0f;
        sparkRoot.gameObject.SetActive(false);
        sparkLight.enabled = false;
        if (!IsLit) SetBulb(0f);
    }

    void TurnOn()
    {
        IsLit = true;
        TimesLit++;
        Energy = 1f;
        flickerTimer = 0f;
        flickerLevel = 1f;
        Charge = 1f;
        lampLight.enabled = true;
        lampLight.intensity = lightIntensity;
        lampLight.range = safeRadius * 1.6f;
        SetBulb(1f);

        sfx.pitch = 1f;
        sfx.PlayOneShot(igniteSound != null ? igniteSound : ProceduralAudio.LampIgnite);
        hum.volume = HumVolume;
        hum.Play();

        if (spawnEntityWhenLit) ShadowEntity.Spawn();
    }

    void TurnOff()
    {
        IsLit = false;
        Energy = 0f;
        Charge = 0f;
        CooldownRemaining = cooldownTime;
        lampLight.enabled = false;
        lampLight.range = 6f;
        SetBulb(0f);
        hum.Stop();

        sfx.pitch = 1f;
        sfx.PlayOneShot(powerDownSound != null ? powerDownSound : ProceduralAudio.LampPowerDown);
    }

    /// <summary>Turns the lamp back off and empties the meter.</summary>
    public void ResetLamp()
    {
        IsLit = false;
        Charge = 0f;
        Energy = 0f;
        CooldownRemaining = 0f;
        EndSpark();
        flash = 0f;
        lampLight.enabled = false;
        lampLight.range = 6f;
        SetBulb(0f);
        hum.Stop();
    }

    static readonly Color BulbOff = new Color(0.08f, 0.07f, 0.06f);

    void SetBulb(float glow)
    {
        MaterialFactory.SetColor(bulbMat, Color.Lerp(BulbOff, lightColor * 6f, glow));
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

        // Recharge spark: a few thin, bright blue arcs and a small flash of blue light.
        sparkMat = MaterialFactory.Unlit(sparkColor * 4f);
        sparkRoot = new GameObject("Spark").transform;
        sparkRoot.SetParent(transform, false);
        for (int i = 0; i < 3; i++)
        {
            var arc = GameObject.CreatePrimitive(PrimitiveType.Cube);
            arc.transform.SetParent(sparkRoot, false);
            arc.transform.localScale = new Vector3(0.006f, 0.006f, Random.Range(0.06f, 0.14f));
            arc.GetComponent<Renderer>().sharedMaterial = sparkMat;
            arc.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Destroy(arc.GetComponent<Collider>());
        }
        sparkLight = sparkRoot.gameObject.AddComponent<Light>();
        sparkLight.type = LightType.Point;
        sparkLight.color = sparkColor;
        sparkLight.range = 2.5f;
        sparkLight.shadows = LightShadows.None;
        sparkLight.enabled = false;
        sparkRoot.gameObject.SetActive(false);
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
