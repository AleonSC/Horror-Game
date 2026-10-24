using Game.Rendering;
using UnityEngine;

namespace Game.Entities
{
    /// <summary>
    /// The flier's body: the built-in one (a vast ragged black bird: a lean body, a long neck and beak, and great
    /// tattered wings) or your own model. One unit across the wings, facing +Z. Its wings beat or glide.
    /// </summary>
    public sealed class FlierBody : MonoBehaviour
    {
        Transform wingL, wingR, tipL, tipR, head;
        Animator animator;
        Material skin;
        float phase;

        public Transform Eye { get; private set; }

        public static FlierBody Create(Transform parent, FlierConfig config)
        {
            var go = new GameObject("Flier body") { layer = Game.Core.Layers.Effects };
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<FlierBody>();
            b.skin = RuntimeMaterials.Haze("Flier", Color.black);
            if (config.customModel != null) b.UseYours(config.customModel);
            else b.Build();
            return b;
        }

        void OnDestroy()
        {
            if (skin != null) Destroy(skin);
        }

        public void SetColor(Color c) => skin.SetColor("_UnlitColor", c);

        void UseYours(GameObject prefab)
        {
            var model = Instantiate(prefab, transform, false);
            foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                if (b.size.x > 0.001f) model.transform.localScale = model.transform.localScale / b.size.x;
                foreach (var r in renderers) r.sharedMaterial = skin; // (drawn in the haze, so the fog can't hide it)
            }
            animator = model.GetComponentInChildren<Animator>();
            foreach (var t in model.GetComponentsInChildren<Transform>()) if (t.name == "Eye") Eye = t;
            if (Eye == null) Eye = Point(transform, new Vector3(0f, -0.01f, 0.2f));
        }

        void Build()
        {
            var t = transform;
            Shape(PrimitiveType.Capsule, t, Vector3.zero, new Vector3(0.05f, 0.11f, 0.05f), Quaternion.Euler(90f, 0f, 0f)); // body
            Shape(PrimitiveType.Capsule, t, new Vector3(0f, 0.005f, 0.13f), new Vector3(0.025f, 0.06f, 0.025f), Quaternion.Euler(70f, 0f, 0f)); // neck
            head = Point(t, new Vector3(0f, -0.01f, 0.19f));
            Shape(PrimitiveType.Sphere, head, Vector3.zero, new Vector3(0.035f, 0.03f, 0.05f), Quaternion.identity);
            Shape(PrimitiveType.Cube, head, new Vector3(0f, -0.005f, 0.045f), new Vector3(0.012f, 0.01f, 0.06f), Quaternion.Euler(12f, 0f, 0f)); // beak
            Eye = Point(head, new Vector3(0f, -0.012f, 0.02f));
            // A ragged tail.
            for (int i = -2; i <= 2; i++)
                Shape(PrimitiveType.Cube, t, new Vector3(i * 0.012f, 0f, -0.16f - Mathf.Abs(i) * 0.01f), new Vector3(0.012f, 0.003f, 0.1f - Mathf.Abs(i) * 0.015f), Quaternion.Euler(0f, i * 9f, 0f));

            wingL = Wing(t, -1, out tipL);
            wingR = Wing(t, 1, out tipR);
        }

        // A wing: an inner part and a tip that bends, both tattered into long uneven feathers.
        Transform Wing(Transform t, int side, out Transform tip)
        {
            var root = Point(t, new Vector3(side * 0.025f, 0.01f, 0.03f));
            Shape(PrimitiveType.Cube, root, new Vector3(side * 0.13f, 0f, 0f), new Vector3(0.26f, 0.006f, 0.1f), Quaternion.identity);
            tip = Point(root, new Vector3(side * 0.26f, 0f, 0f));
            Shape(PrimitiveType.Cube, tip, new Vector3(side * 0.11f, 0f, -0.01f), new Vector3(0.22f, 0.005f, 0.075f), Quaternion.Euler(0f, side * 8f, 0f));
            var rng = new System.Random(side > 0 ? 3 : 4);
            for (int f = 0; f < 9; f++)
            {
                float x = side * (0.03f + f * 0.055f);
                float len = 0.06f + (float)rng.NextDouble() * 0.07f;
                var parent = f < 5 ? root : tip;
                Vector3 at = f < 5 ? new Vector3(x, -0.002f, -0.05f - len * 0.5f) : new Vector3(x - side * 0.26f, -0.002f, -0.045f - len * 0.5f);
                Shape(PrimitiveType.Cube, parent, at, new Vector3(0.018f, 0.003f, len), Quaternion.Euler(0f, side * (f * 2f + (float)rng.NextDouble() * 8f), 0f));
            }
            return root;
        }

        /// <summary><paramref name="flap"/> 0 gliding, 1 beating hard; <paramref name="bank"/> -1..1 (it banks into turns).</summary>
        public void Pose(float flap, float bank, float dt)
        {
            if (animator != null) animator.SetFloat("Flap", flap);
            if (wingL == null) return;
            phase += dt * Mathf.Lerp(0.6f, 2.2f, flap) * Mathf.PI * 2f;
            float beat = Mathf.Sin(phase) * Mathf.Lerp(6f, 38f, flap);
            wingL.localRotation = Quaternion.Euler(0f, 0f, beat + 4f);
            wingR.localRotation = Quaternion.Euler(0f, 0f, -beat - 4f);
            float bend = Mathf.Sin(phase - 0.8f) * Mathf.Lerp(4f, 25f, flap);
            tipL.localRotation = Quaternion.Euler(0f, 0f, bend);
            tipR.localRotation = Quaternion.Euler(0f, 0f, -bend);
            transform.localRotation = Quaternion.Euler(0f, 0f, -bank * 25f);
        }

        Transform Point(Transform parent, Vector3 at)
        {
            var p = new GameObject("Point").transform;
            p.SetParent(parent, false);
            p.localPosition = at;
            return p;
        }

        void Shape(PrimitiveType type, Transform parent, Vector3 at, Vector3 scale, Quaternion rot)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.layer = Game.Core.Layers.Effects;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = skin;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }
    }
}
