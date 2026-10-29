using Game.Rendering;
using UnityEngine;

namespace Game.Entities
{
    /// <summary>
    /// The flier's body: the built-in one (the HDRP build's vast ragged black bird: a crooked neck, a hooked beak
    /// and one great eye, wings of long separate feathers, a fanned tail, talons trailing) or your own model. One unit across the wings, facing +Z. Its wings beat or glide.
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
            b.skin = RuntimeMaterials.Silhouette("Flier");
            if (config.customModel != null) b.UseYours(config.customModel);
            else b.Build();
            return b;
        }

        void OnDestroy()
        {
            if (skin != null) Destroy(skin);
        }

        public void SetShade(float shade) => RuntimeMaterials.SetSilhouetteShade(skin, shade);

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

        // The bird from the HDRP build (FlyingEntity), part for part: a heavy body, a long crooked neck, a narrow
        // head with a long hooked beak and one great eye, wings of an arm and a hand with long separate flight
        // feathers hanging off the arm and splayed like fingers at the tip, a ragged fanned tail, and legs with
        // hooked talons trailing under it. All a silhouette against the sky. (Its parts were in metres for a bird
        // about 48 m across; here they're scaled to one unit across, and the flier scales it to its wingspan.)
        const float K = 1f / 47.8f;
        static Vector3 M(float x, float y, float z) => new Vector3(x, y, z) * K;

        void Build()
        {
            var rng = new System.Random(11);
            float R(float a, float b) => a + (b - a) * (float)rng.NextDouble();
            var t = transform;
            bodyPivot = Point(t, Vector3.zero);
            Shape(PrimitiveType.Sphere, bodyPivot, M(0f, 0f, 0f), M(3.4f, 2.6f, 9f), Quaternion.identity);
            Shape(PrimitiveType.Sphere, bodyPivot, M(0f, 0.4f, 2.6f), M(3.6f, 2.8f, 4.5f), Quaternion.identity); // chest

            // The neck: crooked, reaching forward and a little down.
            var neck = Point(bodyPivot, M(0f, 0.4f, 4.2f));
            Capsule(neck, Vector3.zero, M(0f, -0.4f, 2.6f), 1.3f * K);
            Capsule(neck, M(0f, -0.4f, 2.6f), M(0f, -0.2f, 5f), 1f * K);
            head = Point(neck, M(0f, -0.2f, 5.2f));
            Shape(PrimitiveType.Sphere, head, M(0f, 0f, 0.4f), M(1.6f, 1.5f, 2.8f), Quaternion.identity);
            // The beak: long, narrow, hooked down at the tip.
            Capsule(head, M(0f, -0.1f, 1.5f), M(0f, -0.4f, 4.6f), 0.5f * K);
            Capsule(head, M(0f, -0.4f, 4.6f), M(0f, -1.1f, 5.1f), 0.25f * K);
            // One great eye under the brow, looking down (it burns when its searchlight's on).
            Eye = Point(head, M(0f, -0.5f, 1.1f));
            eyeGlow = Effects.Glow(Eye, Vector3.zero, 2.2f * K, Color.black, 0.15f, 2.5f, "Flier eye");

            // Wings: an arm and a hand.
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                var shoulder = Point(bodyPivot, M(side * 1.4f, 0.8f, 1.6f));
                Shape(PrimitiveType.Cube, shoulder, M(side * 4.2f, 0f, -0.6f), M(8.4f, 0.45f, 3.6f), Quaternion.identity);
                for (int f = 0; f < 6; f++) // secondaries, hanging back off the arm
                    Shape(PrimitiveType.Cube, shoulder, M(side * (1.2f + f * 1.25f), -0.05f, -3.4f + R(-0.3f, 0.3f)),
                        M(1.1f, 0.12f, 3.6f + R(-0.6f, 0.6f)), Quaternion.Euler(0f, side * R(-6f, 6f), 0f));
                var elbow = Point(shoulder, M(side * 8.2f, 0f, -0.4f));
                Shape(PrimitiveType.Cube, elbow, M(side * 3f, 0f, -0.3f), M(6.2f, 0.35f, 2.6f), Quaternion.Euler(0f, side * 8f, 0f));
                for (int f = 0; f < 7; f++)
                {
                    // Primaries: long fingers fanning out from the tip, ragged and uneven.
                    float a = Mathf.Lerp(-5f, 70f, f / 6f);
                    float len = R(6f, 8.5f) * (1f - f * 0.06f);
                    var feather = Point(elbow, M(side * 5.8f, 0f, -0.6f - f * 0.2f));
                    feather.localRotation = Quaternion.Euler(0f, side * a, 0f);
                    Shape(PrimitiveType.Cube, feather, M(side * len * 0.5f, 0f, 0f), M(len, 0.1f, R(0.6f, 0.9f)), Quaternion.Euler(0f, 0f, side * R(-3f, 3f)));
                }
                if (i == 0) { wingL = shoulder; tipL = elbow; } else { wingR = shoulder; tipR = elbow; }
            }

            // The tail: a ragged fan.
            for (int f = 0; f < 7; f++)
            {
                var feather = Point(bodyPivot, M(0f, 0.2f, -4f));
                feather.localRotation = Quaternion.Euler(0f, Mathf.Lerp(-30f, 30f, f / 6f), 0f);
                Shape(PrimitiveType.Cube, feather, M(0f, 0f, -3f), M(0.9f, 0.1f, R(5f, 6.5f)), Quaternion.identity);
            }

            // Legs and talons, trailing.
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                var leg = Point(bodyPivot, M(side * 0.9f, -1f, -1f));
                Capsule(leg, Vector3.zero, M(0f, -3f, 0f), 0.45f * K);
                for (int k = -1; k <= 1; k++)
                {
                    Vector3 knuckle = M(k * 0.3f, -3f, 0f);
                    Vector3 mid = knuckle + M(k * 0.4f, -0.6f, 1.1f);
                    Capsule(leg, knuckle, mid, 0.18f * K);
                    Capsule(leg, mid, knuckle + M(k * 0.5f, -1.3f, 1.2f), 0.12f * K);
                }
                leg.localRotation = Quaternion.Euler(55f, 0f, 0f);
                if (i == 0) legL = leg; else legR = leg;
            }
        }

        Transform bodyPivot, legL, legR;
        MeshRenderer eyeGlow;

        /// <summary>Its eye's glow (black: dark).</summary>
        public void SetEyeGlow(Color glow)
        {
            if (eyeGlow != null) Effects.SetGlow(eyeGlow, glow);
        }

        // A capsule from a to b, this thick.
        void Capsule(Transform parent, Vector3 a, Vector3 b, float thickness)
        {
            Vector3 d = b - a;
            if (d.sqrMagnitude < 1e-10f) return;
            Shape(PrimitiveType.Capsule, parent, (a + b) * 0.5f, new Vector3(thickness, d.magnitude * 0.5f + thickness * 0.5f, thickness), Quaternion.FromToRotation(Vector3.up, d));
        }

        /// <summary><paramref name="flap"/> 0 gliding, 1 beating hard; <paramref name="bank"/> -1..1 (it banks into turns).</summary>
        public void Pose(float flap, float bank, float dt)
        {
            if (animator != null) animator.SetFloat("Flap", flap);
            if (wingL == null) return;
            // (As the HDRP build: slow, heavy beats, the hand lagging the arm, the body heaving.)
            phase += dt * Mathf.Lerp(0.6f, 1.6f, flap) * Mathf.PI * 2f * 0.35f;
            float s = Mathf.Sin(phase);
            float amp = Mathf.Lerp(4f, 38f, flap);
            float lag = Mathf.Sin(phase - 0.9f);
            float follow = 1f - Mathf.Exp(-8f * dt);
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                var shoulder = i == 0 ? wingL : wingR;
                var elbow = i == 0 ? tipL : tipR;
                shoulder.localRotation = Quaternion.Slerp(shoulder.localRotation, Quaternion.Euler(0f, side * -6f, side * (s * amp + 6f)), follow);
                elbow.localRotation = Quaternion.Slerp(elbow.localRotation, Quaternion.Euler(0f, side * -8f, side * (lag * amp * 0.7f - 4f)), follow);
            }
            if (bodyPivot != null) bodyPivot.localPosition = Vector3.up * (-s * amp * 0.03f * K);
            transform.localRotation = Quaternion.Euler(0f, 0f, -bank * 25f);
            if (head != null) head.localRotation = Quaternion.Slerp(head.localRotation, Quaternion.Euler(10f + Mathf.Sin(Time.time * 0.5f) * 6f, Mathf.Sin(Time.time * 0.37f) * 15f, 0f), 1f - Mathf.Exp(-4f * dt));
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
