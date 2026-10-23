using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The scour's body, sculpted: smooth meshes instead of sticks and balls. A gaunt, too-tall figure, standing
/// upright: a starved torso (ribs showing down its sides, a sunken belly, every vertebra standing out down its
/// back, shoulder blades jutting), a long neck strung with tendons, a long skull with a heavy brow, sunken
/// cheeks and deep eye sockets, long thin arms with tendons down the forearms, long hands
/// with jointed, clawed fingers, long legs, and narrow feet with long toes. Limbs run smoothly from one
/// segment into the next (rounded ends overlapping at the joints).
///
/// Every mesh is built once, in the units of a 2.4m figure (+z forward, +y up), and shared by every scour.
/// Limb meshes run down their own -y from where they join on, so they hang from a pivot. Meshes are named
/// "Scour ..." and listed with EnemyMeshes, so the model editor can show and save them.
/// The layout (where each joint is) is here too, for ShadowEntity to put them together.
/// </summary>
public static class ScourBody
{
    // ---------------------------------------------------------------- The layout (2.4m figure)

    public static readonly Vector3 Hip = new Vector3(0.1f, 1.1f, -0.02f);        // (x for the right side; mirrored)
    public static readonly Vector3 Knee = new Vector3(0.11f, 0.6f, 0.05f);
    public static readonly Vector3 Ankle = new Vector3(0.1f, 0.09f, -0.03f);
    public static readonly Vector3 Shoulder = new Vector3(0.22f, 1.86f, 0f);
    public static readonly Vector3 Elbow = new Vector3(0.27f, 1.36f, -0.05f);
    public static readonly Vector3 Wrist = new Vector3(0.25f, 0.86f, 0.04f);
    public static readonly Vector3 NeckBase = new Vector3(0f, 1.93f, 0.04f);
    public static readonly Vector3 HeadJoint = new Vector3(0f, 2.12f, 0.24f);
    public static readonly Vector3[] Spine =
    {
        new Vector3(0f, 1.02f, -0.02f), new Vector3(0f, 1.12f, -0.03f), new Vector3(0f, 1.3f, -0.06f),
        new Vector3(0f, 1.47f, -0.07f), new Vector3(0f, 1.66f, -0.04f), new Vector3(0f, 1.82f, 0f), NeckBase,
    };

    public static float ThighLength => (Knee - Hip).magnitude;
    public static float ShinLength => (Ankle - Knee).magnitude;
    public static float UpperArmLength => (Elbow - Shoulder).magnitude;
    public static float ForearmLength => (Wrist - Elbow).magnitude;
    public const float PalmLength = 0.11f;
    public static readonly float[] FingerBones = { 0.075f, 0.06f, 0.055f };
    public static readonly float[] ThumbBones = { 0.05f, 0.045f };

    // ---------------------------------------------------------------- The meshes

    public static Mesh Torso, Neck, Skull, Jaw, UpperArm, Forearm, Palm, FingerBase, FingerMid, FingerTip, Thigh, Shin, Foot, Toe;

    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() => Torso = null;

