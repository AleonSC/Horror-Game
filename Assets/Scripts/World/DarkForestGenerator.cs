using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Procedurally builds a dark wooded area: bumpy ground, pine and dead trees,
/// rocks, fallen logs, thick fog, moonlight and boundary walls.
///
/// Usage: add this to an empty GameObject in an empty scene and press Play.
/// With "Spawn Player" on, a first person player with a flashlight is created too.
/// You can also right-click the component header > "Generate" to build it in edit mode.
///
/// Works with the Built-in render pipeline and URP. Assign your own prefabs or
/// materials to replace the procedural placeholder art.
/// </summary>
public class DarkForestGenerator : MonoBehaviour
{
    [Header("General")]
    public bool generateOnStart = true;
    public int seed = 1337;
    public bool randomizeSeed = false;
    [Tooltip("Width/length of the forest in meters.")]
    public float areaSize = 160f;
    [Tooltip("Radius of the open clearing around the center where the player spawns.")]
    public float clearingRadius = 8f;

    [Header("Ground")]
    public float groundVertexSpacing = 1f;
    public float groundBumpiness = 1.5f;
    public float groundNoiseScale = 0.04f;

    [Header("Trees")]
    public int treeCount = 700;
    public float minTreeSpacing = 2.2f;
    [Range(0f, 1f), Tooltip("Portion of trees that are dead/bare instead of pines.")]
    public float deadTreeRatio = 0.3f;
    [Tooltip("Perlin scale for tree density patches (lower = bigger patches).")]
    public float densityNoiseScale = 0.05f;
    [Tooltip("Optional tree prefabs. If set, these are used instead of procedural trees.")]
    public GameObject[] treePrefabs;

    [Header("Props")]
    public int rockCount = 120;
    public int fallenLogCount = 35;
    public GameObject[] rockPrefabs;

    [Header("Atmosphere")]
    public bool applyLighting = true;
    public Color fogColor = new Color(0.02f, 0.025f, 0.035f);
    public float fogDensity = 0.05f;
    public Color ambientColor = new Color(0.06f, 0.07f, 0.09f);
    public Color moonColor = new Color(0.45f, 0.55f, 0.8f);
    public float moonIntensity = 0.2f;

    [Header("Player")]
    [Tooltip("Spawn a first person player with a flashlight if the scene has none.")]
    public bool spawnPlayer = true;

    [Header("Entity")]
    [Tooltip("Spawn the shadow entity that stalks the player if the scene has none.")]
    public bool spawnEntity = true;

    [Header("Lamps & Audio")]
    [Tooltip("Add a LampSpawner that scatters unlit lamps around the forest.")]
    public bool spawnLamps = true;
    [Tooltip("Add AmbienceAudio (background drone, wind and random forest noises).")]
    public bool spawnAmbience = true;
    [Tooltip("Add a BatterySpawner that scatters flashlight batteries around the forest.")]
    public bool spawnBatteries = true;
    [Tooltip("Add a WeatherSystem (wind that sways the trees).")]
    public bool spawnWeather = true;
    [Tooltip("Add IntroText (opening narration that fades in and out).")]
    public bool spawnIntro = true;

    /// <summary>Positions (x, z) of every generated tree, so other scripts can avoid them.</summary>
    public readonly List<Vector2> TreePositions = new List<Vector2>();

    /// <summary>The moving part of each tree (pivot at its base), for WeatherSystem to sway in the wind.</summary>
    public readonly List<Transform> SwayPivots = new List<Transform>();

    [Header("Material Overrides (optional)")]
    public Material groundMaterial;
    public Material barkMaterial;
    public Material foliageMaterial;
    public Material rockMaterial;

    const string RootName = "Generated Forest";

    System.Random rng;
    Transform root;
    Mesh coneMesh;
    Vector2 noiseOffset;
    Material groundMat, barkMat, deadBarkMat, foliageMat, rockMat;

