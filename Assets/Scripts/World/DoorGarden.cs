using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The garden round the lone door: the one cared-for place in the whole forest. A flagstone path runs up
/// to the step between borders of flowers, across a neat lawn; a stone bench and a birdbath sit out on the
/// grass; flowers ring the foot of each lamp post; tall grass at the edges where the forest takes back over. Fireflies drift round it all, in the lamps' warm light.
/// PortalDoor.BuildGarden builds it (in the door's space: the door faces +z, the path runs out that way).
/// </summary>
public class DoorGarden : MonoBehaviour
{
    readonly List<Transform> fireflies = new List<Transform>();
    readonly List<Vector3> fireflySeeds = new List<Vector3>();

    static Mesh sphereMesh, cubeMesh, cylinderMesh;
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() => sphereMesh = cubeMesh = cylinderMesh = null; // (Unity's own primitive meshes: forgotten, not destroyed)

    const float GardenRadius = 7.6f, PathHalfWidth = 0.55f; // how far out the lawn and path go

    public static DoorGarden Build(PortalDoor door, DarkForestGenerator generator)
    {
        if (sphereMesh == null)
        {
            sphereMesh = PrimitiveMesh(PrimitiveType.Sphere);
            cubeMesh = PrimitiveMesh(PrimitiveType.Cube);
            cylinderMesh = PrimitiveMesh(PrimitiveType.Cylinder);
        }
        var lawnMat = MaterialFactory.Lit(new Color(0.13f, 0.3f, 0.08f), 0.12f);
        var tallMat = MaterialFactory.Lit(new Color(0.12f, 0.24f, 0.07f), 0.12f);
        var stemMat = MaterialFactory.Lit(new Color(0.08f, 0.2f, 0.06f), 0.15f);
        var flagMat = MaterialFactory.Lit(new Color(0.3f, 0.29f, 0.27f), 0.12f);
        var paleMat = MaterialFactory.Lit(new Color(0.42f, 0.4f, 0.36f), 0.12f);
        var waterMat = MaterialFactory.Lit(new Color(0.02f, 0.03f, 0.04f), 0.95f);
        Material[] petals =
        {
            MaterialFactory.Lit(new Color(0.55f, 0.03f, 0.05f), 0.3f), // deep red roses
            MaterialFactory.Lit(new Color(0.8f, 0.78f, 0.72f), 0.3f),  // white
            MaterialFactory.Lit(new Color(0.3f, 0.1f, 0.45f), 0.3f),   // violet
            MaterialFactory.Lit(new Color(0.7f, 0.55f, 0.12f), 0.3f),  // yellow
        };

        var root = new GameObject("Garden").transform;
        root.SetParent(door.transform, false);
        var garden = root.gameObject.AddComponent<DoorGarden>();
        Transform at = door.transform;
        float Ground(float x, float z)
        {
            Vector3 w = at.TransformPoint(new Vector3(x, 0f, z));
            return (generator != null ? generator.GetGroundHeight(w.x, w.z) : at.position.y) - at.position.y;
        }
        bool OnPath(float x, float z) => z > 0.2f && z < GardenRadius + 0.3f && Mathf.Abs(x) < PathHalfWidth + 0.1f;
        Vector2 LampPost(int side) => new Vector2(side * 1.6f, 0.35f);

        // The path: two staggered flagstones a row, up to the step.
        var flags = new List<CombineInstance>();
        int row = 0;
        for (float z = 1.0f; z < GardenRadius + 0.2f; z += 0.55f, row++)
            for (int k = 0; k < 2; k++)
            {
                float x = (k == 0 ? -0.26f : 0.26f) + (row % 2 == 0 ? 0.04f : -0.04f) + Random.Range(-0.03f, 0.03f);
                float zz = z + Random.Range(-0.04f, 0.04f);
                var rot = Quaternion.Euler(0f, Random.Range(-10f, 10f), 0f);
                var size = new Vector3(Random.Range(0.42f, 0.5f), 0.1f, Random.Range(0.4f, 0.48f));
                flags.Add(Piece(cubeMesh, new Vector3(x, Ground(x, zz) - 0.025f, zz), rot, size));
            }
        Combined(root, "Path", flags, flagMat);

        // The lawn: short, thick, even grass everywhere off the path, out to the fence (and round the back).
        var lawn = new BladeMesh();
        for (int i = 0; i < 2600; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            float sin = Mathf.Sin(a);
            float r = Mathf.Sqrt(Random.value) * (sin > 0f ? GardenRadius - 0.1f : 4.6f);
            float x = Mathf.Cos(a) * r, z = sin * r;
            if (OnPath(x, z) || (Mathf.Abs(x) < 1f && Mathf.Abs(z) < 0.8f)) continue; // not on the path or the steps
            lawn.Add(new Vector3(x, Ground(x, z) - 0.02f, z), Random.Range(0.08f, 0.18f), Random.Range(0.02f, 0.035f), Random.Range(0f, 360f), Random.Range(5f, 30f));
        }
        Blades(root, "Lawn", lawn, lawnMat);

        // Long grass round the outside, where the forest starts.
        var wild = new BladeMesh();
        for (int i = 0; i < 900; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            float inner = Mathf.Sin(a) > 0f ? GardenRadius + 0.2f : 4.8f;
            float r = inner + Mathf.Pow(Random.value, 1.5f) * 1.8f;
            float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
            if (OnPath(x, z)) continue;
            float h = Mathf.Lerp(0.9f, 0.4f, (r - inner) / 1.8f) * Random.Range(0.7f, 1.15f);
            wild.Add(new Vector3(x, Ground(x, z) - 0.02f, z), h, Random.Range(0.025f, 0.045f), Random.Range(0f, 360f), Random.Range(5f, 25f));
        }
        Blades(root, "Long Grass", wild, tallMat);

        // Flowers: in the borders along the path, ringing each lamp post's foot, and round the birdbath.
        var stems = new List<CombineInstance>();
        var heads = new List<CombineInstance>[petals.Length];
        for (int k = 0; k < heads.Length; k++) heads[k] = new List<CombineInstance>();
        void Flower(float x, float z, int colour)
        {
            float h = Random.Range(0.22f, 0.45f);
            float g = Ground(x, z);
            var lean = Quaternion.Euler(Random.Range(-8f, 8f), 0f, Random.Range(-8f, 8f));
            stems.Add(Piece(cylinderMesh, new Vector3(x, g, z) + lean * Vector3.up * h * 0.5f, lean, new Vector3(0.012f, h * 0.5f, 0.012f)));
            float s = Random.Range(0.05f, 0.085f);
            heads[colour].Add(Piece(sphereMesh, new Vector3(x, g, z) + lean * Vector3.up * h, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), new Vector3(s, s * 0.7f, s)));
            // A leaf or two.
            var leaf = Quaternion.Euler(0f, Random.Range(0f, 360f), 50f);
            stems.Add(Piece(sphereMesh, new Vector3(x, g + h * 0.35f, z) + leaf * Vector3.up * 0.04f, leaf, new Vector3(0.025f, 0.07f, 0.012f)));
        }
        for (int side = -1; side <= 1; side += 2)
            for (int i = 0; i < 45; i++)
            {
                float z = Random.Range(1.3f, 6.9f);
                float x = side * Random.Range(0.75f, 1.25f);
                Flower(x, z, (int)(z / 1.4f + (side > 0 ? 1 : 0)) % petals.Length); // drifts of colour
            }
        for (int side = -1; side <= 1; side += 2)
        {
            Vector2 p = LampPost(side);
            for (int i = 0; i < 22; i++)
            {
                float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(0.42f, 0.75f);
                float x = p.x + Mathf.Cos(a) * r, z = p.y + Mathf.Sin(a) * r;
                if (Mathf.Abs(x) < 1.05f) continue; // not against the doorcase
                Flower(x, z, Random.value < 0.6f ? 0 : 1);
            }
        }

