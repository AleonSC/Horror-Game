using Game.Core;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;
using V2 = UnityEngine.Vector2;

namespace Game.World.Build
{
    /// <summary>
    /// Makes a lamp post to crank (built in, until there's a prefab): a Victorian street lamp in turned cast iron and
    /// brass on a stone plinth, a fluted column, scrollwork under a six-sided glass lantern with an ogee roof and
    /// a crown of spikes, a blinking red marker on its tip, a crank to click, and its warm light.
    /// </summary>
    public sealed class LampFactory
    {
        readonly Material metal, glass, marker, crank, brass, stone;
        static Mesh shapeStone, shapeIron, shapeBrass, shapeGlass, shapeLantern;
        readonly GameObject prefab;

        /// <param name="prefab">Your own lamp post (WorldArtConfig), or null for the built-in one.</param>
        public LampFactory(GameObject prefab = null)
        {
            this.prefab = prefab;
            metal = RuntimeMaterials.Lit("Lamp metal", new Color(0.035f, 0.045f, 0.04f), 0.55f); // (old green-black paint)
            glass = RuntimeMaterials.LitGlow("Lamp glass", new Color(1f, 0.85f, 0.6f), new Color(1f, 0.7f, 0.4f) * 1.5f);
            marker = RuntimeMaterials.Glow("Lamp marker", new Color(1f, 0.05f, 0.02f));
            crank = RuntimeMaterials.Lit("Crank", new Color(0.12f, 0.1f, 0.08f), 0.4f);
            brass = RuntimeMaterials.Lit("Lamp brass", new Color(0.4f, 0.29f, 0.12f), 0.6f);
            stone = RuntimeMaterials.Lit("Lamp base", new Color(0.17f, 0.165f, 0.155f), 0.15f);
            foreach (var m in new[] { metal, brass, crank }) m.SetFloat("_Metallic", m == brass ? 0.9f : 0.75f);
        }

        public Material[] Materials => new[] { metal, glass, marker, crank, brass, stone };

        public Lamp Create(int index, Vector3 position, float yaw, Transform parent)
        {
            if (prefab != null) return FromPrefab(index, position, yaw, parent);
            var root = new GameObject($"Lamp {index}") { layer = Layers.Props };
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            // A Victorian street lamp, in turned cast iron and brass on a stone plinth (the shapes are built once
            // and shared): see Shapes.
            var t = root.transform;
            Shapes();
            Shape(t, "Plinth", shapeStone, stone);
            Shape(t, "Iron", shapeIron, metal);
            Shape(t, "Brass", shapeBrass, brass).GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var glassPart = Shape(t, "Glass", shapeGlass, glass);
            glassPart.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Shape(t, "Lantern", shapeLantern, metal).GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var markerPart = Part(PrimitiveType.Sphere, "Marker", t, new Vector3(0f, 4.1f, 0f), Vector3.one * 0.07f, marker, false);
            // Something solid to bump into and for the crosshair: the plinth and the post.
            var plinth = root.AddComponent<BoxCollider>();
            plinth.center = new Vector3(0f, 0.15f, 0f);
            plinth.size = new Vector3(0.72f, 0.3f, 0.72f);
            var post = root.AddComponent<CapsuleCollider>();
            post.radius = 0.11f;
            post.height = 3.7f;
            post.center = new Vector3(0f, 2.1f, 0f);
            // The crank: a brass-rimmed housing on the post, the axle out of it, an arm and a knob.
            Part(PrimitiveType.Cube, "Crank plate", t, new Vector3(0.082f, 1.2f, 0f), new Vector3(0.02f, 0.24f, 0.17f), brass, false);
            Part(PrimitiveType.Cube, "Crank housing", t, new Vector3(0.11f, 1.2f, 0f), new Vector3(0.05f, 0.2f, 0.14f), metal, false);
            // (The crank turns about its axle, local X, as the meter fills: the axle, arm and knob all go round.)
            var crankPivot = new GameObject("Crank") { layer = Layers.Props }.transform;
            crankPivot.SetParent(t, false);
            crankPivot.localPosition = new Vector3(0.17f, 1.2f, 0f);
            var crankPart = Part(PrimitiveType.Cylinder, "Crank axle", crankPivot, Vector3.zero, new Vector3(0.05f, 0.04f, 0.05f), crank, true);
            crankPart.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Part(PrimitiveType.Cube, "Crank arm", crankPivot, new Vector3(0.045f, 0.07f, 0f), new Vector3(0.022f, 0.16f, 0.03f), crank, false);
            Part(PrimitiveType.Cylinder, "Crank knob", crankPivot, new Vector3(0.08f, 0.14f, 0f), new Vector3(0.035f, 0.04f, 0.035f), brass, false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            var lightGo = new GameObject("Lamp Light");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 3.24f, 0f);
            lightGo.AddHDLight(LightType.Point);
            var light = lightGo.GetComponent<Light>();
            light.shadows = LightShadows.Soft;
            light.enabled = false;
            lightGo.AddComponent<FogGlowLight>();

            var lamp = root.AddComponent<Lamp>();
            lamp.index = index;
            lamp.lampLight = light;
            lamp.marker = markerPart.GetComponent<Renderer>();
            lamp.glass = glassPart.GetComponent<Renderer>();
            lamp.crank = crankPivot;
            return lamp;
        }

