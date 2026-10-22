using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Life round the ponds: lily pads floating out on the water (a few in flower), frogs sitting on them and on
/// the bank, croaking, and clusters of glowing mushrooms round the shore, a soft blue-green light in the dark.
/// The frogs plop into the water if you come close, and come back after a while. When something's near the
/// pond (a scour, the blind one), every frog goes quiet: the silence is the warning. DarkForestGenerator
/// adds this.
/// </summary>
public class PondLife : MonoBehaviour
{
    void Awake() => Tuning.Apply(this);

    [Tooltip("Lily pads per metre of a pond's radius.")]
    public float padsPerMetre = 2.5f;
    public int minFrogs = 3, maxFrogs = 6;
    [Tooltip("Clusters of glowing mushrooms per metre of a pond's radius.")]
    public float mushroomClustersPerMetre = 0.7f;
    public Color mushroomGlow = new Color(0.25f, 0.85f, 0.7f);
    [Tooltip("Frogs jump in when you come this close (running, further).")]
    public float frogFleeDistance = 3f;
    [Tooltip("Frogs go silent when a scour or the blind one is this close to the pond.")]
    public float quietDistance = 35f;
    [Range(0f, 1f)] public float volume = 0.5f;

    class Frog
    {
        public Transform root, throat;
        public AudioSource voice;
        public Vector3 home, pondCenter;
        public float croakTimer, croaking, hop, gone;
        public Vector3 hopFrom, hopTo;
        public bool hopping, underwater;
    }

    class Pond
    {
        public Transform root;
        public Vector3 center;
        public float radius, level;
        public readonly List<Frog> frogs = new List<Frog>();
        public readonly List<Transform> pads = new List<Transform>();
        public readonly List<Vector3> perches = new List<Vector3>(); // where frogs can sit (world, at the pond's canonical spot)
    }

    readonly List<Pond> ponds = new List<Pond>();
    DarkForestGenerator generator;
    Transform player;
    FirstPersonController controller;
    Material padMat, padDarkMat, flowerMat, flowerHeartMat, frogMat, frogBellyMat, frogEyeMat, stalkMat, capMat;

    void Start()
    {
        generator = FindAnyObjectByType<DarkForestGenerator>();
        padMat = MaterialFactory.Lit(new Color(0.07f, 0.12f, 0.04f), 0.35f);
        padDarkMat = MaterialFactory.Lit(new Color(0.05f, 0.08f, 0.03f), 0.3f);
        flowerMat = MaterialFactory.Lit(new Color(0.7f, 0.62f, 0.66f), 0.2f);
        flowerHeartMat = MaterialFactory.Lit(new Color(0.6f, 0.45f, 0.08f), 0.2f);
        frogMat = MaterialFactory.Lit(new Color(0.08f, 0.1f, 0.04f), 0.55f);
        frogBellyMat = MaterialFactory.Lit(new Color(0.22f, 0.2f, 0.12f), 0.4f);
        frogEyeMat = MaterialFactory.Lit(new Color(0.12f, 0.1f, 0.02f), 0.9f);
        stalkMat = MaterialFactory.Lit(new Color(0.3f, 0.32f, 0.3f), 0.1f);
        capMat = MaterialFactory.NoFog(mushroomGlow * 1.2f);

        for (int i = 0; i < Water.PondCount; i++)
        {
            Water.GetPond(i, out Vector3 c, out float radius, out float level);
            var pond = new Pond { center = new Vector3(c.x, level, c.z), radius = radius, level = level };
            pond.root = new GameObject("Pond Life").transform;
            pond.root.SetParent(transform, false);
            pond.root.position = pond.center;
            BuildPads(pond);
            BuildMushrooms(pond);
            int frogs = Random.Range(minFrogs, maxFrogs + 1);
            for (int f = 0; f < frogs; f++) pond.frogs.Add(BuildFrog(pond));
            WorldWrap.RegisterStatic(pond.root);
            ponds.Add(pond);
        }
    }

    // ---------------------------------------------------------------- Lily pads

