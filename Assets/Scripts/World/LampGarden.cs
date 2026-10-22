using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The ground round a lamp: tall, thick grass grown up round the foot of the post, brighter grass out past
/// the worn path. (The proper garden is round the lone door: DoorGarden.) When the lamp's lit, fireflies come out and drift round the light.
/// LampSpawner builds one round each lamp (after it's placed).
/// </summary>
public class LampGarden : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Inspector changes made in Play are kept)

    Lamp lamp;
    readonly List<Transform> fireflies = new List<Transform>();
    readonly List<Vector3> fireflySeeds = new List<Vector3>();
    float bloom;
    Material fireflyMat;

    static Material shrub, grass, tallGrass;
    static Mesh sphereMesh, cylinderMesh;
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics()
    {
        StaticReset.Drop(ref shrub);
        StaticReset.Drop(ref grass);
        StaticReset.Drop(ref tallGrass);
        sphereMesh = cylinderMesh = null; // (Unity's own primitive meshes: forgotten, not destroyed)
    }

    /// <summary>Builds the garden round a lamp that's already in place, parented to it.</summary>
    public static LampGarden Build(Lamp lamp, DarkForestGenerator generator)
    {
        MakeMaterials();
        var root = new GameObject("Garden").transform;
        root.SetParent(lamp.transform, false);
        root.rotation = Quaternion.identity; // laid out in world directions, whatever way the lamp faces
        var garden = root.gameObject.AddComponent<LampGarden>();
        garden.lamp = lamp;

        Vector3 c = lamp.transform.position;
        float Ground(float x, float z) => (generator != null ? generator.GetGroundHeight(c.x + x, c.z + z) : c.y) - c.y;

        // Tall, thick grass grown up round the foot of the post, thinning out toward the path.
        var blades = new BladeMesh();
        for (int i = 0; i < 420; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            float r = Mathf.Lerp(0.3f, 1.9f, Mathf.Pow(Random.value, 0.8f));
            float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
            float h = Mathf.Lerp(1.1f, 0.45f, Mathf.InverseLerp(0.3f, 1.9f, r)) * Random.Range(0.7f, 1.15f);
            blades.Add(new Vector3(x, Ground(x, z) - 0.02f, z), h, Random.Range(0.025f, 0.045f), Random.Range(0f, 360f), Random.Range(5f, 25f));
        }
        var tall = new GameObject("Tall Grass");
        tall.transform.SetParent(root, false);
        tall.AddComponent<MeshFilter>().sharedMesh = blades.Build();
        tall.AddComponent<MeshRenderer>().sharedMaterial = tallGrass;

        // Bright, healthy grass tufts round the bed and the path.
        var tufts = new List<CombineInstance>();
        for (int i = 0; i < 60; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(2f, 4.4f);
            if (r > 1.6f && r < 3.0f && Random.value < 0.75f) continue; // mostly not on the worn path
            float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
            for (int blade = 0; blade < 5; blade++)
            {
                float h = Random.Range(0.12f, 0.24f);
                var lean = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f));
                tufts.Add(new CombineInstance { mesh = cylinderMesh, transform = Matrix4x4.TRS(new Vector3(x, Ground(x, z), z) + lean * Vector3.up * h * 0.5f, lean, new Vector3(0.012f, h * 0.5f, 0.012f)) });
            }
        }
        Combined(root, "Grass", tufts, grass);

        // Fireflies, waiting for the light.
        garden.fireflyMat = MaterialFactory.NoFog(new Color(0.75f, 1f, 0.35f) * 3f);
        for (int i = 0; i < 7; i++)
        {
            var fly = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fly.name = "Firefly";
            fly.transform.SetParent(root, false);
            fly.transform.localScale = Vector3.one * 0.03f;
            Object.Destroy(fly.GetComponent<Collider>());
            var r = fly.GetComponent<Renderer>();
            r.sharedMaterial = garden.fireflyMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            fly.SetActive(false);
            garden.fireflies.Add(fly.transform);
            garden.fireflySeeds.Add(new Vector3(Random.Range(0f, 100f), Random.Range(1.2f, 3.2f), Random.Range(0.6f, 1.4f)));
        }
        return garden;
    }

    void Update()
    {
        if (lamp == null) return;
        // Open in the light, closed up in the dark: slowly, unevenly.
        bloom = Mathf.MoveTowards(bloom, lamp.IsLit ? 1f : 0f, Time.deltaTime / 4f);
        var cam = Camera.main;
        bool near = cam != null && (cam.transform.position - transform.position).sqrMagnitude < 60f * 60f;
        if (!near) return;

        float t = Time.time;

        // Fireflies: slow, wandering loops round the light, blinking.
        for (int i = 0; i < fireflies.Count; i++)
        {
            bool show = bloom > 0.5f;
            if (fireflies[i].gameObject.activeSelf != show) fireflies[i].gameObject.SetActive(show);
            if (!show) continue;
            Vector3 s = fireflySeeds[i];
            float a = t * 0.25f * s.z + s.x;
            float r = 1.2f + 1.6f * Mathf.PerlinNoise(s.x, t * 0.1f);
            fireflies[i].localPosition = new Vector3(Mathf.Cos(a) * r, s.y + Mathf.Sin(t * 0.7f + s.x) * 0.4f, Mathf.Sin(a) * r);
            float blink = Mathf.Pow(0.5f + 0.5f * Mathf.Sin(t * 1.3f * s.z + s.x * 3f), 4f);
            fireflies[i].localScale = Vector3.one * Mathf.Lerp(0.008f, 0.035f, blink);
        }
    }

    // ---------------------------------------------------------------- Building blocks

    static void MakeMaterials()
    {
        if (shrub != null) return;
        tallGrass = MaterialFactory.Lit(new Color(0.12f, 0.24f, 0.07f), 0.12f);
        shrub = MaterialFactory.Lit(new Color(0.07f, 0.22f, 0.07f), 0.2f);
        grass = MaterialFactory.Lit(new Color(0.16f, 0.38f, 0.1f), 0.1f);
        var probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphereMesh = probe.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(probe);
        probe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinderMesh = probe.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(probe);
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
}

