using Game.Rendering;
using UnityEngine;

namespace Game.Entities
{
    /// <summary>
    /// The blind one's body: the built-in one (hunched and long-armed, starved: a spine of knobs down its back, ribs
    /// showing, shoulder blades jutting, tatters of skin; a smooth, eyeless head with rows of pits and great thin ear
    /// membranes, the mouth split down its front lined with teeth; knobbed elbows and knees, long two-jointed
    /// fingers and clawed feet up on their toes) or your own model. One unit tall with its feet at the pivot, facing +Z. It walks, cocks its head to listen,
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
            foreach (var m in new[] { skin, mouth, bone, membrane, claw }) if (m != null) Destroy(m);
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
            bone = RuntimeMaterials.Lit("Blind one bone", new Color(0.32f, 0.29f, 0.24f), 0.45f);
            membrane = RuntimeMaterials.Lit("Blind one membrane", new Color(0.16f, 0.05f, 0.05f), 0.6f, true);
            claw = RuntimeMaterials.Lit("Blind one claw", new Color(0.02f, 0.018f, 0.016f), 0.8f);

            // The hips, and the long hunched back over them: a spine of knobs down it, shoulder blades jutting.
            hips = Joint(t, new Vector3(0f, 0.44f, 0f));
            Part(hips, PrimitiveType.Capsule, new Vector3(0f, 0.07f, 0f), new Vector3(0.13f, 0.09f, 0.09f), skin);
            foreach (float x in new[] { -0.055f, 0.055f }) Part(hips, PrimitiveType.Sphere, new Vector3(x, 0.08f, -0.03f), new Vector3(0.05f, 0.04f, 0.04f), bone); // (hip bones)
            chest = Joint(hips, new Vector3(0f, 0.16f, 0f));
            chest.localRotation = Quaternion.Euler(45f, 0f, 0f);  // hunched far over
            Part(chest, PrimitiveType.Capsule, new Vector3(0f, 0.13f, 0.01f), new Vector3(0.19f, 0.16f, 0.12f), skin);
            Part(chest, PrimitiveType.Capsule, new Vector3(0f, 0.02f, 0.015f), new Vector3(0.1f, 0.06f, 0.08f), skin); // (the sunken belly)
            for (int k = 0; k < 9; k++) // (the spine's knobs, down the back)
            {
                float y = Mathf.Lerp(-0.04f, 0.27f, k / 8f);
                Part(chest, PrimitiveType.Sphere, new Vector3(0f, y, -0.06f - 0.012f * Mathf.Sin(k / 8f * Mathf.PI)), new Vector3(0.026f, 0.02f, 0.03f), bone);
            }
            for (int k = 0; k < 6; k++) // (ribs, showing through on each side)
            {
                float y = Mathf.Lerp(0.07f, 0.22f, k / 5f);
                foreach (float s in new[] { -1f, 1f })
                {
                    Vector3 a = new Vector3(s * 0.02f, y, -0.055f), m = new Vector3(s * 0.098f, y - 0.01f, -0.005f), b = new Vector3(s * 0.065f, y - 0.025f, 0.055f);
                    Rod(chest, a, m, 0.009f, bone);
                    Rod(chest, m, b, 0.008f, bone);
                }
            }
            foreach (float s in new[] { -1f, 1f }) // (shoulder blades)
            {
                var blade = Part(chest, PrimitiveType.Sphere, new Vector3(s * 0.07f, 0.23f, -0.055f), new Vector3(0.07f, 0.09f, 0.02f), bone);
                blade.transform.localRotation = Quaternion.Euler(10f, s * 25f, s * 15f);
            }
            // Tatters of skin hanging off its back.
            for (int k = 0; k < 4; k++)
            {
                var flap = Part(chest, PrimitiveType.Cube, new Vector3((k - 1.5f) * 0.04f, 0.1f + k * 0.03f, -0.075f), new Vector3(0.03f, 0.1f, 0.004f), membrane);
                flap.transform.localRotation = Quaternion.Euler(-15f - k * 6f, 0f, (k - 1.5f) * 10f);
            }

