using System.Collections.Generic;
using System.IO;
using Game.Core;
using Game.Player;
using Game.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

namespace Game.Editor
{
    /// <summary>
    /// Game > Build Look Test Scene: makes a small patch of night forest for matching the look (milestone 1).
    /// A path leads from where you stand, past a scour stand-in, to a clearing with a lamp post. Everything is
    /// generated, so the scene and its assets live in Assets/Scenes/Generated (not in git): build it again any
    /// time. The look itself is in Assets/Config/Look/LookConfig.
    /// </summary>
    public static class LookTestSceneBuilder
    {
        const string Folder = "Assets/Scenes/Generated/LookTest";
        const string ScenePath = Folder + "/LookTest.unity";
        const string AssetsPath = Folder + "/LookTestAssets.asset";
        const float HalfSize = 70f;     // the ground is 140 m across
        const float CellSize = 16f;     // scenery is merged into meshes this big
        const int Seed = 1234;

        static readonly Vector2 Clearing = new Vector2(0f, 8f);
        const float ClearingRadius = 11f;
        static readonly Vector2[] PathPoints = { new Vector2(0f, -45f), new Vector2(1.5f, -28f), new Vector2(-1f, -14f), new Vector2(0.5f, -2f), Clearing };

        [MenuItem("Game/Build Look Test Scene")]
        public static void Build()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            try
            {
                EditorUtility.DisplayProgressBar("Look test scene", "Building...", 0.1f);
                BuildScene();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static void BuildScene()
        {
            Directory.CreateDirectory(Folder);
            AssetDatabase.Refresh();
            AssetDatabase.DeleteAsset(AssetsPath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var rng = new System.Random(Seed);
            var assets = new List<Object>();

            // Materials
            var groundTex = GroundTexture(rng);
            assets.Add(groundTex);
            var mats = new Dictionary<string, Material>
            {
                ["ground"] = EditorMaterials.Lit("Ground", Color.white, 0.1f, false, groundTex, new Vector2(HalfSize * 2f / 3f, HalfSize * 2f / 3f)),
                ["path"] = EditorMaterials.Lit("Path", new Color(0.17f, 0.13f, 0.1f), 0.12f),
                ["bark"] = EditorMaterials.Lit("Bark", new Color(0.1f, 0.08f, 0.07f), 0.2f),
                ["needles"] = EditorMaterials.Lit("Needles", new Color(0.05f, 0.07f, 0.05f), 0.1f, true),
                ["deadwood"] = EditorMaterials.Lit("Dead wood", new Color(0.14f, 0.12f, 0.1f), 0.15f),
                ["rock"] = EditorMaterials.Lit("Rock", new Color(0.18f, 0.18f, 0.17f), 0.25f),
                ["drygrass"] = EditorMaterials.Lit("Dry grass", new Color(0.24f, 0.2f, 0.12f), 0.1f, true),
                ["grass"] = EditorMaterials.Lit("Grass", new Color(0.09f, 0.11f, 0.06f), 0.1f, true),
                ["twigs"] = EditorMaterials.Lit("Twigs", new Color(0.12f, 0.09f, 0.07f), 0.1f, true),
                ["metal"] = EditorMaterials.Lit("Lamp metal", new Color(0.06f, 0.06f, 0.06f), 0.5f),
                ["glass"] = EditorMaterials.LitGlow("Lamp glass", new Color(1f, 0.85f, 0.6f), new Color(1f, 0.7f, 0.4f) * 6f),
                ["scour"] = EditorMaterials.Unlit("Scour body", Color.black),
                ["eyes"] = EditorMaterials.Glow("Scour eyes", new Color(1f, 0.005f, 0f) * 0.6f),
            };
            assets.AddRange(mats.Values);

            EditorUtility.DisplayProgressBar("Look test scene", "Ground...", 0.2f);
            var groundMesh = GroundMesh();
            assets.Add(groundMesh);
            var ground = MakeObject("Ground", null, groundMesh, mats["ground"], Layers.Ground);
            ground.AddComponent<MeshCollider>().sharedMesh = groundMesh;

            var pathMesh = PathMesh();
            assets.Add(pathMesh);
            MakeObject("Path", null, pathMesh, mats["path"], Layers.Ground);

            EditorUtility.DisplayProgressBar("Look test scene", "Trees...", 0.4f);
            var cells = new Dictionary<(int, int, string), MeshBuilder>();
            var treeRoot = new GameObject("Tree Colliders").transform;
            PlaceTrees(rng, cells, treeRoot);

            EditorUtility.DisplayProgressBar("Look test scene", "Undergrowth...", 0.6f);
            PlaceGroundCover(rng, cells);

            EditorUtility.DisplayProgressBar("Look test scene", "Merging...", 0.75f);
            var sceneryRoot = new GameObject("Scenery").transform;
            foreach (var cell in cells)
            {
                var mesh = cell.Value.ToMesh($"{cell.Key.Item3} {cell.Key.Item1},{cell.Key.Item2}");
                assets.Add(mesh);
                int layer = cell.Key.Item3 == "rock" ? Layers.Props : Layers.Default;
                MakeObject(mesh.name, sceneryRoot, mesh, mats[cell.Key.Item3], layer);
            }

            BuildLamp(mats);
            BuildScour(mats);
            BuildCamera();

            EditorUtility.DisplayProgressBar("Look test scene", "Saving...", 0.9f);
            SaveAssets(assets);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"Look test scene built: {ScenePath}. Press Play, then F9 > Lighting / Look to tune.");
        }

        // ------------------------------------------------------------------ Ground and path

        static float GroundHeight(float x, float z) =>
            0.6f * Mathf.PerlinNoise(x * 0.035f + 11f, z * 0.035f + 7f) + 0.15f * Mathf.PerlinNoise(x * 0.18f + 3f, z * 0.18f + 5f);

        static Mesh GroundMesh()
        {
            var b = new MeshBuilder();
            int n = Mathf.RoundToInt(HalfSize * 2f);
            for (int j = 0; j <= n; j++)
            for (int i = 0; i <= n; i++)
            {
                float x = -HalfSize + i, z = -HalfSize + j;
                b.AddVertex(new Vector3(x, GroundHeight(x, z), z), new Vector2(i / (float)n, j / (float)n));
            }
            for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
            {
                int a = j * (n + 1) + i;
                b.AddTriangle(a, a + n + 1, a + 1);
                b.AddTriangle(a + 1, a + n + 1, a + n + 2);
            }
            return b.ToMesh("Ground");
        }

        // A ribbon of packed earth along the path, just above the ground.
        static Mesh PathMesh()
        {
            var b = new MeshBuilder();
            var points = SamplePath(0.5f);
            for (int i = 0; i < points.Count; i++)
            {
                Vector2 p = points[i];
                Vector2 dir = (points[Mathf.Min(i + 1, points.Count - 1)] - points[Mathf.Max(i - 1, 0)]).normalized;
                Vector2 side = new Vector2(-dir.y, dir.x) * (0.8f + 0.25f * Mathf.PerlinNoise(i * 0.15f, 3f));
                Vector2 l = p - side, r = p + side;
                b.AddVertex(new Vector3(l.x, GroundHeight(l.x, l.y) + 0.02f, l.y), new Vector2(0f, i * 0.5f));
                b.AddVertex(new Vector3(r.x, GroundHeight(r.x, r.y) + 0.02f, r.y), new Vector2(1f, i * 0.5f));
            }
            for (int i = 0; i < points.Count - 1; i++)
            {
                int a = i * 2;
                b.AddTriangle(a, a + 1, a + 2);
                b.AddTriangle(a + 1, a + 3, a + 2);
            }
            return b.ToMesh("Path");
        }

        static List<Vector2> SamplePath(float step)
        {
            var result = new List<Vector2>();
            for (int s = 0; s < PathPoints.Length - 1; s++)
            {
                Vector2 a = PathPoints[s], c = PathPoints[s + 1];
                int count = Mathf.CeilToInt(Vector2.Distance(a, c) / step);
                for (int k = 0; k < count; k++) result.Add(Vector2.Lerp(a, c, k / (float)count));
            }
            result.Add(PathPoints[PathPoints.Length - 1]);
            return result;
        }

        static float DistanceToPath(Vector2 p)
        {
            float best = float.MaxValue;
            for (int s = 0; s < PathPoints.Length - 1; s++)
            {
                Vector2 a = PathPoints[s], ab = PathPoints[s + 1] - a;
                float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
                best = Mathf.Min(best, Vector2.Distance(p, a + ab * t));
            }
            return best;
        }

        // Dark earth with leaf litter: specks and blotches of brown, ochre and near-black.
        static Texture2D GroundTexture(System.Random rng)
        {
            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, true) { name = "Ground Litter", wrapMode = TextureWrapMode.Repeat };
            var px = new Color[size * size];
            Color soil = new Color(0.11f, 0.085f, 0.065f), dark = new Color(0.05f, 0.04f, 0.035f), leaf = new Color(0.26f, 0.17f, 0.09f);
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)size, v = y / (float)size;
                float n = Tileable(u, v, 4, 2f) * 0.6f + Tileable(u, v, 16, 9f) * 0.4f;
                px[y * size + x] = Color.Lerp(dark, soil, Mathf.SmoothStep(0.2f, 0.8f, n));
            }
            for (int i = 0; i < 900; i++) // leaves
            {
                int cx = rng.Next(size), cy = rng.Next(size);
                float r = 1.5f + (float)rng.NextDouble() * 3f;
                Color c = Color.Lerp(leaf, soil, (float)rng.NextDouble() * 0.7f);
                for (int dy = -4; dy <= 4; dy++)
                for (int dx = -4; dx <= 4; dx++)
                {
                    if (dx * dx + dy * dy * 2f > r * r) continue;
                    int xx = (cx + dx + size) % size, yy = (cy + dy + size) % size;
                    px[yy * size + xx] = Color.Lerp(px[yy * size + xx], c, 0.8f);
                }
            }
            tex.SetPixels(px);
            tex.Apply(true);
            return tex;
        }

        static float Tileable(float u, float v, int period, float seed)
        {
            // Blend four offset samples so the noise wraps at the texture edge.
            float s = period;
            float a = Mathf.PerlinNoise(u * s + seed, v * s + seed);
            float b = Mathf.PerlinNoise((u - 1f) * s + seed, v * s + seed);
            float c = Mathf.PerlinNoise(u * s + seed, (v - 1f) * s + seed);
            float d = Mathf.PerlinNoise((u - 1f) * s + seed, (v - 1f) * s + seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        // ------------------------------------------------------------------ Trees and undergrowth

        static void PlaceTrees(System.Random rng, Dictionary<(int, int, string), MeshBuilder> cells, Transform colliderRoot)
        {
            var pines = new List<(MeshBuilder trunk, MeshBuilder needles)>();
            var dead = new List<MeshBuilder>();
            for (int i = 0; i < 8; i++) pines.Add(LookTestMeshes.Pine(rng));
            for (int i = 0; i < 5; i++) dead.Add(LookTestMeshes.DeadTree(rng));

            var placed = new List<Vector2>();
            const float minSpacing = 2.2f;
            for (int attempt = 0; attempt < 6000 && placed.Count < 420; attempt++)
            {
                var p = new Vector2(Range(rng, -HalfSize + 2f, HalfSize - 2f), Range(rng, -HalfSize + 2f, HalfSize - 2f));
                if (DistanceToPath(p) < 2.2f) continue;
                // The clearing: empty in the middle, trees thinning back in over its ragged edge.
                float edge = ClearingRadius * (0.9f + 0.25f * Mathf.PerlinNoise(p.x * 0.2f, p.y * 0.2f));
                float fromClearing = Vector2.Distance(p, Clearing);
                if (fromClearing < edge || (fromClearing < edge * 1.4f && rng.NextDouble() < 0.6)) continue;
                if (Mathf.PerlinNoise(p.x * 0.05f + 50f, p.y * 0.05f + 50f) < 0.3f && rng.NextDouble() < 0.5) continue; // patchy
                bool clash = false;
                foreach (var q in placed)
                    if ((q - p).sqrMagnitude < minSpacing * minSpacing) { clash = true; break; }
                if (clash) continue;
                placed.Add(p);

                float y = GroundHeight(p.x, p.y);
                float scale = Range(rng, 0.8f, 1.2f) * 0.85f;
                var m = Matrix4x4.TRS(new Vector3(p.x, y, p.y), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), Vector3.one * scale);
                float trunkRadius;
                if (rng.NextDouble() < 0.3)
                {
                    Cell(cells, p, "deadwood").Append(dead[rng.Next(dead.Count)], m);
                    trunkRadius = 0.25f * scale;
                }
                else
                {
                    var pine = pines[rng.Next(pines.Count)];
                    Cell(cells, p, "bark").Append(pine.trunk, m);
                    Cell(cells, p, "needles").Append(pine.needles, m);
                    trunkRadius = 0.3f * scale;
                }

                // A trunk collider, so the flashlight's bounce and the mist find the trees.
                var col = new GameObject("Tree") { layer = Layers.Trees };
                col.transform.SetParent(colliderRoot, false);
                col.transform.position = new Vector3(p.x, y, p.y);
                var capsule = col.AddComponent<CapsuleCollider>();
                capsule.radius = trunkRadius;
                capsule.height = 6f;
                capsule.center = Vector3.up * 3f;
            }
        }

        static void PlaceGroundCover(System.Random rng, Dictionary<(int, int, string), MeshBuilder> cells)
        {
            for (int i = 0; i < 2600; i++)
            {
                var p = new Vector2(Range(rng, -HalfSize, HalfSize), Range(rng, -HalfSize, HalfSize));
                float meadow = Mathf.PerlinNoise(p.x * 0.06f + 20f, p.y * 0.06f + 30f);
                float fromPath = DistanceToPath(p);
                if (fromPath < 0.7f) continue;                          // the path's trodden middle stays bare
                bool verge = fromPath < 2.5f;                          // thicker along the path's edges
                if (!verge && meadow < 0.45f && rng.NextDouble() < 0.7) continue;
                float h = verge ? Range(rng, 0.35f, 0.8f) : Range(rng, 0.25f, 0.9f);
                var clump = LookTestMeshes.GrassClump(rng, h);
                var m = Matrix4x4.TRS(new Vector3(p.x, GroundHeight(p.x, p.y), p.y), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), Vector3.one);
                Cell(cells, p, rng.NextDouble() < 0.65 ? "drygrass" : "grass").Append(clump, m);
            }

            for (int i = 0; i < 450; i++)
            {
                var p = new Vector2(Range(rng, -HalfSize, HalfSize), Range(rng, -HalfSize, HalfSize));
                if (DistanceToPath(p) < 0.9f) continue;
                var m = Matrix4x4.TRS(new Vector3(p.x, GroundHeight(p.x, p.y), p.y), Quaternion.Euler(0f, Range(rng, 0f, 360f), 0f), Vector3.one * Range(rng, 0.7f, 1.6f));
                Cell(cells, p, "twigs").Append(LookTestMeshes.Twigs(rng), m);
            }

            var rocks = new List<MeshBuilder>();
            for (int i = 0; i < 6; i++) rocks.Add(LookTestMeshes.Rock(rng));
            for (int i = 0; i < 70; i++)
            {
                var p = new Vector2(Range(rng, -HalfSize, HalfSize), Range(rng, -HalfSize, HalfSize));
                bool onPath = DistanceToPath(p) < 1f;
                float size = onPath ? Range(rng, 0.15f, 0.35f) : Range(rng, 0.3f, 1.4f);
                var m = Matrix4x4.TRS(new Vector3(p.x, GroundHeight(p.x, p.y), p.y), Quaternion.Euler(Range(rng, -10f, 10f), Range(rng, 0f, 360f), 0f), Vector3.one * size);
                Cell(cells, p, "rock").Append(rocks[rng.Next(rocks.Count)], m);
            }
        }

        static MeshBuilder Cell(Dictionary<(int, int, string), MeshBuilder> cells, Vector2 p, string material)
        {
            var key = (Mathf.FloorToInt(p.x / CellSize), Mathf.FloorToInt(p.y / CellSize), material);
            if (!cells.TryGetValue(key, out var b)) cells[key] = b = new MeshBuilder();
            return b;
        }

        // ------------------------------------------------------------------ Lamp, scour, camera

        static void BuildLamp(Dictionary<string, Material> mats)
        {
            Vector2 at = Clearing + new Vector2(2f, 2f);
            float y = GroundHeight(at.x, at.y);
            var root = new GameObject("Lamp Post") { layer = Layers.Props }.transform;
            root.position = new Vector3(at.x, y, at.y);

            Primitive(PrimitiveType.Cylinder, "Post", root, new Vector3(0f, 1.6f, 0f), new Vector3(0.12f, 1.6f, 0.12f), mats["metal"], Layers.Props, keepCollider: true);
            Primitive(PrimitiveType.Cube, "Head", root, new Vector3(0f, 3.35f, 0f), new Vector3(0.45f, 0.12f, 0.45f), mats["metal"], Layers.Props);
            Primitive(PrimitiveType.Cube, "Glass", root, new Vector3(0f, 3.12f, 0f), new Vector3(0.32f, 0.34f, 0.32f), mats["glass"], Layers.Props);

            var lightGo = new GameObject("Lamp Light");
            lightGo.transform.SetParent(root, false);
            lightGo.transform.localPosition = new Vector3(0f, 3.0f, 0f);
            lightGo.AddHDLight(LightType.Point);
            var light = lightGo.GetComponent<Light>();
            light.color = new Color(1f, 0.75f, 0.45f);
            light.intensity = 25f; // candela
            light.range = 13f;
            light.shadows = LightShadows.Soft;
            lightGo.AddComponent<FogGlowLight>();
        }

        // A stand-in scour on the path ahead: a too-thin black figure with burning red eyes, 2.4 m tall.
        static void BuildScour(Dictionary<string, Material> mats)
        {
            Vector2 at = new Vector2(0.4f, -18f);
            var root = new GameObject("Scour Stand-in") { layer = Layers.Entities }.transform;
            root.position = new Vector3(at.x, GroundHeight(at.x, at.y), at.y);
            root.rotation = Quaternion.LookRotation(Vector3.back);
            var body = mats["scour"];

            Primitive(PrimitiveType.Capsule, "Body", root, new Vector3(0f, 1.45f, 0f), new Vector3(0.22f, 0.45f, 0.14f), body, Layers.Entities);
            Primitive(PrimitiveType.Capsule, "Neck", root, new Vector3(0f, 2.0f, 0.06f), new Vector3(0.06f, 0.14f, 0.06f), body, Layers.Entities);
            Primitive(PrimitiveType.Capsule, "Head", root, new Vector3(0f, 2.24f, 0.1f), new Vector3(0.17f, 0.15f, 0.17f), body, Layers.Entities);
            foreach (float side in new[] { -1f, 1f })
            {
                Primitive(PrimitiveType.Capsule, "Upper Arm", root, new Vector3(0.2f * side, 1.55f, 0f), new Vector3(0.05f, 0.32f, 0.05f), body, Layers.Entities);
                Primitive(PrimitiveType.Capsule, "Forearm", root, new Vector3(0.24f * side, 0.95f, 0.04f), new Vector3(0.045f, 0.32f, 0.045f), body, Layers.Entities);
                Primitive(PrimitiveType.Capsule, "Hand", root, new Vector3(0.26f * side, 0.55f, 0.08f), new Vector3(0.04f, 0.12f, 0.04f), body, Layers.Entities);
                Primitive(PrimitiveType.Capsule, "Thigh", root, new Vector3(0.09f * side, 0.85f, 0f), new Vector3(0.06f, 0.3f, 0.06f), body, Layers.Entities);
                Primitive(PrimitiveType.Capsule, "Shin", root, new Vector3(0.09f * side, 0.3f, 0.02f), new Vector3(0.045f, 0.3f, 0.045f), body, Layers.Entities);
                Primitive(PrimitiveType.Sphere, "Eye", root, new Vector3(0.055f * side, 2.27f, 0.25f), Vector3.one * 0.045f, mats["eyes"], Layers.Entities);
            }

            var glow = new GameObject("Eye Glow");
            glow.transform.SetParent(root, false);
            glow.transform.localPosition = new Vector3(0f, 2.27f, 0.4f);
            glow.AddHDLight(LightType.Point);
            var light = glow.GetComponent<Light>();
            light.color = new Color(1f, 0.12f, 0.05f);
            light.intensity = 1.5f; // candela
            light.range = 5f;
            light.shadows = LightShadows.None;
            glow.AddComponent<FogGlowLight>().strength = 3f; // (a red haze round the head in the mist)
        }

        static void BuildCamera()
        {
            Vector2 at = PathPoints[0] + new Vector2(0f, 12f);
            var go = new GameObject("Main Camera") { tag = "MainCamera" };
            go.transform.position = new Vector3(at.x, GroundHeight(at.x, at.y) + 1.65f, at.y);
            go.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            var cam = go.AddComponent<Camera>();
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 600f;
            go.AddComponent<HDAdditionalCameraData>();
            go.AddComponent<AudioListener>();
            go.AddComponent<LookWalker>();
            go.AddComponent<Flashlight>();
        }

        // ------------------------------------------------------------------ Helpers

        static GameObject MakeObject(string name, Transform parent, Mesh mesh, Material material, int layer)
        {
            var go = new GameObject(name) { layer = layer };
            if (parent != null) go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
            return go;
        }

        static GameObject Primitive(PrimitiveType type, string name, Transform parent, Vector3 position, Vector3 scale, Material material, int layer, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = layer;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = material;
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        // Everything generated goes in one asset file beside the scene.
        static void SaveAssets(List<Object> assets)
        {
            var main = ScriptableObject.CreateInstance<LookTestAssets>();
            main.name = "LookTestAssets";
            AssetDatabase.CreateAsset(main, AssetsPath);
            foreach (var a in assets) AssetDatabase.AddObjectToAsset(a, main);
        }

        static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
    }
}