    // When true, pressing Play in a scene with no generator and no player creates one automatically.
    // Set to false once you have other scenes (e.g. a main menu) that shouldn't get a forest.
    public static bool autoCreateInEmptyScenes = true;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void AutoCreate()
    {
        if (!autoCreateInEmptyScenes) return;
        if (FindAnyObjectByType<DarkForestGenerator>() != null) return;
        if (FindAnyObjectByType<FirstPersonController>() != null) return;
        new GameObject("Dark Forest Generator").AddComponent<DarkForestGenerator>();
    }

    void Start()
    {
        if (generateOnStart) Generate();
    }

    [ContextMenu("Generate")]
    public void Generate()
    {
        Clear();

        if (randomizeSeed) seed = Random.Range(int.MinValue, int.MaxValue);
        rng = new System.Random(seed);
        noiseOffset = new Vector2(Range(0f, 10000f), Range(0f, 10000f));

        root = new GameObject(RootName).transform;
        root.SetParent(transform, false);

        CreateMaterials();
        coneMesh = BuildConeMesh(10);

        BuildGround();
        BuildBoundary();
        ScatterTrees();
        ScatterRocks();
        ScatterLogs();

        if (applyLighting) ApplyAtmosphere();
        if (spawnPlayer) SpawnPlayer();
        if (spawnEntity && FindAnyObjectByType<ShadowEntity>() == null)
            ShadowEntity.Spawn();
        if (spawnLamps && FindAnyObjectByType<LampSpawner>() == null)
            new GameObject("Lamp Spawner").AddComponent<LampSpawner>();
        if (spawnBatteries && FindAnyObjectByType<BatterySpawner>() == null)
            new GameObject("Battery Spawner").AddComponent<BatterySpawner>();
        if (spawnAmbience && FindAnyObjectByType<AmbienceAudio>() == null)
            new GameObject("Ambience").AddComponent<AmbienceAudio>();
        if (spawnWeather && FindAnyObjectByType<WeatherSystem>() == null)
            new GameObject("Weather").AddComponent<WeatherSystem>();
        if (spawnIntro && Application.isPlaying && FindAnyObjectByType<IntroText>() == null)
            new GameObject("Intro Text").AddComponent<IntroText>();

        // Batch the things that never move. Trees are left out so the wind can sway them.
        if (Application.isPlaying)
        {
            StaticBatchingUtility.Combine(root.Find("Rocks").gameObject);
            StaticBatchingUtility.Combine(root.Find("Fallen Logs").gameObject);
        }
    }

    [ContextMenu("Clear")]
    public void Clear()
    {
        var existing = transform.Find(RootName);
        if (existing == null) return;
        if (Application.isPlaying) Destroy(existing.gameObject);
        else DestroyImmediate(existing.gameObject);
    }

    // ---------------------------------------------------------------- Ground

    public float GetGroundHeight(float x, float z)
    {
        float n = Mathf.PerlinNoise((x + noiseOffset.x) * groundNoiseScale, (z + noiseOffset.y) * groundNoiseScale);
        float detail = Mathf.PerlinNoise((x - noiseOffset.y) * groundNoiseScale * 4f, (z + noiseOffset.x) * groundNoiseScale * 4f);
        return (n - 0.5f) * groundBumpiness * 2f + (detail - 0.5f) * groundBumpiness * 0.3f;
    }

