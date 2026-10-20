using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Procedurally builds a dark wooded area: bumpy ground, pine and dead trees,
/// rocks, fallen logs, thick fog and moonlight. With "Wrap World" on, the map loops
/// (walk off one edge and you come back in on the other); otherwise it has boundary walls.
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
    public float areaSize = 320f;
    [Tooltip("Radius of the open clearing around the center where the player spawns.")]
    public float clearingRadius = 8f;
    [Tooltip("Make the map loop: leaving one side brings you in on the opposite side. Off = invisible walls at the edges.")]
    public bool wrapWorld = true;

    [Header("Ground")]
    public float groundVertexSpacing = 1f;
    public float groundBumpiness = 1.5f;

    [Header("Trees")]
    public int treeCount = 2800;
    public float minTreeSpacing = 2.2f;
    [Range(0f, 1f), Tooltip("Portion of trees that are dead/bare instead of pines.")]
    public float deadTreeRatio = 0.3f;
    [Tooltip("Chance a tree has vines hanging from its branches (they swing in the wind and when brushed).")]
    [Range(0f, 1f)] public float vineTreeChance = 0.04f;
    [Tooltip("Chance a tree grows far bigger than the rest: an old giant towering over the forest.")]
    [Range(0f, 1f)] public float bigTreeChance = 0.03f;
    public float minBigTreeSize = 1.7f;
    public float maxBigTreeSize = 2.5f;
    [Tooltip("Perlin scale for tree density patches (lower = bigger patches).")]
    public float densityNoiseScale = 0.05f;
    [Tooltip("Optional tree prefabs. If set, these are used instead of procedural trees.")]
    public GameObject[] treePrefabs;

    [Header("Grass")]
    public int grassPatchCount = 16000;
    [Tooltip("Grass clumps gathered round the base of each tree (0 for none).")]
    public int minTreeBaseClumps = 4;
    public int maxTreeBaseClumps = 10;
    [Tooltip("Bushes you can push through (they rustle and shake).")]
    public int bushCount = 1400;
    public float minBushSize = 0.45f;
    public float maxBushSize = 1.4f;
    [Tooltip("Perlin scale for where grass grows (lower = bigger meadows).")]
    public float grassNoiseScale = 0.03f;
    public Color grassColor = new Color(0.07f, 0.09f, 0.05f);
    public Material grassMaterial;

    [Header("Shacks")]
    [Tooltip("Abandoned shacks, each with a torch and a lighter inside.")]
    public int shackCount = 3;
    [Tooltip("Keep shacks at least this far apart.")]
    public float minShackSpacing = 60f;

    [Header("Campsites")]
    [Tooltip("Abandoned campsites, each with a smoking campfire and a backpack with a flare gun.")]
    public int campsiteCount = 3;
    [Tooltip("Keep campsites at least this far apart.")]
    public float minCampsiteSpacing = 50f;

    [Header("Watchtower")]
    [Tooltip("Place one tall watchtower with floodlights somewhere on the map.")]
    public bool spawnWatchtower = true;
    [Tooltip("Park a broken-down car with working headlights somewhere at random.")]
    public bool spawnCar = true;
    [Tooltip("Stand a lone open door somewhere, between two lamp posts, that leads to a vast dark room.")]
    public bool spawnPortalDoor = true;

    [Header("Props")]
    public int rockCount = 480;
    [Tooltip("Big boulders, half buried, scattered at random.")]
    public int boulderCount = 40;
    [Header("Water")]
    [Tooltip("Ponds sunk into the ground, with reeds round the edge.")]
    public int pondCount = 3;
    public float minPondRadius = 7f;
    public float maxPondRadius = 13f;
    [Tooltip("Shallow puddles on flat ground.")]
    public int puddleCount = 40;
    public float minBoulderSize = 2.5f;
    public float maxBoulderSize = 6f;
    public int fallenLogCount = 140;
    public GameObject[] rockPrefabs;

    [Header("Atmosphere")]
    public bool applyLighting = true;
    // (The pause menu's lighting panel starts from its own "Default" theme, the same values.)
    public Color fogColor = new Color(0.02f, 0.025f, 0.035f);
    public float fogDensity = 0.05f;
    public Color ambientColor = new Color(0.06f, 0.07f, 0.09f);
    public Color moonColor = new Color(0.45f, 0.55f, 0.8f);
    public float moonIntensity = 0.25f;
    [Tooltip("On = use the scene's own Directional Light (with all its Inspector settings) instead. Off (default) = turn it off and create a dim blue moonlight from the values above.")]
    public bool useSceneLight = false;

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
    [Tooltip("Add Wildlife (beetles, rats, moths around lit lamps, bats overhead, owls in the trees, deer).")]
    public bool spawnWildlife = true;
    [Tooltip("Add FallingTreeEvent (every minute or so, a nearby tree snaps and falls).")]
    public bool spawnFallingTree = true;
    [Tooltip("Add FallingLeaves (leaves drifting down from the trees and blowing about).")]
    public bool spawnLeaves = true;
    [Tooltip("A very light, silent rain: a few drops falling round the player.")]
    public bool spawnRain = true;
    [Tooltip("Add WatcherEvent (after 5 lamps: an eye in the sky, then a giant walking toward you).")]
    public bool spawnWatcher = true;

    /// <summary>Positions (x, z) of every generated tree, so other scripts can avoid them.</summary>
    public readonly List<Vector2> TreePositions = new List<Vector2>();

    /// <summary>The moving part of each tree (pivot at its base), for WeatherSystem to sway in the wind.</summary>
    public readonly List<Transform> SwayPivots = new List<Transform>();

    /// <summary>Where a vine hangs from: a point on a tree's sway pivot (in its local space), and how long the vine is.</summary>
    public struct VineAnchor
    {
        public Transform sway;
        public Vector3 local;
        public float length;
    }

    /// <summary>Every vine to hang (VineSystem simulates them).</summary>
    public readonly List<VineAnchor> VineAnchors = new List<VineAnchor>();

    /// <summary>Positions (x, z) of the shacks, so other things can keep clear of them.</summary>
    public readonly List<Vector2> ShackPositions = new List<Vector2>();

    /// <summary>Positions (x, z) of the campsites.</summary>
    public readonly List<Vector2> CampsitePositions = new List<Vector2>();

    /// <summary>Where the big boulders are (x, z).</summary>
    public readonly List<Vector2> BoulderSpots = new List<Vector2>();

    /// <summary>Spots kept clear for the broken-down car: where it's parked, and the ground its headlights light up.</summary>
    public readonly List<Vector2> CarClearings = new List<Vector2>();

    /// <summary>Position (x, z) of the lone door to the dark room, if there is one.</summary>
    public Vector2? PortalDoorPosition { get; private set; }

    /// <summary>Position (x, z) of the watchtower, if there is one.</summary>
    public Vector2? WatchtowerPosition { get; private set; }

    /// <summary>True if the point is within distance of any shack (or a bit more, for the bigger watchtower).</summary>
    public bool IsNearStructure(Vector3 p, float distance)
    {
        var flat = new Vector2(p.x, p.z);
        foreach (var shack in ShackPositions)
            if (Near(shack, flat, distance)) return true;
        foreach (var camp in CampsitePositions)
            if (Near(camp, flat, distance + 1f)) return true;
        foreach (var spot in CarClearings)
            if (Near(spot, flat, distance + 2f)) return true;
        if (PortalDoorPosition.HasValue && Near(PortalDoorPosition.Value, flat, distance + 3f)) return true;
        foreach (var boulder in BoulderSpots)
            if (Near(boulder, flat, distance + 2f)) return true;
        foreach (var pond in ponds)
            if (Near(pond.center, flat, distance + pond.radius * 0.9f)) return true;
        return WatchtowerPosition.HasValue && Near(WatchtowerPosition.Value, flat, distance + 2f);
    }

    bool Near(Vector2 structure, Vector2 flat, float distance)
    {
        Vector2 d = structure - flat;
        if (wrapWorld)
        {
            // Measure across the map's edges too.
            d.x -= areaSize * Mathf.Round(d.x / areaSize);
            d.y -= areaSize * Mathf.Round(d.y / areaSize);
        }
        return d.sqrMagnitude < distance * distance;
    }

    /// <summary>Every grass patch, for WeatherSystem to sway in the wind.</summary>
    public readonly List<Transform> GrassPivots = new List<Transform>();

    [Header("Material Overrides (optional)")]
    public Material groundMaterial;
    [Tooltip("Tint over the ground's soil texture: lower it to darken the earth.")]
    public Color groundTint = new Color(0.85f, 0.85f, 0.85f);
    public Material barkMaterial;
    public Material foliageMaterial;
    public Material rockMaterial;

    public const string RootName = "Generated Forest";

    System.Random rng;
    Transform root;
    Vector2 noiseOffset;

    // The ground is a sum of sine waves that each fit a whole number of times across the map,
    // so its edges line up perfectly and it can tile when the world wraps.
    struct TerrainWave
    {
        public float kx, kz, amplitude, phase;
    }
    TerrainWave[] terrainWaves;
    Material groundMat, barkMat, deadBarkMat, rockMat;
    Material[] foliageMats;
    Mesh sphereMesh;

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
        BuildTerrainWaves();
        PlanPonds();

        root = new GameObject(RootName).transform;
        root.SetParent(transform, false);

        if (wrapWorld && Application.isPlaying)
        {
            var wrap = FindAnyObjectByType<WorldWrap>();
            if (wrap == null) wrap = new GameObject("World Wrap").AddComponent<WorldWrap>();
            wrap.size = areaSize;
            wrap.center = transform.position;
        }

        CreateMaterials();

        BuildGround();
        if (!wrapWorld) BuildBoundary();
        CampsitePositions.Clear(); // before the others, so old camps don't count as obstacles
        CarClearings.Clear();
        PortalDoorPosition = null;
        BoulderSpots.Clear();
        PlaceShacks();
        PlaceWatchtower();
        PlaceCampsites();
        PlaceCar();
        PlacePortalDoor();
        ScatterTrees();
        ScatterRocks();
        ScatterLogs();
        ScatterGrass();
        ScatterBushes();
        BuildWater();

        if (applyLighting) ApplyAtmosphere();
        if (spawnPlayer) SpawnPlayer();
        if (spawnEntity && FindAnyObjectByType<ShadowEntity>() == null)
            ShadowEntity.Spawn(ShadowEntity.FirstEntityDelay); // a minute of calm before it starts
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
        if (spawnIntro && Application.isPlaying && FindAnyObjectByType<LampNarration>() == null)
            new GameObject("Lamp Narration").AddComponent<LampNarration>();
        if (Application.isPlaying && FindAnyObjectByType<PauseMenu>() == null)
            new GameObject("Pause Menu").AddComponent<PauseMenu>();
        if (spawnLeaves && Application.isPlaying && FindAnyObjectByType<FallingLeaves>() == null)
            new GameObject("Falling Leaves").AddComponent<FallingLeaves>();
        if (spawnRain && Application.isPlaying && FindAnyObjectByType<Tornadoes>() == null)
            new GameObject("Tornadoes").AddComponent<Tornadoes>();
        if (spawnRain && Application.isPlaying && FindAnyObjectByType<RainDrops>() == null)
            new GameObject("Rain").AddComponent<RainDrops>();
        if (spawnLeaves && Application.isPlaying && FindAnyObjectByType<GroundLeaves>() == null)
            new GameObject("Ground Leaves").AddComponent<GroundLeaves>();
        if (spawnEntity && Application.isPlaying && FindAnyObjectByType<BlindEntitySpawner>() == null)
            new GameObject("Blind Entity Spawner").AddComponent<BlindEntitySpawner>();
        if (spawnEntity && Application.isPlaying && FindAnyObjectByType<FlyingEntitySpawner>() == null)
            new GameObject("Flying Entity Spawner").AddComponent<FlyingEntitySpawner>();
        if (Application.isPlaying && FindAnyObjectByType<DevTools>() == null)
            new GameObject("Dev Tools").AddComponent<DevTools>();
        if (Application.isPlaying && FindAnyObjectByType<SpawnDebugOverlay>() == null)
            new GameObject("Spawn Debug Overlay").AddComponent<SpawnDebugOverlay>();
        if (Application.isPlaying && FindAnyObjectByType<MusicManager>() == null)
            new GameObject("Music").AddComponent<MusicManager>();
        if (Application.isPlaying && FindAnyObjectByType<EntityDread>() == null)
            new GameObject("Entity Dread").AddComponent<EntityDread>();
        if (Application.isPlaying && FindAnyObjectByType<VineSystem>() == null)
            new GameObject("Vines").AddComponent<VineSystem>();
        if (Application.isPlaying && FindAnyObjectByType<NightSky>() == null)
            new GameObject("Sky").AddComponent<NightSky>();
        if (Application.isPlaying && FindAnyObjectByType<Lightning>() == null)
            new GameObject("Lightning").AddComponent<Lightning>();
        if (Application.isPlaying && FindAnyObjectByType<ScreamEvent>() == null)
            new GameObject("Scream Event").AddComponent<ScreamEvent>();
        if (spawnWildlife && FindAnyObjectByType<Wildlife>() == null)
            new GameObject("Wildlife").AddComponent<Wildlife>();
        if (spawnFallingTree && Application.isPlaying && FindAnyObjectByType<FallingTreeEvent>() == null)
            new GameObject("Falling Tree Event").AddComponent<FallingTreeEvent>();
        if (spawnWatcher && Application.isPlaying && FindAnyObjectByType<WatcherEvent>() == null)
            new GameObject("Watcher Event").AddComponent<WatcherEvent>();

        if (Application.isPlaying)
        {
            var wrap = FindAnyObjectByType<WorldWrap>();
            if (wrapWorld && wrap != null)
            {
                wrap.CollectFromGenerator(this);
            }
            else
            {
                // Batch the things that never move. Trees are left out so the wind can sway them,
                // and nothing is batched when the world wraps, since wrapping moves things around.
                StaticBatchingUtility.Combine(root.Find("Rocks").gameObject);
                StaticBatchingUtility.Combine(root.Find("Fallen Logs").gameObject);
            }
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
        float h = BaseHeight(x, z);
        // Ponds: smooth bowls pressed into the ground.
        foreach (var pond in ponds)
        {
            float dx = Wrapped(x - pond.center.x), dz = Wrapped(z - pond.center.y);
            float t = Mathf.Sqrt(dx * dx + dz * dz) / pond.radius;
            if (t < 1.4f) h -= pond.depth * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 1.4f, t)));
        }
        return h;
    }

    /// <summary>The rolling ground before anything is pressed into it.</summary>
    float BaseHeight(float x, float z)
    {
        if (terrainWaves == null) return 0f;
        float h = 0f;
        foreach (var w in terrainWaves)
            h += w.amplitude * Mathf.Sin(w.kx * x + w.kz * z + w.phase);
        return h;
    }

    /// <summary>A difference along one axis, the short way round when the map loops.</summary>
    float Wrapped(float d) => wrapWorld ? Mathf.Repeat(d + areaSize * 0.5f, areaSize) - areaSize * 0.5f : d;

    struct PondShape
    {
        public Vector2 center;
        public float radius, depth, level;
    }
    readonly List<PondShape> ponds = new List<PondShape>();

    /// <summary>
    /// Picks the ponds before the ground is built (they shape it): away from the spawn and each other. Each
    /// bowl is made deep enough that its middle sits well under the water, and the water level is the lowest
    /// point of its rim, so the shoreline follows the lie of the land.
    /// </summary>
    void PlanPonds()
    {
        ponds.Clear();
        Water.Clear();
        float half = areaSize * 0.5f - 20f;
        for (int attempt = 0; attempt < 300 && ponds.Count < pondCount; attempt++)
        {
            var c = new Vector2(Range(-half, half), Range(-half, half));
            if (c.magnitude < 35f) continue;
            bool tooClose = false;
            foreach (var other in ponds) if ((other.center - c).magnitude < 55f) tooClose = true;
            if (tooClose) continue;

            var pond = new PondShape { center = c, radius = Range(minPondRadius, maxPondRadius), depth = 1.6f };
            ponds.Add(pond);
            for (int pass = 0; pass < 3; pass++)
            {
                // Water level: the lowest point round the rim.
                float level = float.MaxValue;
                for (int k = 0; k < 24; k++)
                {
                    float a = k / 24f * Mathf.PI * 2f;
                    level = Mathf.Min(level, GetGroundHeight(c.x + Mathf.Cos(a) * pond.radius, c.y + Mathf.Sin(a) * pond.radius));
                }
                pond.level = level - 0.1f;
                float bottom = GetGroundHeight(c.x, c.y);
                if (pond.level - bottom >= 0.9f) break;
                pond.depth += 0.9f - (pond.level - bottom) + 0.1f;
                ponds[ponds.Count - 1] = pond;
            }
            ponds[ponds.Count - 1] = pond;
        }
    }

    void BuildTerrainWaves()
    {
        var waves = new List<TerrainWave>();
        AddWaves(waves, 8, 40f, 0.35f);   // broad rolling hills
        AddWaves(waves, 12, 10f, 0.06f);  // small bumps
        terrainWaves = waves.ToArray();
    }

    void AddWaves(List<TerrainWave> waves, int count, float minWavelength, float amplitude)
    {
        float k = Mathf.PI * 2f / areaSize;
        int maxCycles = Mathf.Max(1, Mathf.RoundToInt(areaSize / minWavelength));
        for (int i = 0; i < count; i++)
        {
            int nx, nz;
            do
            {
                nx = rng.Next(-maxCycles, maxCycles + 1);
                nz = rng.Next(-maxCycles, maxCycles + 1);
            } while (nx == 0 && nz == 0);

            waves.Add(new TerrainWave
            {
                kx = nx * k,
                kz = nz * k,
                amplitude = groundBumpiness * amplitude * Range(0.6f, 1.2f),
                phase = Range(0f, Mathf.PI * 2f),
            });
        }
    }

    Vector3 GroundNormal(float x, float z)
    {
        const float e = 0.25f;
        float dx = GetGroundHeight(x + e, z) - GetGroundHeight(x - e, z);
        float dz = GetGroundHeight(x, z + e) - GetGroundHeight(x, z - e);
        return new Vector3(-dx, 2f * e, -dz).normalized;
    }

    void BuildGround()
    {
        int res = Mathf.Clamp(Mathf.RoundToInt(areaSize / groundVertexSpacing), 2, 512);
        float half = areaSize * 0.5f;
        float step = areaSize / res;

        var verts = new Vector3[(res + 1) * (res + 1)];
        var normals = new Vector3[verts.Length];
        var uvs = new Vector2[verts.Length];
        var tris = new int[res * res * 6];

        for (int z = 0, i = 0; z <= res; z++)
        {
            for (int x = 0; x <= res; x++, i++)
            {
                float wx = -half + x * step;
                float wz = -half + z * step;
                verts[i] = new Vector3(wx, GetGroundHeight(wx, wz), wz);
                normals[i] = GroundNormal(wx, wz); // computed from the height function so tile edges shade seamlessly
                uvs[i] = new Vector2(wx, wz) * 0.35f; // the soil texture repeats about every 3m
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
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();

        // When the world wraps, surround the map with copies of the ground so the far side is always underfoot.
        int copies = wrapWorld ? 1 : 0;
        for (int tz = -copies; tz <= copies; tz++)
        {
            for (int tx = -copies; tx <= copies; tx++)
            {
                var go = new GameObject(tx == 0 && tz == 0 ? "Ground" : $"Ground ({tx}, {tz})");
                go.transform.SetParent(root, false);
                go.transform.localPosition = new Vector3(tx * areaSize, 0f, tz * areaSize);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = groundMat;
                go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }
        }
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

        float half = areaSize * 0.5f - (wrapWorld ? 0f : 1f); // no gap at the seam when the map loops
        var placed = TreePositions;
        placed.Clear();
        SwayPivots.Clear();
        VineAnchors.Clear();
        var grid = new Dictionary<Vector2Int, List<Vector2>>();
        float cell = Mathf.Max(minTreeSpacing, 0.1f);
        int attempts = treeCount * 15;

        for (int a = 0; a < attempts && placed.Count < treeCount; a++)
        {
            var p = new Vector2(Range(-half, half), Range(-half, half));
            float distFromCenter = p.magnitude;
            if (distFromCenter < clearingRadius) continue;
            if (IsNearStructure(new Vector3(p.x, 0f, p.y), 6.5f)) continue;

            // Patchy density: thin out trees in low-noise areas, thicker near the edges.
            float density = Mathf.PerlinNoise((p.x + noiseOffset.y) * densityNoiseScale, (p.y + noiseOffset.x) * densityNoiseScale);
            // Without wrapping, the edges get thicker so the walls feel like dense forest.
            float edgeBoost = wrapWorld ? 0f : Mathf.InverseLerp(half * 0.6f, half, Mathf.Max(Mathf.Abs(p.x), Mathf.Abs(p.y)));
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
            else
            {
                // Now and then an old giant, where there's room for one.
                float size = Range(0f, 1f) < bigTreeChance && !Crowded(p, 3.5f) ? Range(minBigTreeSize, maxBigTreeSize) : 1f;
                if (size > 1f)
                    for (int k = 0; k < 6; k++) // keep later trees back from its wide trunk
                        AddToGrid(p + new Vector2(Mathf.Cos(k * Mathf.PI / 3f), Mathf.Sin(k * Mathf.PI / 3f)) * 1.6f, grid, cell);
                if (Range(0f, 1f) < deadTreeRatio) BuildDeadTree(pos, parent, size);
                else BuildPineTree(pos, parent, size);
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

    /// <summary>True if another tree already stands within distance of p.</summary>
    bool Crowded(Vector2 p, float distance)
    {
        foreach (var other in TreePositions)
        {
            float d = (other - p).sqrMagnitude;
            if (d > 0.0001f && d < distance * distance) return true;
        }
        return false;
    }

    /// <summary>A pine. size above 1 makes one of the rare giants (taller, wider, and thicker in the trunk).</summary>
    void BuildPineTree(Vector3 pos, Transform parent, float size = 1f)
    {
        float scale = Range(0.8f, 1.6f) * size;
        float trunkHeight = Range(6f, 10f) * scale;
        float trunkRadius = Range(0.18f, 0.3f) * scale * (size > 1f ? 1.15f : 1f);

        var tree = new GameObject("Pine");
        tree.transform.SetParent(parent, false);
        tree.transform.localPosition = pos;
        tree.transform.localRotation = Quaternion.Euler(Range(-3f, 3f), Range(0f, 360f), Range(-3f, 3f));

        var shape = new TrunkShape(this, trunkHeight, trunkRadius, 0.45f, scale);
        float foliageStart = trunkHeight * Range(0.3f, 0.45f);
        bool hasVines = Range(0f, 1f) < vineTreeChance;
        var vineLimbs = new List<Vector3[]>();

        Transform sway = AddTrunk(tree.transform, shape, barkMat, "Trunk", bark =>
        {
            // Vines need something to hang from: a sturdy dead branch just under the lowest boughs, reaching out.
            if (hasVines)
            {
                int count = rng.Next(1, 4);
                for (int i = 0; i < count; i++)
                {
                    float y = foliageStart - Range(0.15f, 0.5f);
                    Vector3 dir = Quaternion.Euler(0f, Range(0f, 360f), 0f) * Vector3.forward;
                    dir = (dir + Vector3.up * Range(0f, 0.15f)).normalized;
                    vineLimbs.Add(AddLimb(bark, shape, y, dir, Range(1f, 1.8f) * scale, shape.RadiusAt(y) * Range(0.3f, 0.4f), 0.1f, 0));
                }
            }

            // Snapped-off dead branch stubs on the bare lower trunk.
            int stubs = rng.Next(3, 8);
            for (int i = 0; i < stubs; i++)
            {
                float y = Range(trunkHeight * 0.12f, foliageStart);
                Vector3 dir = Quaternion.Euler(0f, Range(0f, 360f), 0f) * Vector3.forward;
                dir = (dir + Vector3.up * Range(-0.3f, 0.25f)).normalized;
                AddLimb(bark, shape, y, dir, Range(0.25f, 0.7f) * scale, shape.RadiusAt(y) * Range(0.2f, 0.32f), 0f, 0);
            }
        });

        // Layers of drooping, star-shaped boughs, starting high so the lower trunk is bare and creepy.
        var foliage = new MeshBuilder();
        int layers = rng.Next(5, 9);
        float foliageSpan = trunkHeight * 1.05f - foliageStart;
        float baseRadius = Range(1.6f, 2.4f) * scale;
        for (int i = 0; i < layers; i++)
        {
            float t = i / (float)layers;
            float y = foliageStart + foliageSpan * t;
            float radius = baseRadius * Mathf.Lerp(1f, 0.25f, Mathf.Pow(t, 0.9f)) * Range(0.85f, 1.15f);
            float height = foliageSpan / layers * Range(1.5f, 2.1f);
            AddBoughLayer(foliage, shape.Center(y), radius, height, Range(-6f, 6f), Range(-6f, 6f));
        }
        var foliageObject = new GameObject("Foliage");
        foliageObject.transform.SetParent(sway, false);
        foliageObject.AddComponent<MeshFilter>().sharedMesh = foliage.ToMesh("Foliage", false);
        foliageObject.AddComponent<MeshRenderer>().sharedMaterial = foliageMats[rng.Next(foliageMats.Length)];

        // The vines, each hanging from near the end of its branch.
        foreach (var path in vineLimbs)
        {
            Vector3 at = path[path.Length - 2];
            AddVine(sway, at, Range(1.5f, Mathf.Max(1.6f, at.y - 0.6f)));
        }
    }

    void AddVine(Transform sway, Vector3 local, float length)
    {
        VineAnchors.Add(new VineAnchor { sway = sway, local = local, length = length });
    }

    /// <summary>A dead tree. size above 1 makes one of the rare giants.</summary>
    void BuildDeadTree(Vector3 pos, Transform parent, float size = 1f)
    {
        float scale = Range(0.7f, 1.3f) * size;
        float trunkHeight = Range(4f, 8f) * scale;
        float trunkRadius = Range(0.12f, 0.25f) * scale * (size > 1f ? 1.15f : 1f);

        var tree = new GameObject("Dead Tree");
        tree.transform.SetParent(parent, false);
        tree.transform.localPosition = pos;
        tree.transform.localRotation = Quaternion.Euler(Range(-8f, 8f), Range(0f, 360f), Range(-8f, 8f));

        var shape = new TrunkShape(this, trunkHeight, trunkRadius, 0.3f, scale);
        bool vines = Range(0f, 1f) < vineTreeChance;
        var limbs = new List<Vector3[]>();
        Transform sway = AddTrunk(tree.transform, shape, deadBarkMat, "Bark", bark =>
        {
            // Gnarled bare branches reaching outward and up, forking into twigs.
            int branches = rng.Next(3, 7);
            for (int i = 0; i < branches; i++)
            {
                float y = trunkHeight * Range(0.45f, 0.92f);
                Vector3 dir = Quaternion.Euler(0f, Range(0f, 360f), 0f) * Quaternion.Euler(-Range(20f, 65f), 0f, 0f) * Vector3.forward;
                limbs.Add(AddLimb(bark, shape, y, dir, Range(1f, 2.8f) * scale, shape.RadiusAt(y) * Range(0.4f, 0.6f), 0.35f, rng.Next(1, 4)));
            }
        });

        // Now and then, vines draped over the bare branches, some long enough to trail on the ground.
        if (vines)
        {
            foreach (var path in limbs)
            {
                if (Range(0f, 1f) < 0.35f) continue;
                Vector3 at = path[rng.Next(2, path.Length)];
                AddVine(sway, at, Range(1.5f, at.y + 0.5f));
            }
        }
    }

    /// <summary>
    /// The outline of one tree's trunk: tapering, lumpy and slightly bent, with a flared base that
    /// swells out into buttresses at the foot. Heights are measured up from the ground.
    /// </summary>
    class TrunkShape
    {
        public readonly float height, radius, topRadius;
        public readonly float[] rootAngles;
        readonly Vector3 bendDir, wobbleDir;
        readonly float bend, phase1, phase2, phase3, flare;

        public TrunkShape(DarkForestGenerator g, float height, float radius, float topRadius, float scale)
        {
            this.height = height;
            this.radius = radius;
            this.topRadius = topRadius;
            float a = g.Range(0f, Mathf.PI * 2f), b = g.Range(0f, Mathf.PI * 2f);
            bendDir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            wobbleDir = new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b));
            bend = g.Range(0f, 0.3f) * scale;
            phase1 = g.Range(0f, 10f);
            phase2 = g.Range(0f, 10f);
            phase3 = g.Range(0f, 10f);
            flare = g.Range(0.6f, 1.1f);

            rootAngles = new float[g.rng.Next(3, 6)];
            float start = g.Range(0f, Mathf.PI * 2f);
            for (int i = 0; i < rootAngles.Length; i++)
                rootAngles[i] = start + (i + g.Range(-0.25f, 0.25f)) / rootAngles.Length * Mathf.PI * 2f;
        }

        /// <summary>Centre of the trunk at a height.</summary>
        public Vector3 Center(float y)
        {
            float t = Mathf.Clamp01(y / height);
            Vector3 c = bendDir * bend * t * t + wobbleDir * 0.05f * Mathf.Sin(y * 1.1f + phase3) * Mathf.Clamp01(y * 0.5f);
            c.y = y;
            return c;
        }

        /// <summary>Average radius at a height (ignoring lumps and buttresses).</summary>
        public float RadiusAt(float y) => radius * Mathf.Lerp(1f, topRadius, Mathf.Clamp01(y / height));

        /// <summary>Radius at a height, in a direction around the trunk.</summary>
        public float Radius(float y, float angle)
        {
            float r = RadiusAt(y);
            float lumps = 1f + 0.07f * Mathf.Sin(3f * angle + phase1 + y * 0.8f) + 0.045f * Mathf.Sin(7f * angle + phase2 - y * 1.7f);

            // Buttresses: the base swells out most towards each root.
            float lobes = 0f;
            foreach (float root in rootAngles)
                lobes = Mathf.Max(lobes, Mathf.Pow(Mathf.Max(0f, Mathf.Cos(angle - root)), 4f));
            float swell = flare * Mathf.Exp(-Mathf.Max(y, 0f) / (0.5f + radius * 1.5f)) * (0.35f + 0.65f * lobes);
            return r * lumps + radius * swell;
        }
    }

    /// <summary>
    /// Adds a fixed trunk collider to the tree, and a "Sway" pivot at its base holding the visible trunk
    /// (a mesh called meshName, with anything addParts adds). Foliage and branches go under the pivot, so
    /// the wind can bend the whole tree without moving the collider.
    /// </summary>
    Transform AddTrunk(Transform tree, TrunkShape shape, Material mat, string meshName, System.Action<MeshBuilder> addParts)
    {
        var col = tree.gameObject.AddComponent<CapsuleCollider>();
        col.radius = shape.radius;
        col.height = shape.height;
        col.center = new Vector3(0f, shape.height * 0.5f, 0f);

        var sway = new GameObject("Sway").transform;
        sway.SetParent(tree, false);
        SwayPivots.Add(sway);

        // The trunk itself, sunk well into the ground so slopes never show a gap under it.
        var bark = new MeshBuilder();
        const int rings = 14;
        var centers = new Vector3[rings + 1];
        for (int j = 0; j < rings; j++)
        {
            float t = j / (float)(rings - 1);
            centers[j] = shape.Center(-0.5f + (shape.height + 0.5f) * Mathf.Pow(t, 1.7f)); // denser rings low down, where the flare is
        }
        centers[rings] = shape.Center(shape.height + shape.radius * 0.5f); // closes the top in a blunt point
        bark.AddTube(centers, (j, angle) => j == rings ? 0.01f : shape.Radius(centers[j].y, angle), 12, TileCount(shape.radius), 1.6f);
        addParts?.Invoke(bark);

        var trunk = new GameObject(meshName);
        trunk.transform.SetParent(sway, false);
        trunk.AddComponent<MeshFilter>().sharedMesh = bark.ToMesh(meshName, true);
        trunk.AddComponent<MeshRenderer>().sharedMaterial = mat;

        return sway;
    }


    /// <summary>How many times the bark texture wraps around a trunk, so it isn't stretched on thick ones.</summary>
    static int TileCount(float radius) => Mathf.Max(1, Mathf.RoundToInt(radius * Mathf.PI * 2f / 0.7f));

    /// <summary>
    /// A branch leaving the trunk at a height: a tapering, slightly curving limb, with forked twigs near its end.
    /// curl bends it upward along its length.
    /// </summary>
    Vector3[] AddLimb(MeshBuilder mesh, TrunkShape shape, float y, Vector3 dir, float length, float thickness, float curl, int twigs)
    {
        Vector3 start = shape.Center(y) + new Vector3(dir.x, 0f, dir.z).normalized * shape.RadiusAt(y) * 0.5f;
        Vector3 kink = Random3() * 0.25f;
        const int steps = 6;
        var path = new Vector3[steps];
        var widths = new float[steps];
        for (int i = 0; i < steps; i++)
        {
            float t = i / (float)(steps - 1);
            path[i] = start + (dir * t + Vector3.up * curl * t * t + kink * Mathf.Sin(t * Mathf.PI)) * length;
            widths[i] = thickness * (1f - 0.85f * t) + 0.006f;
        }
        mesh.AddTube(path, (i, a) => widths[i], 6, 1, 1.2f);

        for (int k = 0; k < twigs; k++)
        {
            float t = Range(0.45f, 0.85f);
            int i = Mathf.Min(steps - 2, Mathf.FloorToInt(t * (steps - 1)));
            Vector3 from = Vector3.Lerp(path[i], path[i + 1], t * (steps - 1) - i);
            Vector3 along = (path[i + 1] - path[i]).normalized;
            Vector3 twigDir = (along + Random3() * 0.9f + Vector3.up * 0.3f).normalized;
            float twigLength = length * Range(0.25f, 0.45f);
            float twigWidth = Mathf.Lerp(widths[i], widths[i + 1], 0.5f) * 0.6f;
            Vector3 twigKink = Random3() * 0.3f;

            var twig = new Vector3[4];
            var twigWidths = new float[4];
            for (int j = 0; j < 4; j++)
            {
                float u = j / 3f;
                twig[j] = from + (twigDir * u + twigKink * u * u) * twigLength;
                twigWidths[j] = twigWidth * (1f - 0.9f * u) + 0.004f;
            }
            mesh.AddTube(twig, (j, a) => twigWidths[j], 5, 1, 1.2f);
        }
        return path;
    }

    Vector3 Random3() => new Vector3(Range(-1f, 1f), Range(-1f, 1f), Range(-1f, 1f));

    /// <summary>
    /// One layer of pine boughs: a ring of branch tips drooping at the ends with gaps between them, sloping up
    /// to the trunk, with a hollow underside (what you see looking up into the canopy). Needles run outward.
    /// </summary>
    void AddBoughLayer(MeshBuilder mesh, Vector3 center, float radius, float height, float tiltX, float tiltZ)
    {
        int tips = rng.Next(7, 12);
        int points = tips * 2;
        float rotation = Range(0f, Mathf.PI * 2f);
        Quaternion tilt = Quaternion.Euler(tiltX, 0f, tiltZ);

        var tipRadius = new float[points + 1];
        var tipDroop = new float[points + 1];
        for (int k = 0; k < points; k++)
        {
            bool tip = k % 2 == 0;
            tipRadius[k] = tip ? Range(0.85f, 1.15f) : Range(0.5f, 0.65f);
            tipDroop[k] = tip ? Range(0.08f, 0.22f) * radius : -0.04f * radius;
        }
        tipRadius[points] = tipRadius[0];
        tipDroop[points] = tipDroop[0];

        // Rings from the outer edge inward: (fraction of each point's radius, height above the layer base).
        Vector3 Point(int k, float radial, float up, float droop)
        {
            float a = rotation + k / (float)points * Mathf.PI * 2f;
            float r = radius * Mathf.Lerp(radial, tipRadius[k] * radial, radial);
            return center + tilt * new Vector3(Mathf.Cos(a) * r, up - tipDroop[k] * droop, Mathf.Sin(a) * r);
        }
        var top = new[] { (1f, 0f, 1f), (0.55f, height * 0.45f, 0.3f), (0f, height, 0f) };
        var under = new[] { (1f, 0f, 1f), (0.45f, height * 0.2f, 0.2f), (0f, height * 0.4f, 0f) };

        AddSurface(mesh, Point, top, points, tips, radius, false);
        AddSurface(mesh, Point, under, points, tips, radius, true);
    }

    static void AddSurface(MeshBuilder mesh, System.Func<int, float, float, float, Vector3> point, (float radial, float up, float droop)[] rings,
        int points, int tips, float radius, bool flip)
    {
        int first = mesh.vertices.Count;
        foreach (var ring in rings)
        {
            for (int k = 0; k <= points; k++)
            {
                mesh.vertices.Add(point(k, ring.radial, ring.up, ring.droop));
                // Each bough is one width of texture; v runs in toward the trunk.
                mesh.uvs.Add(new Vector2(k / (float)points * tips, (1f - ring.radial) * radius / 1.2f));
            }
            if (mesh.vertices.Count - first > points + 1) mesh.seams.Add((mesh.vertices.Count - points - 1, mesh.vertices.Count - 1));
        }
        mesh.seams.Add((first, first + points));
        mesh.AddGrid(first, rings.Length, points, flip);
    }

    /// <summary>Collects vertices and triangles for a procedural mesh (tubes for trunks and branches, bough layers).</summary>
    class MeshBuilder
    {
        public readonly List<Vector3> vertices = new List<Vector3>();
        public readonly List<Vector2> uvs = new List<Vector2>();
        public readonly List<int> triangles = new List<int>();
        /// <summary>Pairs of vertices at the same spot (where the texture wraps), whose normals get averaged.</summary>
        public readonly List<(int, int)> seams = new List<(int, int)>();

        /// <summary>
        /// A tube along a path. radius(ring, angle) gives its outline; it wraps the texture `wraps` times around,
        /// one texture height per metresPerV along its length. flatten squashes it top to bottom.
        /// </summary>
        public void AddTube(IList<Vector3> path, System.Func<int, float, float> radius, int segments, int wraps, float metresPerV, float flatten = 1f)
        {
            int first = vertices.Count;
            float v = 0f;
            Vector3 side = Vector3.zero;
            for (int j = 0; j < path.Count; j++)
            {
                Vector3 forward = (path[Mathf.Min(j + 1, path.Count - 1)] - path[Mathf.Max(j - 1, 0)]).normalized;
                // Carry the frame along the path so the tube doesn't twist.
                if (j == 0) side = Vector3.Cross(Mathf.Abs(forward.y) < 0.95f ? Vector3.up : Vector3.right, forward).normalized;
                else side = (side - forward * Vector3.Dot(side, forward)).normalized;
                Vector3 up = Vector3.Cross(forward, side);
                if (j > 0) v += Vector3.Distance(path[j], path[j - 1]) / metresPerV;

                for (int k = 0; k <= segments; k++)
                {
                    float a = k / (float)segments * Mathf.PI * 2f;
                    float r = radius(j, a);
                    vertices.Add(path[j] + up * (Mathf.Cos(a) * r * flatten) + side * (Mathf.Sin(a) * r));
                    uvs.Add(new Vector2(k / (float)segments * wraps, v));
                }
                seams.Add((vertices.Count - segments - 1, vertices.Count - 1));
            }
            AddGrid(first, path.Count, segments, false);
        }

        /// <summary>Triangles joining rings of (columns + 1) vertices, each ring to the next.</summary>
        public void AddGrid(int first, int rings, int columns, bool flip)
        {
            int stride = columns + 1;
            for (int j = 0; j < rings - 1; j++)
            {
                for (int k = 0; k < columns; k++)
                {
                    int a = first + j * stride + k, b = a + stride, c = a + 1, d = b + 1;
                    if (flip) { triangles.Add(a); triangles.Add(c); triangles.Add(b); triangles.Add(c); triangles.Add(d); triangles.Add(b); }
                    else { triangles.Add(a); triangles.Add(b); triangles.Add(c); triangles.Add(c); triangles.Add(b); triangles.Add(d); }
                }
            }
        }

        public Mesh ToMesh(string name, bool tangents)
        {
            var mesh = new Mesh { name = name };
            if (vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();

            // Smooth over the texture seams.
            var normals = mesh.normals;
            foreach (var (a, b) in seams)
            {
                Vector3 n = (normals[a] + normals[b]).normalized;
                normals[a] = normals[b] = n;
            }
            mesh.normals = normals;
            if (tangents) mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    // ---------------------------------------------------------------- Shacks

    void PlaceShacks()
    {
        var parent = new GameObject("Shacks").transform;
        parent.SetParent(root, false);
        ShackPositions.Clear();

        float half = areaSize * 0.5f - 10f;
        for (int attempt = 0; attempt < 500 && ShackPositions.Count < shackCount; attempt++)
        {
            var p = new Vector2(Range(-half, half), Range(-half, half));
            if (p.magnitude < 30f) continue; // not right next to the spawn
            if (IsNearStructure(new Vector3(p.x, 0f, p.y), minShackSpacing)) continue;
            ShackPositions.Add(p);

            var go = new GameObject("Shack");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(p.x, GetGroundHeight(p.x, p.y), p.y);
            go.transform.localRotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
            go.AddComponent<Shack>().Build(this, rng);
        }
    }

    void PlaceCampsites()
    {
        var parent = new GameObject("Campsites").transform;
        parent.SetParent(root, false);

        float half = areaSize * 0.5f - 10f;
        int placed = 0;
        for (int attempt = 0; attempt < 500 && placed < campsiteCount; attempt++)
        {
            var p = new Vector2(Range(-half, half), Range(-half, half));
            if (p.magnitude < 25f) continue;
            if (IsNearStructure(new Vector3(p.x, 0f, p.y), 30f)) continue;
            // Camps need fairly level ground: the tent, fire and seat sit within ~3.5m of the middle.
            float lo = float.MaxValue, hi = float.MinValue;
            for (int k = 0; k < 12; k++)
            {
                float a = k / 12f * Mathf.PI * 2f;
                float h = GetGroundHeight(p.x + Mathf.Cos(a) * 3.5f, p.y + Mathf.Sin(a) * 3.5f);
                lo = Mathf.Min(lo, h);
                hi = Mathf.Max(hi, h);
            }
            if (hi - lo > 0.6f && attempt < 400) continue;
            bool tooClose = false;
            foreach (var other in CampsitePositions)
                if (Near(other, p, minCampsiteSpacing)) { tooClose = true; break; }
            if (tooClose) continue;

            CampsitePositions.Add(p);
            var go = new GameObject("Campsite");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(p.x, GetGroundHeight(p.x, p.y), p.y);
            go.transform.localRotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
            go.AddComponent<Campsite>().Build(this, rng);
            placed++;
        }
    }

    /// <summary>Parks the broken-down car somewhere at random, settled onto the slope of the ground.</summary>
    void PlaceCar()
    {
        if (!spawnCar) return;
        var parent = new GameObject("Car").transform;
        parent.SetParent(root, false);

        float half = areaSize * 0.5f - 15f;
        for (int attempt = 0; attempt < 500; attempt++)
        {
            var p = new Vector2(Range(-half, half), Range(-half, half));
            if (p.magnitude < 30f) continue;
            if (IsNearStructure(new Vector3(p.x, 0f, p.y), 30f)) continue;

            var yaw = Quaternion.Euler(0f, Range(0f, 360f), 0f);
            Vector3 fwd = yaw * Vector3.forward, right = yaw * Vector3.right;
            float H(Vector3 offset) => GetGroundHeight(p.x + offset.x, p.y + offset.z);
            float front = H(fwd * 1.4f), back = H(-fwd * 1.4f), left = H(-right * 0.85f), rightH = H(right * 0.85f);
            float pitch = -Mathf.Atan2(front - back, 2.8f) * Mathf.Rad2Deg;
            float roll = Mathf.Atan2(rightH - left, 1.7f) * Mathf.Rad2Deg;

            var go = new GameObject("Broken Car");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(p.x, GetGroundHeight(p.x, p.y) - 0.05f, p.y);
            go.transform.localRotation = yaw * Quaternion.Euler(pitch, 0f, roll);
            go.AddComponent<BrokenCar>().Build(rng);

            CarClearings.Add(p);
            CarClearings.Add(p + new Vector2(fwd.x, fwd.z) * 9f); // keep the headlight beams fairly open
            return;
        }
    }

    /// <summary>
    /// Stands the lone door somewhere at random, well inside the map (its room is built far below it and
    /// mustn't reach past the edge where the map loops).
    /// </summary>
    void PlacePortalDoor()
    {
        if (!spawnPortalDoor) return;
        var parent = new GameObject("Portal Door").transform;
        parent.SetParent(root, false);

        // Look for the flattest ground: the door and its lamp posts need to stand level.
        float half = Mathf.Max(10f, areaSize * 0.5f - 75f);
        Vector2 best = Vector2.zero;
        float bestYaw = 0f, bestRange = float.MaxValue;
        for (int attempt = 0; attempt < 600 && bestRange > 0.06f; attempt++)
        {
            var p = new Vector2(Range(-half, half), Range(-half, half));
            if (p.magnitude < 30f) continue;
            if (IsNearStructure(new Vector3(p.x, 0f, p.y), 25f)) continue;
            float yaw = Range(0f, 360f);
            float range = GroundUnevenness(p, yaw);
            if (range < bestRange) { best = p; bestYaw = yaw; bestRange = range; }
        }
        if (bestRange == float.MaxValue) return;

        var go = new GameObject("Lone Door");
        go.transform.SetParent(parent, false);
        // Stood on the highest point under the frame, so no side of it is buried; the step below fills any gap.
        go.transform.localPosition = new Vector3(best.x, FootprintHeight(best, bestYaw, true), best.y);
        go.transform.localRotation = Quaternion.Euler(0f, bestYaw, 0f);
        var door = go.AddComponent<PortalDoor>();
        door.Build(rng);
        // Each lamp post on the ground right under it.
        foreach (Transform child in go.transform)
        {
            if (child.name != "Lamp Post") continue;
            Vector3 w = child.position;
            child.position = new Vector3(w.x, GetGroundHeight(w.x, w.z) - 0.05f, w.z);
        }
        door.BuildGarden(this);
        PortalDoorPosition = best;
    }

    /// <summary>Spots under the lone door's frame and lamp posts, for a position and facing.</summary>
    IEnumerable<Vector2> DoorFootprint(Vector2 p, float yaw)
    {
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        foreach (var local in new[] { new Vector3(0f, 0f, 0f), new Vector3(-0.8f, 0f, 0f), new Vector3(0.8f, 0f, 0f),
                                      new Vector3(0f, 0f, 0.6f), new Vector3(0f, 0f, -0.6f),
                                      new Vector3(-1.6f, 0f, 0.35f), new Vector3(1.6f, 0f, 0.35f) })
        {
            Vector3 w = rot * local;
            yield return new Vector2(p.x + w.x, p.y + w.z);
        }
    }

    /// <summary>How much the ground under the door rises and falls (highest minus lowest), in metres.</summary>
    float GroundUnevenness(Vector2 p, float yaw)
    {
        float lo = float.MaxValue, hi = float.MinValue;
        foreach (var q in DoorFootprint(p, yaw))
        {
            float h = GetGroundHeight(q.x, q.y);
            lo = Mathf.Min(lo, h);
            hi = Mathf.Max(hi, h);
        }
        return hi - lo;
    }

    /// <summary>The highest (or lowest) ground under the door's frame itself.</summary>
    float FootprintHeight(Vector2 p, float yaw, bool highest)
    {
        float result = highest ? float.MinValue : float.MaxValue;
        int i = 0;
        foreach (var q in DoorFootprint(p, yaw))
        {
            if (i++ >= 5) break; // just the frame, not the lamp posts
            float h = GetGroundHeight(q.x, q.y);
            result = highest ? Mathf.Max(result, h) : Mathf.Min(result, h);
        }
        return result;
    }

    // ---------------------------------------------------------------- Water

    /// <summary>The ponds' water and reeds, and puddles on flat ground.</summary>
    void BuildWater()
    {
        var parent = new GameObject("Water").transform;
        parent.SetParent(root, false);

        // Dark, glassy, slowly rippling: it only shows what light falls on it (lamps, moon, flashlight).
        var water = MaterialFactory.LitTextured(null, ProceduralTextures.WaterNormal, new Color(0.012f, 0.016f, 0.02f), 0.96f);
        water.mainTextureScale = Vector2.one;
        parent.gameObject.AddComponent<WaterRipples>().material = water;
        var reedMat = MaterialFactory.Lit(new Color(0.07f, 0.08f, 0.04f), 0.1f);
        var headMat = MaterialFactory.Lit(new Color(0.1f, 0.06f, 0.035f), 0.05f);

        foreach (var pond in ponds)
        {
            var go = new GameObject("Pond");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(pond.center.x, pond.level, pond.center.y);
            go.AddComponent<MeshFilter>().sharedMesh = DiscMesh(pond.radius * 1.45f, 48, 0.25f);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = water;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            Water.AddPond(go.transform.position, pond.radius, pond.level);
            AddReeds(go.transform, pond, reedMat, headMat);
        }

        float half = areaSize * 0.5f - 4f;
        int puddles = 0;
        for (int attempt = 0; attempt < puddleCount * 30 && puddles < puddleCount; attempt++)
        {
            var p = new Vector2(Range(-half, half), Range(-half, half));
            float radius = Range(0.6f, 1.8f);
            if (p.magnitude < 6f || IsNearStructure(new Vector3(p.x, 0f, p.y), 2f) || NearAnyTree(p, radius + 0.6f)) continue;
            // Only where the ground is nearly level, or the water would run off.
            float lo = float.MaxValue, hi = float.MinValue;
            for (int k = 0; k < 8; k++)
            {
                float a = k / 8f * Mathf.PI * 2f;
                float h = GetGroundHeight(p.x + Mathf.Cos(a) * radius, p.y + Mathf.Sin(a) * radius);
                lo = Mathf.Min(lo, h);
                hi = Mathf.Max(hi, h);
            }
            if (hi - lo > 0.07f) continue;

            var go = new GameObject("Puddle");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(p.x, GetGroundHeight(p.x, p.y), p.y);
            go.AddComponent<MeshFilter>().sharedMesh = PuddleMesh(go.transform.localPosition, radius);
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = water;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            Water.AddPuddle(go.transform.position, radius * 0.9f);
            puddles++;
        }
    }

    /// <summary>A flat disc, its UVs in metres times uvScale (so ripples keep their size whatever the disc).</summary>
    static Mesh DiscMesh(float radius, int segments, float uvScale)
    {
        var verts = new Vector3[segments + 1];
        var uvs = new Vector2[segments + 1];
        var tris = new int[segments * 3];
        for (int i = 0; i < segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            verts[i + 1] = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
            uvs[i + 1] = new Vector2(verts[i + 1].x, verts[i + 1].z) * uvScale;
            tris[i * 3] = 0;
            tris[i * 3 + 1] = 1 + (i + 1) % segments;
            tris[i * 3 + 2] = 1 + i;
        }
        var mesh = new Mesh { name = "Water" };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>An irregular puddle, its edge hugging the ground just above it.</summary>
    Mesh PuddleMesh(Vector3 center, float radius)
    {
        const int segments = 20;
        float seed = Range(0f, 100f);
        var verts = new Vector3[segments + 1];
        var uvs = new Vector2[segments + 1];
        var tris = new int[segments * 3];
        verts[0] = new Vector3(0f, 0.015f, 0f);
        for (int i = 0; i < segments; i++)
        {
            float a = i / (float)segments * Mathf.PI * 2f;
            float r = radius * (0.75f + 0.45f * Mathf.PerlinNoise(Mathf.Cos(a) * 1.3f + seed, Mathf.Sin(a) * 1.3f + seed));
            float x = Mathf.Cos(a) * r, z = Mathf.Sin(a) * r;
            verts[i + 1] = new Vector3(x, GetGroundHeight(center.x + x, center.z + z) - center.y + 0.015f, z);
            uvs[i + 1] = new Vector2(x, z) * 0.25f;
            tris[i * 3] = 0;
            tris[i * 3 + 1] = 1 + (i + 1) % segments;
            tris[i * 3 + 2] = 1 + i;
        }
        uvs[0] = Vector2.zero;
        var mesh = new Mesh { name = "Puddle" };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Clumps of reeds and bulrushes along a pond's waterline, merged into one mesh.</summary>
    void AddReeds(Transform pondObject, PondShape pond, Material reedMat, Material headMat)
    {
        if (sphereMesh == null)
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Destroy(temp); else DestroyImmediate(temp);
        }
        var cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        Mesh stalkMesh = cylinder.GetComponent<MeshFilter>().sharedMesh;
        if (Application.isPlaying) Destroy(cylinder); else DestroyImmediate(cylinder);

        var stalks = new List<CombineInstance>();
        var heads = new List<CombineInstance>();
        int clumps = Mathf.RoundToInt(pond.radius * 1.4f);
        for (int c = 0; c < clumps; c++)
        {
            // Find the shore along a random direction: where the ground comes up to the water.
            float a = Range(0f, Mathf.PI * 2f);
            Vector2 dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
            float shore = pond.radius;
            for (float r = pond.radius * 0.4f; r < pond.radius * 1.4f; r += 0.25f)
            {
                Vector2 q = pond.center + dir * r;
                if (GetGroundHeight(q.x, q.y) > pond.level) { shore = r; break; }
            }

            int count = rng.Next(5, 13);
            for (int i = 0; i < count; i++)
            {
                Vector2 q = pond.center + dir * (shore + Range(-0.8f, 0.4f)) + new Vector2(Range(-0.6f, 0.6f), Range(-0.6f, 0.6f));
                float ground = Mathf.Min(GetGroundHeight(q.x, q.y), pond.level);
                float tall = Range(0.9f, 1.8f);
                var local = new Vector3(q.x - pond.center.x, ground - pond.level - 0.2f, q.y - pond.center.y);
                var lean = Quaternion.Euler(Range(-8f, 8f), Range(0f, 360f), Range(-8f, 8f));
                stalks.Add(new CombineInstance { mesh = stalkMesh, transform = Matrix4x4.TRS(local + lean * Vector3.up * (tall * 0.5f), lean, new Vector3(0.02f, tall * 0.5f, 0.02f)) });
                if (Range(0f, 1f) < 0.4f)
                    heads.Add(new CombineInstance { mesh = sphereMesh, transform = Matrix4x4.TRS(local + lean * Vector3.up * (tall - 0.12f), lean, new Vector3(0.05f, 0.18f, 0.05f)) });
            }
        }

        foreach (var (list, mat, name) in new[] { (stalks, reedMat, "Reeds"), (heads, headMat, "Bulrushes") })
        {
            if (list.Count == 0) continue;
            var mesh = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
            mesh.CombineMeshes(list.ToArray(), true, true);
            var go = new GameObject(name);
            go.transform.SetParent(pondObject, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        }
    }

    void PlaceWatchtower()
    {
        WatchtowerPosition = null;
        if (!spawnWatchtower) return;

        var parent = new GameObject("Watchtower").transform;
        parent.SetParent(root, false);

        float half = areaSize * 0.5f - 12f;
        for (int attempt = 0; attempt < 500; attempt++)
        {
            var p = new Vector2(Range(-half, half), Range(-half, half));
            if (p.magnitude < 50f) continue;                         // a trek from the spawn
            if (IsNearStructure(new Vector3(p.x, 0f, p.y), 40f)) continue; // not beside a shack

            var go = new GameObject("Watchtower");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(p.x, GetGroundHeight(p.x, p.y), p.y);
            go.transform.localRotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
            go.AddComponent<Watchtower>().Build(this);
            WatchtowerPosition = p;
            return;
        }
    }

    // ---------------------------------------------------------------- Grass

    void ScatterBushes()
    {
        var parent = new GameObject("Bushes").transform;
        parent.SetParent(root, false);

        var variants = new Mesh[6];
        for (int i = 0; i < variants.Length; i++) variants[i] = BuildBushMesh();
        var materials = new[]
        {
            MaterialFactory.LitTextured(ProceduralTextures.Needles, null, new Color(0.8f, 1f, 0.75f), 0.05f),
            MaterialFactory.LitTextured(ProceduralTextures.Needles, null, new Color(0.65f, 0.8f, 0.6f), 0.05f),
            MaterialFactory.LitTextured(ProceduralTextures.Needles, null, new Color(0.9f, 0.85f, 0.6f), 0.05f),
        };

        float half = areaSize * 0.5f;
        int placed = 0;
        for (int attempt = 0; attempt < bushCount * 4 && placed < bushCount; attempt++)
        {
            var p = new Vector2(Range(-half, half), Range(-half, half));
            if (IsNearStructure(new Vector3(p.x, 0f, p.y), 4f)) continue;
            if (Vector2.Distance(p, Vector2.zero) < 6f) continue; // leave the spawn point clear

            // Mostly small, now and then a bigger one.
            float size = Mathf.Lerp(minBushSize, maxBushSize, Mathf.Pow(Range(0f, 1f), 1.6f));
            var bush = new GameObject("Bush");
            bush.transform.SetParent(parent, false);
            bush.transform.localPosition = new Vector3(p.x, GetGroundHeight(p.x, p.y) - 0.08f * size, p.y);
            bush.transform.localRotation = Quaternion.Euler(Range(-4f, 4f), Range(0f, 360f), Range(-4f, 4f));
            bush.transform.localScale = new Vector3(size, size * Range(0.8f, 1.15f), size);
            bush.AddComponent<MeshFilter>().sharedMesh = variants[rng.Next(variants.Length)];
            bush.AddComponent<MeshRenderer>().sharedMaterial = materials[rng.Next(materials.Length)];
            placed++;
        }

        if (Application.isPlaying) parent.gameObject.AddComponent<BushRustle>();
    }

    /// <summary>A leafy bush about 1m across: a cluster of lumpy blobs, the middle ones highest. No collider.</summary>
    Mesh BuildBushMesh()
    {
        if (sphereMesh == null)
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Destroy(temp); else DestroyImmediate(temp);
        }
        Vector3[] sphereVerts = sphereMesh.vertices;
        Vector2[] sphereUVs = sphereMesh.uv;
        int[] sphereTris = sphereMesh.triangles;

        var combine = new List<CombineInstance>();
        int blobs = rng.Next(5, 9);
        for (int b = 0; b < blobs; b++)
        {
            float t = b / (float)blobs;
            float angle = Range(0f, Mathf.PI * 2f), reach = b == 0 ? 0f : Range(0.15f, 0.5f);
            float size = Range(0.35f, 0.55f) * (b == 0 ? 1.2f : 1f);
            var center = new Vector3(Mathf.Cos(angle) * reach, size * 0.75f + (0.5f - reach) * 0.4f, Mathf.Sin(angle) * reach);
            float seed = Range(0f, 100f);

            var verts = new Vector3[sphereVerts.Length];
            var uvs = new Vector2[sphereVerts.Length];
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 dir = sphereVerts[i].normalized;
                // Lumpy outline, the same at shared seam vertices since it depends only on direction.
                float lump = 1f + 0.35f * (Mathf.PerlinNoise(dir.x * 1.7f + seed, dir.z * 1.7f + dir.y * 1.3f) - 0.5f)
                                + 0.15f * (Mathf.PerlinNoise(dir.x * 4f + seed, dir.y * 4f - dir.z * 2f) - 0.5f);
                verts[i] = center + Vector3.Scale(dir * size * lump, new Vector3(1f, 0.8f, 1f));
                uvs[i] = new Vector2(sphereUVs[i].x * 4f, sphereUVs[i].y * 2f);
            }
            var blob = new Mesh();
            blob.vertices = verts;
            blob.uv = uvs;
            blob.triangles = sphereTris;
            blob.RecalculateNormals();
            combine.Add(new CombineInstance { mesh = blob, transform = Matrix4x4.identity });
        }

        var mesh = new Mesh { name = "Bush" };
        mesh.CombineMeshes(combine.ToArray(), true, true);
        mesh.RecalculateBounds();
        foreach (var c in combine)
        {
            if (Application.isPlaying) Destroy(c.mesh); else DestroyImmediate(c.mesh);
        }
        return mesh;
    }

    void ScatterGrass()
    {
        var parent = new GameObject("Grass").transform;
        parent.SetParent(root, false);
        GrassPivots.Clear();

        // A handful of shared clump shapes, from short and sparse to tall and thick.
        var variants = new Mesh[8];
        for (int i = 0; i < variants.Length; i++)
        {
            float t = i / (float)(variants.Length - 1);
            variants[i] = BuildGrassMesh(
                blades: Mathf.RoundToInt(Mathf.Lerp(12f, 40f, t)),
                radius: Mathf.Lerp(0.2f, 0.5f, Range(0f, 1f)),
                height: Mathf.Lerp(0.25f, 0.8f, t),
                width: Mathf.Lerp(0.015f, 0.04f, Range(0f, 1f)));
        }

        var materials = new Material[3];
        for (int i = 0; i < materials.Length; i++)
        {
            materials[i] = grassMaterial != null
                ? grassMaterial
                : MaterialFactory.Lit(grassColor * Range(0.75f, 1.25f), 0f);
        }

        float half = areaSize * 0.5f;
        for (int attempt = 0; attempt < grassPatchCount * 6 && GrassPivots.Count < grassPatchCount; attempt++)
        {
            var p = new Vector2(Range(-half, half), Range(-half, half));

            // Grass grows all over, thickest in meadows and drifts.
            float n = Mathf.PerlinNoise((p.x + noiseOffset.x) * grassNoiseScale, (p.y + noiseOffset.y) * grassNoiseScale);
            if (Range(0f, 1f) > 0.4f + n * n * 2f) continue;
            if (IsNearStructure(new Vector3(p.x, 0f, p.y), 3.5f)) continue;

            var patch = new GameObject("Grass Patch");
            patch.transform.SetParent(parent, false);
            patch.transform.localPosition = new Vector3(p.x, GetGroundHeight(p.x, p.y) - 0.02f, p.y);
            patch.transform.localRotation = Quaternion.Euler(0f, Range(0f, 360f), 0f);
            float thickness = Range(0.7f, 1.5f);
            patch.transform.localScale = new Vector3(thickness, Range(0.6f, 1.8f), thickness);

            patch.AddComponent<MeshFilter>().sharedMesh = variants[rng.Next(variants.Length)];
            var renderer = patch.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materials[rng.Next(materials.Length)];
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            GrassPivots.Add(patch.transform);
        }

        AddTreeBaseGrass(parent, variants, materials);
    }

    /// <summary>
    /// Tufts of grass round the foot of every tree, nestled against the trunk,
    /// so the trunk grows up out of the undergrowth. One merged mesh per tree; these don't sway.
    /// </summary>
    void AddTreeBaseGrass(Transform parent, Mesh[] variants, Material[] materials)
    {
        if (maxTreeBaseClumps <= 0) return;
        var combine = new List<CombineInstance>();
        foreach (var pivot in SwayPivots)
        {
            if (pivot == null || pivot.parent == null) continue;
            var trunk = pivot.parent.GetComponent<CapsuleCollider>();
            if (trunk == null) continue; // prefab trees: leave them be
            Vector3 center = pivot.parent.localPosition;
            float baseY = GetGroundHeight(center.x, center.z);

            combine.Clear();
            int clumps = rng.Next(minTreeBaseClumps, maxTreeBaseClumps + 1);
            float start = Range(0f, Mathf.PI * 2f);
            for (int i = 0; i < clumps; i++)
            {
                // Spread round the trunk, most just outside it, a few further out.
                float angle = start + (i + Range(-0.35f, 0.35f)) / clumps * Mathf.PI * 2f;
                float dist = trunk.radius + Range(0.05f, 0.5f) + (Range(0f, 1f) < 0.35f ? Range(0.4f, 1.2f) : 0f);
                float x = Mathf.Cos(angle) * dist, z = Mathf.Sin(angle) * dist;
                float y = GetGroundHeight(center.x + x, center.z + z) - baseY - 0.03f;
                float thickness = Range(0.8f, 1.6f);
                var scale = new Vector3(thickness, Range(0.7f, 1.6f), thickness);
                combine.Add(new CombineInstance
                {
                    mesh = variants[rng.Next(variants.Length)],
                    transform = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(0f, Range(0f, 360f), 0f), scale),
                });
            }

            var mesh = new Mesh { name = "Tree Base Grass" };
            mesh.CombineMeshes(combine.ToArray(), true, true);
            var go = new GameObject("Tree Base Grass");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(center.x, baseY, center.z);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = materials[rng.Next(materials.Length)];
            renderer.shadowCastingMode = ShadowCastingMode.Off;
        }
    }

    /// <summary>A clump of curved, tapering blades, visible from both sides.</summary>
    Mesh BuildGrassMesh(int blades, float radius, float height, float width)
    {
        var verts = new List<Vector3>();
        var tris = new List<int>();

        for (int b = 0; b < blades; b++)
        {
            Vector2 at = Random2(radius);
            var basePos = new Vector3(at.x, 0f, at.y);
            float h = height * Range(0.5f, 1.2f);
            float w = width * Range(0.6f, 1.4f);
            float yaw = Range(0f, Mathf.PI * 2f);
            var across = new Vector3(Mathf.Cos(yaw), 0f, Mathf.Sin(yaw)) * (w * 0.5f);
            var lean = new Vector3(-Mathf.Sin(yaw), 0f, Mathf.Cos(yaw)) * (h * Range(0.1f, 0.4f));

            int i = verts.Count;
            verts.Add(basePos - across);
            verts.Add(basePos + across);
            verts.Add(basePos - across * 0.6f + Vector3.up * (h * 0.55f) + lean * 0.3f);
            verts.Add(basePos + across * 0.6f + Vector3.up * (h * 0.55f) + lean * 0.3f);
            verts.Add(basePos + Vector3.up * h + lean);

            int[] front = { i, i + 2, i + 1, i + 1, i + 2, i + 3, i + 2, i + 4, i + 3 };
            tris.AddRange(front);
            for (int k = front.Length - 1; k >= 0; k--) tris.Add(front[k]); // back faces
        }

        var mesh = new Mesh { name = "Grass Clump" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        // Normals point up so both sides of every blade are lit like the ground beneath.
        var normals = new Vector3[verts.Count];
        for (int n = 0; n < normals.Length; n++) normals[n] = Vector3.up;
        mesh.normals = normals;
        mesh.RecalculateBounds();
        return mesh;
    }

    Vector2 Random2(float radius)
    {
        float a = Range(0f, Mathf.PI * 2f);
        float r = Mathf.Sqrt(Range(0f, 1f)) * radius;
        return new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r);
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
        BoulderSpots.Clear();

        // A handful of rock shapes shared by the small ones.
        var shapes = new Mesh[10];
        for (int i = 0; i < shapes.Length; i++) shapes[i] = BuildRockMesh(1f);

        for (int i = 0; i < rockCount; i++)
        {
            var p = new Vector2(Range(-half, half), Range(-half, half));
            if (p.magnitude < clearingRadius * 0.5f) continue;
            if (IsNearStructure(new Vector3(p.x, 0f, p.y), 5f)) continue;
            float size = Range(0.3f, 1.6f);
            // Sunk a little into the ground so it sits in the earth rather than on it.
            var pos = new Vector3(p.x, GetGroundHeight(p.x, p.y) - size * 0.12f, p.y);
            var rot = Quaternion.Euler(Range(-15f, 15f), Range(0f, 360f), Range(-15f, 15f));

            if (rockPrefabs != null && rockPrefabs.Length > 0)
            {
                var prefab = rockPrefabs[rng.Next(rockPrefabs.Length)];
                if (prefab != null) Instantiate(prefab, pos, rot, parent);
                continue;
            }

            var scale = new Vector3(size * Range(0.8f, 1.5f), size * Range(0.5f, 0.9f), size * Range(0.8f, 1.3f));
            AddRock("Rock", shapes[rng.Next(shapes.Length)], parent, pos, rot, scale);
        }

        if (rockPrefabs != null && rockPrefabs.Length > 0) return;

        // Now and then a great boulder, half buried, shouldering out of the forest floor.
        for (int i = 0, attempts = 0; i < boulderCount && attempts < boulderCount * 20; attempts++)
        {
            var p = new Vector2(Range(-half, half), Range(-half, half));
            float size = Range(minBoulderSize, maxBoulderSize);
            if (p.magnitude < clearingRadius + size) continue;
            if (IsNearStructure(new Vector3(p.x, 0f, p.y), 6f + size * 0.5f)) continue;
            if (NearAnyTree(p, size * 0.55f)) continue;

            var pos = new Vector3(p.x, GetGroundHeight(p.x, p.y) - size * Range(0.15f, 0.3f), p.y);
            var rot = Quaternion.Euler(Range(-10f, 10f), Range(0f, 360f), Range(-10f, 10f));
            var scale = new Vector3(size * Range(0.9f, 1.3f), size * Range(0.6f, 0.95f), size * Range(0.85f, 1.2f));
            AddRock("Boulder", BuildRockMesh(size), parent, pos, rot, scale); // its own mesh, so the texture keeps its scale
            BoulderSpots.Add(p);
            i++;
        }
    }

    void AddRock(string name, Mesh mesh, Transform parent, Vector3 pos, Quaternion rot, Vector3 scale)
    {
        var rock = new GameObject(name);
        rock.transform.SetParent(parent, false);
        rock.transform.localPosition = pos;
        rock.transform.localRotation = rot;
        rock.transform.localScale = scale;
        rock.AddComponent<MeshFilter>().sharedMesh = mesh;
        rock.AddComponent<MeshRenderer>().sharedMaterial = rockMat;
        rock.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    bool NearAnyTree(Vector2 p, float distance)
    {
        foreach (var tree in TreePositions)
            if ((tree - p).sqrMagnitude < distance * distance) return true;
        return false;
    }

    /// <summary>
    /// A rough, chunky rock about 1 unit across: a lumpy ball with a few flat faces sheared off it (like
    /// split stone) and a flattened base, flat-shaded so the facets catch the light. Texture mapped per face,
    /// repeating about once a metre at the given size.
    /// </summary>
    /// <summary>A rough rock mesh about 1 unit across (for campfire stones and the like).</summary>
    public Mesh MakeRockMesh(float size) => BuildRockMesh(size);
    /// <summary>The forest's granite rock material.</summary>
    public Material RockMaterial => rockMat;

    Mesh BuildRockMesh(float size)
    {
        if (sphereMesh == null)
        {
            var temp = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            sphereMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying) Destroy(temp); else DestroyImmediate(temp);
        }
        Vector3[] source = sphereMesh.vertices;
        int[] tris = sphereMesh.triangles;
        float seed = Range(0f, 100f);

        // Shear planes: chunks split off the rock.
        int cuts = rng.Next(3, 7);
        var cutNormals = new Vector3[cuts];
        var cutDist = new float[cuts];
        for (int c = 0; c < cuts; c++)
        {
            cutNormals[c] = new Vector3(Range(-1f, 1f), Range(-0.3f, 1f), Range(-1f, 1f)).normalized;
            cutDist[c] = Range(0.3f, 0.45f);
        }

        Vector3 Shape(Vector3 v)
        {
            Vector3 dir = v.normalized;
            float n = (Mathf.PerlinNoise(dir.x * 1.8f + seed, dir.y * 1.8f) + Mathf.PerlinNoise(dir.y * 1.8f + seed, dir.z * 1.8f)
                     + Mathf.PerlinNoise(dir.z * 1.8f + seed, dir.x * 1.8f)) / 3f;
            float fine = Mathf.PerlinNoise(dir.x * 6f + seed, dir.z * 6f + dir.y * 3f);
            Vector3 p = dir * 0.5f * (0.75f + n * 0.5f + fine * 0.1f);
            for (int c = 0; c < cuts; c++)
            {
                float d = Vector3.Dot(p, cutNormals[c]);
                if (d > cutDist[c]) p -= cutNormals[c] * (d - cutDist[c]);
            }
            p.y = Mathf.Max(p.y, -0.25f); // flat underneath
            return p;
        }

        // Flat-shaded: every triangle gets its own corners.
        var verts = new Vector3[tris.Length];
        var uvs = new Vector2[tris.Length];
        var newTris = new int[tris.Length];
        for (int t = 0; t < tris.Length; t += 3)
        {
            Vector3 a = Shape(source[tris[t]]), b = Shape(source[tris[t + 1]]), c = Shape(source[tris[t + 2]]);
            Vector3 normal = Vector3.Cross(b - a, c - a);
            Vector3 an = new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z));
            Vector2 Project(Vector3 p) => (an.x >= an.y && an.x >= an.z ? new Vector2(p.z, p.y) : an.y >= an.z ? new Vector2(p.x, p.z) : new Vector2(p.x, p.y)) * size;
            verts[t] = a; verts[t + 1] = b; verts[t + 2] = c;
            uvs[t] = Project(a); uvs[t + 1] = Project(b); uvs[t + 2] = Project(c);
            newTris[t] = t; newTris[t + 1] = t + 1; newTris[t + 2] = t + 2;
        }

        var mesh = new Mesh { name = "Rock" };
        mesh.vertices = verts;
        mesh.uv = uvs;
        mesh.triangles = newTris;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        return mesh;
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
            if (IsNearStructure(new Vector3(p.x, 0f, p.y), 8f)) continue;

            float radius = Range(0.2f, 0.4f);
            // Try a few lengths and directions for one that lies along the ground: the ground under it has to
            // be close to a straight line from end to end (no humps or dips for it to stick out over), and it
            // mustn't run through a tree.
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float length = Range(3f, 7f) * (1f - attempt * 0.08f);
                float yaw = Range(0f, Mathf.PI * 2f);
                var dir = new Vector2(Mathf.Cos(yaw), Mathf.Sin(yaw));
                Vector2 a = p - dir * length * 0.5f, b = p + dir * length * 0.5f;
                float ha = GetGroundHeight(a.x, a.y), hb = GetGroundHeight(b.x, b.y);

                bool fits = true;
                for (int k = 1; k < 8 && fits; k++)
                {
                    float t = k / 8f;
                    Vector2 q = Vector2.Lerp(a, b, t);
                    if (Mathf.Abs(GetGroundHeight(q.x, q.y) - Mathf.Lerp(ha, hb, t)) > 0.1f) fits = false;
                    else if (NearAnyTree(q, radius + 0.45f)) fits = false;
                }
                if (!fits) continue;

                // Lying along the slope, settled a little into the soil.
                var axis = new Vector3(b.x - a.x, hb - ha, b.y - a.y).normalized;
                Vector3 side = Vector3.Cross(Vector3.up, axis).normalized;
                Vector3 up = Vector3.Cross(axis, side);
                var log = CreatePrimitive(PrimitiveType.Cylinder, "Log", parent, deadBarkMat);
                log.transform.localPosition = new Vector3(p.x, (ha + hb) * 0.5f, p.y) + up * radius * 0.65f;
                log.transform.localRotation = Quaternion.AngleAxis(Range(0f, 360f), axis) * Quaternion.FromToRotation(Vector3.up, axis);
                log.transform.localScale = new Vector3(radius * 2f, length * 0.5f, radius * 2f);
                break;
            }
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

        // Find the scene's own directional lights.
        // (Resources.FindObjectsOfTypeAll also returns assets, so only take lights that are in a scene.)
        Light sceneLight = null;
        foreach (var light in Resources.FindObjectsOfTypeAll<Light>())
        {
            if (!light.gameObject.scene.IsValid() || light.type != LightType.Directional || light.transform.IsChildOf(transform)) continue;
            if (useSceneLight && sceneLight == null && light.isActiveAndEnabled) sceneLight = light;
            else light.enabled = false;
        }

        // Keep the scene's light exactly as it was set up in the Inspector.
        if (sceneLight != null)
        {
            RenderSettings.sun = sceneLight;
            foreach (var cam in Camera.allCameras)
                ConfigureCamera(cam);
            return;
        }

        // Otherwise make a dim moonlight.
        var moon = new GameObject("Moonlight").AddComponent<Light>();
        moon.transform.SetParent(root, false);
        moon.type = LightType.Directional;
        moon.color = moonColor;
        moon.intensity = moonIntensity;
        moon.shadows = LightShadows.Soft;
        moon.transform.rotation = Quaternion.Euler(35f, Range(0f, 360f), 0f);
        RenderSettings.sun = moon;

        foreach (var cam in Camera.allCameras)
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
        cam.fieldOfView = 60f; // 70 stretched the edges of the view badly looking down slopes (the pause menu can change it)
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
        groundMat = groundMaterial != null
            ? groundMaterial
            : MaterialFactory.LitTextured(ProceduralTextures.Dirt, ProceduralTextures.DirtNormal, groundTint, 0.08f);
        barkMat = barkMaterial != null ? barkMaterial : MaterialFactory.Lit(new Color(0.11f, 0.08f, 0.06f));
        deadBarkMat = barkMaterial != null ? barkMaterial : MaterialFactory.Lit(new Color(0.16f, 0.15f, 0.14f));
        if (barkMaterial == null)
        {
            barkMat = MaterialFactory.LitTextured(ProceduralTextures.Bark, ProceduralTextures.BarkNormal, new Color(0.95f, 0.85f, 0.78f), 0.05f);
            deadBarkMat = MaterialFactory.LitTextured(ProceduralTextures.Bark, ProceduralTextures.BarkNormal, new Color(1f, 1f, 1f), 0.08f);
        }
        // A few shades of needles, so neighbouring pines don't all match.
        foliageMats = foliageMaterial != null
            ? new[] { foliageMaterial }
            : new[]
            {
                MaterialFactory.LitTextured(ProceduralTextures.Needles, null, Color.white, 0.05f),
                MaterialFactory.LitTextured(ProceduralTextures.Needles, null, new Color(0.75f, 0.85f, 0.8f), 0.05f),
                MaterialFactory.LitTextured(ProceduralTextures.Needles, null, new Color(1f, 0.95f, 0.75f), 0.05f),
            };
        rockMat = rockMaterial != null
            ? rockMaterial
            : MaterialFactory.LitTextured(ProceduralTextures.Rock, ProceduralTextures.RockNormal, new Color(0.8f, 0.8f, 0.82f), 0.12f);
    }
}
