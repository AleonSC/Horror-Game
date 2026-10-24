using Game.Rendering;
using UnityEngine;

namespace Game.Entities
{
    /// <summary>
    /// The blind one's body: the built-in one (hunched and long-armed, a smooth eyeless head with a split mouth) or
    /// your own model. One unit tall with its feet at the pivot, facing +Z. It walks, cocks its head to listen,
    /// opens its mouth to roar, and leans down to your face.
    /// </summary>
    public sealed class BlindBody : MonoBehaviour
    {
        Transform hips, chest, neck, head, jawL, jawR, armL, armR, foreL, foreR, thighL, thighR, shinL, shinR;
        Animator animator;
        Material skin, mouth;
        bool custom;
        float walkPhase;

        public static BlindBody Create(Transform parent, BlindConfig config)
        {
            var go = new GameObject("Blind one body") { layer = Game.Core.Layers.Entities };
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<BlindBody>();
            b.skin = RuntimeMaterials.Lit("Blind one skin", new Color(0.035f, 0.032f, 0.03f), 0.35f);
            b.mouth = RuntimeMaterials.Glow("Blind one mouth", new Color(0.5f, 0.02f, 0.01f));
            if (config.customModel != null) b.UseYours(config.customModel);
            else b.Build();
            return b;
        }

        void OnDestroy()
        {
            if (skin != null) Destroy(skin);
            if (mouth != null) Destroy(mouth);
        }

        /// <summary>The top of its head, in its own space (one unit tall).</summary>
        public Vector3 HeadLocal => new Vector3(0f, 0.9f, 0.18f);

        void UseYours(GameObject prefab)
        {
            custom = true;
            var model = Instantiate(prefab, transform, false);
            foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                if (b.size.y > 0.001f) model.transform.localScale = model.transform.localScale / b.size.y;
            }
            animator = model.GetComponentInChildren<Animator>();
        }

        void Build()
        {
            var t = transform;
            hips = Joint(t, new Vector3(0f, 0.44f, 0f));
            Part(hips, PrimitiveType.Capsule, new Vector3(0f, 0.08f, 0f), new Vector3(0.12f, 0.1f, 0.09f), skin);
            chest = Joint(hips, new Vector3(0f, 0.16f, 0f));
            chest.localRotation = Quaternion.Euler(45f, 0f, 0f);  // hunched far over
            Part(chest, PrimitiveType.Capsule, new Vector3(0f, 0.12f, 0f), new Vector3(0.2f, 0.16f, 0.13f), skin);
            neck = Joint(chest, new Vector3(0f, 0.26f, 0.02f));
            Part(neck, PrimitiveType.Capsule, new Vector3(0f, 0.04f, 0f), new Vector3(0.05f, 0.05f, 0.05f), skin);
            head = Joint(neck, new Vector3(0f, 0.08f, 0f));
            // A smooth, eyeless, elongated head, and a mouth split down its front.
            Part(head, PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.03f), new Vector3(0.09f, 0.14f, 0.11f), skin);
            jawL = Joint(head, new Vector3(-0.012f, 0.02f, 0.07f));
            jawR = Joint(head, new Vector3(0.012f, 0.02f, 0.07f));
            Part(jawL, PrimitiveType.Cube, new Vector3(-0.012f, -0.01f, 0f), new Vector3(0.024f, 0.08f, 0.03f), skin);
            Part(jawR, PrimitiveType.Cube, new Vector3(0.012f, -0.01f, 0f), new Vector3(0.024f, 0.08f, 0.03f), skin);
            Part(head, PrimitiveType.Cube, new Vector3(0f, 0.01f, 0.075f), new Vector3(0.018f, 0.07f, 0.01f), mouth); // the glow inside the split

            armL = Joint(chest, new Vector3(-0.12f, 0.22f, 0f));
            armR = Joint(chest, new Vector3(0.12f, 0.22f, 0f));
            foreL = Joint(armL, new Vector3(0f, -0.3f, 0f));
            foreR = Joint(armR, new Vector3(0f, -0.3f, 0f));
            foreach (var a in new[] { armL, armR }) Part(a, PrimitiveType.Capsule, new Vector3(0f, -0.15f, 0f), new Vector3(0.045f, 0.16f, 0.045f), skin);
            foreach (var f in new[] { foreL, foreR })
            {
                Part(f, PrimitiveType.Capsule, new Vector3(0f, -0.16f, 0f), new Vector3(0.035f, 0.17f, 0.035f), skin);
                for (int i = -1; i <= 1; i++)
                    Part(f, PrimitiveType.Capsule, new Vector3(i * 0.012f, -0.36f, 0.01f), new Vector3(0.01f, 0.06f, 0.01f), skin); // long fingers
            }

