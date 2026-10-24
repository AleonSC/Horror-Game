using System.Collections.Generic;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Rendering;

namespace Game.Player
{
    /// <summary>
    /// The built-in item models, made from simple shapes (used where ItemsConfig has none of your own). Each
    /// points forward along +Z with its pivot at the grip. A torch or lighter has a child named "Flame".
    /// </summary>
    public static class ItemModels
    {
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();

        /// <summary>Your model if there is one, else the built-in one. No colliders, no shadows from it.</summary>
        public static GameObject Make(ItemKind kind, GameObject yours, Transform parent, bool forHand)
        {
            GameObject go;
            if (yours != null)
            {
                go = Object.Instantiate(yours, parent, false);
                go.name = Inventory.NameOf(kind);
                foreach (var c in go.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            }
            else go = BuiltIn(kind, parent);
            if (forHand)
                foreach (var r in go.GetComponentsInChildren<Renderer>()) r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }

        /// <summary>A snack lying in a tent.</summary>
        public static GameObject Snack(GameObject yours, Transform parent)
        {
            if (yours != null) return Object.Instantiate(yours, parent, false);
            var root = new GameObject("Snack");
            root.transform.SetParent(parent, false);
            Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.012f, 0f), new Vector3(0.09f, 0.024f, 0.16f), Mat("Wrapper", new Color(0.55f, 0.12f, 0.08f), 0.5f));
            Part(PrimitiveType.Cube, root.transform, new Vector3(0f, 0.025f, 0.02f), new Vector3(0.06f, 0.002f, 0.05f), Mat("Label", new Color(0.8f, 0.7f, 0.3f), 0.3f));
            return root;
        }