            // The neck: knobbed vertebrae. The head: long, smooth and eyeless, a ridge down it, a row of pits each
            // side (it hears through them), great thin ear membranes, the mouth split down its front with teeth.
            neck = Joint(chest, new Vector3(0f, 0.26f, 0.02f));
            Part(neck, PrimitiveType.Capsule, new Vector3(0f, 0.04f, 0f), new Vector3(0.045f, 0.05f, 0.045f), skin);
            for (int k = 0; k < 3; k++) Part(neck, PrimitiveType.Sphere, new Vector3(0f, 0.015f + k * 0.025f, -0.022f), new Vector3(0.022f, 0.016f, 0.02f), bone);
            head = Joint(neck, new Vector3(0f, 0.08f, 0f));
            Part(head, PrimitiveType.Sphere, new Vector3(0f, 0.05f, 0.03f), new Vector3(0.09f, 0.14f, 0.11f), skin);
            Part(head, PrimitiveType.Sphere, new Vector3(0f, 0.09f, 0.0f), new Vector3(0.075f, 0.09f, 0.1f), skin); // (the swollen back of the skull)
            Rod(head, new Vector3(0f, 0.12f, -0.03f), new Vector3(0f, 0.1f, 0.07f), 0.008f, bone); // (the ridge)
            foreach (float s in new[] { -1f, 1f })
            {
                for (int k = 0; k < 5; k++) // (the pits)
                    Part(head, PrimitiveType.Sphere, new Vector3(s * 0.04f, 0.1f - k * 0.017f, 0.03f + k * 0.008f), Vector3.one * 0.011f, mouth);
                var ear = Part(head, PrimitiveType.Sphere, new Vector3(s * 0.055f, 0.06f, -0.01f), new Vector3(0.006f, 0.09f, 0.07f), membrane);
                ear.transform.localRotation = Quaternion.Euler(-20f, s * 25f, s * 10f);
                Rod(head, new Vector3(s * 0.05f, 0.02f, -0.01f), new Vector3(s * 0.075f, 0.11f, -0.04f), 0.004f, bone); // (its spine)
            }
            jawL = Joint(head, new Vector3(-0.012f, 0.02f, 0.07f));
            jawR = Joint(head, new Vector3(0.012f, 0.02f, 0.07f));
            Part(jawL, PrimitiveType.Cube, new Vector3(-0.012f, -0.01f, 0f), new Vector3(0.024f, 0.08f, 0.03f), skin);
            Part(jawR, PrimitiveType.Cube, new Vector3(0.012f, -0.01f, 0f), new Vector3(0.024f, 0.08f, 0.03f), skin);
            foreach (var (jaw, s) in new[] { (jawL, 1f), (jawR, -1f) }) // (teeth along each edge of the split)
                for (int k = 0; k < 6; k++)
                {
                    var tooth = Part(jaw, PrimitiveType.Capsule, new Vector3(s * 0.002f, 0.022f - k * 0.012f, 0.012f), new Vector3(0.005f, 0.008f, 0.005f), bone);
                    tooth.transform.localRotation = Quaternion.Euler(0f, 0f, s * 80f);
                }
            Part(head, PrimitiveType.Cube, new Vector3(0f, 0.01f, 0.075f), new Vector3(0.018f, 0.07f, 0.01f), mouth); // the glow inside the split

