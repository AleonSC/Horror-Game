using Game.Rendering;
using UnityEngine;

namespace Game.Entities
{
    /// <summary>
    /// The giant's body: the built-in one (a vast, blocky, gaunt black figure with long fingers, on a jointed frame) or your own
    /// model. Built one unit tall with its feet at the pivot, facing +Z; the giant scales it. Poses: hanging (head
    /// lolled, limbs dangling), falling (into a crouch), landing (rising out of it), walking (a stride phase), and
    /// scanning (the head turning).
    /// </summary>
    public sealed class GiantBody : MonoBehaviour
    {
        Transform hips, chest, neck, head;
        Transform thighL, thighR, shinL, shinR, armL, armR, foreL, foreR, ankleL, ankleR;
        static readonly Vector3 HipsAt = new Vector3(0f, 0.52f, 0f);
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
            hips = Joint(t, HipsAt);
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
                // The hand, turned at the wrist so its palm faces the thigh and the fingers hang in a row along
                // the body (front to back), the thumb forward.
                float side = f == foreL ? -1f : 1f;
                var wrist = Joint(f, new Vector3(0f, -0.2f, 0f));
                wrist.localRotation = Quaternion.Euler(0f, -90f * side, 0f);
                Block(wrist, new Vector3(0f, -0.015f, 0.005f), new Vector3(0.045f, 0.04f, 0.022f)); // palm
                // Long fingers, splayed a little and crooked at the end, curling in toward the leg.
                for (int k = 0; k < 4; k++)
                {
                    float x = (k - 1.5f) * 0.011f;
                    var finger = Joint(wrist, new Vector3(x, -0.035f, 0.006f));
                    finger.localRotation = Quaternion.Euler(-6f, 0f, (k - 1.5f) * 5f * side);
                    float length = k == 0 || k == 3 ? 0.1f : 0.13f;
                    Block(finger, new Vector3(0f, -length * 0.35f, 0f), new Vector3(0.007f, length * 0.7f, 0.008f));
                    var tip = Joint(finger, new Vector3(0f, -length * 0.7f, 0f));
                    tip.localRotation = Quaternion.Euler(-28f, 0f, 0f);
                    Block(tip, new Vector3(0f, -length * 0.15f, 0f), new Vector3(0.006f, length * 0.32f, 0.007f));
                }
                Block(wrist, new Vector3(0.016f * side, -0.025f, 0.016f), new Vector3(0.007f, 0.05f, 0.008f)); // thumb
            }

            thighL = Joint(hips, new Vector3(-0.035f, 0f, 0f));
            thighR = Joint(hips, new Vector3(0.035f, 0f, 0f));
            shinL = Joint(thighL, new Vector3(0f, -0.25f, 0f));
            shinR = Joint(thighR, new Vector3(0f, -0.25f, 0f));
            foreach (var th in new[] { thighL, thighR }) Block(th, new Vector3(0f, -0.125f, 0f), new Vector3(0.05f, 0.25f, 0.055f));
            foreach (var sh in new[] { shinL, shinR })
            {
                Block(sh, new Vector3(0f, -0.125f, 0f), new Vector3(0.042f, 0.25f, 0.045f));
                var ankle = Foot(sh, sh == shinL ? 1f : -1f);
                if (sh == shinL) ankleL = ankle; else ankleR = ankle;
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
            // The collar round its neck, an eye at the back for the chain; and the few links left hanging from it
            // once the chain's snapped.
            var collar = new GameObject("Collar") { layer = Game.Core.Layers.Effects }.transform;
            collar.SetParent(neck, false);
            collar.localPosition = new Vector3(0f, 0.035f, 0f);
            collar.localScale = Vector3.one * 0.03f;
            AddMesh(collar, ChainMeshes.Collar);
            chainAnchor = new GameObject("Chain anchor").transform;
            chainAnchor.SetParent(collar, false);
            chainAnchor.localPosition = new Vector3(0f, 0.45f, -1.2f);
            brokenChain = new GameObject("Broken chain") { layer = Game.Core.Layers.Effects }.transform;
            brokenChain.SetParent(collar, false);
            brokenChain.localPosition = new Vector3(0f, 0.2f, -1.2f);
            brokenChain.localRotation = Quaternion.Euler(180f, 0f, 0f); // (hanging down)
            brokenChain.localScale = Vector3.one * (0.0125f / 0.03f);
            AddMesh(brokenChain, ChainMeshes.Stub);
            brokenChain.gameObject.SetActive(false);
            NeckLocal = new Vector3(0f, 0.86f, -0.03f);
            EyesLocal = new Vector3(0f, 0.95f, 0.055f);
        }

