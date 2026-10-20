using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// An abandoned campsite: a sagging tent, a burnt-out campfire with a few dying embers and a thin trail of
/// smoke (it ignores fog, so you can spot camps far off across the forest), a log to sit on, and a backpack
/// with a flare gun and a bundle of glowsticks beside it, and two tall torches staked out at its edge that
/// a lighter will light (LightableTorch). DarkForestGenerator places a few around the map.
/// </summary>
public class Campsite : MonoBehaviour
{
    public static readonly List<Campsite> All = new List<Campsite>();

    [Header("Smoke")]
    public int puffCount = 34;
    public float puffLife = 16f;
    [Tooltip("How fast smoke leaves the embers; it slows as it rises and spreads.")]
    public float riseSpeed = 2.4f;
    [Tooltip("Colour low down, faintly warmed by the embers...")]
    public Color smokeColorLow = new Color(0.33f, 0.28f, 0.25f, 0.5f);
    [Tooltip("...and higher up, cool grey.")]
    public Color smokeColor = new Color(0.27f, 0.28f, 0.3f, 0.32f);

    [SerializeField] Transform fire;
    [SerializeField] Transform backpack;
    readonly List<Transform> puffs = new List<Transform>();
    readonly List<float> puffAge = new List<float>();
    readonly List<Renderer> puffRenderers = new List<Renderer>();
    readonly List<Renderer> embers = new List<Renderer>();
    readonly List<float> puffSeed = new List<float>();
    readonly List<float> puffSpin = new List<float>();
    readonly List<Vector2> puffDrift = new List<Vector2>();
    MaterialPropertyBlock block;
    WeatherSystem weather;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    /// <summary>Builds the camp at its current position (pivot on the ground at the centre).</summary>
    public void Build(DarkForestGenerator generator, System.Random rng)
    {
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        float Ground(Vector3 local)
        {
            Vector3 w = transform.TransformPoint(local);
            return generator.GetGroundHeight(w.x, w.z) - transform.position.y;
        }

        var canvas = MaterialFactory.Lit(new Color(0.13f, 0.14f, 0.085f), 0.05f);
        var pole = MaterialFactory.Lit(new Color(0.15f, 0.15f, 0.16f), 0.4f);
        var rope = MaterialFactory.Lit(new Color(0.2f, 0.18f, 0.14f), 0.05f);
        var ash = MaterialFactory.Lit(new Color(0.12f, 0.115f, 0.11f), 0f);
        // Faded maroon, navy, olive or dull orange.
        Color[] packColors = { new Color(0.2f, 0.06f, 0.045f), new Color(0.06f, 0.08f, 0.15f), new Color(0.12f, 0.13f, 0.07f), new Color(0.3f, 0.13f, 0.04f) };
        Color packColor = packColors[rng.Next(packColors.Length)];
        var bark = MaterialFactory.LitTextured(ProceduralTextures.Bark, ProceduralTextures.BarkNormal, new Color(0.85f, 0.78f, 0.7f), 0.05f);
        var charred = MaterialFactory.LitTextured(ProceduralTextures.Bark, ProceduralTextures.BarkNormal, new Color(0.18f, 0.16f, 0.15f), 0.1f);
        var endGrain = MaterialFactory.LitTextured(ProceduralTextures.EndGrain, null, Color.white, 0.05f);
        var sooty = generator.RockMaterial != null ? new Material(generator.RockMaterial) : MaterialFactory.Lit(new Color(0.15f, 0.15f, 0.15f));
        MaterialFactory.SetColor(sooty, new Color(0.45f, 0.43f, 0.42f)); // blackened by the fire

        // ---- The tent: a proper canvas A-frame, sides sagging between the poles, back closed, one door flap
        // tied open, guy ropes out to pegs. Tilted to sit on the slope.
        var tent = AnchorOnGround("Tent", new Vector3(-1.7f, 0f, 0.6f), R(-25f, 25f), 1f, 1.4f, Ground);
        const float tentWidth = 0.95f, tentHeight = 1.15f, tentLength = 2.2f;
        MeshPart(tent, "Canvas", BuildTentMesh(tentWidth, tentHeight, tentLength, R(0.08f, 0.16f)), canvas);
        // Door: the left flap hangs closed, the right one is pulled back and tied open.
        MeshPart(tent, "Door Flap", Triangle(new Vector3(-tentWidth, 0f, tentLength * 0.5f), new Vector3(0f, 0f, tentLength * 0.5f + 0.05f), new Vector3(0f, tentHeight, tentLength * 0.5f)), canvas);
        var flapHinge = new GameObject("Open Flap").transform;
        flapHinge.SetParent(tent, false);
        flapHinge.localPosition = new Vector3(tentWidth, 0f, tentLength * 0.5f);
        flapHinge.localRotation = Quaternion.AngleAxis(-R(55f, 80f), new Vector3(-tentWidth, tentHeight, 0f).normalized);
        MeshPart(flapHinge, "Flap", Triangle(Vector3.zero, new Vector3(-tentWidth, tentHeight, 0f), new Vector3(-tentWidth, 0f, 0.03f)), canvas);
        // Poles at each end and the ridge.
        for (int end = -1; end <= 1; end += 2)
            Part(PrimitiveType.Cylinder, pole, tent, new Vector3(0f, tentHeight * 0.5f, end * tentLength * 0.5f), new Vector3(0.025f, tentHeight * 0.5f, 0.025f));
        Part(PrimitiveType.Cylinder, pole, tent, new Vector3(0f, tentHeight, 0f), new Vector3(0.02f, tentLength * 0.5f + 0.05f, 0.02f)).localRotation = Quaternion.Euler(90f, 0f, 0f);
        // Guy ropes from each end of the ridge, and from the sides, down to pegs in the ground.
        foreach (var (from, to) in new[]
        {
            (new Vector3(0f, tentHeight, tentLength * 0.5f), new Vector3(0f, 0f, tentLength * 0.5f + 0.9f)),
            (new Vector3(0f, tentHeight, -tentLength * 0.5f), new Vector3(0f, 0f, -tentLength * 0.5f - 0.9f)),
            (new Vector3(-tentWidth * 0.5f, tentHeight * 0.5f, 0f), new Vector3(-tentWidth - 0.7f, 0f, 0f)),
            (new Vector3(tentWidth * 0.5f, tentHeight * 0.5f, 0f), new Vector3(tentWidth + 0.7f, 0f, 0f)),
        })
        {
            Vector3 d = to - from;
            Part(PrimitiveType.Cylinder, rope, tent, (from + to) * 0.5f, new Vector3(0.008f, d.magnitude * 0.5f, 0.008f)).localRotation = Quaternion.FromToRotation(Vector3.up, d);
            Part(PrimitiveType.Cylinder, pole, tent, to + Vector3.up * 0.04f, new Vector3(0.015f, 0.06f, 0.015f)); // peg
        }
        var tentCollider = tent.gameObject.AddComponent<BoxCollider>();
        tentCollider.center = new Vector3(0f, 0.55f, 0f);
        tentCollider.size = new Vector3(1.7f, 1.1f, 2.2f);

        // ---- The burnt-out campfire: a ring of soot-blackened stones (real rock shapes), each sitting on
        // the ground under it, ash, the charred stubs of logs, a few embers still glowing.
        fire = Anchor("Campfire", new Vector3(1.2f, 0f, -0.4f), 0f, Ground);
        for (int i = 0; i < 10; i++)
        {
            float a = i / 10f * Mathf.PI * 2f + R(-0.12f, 0.12f);
            var local = new Vector3(Mathf.Cos(a) * 0.58f, 0f, Mathf.Sin(a) * 0.58f);
            float size = R(0.22f, 0.32f);
            local.y = GroundUnder(fire, local, Ground) - size * 0.2f; // bedded into the dirt
            var stone = MeshPart(fire, "Stone", generator.MakeRockMesh(size), sooty);
            stone.localPosition = local;
            stone.localRotation = Quaternion.Euler(R(-10f, 10f), R(0f, 360f), R(-10f, 10f));
            stone.localScale = new Vector3(size * R(0.9f, 1.2f), size * R(0.6f, 0.85f), size * R(0.85f, 1.1f));
        }
        Part(PrimitiveType.Cylinder, ash, fire, new Vector3(0f, GroundUnder(fire, Vector3.zero, Ground) + 0.01f, 0f), new Vector3(0.9f, 0.012f, 0.9f));
        for (int i = 0; i < 5; i++)
        {
            // Logs that burned down where they fell: short, blackened, angled into the middle.
            float a = i / 5f * Mathf.PI * 2f + R(-0.3f, 0.3f);
            float len = R(0.35f, 0.55f);
            var log = Part(PrimitiveType.Cylinder, charred, fire, new Vector3(Mathf.Cos(a) * 0.18f, GroundUnder(fire, Vector3.zero, Ground) + 0.06f, Mathf.Sin(a) * 0.18f), new Vector3(0.07f, len * 0.5f, 0.07f));
            log.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f) * Quaternion.Euler(0f, 0f, 90f - R(5f, 18f));
        }
        var emberMat = MaterialFactory.Unlit(new Color(0.9f, 0.2f, 0.03f));
        for (int i = 0; i < 7; i++)
        {
            var ember = Part(PrimitiveType.Sphere, emberMat, fire, new Vector3(R(-0.22f, 0.22f), GroundUnder(fire, Vector3.zero, Ground) + 0.05f, R(-0.22f, 0.22f)), Vector3.one * R(0.03f, 0.06f));
            embers.Add(ember.GetComponent<Renderer>());
        }

