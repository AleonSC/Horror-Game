using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A fired flare. Shot into the sky it climbs, a white-hot core in a blood-red glow, trailing fire and smoke
/// and spitting sparks, lighting the trees more and more as it rises. At the top of its arc it bursts: a
/// big bright pop that floods the whole forest with light and throws a shell of sparks out in every
/// direction (wiping out the hunt progress of every entity: they're blasted away and have to start over);
/// then it hangs there burning, drifting slowly down, raining embers, still lighting everything red.
/// Fired low, it just hits the ground and burns out there, wasted. Hotbar fires these from the flare gun.
/// </summary>
public class Flare : MonoBehaviour
{
    public float gravity = 9f;
    [Tooltip("Seconds it burns after bursting (or landing).")]
    public float burnTime = 14f;
    [Tooltip("It has to burst at least this high above the ground to count as 'in the sky'.")]
    public float skyHeight = 12f;
    public Color color = new Color(1f, 0.15f, 0.08f);
    [Header("Light")]
    [Tooltip("Brightness and reach as it leaves the gun...")]
    public float launchIntensity = 10f;
    public float launchRange = 45f;
    [Tooltip("...growing to this as it climbs (reached at climbFullHeight metres up)...")]
    public float climbIntensity = 35f;
    public float climbRange = 140f;
    public float climbFullHeight = 30f;
    [Tooltip("...and this once it bursts in the sky, lighting up the whole forest around.")]
    public float burstIntensity = 70f;
    public float burstRange = 260f;
    [Tooltip("The pop: how much brighter (times) for an instant as it bursts, fading over popTime.")]
    public float popFlash = 4f;
    public float popTime = 0.8f;

    Vector3 velocity;
    bool burst, landed, burstDone; // burstDone: it really burst in the sky (not a low fizzle)
    float burnLeft, sinceBurst = -1f, sparkTimer, smokeTimer;
    Light glow;
    Transform core, halo, popHalo;
    Material haloMat, popMat;
    AudioSource hiss, popSource;
    DarkForestGenerator generator;

    // Sparks and smoke: little pools of quads recycled as they burn out.
    class Particle { public Transform t; public Vector3 v; public float age, life, size; public bool smoke; }
    readonly List<Particle> particles = new List<Particle>();
    static Material sparkMat, smokeMat;
    static Mesh quad;
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics()
    {
        StaticReset.Drop(ref sparkMat);
        StaticReset.Drop(ref smokeMat);
        quad = null; // (Unity's own quad: forgotten, not destroyed)
    }

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
        if (quad == null)
        {
            var probe = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad = probe.GetComponent<MeshFilter>().sharedMesh;
            Destroy(probe);
            Shader glowShader = Shader.Find("Horror/Glow"), smokeShader = Shader.Find("Horror/Smoke");
            if (glowShader != null)
            {
                sparkMat = new Material(glowShader);
                sparkMat.SetColor("_Color", new Color(1f, 0.55f, 0.3f));
                sparkMat.SetFloat("_Intensity", 2.5f);
            }
            if (smokeShader != null) smokeMat = new Material(smokeShader) { renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent };
        }

        // A white-hot core, seen through the fog...
        var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        Destroy(go.GetComponent<Collider>());
        core = go.transform;
        core.SetParent(transform, false);
        core.localScale = Vector3.one * 0.22f;
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = MaterialFactory.NoFog(new Color(1f, 0.85f, 0.75f) * 8f);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        // ...in a big soft red glow...
        Shader glow2 = Shader.Find("Horror/Glow");
        if (glow2 != null)
        {
            haloMat = new Material(glow2);
            haloMat.SetColor("_Color", color);
            halo = MakeQuad("Flare Glow", haloMat);
            popMat = new Material(glow2);
            popMat.SetColor("_Color", new Color(1f, 0.7f, 0.6f));
            popHalo = MakeQuad("Flare Pop", popMat);
            popHalo.gameObject.SetActive(false);
        }

