using Game.Rendering;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// The built-in out-of-place things for the phantoms: a chair, a stop sign, a gurney, a TV, a mailbox and a
    /// tricycle, made of simple shapes (your own models replace them: ForestLifeConfig > Phantoms).
    /// </summary>
    public static class Phantoms
    {
        public const int Count = 6;
        static Material dark, metal, red, white;

        public static GameObject Make(int which, Transform parent)
        {
            dark ??= RuntimeMaterials.Lit("Phantom wood", new Color(0.12f, 0.09f, 0.07f), 0.3f);
            metal ??= RuntimeMaterials.Lit("Phantom metal", new Color(0.4f, 0.4f, 0.42f), 0.7f);
            red ??= RuntimeMaterials.Lit("Phantom red", new Color(0.5f, 0.04f, 0.03f), 0.5f);
            white ??= RuntimeMaterials.Lit("Phantom white", new Color(0.7f, 0.7f, 0.68f), 0.4f);
            var root = new GameObject("Phantom");
            root.transform.SetParent(parent, false);
            var t = root.transform;
            switch (which % Count)
            {
                case 0: // a chair
                    Box(t, new Vector3(0f, 0.45f, 0f), new Vector3(0.45f, 0.05f, 0.45f), dark);
                    Box(t, new Vector3(0f, 0.75f, -0.2f), new Vector3(0.45f, 0.6f, 0.05f), dark);
                    for (int i = 0; i < 4; i++) Box(t, new Vector3(i % 2 == 0 ? -0.2f : 0.2f, 0.22f, i < 2 ? -0.2f : 0.2f), new Vector3(0.04f, 0.45f, 0.04f), dark);
                    break;
                case 1: // a stop sign
                    Box(t, new Vector3(0f, 1.1f, 0f), new Vector3(0.06f, 2.2f, 0.06f), metal);
                    var sign = Shape(PrimitiveType.Cylinder, t, new Vector3(0f, 2.3f, 0.04f), new Vector3(0.75f, 0.01f, 0.75f), red);
                    sign.transform.localRotation = Quaternion.Euler(90f, 0f, 22.5f);
                    break;
                case 2: // a gurney
                    Box(t, new Vector3(0f, 0.85f, 0f), new Vector3(0.65f, 0.08f, 1.9f), white);
                    for (int i = 0; i < 4; i++) Box(t, new Vector3(i % 2 == 0 ? -0.28f : 0.28f, 0.42f, i < 2 ? -0.85f : 0.85f), new Vector3(0.04f, 0.85f, 0.04f), metal);
                    Box(t, new Vector3(0f, 0.95f, 0.1f), new Vector3(0.6f, 0.12f, 1.6f), white); // a sheet over something
                    break;
                case 3: // an old TV
                    Box(t, new Vector3(0f, 0.3f, 0f), new Vector3(0.6f, 0.5f, 0.5f), dark);
                    Box(t, new Vector3(0f, 0.32f, 0.255f), new Vector3(0.45f, 0.36f, 0.01f), RuntimeMaterials.Lit("Phantom screen", new Color(0.05f, 0.06f, 0.06f), 0.95f));
                    break;
                case 4: // a mailbox
                    Box(t, new Vector3(0f, 0.55f, 0f), new Vector3(0.08f, 1.1f, 0.08f), dark);
                    var box = Shape(PrimitiveType.Capsule, t, new Vector3(0f, 1.2f, 0f), new Vector3(0.25f, 0.25f, 0.25f), metal);
                    box.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    box.transform.localScale = new Vector3(0.25f, 0.25f, 0.25f);
                    break;
                default: // a child's tricycle
                    var front = Shape(PrimitiveType.Cylinder, t, new Vector3(0f, 0.22f, 0.3f), new Vector3(0.44f, 0.02f, 0.44f), red);
                    front.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    for (int i = -1; i <= 1; i += 2)
                    {
                        var back = Shape(PrimitiveType.Cylinder, t, new Vector3(i * 0.22f, 0.13f, -0.2f), new Vector3(0.26f, 0.02f, 0.26f), red);
                        back.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    }
                    Box(t, new Vector3(0f, 0.35f, 0.05f), new Vector3(0.05f, 0.05f, 0.55f), red);
                    Box(t, new Vector3(0f, 0.42f, -0.05f), new Vector3(0.18f, 0.04f, 0.2f), dark);
                    Box(t, new Vector3(0f, 0.55f, 0.3f), new Vector3(0.4f, 0.04f, 0.04f), metal);
                    break;
            }
            return root;
        }

        static GameObject Box(Transform p, Vector3 at, Vector3 size, Material m) => Shape(PrimitiveType.Cube, p, at, size, m);

        static GameObject Shape(PrimitiveType type, Transform parent, Vector3 at, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }
    }
}
