using Game.Rendering;
using UnityEngine;

namespace Game.Entities
{
    /// <summary>
    /// The giant's body: the built-in one (a vast, blocky, gaunt black figure with long fingers, on a jointed frame) or your own
    /// model. Built one unit tall with its feet at the pivot, facing +Z; the giant scales it. Poses: hanging (head
    /// lolled, limbs dangling), falling, walking (a stride phase), and scanning (the head turning).
    /// </summary>
    public sealed class GiantBody : MonoBehaviour
    {
        Transform hips, chest, neck, head;
        Transform thighL, thighR, shinL, shinR, armL, armR, foreL, foreR;
        Animator animator;
        Material haze;
        bool custom;

        /// <summary>Where the chain holds it (the back of the neck), in its own space.</summary>
        public Vector3 NeckLocal { get; private set; } = new Vector3(0f, 0.86f, -0.02f);
        /// <summary>Its eyes, in its own space (the searchlights come from here).</summary>
        public Vector3 EyesLocal { get; private set; } = new Vector3(0f, 0.93f, 0.05f);

        public static GiantBody Create(Transform parent, GiantConfig config)
        {
            var go = new GameObject("Giant body") { layer = Game.Core.Layers.Effects };
            go.transform.SetParent(parent, false);
            var body = go.AddComponent<GiantBody>();
            body.haze = RuntimeMaterials.Silhouette("Giant");
            body.eyeMat = RuntimeMaterials.Haze("The giant's own eyes", Color.black);
            RuntimeMaterials.DrawAfter(body.eyeMat, 3); // (over its black face)
            if (config.customModel != null) body.UseYours(config.customModel);
            else body.BuildBuiltIn();
            return body;
        }

        public Material Material => haze;

        void OnDestroy()
        {
            if (haze != null) Destroy(haze);
            if (eyeMat != null) Destroy(eyeMat);
        }

        void UseYours(GameObject prefab)
        {
            custom = true;
            var model = Instantiate(prefab, transform, false);
            foreach (var c in model.GetComponentsInChildren<Collider>()) Destroy(c);
            // Scaled to one unit tall, so the giant's Height sets its size.
            var renderers = model.GetComponentsInChildren<Renderer>();
            if (renderers.Length > 0)
            {
                var b = renderers[0].bounds;
                foreach (var r in renderers) b.Encapsulate(r.bounds);
                if (b.size.y > 0.001f) model.transform.localScale = model.transform.localScale / b.size.y;
                // Your materials stay, but the fog would hide it: it's drawn in the haze unless you say otherwise.
                foreach (var r in renderers) r.sharedMaterial = haze;
            }
            animator = model.GetComponentInChildren<Animator>();
        }