        static GameObject BuiltIn(ItemKind kind, Transform parent)
        {
            var root = new GameObject(Inventory.NameOf(kind));
            root.transform.SetParent(parent, false);
            var t = root.transform;
            var black = Mat("Black plastic", new Color(0.03f, 0.03f, 0.03f), 0.4f);
            var metal = Mat("Steel", new Color(0.35f, 0.35f, 0.36f), 0.7f);
            switch (kind)
            {
                case ItemKind.Flashlight:
                    Cyl(t, new Vector3(0f, 0f, -0.1f), 0.032f, 0.2f, black);
                    Cyl(t, new Vector3(0f, 0f, -0.01f), 0.042f, 0.03f, metal);
                    break;
                case ItemKind.Batteries:
                    Cyl(t, new Vector3(0f, 0f, 0f), 0.017f, 0.06f, Mat("Battery", new Color(0.55f, 0.32f, 0.1f), 0.5f));
                    Cyl(t, new Vector3(0f, 0f, 0.033f), 0.006f, 0.006f, metal);
                    break;
                case ItemKind.Torch:
                    Cyl(t, new Vector3(0f, 0f, 0.15f), 0.018f, 0.55f, Mat("Torch stick", new Color(0.2f, 0.12f, 0.06f), 0.15f));
                    Cyl(t, new Vector3(0f, 0f, 0.44f), 0.035f, 0.1f, Mat("Pitch rag", new Color(0.05f, 0.04f, 0.03f), 0.2f));
                    Flame(t, new Vector3(0f, 0.02f, 0.52f), 0.09f);
                    break;
                case ItemKind.Lighter:
                    Part(PrimitiveType.Cube, t, new Vector3(0f, 0f, 0f), new Vector3(0.024f, 0.06f, 0.012f), Mat("Lighter body", new Color(0.12f, 0.2f, 0.45f), 0.6f));
                    Part(PrimitiveType.Cube, t, new Vector3(0f, 0.035f, 0f), new Vector3(0.024f, 0.01f, 0.012f), metal);
                    Flame(t, new Vector3(0f, 0.055f, 0f), 0.022f);
                    break;
                case ItemKind.FlareGun:
                    var orange = Mat("Flare gun", new Color(0.75f, 0.25f, 0.04f), 0.4f);
                    Cyl(t, new Vector3(0f, 0.04f, 0.1f), 0.022f, 0.2f, orange);
                    Part(PrimitiveType.Cube, t, new Vector3(0f, 0.04f, 0f), new Vector3(0.04f, 0.05f, 0.08f), orange);
                    var grip = Part(PrimitiveType.Cube, t, new Vector3(0f, -0.02f, -0.02f), new Vector3(0.03f, 0.09f, 0.035f), orange);
                    grip.transform.localRotation = Quaternion.Euler(-15f, 0f, 0f);
                    break;
                case ItemKind.Glowsticks:
                    Glowstick(t);
                    break;
                case ItemKind.Camera:
                    Part(PrimitiveType.Cube, t, new Vector3(0f, 0f, 0f), new Vector3(0.14f, 0.09f, 0.05f), black);
                    Cyl(t, new Vector3(0f, 0f, 0.04f), 0.03f, 0.035f, metal);
                    Part(PrimitiveType.Cube, t, new Vector3(0.04f, 0.035f, 0.026f), new Vector3(0.03f, 0.015f, 0.004f), Mat("Flash window", new Color(0.85f, 0.85f, 0.8f), 0.9f));
                    break;
                case ItemKind.Map:
                {
                    // Folded paper, a little creased.
                    var paper = Mat("Map paper", new Color(0.62f, 0.56f, 0.44f), 0.2f);
                    var a = Part(PrimitiveType.Cube, t, new Vector3(-0.055f, 0f, 0f), new Vector3(0.11f, 0.003f, 0.16f), paper);
                    a.transform.localRotation = Quaternion.Euler(0f, 0f, -4f);
                    var b = Part(PrimitiveType.Cube, t, new Vector3(0.055f, 0.002f, 0f), new Vector3(0.11f, 0.003f, 0.16f), paper);
                    b.transform.localRotation = Quaternion.Euler(0f, 0f, 5f);
                    Part(PrimitiveType.Cube, t, new Vector3(0.05f, 0.004f, 0.02f), new Vector3(0.05f, 0.001f, 0.004f), Mat("Map ink", new Color(0.08f, 0.06f, 0.05f), 0.1f));
                    break;
                }
                case ItemKind.KeyFob:
                    Part(PrimitiveType.Cube, t, new Vector3(0f, 0f, 0f), new Vector3(0.03f, 0.012f, 0.06f), black);
                    Part(PrimitiveType.Cube, t, new Vector3(0f, 0.007f, 0.01f), new Vector3(0.014f, 0.004f, 0.014f), Mat("Fob button", new Color(0.5f, 0.05f, 0.04f), 0.5f));
                    break;
            }
            return root;
        }

        /// <summary>A glowstick (also the thrown one): a thin tube that glows.</summary>
        public static GameObject Glowstick(Transform parent, Color? glow = null)
        {
            var c = glow ?? new Color(0.35f, 1f, 0.45f);
            var tube = Part(PrimitiveType.Capsule, parent, Vector3.zero, new Vector3(0.016f, 0.075f, 0.016f), GlowMat("Glowstick", c * 2f));
            tube.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            return tube;
        }

        static void Flame(Transform parent, Vector3 at, float size)
        {
            var flame = Part(PrimitiveType.Sphere, parent, at, new Vector3(size * 0.6f, size, size * 0.6f), GlowMat("Flame", new Color(1f, 0.5f, 0.15f) * 2f));
            flame.name = "Flame";
            flame.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
        }

        static GameObject Cyl(Transform parent, Vector3 at, float radius, float length, Material m)
        {
            var go = Part(PrimitiveType.Cylinder, parent, at, new Vector3(radius * 2f, length * 0.5f, radius * 2f), m);
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // (along +Z)
            return go;
        }

        static GameObject Part(PrimitiveType type, Transform parent, Vector3 at, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        static Material Mat(string name, Color c, float smooth)
        {
            if (!materials.TryGetValue(name, out var m) || m == null) materials[name] = m = RuntimeMaterials.Lit(name, c, smooth);
            return m;
        }

        static Material GlowMat(string name, Color glow)
        {
            string key = name + glow;
            if (!materials.TryGetValue(key, out var m) || m == null) materials[key] = m = RuntimeMaterials.Glow(name, glow);
            return m;
        }
    }
}