        // ...trailing fire: a glowing ribbon, white-hot near the flare, red, fading away behind.
        var trail = gameObject.AddComponent<TrailRenderer>();
        Shader trailShader = Shader.Find("Horror/Trail");
        if (trailShader != null)
        {
            var tm = new Material(trailShader);
            tm.SetColor("_Color", new Color(1f, 0.35f, 0.18f));
            tm.SetFloat("_Intensity", 3f);
            trail.sharedMaterial = tm;
        }
        else trail.sharedMaterial = MaterialFactory.NoFog(color * 4f);
        trail.time = 1.8f;
        trail.widthCurve = new AnimationCurve(new Keyframe(0f, 0.35f), new Keyframe(0.2f, 0.25f), new Keyframe(1f, 0f));
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.6f, 0.3f), new GradientAlphaKey(0f, 1f) });
        trail.colorGradient = gradient;
        trail.textureMode = LineTextureMode.Stretch;
        trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        trail.receiveShadows = false;

        glow = gameObject.AddComponent<Light>();
        glow.type = LightType.Point;
        glow.color = color;
        glow.range = launchRange;
        glow.intensity = launchIntensity;
        glow.shadows = LightShadows.None;

        hiss = ProceduralAudio.AddSource(gameObject, ProceduralAudio.FlareBurn, true, true, 0.6f, 6f, 120f);
        hiss.Play();
        popSource = ProceduralAudio.AddSource(gameObject, null, false, true, 1f, 40f, 600f); // heard all over

        burnLeft = burnTime + 6f; // safety: it never lives forever
    }

    Transform MakeQuad(string name, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = quad;
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;
        return go.transform;
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

        // Brighter and further-reaching the higher it climbs; brightest of all once it bursts, with a flash
        // as it pops.
        float up = Mathf.Clamp01((transform.position.y - ground) / Mathf.Max(climbFullHeight, 0.01f));
        float intensity, range;
        if (landed) { intensity = launchIntensity * 0.7f; range = launchRange * 0.5f; }
        else if (burst && burstDone) { intensity = burstIntensity; range = burstRange; }
        else { intensity = Mathf.Lerp(launchIntensity, climbIntensity, up); range = Mathf.Lerp(launchRange, climbRange, up); }
        float pop = 0f;
        if (sinceBurst >= 0f)
        {
            sinceBurst += dt;
            pop = Mathf.Exp(-sinceBurst / Mathf.Max(popTime * 0.35f, 0.01f));
        }
        glow.range = Mathf.MoveTowards(glow.range, range * (1f + pop * 0.5f), dt * 400f);
        glow.intensity = intensity * flicker * fade * (1f + popFlash * pop);
        glow.color = Color.Lerp(color, new Color(1f, 0.75f, 0.65f), pop); // the pop flashes near white
        core.localScale = Vector3.one * (burst ? 0.45f : 0.22f) * flicker * Mathf.Max(fade, 0.2f) * (1f + pop * 2f);
        hiss.volume = 0.6f * fade;

        // The glow round it, and the pop's great blooming flash.
        var cam = Camera.main;
        if (halo != null && cam != null)
        {
            halo.rotation = cam.transform.rotation;
            halo.localScale = Vector3.one * (burst ? 9f : 4f) * Mathf.Lerp(0.9f, 1.1f, flicker - 0.85f) * Mathf.Max(fade, 0.1f);
            haloMat.SetFloat("_Intensity", (burst ? 0.7f : 0.5f) * fade);
            if (popHalo != null)
            {
                bool popping = pop > 0.01f;
                if (popHalo.gameObject.activeSelf != popping) popHalo.gameObject.SetActive(popping);
                if (popping)
                {
                    popHalo.rotation = cam.transform.rotation;
                    popHalo.localScale = Vector3.one * Mathf.Lerp(60f, 12f, pop);
                    popMat.SetFloat("_Intensity", 1.6f * pop);
                }
            }
        }

        UpdateParticles(dt, ground, fade, cam);

        if (burnLeft <= 0f && particles.TrueForAll(p => !p.t.gameObject.activeSelf)) Destroy(gameObject);
    }

    // ---------------------------------------------------------------- Sparks & smoke

    void UpdateParticles(float dt, float ground, float fade, Camera cam)
    {
        bool burning = burnLeft > 0f;
        // Climbing it spits sparks and leaves smoke behind; hanging in the sky it rains embers.
        sparkTimer -= dt;
        if (burning && sparkTimer <= 0f)
        {
            sparkTimer = burst ? 0.06f : 0.025f;
            Vector3 v = burst ? new Vector3(Random.Range(-1f, 1f), Random.Range(-3f, -0.5f), Random.Range(-1f, 1f))
                              : -velocity * 0.15f + Random.insideUnitSphere * 2f;
            Spawn(transform.position, v, Random.Range(0.4f, 1.1f), Random.Range(0.08f, 0.16f) * Mathf.Max(fade, 0.3f), false);
        }
        smokeTimer -= dt;
        if (burning && smokeMat != null && smokeTimer <= 0f && !landed)
        {
            smokeTimer = burst ? 0.35f : 0.08f;
            Spawn(transform.position, Vector3.up * 0.3f + Random.insideUnitSphere * 0.4f, Random.Range(2.5f, 4f), Random.Range(0.6f, 1f), true);
        }

        foreach (var p in particles)
        {
            if (!p.t.gameObject.activeSelf) continue;
            p.age += dt;
            if (p.age >= p.life) { p.t.gameObject.SetActive(false); continue; }
            float u = p.age / p.life;
            if (!p.smoke) p.v += Vector3.down * 6f * dt; // sparks fall
            p.v *= 1f - Mathf.Clamp01((p.smoke ? 0.6f : 0.8f) * dt);
            p.t.position += p.v * dt;
            if (cam != null) p.t.rotation = cam.transform.rotation;
            p.t.localScale = Vector3.one * (p.smoke ? p.size * (1f + u * 2.5f) : p.size * (1f - u));
        }
    }

    void Spawn(Vector3 at, Vector3 v, float life, float size, bool smoke)
    {
        Particle p = particles.Find(x => !x.t.gameObject.activeSelf && x.smoke == smoke);
        if (p == null)
        {
            if (particles.Count >= 200) return;
            var go = new GameObject(smoke ? "Flare Smoke" : "Flare Spark");
            go.AddComponent<MeshFilter>().sharedMesh = quad;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = smoke ? smokeMat : sparkMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (smoke)
            {
                var block = new MaterialPropertyBlock();
                block.SetFloat("_Seed", Random.Range(0f, 100f));
                block.SetColor("_Color", new Color(0.32f, 0.12f, 0.1f, 0.35f)); // lit red from inside
                r.SetPropertyBlock(block);
            }
            p = new Particle { t = go.transform, smoke = smoke };
            particles.Add(p);
        }
        if (sparkMat == null && !smoke) return;
        p.t.gameObject.SetActive(true);
        p.t.position = at;
        p.v = v;
        p.age = 0f;
        p.life = life;
        p.size = size;
        p.t.localScale = Vector3.one * size;
        if (Camera.main != null) p.t.rotation = Camera.main.transform.rotation;
    }

    void OnDestroy()
    {
        foreach (var p in particles) if (p.t != null) Destroy(p.t.gameObject);
    }

    // ---------------------------------------------------------------- The burst

    void Burst()
    {
        burst = true;
        burstDone = true;
        burnLeft = burnTime;
        sinceBurst = 0f;

        // The pop: a crack heard all over the forest, and a shell of sparks thrown out in every direction.
        if (popSource != null) popSource.PlayOneShot(ProceduralAudio.FlarePop, 1f);
        for (int i = 0; i < 70; i++)
            Spawn(transform.position, Random.onUnitSphere * Random.Range(10f, 18f), Random.Range(1.2f, 2.2f), Random.Range(0.18f, 0.3f), false);
        var player = FindAnyObjectByType<FirstPersonController>();
        if (player != null) player.AddCameraShake(0.6f);

        // The whole forest lights up red: every entity's hunt starts over.
        foreach (var entity in new List<ShadowEntity>(ShadowEntity.All))
            entity.ResetHunt();
    }
}