        // Blocky but gaunt: narrow square slabs for a body, thin square limbs, hands that end in long, crooked
        // fingers, big long-toed feet, a cube of a head under a heavy brow. One unit tall, feet at the pivot.
        void BuildBuiltIn()
        {
            var t = transform;
            hips = Joint(t, new Vector3(0f, 0.52f, 0f));
            Block(hips, new Vector3(0f, 0.01f, 0f), new Vector3(0.12f, 0.1f, 0.07f));           // pelvis
            Block(hips, new Vector3(0f, 0.12f, 0.005f), new Vector3(0.09f, 0.13f, 0.06f));      // a narrow waist
            chest = Joint(hips, new Vector3(0f, 0.2f, 0f));
            Block(chest, new Vector3(0f, 0.06f, 0f), new Vector3(0.16f, 0.16f, 0.09f));         // ribcage
            Block(chest, new Vector3(0f, 0.125f, -0.005f), new Vector3(0.21f, 0.045f, 0.08f));  // shoulders
            Block(chest, new Vector3(0f, 0.155f, -0.015f), new Vector3(0.08f, 0.04f, 0.06f));   // the hump of the back of the neck
            neck = Joint(chest, new Vector3(0f, 0.14f, 0.005f));
            Block(neck, new Vector3(0f, 0.03f, 0f), new Vector3(0.04f, 0.075f, 0.04f));
            head = Joint(neck, new Vector3(0f, 0.07f, 0f));
            var skull = Block(head, new Vector3(0f, 0.045f, 0.01f), new Vector3(0.075f, 0.1f, 0.08f));
            skull.name = "Head";
            Block(head, new Vector3(0f, 0.07f, 0.048f), new Vector3(0.085f, 0.02f, 0.025f));    // brow
            Block(head, new Vector3(0f, 0.0f, 0.025f), new Vector3(0.06f, 0.03f, 0.06f));       // jaw

            armL = Joint(chest, new Vector3(-0.11f, 0.115f, 0f));
            armR = Joint(chest, new Vector3(0.11f, 0.115f, 0f));
            foreL = Joint(armL, new Vector3(0f, -0.2f, 0f));
            foreR = Joint(armR, new Vector3(0f, -0.2f, 0f));
            foreach (var a in new[] { armL, armR })
            {
                Block(a, new Vector3(0f, -0.005f, 0f), new Vector3(0.05f, 0.05f, 0.055f));     // shoulder
                Block(a, new Vector3(0f, -0.1f, 0f), new Vector3(0.036f, 0.2f, 0.04f));        // upper arm
            }
            foreach (var f in new[] { foreL, foreR })
            {
                Block(f, new Vector3(0f, -0.1f, 0f), new Vector3(0.032f, 0.2f, 0.036f));       // forearm
                Block(f, new Vector3(0f, -0.215f, 0.005f), new Vector3(0.045f, 0.04f, 0.022f)); // palm
                // Long fingers, splayed a little and crooked at the end.
                float side = f == foreL ? -1f : 1f;
                for (int k = 0; k < 4; k++)
                {
                    float x = (k - 1.5f) * 0.011f;
                    var finger = Joint(f, new Vector3(x, -0.235f, 0.006f));
                    finger.localRotation = Quaternion.Euler(-6f, 0f, (k - 1.5f) * 5f * side);
                    float length = k == 0 || k == 3 ? 0.1f : 0.13f;
                    Block(finger, new Vector3(0f, -length * 0.35f, 0f), new Vector3(0.007f, length * 0.7f, 0.008f));
                    var tip = Joint(finger, new Vector3(0f, -length * 0.7f, 0f));
                    tip.localRotation = Quaternion.Euler(-28f, 0f, 0f);
                    Block(tip, new Vector3(0f, -length * 0.15f, 0f), new Vector3(0.006f, length * 0.32f, 0.007f));
                }
                Block(f, new Vector3(0.016f * side, -0.225f, 0.016f), new Vector3(0.007f, 0.05f, 0.008f)); // thumb
            }

            thighL = Joint(hips, new Vector3(-0.035f, 0f, 0f));
            thighR = Joint(hips, new Vector3(0.035f, 0f, 0f));
            shinL = Joint(thighL, new Vector3(0f, -0.25f, 0f));
            shinR = Joint(thighR, new Vector3(0f, -0.25f, 0f));
            foreach (var th in new[] { thighL, thighR }) Block(th, new Vector3(0f, -0.125f, 0f), new Vector3(0.05f, 0.25f, 0.055f));
            foreach (var sh in new[] { shinL, shinR })
            {
                Block(sh, new Vector3(0f, -0.125f, 0f), new Vector3(0.042f, 0.25f, 0.045f));
                Foot(sh, sh == shinL ? 1f : -1f);
            }

            // The eyes: shut until it wakes (see SetEyes).
            eyes = new Transform[2];
            for (int e = 0; e < 2; e++)
            {
                var eye = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(eye.GetComponent<Collider>());
                eye.name = "Eye";
                eye.layer = Game.Core.Layers.Effects;
                eye.transform.SetParent(head, false);
                eye.transform.localPosition = new Vector3((e == 0 ? -1f : 1f) * 0.018f, 0.052f, 0.051f);
                eye.transform.localScale = new Vector3(0.016f, 0f, 0.004f);
                var r = eye.GetComponent<Renderer>();
                r.sharedMaterial = eyeMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                eyes[e] = eye.transform;
            }
            NeckLocal = new Vector3(0f, 0.86f, -0.03f);
            EyesLocal = new Vector3(0f, 0.95f, 0.055f);
        }