        // Your lamp: its own Lamp component if it has one, otherwise one wired up from parts found by name.
        Lamp FromPrefab(int index, Vector3 position, float yaw, Transform parent)
        {
            var go = Object.Instantiate(prefab, position, Quaternion.Euler(0f, yaw, 0f), parent);
            go.name = $"Lamp {index}";
            var lamp = go.GetComponent<Lamp>();
            if (lamp == null)
            {
                lamp = go.AddComponent<Lamp>();
                lamp.lampLight = go.GetComponentInChildren<Light>(true);
                lamp.marker = Find<Renderer>(go, "marker");
                lamp.glass = Find<Renderer>(go, "glass");
                var crankPart = Find<Transform>(go, "crank");
                lamp.crank = crankPart;
            }
            lamp.index = index;
            if (lamp.lampLight != null)
            {
                if (lamp.lampLight.GetComponent<HDAdditionalLightData>() == null) lamp.lampLight.gameObject.AddComponent<HDAdditionalLightData>();
                if (lamp.lampLight.GetComponent<FogGlowLight>() == null) lamp.lampLight.gameObject.AddComponent<FogGlowLight>();
                lamp.lampLight.enabled = false;
            }
            else Debug.LogWarning("WorldArtConfig: the lamp prefab has no Light; lit lamps won't shine.");
            if (go.GetComponentInChildren<Collider>() == null)
            {
                var c = go.AddComponent<CapsuleCollider>();
                c.radius = 0.2f;
                c.height = 3f;
                c.center = Vector3.up * 1.5f; // (something for the crosshair to find)
            }
            return lamp;
        }

        GameObject Shape(Transform parent, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name) { layer = Layers.Props };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            return go;
        }

