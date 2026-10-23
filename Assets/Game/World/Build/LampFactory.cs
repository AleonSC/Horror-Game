using Game.Core;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.World.Build
{
    /// <summary>
    /// Makes a lamp post to crank (a grey-box stand-in until there's a prefab): post, head, glass, a blinking red
    /// marker on top, a crank to click, and its warm light.
    /// </summary>
    public sealed class LampFactory
    {
        readonly Material metal, glass, marker, crank;
        readonly GameObject prefab;

        /// <param name="prefab">Your own lamp post (WorldArtConfig), or null for the built-in one.</param>
        public LampFactory(GameObject prefab = null)
        {
            this.prefab = prefab;
            metal = RuntimeMaterials.Lit("Lamp metal", new Color(0.06f, 0.06f, 0.06f), 0.5f);
            glass = RuntimeMaterials.LitGlow("Lamp glass", new Color(1f, 0.85f, 0.6f), new Color(1f, 0.7f, 0.4f) * 6f);
            marker = RuntimeMaterials.Glow("Lamp marker", new Color(1f, 0.05f, 0.02f));
            crank = RuntimeMaterials.Lit("Crank", new Color(0.12f, 0.1f, 0.08f), 0.4f);
        }

        public Material[] Materials => new[] { metal, glass, marker, crank };

        public Lamp Create(int index, Vector3 position, float yaw, Transform parent)
        {
            if (prefab != null) return FromPrefab(index, position, yaw, parent);
            var root = new GameObject($"Lamp {index}") { layer = Layers.Props };
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

            Part(PrimitiveType.Cylinder, "Post", root.transform, new Vector3(0f, 1.6f, 0f), new Vector3(0.12f, 1.6f, 0.12f), metal, true);
            Part(PrimitiveType.Cube, "Head", root.transform, new Vector3(0f, 3.35f, 0f), new Vector3(0.45f, 0.12f, 0.45f), metal, false);
            var glassPart = Part(PrimitiveType.Cube, "Glass", root.transform, new Vector3(0f, 3.12f, 0f), new Vector3(0.32f, 0.34f, 0.32f), glass, false);
            var markerPart = Part(PrimitiveType.Sphere, "Marker", root.transform, new Vector3(0f, 3.5f, 0f), Vector3.one * 0.1f, marker, false);
            var crankPart = Part(PrimitiveType.Cylinder, "Crank", root.transform, new Vector3(0.18f, 1.1f, 0f), new Vector3(0.08f, 0.12f, 0.08f), crank, true);
            crankPart.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            var lightGo = new GameObject("Lamp Light");
            lightGo.transform.SetParent(root.transform, false);
            lightGo.transform.localPosition = new Vector3(0f, 3f, 0f);
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