    /// <summary>Builds the meshes the first time they're wanted.</summary>
    public static void Ensure()
    {
        if (Torso != null) return;
        Torso = Name(BuildTorso(), "Scour Torso");
        Neck = Name(BuildNeck(), "Scour Neck");
        Skull = Name(BuildSkull(), "Scour Skull");
        Jaw = Name(BuildJaw(), "Scour Jaw");
        UpperArm = Name(Limb(UpperArmLength, t => Keys(t, 0f, 0.042f, 0.25f, 0.036f, 0.7f, 0.032f, 1f, 0.036f), t => 1f, Tendons(0.04f, 0.4f, 0.9f)), "Scour Upper Arm");
        Forearm = Name(Limb(ForearmLength, t => Keys(t, 0f, 0.036f, 0.2f, 0.034f, 0.75f, 0.024f, 1f, 0.022f), t => 0.85f, Tendons(0.09f, 0.1f, 0.85f)), "Scour Forearm");
        Palm = Name(Limb(PalmLength, t => Keys(t, 0f, 0.022f, 0.4f, 0.034f, 1f, 0.03f), t => 0.42f, Tendons(0.05f, 0.3f, 1f), 10, 12), "Scour Palm");
        FingerBase = Name(Limb(FingerBones[0], t => Keys(t, 0f, 0.011f, 0.15f, 0.0125f, 0.6f, 0.009f, 1f, 0.0105f), t => 0.9f, null, 8, 8), "Scour Finger Base");
        FingerMid = Name(Limb(FingerBones[1], t => Keys(t, 0f, 0.0095f, 0.2f, 0.0105f, 0.7f, 0.008f, 1f, 0.009f), t => 0.9f, null, 8, 8), "Scour Finger Mid");
        FingerTip = Name(Limb(FingerBones[2], t => Keys(t, 0f, 0.0085f, 0.3f, 0.008f, 0.85f, 0.004f, 1f, 0.0005f), t => 0.8f, null, 8, 8), "Scour Finger Tip");
        Thigh = Name(Limb(ThighLength, t => Keys(t, 0f, 0.075f, 0.3f, 0.062f, 0.8f, 0.045f, 1f, 0.05f), t => 0.95f, Tendons(0.03f, 0.5f, 0.95f)), "Scour Thigh");
        Shin = Name(Limb(ShinLength, t => Keys(t, 0f, 0.05f, 0.1f, 0.045f, 0.3f, 0.05f, 0.8f, 0.03f, 1f, 0.03f), t => 0.9f, ShinDetail), "Scour Shin");
        Foot = Name(BuildFoot(), "Scour Foot");
        Toe = Name(Limb(0.09f, t => Keys(t, 0f, 0.012f, 0.3f, 0.011f, 0.85f, 0.006f, 1f, 0.001f), t => 0.75f, null, 8, 8), "Scour Toe");
        foreach (var m in new[] { Torso, Neck, Skull, Jaw, UpperArm, Forearm, Palm, FingerBase, FingerMid, FingerTip, Thigh, Shin, Foot, Toe })
            EnemyMeshes.Register(m);
    }

    static Mesh Name(Mesh m, string name) { m.name = name; return m; }

    /// <summary>A smooth curve through (t, value) keys: Keys(t, t0, v0, t1, v1, ...).</summary>
    static float Keys(float t, params float[] kv)
    {
        if (t <= kv[0]) return kv[1];
        for (int i = 2; i < kv.Length; i += 2)
            if (t <= kv[i])
            {
                float u = Mathf.InverseLerp(kv[i - 2], kv[i], t);
                return Mathf.Lerp(kv[i - 1], kv[i + 1], u * u * (3f - 2f * u));
            }
        return kv[kv.Length - 1];
    }

    static float Bump(float x, float centre, float width) => Mathf.Exp(-Mathf.Pow((x - centre) / width, 2f));