        // A birdbath out on the lawn, ringed with flowers.
        var bath = new Vector2(3.0f, 3.6f);
        float bathY = Ground(bath.x, bath.y);
        Prim(root, PrimitiveType.Cylinder, paleMat, new Vector3(bath.x, bathY + 0.04f, bath.y), new Vector3(0.36f, 0.06f, 0.36f), true);
        Prim(root, PrimitiveType.Cylinder, paleMat, new Vector3(bath.x, bathY + 0.42f, bath.y), new Vector3(0.14f, 0.34f, 0.14f), false);
        Prim(root, PrimitiveType.Cylinder, paleMat, new Vector3(bath.x, bathY + 0.8f, bath.y), new Vector3(0.62f, 0.06f, 0.62f), false);
        Prim(root, PrimitiveType.Cylinder, paleMat, new Vector3(bath.x, bathY + 0.75f, bath.y), new Vector3(0.4f, 0.05f, 0.4f), false);
        Prim(root, PrimitiveType.Cylinder, waterMat, new Vector3(bath.x, bathY + 0.835f, bath.y), new Vector3(0.54f, 0.01f, 0.54f), false);
        for (int i = 0; i < 18; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(0.45f, 0.8f);
            Flower(bath.x + Mathf.Cos(a) * r, bath.y + Mathf.Sin(a) * r, Random.value < 0.5f ? 2 : 3);
        }
        Combined(root, "Flower Stems", stems, stemMat);
        for (int k = 0; k < petals.Length; k++) Combined(root, "Flowers", heads[k], petals[k]);

