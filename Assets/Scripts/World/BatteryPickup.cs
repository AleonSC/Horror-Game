using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A battery lying on the ground. Clicking it (via LampInteractor) refills the flashlight
/// by a quarter and uses the battery up. A small pulsing green light helps find it in the dark.
/// BatterySpawner scatters these around the map.
/// </summary>
public class BatteryPickup : MonoBehaviour
{
    public static readonly List<BatteryPickup> All = new List<BatteryPickup>();

    [Range(0f, 1f), Tooltip("How much of the flashlight battery this refills.")]
    public float charge = 0.25f;
    public Color glowColor = new Color(0.3f, 1f, 0.4f);
    [Tooltip("Leave empty to use the built-in sound.")]
    public AudioClip useSound;

    Material ledMat;
    Light glow;
    float pulseOffset;

    void Awake()
    {
        Build();
        pulseOffset = Random.Range(0f, 10f);
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Update()
    {
        // Slow pulse, like a standby LED.
        float pulse = 0.35f + 0.65f * Mathf.Pow(0.5f + 0.5f * Mathf.Sin((Time.time + pulseOffset) * 2.2f), 3f);
        MaterialFactory.SetColor(ledMat, glowColor * (0.5f + 2.5f * pulse));
        glow.intensity = 0.6f * pulse;
    }

    /// <summary>Refills the flashlight and removes the battery. Returns false if the flashlight is already full.</summary>
    public bool Use(Flashlight flashlight)
    {
        if (flashlight == null || !flashlight.AddBattery(charge)) return false;
        ProceduralAudio.PlayAt(useSound != null ? useSound : ProceduralAudio.BatteryInsert, transform.position, 0.9f, 1f, 2f);
        Destroy(gameObject);
        return true;
    }

    void Build()
    {
        var body = MaterialFactory.Lit(new Color(0.08f, 0.08f, 0.09f), 0.4f);
        var label = MaterialFactory.Lit(new Color(0.75f, 0.45f, 0.1f), 0.5f);
        var metal = MaterialFactory.Lit(new Color(0.6f, 0.6f, 0.62f), 0.8f);
        ledMat = MaterialFactory.Unlit(glowColor);

        // Lying on its side: cylinders rotated to point along X.
        Part(PrimitiveType.Cylinder, body, new Vector3(0f, 0.05f, 0f), new Vector3(0.1f, 0.11f, 0.1f));
        Part(PrimitiveType.Cylinder, label, new Vector3(0.03f, 0.05f, 0f), new Vector3(0.104f, 0.06f, 0.104f));
        Part(PrimitiveType.Cylinder, metal, new Vector3(0.12f, 0.05f, 0f), new Vector3(0.035f, 0.012f, 0.035f));
        var led = Part(PrimitiveType.Sphere, ledMat, new Vector3(-0.06f, 0.1f, 0f), Vector3.one * 0.02f);
        led.transform.rotation = Quaternion.identity;

        glow = new GameObject("Battery Glow").AddComponent<Light>();
        glow.transform.SetParent(transform, false);
        glow.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        glow.type = LightType.Point;
        glow.color = glowColor;
        glow.range = 1.5f;
        glow.shadows = LightShadows.None;

        // Generous invisible box so it's easy to click on.
        var aim = gameObject.AddComponent<BoxCollider>();
        aim.isTrigger = true;
        aim.center = new Vector3(0f, 0.15f, 0f);
        aim.size = new Vector3(0.6f, 0.4f, 0.6f);
    }

    GameObject Part(PrimitiveType type, Material mat, Vector3 pos, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        Destroy(go.GetComponent<Collider>());
        return go;
    }
}