            // The arms: long, knobbed at the elbow, the hands with long two-jointed fingers ending in claws.
            armL = Joint(chest, new Vector3(-0.12f, 0.22f, 0f));
            armR = Joint(chest, new Vector3(0.12f, 0.22f, 0f));
            foreL = Joint(armL, new Vector3(0f, -0.3f, 0f));
            foreR = Joint(armR, new Vector3(0f, -0.3f, 0f));
            foreach (var a in new[] { armL, armR })
            {
                Part(a, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.055f, skin); // (shoulder)
                Part(a, PrimitiveType.Capsule, new Vector3(0f, -0.15f, 0f), new Vector3(0.042f, 0.16f, 0.042f), skin);
                Part(a, PrimitiveType.Sphere, new Vector3(0f, -0.3f, -0.01f), Vector3.one * 0.04f, bone); // (elbow)
                var tatter = Part(a, PrimitiveType.Cube, new Vector3(0.01f, -0.18f, -0.022f), new Vector3(0.025f, 0.12f, 0.003f), membrane);
                tatter.transform.localRotation = Quaternion.Euler(-8f, 0f, 6f);
            }
            foreach (var f in new[] { foreL, foreR })
            {
                Part(f, PrimitiveType.Capsule, new Vector3(0f, -0.16f, 0f), new Vector3(0.032f, 0.17f, 0.032f), skin);
                Part(f, PrimitiveType.Sphere, new Vector3(0f, -0.32f, 0.005f), new Vector3(0.04f, 0.03f, 0.025f), skin); // (the hand)
                for (int i = -2; i <= 1; i++)
                {
                    float x = (i + 0.5f) * 0.011f;
                    Vector3 k1 = new Vector3(x, -0.33f, 0.008f), k2 = new Vector3(x * 1.3f, -0.4f, 0.02f), k3 = new Vector3(x * 1.5f, -0.46f, 0.012f);
                    Rod(f, k1, k2, 0.0045f, skin);
                    Rod(f, k2, k3, 0.0035f, skin);
                    Rod(f, k3, k3 + new Vector3(0f, -0.025f, 0.012f), 0.0025f, claw);
                }
                Rod(f, new Vector3(0.022f, -0.32f, 0.01f), new Vector3(0.035f, -0.37f, 0.03f), 0.004f, skin); // (thumb)
            }

            // The legs: thighs, knobbed knees, shins, and a long foot back up off the ground on its toes (it walks
            // like a dog stands), the toes clawed.
            thighL = Joint(hips, new Vector3(-0.06f, 0f, 0f));
            thighR = Joint(hips, new Vector3(0.06f, 0f, 0f));
            shinL = Joint(thighL, new Vector3(0f, -0.22f, 0f));
            shinR = Joint(thighR, new Vector3(0f, -0.22f, 0f));
            foreach (var th in new[] { thighL, thighR })
            {
                Part(th, PrimitiveType.Capsule, new Vector3(0f, -0.11f, 0f), new Vector3(0.06f, 0.12f, 0.06f), skin);
                Part(th, PrimitiveType.Sphere, new Vector3(0f, -0.22f, 0.012f), Vector3.one * 0.042f, bone); // (knee)
            }
            foreach (var sh in new[] { shinL, shinR })
            {
                Part(sh, PrimitiveType.Capsule, new Vector3(0f, -0.1f, 0f), new Vector3(0.042f, 0.11f, 0.042f), skin);
                Rod(sh, new Vector3(0f, -0.19f, 0f), new Vector3(0f, -0.21f, -0.04f), 0.016f, skin); // (the long foot, heel up)
                for (int i = -1; i <= 1; i++)
                {
                    Vector3 a = new Vector3(i * 0.012f, -0.215f, 0f), b = new Vector3(i * 0.018f, -0.225f, 0.04f);
                    Rod(sh, a, b, 0.006f, skin);
                    Rod(sh, b, b + new Vector3(0f, -0.006f, 0.018f), 0.0035f, claw);
                }
            }
        }

        Material bone, membrane, claw;

        // A capsule from a to b (in the parent's space), this thick.
        void Rod(Transform parent, Vector3 a, Vector3 b, float radius, Material m)
        {
            Vector3 d = b - a;
            if (d.sqrMagnitude < 1e-8f) return;
            var go = Part(parent, PrimitiveType.Capsule, (a + b) * 0.5f, new Vector3(radius * 2f, d.magnitude * 0.5f + radius, radius * 2f), m);
            go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d);
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

        GameObject Part(Transform parent, PrimitiveType type, Vector3 at, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.layer = Game.Core.Layers.Entities;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }
    }
}
