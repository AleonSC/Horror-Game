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

        public LampFactory()
        {
            metal = RuntimeMaterials.Lit("Lamp metal", new Color(0.06f, 0.06f, 0.06f), 0.5f);
            glass = RuntimeMaterials.LitGlow("Lamp glass", new Color(1f, 0.85f, 0.6f), new Color(1f, 0.7f, 0.4f) * 6f);
            marker = RuntimeMaterials.Glow("Lamp marker", new Color(1f, 0.05f, 0.02f));
            crank = RuntimeMaterials.Lit("Crank", new Color(0.12f, 0.1f, 0.08f), 0.4f);
        }

        public Material[] Materials => new[] { metal, glass, marker, crank };

        public Lamp Create(int index, Vector3 position, float yaw, Transform parent)
        {
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
