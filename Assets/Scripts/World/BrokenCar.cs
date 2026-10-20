using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A broken-down car abandoned in the woods: an old rounded sedan, rusting, dirty, one tyre flat, the
/// windscreen smashed in. Open the driver's door and click the headlight switch inside to flick the
/// headlights on. Standing in their beams is safe: the entities won't come into them. The battery is nearly
/// dead, though: the lights dim and flicker as it runs down, and once it's flat that's it (until the run
/// restarts). Turning them off saves what's left.
///
/// Also in it: a couple of batteries in the glove box; a portable radio on the back seat (CarRadio: turn it
/// on and the blind one comes for it from anywhere); and in the trunk, a backpack with a camera whose flash
/// drives the entities off for a while (three shots).
/// DarkForestGenerator parks one somewhere at random.
/// </summary>
public class BrokenCar : MonoBehaviour, SafeZones.IShapedSource
{
    public static readonly List<BrokenCar> All = new List<BrokenCar>();

    [Tooltip("Seconds of headlights the battery has left.")]
    public float batteryTime = 120f;
    [Tooltip("How far the safe beam reaches in front of the car.")]
    public float beamLength = 16f;
    [Tooltip("Half-angle the safe beam spreads at, in degrees.")]
    public float beamSpread = 20f;
    public Color lightColor = new Color(1f, 0.93f, 0.78f);
    public float lightIntensity = 4f;
    [Tooltip("Batteries in the glove box.")]
    public int gloveBoxBatteries = 2;

    public bool LightsOn { get; private set; }
    public float Battery { get; private set; } = 1f;
    public bool IsDead => Battery <= 0f;
    public bool DoorOpen => door != null && door.IsOpen;

    // The car is 4.2m long, nose toward +z; the bumper is at z = 2.1.
    const float FrontZ = 2.1f, Length = 4.2f, HalfWidth = 0.9f;
    const float CabinFront = 0.75f, CabinBack = -1.35f, DoorSplit = -0.4f;
    const float Squareness = 6f; // the body's cross-section: a rounded box

    ShackDoor door, rearDoor, gloveBox, trunk;
    Transform gloveBoxInside, trunkInside;
    CarRadio radio;
    readonly List<Light> headlights = new List<Light>();
    readonly List<Renderer> lamps = new List<Renderer>();
    Renderer dashGlow;
    Material lampOff, lampOn;
    AudioSource sfx;
    float flickerTimer;

    bool SafeZones.ISource.IsActive => LightsOn;
    Vector3 SafeZones.ISource.Center => transform.TransformPoint(new Vector3(0f, 0f, FrontZ + beamLength * 0.5f));
    float SafeZones.ISource.Radius => beamLength * 0.5f;

    bool SafeZones.IShapedSource.Contains(Vector3 point)
    {
        Vector3 local = transform.InverseTransformPoint(point);
        float ahead = local.z - FrontZ + 0.5f; // a little behind the bumper counts too
        if (ahead < 0f || ahead > beamLength) return false;
        float halfWidth = 1.1f + ahead * Mathf.Tan(beamSpread * Mathf.Deg2Rad);
        return Mathf.Abs(local.x) < halfWidth;
    }

    void OnEnable()
    {
        All.Add(this);
        SafeZones.Register(this);
    }

    void OnDisable()
    {
        All.Remove(this);
        SafeZones.Unregister(this);
    }

    /// <summary>Called by the switch inside the car.</summary>
    public void ToggleLights()
    {
        if (IsDead)
        {
            Play(ProceduralAudio.LampClick, 0.6f, 0.7f); // click... nothing
            return;
        }
        LightsOn = !LightsOn;
        Noise.Emit(transform.position, 9f);
        Play(ProceduralAudio.LampClick, 0.8f, LightsOn ? 1.2f : 0.9f);
        if (LightsOn) Play(ProceduralAudio.LampSpark, 0.3f, 1.4f);
    }

    /// <summary>Lights off, battery back, everything shut and restocked (used when the run resets).</summary>
    public void ResetCar()
    {
        LightsOn = false;
        Battery = 1f;
        foreach (var d in new[] { door, rearDoor, gloveBox, trunk }) if (d != null) d.Close();
        if (radio != null) radio.ResetRadio();
        StockItems();
    }

    void Update()
    {
        if (LightsOn)
        {
            Battery -= Time.deltaTime / Mathf.Max(batteryTime, 0.01f);
            if (Battery <= 0f)
            {
                Battery = 0f;
                LightsOn = false;
                Play(ProceduralAudio.LampPowerDown, 0.7f, 1.2f);
            }
        }

        // Dims as the battery runs down, and stutters badly near the end.
        float level = LightsOn ? Mathf.Lerp(0.35f, 1f, Mathf.Clamp01(Battery / 0.5f)) : 0f;
        if (LightsOn && Battery < 0.2f)
        {
            flickerTimer -= Time.deltaTime;
            if (flickerTimer <= 0f)
            {
                flickerTimer = Random.Range(0.05f, 0.6f) * (Battery / 0.2f + 0.2f);
                if (Random.value < 0.4f) Play(ProceduralAudio.LampSpark, 0.15f, Random.Range(1.2f, 1.6f));
            }
            if (flickerTimer < 0.08f) level *= Random.Range(0.1f, 0.5f);
        }

        foreach (var light in headlights)
        {
            light.enabled = level > 0f;
            light.intensity = lightIntensity * level;
        }
        foreach (var lamp in lamps) lamp.sharedMaterial = level > 0f ? lampOn : lampOff;
        if (dashGlow != null) dashGlow.enabled = level > 0f;

        // What's in the glove box and the trunk can only be seen (and taken) with them open.
        if (gloveBoxInside != null) gloveBoxInside.gameObject.SetActive(gloveBox != null && gloveBox.OpenAmount > 0.4f);
        if (trunkInside != null) trunkInside.gameObject.SetActive(trunk != null && trunk.OpenAmount > 0.3f);
    }