        // ---- A log to sit on: bark all round, sawn ends showing their rings, lying along the ground.
        var seat = AnchorOnGround("Log Seat", new Vector3(1.4f, 0f, 1.2f), R(-30f, 30f), 0.85f, 0.25f, Ground);
        const float seatLength = 1.7f, seatRadius = 0.2f;
        var seatLog = Part(PrimitiveType.Cylinder, bark, seat, new Vector3(0f, seatRadius * 0.85f, 0f), new Vector3(seatRadius * 2f, seatLength * 0.5f, seatRadius * 2f), true);
        seatLog.localRotation = Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(0f, R(0f, 360f), 0f);
        for (int end = -1; end <= 1; end += 2)
        {
            var cap = Part(PrimitiveType.Cylinder, endGrain, seat, new Vector3(end * (seatLength * 0.5f + 0.004f), seatRadius * 0.85f, 0f), new Vector3(seatRadius * 1.98f, 0.004f, seatRadius * 1.98f));
            cap.localRotation = Quaternion.Euler(0f, 0f, 90f) * Quaternion.Euler(0f, R(0f, 360f), 0f);
        }

        // Sometimes a portable radio left on the log: turn it on and the blind one comes for it.
        if (rng.NextDouble() < radioChance)
        {
            var radioGo = new GameObject("Radio");
            radioGo.transform.SetParent(seat, false);
            radioGo.transform.localPosition = new Vector3(R(-0.5f, 0.5f), seatRadius * 0.85f + seatRadius - 0.02f, 0f);
            radioGo.transform.localRotation = Quaternion.Euler(0f, R(-30f, 30f) + 180f, 0f);
            radio = radioGo.AddComponent<CarRadio>();
            radio.Build();
        }

