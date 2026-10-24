using Game.Rendering;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// The built-in animals, from simple shapes: an owl, a rat, a beetle, a bat and a crow. Each faces +Z
    /// with its feet (or middle, for fliers) at the pivot. Parts that move are named: "Head", "Leg0".."Leg3",
    /// "WingL"/"WingR".
    /// </summary>
    public static class AnimalModels
    {
        static Material brown, grey, black, dark, eyes;

        static void Mats()
        {
            if (brown != null) return;
            brown = RuntimeMaterials.Lit("Animal brown", new Color(0.16f, 0.11f, 0.07f), 0.25f);
            grey = RuntimeMaterials.Lit("Animal grey", new Color(0.13f, 0.12f, 0.12f), 0.3f);
            black = RuntimeMaterials.Lit("Animal black", new Color(0.02f, 0.02f, 0.02f), 0.4f);
            dark = RuntimeMaterials.Lit("Beetle shell", new Color(0.03f, 0.035f, 0.03f), 0.85f);
            eyes = RuntimeMaterials.Glow("Owl eyes", new Color(0.9f, 0.95f, 1f) * 3f);
        }

        public static GameObject Owl(Transform parent)
        {
            Mats();
            var root = Root("Owl", parent);
            Part(PrimitiveType.Capsule, root, new Vector3(0f, 0.18f, 0f), new Vector3(0.22f, 0.18f, 0.2f), grey);
            var head = Joint(root, "Head", new Vector3(0f, 0.38f, 0f));
            Part(PrimitiveType.Sphere, head, Vector3.zero, new Vector3(0.2f, 0.17f, 0.18f), grey);
            for (int e = -1; e <= 1; e += 2) Part(PrimitiveType.Sphere, head, new Vector3(e * 0.045f, 0.01f, 0.08f), Vector3.one * 0.035f, eyes);
            return root.gameObject;
        }

        public static GameObject Rat(Transform parent)
        {
            Mats();
            var root = Root("Rat", parent);
            var body = Part(PrimitiveType.Capsule, root, new Vector3(0f, 0.05f, 0f), new Vector3(0.08f, 0.1f, 0.08f), grey);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part(PrimitiveType.Sphere, root, new Vector3(0f, 0.06f, 0.11f), new Vector3(0.05f, 0.05f, 0.07f), grey);
            var tail = Part(PrimitiveType.Cylinder, root, new Vector3(0f, 0.03f, -0.2f), new Vector3(0.01f, 0.1f, 0.01f), grey);
            tail.transform.localRotation = Quaternion.Euler(80f, 0f, 0f);
            return root.gameObject;
        }

        public static GameObject Beetle(Transform parent)
        {
            Mats();
            var root = Root("Beetle", parent);
            Part(PrimitiveType.Sphere, root, new Vector3(0f, 0.008f, 0f), new Vector3(0.025f, 0.014f, 0.035f), dark);
            return root.gameObject;
        }

        /// <summary>A bat or a crow: a body and two wings that flap.</summary>
        public static GameObject Flier(Transform parent, bool crow)
        {
            Mats();
            var root = Root(crow ? "Crow" : "Bat", parent);
            float span = crow ? 0.45f : 0.3f;
            var body = Part(PrimitiveType.Capsule, root, Vector3.zero, new Vector3(0.06f, crow ? 0.12f : 0.07f, 0.06f), black);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            for (int side = -1; side <= 1; side += 2)
            {
                var wing = Joint(root, side < 0 ? "WingL" : "WingR", new Vector3(side * 0.03f, 0f, 0f));
                Part(PrimitiveType.Cube, wing, new Vector3(side * span * 0.25f, 0f, 0f), new Vector3(span * 0.5f, 0.005f, crow ? 0.12f : 0.1f), black);
            }
            return root.gameObject;
        }

        static Transform Root(string name, Transform parent)
        {
            var go = new GameObject(name) { layer = Game.Core.Layers.Default };
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        static Transform Joint(Transform parent, string name, Vector3 at)
        {
            var j = new GameObject(name).transform;
            j.SetParent(parent, false);
            j.localPosition = at;
            return j;
        }

        static GameObject Part(PrimitiveType type, Transform parent, Vector3 at, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return go;
        }
    }
}