    void Play(AudioClip clip, float volume, float pitch)
    {
        if (sfx == null || clip == null) return;
        sfx.pitch = pitch;
        sfx.PlayOneShot(clip, volume);
    }

    /// <summary>Fresh batteries in the glove box and the camera back in the trunk.</summary>
    void StockItems()
    {
        if (gloveBoxInside != null)
        {
            foreach (var old in gloveBoxInside.GetComponentsInChildren<BatteryPickup>(true)) Destroy(old.gameObject);
            for (int i = 0; i < gloveBoxBatteries; i++)
                BatterySpawner.Create(gloveBoxInside, new Vector3(Random.Range(-0.06f, 0.06f), 0f, -0.045f + i * 0.09f), Random.Range(-8f, 8f));
        }
        if (trunkInside != null)
        {
            foreach (var old in trunkInside.GetComponentsInChildren<ItemPickup>(true)) Destroy(old.gameObject);
            ItemPickup.CreateCamera(trunkInside, new Vector3(0.38f, 0f, -0.25f), Random.Range(150f, 210f));
        }
    }

    // ---------------------------------------------------------------- Body shape

    /// <summary>Half the body's width at a point along it.</summary>
    static float SectionHalfWidth(float z) => HalfWidth * (1f - 0.06f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.7f, 2.1f, Mathf.Abs(z))));

    /// <summary>Height of the body's top edge: the hood sloping down to the nose, the beltline, the rear deck.</summary>
    static float SectionTop(float z)
    {
        float top = 1.0f;
        if (z > CabinFront) top = Mathf.Lerp(0.98f, 0.9f, Mathf.InverseLerp(CabinFront, 2f, z));
        else if (z < CabinBack) top = Mathf.Lerp(0.99f, 0.95f, Mathf.InverseLerp(CabinBack, -2f, z));
        return top - 0.07f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.88f, 2.1f, Mathf.Abs(z)));
    }

    /// <summary>Height of the body's bottom edge, arching up over each wheel.</summary>
    static float SectionBottom(float z)
    {
        float bottom = 0.32f + 0.06f * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1.8f, 2.1f, Mathf.Abs(z)));
        const float wheelZ = 1.35f, archRadius = 0.44f, axle = 0.3f;
        foreach (float wz in new[] { wheelZ, -wheelZ })
        {
            float dz = Mathf.Abs(z - wz);
            if (dz < archRadius) bottom = Mathf.Max(bottom, axle + Mathf.Sqrt(archRadius * archRadius - dz * dz));
        }
        return bottom;
    }

    static float SignedPow(float v, float p) => Mathf.Sign(v) * Mathf.Pow(Mathf.Abs(v), p);

    /// <summary>
    /// A piece of the body: the cross-section (a rounded box, between angles a0 and a1, 0 = the right side,
    /// a quarter turn = the top) swept along the car from z0 to z1. Optionally capped at either end, and
    /// optionally two-sided (open pieces you can see inside). Built relative to a pivot (for doors and lids).
    /// </summary>
    static Mesh Loft(float z0, float z1, int steps, float a0, float a1, int around, bool twoSided, bool capStart, bool capEnd, Vector3 pivot)
    {
        var verts = new List<Vector3>();
        var normals = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        float e = 2f / Squareness, ne = 2f - 2f / Squareness;

        Vector3 Point(float z, float a, out Vector3 n)
        {
            float hw = SectionHalfWidth(z), y0 = SectionBottom(z), y1 = SectionTop(z);
            float hh = (y1 - y0) * 0.5f, mid = (y0 + y1) * 0.5f;
            float c = Mathf.Cos(a), s = Mathf.Sin(a);
            n = new Vector3(SignedPow(c, ne) / hw, SignedPow(s, ne) / Mathf.Max(hh, 0.01f), 0f).normalized;
            return new Vector3(hw * SignedPow(c, e), mid + hh * SignedPow(s, e), z) - pivot;
        }

        for (int k = 0; k <= steps; k++)
        {
            float z = Mathf.Lerp(z0, z1, k / (float)steps);
            for (int j = 0; j <= around; j++)
            {
                float a = Mathf.Lerp(a0, a1, j / (float)around);
                verts.Add(Point(z, a, out var n));
                normals.Add(n);
                uvs.Add(new Vector2(z, a));
            }
        }
        int ring = around + 1;
        for (int k = 0; k < steps; k++)
            for (int j = 0; j < around; j++)
            {
                int a = k * ring + j, b = a + 1, c = a + ring, d = c + 1;
                tris.AddRange(new[] { a, b, c, b, d, c });
            }

        void Cap(float z, bool facingForward)
        {
            int centre = verts.Count;
            float y0 = SectionBottom(z), y1 = SectionTop(z);
            verts.Add(new Vector3(0f, (y0 + y1) * 0.5f, z) - pivot);
            Vector3 n = facingForward ? Vector3.forward : Vector3.back;
            normals.Add(n);
            uvs.Add(Vector2.zero);
            for (int j = 0; j <= around; j++)
            {
                verts.Add(Point(z, Mathf.Lerp(a0, a1, j / (float)around), out _));
                normals.Add(n);
                uvs.Add(Vector2.zero);
            }
            for (int j = 0; j < around; j++)
            {
                if (facingForward) tris.AddRange(new[] { centre, centre + 1 + j, centre + 2 + j });
                else tris.AddRange(new[] { centre, centre + 2 + j, centre + 1 + j });
            }
        }
        if (capStart) Cap(z0, false);
        if (capEnd) Cap(z1, true);

        if (twoSided)
        {
            // The inside: a copy of every vertex, normals reversed, wound the other way.
            int count = verts.Count, triCount = tris.Count;
            for (int i = 0; i < count; i++)
            {
                verts.Add(verts[i]);
                normals.Add(-normals[i]);
                uvs.Add(uvs[i]);
            }
            for (int i = 0; i < triCount; i += 3)
                tris.AddRange(new[] { tris[i] + count, tris[i + 2] + count, tris[i + 1] + count });
        }

        var mesh = new Mesh { name = "Car Body" };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    // ---------------------------------------------------------------- Building

    /// <summary>Builds the car around its pivot (on the ground, at the middle of the car).</summary>
    public void Build(System.Random rng)
    {
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        const float Pi = Mathf.PI;

        Color[] paints = { new Color(0.24f, 0.07f, 0.05f), new Color(0.1f, 0.15f, 0.18f), new Color(0.22f, 0.2f, 0.14f), new Color(0.09f, 0.12f, 0.08f), new Color(0.3f, 0.27f, 0.2f) };
        var paint = MaterialFactory.Lit(paints[rng.Next(paints.Length)], 0.35f);
        var rust = MaterialFactory.Lit(new Color(0.22f, 0.1f, 0.04f), 0.05f);
        var grime = MaterialFactory.Lit(new Color(0.08f, 0.07f, 0.05f), 0.02f);
        var moss = MaterialFactory.Lit(new Color(0.06f, 0.09f, 0.04f), 0.02f);
        var dark = MaterialFactory.Lit(new Color(0.025f, 0.025f, 0.025f), 0.15f);
        var trim = MaterialFactory.Lit(new Color(0.05f, 0.05f, 0.05f), 0.3f);
        var tyre = MaterialFactory.Lit(new Color(0.035f, 0.035f, 0.035f), 0.08f);
        var chrome = MaterialFactory.Lit(new Color(0.42f, 0.42f, 0.42f), 0.75f);
        var dullChrome = MaterialFactory.Lit(new Color(0.25f, 0.24f, 0.23f), 0.4f);
        var upholstery = MaterialFactory.LitTextured(ProceduralTextures.Fabric, ProceduralTextures.FabricNormal, new Color(0.2f, 0.15f, 0.11f), 0.05f);
        var carpet = MaterialFactory.LitTextured(ProceduralTextures.Fabric, ProceduralTextures.FabricNormal, new Color(0.07f, 0.06f, 0.05f), 0.02f);
        var glass = MaterialFactory.LitTransparent(Texture2D.whiteTexture, new Color(0.45f, 0.52f, 0.5f, 0.28f), 0.95f);
        var plate = MaterialFactory.Lit(new Color(0.55f, 0.53f, 0.45f), 0.3f);
        lampOff = MaterialFactory.Lit(new Color(0.5f, 0.5f, 0.45f), 0.9f);
        lampOn = MaterialFactory.NoFog(lightColor * 3f);

        // Leaning: a flat front-left tyre drops that corner.
        var body = new GameObject("Body").transform;
        body.SetParent(transform, false);
        body.localRotation = Quaternion.Euler(2.5f, 0f, 3f); // nose and left side down

        // ---- The shell. The front (engine bay and hood) is closed; the cabin and trunk are open-topped tubs,
        // with the doors and the trunk lid as separate hinged pieces of the same shape.
        MeshPart(body, "Front", Loft(CabinFront, FrontZ, 30, 0f, 2f * Pi, 48, false, true, true, Vector3.zero), paint);
        MeshPart(body, "Rear Panel", Loft(-FrontZ, -FrontZ + 0.001f, 1, 0f, 2f * Pi, 48, false, true, false, Vector3.zero), paint);
        MeshPart(body, "Right Side", Loft(-FrontZ, CabinFront, 60, -0.3f * Pi, 0.32f * Pi, 16, true, false, false, Vector3.zero), paint);
        MeshPart(body, "Underside", Loft(-FrontZ, CabinFront, 60, 1.18f * Pi, 1.72f * Pi, 12, true, false, false, Vector3.zero), dark);
        MeshPart(body, "Left Rear Quarter", Loft(-FrontZ, CabinBack, 16, 0.68f * Pi, 1.2f * Pi, 16, true, false, false, Vector3.zero), paint);
        MeshPart(body, "Left Sill", Loft(CabinBack, CabinFront, 30, 1.12f * Pi, 1.2f * Pi, 3, true, false, false, Vector3.zero), paint);

        // Colliders: you can't walk through it, and you can't climb in.
        AddCollider(body, new Vector3(0f, 0.62f, (CabinFront + FrontZ) * 0.5f), new Vector3(1.8f, 0.6f, FrontZ - CabinFront));
        AddCollider(body, new Vector3(0f, 0.45f, (-FrontZ + CabinFront) * 0.5f), new Vector3(1.8f, 0.3f, FrontZ + CabinFront));
        AddCollider(body, new Vector3(0.86f, 0.75f, (-FrontZ + CabinFront) * 0.5f), new Vector3(0.08f, 0.5f, FrontZ + CabinFront));
        AddCollider(body, new Vector3(-0.86f, 0.75f, (-FrontZ + CabinBack) * 0.5f), new Vector3(0.08f, 0.5f, FrontZ + CabinBack));
        AddCollider(body, new Vector3(0f, 0.75f, -FrontZ + 0.04f), new Vector3(1.8f, 0.5f, 0.08f));
        AddCollider(body, new Vector3(0f, 0.8f, CabinBack + 0.05f), new Vector3(1.6f, 0.6f, 0.1f)); // behind the back seat

        // ---- Doors, hinged at their front edges, and the trunk lid, hinged at its front edge.
        door = Door(body, "Driver Door", DoorSplit, CabinFront, paint, chrome, glass, false);
        rearDoor = Door(body, "Rear Door", CabinBack, DoorSplit, paint, chrome, glass, true);
        var lidPivot = new Vector3(0f, SectionTop(CabinBack), CabinBack);
        var lid = new GameObject("Trunk Lid").transform;
        lid.SetParent(body, false);
        lid.localPosition = lidPivot;
        var lidPanel = MeshPart(lid, "Lid", Loft(-FrontZ + 0.01f, CabinBack, 16, 0.3f * Pi, 0.7f * Pi, 16, true, false, false, lidPivot), paint);
        lidPanel.gameObject.AddComponent<BoxCollider>();
        Box(chrome, lid, new Vector3(0f, -0.06f, -0.72f), new Vector3(0.25f, 0.04f, 0.03f)); // the lock
        trunk = lid.gameObject.AddComponent<ShackDoor>();
        trunk.axis = Vector3.right;
        trunk.openAngle = 68f;
        trunk.swingSpeed = 90f;
        trunk.noun = "trunk";

        // ---- Greenhouse: pillars, a rounded roof, glass (the windscreen smashed in, the driver's window down).
        foreach (float side in new[] { -1f, 1f })
        {
            Strut(paint, body, new Vector3(side * 0.8f, 1.0f, 0.72f), new Vector3(side * 0.68f, 1.45f, 0.18f), 0.07f);   // A pillars
            Strut(paint, body, new Vector3(side * 0.82f, 1.0f, DoorSplit), new Vector3(side * 0.73f, 1.45f, DoorSplit - 0.05f), 0.08f); // B
            Strut(paint, body, new Vector3(side * 0.82f, 1.0f, CabinBack), new Vector3(side * 0.7f, 1.45f, -1.0f), 0.12f);  // C pillars
        }
        var roof = MeshPart(body, "Roof", PropModels.SoftBox(new Vector3(1.48f, 0.08f, 1.25f), 0.25f, 0f, 0f, 0f, rng.Next(), 1f), paint);
        roof.localPosition = new Vector3(0f, 1.47f, -0.42f);
        roof.gameObject.AddComponent<BoxCollider>();
        Box(moss, roof, new Vector3(R(-0.3f, 0.3f), 0.041f, R(-0.3f, 0.3f)), new Vector3(R(0.3f, 0.6f), 0.005f, R(0.3f, 0.5f))); // moss and muck on the roof
        Box(grime, roof, new Vector3(R(-0.4f, 0.4f), 0.041f, R(-0.4f, 0.4f)), new Vector3(R(0.3f, 0.5f), 0.004f, R(0.2f, 0.4f)));
        // Rear window and the passenger side windows: grimy glass; the rear quarter one cracked through.
        Pane(glass, body, new Vector3(0.7f, 1.45f, -1.0f), new Vector3(-0.7f, 1.45f, -1.0f), new Vector3(-0.8f, 1.0f, CabinBack), new Vector3(0.8f, 1.0f, CabinBack));
        Pane(glass, body, new Vector3(0.83f, 1.02f, 0.68f), new Vector3(0.83f, 1.02f, DoorSplit), new Vector3(0.74f, 1.43f, DoorSplit), new Vector3(0.7f, 1.43f, 0.22f));
        Pane(glass, body, new Vector3(0.83f, 1.02f, DoorSplit), new Vector3(0.83f, 1.02f, CabinBack + 0.05f), new Vector3(0.72f, 1.42f, -1.02f), new Vector3(0.74f, 1.43f, DoorSplit));
        // What's left of the windscreen: jagged shards round the frame.
        for (int i = 0; i < 9; i++)
        {
            float u = i / 8f;
            Vector3 bottom = new Vector3(Mathf.Lerp(-0.76f, 0.76f, u), 1.0f, 0.72f);
            Vector3 top = new Vector3(Mathf.Lerp(-0.66f, 0.66f, u), 1.45f, 0.2f);
            bool low = i % 2 == 0;
            Vector3 root = low ? bottom : top;
            Vector3 along = new Vector3(0.18f, 0f, 0f);
            Vector3 tip = Vector3.Lerp(root, low ? top : bottom, R(0.1f, 0.35f)) + new Vector3(R(-0.08f, 0.08f), 0f, 0f);
            Shard(glass, body, root - along * 0.5f, root + along * 0.5f, tip);
        }

        // ---- Lights, grille, bumpers, plates.
        foreach (float x in new[] { -0.62f, 0.62f })
        {
            var bezel = Part(PrimitiveType.Cylinder, chrome, body, new Vector3(x, 0.7f, 2.08f), new Vector3(0.27f, 0.025f, 0.27f));
            bezel.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var lamp = Part(PrimitiveType.Sphere, lampOff, body, new Vector3(x, 0.7f, 2.095f), new Vector3(0.22f, 0.22f, 0.07f));
            lamps.Add(lamp.GetComponent<Renderer>());

            var light = new GameObject("Headlight").AddComponent<Light>();
            light.transform.SetParent(body, false);
            light.transform.localPosition = new Vector3(x, 0.7f, 2.2f);
            light.transform.localRotation = Quaternion.Euler(6f, x * 6f, 0f);
            light.type = LightType.Spot;
            light.color = lightColor;
            light.range = beamLength * 1.6f;
            light.spotAngle = 55f;
            light.innerSpotAngle = 25f;
            light.shadows = x < 0f ? LightShadows.Soft : LightShadows.None; // one shadowed beam is enough
            light.enabled = false;
            headlights.Add(light);

            // Tail lights: red lenses in chrome surrounds, one cracked out.
            Box(chrome, body, new Vector3(x * 1.05f, 0.72f, -2.085f), new Vector3(0.34f, 0.16f, 0.02f));
            Box(MaterialFactory.Lit(x < 0f ? new Color(0.12f, 0.02f, 0.02f) : new Color(0.4f, 0.03f, 0.02f), 0.7f), body, new Vector3(x * 1.05f, 0.72f, -2.095f), new Vector3(0.3f, 0.12f, 0.015f));
        }
        Box(dark, body, new Vector3(0f, 0.68f, 2.09f), new Vector3(0.82f, 0.22f, 0.03f)); // grille
        for (int i = 0; i < 5; i++) Box(dullChrome, body, new Vector3(0f, 0.6f + i * 0.04f, 2.105f), new Vector3(0.8f, 0.012f, 0.012f));
        var frontBumper = MeshPart(body, "Front Bumper", PropModels.SoftBox(new Vector3(1.86f, 0.13f, 0.12f), 0.35f, 0f, 0f, 0f, 1, 1f), chrome);
        frontBumper.localPosition = new Vector3(0f, 0.42f, 2.14f);
        frontBumper.localRotation = Quaternion.Euler(0f, 0f, R(2f, 5f)); // bent
        var rearBumper = MeshPart(body, "Rear Bumper", PropModels.SoftBox(new Vector3(1.86f, 0.13f, 0.12f), 0.35f, 0f, 0f, 0f, 2, 1f), chrome);
        rearBumper.localPosition = new Vector3(0f, 0.42f, -2.14f);
        Box(plate, body, new Vector3(0f, 0.6f, -2.105f), new Vector3(0.4f, 0.13f, 0.01f));
        Box(plate, body, new Vector3(0f, 0.42f, 2.205f), new Vector3(0.4f, 0.11f, 0.01f));
        var exhaust = Part(PrimitiveType.Cylinder, dullChrome, body, new Vector3(0.55f, 0.3f, -2.1f), new Vector3(0.06f, 0.12f, 0.06f));
        exhaust.localRotation = Quaternion.Euler(90f, 0f, 0f);
        // Wipers, a hood ornament, the hanging driver's mirror, door handles.
        foreach (float x in new[] { -0.35f, 0.3f }) Box(trim, body, new Vector3(x, 0.99f, 0.82f), new Vector3(0.45f, 0.012f, 0.02f)).localRotation = Quaternion.Euler(0f, x < 0f ? 12f : -18f, 0f);
        Box(chrome, body, new Vector3(0f, 0.93f, 1.95f), new Vector3(0.03f, 0.04f, 0.1f));
        var mirror = Box(chrome, body, new Vector3(-0.95f, 0.88f, 0.68f), new Vector3(0.04f, 0.1f, 0.16f));
        mirror.localRotation = Quaternion.Euler(0f, 0f, -55f); // hanging off by a wire
        Box(chrome, body, new Vector3(0.905f, 0.88f, 0.1f), new Vector3(0.02f, 0.03f, 0.14f));
        Box(chrome, body, new Vector3(0.905f, 0.88f, -0.85f), new Vector3(0.02f, 0.03f, 0.14f));
        // Chrome trim along the sides, rust and dirt.
        Box(dullChrome, body, new Vector3(0.905f, 0.68f, -0.6f), new Vector3(0.01f, 0.025f, 2.9f));
        for (int i = 0; i < 8; i++)
        {
            bool left = R(-1f, 1f) < 0f;
            float z = R(-1.9f, 1.9f);
            Box(rust, body, new Vector3((left ? -1f : 1f) * (SectionHalfWidth(z) + 0.004f), R(0.38f, 0.6f), z), new Vector3(0.01f, R(0.06f, 0.2f), R(0.15f, 0.5f)));
        }
        foreach (float wz in new[] { 1.35f, -1.35f })
            foreach (float side in new[] { -1f, 1f })
                Box(grime, body, new Vector3(side * (HalfWidth + 0.003f), 0.52f, wz + R(-0.5f, 0.5f)), new Vector3(0.01f, 0.18f, R(0.4f, 0.8f))); // mud thrown up off the wheels
        Box(rust, body, new Vector3(R(-0.5f, 0.5f), SectionTop(1.3f) + 0.005f, R(1f, 1.6f)), new Vector3(R(0.3f, 0.6f), 0.01f, R(0.2f, 0.5f)));

        // ---- Wheels: tyres with sidewalls, steel rims and hubcaps (one missing), the front left one flat.
        foreach (var w in new[] { new Vector3(-0.8f, 0.32f, 1.35f), new Vector3(0.8f, 0.32f, 1.35f), new Vector3(-0.8f, 0.32f, -1.35f), new Vector3(0.8f, 0.32f, -1.35f) })
        {
            bool flat = w.x < 0f && w.z > 0f;
            var wheel = new GameObject("Wheel").transform;
            wheel.SetParent(transform, false);
            wheel.localPosition = flat ? w + Vector3.down * 0.11f : w;
            wheel.localRotation = Quaternion.Euler(0f, R(-4f, 4f), 90f);
            float outward = w.x < 0f ? 1f : -1f; // the wheel's local +y points to -x after the roll
            Part(PrimitiveType.Cylinder, tyre, wheel, Vector3.zero, flat ? new Vector3(0.72f, 0.1f, 0.5f) : new Vector3(0.64f, 0.1f, 0.64f));
            Part(PrimitiveType.Cylinder, tyre, wheel, new Vector3(0f, outward * 0.02f, 0f), flat ? new Vector3(0.62f, 0.1f, 0.44f) : new Vector3(0.56f, 0.1f, 0.56f));
            Part(PrimitiveType.Cylinder, dullChrome, wheel, new Vector3(0f, outward * 0.06f, 0f), new Vector3(0.38f, 0.07f, 0.38f));
            if (!(w.x > 0f && w.z < 0f)) Part(PrimitiveType.Sphere, chrome, wheel, new Vector3(0f, outward * 0.11f, 0f), new Vector3(0.3f, 0.05f, 0.3f));
            // Dark wheel tub inside the arch.
            var tub = Part(PrimitiveType.Cylinder, dark, body, new Vector3(Mathf.Sign(w.x) * 0.62f, 0.38f, w.z), new Vector3(0.86f, 0.17f, 0.86f));
            tub.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        // ---- Inside: carpet, seats, dashboard and gauges, steering wheel, glove box, back seat.
        Box(carpet, body, new Vector3(0f, 0.6f, (CabinBack + CabinFront) * 0.5f), new Vector3(1.6f, 0.02f, CabinFront - CabinBack));
        foreach (float x in new[] { -0.4f, 0.4f })
        {
            var cushion = MeshPart(body, "Seat", PropModels.SoftBox(new Vector3(0.55f, 0.16f, 0.55f), 0.35f, 0f, 0.15f, 0.006f, rng.Next(), 0.15f), upholstery);
            cushion.localPosition = new Vector3(x, 0.78f, -0.05f);
            var back = MeshPart(body, "Seat Back", PropModels.SoftBox(new Vector3(0.53f, 0.6f, 0.14f), 0.35f, 0.08f, 0.1f, 0.006f, rng.Next(), 0.15f), upholstery);
            back.localPosition = new Vector3(x, 1.1f, -0.36f);
            back.localRotation = Quaternion.Euler(x < 0f ? -14f : -6f, 0f, 0f);
            var rest = MeshPart(body, "Headrest", PropModels.SoftBox(new Vector3(0.26f, 0.15f, 0.1f), 0.4f, 0f, 0f, 0f, rng.Next(), 0.15f), upholstery);
            rest.localPosition = new Vector3(x, 1.47f, x < 0f ? -0.46f : -0.42f);
        }
        var backCushion = MeshPart(body, "Back Seat", PropModels.SoftBox(new Vector3(1.5f, 0.16f, 0.5f), 0.35f, 0f, 0.1f, 0.006f, rng.Next(), 0.15f), upholstery);
        backCushion.localPosition = new Vector3(0f, 0.74f, -0.98f);
        var backRest = MeshPart(body, "Back Seat Back", PropModels.SoftBox(new Vector3(1.5f, 0.55f, 0.14f), 0.35f, 0f, 0.1f, 0.006f, rng.Next(), 0.15f), upholstery);
        backRest.localPosition = new Vector3(0f, 1.05f, -1.24f);
        backRest.localRotation = Quaternion.Euler(-14f, 0f, 0f);

        // Dashboard, in pieces round the glove box's opening.
        const float dashFace = 0.5f, gloveX = 0.42f, gloveW = 0.4f, gloveY = 0.86f, gloveH = 0.17f;
        Box(trim, body, new Vector3(0f, 1.06f, 0.62f), new Vector3(1.66f, 0.08f, 0.26f)); // top
        Box(trim, body, new Vector3(-0.305f, 0.94f, 0.62f), new Vector3(1.05f, 0.18f, 0.24f)); // driver's side, up to the glove box
        Box(trim, body, new Vector3(0.725f, 0.94f, 0.62f), new Vector3(0.21f, 0.18f, 0.24f));  // the other side of it
        Box(trim, body, new Vector3(gloveX, 0.82f, 0.62f), new Vector3(gloveW + 0.04f, 0.06f, 0.24f));
        Box(dark, body, new Vector3(0f, 0.75f, 0.68f), new Vector3(1.6f, 0.3f, 0.12f)); // footwell wall
        // Gauges under a hood in front of the driver; they glow with the lights on.
        var cluster = Box(dark, body, new Vector3(-0.4f, 1.0f, dashFace - 0.005f), new Vector3(0.42f, 0.13f, 0.02f));
        foreach (float gx in new[] { -0.1f, 0.1f })
        {
            var dial = Part(PrimitiveType.Cylinder, MaterialFactory.Lit(new Color(0.75f, 0.72f, 0.6f), 0.4f), cluster, new Vector3(gx / 0.42f, 0f, -0.6f), new Vector3(0.24f, 0.4f, 0.75f));
            dial.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }
        var glow = Part(PrimitiveType.Cube, MaterialFactory.NoFog(new Color(0.3f, 0.8f, 0.4f) * 0.5f), body, new Vector3(-0.4f, 1.0f, dashFace - 0.02f), new Vector3(0.36f, 0.08f, 0.005f));
        dashGlow = glow.GetComponent<Renderer>();
        dashGlow.enabled = false;
        // Steering wheel: a rim of short segments, three spokes, a column.
        var wheelHub = new GameObject("Steering Wheel").transform;
        wheelHub.SetParent(body, false);
        wheelHub.localPosition = new Vector3(-0.4f, 1.1f, 0.36f);
        wheelHub.localRotation = Quaternion.Euler(-60f, 0f, R(-20f, 20f));
        for (int i = 0; i < 16; i++)
        {
            float a = i / 16f * Pi * 2f;
            var seg = Part(PrimitiveType.Cylinder, trim, wheelHub, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.18f, new Vector3(0.025f, 0.037f, 0.025f));
            seg.localRotation = Quaternion.Euler(0f, 90f - a * Mathf.Rad2Deg, 90f); // lying along the rim
        }
        for (int i = 0; i < 3; i++)
        {
            float a = (90f + i * 120f) * Mathf.Deg2Rad;
            var spoke = Part(PrimitiveType.Cube, trim, wheelHub, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * 0.09f, new Vector3(0.18f, 0.012f, 0.03f));
            spoke.localRotation = Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f);
        }
        Part(PrimitiveType.Cylinder, dullChrome, wheelHub, Vector3.zero, new Vector3(0.07f, 0.02f, 0.07f));
        Part(PrimitiveType.Cylinder, trim, wheelHub, new Vector3(0f, -0.15f, 0f), new Vector3(0.05f, 0.15f, 0.05f));
        // Rear-view mirror, hanging askew.
        var rearView = Box(dark, body, new Vector3(0f, 1.38f, 0.28f), new Vector3(0.22f, 0.06f, 0.02f));
        rearView.localRotation = Quaternion.Euler(10f, 0f, R(-15f, 15f));

        // The headlight switch, a knob on the dash beside the wheel. Click it through the open door.
        var knob = Part(PrimitiveType.Cylinder, chrome, body, new Vector3(-0.68f, 0.98f, dashFace - 0.01f), new Vector3(0.05f, 0.02f, 0.05f));
        knob.localRotation = Quaternion.Euler(90f, 0f, 0f);
        var switchGo = new GameObject("Headlight Switch");
        switchGo.transform.SetParent(body, false);
        switchGo.transform.localPosition = new Vector3(-0.66f, 0.98f, dashFace - 0.04f);
        var trigger = switchGo.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(0.3f, 0.2f, 0.25f);
        switchGo.AddComponent<CarHeadlightSwitch>().car = this;

        // The glove box: a little hollow in the dash behind a flap that drops open.
        var cavity = new GameObject("Glove Box").transform;
        cavity.SetParent(body, false);
        cavity.localPosition = new Vector3(gloveX, gloveY, dashFace + 0.1f);
        Box(dark, cavity, new Vector3(0f, -0.005f, 0f), new Vector3(gloveW, 0.01f, 0.2f));
        Box(dark, cavity, new Vector3(0f, gloveH, 0f), new Vector3(gloveW, 0.01f, 0.2f));
        Box(dark, cavity, new Vector3(0f, gloveH * 0.5f, 0.1f), new Vector3(gloveW, gloveH, 0.01f));
        foreach (float side in new[] { -1f, 1f }) Box(dark, cavity, new Vector3(side * gloveW * 0.5f, gloveH * 0.5f, 0f), new Vector3(0.01f, gloveH, 0.2f));
        gloveBoxInside = new GameObject("Glove Box Contents").transform;
        gloveBoxInside.SetParent(cavity, false);
        gloveBoxInside.localPosition = Vector3.zero;
        var flap = new GameObject("Glove Box Flap").transform;
        flap.SetParent(body, false);
        flap.localPosition = new Vector3(gloveX, gloveY - 0.005f, dashFace - 0.005f);
        var flapPanel = Part(PrimitiveType.Cube, trim, flap, new Vector3(0f, gloveH * 0.5f + 0.005f, 0f), new Vector3(gloveW + 0.02f, gloveH + 0.02f, 0.02f), true);
        Box(chrome, flapPanel, new Vector3(0f, 0.3f, -0.7f), new Vector3(0.2f, 0.08f, 0.6f)); // the latch
        gloveBox = flap.gameObject.AddComponent<ShackDoor>();
        gloveBox.axis = Vector3.right;
        gloveBox.openAngle = -80f;
        gloveBox.swingSpeed = 200f;
        gloveBox.noun = "glove box";

        // The radio on the back seat, behind the driver.
        var radioGo = new GameObject("Radio");
        radioGo.transform.SetParent(body, false);
        radioGo.transform.localPosition = new Vector3(-0.35f, 0.82f, -0.95f);
        radioGo.transform.localRotation = Quaternion.Euler(0f, R(60f, 110f), R(-4f, 4f));
        radio = radioGo.AddComponent<CarRadio>();
        radio.Build();

        // The trunk: carpeted, a spare wheel, and a backpack with the camera beside it.
        Box(carpet, body, new Vector3(0f, 0.61f, (CabinBack - FrontZ) * 0.5f), new Vector3(1.55f, 0.02f, FrontZ + CabinBack - 0.1f));
        var spare = Part(PrimitiveType.Cylinder, tyre, body, new Vector3(-0.45f, 0.67f, -1.75f), new Vector3(0.55f, 0.07f, 0.55f));
        Part(PrimitiveType.Cylinder, dullChrome, spare, new Vector3(0f, 0.5f, 0f), new Vector3(0.6f, 0.3f, 0.6f));
        trunkInside = new GameObject("Trunk Contents").transform;
        trunkInside.SetParent(body, false);
        trunkInside.localPosition = new Vector3(0f, 0.62f, -1.72f);
        var pack = PropModels.Backpack(trunkInside, new Color(0.12f, 0.13f, 0.07f), rng.Next());
        pack.localScale = Vector3.one * 0.82f;
        pack.localPosition = new Vector3(0.05f, 0.1f, 0.22f);
        pack.localRotation = Quaternion.Euler(-90f, 0f, R(-15f, 15f)); // lying on its back

        sfx = ProceduralAudio.AddSource(gameObject, null, false, true, 0.8f, 2f, 30f);
        StockItems();
    }

    /// <summary>A door: its piece of the body's side, a window frame (and maybe glass), a handle, hinged at its front edge.</summary>
    ShackDoor Door(Transform body, string name, float z0, float z1, Material paint, Material chrome, Material glass, bool withGlass)
    {
        var pivot = new Vector3(-SectionHalfWidth(z1), 0f, z1);
        var hinge = new GameObject(name).transform;
        hinge.SetParent(body, false);
        hinge.localPosition = pivot;
        var panel = MeshPart(hinge, "Panel", Loft(z0 + 0.01f, z1 - 0.01f, 12, 0.68f * Mathf.PI, 1.12f * Mathf.PI, 16, true, false, false, pivot), paint);
        var block = panel.gameObject.AddComponent<BoxCollider>();
        // Taller than the panel, so a shut door blocks reaching in through the window too.
        block.center = new Vector3(block.center.x, 0.95f, block.center.z);
        block.size = new Vector3(Mathf.Max(block.size.x, 0.08f), 1.2f, block.size.z);
        // Window frame, and the glass (wound down in the driver's door).
        float top = 1.43f;
        Box(paint, hinge, new Vector3(0.06f, top, (z0 + z1) * 0.5f - z1), new Vector3(0.04f, 0.04f, z1 - z0 - 0.04f));
        if (withGlass)
            Pane(glass, hinge, new Vector3(0.07f, 1.0f, z0 - z1 + 0.04f), new Vector3(0.07f, 1.0f, -0.04f), new Vector3(0.1f, top - 0.02f, -0.06f), new Vector3(0.1f, top - 0.02f, z0 - z1 + 0.3f));
        Box(chrome, hinge, new Vector3(-0.015f, 0.88f, z0 - z1 + 0.18f), new Vector3(0.03f, 0.03f, 0.14f)); // handle
        var d = hinge.gameObject.AddComponent<ShackDoor>();
        d.openAngle = 70f; // swings outward
        d.noun = "door";
        return d;
    }

    /// <summary>A pillar: a thin box from one point to another.</summary>
    void Strut(Material mat, Transform parent, Vector3 a, Vector3 b, float thickness)
    {
        Vector3 d = b - a;
        var t = Part(PrimitiveType.Cube, mat, parent, (a + b) * 0.5f, new Vector3(thickness, d.magnitude, thickness));
        t.localRotation = Quaternion.FromToRotation(Vector3.up, d);
    }

    /// <summary>A flat four-cornered pane of glass, two-sided, no collider.</summary>
    void Pane(Material mat, Transform parent, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
    {
        var mesh = new Mesh { name = "Glass" };
        mesh.vertices = new[] { a, b, c, d, a, b, c, d };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        var t = MeshPart(parent, "Glass", mesh, mat);
        t.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    /// <summary>A jagged triangle of broken glass.</summary>
    void Shard(Material mat, Transform parent, Vector3 a, Vector3 b, Vector3 c)
    {
        var mesh = new Mesh { name = "Shard" };
        mesh.vertices = new[] { a, b, c, a, b, c };
        mesh.triangles = new[] { 0, 1, 2, 3, 5, 4 };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        MeshPart(parent, "Shard", mesh, mat).GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }

    static Transform MeshPart(Transform parent, string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go.transform;
    }

    static void AddCollider(Transform parent, Vector3 center, Vector3 size)
    {
        var go = new GameObject("Collider");
        go.transform.SetParent(parent, false);
        var box = go.AddComponent<BoxCollider>();
        box.center = center;
        box.size = size;
    }

    Transform Box(Material mat, Transform parent, Vector3 pos, Vector3 scale) => Part(PrimitiveType.Cube, mat, parent, pos, scale);

    static Transform Part(PrimitiveType type, Material mat, Transform parent, Vector3 pos, Vector3 scale, bool keepCollider = false)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!keepCollider)
        {
            var col = go.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }
        return go.transform;
    }
}

/// <summary>The headlight switch inside the car. LampInteractor clicks it.</summary>
public class CarHeadlightSwitch : MonoBehaviour
{
    public BrokenCar car;
}