            thighL = Joint(hips, new Vector3(-0.06f, 0f, 0f));
            thighR = Joint(hips, new Vector3(0.06f, 0f, 0f));
            shinL = Joint(thighL, new Vector3(0f, -0.22f, 0f));
            shinR = Joint(thighR, new Vector3(0f, -0.22f, 0f));
            foreach (var th in new[] { thighL, thighR }) Part(th, PrimitiveType.Capsule, new Vector3(0f, -0.11f, 0f), new Vector3(0.06f, 0.12f, 0.06f), skin);
            foreach (var sh in new[] { shinL, shinR }) Part(sh, PrimitiveType.Capsule, new Vector3(0f, -0.11f, 0f), new Vector3(0.045f, 0.12f, 0.045f), skin);
        }

        /// <summary>
        /// The pose for this frame. <paramref name="speed"/> m/s walking; <paramref name="listen"/> 0-1 cocking its head;
        /// <paramref name="roar"/> 0-1 mouth wide and arms up; <paramref name="lean"/> 0-1 bent down to your face.
        /// </summary>
        public void Pose(float speed, float height, float listen, float roar, float lean, float dt)
        {
            if (custom)
            {
                if (animator != null)
                {
                    animator.SetFloat("Speed", speed);
                    animator.SetBool("Listen", listen > 0.5f);
                }
                return;
            }
            walkPhase += dt * speed / Mathf.Max(0.5f, height * 0.55f) * Mathf.PI * 2f;
            float moving = Mathf.Clamp01(speed / 1.5f);
            float sw = Mathf.Sin(walkPhase);
            // A limping gait: one leg drags.
            thighL.localRotation = Quaternion.Euler(-sw * 28f * moving, 0f, -3f);
            thighR.localRotation = Quaternion.Euler(sw * 18f * moving, 0f, 3f);
            shinL.localRotation = Quaternion.Euler(Mathf.Max(0f, Mathf.Sin(walkPhase + 1.2f)) * 40f * moving + 6f, 0f, 0f);
            shinR.localRotation = Quaternion.Euler(Mathf.Max(0f, -Mathf.Sin(walkPhase + 1.2f)) * 25f * moving + 6f, 0f, 0f);
            hips.localRotation = Quaternion.Euler(lean * 25f, sw * 6f * moving, sw * 3f * moving);
            chest.localRotation = Quaternion.Euler(45f + lean * 30f - roar * 35f, 0f, 0f);
            // Arms swing long and loose, knuckles near the ground; up and wide to roar.
            armL.localRotation = Quaternion.Euler(sw * 22f * moving - 40f - roar * 60f, 0f, -8f - roar * 50f);
            armR.localRotation = Quaternion.Euler(-sw * 22f * moving - 40f - roar * 60f, 0f, 8f + roar * 50f);
            foreL.localRotation = foreR.localRotation = Quaternion.Euler(-10f - roar * 20f, 0f, 0f);
            neck.localRotation = Quaternion.Euler(-35f + lean * 40f, 0f, 0f);
            head.localRotation = Quaternion.Euler(-10f + lean * 20f, listen * 12f, listen * 32f);
            float open = Mathf.Max(roar, lean * 0.6f) * 18f + listen * 3f * (0.5f + 0.5f * Mathf.Sin(Time.time * 30f));
            jawL.localRotation = Quaternion.Euler(0f, -open, 0f);
            jawR.localRotation = Quaternion.Euler(0f, open, 0f);
        }

        public void Trigger(string name)
        {
            if (animator != null) animator.SetTrigger(name);
        }

        Transform Joint(Transform parent, Vector3 at)
        {
            var j = new GameObject("Joint").transform;
            j.SetParent(parent, false);
            j.localPosition = at;
            return j;
        }

        void Part(Transform parent, PrimitiveType type, Vector3 at, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.layer = Game.Core.Layers.Entities;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
        }
    }
}