    /// <summary>Two cords standing out along a limb (front and back), between from and to along it.</summary>
    static System.Func<float, float, float> Tendons(float height, float from, float to) =>
        (t, a) => 1f + height * Mathf.Pow(Mathf.Abs(Mathf.Sin(a * 2f + 0.6f)), 12f) * Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(from, from + 0.15f, t)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(to - 0.15f, to, t)));

    /// <summary>The shin: the bone's sharp edge down the front, the calf hanging behind.</summary>
    static float ShinDetail(float t, float a)
    {
        float front = Mathf.Max(0f, Mathf.Sin(a)), back = Mathf.Max(0f, -Mathf.Sin(a));
        return 1f + 0.12f * Mathf.Pow(front, 16f) * Mathf.SmoothStep(0.1f, 0.3f, t) - 0.04f * front
               + 0.25f * back * back * Bump(t, 0.28f, 0.14f);
    }

    // ---------------------------------------------------------------- Building blocks

    /// <summary>
    /// A tube swept down a path: at each step a ring (radius rx sideways, rz forward, times detail(t, angle),
    /// angle 0 sideways and pi/2 forward), closed at both ends.
    /// </summary>
    static Mesh Loft(Vector3[] path, System.Func<float, float> rx, System.Func<float, float> rz,
        System.Func<float, float, float> detail, int rings, int sides, Vector3 forward)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        int cols = sides + 1;
        for (int k = 0; k <= rings; k++)
        {
            float t = k / (float)rings;
            Vector3 c = Along(path, t);
            Vector3 tangent = (Along(path, Mathf.Min(1f, t + 0.01f)) - Along(path, Mathf.Max(0f, t - 0.01f))).normalized;
            Vector3 n = Vector3.ProjectOnPlane(forward, tangent);
            if (n.sqrMagnitude < 1e-6f) n = Vector3.ProjectOnPlane(Vector3.up, tangent);
            n.Normalize();
            Vector3 b = Vector3.Cross(n, tangent).normalized;
            for (int i = 0; i <= sides; i++)
            {
                float a = i / (float)sides * Mathf.PI * 2f;
                float d = detail != null ? detail(t, a) : 1f;
                verts.Add(c + (b * Mathf.Cos(a) * rx(t) + n * Mathf.Sin(a) * rz(t)) * d);
            }
        }
        for (int k = 0; k < rings; k++)
            for (int i = 0; i < sides; i++)
            {
                int p = k * cols + i, q = p + cols; // (wound so the faces look outward)
                tris.Add(p); tris.Add(p + 1); tris.Add(q);
                tris.Add(p + 1); tris.Add(q + 1); tris.Add(q);
            }
        // The ends.
        foreach (int k in new[] { 0, rings })
        {
            int centre = verts.Count;
            Vector3 sum = Vector3.zero;
            for (int i = 0; i < sides; i++) sum += verts[k * cols + i];
            verts.Add(sum / sides);
            for (int i = 0; i < sides; i++)
            {
                int p = k * cols + i;
                if (k == 0) { tris.Add(centre); tris.Add(p + 1); tris.Add(p); }
                else { tris.Add(centre); tris.Add(p); tris.Add(p + 1); }
            }
        }
        return Finish(verts, tris, cols, rings + 1);
    }

    /// <summary>A point along a path of points, smoothly (Catmull-Rom), t from 0 to 1.</summary>
    static Vector3 Along(Vector3[] path, float t)
    {
        if (path.Length == 2) return Vector3.Lerp(path[0], path[1], t);
        float f = Mathf.Clamp01(t) * (path.Length - 1);
        int i = Mathf.Min(Mathf.FloorToInt(f), path.Length - 2);
        float u = f - i;
        Vector3 p0 = path[Mathf.Max(i - 1, 0)], p1 = path[i], p2 = path[i + 1], p3 = path[Mathf.Min(i + 2, path.Length - 1)];
        return 0.5f * (2f * p1 + (-p0 + p2) * u + (2f * p0 - 5f * p1 + 4f * p2 - p3) * u * u + (-p0 + 3f * p1 - 3f * p2 + p3) * u * u * u);
    }

    /// <summary>
    /// A limb segment hanging down its own -y, length long, its ends rounded off and reaching a little past
    /// the joints (by the radius there), so each segment runs smoothly into the next with no seam or knob.
    /// </summary>
    static Mesh Limb(float length, System.Func<float, float> radius, System.Func<float, float> squash,
        System.Func<float, float, float> detail, int rings = 16, int sides = 14)
    {
        float r0 = radius(0f), r1 = radius(1f), total = length + r0 + r1;
        float T(float s) => Mathf.Clamp01((s * total - r0) / length);
        float Dome(float s)
        {
            float d = s * total, e = total - d;
            float u = d < r0 ? (r0 - d) / r0 : e < r1 ? (r1 - e) / r1 : 0f;
            return Mathf.Sqrt(Mathf.Max(0f, 1f - u * u));
        }
        return Loft(new[] { Vector3.up * r0, Vector3.down * (length + r1) },
            s => radius(T(s)) * Dome(s), s => radius(T(s)) * squash(T(s)) * Dome(s),
            detail != null ? (s, a) => detail(T(s), a) : (System.Func<float, float, float>)null, rings + 8, sides, Vector3.forward);
    }

    /// <summary>Normals, with the seam where each ring meets itself smoothed over.</summary>
    static Mesh Finish(List<Vector3> verts, List<int> tris, int cols, int rows)
    {
        var mesh = new Mesh();
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        if (cols > 0)
        {
            var normals = mesh.normals;
            for (int r = 0; r < rows; r++)
            {
                int a = r * cols, b = a + cols - 1;
                Vector3 n = (normals[a] + normals[b]).normalized;
                normals[a] = normals[b] = n;
            }
            mesh.normals = normals;
        }
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>A sphere (radius 1) pushed about by shape(direction) into a new point.</summary>
    static Mesh Sculpt(System.Func<Vector3, Vector3> shape, int lat = 28, int lon = 36)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        int cols = lon + 1;
        for (int y = 0; y <= lat; y++)
        {
            float v = y / (float)lat * Mathf.PI;
            for (int x = 0; x <= lon; x++)
            {
                float u = x / (float)lon * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Sin(v) * Mathf.Sin(u), Mathf.Cos(v), Mathf.Sin(v) * Mathf.Cos(u));
                verts.Add(shape(d));
            }
        }
        for (int y = 0; y < lat; y++)
            for (int x = 0; x < lon; x++)
            {
                int p = y * cols + x, q = p + cols; // (wound so the faces look outward)
                tris.Add(p); tris.Add(q); tris.Add(p + 1);
                tris.Add(p + 1); tris.Add(q); tris.Add(q + 1);
            }
        return Finish(verts, tris, cols, lat + 1);
    }

    // ---------------------------------------------------------------- The parts

    /// <summary>
    /// The torso, pelvis to the base of the neck: narrow hips, a waist starved thin, ribs flaring and showing
    /// down its sides and front, the belly sunken, vertebrae ridged down its back, shoulder blades and
    /// collarbones standing out.
    /// </summary>
    static Mesh BuildTorso()
    {
        System.Func<float, float> rx = t => Keys(t, 0f, 0.13f, 0.14f, 0.12f, 0.28f, 0.075f, 0.42f, 0.115f, 0.6f, 0.14f, 0.8f, 0.165f, 0.92f, 0.1f, 1f, 0.05f);
        System.Func<float, float> rz = t => Keys(t, 0f, 0.09f, 0.14f, 0.085f, 0.28f, 0.065f, 0.42f, 0.095f, 0.6f, 0.105f, 0.8f, 0.09f, 0.92f, 0.07f, 1f, 0.045f);
        float Detail(float t, float a)
        {
            float s = Mathf.Sin(a), c = Mathf.Cos(a);
            float front = Mathf.Max(0f, s), back = Mathf.Max(0f, -s), side = Mathf.Abs(c);
            float d = 1f;
            // Ribs: bands round the sides and front, grooves between.
            float ribZone = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.36f, 0.44f, t)) * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.74f, 0.82f, t)));
            float band = Mathf.Pow(Mathf.Max(0f, Mathf.Sin((t - 0.36f) / 0.46f * Mathf.PI * 7f - side * 0.8f)), 3f);
            float ribs = ribZone * (0.35f + 0.65f * Mathf.Max(side, front * 0.8f)) * (1f - back * 0.9f);
            d *= 1f + 0.06f * band * ribs - 0.035f * (1f - band) * ribs;
            // The belly sunk in under the ribs.
            d *= 1f - 0.22f * front * front * Bump(t, 0.3f, 0.07f);
            // Vertebrae down the back.
            float vert = 0.55f + 0.45f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(t * Mathf.PI * 16f)), 2f);
            d *= 1f + 0.14f * Mathf.Pow(back, 14f) * vert * Mathf.SmoothStep(0.05f, 0.15f, t);
            // Shoulder blades, jutting either side of the spine.
            d *= 1f + 0.13f * Bump(t, 0.76f, 0.07f) * Mathf.Pow(Mathf.Max(0f, back * side * 2f), 2f);
            // Collarbones across the top of the chest; hip bones at the front of the pelvis.
            d *= 1f + 0.1f * Bump(t, 0.9f, 0.03f) * front * side * 2f;
            d *= 1f + 0.12f * Bump(t, 0.12f, 0.04f) * Mathf.Pow(Mathf.Max(0f, front * side * 2f), 2f);
            // Skin: never quite smooth.
            d *= 1f + 0.012f * (Mathf.PerlinNoise(t * 24f, a * 2.2f) - 0.5f);
            return d;
        }
        return Loft(Spine, rx, rz, Detail, 64, 40, Vector3.forward);
    }

    /// <summary>The neck: long, thin, two cords standing out down the front, jutting forward to the head.</summary>
    static Mesh BuildNeck()
    {
        var path = new[] { NeckBase - Vector3.up * 0.02f, new Vector3(0f, 2.02f, 0.1f), HeadJoint + new Vector3(0f, 0.02f, 0f) };
        return Loft(path, t => Keys(t, 0f, 0.055f, 0.4f, 0.04f, 1f, 0.045f), t => Keys(t, 0f, 0.05f, 0.4f, 0.038f, 1f, 0.045f),
            (t, a) => 1f + 0.18f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(a) * Mathf.Abs(Mathf.Cos(a)) * 2f), 10f) * Bump(t, 0.5f, 0.3f)
                         + 0.06f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(a)), 20f) * Bump(t, 0.35f, 0.1f), 20, 18, Vector3.forward);
    }

    /// <summary>
    /// The skull (in the head's space, as the old one sat): long and tall, swept back at the crown, a heavy brow,
    /// deep sockets where the eyes sit, hollow temples and cheeks, narrowing to the jaw.
    /// </summary>
    static Mesh BuildSkull()
    {
        Vector3 centre = new Vector3(0f, 0.1f, 0.02f);
        Vector3 radii = new Vector3(0.095f, 0.145f, 0.11f);
        return Sculpt(d =>
        {
            Vector3 p = centre + Vector3.Scale(d, radii);
            float front = Mathf.Max(0f, d.z), up = Mathf.Max(0f, d.y), back = Mathf.Max(0f, -d.z);
            // The crown swept up and back.
            p += new Vector3(0f, 0.035f, -0.05f) * Mathf.Pow(up * back, 0.8f);
            // Narrower toward the jaw.
            if (p.y < 0.05f) p.x *= Mathf.Lerp(0.72f, 1f, Mathf.InverseLerp(-0.06f, 0.05f, p.y));
            // The brow, heavy over the eyes.
            p.z += 0.016f * front * Bump(p.y, 0.16f, 0.022f) * (1f - 0.3f * Mathf.Abs(d.x));
            // Sockets: sunk in where each eye sits.
            foreach (float side in new[] { -1f, 1f })
            {
                float dist = new Vector2(p.x - side * 0.05f, (p.y - 0.12f) * 0.85f).magnitude;
                p -= d * 0.028f * front * Mathf.Exp(-Mathf.Pow(dist / 0.032f, 2f));
            }
            // Hollow temples and cheeks.
            float sideness = Mathf.Abs(d.x);
            p -= new Vector3(d.x, 0f, 0f) * 0.012f * sideness * Bump(p.y, 0.16f, 0.035f);
            p -= new Vector3(d.x, 0f, d.z * 0.3f) * 0.022f * sideness * front * Bump(p.y, 0.05f, 0.03f);
            // Cheekbones, sharp under the sockets.
            p += new Vector3(d.x, 0f, 0f) * 0.008f * sideness * front * Bump(p.y, 0.085f, 0.012f);
            return p;
        });
    }

    /// <summary>The jaw: long and narrow, hanging a little open under the mouth, a sharp chin.</summary>
    static Mesh BuildJaw()
    {
        Vector3 centre = new Vector3(0f, -0.05f, 0.07f);
        Quaternion tilt = Quaternion.Euler(20f, 0f, 0f);
        return Sculpt(d =>
        {
            Vector3 p = new Vector3(d.x * 0.058f, d.y * 0.048f, d.z * 0.082f);
            float front = Mathf.Max(0f, d.z);
            p.x *= Mathf.Lerp(1f, 0.45f, front * front); // narrowing to the chin
            p.y -= 0.012f * front * front * Mathf.Max(0f, -d.y); // the point of the chin
            p += new Vector3(d.x, 0f, 0f) * 0.006f * Mathf.Abs(d.x) * Bump(d.z, -0.2f, 0.3f); // the hinge of the jaw
            return centre + tilt * p;
        }, 20, 28);
    }

    /// <summary>The foot, from the ankle: long and narrow, high-arched, along +z to where the toes start.</summary>
    static Mesh BuildFoot()
    {
        var path = new[] { new Vector3(0f, 0.01f, -0.04f), new Vector3(0f, -0.035f, 0.08f), new Vector3(0f, -0.075f, 0.2f) };
        return Loft(path, t => Keys(t, 0f, 0.03f, 0.3f, 0.032f, 1f, 0.04f), t => Keys(t, 0f, 0.035f, 0.5f, 0.025f, 1f, 0.012f),
            (t, a) => 1f + 0.15f * Mathf.Pow(Mathf.Max(0f, Mathf.Sin(a)), 6f) * Bump(t, 0.55f, 0.2f), 18, 16, Vector3.up);
    }
}

/// <summary>
/// The detailed meshes characters are made of (the scour's, for now), by name: so a model (EnemyModel) can
/// name one and get it back, in the editor or the game.
/// </summary>
public static class EnemyMeshes
{
    static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() => meshes.Clear();

    public static void Register(Mesh mesh)
    {
        if (mesh != null) meshes[mesh.name] = mesh;
    }

    public static bool Has(Mesh mesh) => mesh != null && meshes.TryGetValue(mesh.name, out var m) && m == mesh;

    /// <summary>The mesh with this name (building the scour's if they're not made yet); null if there's none.</summary>
    public static Mesh Get(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (!meshes.ContainsKey(name) && name.StartsWith("Scour ")) ScourBody.Ensure();
        meshes.TryGetValue(name, out var mesh);
        return mesh;
    }
}
