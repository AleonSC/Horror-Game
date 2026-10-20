using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Detailed procedural props shared by more than one script: the campsite backpack and the flare gun (lying
/// in the world as a pickup, and held in the hand). Soft, padded shapes are built from SoftBox meshes;
/// hard parts from primitives. Nothing here has colliders.
/// </summary>
public static class PropModels
{
    // ---------------------------------------------------------------- Backpack

    /// <summary>
    /// A hiking pack standing on its base (origin at the bottom centre, front facing +z): a stuffed, slightly
    /// lumpy body of worn woven nylon, a lid strapped down over the top, a zipped front pocket, side pockets,
    /// compression straps with buckles, padded shoulder straps and back panel, a haul loop, and a rolled
    /// foam mat strapped on top.
    /// </summary>
    public static Transform Backpack(Transform parent, Color color, int seed)
    {
        var rng = new System.Random(seed);
        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        var root = new GameObject("Backpack Model").transform;
        root.SetParent(parent, false);

        var fabric = MaterialFactory.LitTextured(ProceduralTextures.Fabric, ProceduralTextures.FabricNormal, color * 1.6f, 0.12f);
        var darkFabric = MaterialFactory.LitTextured(ProceduralTextures.Fabric, ProceduralTextures.FabricNormal, new Color(0.07f, 0.07f, 0.075f), 0.1f);
        var webbing = MaterialFactory.Lit(new Color(0.03f, 0.03f, 0.032f), 0.2f);
        var plastic = MaterialFactory.Lit(new Color(0.05f, 0.05f, 0.05f), 0.55f);
        var metal = MaterialFactory.Lit(new Color(0.45f, 0.45f, 0.47f), 0.7f);
        var foam = MaterialFactory.Lit(new Color(0.1f, 0.14f, 0.07f), 0.05f);

        // The main body: stuffed full, bulging, narrowing a little to the top.
        MeshObj(root, "Body", SoftBox(new Vector3(0.34f, 0.5f, 0.22f), 0.35f, 0.12f, 0.08f, 0.012f, seed, 0.12f), fabric,
            new Vector3(0f, 0.25f, 0f), Vector3.zero);
        // Padded back panel.
        MeshObj(root, "Back Panel", SoftBox(new Vector3(0.28f, 0.4f, 0.035f), 0.4f, 0.05f, 0f, 0.004f, seed + 1, 0.12f), darkFabric,
            new Vector3(0f, 0.25f, -0.104f), Vector3.zero);
        // The lid, strapped down over the top and hanging over the front.
        MeshObj(root, "Lid", SoftBox(new Vector3(0.33f, 0.085f, 0.25f), 0.5f, 0f, 0.15f, 0.008f, seed + 2, 0.12f), fabric,
            new Vector3(0f, 0.5f, 0.018f), new Vector3(9f + R(-3f, 3f), 0f, R(-3f, 3f)));
        // Front pocket, zipped.
        MeshObj(root, "Front Pocket", SoftBox(new Vector3(0.25f, 0.2f, 0.07f), 0.4f, 0.06f, 0.12f, 0.006f, seed + 3, 0.12f), fabric,
            new Vector3(0f, 0.15f, 0.112f), new Vector3(-4f, 0f, 0f));
        Prim(PrimitiveType.Cube, webbing, root, new Vector3(0f, 0.247f, 0.141f), new Vector3(0.21f, 0.006f, 0.006f), new Vector3(-12f, 0f, 0f));
        Prim(PrimitiveType.Cube, metal, root, new Vector3(0.085f, 0.232f, 0.15f), new Vector3(0.008f, 0.026f, 0.004f), new Vector3(-8f, 0f, R(-10f, 10f)));

        for (int side = -1; side <= 1; side += 2)
        {
            // Side pockets of darker mesh.
            MeshObj(root, "Side Pocket", SoftBox(new Vector3(0.06f, 0.17f, 0.15f), 0.4f, 0.1f, 0.1f, 0.005f, seed + 4 + side, 0.1f), darkFabric,
                new Vector3(side * 0.172f, 0.12f, 0.005f), new Vector3(0f, 0f, side * -3f));
            // Compression strap round the side, with its buckle.
            Prim(PrimitiveType.Cube, webbing, root, new Vector3(side * 0.17f, 0.33f, 0f), new Vector3(0.006f, 0.024f, 0.21f), new Vector3(0f, 0f, side * 4f));
            Prim(PrimitiveType.Cube, plastic, root, new Vector3(side * 0.175f, 0.33f, 0.04f), new Vector3(0.008f, 0.03f, 0.035f), Vector3.zero);

            // The lid's straps down the front to buckles on the pocket.
            Prim(PrimitiveType.Cube, webbing, root, new Vector3(side * 0.085f, 0.39f, 0.112f), new Vector3(0.024f, 0.17f, 0.005f), new Vector3(-6f, 0f, 0f));
            Prim(PrimitiveType.Cube, plastic, root, new Vector3(side * 0.085f, 0.3f, 0.12f), new Vector3(0.032f, 0.022f, 0.01f), Vector3.zero);

            // Padded shoulder straps: an S down the back, bulging out at the middle, webbing down to the base.
            const int segments = 7;
            Vector3 prev = StrapPoint(side, 0f);
            for (int i = 1; i <= segments; i++)
            {
                Vector3 next = StrapPoint(side, i / (float)segments);
                Vector3 d = next - prev;
                var seg = MeshObj(root, "Shoulder Strap", SoftBox(new Vector3(0.06f, d.magnitude * 1.15f, 0.022f), 0.5f, 0f, 0f, 0.002f, seed + 10 + i, 0.1f), darkFabric,
                    (prev + next) * 0.5f, Vector3.zero);
                seg.localRotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
                prev = next;
            }
            Vector3 end = StrapPoint(side, 1f), corner = new Vector3(side * 0.15f, 0.02f, -0.1f);
            var tail = Prim(PrimitiveType.Cube, webbing, root, (end + corner) * 0.5f, new Vector3(0.02f, (corner - end).magnitude, 0.004f), Vector3.zero);
            tail.localRotation = Quaternion.FromToRotation(Vector3.up, (corner - end).normalized);

            // Straps round the mat on top.
            var band = Prim(PrimitiveType.Cylinder, webbing, root, new Vector3(side * 0.12f, 0.6f, -0.025f), new Vector3(0.128f, 0.012f, 0.128f), new Vector3(0f, 0f, 90f));
            band.name = "Mat Strap";
        }

        // Haul loop at the top of the back.
        Prim(PrimitiveType.Cube, webbing, root, new Vector3(0f, 0.505f, -0.118f), new Vector3(0.05f, 0.02f, 0.006f), new Vector3(30f, 0f, 0f));
        // The rolled foam mat, strapped on top.
        Prim(PrimitiveType.Cylinder, foam, root, new Vector3(0f, 0.6f, -0.025f), new Vector3(0.12f, 0.21f, 0.12f), new Vector3(0f, 0f, 90f));
        return root;
    }

    static Vector3 StrapPoint(int side, float t)
    {
        float y = Mathf.Lerp(0.46f, 0.1f, t);
        float z = -0.125f - 0.045f * Mathf.Sin(Mathf.PI * Mathf.Pow(t, 0.8f));
        float x = side * Mathf.Lerp(0.06f, 0.1f, t);
        return new Vector3(x, y, z);
    }

    // ---------------------------------------------------------------- Flare gun

    /// <summary>
    /// A break-action orange plastic flare pistol (origin at the bottom of the frame, barrel along +z): a wide
    /// ribbed barrel with a dark bore, breech and hinge, hammer and latch, a raked grip with textured panels
    /// and a lanyard ring, trigger and guard, a warning label down the barrel. Muzzle marks the bore's mouth.
    /// </summary>
    public static Transform FlareGun(Transform parent, bool castShadows, out Transform muzzle)
    {
        var root = new GameObject("Flare Gun Model").transform;
        root.SetParent(parent, false);

        var orange = MaterialFactory.Lit(new Color(0.8f, 0.27f, 0.04f), 0.35f);
        var deepOrange = MaterialFactory.Lit(new Color(0.55f, 0.17f, 0.03f), 0.15f);
        var black = MaterialFactory.Lit(new Color(0.035f, 0.035f, 0.035f), 0.35f);
        var bore = MaterialFactory.Unlit(new Color(0.01f, 0.01f, 0.01f));
        var label = MaterialFactory.Lit(new Color(0.75f, 0.72f, 0.65f), 0.2f);
        var parts = new List<Transform>();
        Transform P(PrimitiveType type, Material mat, Transform under, Vector3 pos, Vector3 scale, Vector3 euler)
        {
            var t = Prim(type, mat, under, pos, scale, euler);
            parts.Add(t);
            return t;
        }

        const float axisY = 0.065f;
        // Barrel, muzzle band, the dark bore, and ribs along the top and sides.
        P(PrimitiveType.Cylinder, orange, root, new Vector3(0f, axisY, 0.085f), new Vector3(0.044f, 0.075f, 0.044f), new Vector3(90f, 0f, 0f));
        P(PrimitiveType.Cylinder, orange, root, new Vector3(0f, axisY, 0.155f), new Vector3(0.049f, 0.006f, 0.049f), new Vector3(90f, 0f, 0f));
        P(PrimitiveType.Cylinder, orange, root, new Vector3(0f, axisY, 0.018f), new Vector3(0.048f, 0.006f, 0.048f), new Vector3(90f, 0f, 0f));
        P(PrimitiveType.Cylinder, bore, root, new Vector3(0f, axisY, 0.1612f), new Vector3(0.029f, 0.0012f, 0.029f), new Vector3(90f, 0f, 0f));
        P(PrimitiveType.Cube, orange, root, new Vector3(0f, axisY + 0.022f, 0.088f), new Vector3(0.008f, 0.006f, 0.13f), Vector3.zero);
        for (int side = -1; side <= 1; side += 2)
            P(PrimitiveType.Cube, deepOrange, root, new Vector3(side * 0.021f, axisY - 0.006f, 0.088f), new Vector3(0.004f, 0.005f, 0.12f), Vector3.zero);
        P(PrimitiveType.Cube, label, root, new Vector3(0.0222f, axisY + 0.004f, 0.092f), new Vector3(0.001f, 0.016f, 0.05f), Vector3.zero);

        // Breech block and frame, the hinge pin under the barrel, lower frame forward to the guard.
        P(PrimitiveType.Cube, orange, root, new Vector3(0f, 0.06f, -0.008f), new Vector3(0.034f, 0.05f, 0.05f), Vector3.zero);
        P(PrimitiveType.Cube, orange, root, new Vector3(0f, 0.038f, 0.03f), new Vector3(0.028f, 0.012f, 0.035f), Vector3.zero);
        P(PrimitiveType.Cylinder, black, root, new Vector3(0f, 0.042f, 0.012f), new Vector3(0.01f, 0.019f, 0.01f), new Vector3(0f, 0f, 90f));
        // Latch and hammer.
        P(PrimitiveType.Cube, deepOrange, root, new Vector3(0f, 0.088f, -0.01f), new Vector3(0.02f, 0.01f, 0.022f), Vector3.zero);
        P(PrimitiveType.Cube, black, root, new Vector3(0f, 0.09f, -0.037f), new Vector3(0.011f, 0.026f, 0.012f), new Vector3(-28f, 0f, 0f));

        // The grip, raked back, with textured side panels, a butt cap and a lanyard ring.
        var grip = new GameObject("Grip").transform;
        grip.SetParent(root, false);
        grip.localPosition = new Vector3(0f, 0.045f, -0.02f);
        grip.localRotation = Quaternion.Euler(20f, 0f, 0f);
        P(PrimitiveType.Cube, orange, grip, new Vector3(0f, -0.05f, -0.004f), new Vector3(0.03f, 0.1f, 0.042f), Vector3.zero);
        P(PrimitiveType.Cube, orange, grip, new Vector3(0f, -0.102f, -0.004f), new Vector3(0.034f, 0.01f, 0.047f), Vector3.zero);
        for (int side = -1; side <= 1; side += 2)
            P(PrimitiveType.Cube, deepOrange, grip, new Vector3(side * 0.0155f, -0.052f, -0.004f), new Vector3(0.002f, 0.072f, 0.032f), Vector3.zero);
        P(PrimitiveType.Cylinder, black, grip, new Vector3(0f, -0.11f, -0.018f), new Vector3(0.012f, 0.002f, 0.012f), new Vector3(0f, 0f, 90f));

        // Trigger guard and trigger.
        P(PrimitiveType.Cube, orange, root, new Vector3(0f, 0.022f, 0.04f), new Vector3(0.008f, 0.026f, 0.006f), new Vector3(-10f, 0f, 0f));
        P(PrimitiveType.Cube, orange, root, new Vector3(0f, 0.009f, 0.018f), new Vector3(0.008f, 0.005f, 0.044f), Vector3.zero);
        P(PrimitiveType.Cube, black, root, new Vector3(0f, 0.024f, 0.014f), new Vector3(0.006f, 0.022f, 0.006f), new Vector3(18f, 0f, 0f));

        foreach (var t in parts)
            t.GetComponent<Renderer>().shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;

        muzzle = new GameObject("Muzzle").transform;
        muzzle.SetParent(root, false);
        muzzle.localPosition = new Vector3(0f, axisY, 0.165f);
        return root;
    }

    // ---------------------------------------------------------------- Camera

    /// <summary>
    /// An old film camera (origin at its base, lens facing +z): a leatherette body with metal top plates,
    /// a big lens barrel, a viewfinder, the shutter button and wind lever, a strap, and a flash gun mounted
    /// on top with a pale reflector. Flash marks the front of the flash, where the light comes from.
    /// </summary>
    public static Transform FilmCamera(Transform parent, bool castShadows, out Transform flash)
    {
        var root = new GameObject("Camera Model").transform;
        root.SetParent(parent, false);
        var leather = MaterialFactory.LitTextured(ProceduralTextures.Fabric, ProceduralTextures.FabricNormal, new Color(0.06f, 0.055f, 0.05f), 0.15f);
        var metal = MaterialFactory.Lit(new Color(0.5f, 0.5f, 0.5f), 0.7f);
        var black = MaterialFactory.Lit(new Color(0.03f, 0.03f, 0.03f), 0.4f);
        var glassMat = MaterialFactory.Lit(new Color(0.05f, 0.08f, 0.12f), 0.95f);
        var reflector = MaterialFactory.Lit(new Color(0.85f, 0.85f, 0.8f), 0.6f);
        var parts = new List<Transform>();
        Transform P(PrimitiveType type, Material mat, Transform under, Vector3 pos, Vector3 scale, Vector3 euler)
        {
            var t = Prim(type, mat, under, pos, scale, euler);
            parts.Add(t);
            return t;
        }

        P(PrimitiveType.Cube, leather, root, new Vector3(0f, 0.045f, 0f), new Vector3(0.14f, 0.075f, 0.05f), Vector3.zero);    // body
        P(PrimitiveType.Cube, metal, root, new Vector3(0f, 0.088f, 0f), new Vector3(0.142f, 0.014f, 0.052f), Vector3.zero);    // top plate
        P(PrimitiveType.Cube, metal, root, new Vector3(0f, 0.004f, 0f), new Vector3(0.142f, 0.008f, 0.052f), Vector3.zero);    // base plate
        P(PrimitiveType.Cube, metal, root, new Vector3(0f, 0.1f, 0f), new Vector3(0.045f, 0.022f, 0.045f), Vector3.zero);      // viewfinder hump
        P(PrimitiveType.Cylinder, metal, root, new Vector3(0f, 0.045f, 0.035f), new Vector3(0.056f, 0.012f, 0.056f), new Vector3(90f, 0f, 0f));  // lens mount
        P(PrimitiveType.Cylinder, black, root, new Vector3(0f, 0.045f, 0.06f), new Vector3(0.05f, 0.02f, 0.05f), new Vector3(90f, 0f, 0f));      // lens barrel
        P(PrimitiveType.Cylinder, metal, root, new Vector3(0f, 0.045f, 0.077f), new Vector3(0.052f, 0.004f, 0.052f), new Vector3(90f, 0f, 0f)); // front ring
        P(PrimitiveType.Cylinder, glassMat, root, new Vector3(0f, 0.045f, 0.0795f), new Vector3(0.036f, 0.001f, 0.036f), new Vector3(90f, 0f, 0f)); // glass
        P(PrimitiveType.Cylinder, metal, root, new Vector3(0.045f, 0.1f, 0f), new Vector3(0.012f, 0.006f, 0.012f), Vector3.zero);  // shutter button
        P(PrimitiveType.Cube, metal, root, new Vector3(0.055f, 0.098f, -0.012f), new Vector3(0.03f, 0.004f, 0.008f), new Vector3(0f, 20f, 0f)); // wind lever
        foreach (float x in new[] { -0.072f, 0.072f })
            P(PrimitiveType.Cube, metal, root, new Vector3(x, 0.07f, 0f), new Vector3(0.006f, 0.012f, 0.006f), Vector3.zero); // strap lugs

        // The flash gun on the hot shoe: a stem and a head with a pale reflector facing forward.
        P(PrimitiveType.Cube, black, root, new Vector3(-0.035f, 0.105f, 0f), new Vector3(0.02f, 0.02f, 0.02f), Vector3.zero);
        P(PrimitiveType.Cube, black, root, new Vector3(-0.035f, 0.14f, 0.005f), new Vector3(0.06f, 0.05f, 0.035f), Vector3.zero);
        P(PrimitiveType.Cube, reflector, root, new Vector3(-0.035f, 0.14f, 0.0235f), new Vector3(0.052f, 0.04f, 0.002f), Vector3.zero);

        foreach (var t in parts)
            t.GetComponent<Renderer>().shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;

        flash = new GameObject("Flash").transform;
        flash.SetParent(root, false);
        flash.localPosition = new Vector3(-0.035f, 0.14f, 0.03f);
        return root;
    }

    // ---------------------------------------------------------------- Torch and lighter

    /// <summary>
    /// A hand-made torch (origin at the bottom of the handle, standing up +y): a rough, slightly tapering
    /// wooden handle with twine wrapped round the grip, and a head of tar-soaked rags bound on with wire.
    /// FlameAt marks the top of the head, where the fire sits.
    /// </summary>
    public static Transform Torch(Transform parent, bool castShadows, out Transform flameAt)
    {
        var root = new GameObject("Torch Model").transform;
        root.SetParent(parent, false);
        var wood = MaterialFactory.LitTextured(ProceduralTextures.Bark, ProceduralTextures.BarkNormal, new Color(0.55f, 0.42f, 0.3f), 0.08f);
        var twine = MaterialFactory.LitTextured(ProceduralTextures.Fabric, ProceduralTextures.FabricNormal, new Color(0.45f, 0.36f, 0.24f), 0.05f);
        var rag = MaterialFactory.LitTextured(ProceduralTextures.Fabric, ProceduralTextures.FabricNormal, new Color(0.13f, 0.1f, 0.07f), 0.45f); // shiny with tar
        var wire = MaterialFactory.Lit(new Color(0.3f, 0.28f, 0.26f), 0.6f);
        var parts = new List<Transform>();

        // The handle: thicker at the grip, narrowing up toward the head, a knot or two.
        parts.Add(Prim(PrimitiveType.Cylinder, wood, root, new Vector3(0f, 0.16f, 0f), new Vector3(0.036f, 0.16f, 0.036f), Vector3.zero));
        parts.Add(Prim(PrimitiveType.Cylinder, wood, root, new Vector3(0.002f, 0.42f, 0f), new Vector3(0.03f, 0.12f, 0.03f), new Vector3(0f, 0f, 1.5f)));
        parts.Add(Prim(PrimitiveType.Sphere, wood, root, new Vector3(0.014f, 0.36f, 0.006f), new Vector3(0.016f, 0.022f, 0.016f), Vector3.zero));
        // Twine round the grip: a few turns, a little uneven.
        for (int i = 0; i < 6; i++)
            parts.Add(Prim(PrimitiveType.Cylinder, twine, root, new Vector3(0f, 0.06f + i * 0.032f, 0f), new Vector3(0.04f, 0.008f, 0.04f), new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f))));
        // The head: rags wound round and round, lumpy, bulging.
        for (int i = 0; i < 4; i++)
            parts.Add(MeshObj(root, "Rag", SoftBox(new Vector3(0.075f - i * 0.004f, 0.06f, 0.07f + i * 0.003f), 0.6f, 0.05f, 0.15f, 0.006f, 31 + i, 0.05f), rag,
                new Vector3(Random.Range(-0.004f, 0.004f), 0.54f + i * 0.035f, Random.Range(-0.004f, 0.004f)), new Vector3(Random.Range(-8f, 8f), i * 40f, Random.Range(-8f, 8f))));
        // Bound on with wire.
        foreach (float y in new[] { 0.53f, 0.63f })
            parts.Add(Prim(PrimitiveType.Cylinder, wire, root, new Vector3(0f, y, 0f), new Vector3(0.078f, 0.003f, 0.078f), Vector3.zero));

        foreach (var t in parts)
            t.GetComponent<Renderer>().shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        flameAt = new GameObject("Flame At").transform;
        flameAt.SetParent(root, false);
        flameAt.localPosition = new Vector3(0f, 0.6f, 0f);
        return root;
    }

    /// <summary>
    /// A flip-top petrol lighter (origin at its base): a brushed steel case, a lid hinged at one side (Lid,
    /// swung open about its local z, to -110 degrees, when it's lit), the chimney with its holes, the flint wheel, the wick.
    /// FlameAt marks the top of the wick.
    /// </summary>
    public static Transform Lighter(Transform parent, bool castShadows, out Transform lid, out Transform flameAt)
    {
        var root = new GameObject("Lighter Model").transform;
        root.SetParent(parent, false);
        var steel = MaterialFactory.Lit(new Color(0.62f, 0.62f, 0.64f), 0.82f);
        var dark = MaterialFactory.Lit(new Color(0.08f, 0.08f, 0.08f), 0.4f);
        var wickMat = MaterialFactory.Lit(new Color(0.85f, 0.8f, 0.7f), 0.1f);
        var parts = new List<Transform>();
        const float w = 0.038f, d = 0.013f, caseH = 0.04f, lidH = 0.017f;

        parts.Add(MeshObj(root, "Case", SoftBox(new Vector3(w, caseH, d), 0.25f, 0f, 0f, 0f, 41, 0.05f), steel, new Vector3(0f, caseH * 0.5f, 0f), Vector3.zero));
        parts.Add(Prim(PrimitiveType.Cube, dark, root, new Vector3(0f, caseH + 0.0005f, 0f), new Vector3(w * 0.96f, 0.001f, d * 0.92f), Vector3.zero)); // the seam
        // The chimney, a perforated box round the wick, the flint wheel beside it.
        parts.Add(Prim(PrimitiveType.Cube, steel, root, new Vector3(-0.006f, caseH + 0.008f, 0f), new Vector3(0.016f, 0.016f, 0.011f), Vector3.zero));
        for (int row = 0; row < 2; row++)
            for (int col = 0; col < 3; col++)
                foreach (float side in new[] { -1f, 1f })
                    parts.Add(Prim(PrimitiveType.Cube, dark, root, new Vector3(-0.0115f + col * 0.0055f, caseH + 0.005f + row * 0.006f, side * 0.0056f), new Vector3(0.0022f, 0.0022f, 0.0004f), Vector3.zero));
        parts.Add(Prim(PrimitiveType.Cylinder, dark, root, new Vector3(0.008f, caseH + 0.008f, 0f), new Vector3(0.008f, 0.0035f, 0.008f), new Vector3(90f, 0f, 0f)));
        parts.Add(Prim(PrimitiveType.Cylinder, wickMat, root, new Vector3(-0.006f, caseH + 0.016f, 0f), new Vector3(0.003f, 0.003f, 0.003f), Vector3.zero));

        // The lid, hinged along the back edge of the case's top.
        lid = new GameObject("Lid").transform;
        lid.SetParent(root, false);
        lid.localPosition = new Vector3(w * 0.5f, caseH, 0f);
        parts.Add(MeshObj(lid, "Lid Shell", SoftBox(new Vector3(w, lidH, d), 0.25f, 0f, 0f, 0f, 42, 0.05f), steel, new Vector3(-w * 0.5f, lidH * 0.5f + 0.0005f, 0f), Vector3.zero));
        parts.Add(Prim(PrimitiveType.Cylinder, steel, root, new Vector3(w * 0.5f + 0.0012f, caseH, 0f), new Vector3(0.003f, 0.006f, 0.003f), new Vector3(90f, 0f, 0f))); // the hinge

        foreach (var t in parts)
            t.GetComponent<Renderer>().shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        flameAt = new GameObject("Flame At").transform;
        flameAt.SetParent(root, false);
        flameAt.localPosition = new Vector3(-0.006f, caseH + 0.018f, 0f);
        return root;
    }

    // ---------------------------------------------------------------- Key fob

    /// <summary>A car key fob (origin at its base, lying flat): a black plastic body, rubber buttons, a little
    /// red LED, a key ring through one end with the car key on it.</summary>
    public static Transform KeyFob(Transform parent, bool castShadows, out Renderer led)
    {
        var root = new GameObject("Key Fob Model").transform;
        root.SetParent(parent, false);
        var plastic = MaterialFactory.Lit(new Color(0.03f, 0.03f, 0.035f), 0.55f);
        var rubber = MaterialFactory.Lit(new Color(0.1f, 0.1f, 0.11f), 0.2f);
        var metal = MaterialFactory.Lit(new Color(0.55f, 0.55f, 0.55f), 0.8f);
        var parts = new List<Transform>
        {
            MeshObj(root, "Body", SoftBox(new Vector3(0.035f, 0.012f, 0.06f), 0.45f, 0f, 0f, 0f, 7, 0.05f), plastic, new Vector3(0f, 0.006f, 0f), Vector3.zero),
        };
        for (int i = 0; i < 3; i++)
            parts.Add(Prim(PrimitiveType.Cylinder, rubber, root, new Vector3(0f, 0.0125f, -0.012f + i * 0.013f), new Vector3(0.012f, 0.0015f, 0.009f), Vector3.zero));
        var ledPart = Prim(PrimitiveType.Sphere, MaterialFactory.Lit(new Color(0.3f, 0.02f, 0.02f), 0.8f), root, new Vector3(0f, 0.0125f, 0.024f), new Vector3(0.004f, 0.002f, 0.004f), Vector3.zero);
        led = ledPart.GetComponent<Renderer>();
        parts.Add(ledPart);
        // The ring, a few segments round, and the key hanging off it.
        for (int i = 0; i < 10; i++)
        {
            float a = i / 10f * Mathf.PI * 2f;
            parts.Add(Prim(PrimitiveType.Cylinder, metal, root, new Vector3(Mathf.Cos(a) * 0.012f, 0.004f, -0.042f + Mathf.Sin(a) * 0.012f), new Vector3(0.002f, 0.004f, 0.002f), new Vector3(90f, -a * Mathf.Rad2Deg, 0f)));
        }
        parts.Add(Prim(PrimitiveType.Cube, metal, root, new Vector3(0.01f, 0.002f, -0.072f), new Vector3(0.012f, 0.002f, 0.045f), new Vector3(0f, 20f, 0f)));
        foreach (var t in parts)
            t.GetComponent<Renderer>().shadowCastingMode = castShadows
                ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        return root;
    }

    // ---------------------------------------------------------------- Building blocks

    /// <summary>
    /// A soft, rounded box (a superellipsoid) for padded and stuffed fabric shapes. Squareness 1 is a
    /// sphere, smaller is boxier. Taper narrows it toward the top, bulge fattens the middle, lumpiness pushes
    /// the surface in and out a little (metres). UVs run round it in metres / uvScale, for tiling fabric.
    /// </summary>
    public static Mesh SoftBox(Vector3 size, float squareness, float taper, float bulge, float lumpiness, int seed, float uvScale)
    {
        const int around = 24, up = 14;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        float e = squareness;
        float perimeter = Mathf.PI * (size.x + size.z) * 0.5f;
        float ox = seed * 13.1f % 100f, oy = seed * 7.7f % 100f;
        for (int j = 0; j <= up; j++)
        {
            float theta = Mathf.Lerp(-Mathf.PI * 0.5f, Mathf.PI * 0.5f, j / (float)up);
            float ct = Mathf.Cos(theta), st = Mathf.Sin(theta);
            for (int i = 0; i <= around; i++)
            {
                float phi = Mathf.Lerp(-Mathf.PI, Mathf.PI, i / (float)around);
                float cp = Mathf.Cos(phi), sp = Mathf.Sin(phi);
                var p = new Vector3(
                    size.x * 0.5f * SignedPow(ct, e) * SignedPow(cp, e),
                    size.y * 0.5f * SignedPow(st, e),
                    size.z * 0.5f * SignedPow(ct, e) * SignedPow(sp, e));
                float h = p.y / Mathf.Max(size.y, 0.0001f) + 0.5f; // 0 at the bottom, 1 at the top
                float widen = (1f - taper * h) * (1f + bulge * Mathf.Sin(Mathf.PI * h));
                p.x *= widen;
                p.z *= widen;
                if (lumpiness > 0f && j > 0 && j < up)
                {
                    Vector3 n = new Vector3(p.x / size.x, p.y / size.y, p.z / size.z).normalized;
                    float lump = Mathf.PerlinNoise(ox + p.x * 9f + p.z * 5f, oy + p.y * 9f) - 0.5f;
                    p += n * lump * 2f * lumpiness;
                }
                verts.Add(p);
                uvs.Add(new Vector2(i / (float)around * perimeter / uvScale, h * size.y / uvScale));
            }
        }
        for (int j = 0; j < up; j++)
            for (int i = 0; i < around; i++)
            {
                int a = j * (around + 1) + i, b = a + 1, c = a + around + 1, d = c + 1;
                tris.AddRange(new[] { a, c, b, b, c, d });
            }
        var mesh = new Mesh { name = "Soft Box" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    static float SignedPow(float x, float p) => Mathf.Sign(x) * Mathf.Pow(Mathf.Abs(x), p);

    static Transform MeshObj(Transform parent, string name, Mesh mesh, Material mat, Vector3 pos, Vector3 euler)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go.transform;
    }

    static Transform Prim(PrimitiveType type, Material mat, Transform parent, Vector3 pos, Vector3 scale, Vector3 euler)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        var col = go.GetComponent<Collider>();
        if (Application.isPlaying) Object.Destroy(col);
        else Object.DestroyImmediate(col);
        return go.transform;
    }
}