    void BuildGround()
    {
        int res = Mathf.Clamp(Mathf.RoundToInt(areaSize / groundVertexSpacing), 2, 512);
        float half = areaSize * 0.5f;
        float step = areaSize / res;

        var verts = new Vector3[(res + 1) * (res + 1)];
        var uvs = new Vector2[verts.Length];
        var tris = new int[res * res * 6];

        for (int z = 0, i = 0; z <= res; z++)
        {
            for (int x = 0; x <= res; x++, i++)
            {
                float wx = -half + x * step;
                float wz = -half + z * step;
                verts[i] = new Vector3(wx, GetGroundHeight(wx, wz), wz);
                uvs[i] = new Vector2(wx, wz) * 0.25f;
            }
        }

        for (int z = 0, t = 0; z < res; z++)
        {
            for (int x = 0; x < res; x++)
            {
                int i = z * (res + 1) + x;
                tris[t++] = i;
                tris[t++] = i + res + 1;
                tris[t++] = i + 1;
                tris[t++] = i + 1;
                tris[t++] = i + res + 1;
                tris[t++] = i + res + 2;
            }
        }

        var mesh = new Mesh { name = "Forest Ground", indexFormat = IndexFormat.UInt32 };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();

        var go = new GameObject("Ground");
        go.transform.SetParent(root, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = groundMat;
        go.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    void BuildBoundary()
    {
        float half = areaSize * 0.5f;
        var walls = new GameObject("Boundary").transform;
        walls.SetParent(root, false);

        for (int i = 0; i < 4; i++)
        {
            var wall = new GameObject("Wall " + i);
            wall.transform.SetParent(walls, false);
            var box = wall.AddComponent<BoxCollider>();
            bool alongX = i < 2;
            float sign = i % 2 == 0 ? 1f : -1f;
            wall.transform.localPosition = alongX ? new Vector3(0f, 10f, sign * half) : new Vector3(sign * half, 10f, 0f);
            box.size = alongX ? new Vector3(areaSize, 40f, 1f) : new Vector3(1f, 40f, areaSize);
        }
    }

    // ---------------------------------------------------------------- Trees

    void ScatterTrees()
    {
        var parent = new GameObject("Trees").transform;
        parent.SetParent(root, false);

        float half = areaSize * 0.5f - 1f;
        var placed = TreePositions;
        placed.Clear();
        SwayPivots.Clear();
        var grid = new Dictionary<Vector2Int, List<Vector2>>();
        float cell = Mathf.Max(minTreeSpacing, 0.1f);
        int attempts = treeCount * 15;

        for (int a = 0; a < attempts && placed.Count < treeCount; a++)
        {
            var p = new Vector2(Range(-half, half), Range(-half, half));
            float distFromCenter = p.magnitude;
            if (distFromCenter < clearingRadius) continue;

            // Patchy density: thin out trees in low-noise areas, thicker near the edges.
            float density = Mathf.PerlinNoise((p.x + noiseOffset.y) * densityNoiseScale, (p.y + noiseOffset.x) * densityNoiseScale);
            float edgeBoost = Mathf.InverseLerp(half * 0.6f, half, Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y)));
            // Trees right at the clearing edge are sparse so the spawn feels open.
            float clearingFade = Mathf.InverseLerp(clearingRadius, clearingRadius * 2f, distFromCenter);
            if (Range(0f, 1f) > (density * 1.4f + edgeBoost) * clearingFade) continue;

            if (!HasSpace(p, grid, cell)) continue;
            AddToGrid(p, grid, cell);
            placed.Add(p);

            var pos = new Vector3(p.x, GetGroundHeight(p.x, p.y) - 0.1f, p.y);
            if (treePrefabs != null && treePrefabs.Length > 0)
            {
                var prefab = treePrefabs[rng.Next(treePrefabs.Length)];
                if (prefab == null) continue;
                var t = Instantiate(prefab, pos, Quaternion.Euler(0f, Range(0f, 360f), 0f), parent);
                t.transform.localScale *= Range(0.8f, 1.3f);
                SwayPivots.Add(t.transform);
            }
            else if (Range(0f, 1f) < deadTreeRatio)
            {
                BuildDeadTree(pos, parent);
            }
            else
            {
                BuildPineTree(pos, parent);
            }
        }
    }

