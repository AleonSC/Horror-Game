using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A little garden round a lamp: the one "nice" spot in the forest. A round bed of dark, tended soil at the
/// foot of the pole, edged with stones, full of flowers in soft colours, with a few neat round shrubs and
/// bright, healthy grass, and a scattering of flowers out past the worn path. While the lamp's dark the
/// flowers are closed up; when it's lit they open, and fireflies come out and drift round the light.
/// LampSpawner builds one round each lamp (after it's placed).
/// </summary>
public class LampGarden : MonoBehaviour
{
    Lamp lamp;
    readonly List<Transform> heads = new List<Transform>();
    readonly List<float> headSeeds = new List<float>();
    readonly List<Transform> fireflies = new List<Transform>();
    readonly List<Vector3> fireflySeeds = new List<Vector3>();
    float bloom;
    Material fireflyMat;

    static Material soil, stem, shrub, grass, centre;
    static Material[] petals;
    static Mesh sphereMesh, cylinderMesh;

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

        // The bed: a disc of dark soil hugging the ground.
        const float bedRadius = 1.3f;
        root.gameObject.AddComponent<MeshFilter>().sharedMesh = Disc(bedRadius, Ground);
        root.gameObject.AddComponent<MeshRenderer>().sharedMaterial = soil;

        // Edging stones round it.
        if (generator != null && generator.RockMaterial != null)
        {
            var stones = new List<CombineInstance>();
            int count = 16;
            for (int i = 0; i < count; i++)
            {
                float a = (i + Random.Range(-0.2f, 0.2f)) / count * Mathf.PI * 2f;
                float x = Mathf.Cos(a) * (bedRadius + 0.05f), z = Mathf.Sin(a) * (bedRadius + 0.05f);
                float size = Random.Range(0.13f, 0.19f);
                stones.Add(new CombineInstance
                {
                    mesh = generator.MakeRockMesh(1f),
                    transform = Matrix4x4.TRS(new Vector3(x, Ground(x, z) - size * 0.25f, z), Quaternion.Euler(0f, Random.Range(0f, 360f), 0f), new Vector3(size, size * 0.7f, size)),
                });
            }
            Combined(root, "Edging Stones", stones, generator.RockMaterial);
        }

        // Flowers in the bed, and a few out past the path.
        var stems = new List<CombineInstance>();
        for (int i = 0; i < 22; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(0.42f, bedRadius - 0.12f);
            garden.Flower(root, Mathf.Cos(a) * r, Mathf.Sin(a) * r, Ground, stems);
        }
        for (int i = 0; i < 10; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(3.1f, 4.2f);
            garden.Flower(root, Mathf.Cos(a) * r, Mathf.Sin(a) * r, Ground, stems);
        }

        // Neat, round little shrubs.
        var bushes = new List<CombineInstance>();
        for (int i = 0; i < 3; i++)
        {
            float a = (i + Random.Range(0f, 0.6f)) / 3f * Mathf.PI * 2f, r = Random.Range(0.75f, 1.0f);
            float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
            for (int b = 0; b < 4; b++)
            {
                float s = Random.Range(0.22f, 0.32f);
                var off = new Vector3(Random.Range(-0.1f, 0.1f), 0f, Random.Range(-0.1f, 0.1f));
                bushes.Add(new CombineInstance { mesh = sphereMesh, transform = Matrix4x4.TRS(new Vector3(x, Ground(x, z) + s * 0.4f, z) + off, Quaternion.identity, new Vector3(s, s * 0.85f, s)) });
            }
        }
        Combined(root, "Shrubs", bushes, shrub);

