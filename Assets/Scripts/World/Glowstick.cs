using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A cracked yellow glowstick, tossed a few metres ahead. It tumbles to the ground and lies there glowing
/// for a long time, slowly dimming: a trail of them marks the way you came. It's only a light to see by;
/// the entities don't care about it. Hotbar throws these.
/// </summary>
public class Glowstick : MonoBehaviour
{
    public static readonly List<Glowstick> All = new List<Glowstick>();

    public static readonly Color GlowColor = new Color(1f, 0.85f, 0.15f);
    [Tooltip("Seconds it glows before it's spent.")]
    public float glowTime = 480f;
    public float lightRange = 4.5f;
    public float lightIntensity = 0.9f;
    public float gravity = 12f;

    Vector3 velocity, spin;
    bool landed;
    float age;
    Light glow;
    Renderer stick;
    MaterialPropertyBlock block;
    DarkForestGenerator generator;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    /// <summary>Throws a freshly cracked glowstick.</summary>
    public static Glowstick Throw(Vector3 from, Vector3 velocity)
    {
        var g = new GameObject("Glowstick").AddComponent<Glowstick>();
        g.transform.SetPositionAndRotation(from, Random.rotation);
        g.velocity = velocity;
        g.spin = Random.onUnitSphere * Random.Range(360f, 900f);
        return g;
    }

    /// <summary>Removes every thrown glowstick (used when the run resets).</summary>
    public static void ClearAll()
    {
        foreach (var g in new List<Glowstick>(All)) Destroy(g.gameObject);
    }

    void Start()
    {
        generator = FindAnyObjectByType<DarkForestGenerator>();
        WorldWrap.RegisterDynamic(transform);

        stick = BuildModel(transform, true);
        glow = gameObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = GlowColor;
        glow.range = lightRange;
        glow.intensity = lightIntensity;
        glow.shadows = LightShadows.None;
        block = new MaterialPropertyBlock();
    }

    /// <summary>A small yellow stick with dark end caps, pointing along local Y. Glowing if lit.</summary>
    public static Renderer BuildModel(Transform parent, bool lit)
    {
        var tube = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        tube.name = "Glowstick Tube";
        tube.transform.SetParent(parent, false);
        tube.transform.localScale = new Vector3(0.022f, 0.08f, 0.022f);
        Destroy(tube.GetComponent<Collider>());
        var renderer = tube.GetComponent<Renderer>();
        renderer.sharedMaterial = lit ? MaterialFactory.NoFog(GlowColor * 2.5f) : MaterialFactory.Lit(GlowColor * 0.6f, 0.6f);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return renderer;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        if (!landed)
        {
            Vector3 pos = transform.position;
            velocity += Vector3.down * gravity * dt;
            Vector3 next = pos + velocity * dt;
            float ground = generator != null ? generator.GetGroundHeight(next.x, next.z) : 0f;

            if (Physics.Raycast(pos, velocity.normalized, out var hit, (next - pos).magnitude + 0.03f, ~0, QueryTriggerInteraction.Ignore))
            {
                // Bounced off a trunk or a rock: knocked back, losing most of its speed.
                velocity = Vector3.Reflect(velocity, hit.normal) * 0.3f;
                next = hit.point + hit.normal * 0.05f;
            }
            if (next.y <= ground + 0.02f)
            {
                landed = true;
                Noise.Emit(next, 14f); // clatters as it lands: something to lure a listener away with
                next.y = ground + 0.02f;
                // Lies flat on the ground.
                transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Quaternion.Euler(90f, 0f, 0f);
            }
            else transform.Rotate(spin * dt, Space.World);
            transform.position = next;
        }

        // Dims slowly over its life, faster at the very end.
        age += dt;
        float life = Mathf.Clamp01(1f - age / glowTime);
        float brightness = Mathf.Sqrt(life);
        glow.intensity = lightIntensity * brightness;
        block.SetColor("_Color", GlowColor * Mathf.Lerp(0.3f, 2.5f, brightness));
        stick.SetPropertyBlock(block);

        if (life <= 0f)
        {
            glow.enabled = false;
            enabled = false; // spent: just a dull stick on the ground now
        }
    }
}
