using UnityEngine;

/// <summary>
/// A fired flare. Shot into the sky, it climbs, bursts at the top of its arc (wiping out the hunt
/// progress of every entity: they're blasted away and have to start over), then drifts slowly down
/// burning bright red. Fired low, it just hits the ground and burns out there, wasted.
/// Hotbar fires these from the flare gun.
/// </summary>
public class Flare : MonoBehaviour
{
    public float gravity = 9f;
    [Tooltip("Seconds it burns after bursting (or landing).")]
    public float burnTime = 12f;
    [Tooltip("It has to burst at least this high above the ground to count as 'in the sky'.")]
    public float skyHeight = 12f;
    public Color color = new Color(1f, 0.15f, 0.08f);

    Vector3 velocity;
    bool burst, landed;
    float burnLeft;
    Light glow;
    Transform core;
    AudioSource hiss;
    DarkForestGenerator generator;

    /// <summary>Fires a flare from a point in a direction.</summary>
    public static Flare Fire(Vector3 from, Vector3 direction, float speed)
    {
        var flare = new GameObject("Flare").AddComponent<Flare>();
        flare.transform.position = from;
        flare.velocity = direction.normalized * speed;
        return flare;
    }

    void Start()
    {
        generator = FindAnyObjectByType<DarkForestGenerator>();
        WorldWrap.RegisterDynamic(transform);

        // A white-hot core (seen through fog), a red trail, and red light.
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(go.GetComponent<Collider>());
        core = go.transform;
        core.SetParent(transform, false);
        core.localScale = Vector3.one * 0.25f;
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = MaterialFactory.NoFog(new Color(1f, 0.6f, 0.5f) * 6f);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        var trail = gameObject.AddComponent<TrailRenderer>();
        trail.sharedMaterial = MaterialFactory.NoFog(color * 4f);
        trail.time = 1.5f;
        trail.startWidth = 0.18f;
        trail.endWidth = 0f;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        glow = gameObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = color;
        glow.range = 25f;
        glow.intensity = 3f;
        glow.shadows = LightShadows.None;

        hiss = ProceduralAudio.AddSource(gameObject, ProceduralAudio.FlareBurn, true, true, 0.6f, 6f, 120f);
        hiss.Play();

        burnLeft = burnTime + 6f; // safety: it never lives forever
    }

    void Update()
    {
        float dt = Time.deltaTime;
        Vector3 pos = transform.position;
        float ground = generator != null ? generator.GetGroundHeight(pos.x, pos.z) : 0f;

        if (!burst && !landed)
        {
            // Climbing: arcs over under light gravity. Bursts at the top if it's high enough.
            velocity += Vector3.down * gravity * dt;
            Vector3 next = pos + velocity * dt;

            if (Physics.Raycast(pos, velocity.normalized, out var hit, (next - pos).magnitude, ~0, QueryTriggerInteraction.Ignore) || next.y <= ground)
            {
                // Hit the ground or a tree: wasted.
                landed = true;
                transform.position = Physics.Raycast(pos, velocity.normalized, out hit, (next - pos).magnitude, ~0, QueryTriggerInteraction.Ignore)
                    ? hit.point : new Vector3(next.x, ground + 0.05f, next.z);
                burnLeft = burnTime * 0.6f;
                glow.range = 15f;
                return;
            }
            transform.position = next;

            if (velocity.y <= 0f)
            {
                if (next.y - ground >= skyHeight) Burst();
                else { burst = true; burnLeft = burnTime * 0.5f; } // too low: just a fizzle that drifts down
            }
        }
        else if (burst)
        {
            // Hanging in the sky, drifting slowly down.
            transform.position += new Vector3(Mathf.Sin(Time.time * 0.4f) * 0.4f, -1.2f, 0f) * dt;
        }

        burnLeft -= dt;
        float flicker = 0.85f + 0.3f * Mathf.PerlinNoise(Time.time * 12f, 0.5f);
        float fade = Mathf.Clamp01(burnLeft / 1.5f);
        glow.intensity = (burst ? 9f : 3f) * flicker * fade;
        core.localScale = Vector3.one * (burst ? 0.5f : 0.25f) * flicker * Mathf.Max(fade, 0.2f);
        hiss.volume = 0.6f * fade;

        if (burnLeft <= 0f) Destroy(gameObject);
    }

    void Burst()
    {
        burst = true;
        burnLeft = burnTime;
        glow.range = 70f;

        // The whole forest lights up red: every entity's hunt starts over.
        foreach (var entity in new System.Collections.Generic.List<ShadowEntity>(ShadowEntity.All))
            entity.ResetHunt();
    }
}