        // Bright, healthy grass tufts round the bed and the path.
        var tufts = new List<CombineInstance>();
        for (int i = 0; i < 60; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(bedRadius + 0.2f, 4.4f);
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
        Combined(root, "Stems", stems, stem);

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

    /// <summary>One flower: a stem with a couple of leaves (into the shared stem mesh), and a head that opens.</summary>
    void Flower(Transform root, float x, float z, System.Func<float, float, float> ground, List<CombineInstance> stems)
    {
        float h = Random.Range(0.22f, 0.42f);
        Vector3 basePos = new Vector3(x, ground(x, z), z);
        var lean = Quaternion.Euler(Random.Range(-10f, 10f), 0f, Random.Range(-10f, 10f));
        Vector3 top = basePos + lean * Vector3.up * h;
        stems.Add(new CombineInstance { mesh = cylinderMesh, transform = Matrix4x4.TRS((basePos + top) * 0.5f, lean, new Vector3(0.012f, h * 0.5f, 0.012f)) });
        for (int l = 0; l < 2; l++)
        {
            var leafRot = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Quaternion.Euler(60f, 0f, 0f);
            stems.Add(new CombineInstance { mesh = sphereMesh, transform = Matrix4x4.TRS(basePos + Vector3.up * h * Random.Range(0.2f, 0.5f) + leafRot * Vector3.up * 0.04f, leafRot, new Vector3(0.035f, 0.08f, 0.012f)) });
        }

        // The head: five petals round a centre, one colour.
        var head = new GameObject("Flower").transform;
        head.SetParent(root, false);
        head.localPosition = top;
        head.localRotation = lean * Quaternion.Euler(Random.Range(-20f, 20f), Random.Range(0f, 360f), 0f);
        var petalParts = new List<CombineInstance>();
        float size = Random.Range(0.8f, 1.2f);
        for (int p = 0; p < 5; p++)
        {
            var rot = Quaternion.Euler(0f, p * 72f, 0f) * Quaternion.Euler(70f, 0f, 0f);
            petalParts.Add(new CombineInstance { mesh = sphereMesh, transform = Matrix4x4.TRS(rot * Vector3.up * 0.035f * size, rot, new Vector3(0.035f, 0.06f, 0.01f) * size) });
        }
        var petalMesh = new Mesh { name = "Petals" };
        petalMesh.CombineMeshes(petalParts.ToArray(), true, true);
        head.gameObject.AddComponent<MeshFilter>().sharedMesh = petalMesh;
        head.gameObject.AddComponent<MeshRenderer>().sharedMaterial = petals[Random.Range(0, petals.Length)];
        var middle = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        middle.transform.SetParent(head, false);
        middle.transform.localScale = new Vector3(0.03f, 0.018f, 0.03f) * size;
        Object.Destroy(middle.GetComponent<Collider>());
        middle.GetComponent<Renderer>().sharedMaterial = centre;
        heads.Add(head);
        headSeeds.Add(Random.Range(0f, 10f));
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
        for (int i = 0; i < heads.Count; i++)
        {
            float own = Mathf.Clamp01(bloom * 1.4f - (headSeeds[i] % 1f) * 0.4f);
            float open = Mathf.SmoothStep(0.3f, 1f, own);
            heads[i].localScale = new Vector3(open, Mathf.Lerp(1.6f, 1f, own), open);
        }

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
        if (soil != null) return;
        soil = MaterialFactory.LitTextured(ProceduralTextures.Dirt, ProceduralTextures.DirtNormal, new Color(0.35f, 0.26f, 0.2f), 0.08f);
        stem = MaterialFactory.Lit(new Color(0.12f, 0.3f, 0.08f), 0.15f);
        shrub = MaterialFactory.Lit(new Color(0.07f, 0.22f, 0.07f), 0.2f);
        grass = MaterialFactory.Lit(new Color(0.16f, 0.38f, 0.1f), 0.1f);
        centre = MaterialFactory.Lit(new Color(0.95f, 0.7f, 0.15f), 0.3f);
        petals = new[]
        {
            MaterialFactory.Lit(new Color(0.95f, 0.93f, 0.88f), 0.3f), // white
            MaterialFactory.Lit(new Color(0.95f, 0.6f, 0.7f), 0.3f),   // pink
            MaterialFactory.Lit(new Color(0.98f, 0.85f, 0.35f), 0.3f), // yellow
            MaterialFactory.Lit(new Color(0.65f, 0.55f, 0.9f), 0.3f),  // lavender
            MaterialFactory.Lit(new Color(0.85f, 0.25f, 0.25f), 0.3f), // red
        };
        var probe = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        sphereMesh = probe.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(probe);
        probe = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinderMesh = probe.GetComponent<MeshFilter>().sharedMesh;
        Object.Destroy(probe);
    }

    /// <summary>A disc following the ground, a hair above it.</summary>
    static Mesh Disc(float radius, System.Func<float, float, float> ground)
    {
        const int rings = 4, around = 28;
        var verts = new List<Vector3> { new Vector3(0f, ground(0f, 0f) + 0.02f, 0f) };
        var uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };
        var tris = new List<int>();
        for (int r = 1; r <= rings; r++)
        {
            float rr = radius * r / rings;
            for (int j = 0; j < around; j++)
            {
                float a = j / (float)around * Mathf.PI * 2f;
                float wobble = r == rings ? 1f + 0.04f * Mathf.Sin(a * 5f) : 1f;
                float x = Mathf.Cos(a) * rr * wobble, z = Mathf.Sin(a) * rr * wobble;
                verts.Add(new Vector3(x, ground(x, z) + 0.02f, z));
                uvs.Add(new Vector2(x, z) * 0.6f);
            }
        }
        for (int j = 0; j < around; j++)
            tris.AddRange(new[] { 0, 1 + (j + 1) % around, 1 + j });
        for (int r = 1; r < rings; r++)
        {
            int inner = 1 + (r - 1) * around, outer = 1 + r * around;
            for (int j = 0; j < around; j++)
            {
                int j1 = (j + 1) % around;
                tris.AddRange(new[] { inner + j, inner + j1, outer + j, inner + j1, outer + j1, outer + j });
            }
        }
        var mesh = new Mesh { name = "Garden Bed" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
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
