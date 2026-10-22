using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A cracked yellow glowstick, tossed a few metres ahead. It tumbles to the ground and lies there glowing
/// for a long time, slowly dimming: a trail of them marks the way you came. It's only a light to see by;
/// the entities don't care about it. Hotbar throws these.
/// </summary>
public class Glowstick : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Inspector changes made in Play are kept)

    public static readonly List<Glowstick> All = new List<Glowstick>();
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() => All.Clear();

    public static readonly Color GlowColor = new Color(1f, 0.85f, 0.15f);
    [Tooltip("Seconds it glows before it's spent.")]
    public float glowTime = 480f;
    public float lightRange = 4.5f;
    public float lightIntensity = 0.9f;
    public float gravity = 12f;

    Vector3 velocity, spin;
    bool landed;
    float age, airTime;
    Quaternion restRotation;
    float settle; // 0 -> 1 as it rolls to a stop after landing
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

    /// <summary>
    /// A glowstick along local Y (about 15cm): a glowing yellow tube, rounded at both ends, with a frosted
    /// clear plastic band at each end, a dark cap at the bottom and at the top a cap with a little hanging
    /// loop and a cord. Returns the glowing tube's renderer (its colour dims over the stick's life).
    /// </summary>
    public static Renderer BuildModel(Transform parent, bool lit)
    {
        var shadows = UnityEngine.Rendering.ShadowCastingMode.Off;
        Renderer Part(PrimitiveType type, Material mat, Vector3 pos, Vector3 scale, Vector3 euler)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.transform.localScale = scale;
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = shadows;
            return r;
        }

        var glowMat = lit ? MaterialFactory.NoFog(GlowColor * 2.5f) : MaterialFactory.Lit(GlowColor * 0.55f, 0.75f);
        var clear = MaterialFactory.Lit(new Color(0.55f, 0.55f, 0.5f), 0.85f);   // the frosted plastic ends
        var cap = MaterialFactory.Lit(new Color(0.08f, 0.08f, 0.085f), 0.35f);
        var cord = MaterialFactory.Lit(new Color(0.05f, 0.05f, 0.06f), 0.1f);

        // The tube: glowing liquid, its ends rounded inside the plastic.
        var tube = Part(PrimitiveType.Capsule, glowMat, Vector3.zero, new Vector3(0.016f, 0.066f, 0.016f), Vector3.zero);
        tube.name = "Glowstick Tube";
        // Frosted bands where the liquid stops short of the ends.
        Part(PrimitiveType.Cylinder, clear, new Vector3(0f, 0.062f, 0f), new Vector3(0.0175f, 0.006f, 0.0175f), Vector3.zero);
        Part(PrimitiveType.Cylinder, clear, new Vector3(0f, -0.062f, 0f), new Vector3(0.0175f, 0.006f, 0.0175f), Vector3.zero);
        // Caps: plain at the bottom, at the top one with a little eye for the cord.
        Part(PrimitiveType.Cylinder, cap, new Vector3(0f, -0.071f, 0f), new Vector3(0.018f, 0.004f, 0.018f), Vector3.zero);
        Part(PrimitiveType.Cylinder, cap, new Vector3(0f, 0.071f, 0f), new Vector3(0.018f, 0.004f, 0.018f), Vector3.zero);
        Part(PrimitiveType.Cube, cap, new Vector3(0f, 0.08f, 0f), new Vector3(0.004f, 0.012f, 0.009f), Vector3.zero);
        // The loop of cord through it, drooping to one side.
        for (int k = 0; k < 6; k++)
        {
            float a = (k + 0.5f) / 6f * Mathf.PI * 2f;
            Vector3 p = new Vector3(0f, 0.094f + Mathf.Sin(a) * 0.011f, Mathf.Cos(a) * 0.011f);
            Part(PrimitiveType.Cylinder, cord, p, new Vector3(0.002f, 0.006f, 0.002f), new Vector3(a * Mathf.Rad2Deg, 0f, 0f));
        }
        return tube;
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

            airTime += dt;
            Vector3 groundNormal = Vector3.up;
            bool down = false;
            if (Physics.Raycast(pos, velocity.normalized, out var hit, (next - pos).magnitude + 0.03f, ~0, QueryTriggerInteraction.Ignore))
            {
                if (hit.normal.y > 0.55f)
                {
                    // Something it can lie on (the ground, a log, a rock's top): it's down.
                    down = true;
                    next = hit.point;
                    groundNormal = hit.normal;
                }
                else
                {
                    // Bounced off a trunk or a rock's side: knocked back, losing most of its speed.
                    velocity = Vector3.Reflect(velocity, hit.normal) * 0.3f;
                    next = hit.point + hit.normal * 0.05f;
                }
            }
            if (next.y <= ground + 0.02f || airTime > 6f) { down = true; next.y = Mathf.Max(next.y, ground); }
            if (down)
            {
                landed = true;
                Noise.Emit(next, 14f); // clatters as it lands: something to lure a listener away with
                next += groundNormal * 0.012f;
                // Comes to rest lying flat along whatever it landed on, turned whichever way it was going.
                Vector3 along = Vector3.ProjectOnPlane(transform.up, groundNormal);
                if (along.sqrMagnitude < 0.0001f) along = Vector3.ProjectOnPlane(Random.onUnitSphere, groundNormal);
                restRotation = Quaternion.LookRotation(groundNormal, along.normalized); // its length (local Y) along the ground
                settle = 0f;
            }
            else transform.Rotate(spin * dt, Space.World);
            transform.position = next;
        }
        else if (settle < 1f)
        {
            // A quick roll to a stop, then still.
            settle = Mathf.Min(1f, settle + dt / 0.25f);
            transform.rotation = Quaternion.Slerp(transform.rotation, restRotation, settle);
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