/// <summary>Builds a mesh of grass blades: tapering, curving, two-sided.</summary>
class BladeMesh
{
    readonly List<Vector3> verts = new List<Vector3>();
    readonly List<Vector3> normals = new List<Vector3>();
    readonly List<int> tris = new List<int>();

    /// <summary>A blade rooted at a point, height h, base width w, facing yaw, bending over by bend degrees.</summary>
    public void Add(Vector3 root, float h, float w, float yaw, float bend)
    {
        Quaternion face = Quaternion.Euler(0f, yaw, 0f);
        Vector3 side = face * Vector3.right * (w * 0.5f);
        Vector3 lean = face * Vector3.forward;
        Vector3 n = face * Vector3.back;
        const int segs = 3;
        int start = verts.Count;
        for (int k = 0; k <= segs; k++)
        {
            float u = k / (float)segs;
            float b = Mathf.Sin(u * u * bend * Mathf.Deg2Rad) * h; // curving over toward the tip
            Vector3 c = root + Vector3.up * h * u + lean * b * 0.6f;
            float taper = 1f - u;
            verts.Add(c - side * taper);
            verts.Add(c + side * taper);
            normals.Add(n);
            normals.Add(n);
        }
        for (int k = 0; k < segs; k++)
        {
            int a = start + k * 2;
            tris.AddRange(new[] { a, a + 2, a + 1, a + 1, a + 2, a + 3 }); // front
            tris.AddRange(new[] { a, a + 1, a + 2, a + 1, a + 3, a + 2 }); // back
        }
    }

    public Mesh Build()
    {
        var mesh = new Mesh { name = "Tall Grass", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }
}
