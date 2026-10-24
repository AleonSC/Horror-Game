using Game.Rendering;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// The built-in animals, from simple shapes: an owl, a rat, a beetle, a bat and two crows (a plain flier and a fuller one). Each faces +Z
    /// with its feet (or middle, for fliers) at the pivot. Parts that move are named: "Head", "Leg0".."Leg3",
    /// "WingL"/"WingR".
    /// </summary>
    public static class AnimalModels
    {
        static Material mothWhite;
        static Material brown, grey, black, dark, eyes, owlBrown, owlCream, owlWing, owlDisc, owlDark, owlFeet, owlBeak;

        static void Mats()
        {
            if (brown != null) return;
            brown = RuntimeMaterials.Lit("Animal brown", new Color(0.16f, 0.11f, 0.07f), 0.25f);
            grey = RuntimeMaterials.Lit("Animal grey", new Color(0.13f, 0.12f, 0.12f), 0.3f);
            black = RuntimeMaterials.Lit("Animal black", new Color(0.02f, 0.02f, 0.02f), 0.4f);
            dark = RuntimeMaterials.Lit("Beetle shell", new Color(0.03f, 0.035f, 0.03f), 0.85f);
            eyes = RuntimeMaterials.Glow("Owl eyes", new Color(0.9f, 0.95f, 1f) * 3f);
            owlBrown = RuntimeMaterials.Lit("Owl feathers", new Color(0.24f, 0.17f, 0.11f), 0.15f);
            owlCream = RuntimeMaterials.Lit("Owl front", new Color(0.5f, 0.43f, 0.33f), 0.12f);
            owlWing = RuntimeMaterials.Lit("Owl wings", new Color(0.17f, 0.12f, 0.08f), 0.15f);
            owlDisc = RuntimeMaterials.Lit("Owl face", new Color(0.58f, 0.52f, 0.43f), 0.1f);
            owlDark = RuntimeMaterials.Lit("Owl markings", new Color(0.09f, 0.065f, 0.045f), 0.12f);
            owlFeet = RuntimeMaterials.Lit("Owl feet", new Color(0.35f, 0.3f, 0.18f), 0.3f);
            owlBeak = RuntimeMaterials.Lit("Owl beak", new Color(0.3f, 0.27f, 0.2f), 0.5f);
        }

        /// <summary>
        /// An owl, small and round: a plump, egg-shaped body in warm brown with a speckled cream front and folded
        /// wings, a big round head with a pale heart-shaped face, ear tufts, a little hooked beak, the big white
        /// eyes, and toes gripping its branch. Feet at the pivot; its head is "Head".
        /// </summary>
        public static GameObject Owl(Transform parent)
        {
            Mats();
            var root = Root("Owl", parent);
            var t = root;
            // The body: an egg, fullest low down, with a pale front and the wings folded over its sides.
            Lofts.Shape(t, "Body", owlBrown, 2f, true, true,
                (new Vector3(0f, 0.015f, -0.01f), new Vector2(0.03f, 0.03f)),
                (new Vector3(0f, 0.05f, 0f), new Vector2(0.085f, 0.08f)),
                (new Vector3(0f, 0.12f, 0.005f), new Vector2(0.105f, 0.1f)),
                (new Vector3(0f, 0.2f, 0f), new Vector2(0.095f, 0.09f)),
                (new Vector3(0f, 0.26f, -0.005f), new Vector2(0.07f, 0.065f)));
            Lofts.Shape(t, "Front", owlCream, 2f, true, true,
                (new Vector3(0f, 0.04f, 0.045f), new Vector2(0.03f, 0.02f)),
                (new Vector3(0f, 0.1f, 0.07f), new Vector2(0.07f, 0.04f)),
                (new Vector3(0f, 0.18f, 0.065f), new Vector2(0.065f, 0.035f)),
                (new Vector3(0f, 0.24f, 0.05f), new Vector2(0.035f, 0.02f)));
            for (int k = 0; k < 9; k++) // (speckles on its front)
            {
                float x = ((k % 3) - 1) * 0.026f + (k / 3 % 2) * 0.013f, y = 0.08f + (k / 3) * 0.045f;
                Part(PrimitiveType.Sphere, t, new Vector3(x, y, 0.105f - Mathf.Abs(x) * 0.4f), new Vector3(0.012f, 0.008f, 0.004f), owlDark);
            }
            for (int side = -1; side <= 1; side += 2)
            {
                var wing = Lofts.Shape(t, "Wing", owlWing, 2.4f, true, true,
                    (new Vector3(side * 0.07f, 0.24f, -0.01f), new Vector2(0.015f, 0.04f)),
                    (new Vector3(side * 0.095f, 0.16f, -0.015f), new Vector2(0.022f, 0.07f)),
                    (new Vector3(side * 0.09f, 0.07f, -0.03f), new Vector2(0.018f, 0.06f)),
                    (new Vector3(side * 0.06f, 0.01f, -0.06f), new Vector2(0.008f, 0.03f)));
                wing.transform.localRotation = Quaternion.identity;
                // Toes, gripping (three forward, curled over the branch).
                for (int toe = -1; toe <= 1; toe++)
                {
                    var tPart = Part(PrimitiveType.Capsule, t, new Vector3(side * 0.03f + toe * 0.01f, 0.004f, 0.025f), new Vector3(0.008f, 0.014f, 0.008f), owlFeet);
                    tPart.transform.localRotation = Quaternion.Euler(70f, toe * 12f, 0f);
                }
            }
            Lofts.Shape(t, "Tail", owlWing, 2.5f, true, true,
                (new Vector3(0f, 0.04f, -0.07f), new Vector2(0.035f, 0.01f)),
                (new Vector3(0f, -0.005f, -0.1f), new Vector2(0.03f, 0.008f)));

            // The head: big and round, a pale heart-shaped face, ear tufts, a little beak, the white eyes.
            var head = Joint(root, "Head", new Vector3(0f, 0.3f, 0f));
            Lofts.Shape(head, "Skull", owlBrown, 2f, true, true,
                (new Vector3(0f, -0.06f, -0.01f), new Vector2(0.06f, 0.05f)),
                (new Vector3(0f, -0.03f, 0f), new Vector2(0.095f, 0.085f)),
                (new Vector3(0f, 0.03f, 0f), new Vector2(0.1f, 0.088f)),
                (new Vector3(0f, 0.07f, -0.005f), new Vector2(0.08f, 0.07f)),
                (new Vector3(0f, 0.09f, -0.01f), new Vector2(0.03f, 0.03f)));
            for (int side = -1; side <= 1; side += 2)
            {
                // Each half of the face: a shallow pale dish round the eye, with a darker rim.
                Part(PrimitiveType.Sphere, head, new Vector3(side * 0.042f, 0.0f, 0.072f), new Vector3(0.085f, 0.095f, 0.03f), owlDisc);
                Part(PrimitiveType.Sphere, head, new Vector3(side * 0.045f, 0.0f, 0.068f), new Vector3(0.095f, 0.105f, 0.026f), owlDark);
                Part(PrimitiveType.Sphere, head, new Vector3(side * 0.04f, 0.005f, 0.088f), Vector3.one * 0.04f, eyes); // (the white eyes)
                var tuft = Lofts.Shape(head, "Tuft", owlWing, 2.2f, true, true,
                    (new Vector3(side * 0.055f, 0.055f, 0.01f), new Vector2(0.018f, 0.01f)),
                    (new Vector3(side * 0.075f, 0.1f, 0f), new Vector2(0.01f, 0.006f)),
                    (new Vector3(side * 0.085f, 0.125f, -0.01f), new Vector2(0.002f, 0.002f)));
                tuft.transform.localRotation = Quaternion.identity;
            }
            // Brows sweeping up between the eyes, and the beak.
            Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.03f, 0.085f), new Vector3(0.03f, 0.04f, 0.02f), owlCream);
            var beak = Part(PrimitiveType.Capsule, head, new Vector3(0f, -0.015f, 0.098f), new Vector3(0.014f, 0.018f, 0.014f), owlBeak);
            beak.transform.localRotation = Quaternion.Euler(25f, 0f, 0f);
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

        /// <summary>
        /// A crow, all black: a body, a head with a thick beak, a fanned tail, little legs, and wings in two parts
        /// that flap (the inner "WingL"/"WingR", the outer "TipL"/"TipR" under them). Middle at the pivot, about
        /// 0.45 m long and 0.9 m across the wings; its head is "Head".
        /// </summary>
        public static GameObject Crow(Transform parent)
        {
            Mats();
            var root = Root("Crow", parent);
            var body = Part(PrimitiveType.Capsule, root, Vector3.zero, new Vector3(0.1f, 0.15f, 0.1f), black);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            var head = Joint(root, "Head", new Vector3(0f, 0.035f, 0.15f));
            Part(PrimitiveType.Sphere, head, Vector3.zero, new Vector3(0.075f, 0.072f, 0.085f), black);
            var beak = Part(PrimitiveType.Capsule, head, new Vector3(0f, -0.008f, 0.07f), new Vector3(0.024f, 0.035f, 0.022f), black);
            beak.transform.localRotation = Quaternion.Euler(95f, 0f, 0f);
            var tail = Part(PrimitiveType.Cube, root, new Vector3(0f, 0.01f, -0.2f), new Vector3(0.09f, 0.012f, 0.14f), black);
            tail.transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            for (int side = -1; side <= 1; side += 2)
            {
                Part(PrimitiveType.Cylinder, root, new Vector3(side * 0.02f, -0.07f, 0.01f), new Vector3(0.008f, 0.035f, 0.008f), black).name = "Leg";
                var wing = Joint(root, side < 0 ? "WingL" : "WingR", new Vector3(side * 0.04f, 0.02f, 0.02f));
                Part(PrimitiveType.Cube, wing, new Vector3(side * 0.1f, 0f, 0f), new Vector3(0.2f, 0.012f, 0.15f), black);
                var tip = Joint(wing, side < 0 ? "TipL" : "TipR", new Vector3(side * 0.2f, 0f, 0f));
                var outer = Part(PrimitiveType.Cube, tip, new Vector3(side * 0.12f, 0f, -0.02f), new Vector3(0.24f, 0.008f, 0.12f), black);
                outer.transform.localRotation = Quaternion.Euler(0f, side * 8f, 0f); // (swept back a little)
            }
            return root.gameObject;
        }

        /// <summary>
        /// The white moth: a pale furry body and two pairs of broad white wings ("WingL"/"WingR", the hind pair under
        /// them) that flutter, faintly glowing. Middle at the pivot, about 0.15 m across.
        /// </summary>
        public static GameObject WhiteMoth(Transform parent)
        {
            Mats();
            if (mothWhite == null) mothWhite = RuntimeMaterials.LitGlow("White moth", new Color(0.92f, 0.9f, 0.85f), new Color(0.9f, 0.92f, 1f) * 0.35f);
            var root = Root("White moth", parent);
            var body = Part(PrimitiveType.Capsule, root, Vector3.zero, new Vector3(0.018f, 0.03f, 0.018f), mothWhite);
            body.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part(PrimitiveType.Sphere, root, new Vector3(0f, 0.004f, 0.03f), Vector3.one * 0.016f, mothWhite);
            for (int side = -1; side <= 1; side += 2)
            {
                var wing = Joint(root, side < 0 ? "WingL" : "WingR", new Vector3(side * 0.006f, 0.004f, 0f));
                var fore = Part(PrimitiveType.Cube, wing, new Vector3(side * 0.035f, 0f, 0.008f), new Vector3(0.07f, 0.002f, 0.045f), mothWhite);
                fore.transform.localRotation = Quaternion.Euler(0f, side * -18f, 0f);
                var hind = Part(PrimitiveType.Cube, wing, new Vector3(side * 0.026f, -0.001f, -0.018f), new Vector3(0.048f, 0.002f, 0.034f), mothWhite);
                hind.transform.localRotation = Quaternion.Euler(0f, side * 22f, 0f);
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
