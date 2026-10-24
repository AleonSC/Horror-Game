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
                    FlareGunModel(t, black, metal);
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
                case ItemKind.Drone:
                    DroneModel(t, black, metal);
                    break;
                case ItemKind.Radio:
                {
                    // A small transistor radio: a case, a speaker grille, a dial, an aerial.
                    Part(PrimitiveType.Cube, t, new Vector3(0f, 0.06f, 0f), new Vector3(0.2f, 0.12f, 0.06f), Mat("Radio case", new Color(0.35f, 0.18f, 0.1f), 0.4f));
                    Part(PrimitiveType.Cube, t, new Vector3(-0.04f, 0.06f, 0.031f), new Vector3(0.09f, 0.08f, 0.004f), Mat("Grille", new Color(0.62f, 0.58f, 0.5f), 0.5f));
                    Cyl(t, new Vector3(0.06f, 0.07f, 0.032f), 0.022f, 0.006f, metal).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    var aerial = Cyl(t, new Vector3(0.08f, 0.2f, 0f), 0.003f, 0.2f, metal);
                    aerial.transform.localRotation = Quaternion.Euler(0f, 0f, -15f);
                    break;
                }
                case ItemKind.KeyFob:
                    Part(PrimitiveType.Cube, t, new Vector3(0f, 0f, 0f), new Vector3(0.03f, 0.012f, 0.06f), black);
                    Part(PrimitiveType.Cube, t, new Vector3(0f, 0.007f, 0.01f), new Vector3(0.014f, 0.004f, 0.014f), Mat("Fob button", new Color(0.5f, 0.05f, 0.04f), 0.5f));
                    break;
            }
            return root;
        }

        /// <summary>A small quadcopter: a body, four arms and rotors, a camera underneath. Its rotors are named
        /// "Rotor" (they spin in flight).</summary>
        public static void DroneModel(Transform t, Material black, Material metal)
        {
            Part(PrimitiveType.Cube, t, new Vector3(0f, 0.04f, 0f), new Vector3(0.14f, 0.05f, 0.2f), Mat("Drone shell", new Color(0.55f, 0.56f, 0.58f), 0.6f));
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -1f : 1f, z = i < 2 ? -1f : 1f;
                var arm = Part(PrimitiveType.Cube, t, new Vector3(x * 0.09f, 0.045f, z * 0.09f), new Vector3(0.012f, 0.012f, 0.16f), black);
                arm.transform.localRotation = Quaternion.Euler(0f, x * z * 45f, 0f);
                Cyl(t, new Vector3(x * 0.15f, 0.06f, z * 0.15f), 0.012f, 0.02f, metal);
                var rotor = Part(PrimitiveType.Cube, t, new Vector3(x * 0.15f, 0.075f, z * 0.15f), new Vector3(0.16f, 0.003f, 0.016f), black);
                rotor.name = "Rotor";
            }
            Part(PrimitiveType.Sphere, t, new Vector3(0f, 0.005f, 0.07f), Vector3.one * 0.035f, black); // (its camera)
        }

        /// <summary>
        /// A flare gun, the break-open kind: a chunky blaze-orange frame and a fat barrel with a dark muzzle ring,
        /// a ribbed top, the hinge pin and the seam where it breaks open, a white warning band, the brass rim of the
        /// cartridge showing at the breech, a black hammer, a trigger in its guard, ribbed black grip panels and a
        /// lanyard ring at the bottom of the grip.
        /// </summary>
        static void FlareGunModel(Transform t, Material black, Material metal)
        {
            var orange = Mat("Flare gun", new Color(0.85f, 0.28f, 0.03f), 0.45f);
            var dark = Mat("Flare gun (dark)", new Color(0.12f, 0.05f, 0.02f), 0.35f);
            var brass = Mat("Cartridge brass", new Color(0.55f, 0.42f, 0.16f), 0.75f);
            var label = Mat("Warning label", new Color(0.82f, 0.8f, 0.72f), 0.3f);
            // The barrel: fat, with a dark muzzle, a top rib, a white band and the seam behind it.
            Cyl(t, new Vector3(0f, 0.05f, 0.11f), 0.024f, 0.17f, orange);
            Cyl(t, new Vector3(0f, 0.05f, 0.198f), 0.027f, 0.014f, dark);
            Cyl(t, new Vector3(0f, 0.05f, 0.204f), 0.017f, 0.004f, black); // (the bore)
            Part(PrimitiveType.Cube, t, new Vector3(0f, 0.076f, 0.11f), new Vector3(0.012f, 0.008f, 0.16f), orange);
            for (int i = 0; i < 6; i++) Part(PrimitiveType.Cube, t, new Vector3(0f, 0.081f, 0.05f + i * 0.024f), new Vector3(0.013f, 0.003f, 0.006f), dark);
            Cyl(t, new Vector3(0f, 0.05f, 0.12f), 0.0245f, 0.03f, label);
            Cyl(t, new Vector3(0f, 0.05f, 0.032f), 0.0255f, 0.003f, dark);
            // The frame, the hinge under the barrel, the cartridge rim at the breech, the hammer.
            Part(PrimitiveType.Cube, t, new Vector3(0f, 0.042f, 0.005f), new Vector3(0.042f, 0.055f, 0.06f), orange);
            Cyl(t, new Vector3(0f, 0.022f, 0.04f), 0.006f, 0.046f, metal).transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            Cyl(t, new Vector3(0f, 0.05f, 0.028f), 0.022f, 0.004f, brass);
            var hammer = Part(PrimitiveType.Cube, t, new Vector3(0f, 0.078f, -0.028f), new Vector3(0.012f, 0.025f, 0.014f), black);
            hammer.transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
            // The trigger and its guard.
            var trigger = Part(PrimitiveType.Cube, t, new Vector3(0f, 0.006f, 0.022f), new Vector3(0.008f, 0.024f, 0.006f), black);
            trigger.transform.localRotation = Quaternion.Euler(20f, 0f, 0f);
            Part(PrimitiveType.Cube, t, new Vector3(0f, -0.012f, 0.03f), new Vector3(0.012f, 0.005f, 0.05f), orange);
            Part(PrimitiveType.Cube, t, new Vector3(0f, 0.002f, 0.056f), new Vector3(0.012f, 0.03f, 0.005f), orange);
            // The grip: angled back, ribbed black panels on both sides, a lanyard ring at its foot.
            var grip = new GameObject("Grip").transform;
            grip.SetParent(t, false);
            grip.localPosition = new Vector3(0f, 0.015f, -0.018f);
            grip.localRotation = Quaternion.Euler(-18f, 0f, 0f);
            Part(PrimitiveType.Cube, grip, new Vector3(0f, -0.045f, 0f), new Vector3(0.032f, 0.095f, 0.036f), orange);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(PrimitiveType.Cube, grip, new Vector3(side * 0.0165f, -0.045f, 0f), new Vector3(0.003f, 0.075f, 0.028f), black);
                for (int r = 0; r < 6; r++)
                    Part(PrimitiveType.Cube, grip, new Vector3(side * 0.0185f, -0.017f - r * 0.011f, 0f), new Vector3(0.002f, 0.003f, 0.026f), dark);
            }
            Part(PrimitiveType.Cube, grip, new Vector3(0f, -0.094f, 0f), new Vector3(0.036f, 0.008f, 0.04f), dark);
            Ring(grip, new Vector3(0f, -0.104f, -0.012f), 0.008f, 0.0018f, metal, Vector3.forward);
        }

        /// <summary>
        /// A glowstick (also the thrown one): a slim tube of glowing yellow, bright but not blown out, a ridged
        /// black cap at each end, and a little hanging loop through one of them.
        /// </summary>
        public static GameObject Glowstick(Transform parent, Color? glow = null)
        {
            var c = glow ?? new Color(1f, 0.85f, 0.15f);
            var root = new GameObject("Glowstick").transform;
            root.SetParent(parent, false);
            var tubeMat = LitGlowMat("Glowstick", new Color(0.95f, 0.85f, 0.3f), c * 0.9f);
            var tube = Part(PrimitiveType.Capsule, root, Vector3.zero, new Vector3(0.016f, 0.07f, 0.016f), tubeMat);
            tube.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var capMat = Mat("Glowstick cap", new Color(0.04f, 0.04f, 0.045f), 0.5f);
            for (int end = -1; end <= 1; end += 2)
            {
                Cyl(root, new Vector3(0f, 0f, end * 0.068f), 0.0095f, 0.012f, capMat);
                Cyl(root, new Vector3(0f, 0f, end * 0.061f), 0.0099f, 0.002f, capMat); // (its ridge)
            }
            Ring(root, new Vector3(0f, 0f, 0.083f), 0.007f, 0.0015f, capMat, Vector3.right);
            return root.gameObject;
        }

        // A thin ring of small segments round an axis.
        static void Ring(Transform parent, Vector3 centre, float radius, float thickness, Material m, Vector3 axis)
        {
            Vector3 u = Vector3.Cross(axis, Mathf.Abs(axis.y) < 0.9f ? Vector3.up : Vector3.right).normalized, v = Vector3.Cross(axis, u);
            const int segments = 10;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i / (float)segments * Mathf.PI * 2f, a1 = (i + 1) / (float)segments * Mathf.PI * 2f;
                Vector3 p0 = centre + (u * Mathf.Cos(a0) + v * Mathf.Sin(a0)) * radius, p1 = centre + (u * Mathf.Cos(a1) + v * Mathf.Sin(a1)) * radius;
                var seg = Part(PrimitiveType.Cylinder, parent, (p0 + p1) * 0.5f, new Vector3(thickness * 2f, (p1 - p0).magnitude * 0.55f, thickness * 2f), m);
                seg.transform.localRotation = Quaternion.FromToRotation(Vector3.up, p1 - p0);
            }
        }

        static Material LitGlowMat(string name, Color baseColor, Color glow)
        {
            string key = name + "lit" + glow;
            if (!materials.TryGetValue(key, out var m) || m == null) materials[key] = m = RuntimeMaterials.LitGlow(name, baseColor, glow);
            return m;
        }

        static Material flameMat;

        // A live flame (the flame shader), or if that can't be used, a glowing teardrop.
        static void Flame(Transform parent, Vector3 at, float size)
        {
            var fire = Effects.Flame(parent, at - Vector3.up * size * 0.4f, size * 1.3f, size * 2.6f, Color.white * 2f);
            if (fire != null)
            {
                if (flameMat == null) flameMat = fire.sharedMaterial;
                else { Object.Destroy(fire.sharedMaterial); fire.sharedMaterial = flameMat; }
                return; // (named "Flame": shown while lit, and the light sits there)
            }
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