    void BuildPads(Pond pond)
    {
        int count = Mathf.RoundToInt(pond.radius * padsPerMetre);
        var meshes = new[] { PadMesh(0.32f), PadMesh(0.22f), PadMesh(0.4f) };
        for (int i = 0; i < count; i++)
        {
            // In loose groups, out from the bank but not right in the middle.
            float a = Random.Range(0f, Mathf.PI * 2f), d = pond.radius * Mathf.Lerp(0.35f, 0.92f, Mathf.Sqrt(Random.value));
            var local = new Vector3(Mathf.Cos(a) * d, 0.015f, Mathf.Sin(a) * d);
            var pad = new GameObject("Lily Pad").transform;
            pad.SetParent(pond.root, false);
            pad.localPosition = local;
            pad.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            float s = Random.Range(0.8f, 1.3f);
            pad.localScale = Vector3.one * s;
            pad.gameObject.AddComponent<MeshFilter>().sharedMesh = meshes[Random.Range(0, meshes.Length)];
            var r = pad.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = Random.value < 0.3f ? padDarkMat : padMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            pond.pads.Add(pad);
            if (s > 1f) pond.perches.Add(local + Vector3.up * 0.01f);
            // Now and then, a flower: pale petals round a yellow heart.
            if (Random.value < 0.18f)
            {
                var flower = new GameObject("Lily").transform;
                flower.SetParent(pad, false);
                flower.localPosition = new Vector3(Random.Range(-0.08f, 0.08f), 0.02f, Random.Range(-0.08f, 0.08f));
                for (int k = 0; k < 7; k++)
                {
                    var petal = Part(PrimitiveType.Sphere, flowerMat, flower, Vector3.zero, new Vector3(0.035f, 0.02f, 0.09f));
                    petal.localRotation = Quaternion.Euler(-35f, k * 360f / 7f, 0f);
                    petal.localPosition = petal.localRotation * new Vector3(0f, 0f, 0.035f);
                }
                Part(PrimitiveType.Sphere, flowerHeartMat, flower, new Vector3(0f, 0.02f, 0f), Vector3.one * 0.035f);
            }
        }
    }