        // The backpack, slumped over, with the flare gun beside it.
        backpack = AnchorOnGround("Backpack", new Vector3(-0.2f, 0f, -1.4f), R(0f, 360f), 0.25f, 0.2f, Ground);
        var packModel = PropModels.Backpack(backpack, packColor, rng.Next());
        packModel.localRotation = Quaternion.Euler(R(-14f, -8f), 0f, R(-6f, 6f)); // slumped back on its straps
        var packBlock = packModel.gameObject.AddComponent<BoxCollider>(); // something to bump into
        packBlock.center = new Vector3(0f, 0.28f, 0f);
        packBlock.size = new Vector3(0.36f, 0.56f, 0.26f);

        // Smoke puffs, recycled as they rise.
        Material smoke = MakeSmokeMaterial();
        for (int i = 0; i < puffCount; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Smoke";
            go.transform.SetParent(fire, false);
            RemoveCollider(go);
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = smoke;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            puffs.Add(go.transform);
            puffAge.Add(i / (float)puffCount * puffLife);
            puffRenderers.Add(renderer);
            puffSeed.Add(Random.Range(0f, 100f));
            puffSpin.Add(Random.Range(-12f, 12f));
            puffDrift.Add(Random.insideUnitCircle);
        }

        // Two tall torches staked out at the edge of the camp, unlit: a lighter will light them.
        torches.Clear();
        foreach (var spot in new[] { new Vector3(2.7f, 0f, 0.3f), new Vector3(0.6f, 0f, 2.6f) })
        {
            var at = new Vector3(spot.x + R(-0.3f, 0.3f), 0f, spot.z + R(-0.3f, 0.3f));
            at.y = Ground(at) - 0.05f;
            torches.Add(LightableTorch.Standing(transform, at, R(0f, 360f)));
        }