        // A big foot under the shin (its sole on the ground, 0.2725 below the knee joint), still in blocks: an ankle,
        // a heel, an arch sloping down to a wide ball, and five long toes splayed out from it, the big toe on the
        // inside (inward is +x for the left foot, -x for the right), each curling down at its tip.
        void Foot(Transform shin, float inward)
        {
            const float sole = -0.2725f;
            Block(shin, new Vector3(0f, -0.235f, -0.005f), new Vector3(0.046f, 0.035f, 0.05f));            // ankle
            Block(shin, new Vector3(0f, sole + 0.021f, -0.035f), new Vector3(0.05f, 0.042f, 0.05f));        // heel
            var arch = Block(shin, new Vector3(inward * 0.004f, sole + 0.02f, 0.03f), new Vector3(0.06f, 0.034f, 0.085f));
            arch.transform.localRotation = Quaternion.Euler(10f, 0f, 0f);                                   // (higher at the back)
            Block(shin, new Vector3(inward * 0.002f, sole + 0.014f, 0.08f), new Vector3(0.074f, 0.028f, 0.04f)); // the ball
            float[] across = { 0.026f, 0.011f, -0.002f, -0.015f, -0.027f };
            float[] length = { 0.046f, 0.043f, 0.039f, 0.034f, 0.028f };
            float[] width = { 0.017f, 0.012f, 0.011f, 0.011f, 0.01f };
            for (int k = 0; k < 5; k++)
            {
                var toe = Joint(shin, new Vector3(inward * across[k], sole + 0.01f, 0.098f - k * 0.004f));
                toe.localRotation = Quaternion.Euler(0f, -inward * (k - 1f) * 4f, 0f);                     // splayed
                Block(toe, new Vector3(0f, 0f, length[k] * 0.5f), new Vector3(width[k], 0.019f, length[k]));
                var tip = Joint(toe, new Vector3(0f, 0f, length[k]));
                tip.localRotation = Quaternion.Euler(30f, 0f, 0f);                                          // curled down
                Block(tip, new Vector3(0f, 0f, 0.007f), new Vector3(width[k] * 0.85f, 0.015f, 0.014f));
            }
        }

        Transform[] eyes = new Transform[0];
        Material eyeMat;

        /// <summary>Its own eyes (once it wakes): <paramref name="open"/> 0 shut to 1 wide, glowing this colour.</summary>
        public void SetEyes(float open, Color glow)
        {
            foreach (var e in eyes) if (e != null) e.localScale = new Vector3(0.016f, 0.009f * Mathf.Clamp01(open), 0.004f);
            if (eyeMat != null) RuntimeMaterials.SetHazeColor(eyeMat, glow * Mathf.Clamp01(open));
        }

        /// <summary>The head, for things that look where it looks (its search ray).</summary>
        public Transform HeadJoint => head;

        GameObject Block(Transform parent, Vector3 at, Vector3 size) => Shape(PrimitiveType.Cube, parent, at, size);

        /// <summary>The fog's colour (as the look shows it) taken by this much: it stays a silhouette, just hazed.</summary>
        public void SetShade(float shade, Color fog, float through) => RuntimeMaterials.SetSilhouette(haze, shade, fog, through);

        /// <summary>Hanging from the chain: <paramref name="sway"/> -1..1 swings the limbs a little.</summary>
        public void PoseHanging(float sway)
        {
            if (custom) { Animate(0f, true); return; }
            hips.localRotation = Quaternion.Euler(4f, 0f, sway * 2f);
            chest.localRotation = Quaternion.Euler(6f, 0f, 0f);
            neck.localRotation = Quaternion.Euler(-25f, 0f, 0f);                // (held back by the chain)
            head.localRotation = Quaternion.Euler(55f, 0f, 22f);                // lolled
            armL.localRotation = Quaternion.Euler(sway * 3f, 0f, -7f);
            armR.localRotation = Quaternion.Euler(-sway * 3f, 0f, 7f);
            foreL.localRotation = foreR.localRotation = Quaternion.Euler(-4f, 0f, 0f);
            thighL.localRotation = Quaternion.Euler(sway * 4f, 0f, -2f);
            thighR.localRotation = Quaternion.Euler(-sway * 4f + 3f, 0f, 2f);
            shinL.localRotation = shinR.localRotation = Quaternion.Euler(2f, 0f, 0f);
        }