    /// <summary>A lily pad: a flat disc with the wedge cut out of it, a little cupped.</summary>
    static Mesh PadMesh(float radius)
    {
        const int steps = 20;
        float notch = Random.Range(0.25f, 0.4f);
        var verts = new List<Vector3> { Vector3.zero };
        var normals = new List<Vector3> { Vector3.up };
        for (int k = 0; k <= steps; k++)
        {
            float a = notch + (Mathf.PI * 2f - notch * 2f) * k / steps;
            float r = radius * Random.Range(0.96f, 1.02f);
            verts.Add(new Vector3(Mathf.Cos(a) * r, 0.01f, Mathf.Sin(a) * r));
            normals.Add(Vector3.up);
        }
        var tris = new List<int>();
        for (int k = 1; k <= steps; k++) tris.AddRange(new[] { 0, k + 1, k });
        var mesh = new Mesh { name = "Lily Pad" };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---------------------------------------------------------------- Mushrooms

    void BuildMushrooms(Pond pond)
    {
        int clusters = Mathf.Max(2, Mathf.RoundToInt(pond.radius * mushroomClustersPerMetre));
        for (int i = 0; i < clusters; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f), d = pond.radius * Random.Range(1.08f, 1.4f);
            Vector3 world = pond.center + new Vector3(Mathf.Cos(a) * d, 0f, Mathf.Sin(a) * d);
            float ground = generator != null ? generator.GetGroundHeight(world.x, world.z) : pond.level;
            if (ground < pond.level + 0.02f) continue; // (under the water)
            var cluster = new GameObject("Glowing Mushrooms").transform;
            cluster.SetParent(pond.root, false);
            cluster.position = new Vector3(world.x, ground, world.z);
            int n = Random.Range(3, 8);
            for (int k = 0; k < n; k++)
            {
                Vector2 o = Random.insideUnitCircle * 0.2f;
                float h = Random.Range(0.04f, 0.13f), cap = Random.Range(0.035f, 0.08f);
                var m = new GameObject("Mushroom").transform;
                m.SetParent(cluster, false);
                m.localPosition = new Vector3(o.x, 0f, o.y);
                m.localRotation = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f));
                Part(PrimitiveType.Cylinder, stalkMat, m, new Vector3(0f, h * 0.5f, 0f), new Vector3(cap * 0.3f, h * 0.5f, cap * 0.3f));
                Part(PrimitiveType.Sphere, capMat, m, new Vector3(0f, h, 0f), new Vector3(cap, cap * 0.45f, cap));
            }
            // A faint glow on the ground and the bank round them.
            var glow = new GameObject("Mushroom Glow").AddComponent<Light>();
            glow.transform.SetParent(cluster, false);
            glow.transform.localPosition = Vector3.up * 0.2f;
            glow.type = LightType.Point;
            glow.color = mushroomGlow;
            glow.range = 2.2f;
            glow.intensity = 0.7f;
            glow.shadows = LightShadows.None;
        }
    }

    // ---------------------------------------------------------------- Frogs

    Frog BuildFrog(Pond pond)
    {
        var f = new Frog { pondCenter = pond.center };
        f.root = new GameObject("Frog").transform;
        f.root.SetParent(pond.root, false);
        float s = Random.Range(0.8f, 1.2f);
        f.root.localScale = Vector3.one * s;
        // Squat body, wide head, bulging eyes, the back legs folded up at its sides, a pale throat that swells.
        Part(PrimitiveType.Sphere, frogMat, f.root, new Vector3(0f, 0.035f, -0.01f), new Vector3(0.08f, 0.05f, 0.1f));
        Part(PrimitiveType.Sphere, frogMat, f.root, new Vector3(0f, 0.045f, 0.045f), new Vector3(0.075f, 0.04f, 0.06f));
        foreach (float side in new[] { -1f, 1f })
        {
            Part(PrimitiveType.Sphere, frogEyeMat, f.root, new Vector3(side * 0.024f, 0.068f, 0.055f), Vector3.one * 0.022f);
            var thigh = Part(PrimitiveType.Sphere, frogMat, f.root, new Vector3(side * 0.04f, 0.025f, -0.035f), new Vector3(0.03f, 0.03f, 0.07f));
            thigh.localRotation = Quaternion.Euler(0f, side * 25f, 0f);
            Part(PrimitiveType.Sphere, frogMat, f.root, new Vector3(side * 0.05f, 0.008f, 0.0f), new Vector3(0.025f, 0.01f, 0.05f));       // foot
            Part(PrimitiveType.Sphere, frogMat, f.root, new Vector3(side * 0.025f, 0.012f, 0.06f), new Vector3(0.015f, 0.02f, 0.015f)); // front leg
        }
        f.throat = Part(PrimitiveType.Sphere, frogBellyMat, f.root, new Vector3(0f, 0.03f, 0.065f), new Vector3(0.04f, 0.03f, 0.03f));
        f.voice = ProceduralAudio.AddSource(f.root.gameObject, null, false, true, volume, 2f, 30f);
        f.croakTimer = Random.Range(1f, 8f);
        Perch(pond, f);
        return f;
    }

    /// <summary>Somewhere to sit: on a lily pad, or on the bank at the water's edge.</summary>
    void Perch(Pond pond, Frog f)
    {
        Vector3 local;
        if (pond.perches.Count > 0 && Random.value < 0.6f) local = pond.perches[Random.Range(0, pond.perches.Count)];
        else
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            Vector3 world = pond.center + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * pond.radius * 1.02f;
            float ground = generator != null ? generator.GetGroundHeight(world.x, world.z) : pond.level;
            local = world - pond.center;
            local.y = Mathf.Max(ground, pond.level) - pond.level + 0.01f;
        }
        f.home = local;
        f.root.localPosition = local;
        f.root.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
        f.root.gameObject.SetActive(true);
        f.underwater = false;
        f.hopping = false;
    }

    void Update()
    {
        if (player == null)
        {
            controller = FindAnyObjectByType<FirstPersonController>();
            if (controller == null) return;
            player = controller.transform;
        }
        float dt = Time.deltaTime;
        foreach (var pond in ponds)
        {
            Vector3 centre = pond.root.position;
            Vector3 toYou = player.position - centre;
            toYou.y = 0f;
            if (toYou.magnitude > pond.radius + 60f) continue; // too far to matter

            bool quiet = SomethingNear(centre);
            // Lily pads rocking gently on the water.
            for (int i = 0; i < pond.pads.Count; i++)
            {
                var pad = pond.pads[i];
                float t = Time.time * 0.6f + i;
                pad.localRotation = Quaternion.Euler(Mathf.Sin(t) * 1.5f, pad.localEulerAngles.y + Mathf.Sin(t * 0.3f) * 2f * dt, Mathf.Cos(t * 0.8f) * 1.5f);
            }
            foreach (var f in pond.frogs) UpdateFrog(pond, f, quiet, dt);
        }
    }

    bool SomethingNear(Vector3 at)
    {
        foreach (var e in ShadowEntity.All)
            if (e != null && e.IsVisible && (WorldWrap.NearestImage(e.transform.position, at) - at).sqrMagnitude < quietDistance * quietDistance) return true;
        foreach (var b in BlindEntity.All)
            if (b != null && b.IsPresent && (WorldWrap.NearestImage(b.transform.position, at) - at).sqrMagnitude < quietDistance * quietDistance * 1.5f) return true;
        return false;
    }

    void UpdateFrog(Pond pond, Frog f, bool quiet, float dt)
    {
        if (f.underwater)
        {
            // Gone under; it comes back up somewhere once you're well away.
            f.gone -= dt;
            Vector3 d = player.position - pond.root.position;
            d.y = 0f;
            if (f.gone <= 0f && d.magnitude > pond.radius + 8f) Perch(pond, f);
            return;
        }
        if (f.hopping)
        {
            // Leaping out over the water, then in.
            f.hop += dt / 0.45f;
            float u = Mathf.Clamp01(f.hop);
            f.root.localPosition = Vector3.Lerp(f.hopFrom, f.hopTo, u) + Vector3.up * Mathf.Sin(u * Mathf.PI) * 0.35f;
            if (u >= 1f)
            {
                f.hopping = false;
                f.underwater = true;
                f.gone = Random.Range(25f, 50f);
                f.root.gameObject.SetActive(false);
                ProceduralAudio.PlayAt(ProceduralAudio.Splash, f.root.position, 0.6f, Random.Range(0.9f, 1.2f), 2f);
            }
            return;
        }

        // Too close: it's off, into the water.
        Vector3 you = player.position - f.root.position;
        you.y = 0f;
        float flee = frogFleeDistance * (controller != null && controller.IsRunning ? 1.8f : 1f);
        if (you.magnitude < flee)
        {
            f.hopping = true;
            f.hop = 0f;
            f.hopFrom = f.root.localPosition;
            Vector3 away = -you.normalized;
            Vector3 toMiddle = -f.root.localPosition;
            toMiddle.y = 0f;
            Vector3 dir = (away + toMiddle.normalized).normalized;
            f.hopTo = f.hopFrom + dir * Random.Range(0.8f, 1.4f);
            f.hopTo.y = -0.05f;
            f.root.localRotation = Quaternion.LookRotation(dir);
            return;
        }

        // Croaking now and then (never when something's near), its throat swelling as it does.
        f.croaking = Mathf.Max(0f, f.croaking - dt);
        float swell = f.croaking > 0f ? Mathf.Abs(Mathf.Sin(f.croaking * 14f)) : 0f;
        f.throat.localScale = new Vector3(0.04f, 0.03f, 0.03f) * (1f + swell * 0.9f);
        if (quiet) return;
        f.croakTimer -= dt;
        if (f.croakTimer > 0f) return;
        f.croakTimer = Random.Range(2.5f, 9f);
        f.croaking = 0.45f;
        f.voice.pitch = Random.Range(0.85f, 1.15f) / Mathf.Max(f.root.localScale.x, 0.5f);
        f.voice.PlayOneShot(ProceduralAudio.FrogCroak, volume * (1f - EntityDread.Hush));
    }

    static Transform Part(PrimitiveType type, Material mat, Transform parent, Vector3 pos, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        var r = go.GetComponent<Renderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        return go.transform;
    }
}