        // A stone bench across the lawn from it, facing the path.
        var bench = new Vector2(-3.0f, 3.4f);
        float benchY = Ground(bench.x, bench.y);
        foreach (float dz in new[] { -0.5f, 0.5f })
            Prim(root, PrimitiveType.Cube, paleMat, new Vector3(bench.x, benchY + 0.15f, bench.y + dz), new Vector3(0.36f, 0.5f, 0.12f), false);
        Prim(root, PrimitiveType.Cube, paleMat, new Vector3(bench.x, benchY + 0.43f, bench.y), new Vector3(0.48f, 0.08f, 1.5f), false);
        var seat = new GameObject("Bench");
        seat.transform.SetParent(root, false);
        seat.transform.localPosition = new Vector3(bench.x, benchY + 0.24f, bench.y);
        seat.AddComponent<BoxCollider>().size = new Vector3(0.48f, 0.5f, 1.5f);

        // A little warm light over the lawn, so the garden glows in the dark like a stage.
        var fill = new GameObject("Garden Light").AddComponent<Light>();
        fill.transform.SetParent(root, false);
        fill.transform.localPosition = new Vector3(0f, 3f, 3.2f);
        fill.type = LightType.Point;
        fill.color = new Color(1f, 0.8f, 0.55f);
        fill.range = 10f;
        fill.intensity = 0.7f;
        fill.shadows = LightShadows.None;

        // Fireflies.
        var fireflyMat = MaterialFactory.NoFog(new Color(0.75f, 1f, 0.35f) * 3f);
        for (int i = 0; i < 16; i++)
        {
            var fly = Prim(root, PrimitiveType.Sphere, fireflyMat, Vector3.up, Vector3.one * 0.03f, false);
            fly.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            garden.fireflies.Add(fly);
            garden.fireflySeeds.Add(new Vector3(Random.Range(0f, 100f), Random.Range(0.6f, 2.6f), Random.Range(0.5f, 1.3f)));
        }
        return garden;
    }

    void Update()
    {
        var cam = Camera.main;
        if (cam == null || (cam.transform.position - transform.position).sqrMagnitude > 60f * 60f) return;
        // Fireflies: slow, wandering loops over the lawn, blinking.
        float t = Time.time;
        for (int i = 0; i < fireflies.Count; i++)
        {
            Vector3 s = fireflySeeds[i];
            float a = t * 0.12f * s.z + s.x;
            float r = 1.5f + 4.5f * Mathf.PerlinNoise(s.x, t * 0.05f);
            fireflies[i].localPosition = new Vector3(Mathf.Cos(a) * r, s.y + Mathf.Sin(t * 0.6f + s.x) * 0.4f, 2.5f + Mathf.Sin(a) * r * 0.8f);
            float blink = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * 1.3f * s.z + s.x * 3f), 4f);
            fireflies[i].localScale = Vector3.one * Mathf.Lerp(0.008f, 0.035f, blink);
        }
    }

    // ---------------------------------------------------------------- Building blocks

    static CombineInstance Piece(Mesh mesh, Vector3 pos, Quaternion rot, Vector3 scale) =>
        new CombineInstance { mesh = mesh, transform = Matrix4x4.TRS(pos, rot, scale) };

    static Mesh PrimitiveMesh(PrimitiveType type)
    {
        var probe = GameObject.CreatePrimitive(type);
        var mesh = probe.GetComponent<MeshFilter>().sharedMesh;
        Destroy(probe);
        return mesh;
    }

    static Transform Prim(Transform parent, PrimitiveType type, Material mat, Vector3 pos, Vector3 scale, bool keepCollider)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!keepCollider) Destroy(go.GetComponent<Collider>());
        return go.transform;
    }

    static void Combined(Transform parent, string name, List<CombineInstance> parts, Material mat)
    {
        if (parts.Count == 0) return;
        var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.CombineMeshes(parts.ToArray(), true, true);
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
    }

    static void Blades(Transform parent, string name, BladeMesh blades, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = blades.Build();
        var r = go.AddComponent<MeshRenderer>();
        r.sharedMaterial = mat;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
    }
}