        /// <summary>Falling: flailing slowly, <paramref name="t"/> 0..1 through the fall.</summary>
        public void PoseFalling(float t)
        {
            if (custom) { Animate(0f, false); return; }
            float flail = Mathf.Sin(t * 9f);
            hips.localRotation = Quaternion.Euler(-8f * t, 0f, 0f);
            head.localRotation = Quaternion.Euler(Mathf.Lerp(55f, -15f, t), 0f, Mathf.Lerp(22f, 0f, t));
            neck.localRotation = Quaternion.identity;
            armL.localRotation = Quaternion.Euler(flail * 20f, 0f, -40f * t);
            armR.localRotation = Quaternion.Euler(-flail * 20f, 0f, 40f * t);
            thighL.localRotation = Quaternion.Euler(-20f * t, 0f, -5f);
            thighR.localRotation = Quaternion.Euler(10f * t, 0f, 5f);
            shinL.localRotation = Quaternion.Euler(30f * t, 0f, 0f);
            shinR.localRotation = Quaternion.Euler(15f * t, 0f, 0f);
        }

        /// <summary>
        /// Walking: <paramref name="phase"/> in strides (each whole number is one stride: two steps), <paramref name="scan"/>
        /// the head turning to look round (-1..1), <paramref name="moving"/> 0 standing to 1 striding.
        /// </summary>
        public void PoseWalking(float phase, float scan, float moving)
        {
            if (custom) { Animate(moving, false); return; }
            float a = phase * Mathf.PI * 2f;
            float swing = Mathf.Sin(a) * 26f * moving;
            hips.localRotation = Quaternion.Euler(10f, Mathf.Sin(a) * 4f * moving, 0f);
            chest.localRotation = Quaternion.Euler(14f, -Mathf.Sin(a) * 6f * moving, 0f);      // hunched
            neck.localRotation = Quaternion.Euler(18f, 0f, 0f);                                // jutting forward
            head.localRotation = Quaternion.Euler(-10f, scan * 55f, scan * 8f);
            thighL.localRotation = Quaternion.Euler(-swing, 0f, -2f);
            thighR.localRotation = Quaternion.Euler(swing, 0f, 2f);
            shinL.localRotation = Quaternion.Euler(Mathf.Max(0f, Mathf.Sin(a + 1.2f)) * 45f * moving + 4f, 0f, 0f);
            shinR.localRotation = Quaternion.Euler(Mathf.Max(0f, Mathf.Sin(a + 1.2f + Mathf.PI)) * 45f * moving + 4f, 0f, 0f);
            armL.localRotation = Quaternion.Euler(swing * 0.6f - 6f, 0f, -5f);
            armR.localRotation = Quaternion.Euler(-swing * 0.6f - 6f, 0f, 5f);
            foreL.localRotation = foreR.localRotation = Quaternion.Euler(-12f, 0f, 0f);
        }

        void Animate(float walk, bool hanging)
        {
            if (animator == null) return;
            animator.SetFloat("Walk", walk);
            animator.SetBool("Hanging", hanging);
        }

        // ------------------------------------------------------------------ building

        Transform Joint(Transform parent, Vector3 at)
        {
            var j = new GameObject("Joint").transform;
            j.SetParent(parent, false);
            j.localPosition = at;
            return j;
        }

        GameObject Shape(PrimitiveType type, Transform parent, Vector3 at, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.layer = Game.Core.Layers.Effects;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = haze;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go;
        }
    }
}