        // The lamp's shapes, one mesh per material, made the first time a lamp is.
        static void Shapes()
        {
            if (shapeIron != null) return;
            var stone = new MeshBuilder();
            var iron = new MeshBuilder();
            var brass = new MeshBuilder();
            var glass = new MeshBuilder();
            var lantern = new MeshBuilder(); // (everything round and under the light: kept out of the shadows so it doesn't shade its own light)
            Vector3 o = Vector3.zero;
            V2[] P(params float[] rh) { var a = new V2[rh.Length / 2]; for (int i = 0; i < a.Length; i++) a[i] = new V2(rh[i * 2], rh[i * 2 + 1]); return a; }

            // The plinth: an eight-sided stone block, stepped and chamfered.
            stone.AddLathe(o, P(0f, 0f, 0.36f, 0f, 0.36f, 0.12f, 0.31f, 0.16f, 0.31f, 0.25f, 0.25f, 0.3f, 0f, 0.3f), 8, true, Mathf.PI / 8f);

            // The base: a bell of cast iron, ring on ring of mouldings, narrowing up into the column.
            iron.AddLathe(o, P(0.21f, 0.3f, 0.21f, 0.35f, 0.18f, 0.38f, 0.18f, 0.42f, 0.2f, 0.45f, 0.2f, 0.47f, 0.16f, 0.5f,
                0.13f, 0.58f, 0.11f, 0.72f, 0.105f, 0.8f, 0.12f, 0.83f, 0.12f, 0.86f, 0.095f, 0.89f, 0.085f, 0.95f, 0.095f, 0.98f, 0.075f, 1.01f), 24, false);
            // The column, tapering a touch, with eight flutes standing proud of it.
            iron.AddLathe(o, P(0.075f, 1.01f, 0.064f, 2.55f), 20, false);
            for (int i = 0; i < 8; i++)
            {
                float a = i / 8f * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                iron.AddTube(d * 0.07f + Vector3.up * 1.08f, d * 0.06f + Vector3.up * 2.47f, 0.011f, 0.009f, 5, true);
            }
            // Brass bands round it.
            foreach (float y in new[] { 1.04f, 1.62f, 2.5f })
                brass.AddLathe(o, P(0.07f, y - 0.022f, 0.086f, y - 0.012f, 0.088f, y + 0.012f, 0.07f, y + 0.022f), 20, false);
            // The capital: a flare, a neck and a wide collar for the lantern to sit on.
            lantern.AddLathe(o, P(0.064f, 2.55f, 0.085f, 2.58f, 0.085f, 2.61f, 0.07f, 2.64f, 0.075f, 2.67f, 0.12f, 2.72f, 0.13f, 2.75f,
                0.1f, 2.78f, 0.088f, 2.88f, 0.11f, 2.92f, 0.13f, 2.94f, 0f, 2.94f), 20, false);
            // The ladder bar, with brass balls on its ends.
            lantern.AddTube(new Vector3(-0.34f, 2.75f, 0f), new Vector3(0.34f, 2.75f, 0f), 0.016f, 0.016f, 8);
            for (int side = -1; side <= 1; side += 2)
                brass.AddLathe(new Vector3(side * 0.35f, 2.75f, 0f), P(0f, -0.03f, 0.022f, -0.022f, 0.03f, 0f, 0.022f, 0.022f, 0f, 0.03f), 10, false);
            // Scrollwork: four curling iron brackets from the capital out under the lantern's floor.
            for (int i = 0; i < 4; i++)
            {
                float a = (i / 4f + 0.125f) * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                Vector3 S(float r, float y) => d * r + Vector3.up * y;
                var curl = Lofts.Tube(new[]
                {
                    (S(0.08f, 2.6f), new Vector2(0.012f, 0.01f)), (S(0.15f, 2.63f), new Vector2(0.012f, 0.01f)), (S(0.21f, 2.7f), new Vector2(0.011f, 0.01f)),
                    (S(0.23f, 2.8f), new Vector2(0.01f, 0.009f)), (S(0.21f, 2.88f), new Vector2(0.01f, 0.009f)), (S(0.17f, 2.9f), new Vector2(0.009f, 0.008f)),
                    (S(0.15f, 2.86f), new Vector2(0.008f, 0.007f)), (S(0.17f, 2.83f), new Vector2(0.007f, 0.006f)),
                }, 2f, true, true);
                lantern.AppendMesh(curl, Matrix4x4.identity);
                Object.Destroy(curl);
                // A small curl back up to the lantern's floor.
                var stay = Lofts.Tube(new[]
                {
                    (S(0.21f, 2.88f), new Vector2(0.008f, 0.008f)), (S(0.2f, 2.95f), new Vector2(0.008f, 0.008f)), (S(0.19f, 3.0f), new Vector2(0.008f, 0.008f)),
                }, 2f, true, true);
                lantern.AppendMesh(stay, Matrix4x4.identity);
                Object.Destroy(stay);
            }

            // The lantern, six-sided and wider at the top: a bowl under it, six panes in an iron frame, a flared cap,
            // an ogee roof, a crown of brass spikes and a turned brass finial.
            lantern.AddLathe(o, P(0f, 2.93f, 0.07f, 2.94f, 0.15f, 2.97f, 0.205f, 3.0f, 0.215f, 3.03f, 0.18f, 3.05f, 0f, 3.05f), 6, true);
            glass.AddLathe(o, P(0.172f, 3.05f, 0.232f, 3.42f), 6, true);
            for (int i = 0; i < 6; i++)
            {
                float a = i / 6f * Mathf.PI * 2f;
                var d = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
                lantern.AddTube(d * 0.18f + Vector3.up * 3.04f, d * 0.242f + Vector3.up * 3.43f, 0.011f, 0.011f, 5);
                // (A thin glazing bar across the middle of each pane.)
                var d2 = new Vector3(Mathf.Cos(a + Mathf.PI / 3f), 0f, Mathf.Sin(a + Mathf.PI / 3f));
                lantern.AddTube(d * 0.2f + Vector3.up * 3.18f, d2 * 0.2f + Vector3.up * 3.18f, 0.005f, 0.005f, 4);
                brass.AddTube(d * 0.285f + Vector3.up * 3.47f, d * 0.27f + Vector3.up * 3.57f, 0.011f, 0f, 5);
            }
            lantern.AddLathe(o, P(0.225f, 3.41f, 0.285f, 3.44f, 0.295f, 3.47f, 0.26f, 3.49f), 6, true);
            lantern.AddLathe(o, P(0.26f, 3.49f, 0.235f, 3.53f, 0.17f, 3.59f, 0.11f, 3.65f, 0.075f, 3.71f, 0.08f, 3.75f, 0.05f, 3.79f, 0f, 3.79f), 6, true);
            brass.AddLathe(o, P(0f, 3.78f, 0.045f, 3.79f, 0.052f, 3.82f, 0.03f, 3.86f, 0.046f, 3.9f, 0.03f, 3.95f, 0.012f, 3.98f, 0.007f, 4.03f, 0f, 4.05f), 12, false);

            shapeStone = stone.ToMesh("Lamp plinth");
            shapeIron = iron.ToMesh("Lamp iron");
            shapeBrass = brass.ToMesh("Lamp brass");
            shapeGlass = glass.ToMesh("Lamp glass");
            shapeLantern = lantern.ToMesh("Lamp lantern");
        }

        static T Find<T>(GameObject root, string nameContains) where T : Component
        {
            foreach (var c in root.GetComponentsInChildren<T>(true))
                if (c.name.ToLowerInvariant().Contains(nameContains)) return c;
            return null;
        }

        static GameObject Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Material mat, bool collider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = Layers.Props;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            return go;
        }
    }
}