    bool HasSpace(Vector2 p, Dictionary<Vector2Int, List<Vector2>> grid, float cell)
    {
        var c = new Vector2Int(Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell));
        for (int x = -1; x <= 1; x++)
        for (int y = -1; y <= 1; y++)
        {
            if (!grid.TryGetValue(new Vector2Int(c.x + x, c.y + y), out var list)) continue;
            foreach (var q in list)
                if ((q - p).sqrMagnitude < minTreeSpacing * minTreeSpacing) return false;
        }
        return true;
    }

    static void AddToGrid(Vector2 p, Dictionary<Vector2Int, List<Vector2>> grid, float cell)
    {
        var c = new Vector2Int(Mathf.FloorToInt(p.x / cell), Mathf.FloorToInt(p.y / cell));
        if (!grid.TryGetValue(c, out var list)) grid[c] = list = new List<Vector2>();
        list.Add(p);
    }

    void BuildPineTree(Vector3 pos, Transform parent)
    {
        float scale = Range(0.8f, 1.6f);
        float trunkHeight = Range(6f, 10f) * scale;
        float trunkRadius = Range(0.18f, 0.3f) * scale;

        var tree = new GameObject("Pine");
        tree.transform.SetParent(parent, false);
        tree.transform.localPosition = pos;
        tree.transform.localRotation = Quaternion.Euler(Range(-3f, 3f), Range(0f, 360f), Range(-3f, 3f));
        Transform sway = AddTrunk(tree.transform, trunkHeight, trunkRadius, barkMat);

        // Stacked cones of foliage, starting high so the lower trunk is bare and creepy.
        int layers = rng.Next(3, 6);
        float foliageStart = trunkHeight * Range(0.3f, 0.45f);
        float foliageSpan = trunkHeight * 1.05f - foliageStart;
        float baseRadius = Range(1.6f, 2.4f) * scale;

        for (int i = 0; i < layers; i++)
        {
            float t = i / (float)layers;
            float radius = baseRadius * Mathf.Lerp(1f, 0.35f, t);
            float height = foliageSpan / layers * 1.8f;

            var cone = new GameObject("Foliage");
            cone.transform.SetParent(sway, false);
            cone.transform.localPosition = new Vector3(0f, foliageStart + foliageSpan * t, 0f);
            cone.transform.localRotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
            cone.transform.localScale = new Vector3(radius, height, radius);
            cone.AddComponent<MeshFilter>().sharedMesh = coneMesh;
            cone.AddComponent<MeshRenderer>().sharedMaterial = foliageMat;
        }
    }

    void BuildDeadTree(Vector3 pos, Transform parent)
    {
        float scale = Range(0.7f, 1.3f);
        float trunkHeight = Range(4f, 8f) * scale;
        float trunkRadius = Range(0.12f, 0.25f) * scale;

        var tree = new GameObject("Dead Tree");
        tree.transform.SetParent(parent, false);
        tree.transform.localPosition = pos;
        tree.transform.localRotation = Quaternion.Euler(Range(-8f, 8f), Range(0f, 360f), Range(-8f, 8f));
        Transform sway = AddTrunk(tree.transform, trunkHeight, trunkRadius, deadBarkMat);

        // Gnarled bare branches reaching outward and up.
        int branches = rng.Next(3, 7);
        for (int i = 0; i < branches; i++)
        {
            float y = trunkHeight * Range(0.45f, 0.95f);
            float length = Range(1f, 2.8f) * scale;
            float radius = trunkRadius * Range(0.3f, 0.5f);

            var pivot = new GameObject("Branch");
            pivot.transform.SetParent(sway, false);
            pivot.transform.localPosition = new Vector3(0f, y, 0f);
            pivot.transform.localRotation = Quaternion.Euler(0f, Range(0f, 360f), 0f) * Quaternion.Euler(Range(25f, 70f), 0f, 0f);

            var branch = CreatePrimitive(PrimitiveType.Cylinder, "Limb", pivot.transform, deadBarkMat, withCollider: false);
            branch.transform.localPosition = new Vector3(0f, length * 0.5f, 0f);
            branch.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
        }
    }

    /// <summary>
    /// Adds a fixed trunk collider to the tree, plus a "Sway" pivot at its base holding the visible trunk.
    /// Foliage and branches go under the pivot, so the wind can bend the whole tree without moving the collider.
    /// </summary>
    Transform AddTrunk(Transform tree, float trunkHeight, float trunkRadius, Material mat)
    {
        var col = tree.gameObject.AddComponent<CapsuleCollider>();
        col.radius = trunkRadius;
        col.height = trunkHeight;
        col.center = new Vector3(0f, trunkHeight * 0.5f, 0f);

        var sway = new GameObject("Sway").transform;
        sway.SetParent(tree, false);
        SwayPivots.Add(sway);

        var trunk = CreatePrimitive(PrimitiveType.Cylinder, "Trunk", sway, mat, withCollider: false);
        trunk.transform.localPosition = new Vector3(0f, trunkHeight * 0.5f, 0f);
        trunk.transform.localScale = new Vector3(trunkRadius * 2f, trunkHeight * 0.5f, trunkRadius * 2f);
        return sway;
    }

    // ---------------------------------------------------------------- Props

    void ScatterRocks()
    {
        var parent = new GameObject("Rocks").transform;
        parent.SetParent(root, false);
        float half = areaSize * 0.5f - 2f;

        for (int i = 0; i < rockCount; i++)
        {
            var p = new Vector2(Range(-half, half), Range(-half, half));
            if (p.magnitude < clearingRadius * 0.5f) continue;
            var pos = new Vector3(p.x, GetGroundHeight(p.x, p.y), p.y);
            var rot = Quaternion.Euler(Range(0f, 360f), Range(0f, 360f), Range(0f, 360f));

            if (rockPrefabs != null && rockPrefabs.Length > 0)
            {
                var prefab = rockPrefabs[rng.Next(rockPrefabs.Length)];
                if (prefab != null) Instantiate(prefab, pos, rot, parent);
                continue;
            }

            var rock = CreatePrimitive(PrimitiveType.Sphere, "Rock", parent, rockMat);
            float size = Range(0.3f, 1.6f);
            rock.transform.localPosition = pos;
            rock.transform.localRotation = rot;
            rock.transform.localScale = new Vector3(size * Range(0.8f, 1.6f), size * Range(0.4f, 0.8f), size * Range(0.8f, 1.4f));
        }
    }

    void ScatterLogs()
    {
        var parent = new GameObject("Fallen Logs").transform;
        parent.SetParent(root, false);
        float half = areaSize * 0.5f - 4f;

        for (int i = 0; i < fallenLogCount; i++)
        {
            var p = new Vector2(Range(-half, half), Range(-half, half));
            if (p.magnitude < clearingRadius) continue;

            float length = Range(3f, 7f);
            float radius = Range(0.2f, 0.4f);
            var log = CreatePrimitive(PrimitiveType.Cylinder, "Log", parent, deadBarkMat);
            log.transform.localPosition = new Vector3(p.x, GetGroundHeight(p.x, p.y) + radius * 0.6f, p.y);
            log.transform.localRotation = Quaternion.Euler(0f, Range(0f, 360f), 90f + Range(-6f, 6f));
            log.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
        }
    }

    // ---------------------------------------------------------------- Atmosphere

    void ApplyAtmosphere()
    {
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.ExponentialSquared;
        RenderSettings.fogColor = fogColor;
        RenderSettings.fogDensity = fogDensity;

        RenderSettings.skybox = null;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = ambientColor;
        RenderSettings.reflectionIntensity = 0.1f;

        // Replace any existing sun with dim moonlight.
        foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (light.type == LightType.Directional && light.transform.parent != root)
                light.enabled = false;

        var moon = new GameObject("Moonlight").AddComponent<Light>();
        moon.transform.SetParent(root, false);
        moon.type = LightType.Directional;
        moon.color = moonColor;
        moon.intensity = moonIntensity;
        moon.shadows = LightShadows.Soft;
        moon.transform.rotation = Quaternion.Euler(35f, Range(0f, 360f), 0f);
        RenderSettings.sun = moon;

        foreach (var cam in FindObjectsByType<Camera>(FindObjectsSortMode.None))
            ConfigureCamera(cam);
    }

    void ConfigureCamera(Camera cam)
    {
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = fogColor;
        cam.farClipPlane = Mathf.Min(cam.farClipPlane, 150f);
    }

    // ---------------------------------------------------------------- Player

    void SpawnPlayer()
    {
        if (FindAnyObjectByType<FirstPersonController>() != null) return;

        var player = new GameObject("Player");
        player.transform.position = new Vector3(0f, GetGroundHeight(0f, 0f) + 0.1f, 0f);

        var cc = player.AddComponent<CharacterController>();
        cc.radius = 0.35f;
        cc.stepOffset = 0.4f;
        cc.slopeLimit = 50f;

        // Reuse the scene's Main Camera if there is one, so its post-processing (e.g. URP Bloom) carries over.
        var cam = Camera.main;
        if (cam == null)
        {
            cam = new GameObject("Player Camera").AddComponent<Camera>();
            cam.tag = "MainCamera";
            cam.gameObject.AddComponent<AudioListener>();
        }
        else if (cam.GetComponent<AudioListener>() == null)
        {
            cam.gameObject.AddComponent<AudioListener>();
        }
        var camGo = cam.gameObject;
        camGo.transform.SetParent(player.transform, false);
        camGo.transform.localRotation = Quaternion.identity;
        cam.nearClipPlane = 0.05f;
        cam.fieldOfView = 70f;
        if (applyLighting) ConfigureCamera(cam);

        var controller = player.AddComponent<FirstPersonController>();
        controller.cameraRoot = camGo.transform;
        camGo.transform.localPosition = new Vector3(0f, controller.standingHeight - controller.eyeOffsetFromTop, 0f);

        camGo.AddComponent<Flashlight>();
    }

    // ---------------------------------------------------------------- Helpers

    float Range(float min, float max) => min + (float)rng.NextDouble() * (max - min);

    static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent, Material mat, bool withCollider = true)
    {
        var go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!withCollider)
        {
            var col = go.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(col);
            else DestroyImmediate(col);
        }
        return go;
    }

    void CreateMaterials()
    {
        groundMat = groundMaterial != null ? groundMaterial : MaterialFactory.Lit(new Color(0.07f, 0.06f, 0.045f));
        barkMat = barkMaterial != null ? barkMaterial : MaterialFactory.Lit(new Color(0.11f, 0.08f, 0.06f));
        deadBarkMat = barkMaterial != null ? barkMaterial : MaterialFactory.Lit(new Color(0.16f, 0.15f, 0.14f));
        foliageMat = foliageMaterial != null ? foliageMaterial : MaterialFactory.Lit(new Color(0.03f, 0.08f, 0.05f));
        rockMat = rockMaterial != null ? rockMaterial : MaterialFactory.Lit(new Color(0.18f, 0.18f, 0.19f));
    }

    /// <summary>Unit cone: radius 1 at y=0, apex at y=1. Faceted for a low-poly look.</summary>
    static Mesh BuildConeMesh(int segments)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();
        Vector3 apex = Vector3.up;

        for (int i = 0; i < segments; i++)
        {
            float a0 = i / (float)segments * Mathf.PI * 2f;
            float a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
            var p0 = new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0));
            var p1 = new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1));

            // Side face
            int s = verts.Count;
            verts.Add(p0); verts.Add(apex); verts.Add(p1);
            tris.Add(s); tris.Add(s + 1); tris.Add(s + 2);

            // Bottom cap face
            int b = verts.Count;
            verts.Add(p0); verts.Add(p1); verts.Add(Vector3.zero);
            tris.Add(b); tris.Add(b + 1); tris.Add(b + 2);
        }

        var mesh = new Mesh { name = "Cone" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