        SpawnItems();
    }

    readonly List<LightableTorch> torches = new List<LightableTorch>();

    /// <summary>Puts a fresh flare gun and glowsticks by the backpack (used when the run resets).</summary>
    public void ResetCampsite()
    {
        SpawnItems();
        if (radio != null) radio.ResetRadio();
        foreach (var torch in torches) if (torch != null) torch.ResetTorch();
    }

    [Range(0f, 1f), Tooltip("Chance of a portable radio on the log (the blind one comes for it when it plays).")]
    public float radioChance = 0.3f;
    CarRadio radio;

    void SpawnItems()
    {
        if (backpack == null) return;
        foreach (var old in backpack.GetComponentsInChildren<ItemPickup>())
        {
            if (Application.isPlaying) Destroy(old.gameObject);
            else DestroyImmediate(old.gameObject);
        }
        foreach (var old in backpack.GetComponentsInChildren<BatteryPickup>())
        {
            if (Application.isPlaying) Destroy(old.gameObject);
            else DestroyImmediate(old.gameObject);
        }
        // A couple of spare batteries spilled out of the pack.
        BatterySpawner.Create(backpack, new Vector3(0.08f, 0f, 0.42f), Random.Range(0f, 360f));
        BatterySpawner.Create(backpack, new Vector3(-0.2f, 0f, -0.45f), Random.Range(0f, 360f));
        ItemPickup.CreateFlareGun(backpack, new Vector3(0.35f, 0.02f, 0.15f), Random.Range(0f, 360f));
        ItemPickup.CreateGlowsticks(backpack, new Vector3(-0.35f, 0.02f, -0.05f), Random.Range(0f, 360f));
        // Now and then, the car's key fob, dropped by the pack.
        if (Random.value < keyFobChance)
            ItemPickup.CreateKeyFob(backpack, new Vector3(0.3f, 0f, -0.4f), Random.Range(0f, 360f));
    }

    [Range(0f, 1f), Tooltip("Chance of the car's key fob lying by the backpack.")]
    public float keyFobChance = 0.2f;

    void Update()
    {
        if (puffs.Count == 0) return;
        var cam = Camera.main;
        if (cam == null) return;
        if (block == null) block = new MaterialPropertyBlock();
        if (weather == null) weather = FindAnyObjectByType<WeatherSystem>();

        // The wind bends the column over as it rises.
        Vector3 wind = weather != null ? weather.WindVector * (0.4f + weather.CurrentWind) : Vector3.zero;
        Vector3 localWind = fire.InverseTransformDirection(wind);
        float dt = Time.deltaTime;

        for (int i = 0; i < puffs.Count; i++)
        {
            float age = puffAge[i] + dt;
            if (age >= puffLife)
            {
                // Recycled at the embers as a fresh, different wisp.
                age -= puffLife;
                puffSeed[i] = Random.Range(0f, 100f);
                puffSpin[i] = Random.Range(-12f, 12f);
                puffDrift[i] = Random.insideUnitCircle;
            }
            puffAge[i] = age;

            float t = age / puffLife;
            // Rises quickly off the embers, slowing as it cools and spreads.
            float height = riseSpeed * puffLife * (1f - Mathf.Exp(-age / (puffLife * 0.45f))) * 0.45f;
            // Curls and wanders more the higher it gets; the wind bends the column over.
            float curl = t * t * 1.4f;
            Vector3 wander = new Vector3(
                Mathf.Sin(age * 0.7f + puffSeed[i]) * curl + puffDrift[i].x * t * 0.8f,
                0f,
                Mathf.Cos(age * 0.55f + puffSeed[i] * 1.3f) * curl + puffDrift[i].y * t * 0.8f);
            Transform puff = puffs[i];
            puff.localPosition = new Vector3(0f, 0.25f + height, 0f) + wander + localWind * (height * height * 0.04f + height * 0.2f);
            puff.localScale = Vector3.one * Mathf.Lerp(0.35f, 5f, 1f - (1f - t) * (1f - t)); // spreads fast, then slowly
            // Face the camera, turning slowly as it billows.
            puff.rotation = cam.transform.rotation * Quaternion.Euler(0f, 0f, puffSeed[i] * 3.6f + age * puffSpin[i]);

            // Thick and faintly warm over the fire, thinning to cool grey wisps.
            Color c = Color.Lerp(smokeColorLow, smokeColor, Mathf.Clamp01(t * 2.5f));
            c.a *= Mathf.Clamp01(t * 8f) * Mathf.Pow(1f - t, 1.6f);
            block.SetColor("_Color", c);
            block.SetFloat("_Seed", puffSeed[i]);
            puffRenderers[i].SetPropertyBlock(block);
        }

        // Embers pulse faintly.
        float glow = 0.6f + 0.4f * Mathf.PerlinNoise(Time.time * 1.5f, transform.position.x);
        foreach (var e in embers) e.transform.localScale = Vector3.one * 0.045f * glow;
    }

    static Material MakeSmokeMaterial()
    {
        Shader shader = Shader.Find("Horror/Smoke");
        if (shader == null) return MaterialFactory.Unlit(new Color(0.3f, 0.3f, 0.3f));
        return new Material(shader) { renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent };
    }

    // ---------------------------------------------------------------- Building helpers

    /// <summary>
    /// A spot for something with a footprint (halfX by halfZ): tilted to match the ground under its corners
    /// and set at their average height, so it sits on a slope instead of floating off or sinking into it.
    /// </summary>
    Transform AnchorOnGround(string name, Vector3 local, float yaw, float halfX, float halfZ, System.Func<Vector3, float> ground)
    {
        var t = new GameObject(name).transform;
        t.SetParent(transform, false);
        Quaternion turn = Quaternion.Euler(0f, yaw, 0f);
        float H(float x, float z) => ground(local + turn * new Vector3(x, 0f, z));
        float front = H(0f, halfZ), back = H(0f, -halfZ), left = H(-halfX, 0f), right = H(halfX, 0f);
        float pitch = -Mathf.Atan2(front - back, halfZ * 2f) * Mathf.Rad2Deg;
        float roll = Mathf.Atan2(right - left, halfX * 2f) * Mathf.Rad2Deg;
        t.localPosition = new Vector3(local.x, (front + back + left + right) * 0.25f - 0.02f, local.z);
        t.localRotation = turn * Quaternion.Euler(pitch, 0f, roll);
        return t;
    }

    /// <summary>Height of the ground under a point given in a child anchor's own space, in that space.</summary>
    float GroundUnder(Transform anchor, Vector3 local, System.Func<Vector3, float> ground)
    {
        Vector3 inCamp = transform.InverseTransformPoint(anchor.TransformPoint(new Vector3(local.x, 0f, local.z)));
        float h = ground(inCamp);
        return h - anchor.localPosition.y;
    }

    Transform MeshPart(Transform parent, string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go.transform;
    }

    /// <summary>A two-sided triangle.</summary>
    static Mesh Triangle(Vector3 a, Vector3 b, Vector3 c)
    {
        var mesh = new Mesh { name = "Canvas Panel" };
        mesh.vertices = new[] { a, b, c, a, c, b };
        mesh.triangles = new[] { 0, 1, 2, 3, 4, 5 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// A ridge tent, two-sided: two sloping sides that sag between the end poles, and a closed back gable.
    /// The front is left open for the door flaps. Ridge along z, base half-width `width`.
    /// </summary>
    static Mesh BuildTentMesh(float width, float height, float length, float sag)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        const int along = 8, up = 5;
        for (int side = -1; side <= 1; side += 2)
        {
            int first = verts.Count;
            for (int i = 0; i <= along; i++)
            {
                float u = i / (float)along;
                for (int j = 0; j <= up; j++)
                {
                    float v = j / (float)up;
                    float droop = sag * Mathf.Sin(Mathf.PI * u) * Mathf.Sin(Mathf.PI * v); // slack canvas between the poles
                    verts.Add(new Vector3(side * (width * (1f - v) - droop * 0.6f), height * v - droop, (u - 0.5f) * length));
                }
            }
            for (int i = 0; i < along; i++)
                for (int j = 0; j < up; j++)
                {
                    int a = first + i * (up + 1) + j, b = a + up + 1, c = a + 1, d = b + 1;
                    if (side < 0) tris.AddRange(new[] { a, b, c, c, b, d });
                    else tris.AddRange(new[] { a, c, b, c, d, b });
                }
        }
        int g = verts.Count;
        verts.Add(new Vector3(-width, 0f, -length * 0.5f));
        verts.Add(new Vector3(width, 0f, -length * 0.5f));
        verts.Add(new Vector3(0f, height, -length * 0.5f));
        tris.AddRange(new[] { g, g + 1, g + 2 });

        // The inside faces: a second copy of every vertex wound the other way, so each face has its own normal
        // (shared vertices would average the two sides' normals to nothing and light it wrong).
        int count = verts.Count, triCount = tris.Count;
        for (int i = 0; i < count; i++) verts.Add(verts[i]);
        for (int t = 0; t < triCount; t += 3) tris.AddRange(new[] { tris[t] + count, tris[t + 2] + count, tris[t + 1] + count });

        var mesh = new Mesh { name = "Tent" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    Transform Anchor(string name, Vector3 local, float yaw, System.Func<Vector3, float> ground)
    {
        var t = new GameObject(name).transform;
        t.SetParent(transform, false);
        t.localPosition = new Vector3(local.x, ground(local), local.z);
        t.localRotation = Quaternion.Euler(0f, yaw, 0f);
        return t;
    }

    Transform Box(Material mat, Transform parent, Vector3 pos, Vector3 scale, bool keepCollider)
        => Part(PrimitiveType.Cube, mat, parent, pos, scale, keepCollider);

    Transform Part(PrimitiveType type, Material mat, Transform parent, Vector3 pos, Vector3 scale, bool keepCollider = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!keepCollider) RemoveCollider(go);
        return go.transform;
    }

    static void RemoveCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (Application.isPlaying) Destroy(col);
        else DestroyImmediate(col);
    }
}
