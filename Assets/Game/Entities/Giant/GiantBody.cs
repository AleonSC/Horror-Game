using Game.Rendering;
using UnityEngine;

namespace Game.Entities
{
    /// <summary>
    /// The giant's body: the built-in one (a vast, too-thin figure of hazed shapes on a jointed frame) or your own
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
            body.haze = RuntimeMaterials.Haze("Giant", Color.black);
            if (config.customModel != null) body.UseYours(config.customModel);
            else body.BuildBuiltIn();
            return body;
        }

        public Material Material => haze;

        void OnDestroy()
        {
            if (haze != null) Destroy(haze);
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

        void BuildBuiltIn()
        {
            var t = transform;
            hips = Joint(t, new Vector3(0f, 0.52f, 0f));
            Limb(hips, Vector3.up * 0.1f, 0.2f, 0.06f, 0.035f);                // belly, narrow
            chest = Joint(hips, new Vector3(0f, 0.2f, 0f));
            Limb(chest, Vector3.up * 0.06f, 0.14f, 0.085f, 0.05f);             // ribcage
            neck = Joint(chest, new Vector3(0f, 0.14f, 0.01f));
            Limb(neck, Vector3.up * 0.035f, 0.07f, 0.014f, 0.014f);            // a long thin neck
            head = Joint(neck, new Vector3(0f, 0.07f, 0f));
            var skull = Shape(PrimitiveType.Sphere, head, new Vector3(0f, 0.04f, 0.01f), new Vector3(0.06f, 0.09f, 0.07f));
            skull.name = "Head";

            armL = Joint(chest, new Vector3(-0.09f, 0.11f, 0f));
            armR = Joint(chest, new Vector3(0.09f, 0.11f, 0f));
            foreL = Joint(armL, new Vector3(0f, -0.2f, 0f));
            foreR = Joint(armR, new Vector3(0f, -0.2f, 0f));
            foreach (var a in new[] { armL, armR }) Limb(a, Vector3.down * 0.1f, 0.2f, 0.016f, 0.016f);
            foreach (var f in new[] { foreL, foreR })
            {
                Limb(f, Vector3.down * 0.11f, 0.22f, 0.013f, 0.013f);
                Limb(f, Vector3.down * 0.25f, 0.07f, 0.007f, 0.02f);           // long fingers
            }

            thighL = Joint(hips, new Vector3(-0.04f, 0f, 0f));
            thighR = Joint(hips, new Vector3(0.04f, 0f, 0f));
            shinL = Joint(thighL, new Vector3(0f, -0.25f, 0f));
            shinR = Joint(thighR, new Vector3(0f, -0.25f, 0f));
            foreach (var th in new[] { thighL, thighR }) Limb(th, Vector3.down * 0.125f, 0.25f, 0.022f, 0.022f);
            foreach (var sh in new[] { shinL, shinR }) Limb(sh, Vector3.down * 0.135f, 0.27f, 0.015f, 0.015f);
        }

        /// <summary>The fog's colour (as the look shows it) taken by this much: it stays a silhouette, just hazed.</summary>
        public void SetHaze(Color hazeColor) => RuntimeMaterials.SetHazeColor(haze, hazeColor);

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

        // A tapering limb as two capsules (thick end at the joint), centred on `centre`.
        void Limb(Transform joint, Vector3 centre, float length, float radiusTop, float radiusBottom)
        {
            float r = (radiusTop + radiusBottom) * 0.5f;
            Shape(PrimitiveType.Capsule, joint, centre, new Vector3(r * 2f, length * 0.5f, r * 2f * 0.85f));
            if (Mathf.Abs(radiusTop - radiusBottom) > 0.004f)
            {
                float big = Mathf.Max(radiusTop, radiusBottom);
                Vector3 end = centre + (radiusTop > radiusBottom ? Vector3.up : Vector3.down) * length * 0.25f;
                Shape(PrimitiveType.Capsule, joint, end, new Vector3(big * 2f, length * 0.25f, big * 1.6f));
            }
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