        // A big foot on an ankle joint at the bottom of the shin (so it can stay flat as the leg moves), its sole on
        // the ground: a flat sole, rounded over the top (a domed heel, a high instep sloping down to a wide ball),
        // and five long rounded toes splayed out from it, the big toe on the inside (inward is +x for the left foot,
        // -x for the right), each curling down at its tip. Returns the ankle.
        Transform Foot(Transform shin, float inward)
        {
            var ankle = Joint(shin, new Vector3(0f, -0.25f, 0f));
            const float sole = -0.0225f; // (the ground, below the ankle)
            Shape(PrimitiveType.Sphere, ankle, new Vector3(0f, 0.012f, -0.005f), new Vector3(0.05f, 0.045f, 0.055f));          // ankle
            // The sole: flat underneath (the domes' lower halves sit inside it).
            Block(ankle, new Vector3(inward * 0.002f, sole + 0.008f, 0.02f), new Vector3(0.07f, 0.016f, 0.17f));
            Shape(PrimitiveType.Sphere, ankle, new Vector3(0f, sole + 0.022f, -0.035f), new Vector3(0.054f, 0.044f, 0.06f));    // heel
            var instep = Shape(PrimitiveType.Sphere, ankle, new Vector3(inward * 0.004f, sole + 0.024f, 0.03f), new Vector3(0.064f, 0.05f, 0.12f));
            instep.transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);                                                   // (higher at the back)
            Shape(PrimitiveType.Sphere, ankle, new Vector3(inward * 0.002f, sole + 0.016f, 0.082f), new Vector3(0.076f, 0.032f, 0.05f)); // the ball
            float[] across = { 0.026f, 0.011f, -0.002f, -0.015f, -0.027f };
            float[] length = { 0.046f, 0.043f, 0.039f, 0.034f, 0.028f };
            float[] width = { 0.017f, 0.012f, 0.011f, 0.011f, 0.01f };
            for (int k = 0; k < 5; k++)
            {
                var toe = Joint(ankle, new Vector3(inward * across[k], sole + 0.009f, 0.098f - k * 0.004f));
                toe.localRotation = Quaternion.Euler(0f, -inward * (k - 1f) * 4f, 0f);                                          // splayed
                var bone = Shape(PrimitiveType.Capsule, toe, new Vector3(0f, 0f, length[k] * 0.5f), new Vector3(width[k], length[k] * 0.5f, 0.018f));
                bone.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);                                                   // (lying along the toe)
                var tip = Joint(toe, new Vector3(0f, 0f, length[k] * 0.85f));
                tip.localRotation = Quaternion.Euler(30f, 0f, 0f);                                                              // curled down
                var nail = Shape(PrimitiveType.Capsule, tip, new Vector3(0f, 0f, 0.008f), new Vector3(width[k] * 0.85f, 0.011f, 0.014f));
                nail.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
            return ankle;
        }

        Transform chainAnchor, brokenChain;

        /// <summary>Where its chain holds it (the eye on the back of its collar), or null for your own model.</summary>
        public Transform ChainAnchor => chainAnchor;

        /// <summary>The broken end of the chain hanging from its collar (after it falls), or not.</summary>
        public void ShowBrokenChain(bool shown)
        {
            if (brokenChain == null) return;
            if (brokenChain.gameObject.activeSelf != shown) brokenChain.gameObject.SetActive(shown);
            // (Hanging straight down whatever its neck's doing.)
            if (shown) brokenChain.rotation = Quaternion.LookRotation(transform.forward, Vector3.down);
        }

        void AddMesh(Transform t, Mesh mesh)
        {
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = t.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = haze;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
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

        // Keep a foot flat on the ground whatever the leg above it is doing (by this much: 1 flat, 0 with the leg).
        // (pitch: the hips', thigh's and shin's forward pitches added up.)
        static void Flat(Transform ankle, float pitch, float amount)
        {
            if (ankle != null) ankle.localRotation = Quaternion.Euler(-pitch * amount, 0f, 0f);
        }

        /// <summary>Hanging from the chain: <paramref name="sway"/> -1..1 swings the limbs a little.</summary>
        public void PoseHanging(float sway)
        {
            if (custom) { Animate(0f, true); return; }
            hips.localPosition = HipsAt;
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
            if (ankleL != null) ankleL.localRotation = ankleR.localRotation = Quaternion.Euler(28f, 0f, 0f); // (toes hanging down)
        }

        /// <summary>
        /// Falling: <paramref name="t"/> 0..1 through the fall. It drops out of its hang, arms flung slowly up and
        /// out against the rush of air, the head coming up off its chest; then, nearing the ground, it gathers
        /// itself: knees drawn up and bent, body pitching forward, arms coming down and forward to meet the ground,
        /// ending in the crouch it lands in (<see cref="PoseLanding"/> at 1).
        /// </summary>
        public void PoseFalling(float t)
        {
            if (custom) { Animate(0f, false); return; }
            float up = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / 0.45f));        // arms rising, head coming up
            float flail = Mathf.Sin(t * 5f) * (1f - t) * up;                       // a slow windmill, dying away
            float gather = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.55f) / 0.45f));

            // In the air...
            var hipsAir = Quaternion.Euler(-6f * up, 0f, 0f);
            var chestAir = Quaternion.Euler(-4f * up, 0f, 0f);
            var neckAir = Quaternion.Euler(Mathf.Lerp(-25f, 0f, up), 0f, 0f);
            var headAir = Quaternion.Euler(Mathf.Lerp(55f, -12f, up), 0f, Mathf.Lerp(22f, 0f, up));
            var armLAir = Quaternion.Euler(-35f * up + flail * 18f, 0f, -55f * up);
            var armRAir = Quaternion.Euler(-35f * up - flail * 18f, 0f, 55f * up);
            var foreAir = Quaternion.Euler(-25f * up, 0f, 0f);
            var thighLAir = Quaternion.Euler(-12f * up + flail * 8f, 0f, -6f * up);
            var thighRAir = Quaternion.Euler(8f * up - flail * 8f, 0f, 6f * up);
            var shinLAir = Quaternion.Euler(18f * up, 0f, 0f);
            var shinRAir = Quaternion.Euler(28f * up, 0f, 0f);

            // ...blended into the crouch it lands in.
            PoseLanding(1f);
            hips.localPosition = Vector3.Lerp(HipsAt, hips.localPosition, gather);
            hips.localRotation = Quaternion.Slerp(hipsAir, hips.localRotation, gather);
            chest.localRotation = Quaternion.Slerp(chestAir, chest.localRotation, gather);
            neck.localRotation = Quaternion.Slerp(neckAir, neck.localRotation, gather);
            head.localRotation = Quaternion.Slerp(headAir, head.localRotation, gather);
            armL.localRotation = Quaternion.Slerp(armLAir, armL.localRotation, gather);
            armR.localRotation = Quaternion.Slerp(armRAir, armR.localRotation, gather);
            foreL.localRotation = Quaternion.Slerp(foreAir, foreL.localRotation, gather);
            foreR.localRotation = Quaternion.Slerp(foreAir, foreR.localRotation, gather);
            thighL.localRotation = Quaternion.Slerp(thighLAir, thighL.localRotation, gather);
            thighR.localRotation = Quaternion.Slerp(thighRAir, thighR.localRotation, gather);
            shinL.localRotation = Quaternion.Slerp(shinLAir, shinL.localRotation, gather);
            shinR.localRotation = Quaternion.Slerp(shinRAir, shinR.localRotation, gather);
            // Toes pointing down as it drops, flattening for the ground.
            if (ankleL != null)
            {
                var hang = Quaternion.Euler(28f * (1f - up * 0.5f), 0f, 0f);
                ankleL.localRotation = Quaternion.Slerp(hang, ankleL.localRotation, gather);
                ankleR.localRotation = Quaternion.Slerp(hang, ankleR.localRotation, gather);
            }
        }

        /// <summary>
        /// Landed: <paramref name="crouch"/> 1 deep in the crouch it hit the ground in (knees bent, hips low, body
        /// pitched over, long arms down with the fingers on the ground), 0 risen to stand as it walks. Feet flat.
        /// </summary>
        public void PoseLanding(float crouch)
        {
            if (custom) { Animate(0f, false); return; }
            float c = Mathf.Clamp(crouch, 0f, 1.3f);
            float hipsPitch = 10f + 22f * c;
            // The legs fold like a Z: the thigh swings forward by bend, the shin back twice as far, so the foot stays
            // under the hips; the hips drop by what the folding takes up. (At 0 it's the walk's standing pose.)
            float bend = 50f * c;
            float thigh = bend + hipsPitch - 10f, knee = 4f + 2f * bend;
            float Height(float b) => 0.25f * Mathf.Cos((b - 10f) * Mathf.Deg2Rad) + 0.25f * Mathf.Cos((b + 14f) * Mathf.Deg2Rad);
            hips.localPosition = HipsAt + Vector3.down * (Height(0f) - Height(bend));
            hips.localRotation = Quaternion.Euler(hipsPitch, 0f, 0f);
            chest.localRotation = Quaternion.Euler(14f + 20f * c, 0f, 0f);
            neck.localRotation = Quaternion.Euler(18f - 30f * c, 0f, 0f);       // head up, looking out from its crouch
            head.localRotation = Quaternion.Euler(-10f - 15f * c, 0f, 0f);
            thighL.localRotation = Quaternion.Euler(-thigh, 0f, -2f - 5f * c);
            thighR.localRotation = Quaternion.Euler(-thigh, 0f, 2f + 5f * c);
            shinL.localRotation = shinR.localRotation = Quaternion.Euler(knee, 0f, 0f);
            armL.localRotation = Quaternion.Euler(-6f - 38f * c, 0f, -5f - 6f * c);
            armR.localRotation = Quaternion.Euler(-6f - 34f * c, 0f, 5f + 6f * c);
            foreL.localRotation = foreR.localRotation = Quaternion.Euler(-12f - 10f * c, 0f, 0f);
            Flat(ankleL, hipsPitch - thigh + knee, 1f);
            Flat(ankleR, hipsPitch - thigh + knee, 1f);
        }

        /// <summary>
        /// Walking: <paramref name="phase"/> in strides (each whole number is one stride: two steps), <paramref name="scan"/>
        /// the head turning to look round (-1..1), <paramref name="moving"/> 0 standing to 1 striding.
        /// </summary>
        public void PoseWalking(float phase, float scan, float moving)
        {
            if (custom) { Animate(moving, false); return; }
            // (Left foot strikes at a quarter of a stride, the right at three quarters.)
            float a = phase * Mathf.PI * 2f;
            float sn = Mathf.Sin(a), cs = Mathf.Cos(a);
            float swing = sn * 30f * moving;
            // Heavy: the whole body sinks as each foot lands and rises over the planted leg, rolling from side to
            // side onto it, the head nodding with each footfall. (Big enough to read from miles off.)
            float dip = Mathf.Abs(sn);
            float strike = Mathf.Pow(dip, 6f);
            hips.localPosition = HipsAt + new Vector3(cs * 0.012f, -0.026f * dip, 0f) * moving; // (over the planted foot: the left is at -x)
            hips.localRotation = Quaternion.Euler(10f + 3f * strike * moving, sn * 6f * moving, -cs * 4f * moving);
            chest.localRotation = Quaternion.Euler(14f + 2f * strike * moving, -sn * 9f * moving, cs * 2f * moving);    // hunched, swaying
            neck.localRotation = Quaternion.Euler(18f + 6f * strike * moving, 0f, 0f);                                   // jutting forward, nodding
            head.localRotation = Quaternion.Euler(-10f - 4f * strike * moving, scan * 55f - sn * 4f * moving, scan * 8f);
            thighL.localRotation = Quaternion.Euler(-swing, 0f, -2f);
            thighR.localRotation = Quaternion.Euler(swing, 0f, 2f);
            // The knee bends as the leg swings through, straight again as the foot comes down.
            float kneeL = Mathf.Max(0f, cs) * 55f * moving + 4f;
            float kneeR = Mathf.Max(0f, -cs) * 55f * moving + 4f;
            shinL.localRotation = Quaternion.Euler(kneeL, 0f, 0f);
            shinR.localRotation = Quaternion.Euler(kneeR, 0f, 2f);
            // The long arms swinging against the legs, the elbows bending as they come forward.
            armL.localRotation = Quaternion.Euler(swing * 0.9f - 6f, 0f, -5f - 3f * dip * moving);
            armR.localRotation = Quaternion.Euler(-swing * 0.9f - 6f, 0f, 5f + 3f * dip * moving);
            foreL.localRotation = Quaternion.Euler(-12f - Mathf.Max(0f, -sn) * 25f * moving, 0f, 0f);
            foreR.localRotation = Quaternion.Euler(-12f - Mathf.Max(0f, sn) * 25f * moving, 0f, 0f);
            // Feet: flat on the ground under it, the lifted one hanging a little toe-down.
            Flat(ankleL, 10f - swing + kneeL, 0.85f);
            Flat(ankleR, 10f + swing + kneeR, 0.85f);
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
