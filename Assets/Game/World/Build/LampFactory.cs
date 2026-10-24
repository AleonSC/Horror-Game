using Game.Core;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.World.Build
{
    /// <summary>
    /// Makes a lamp post to crank (built in, until there's a prefab): a Victorian street lamp with a glass lantern,
    /// a blinking red marker on its tip, a crank to click, and its warm light.
    /// </summary>
    public sealed class LampFactory
    {
        readonly Material metal, glass, marker, crank, brass, stone;
        static Mesh pyramid;
        readonly GameObject prefab;

        /// <param name="prefab">Your own lamp post (WorldArtConfig), or null for the built-in one.</param>
        public LampFactory(GameObject prefab = null)
        {
            this.prefab = prefab;
            metal = RuntimeMaterials.Lit("Lamp metal", new Color(0.06f, 0.06f, 0.06f), 0.5f);
            glass = RuntimeMaterials.LitGlow("Lamp glass", new Color(1f, 0.85f, 0.6f), new Color(1f, 0.7f, 0.4f) * 6f);
            marker = RuntimeMaterials.Glow("Lamp marker", new Color(1f, 0.05f, 0.02f));
            crank = RuntimeMaterials.Lit("Crank", new Color(0.12f, 0.1f, 0.08f), 0.4f);
            brass = RuntimeMaterials.Lit("Lamp brass", new Color(0.42f, 0.3f, 0.12f), 0.65f);
            stone = RuntimeMaterials.Lit("Lamp base", new Color(0.16f, 0.16f, 0.15f), 0.2f);
            foreach (var m in new[] { metal, brass, crank }) m.SetFloat("_Metallic", m == brass ? 0.9f : 0.75f);
        }

        // A four-sided pyramid, its base 1 x 1 at y 0 and its point at y 1 (the lantern's roof).
        static Mesh Pyramid
        {
            get
            {
                if (pyramid != null) return pyramid;
                var top = new Vector3(0f, 1f, 0f);
                var c = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f) };
                var verts = new System.Collections.Generic.List<Vector3>();
                var tris = new System.Collections.Generic.List<int>();
                for (int i = 0; i < 4; i++)
                {
                    int v = verts.Count;
                    verts.Add(c[i]); verts.Add(top); verts.Add(c[(i + 1) % 4]);
                    tris.Add(v); tris.Add(v + 1); tris.Add(v + 2);
                }
                pyramid = new Mesh { name = "Lamp roof" };
                pyramid.SetVertices(verts);
                pyramid.SetTriangles(tris, 0);
                pyramid.RecalculateNormals();
                pyramid.RecalculateBounds();
                return pyramid;
            }
        }

        public Material[] Materials => new[] { metal, glass, marker, crank, brass, stone };

        public Lamp Create(int index, Vector3 position, float yaw, Transform parent)
        {
            if (prefab != null) return FromPrefab(index, position, yaw, parent);
            var root = new GameObject($"Lamp {index}") { layer = Layers.Props };
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            // A Victorian street lamp: a stepped base, a fluted iron post ringed with brass, a crossbar, and a
            // lantern of four glass panes in an iron frame under a sloping roof and a brass finial.
            var t = root.transform;
            Part(PrimitiveType.Cylinder, "Base", t, new Vector3(0f, 0.09f, 0f), new Vector3(0.6f, 0.09f, 0.6f), stone, true);
            Part(PrimitiveType.Cylinder, "Base step", t, new Vector3(0f, 0.24f, 0f), new Vector3(0.44f, 0.06f, 0.44f), metal, false);
            Part(PrimitiveType.Cylinder, "Base bell", t, new Vector3(0f, 0.46f, 0f), new Vector3(0.3f, 0.16f, 0.3f), metal, false);
            Part(PrimitiveType.Sphere, "Base bulb", t, new Vector3(0f, 0.66f, 0f), new Vector3(0.24f, 0.12f, 0.24f), metal, false);
            Part(PrimitiveType.Cylinder, "Post", t, new Vector3(0f, 1.75f, 0f), new Vector3(0.13f, 1.12f, 0.13f), metal, true);
            for (int i = 0; i < 6; i++) // (the flutes)
            {
                float a = i / 6f * Mathf.PI * 2f;
                Part(PrimitiveType.Cylinder, "Flute", t, new Vector3(Mathf.Cos(a) * 0.066f, 1.7f, Mathf.Sin(a) * 0.066f), new Vector3(0.022f, 0.95f, 0.022f), metal, false);
            }
            foreach (float y in new[] { 0.78f, 1.55f, 2.62f, 2.86f })
                Part(PrimitiveType.Cylinder, "Collar", t, new Vector3(0f, y, 0f), new Vector3(0.18f, 0.025f, 0.18f), brass, false);
            // The crossbar (for a ladder to lean on), with ball ends.
            Part(PrimitiveType.Cube, "Crossbar", t, new Vector3(0f, 2.74f, 0f), new Vector3(0.62f, 0.035f, 0.035f), metal, false);
            for (int side = -1; side <= 1; side += 2)
                Part(PrimitiveType.Sphere, "Crossbar end", t, new Vector3(side * 0.31f, 2.74f, 0f), Vector3.one * 0.06f, brass, false);
            // The lantern: a dish under it, corner posts, the glass, a cap, the roof and the finial.
            Part(PrimitiveType.Cylinder, "Lantern dish", t, new Vector3(0f, 2.97f, 0f), new Vector3(0.34f, 0.03f, 0.34f), metal, false);
            Part(PrimitiveType.Cube, "Lantern floor", t, new Vector3(0f, 3.02f, 0f), new Vector3(0.36f, 0.04f, 0.36f), metal, false);
            var glassPart = Part(PrimitiveType.Cube, "Glass", t, new Vector3(0f, 3.24f, 0f), new Vector3(0.3f, 0.4f, 0.3f), glass, false);
            for (int c = 0; c < 4; c++)
            {
                float x = c % 2 == 0 ? -0.165f : 0.165f, z = c < 2 ? -0.165f : 0.165f;
                Part(PrimitiveType.Cube, "Frame post", t, new Vector3(x, 3.24f, z), new Vector3(0.03f, 0.44f, 0.03f), metal, false);
            }
            Part(PrimitiveType.Cube, "Frame band", t, new Vector3(0f, 3.12f, 0f), new Vector3(0.34f, 0.02f, 0.34f), metal, false);
            Part(PrimitiveType.Cube, "Lantern cap", t, new Vector3(0f, 3.46f, 0f), new Vector3(0.42f, 0.04f, 0.42f), metal, false);
            var roof = new GameObject("Roof") { layer = Layers.Props };
            roof.transform.SetParent(t, false);
            roof.transform.localPosition = new Vector3(0f, 3.48f, 0f);
            roof.transform.localScale = new Vector3(0.42f, 0.24f, 0.42f);
            roof.AddComponent<MeshFilter>().sharedMesh = Pyramid;
            roof.AddComponent<MeshRenderer>().sharedMaterial = metal;
            Part(PrimitiveType.Sphere, "Finial", t, new Vector3(0f, 3.75f, 0f), Vector3.one * 0.07f, brass, false);
            Part(PrimitiveType.Cylinder, "Finial spike", t, new Vector3(0f, 3.84f, 0f), new Vector3(0.02f, 0.06f, 0.02f), brass, false);
            var markerPart = Part(PrimitiveType.Sphere, "Marker", t, new Vector3(0f, 3.93f, 0f), Vector3.one * 0.08f, marker, false);
            // The crank: a housing on the post, the axle out of it, an arm and a knob.
            Part(PrimitiveType.Cube, "Crank housing", t, new Vector3(0.09f, 1.1f, 0f), new Vector3(0.09f, 0.18f, 0.13f), metal, false);
            var crankPart = Part(PrimitiveType.Cylinder, "Crank", t, new Vector3(0.18f, 1.1f, 0f), new Vector3(0.05f, 0.06f, 0.05f), crank, true);
            crankPart.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            Part(PrimitiveType.Cube, "Crank arm", t, new Vector3(0.23f, 1.17f, 0f), new Vector3(0.025f, 0.16f, 0.03f), crank, false);
            Part(PrimitiveType.Cylinder, "Crank knob", t, new Vector3(0.27f, 1.24f, 0f), new Vector3(0.035f, 0.04f, 0.035f), brass, false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

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
            lamp.crank = crankPart.transform;
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
