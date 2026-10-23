using Game.Core;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.Entities
{
    /// <summary>
    /// How a scour looks (grey-box stand-in for M2): a too-thin black figure, arms hanging past the knees,
    /// a small head on a forward-jutting neck, burning eyes. Logic tells it how to look (visible, size,
    /// shudder, eye colour) and it does the rest. The real model replaces this later.
    /// </summary>
    public sealed class ScourBody : MonoBehaviour
    {
        static readonly Color Red = new Color(1f, 0.01f, 0f);
        static readonly Color Blue = new Color(0.15f, 0.45f, 1f);
        static readonly Color Warm = new Color(1f, 0.55f, 0.35f);

        Transform visual;
        Renderer[] renderers;
        Material bodyMat, eyeMat;
        Transform[] eyes;
        Light eyeLight;
        HDAdditionalLightData eyeLightData;
        Vector3 eyeScale;
        float height = 2.4f;

        /// <summary>The point between the eyes, in the world.</summary>
        public Vector3 EyePoint => eyes != null && eyes.Length == 2 ? (eyes[0].position + eyes[1].position) * 0.5f : transform.position + Vector3.up * height;
        /// <summary>Roughly the middle of the body (for "is the beam on it").</summary>
        public Vector3 Chest => transform.position + Vector3.up * height * 0.6f * transform.localScale.y;
        public bool Visible { get; private set; } = true;

        public void Build(float bodyHeight, bool extraArms, System.Random rng)
        {
            height = bodyHeight;
            float k = bodyHeight / 2.4f;
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);
            bodyMat = RuntimeMaterials.Unlit("Scour body", Color.black);
            eyeMat = RuntimeMaterials.Glow("Scour eyes", Red);

            // Proportions from the plan: hunched, long arms, legs thinning to nothing near the ground.
            Part(PrimitiveType.Capsule, new Vector3(0f, 1.5f, -0.04f), new Vector3(0.2f, 0.46f, 0.13f), new Vector3(12f, 0f, 0f), k);
            Part(PrimitiveType.Capsule, new Vector3(0f, 2.02f, 0.1f), new Vector3(0.055f, 0.16f, 0.055f), new Vector3(40f, 0f, 0f), k);
            Part(PrimitiveType.Capsule, new Vector3(0f, 2.22f, 0.2f), new Vector3(0.15f, 0.17f, 0.15f), Vector3.zero, k);
            foreach (float side in new[] { -1f, 1f })
            {
                Part(PrimitiveType.Capsule, new Vector3(0.19f * side, 1.55f, 0.02f), new Vector3(0.045f, 0.33f, 0.045f), new Vector3(0f, 0f, 6f * side), k);
                Part(PrimitiveType.Capsule, new Vector3(0.24f * side, 0.95f, 0.08f), new Vector3(0.04f, 0.33f, 0.04f), new Vector3(-10f, 0f, -4f * side), k);
                Part(PrimitiveType.Capsule, new Vector3(0.26f * side, 0.5f, 0.14f), new Vector3(0.03f, 0.14f, 0.03f), new Vector3(-20f, 0f, 0f), k);
                Part(PrimitiveType.Capsule, new Vector3(0.08f * side, 0.85f, 0f), new Vector3(0.055f, 0.3f, 0.055f), Vector3.zero, k);
                Part(PrimitiveType.Capsule, new Vector3(0.09f * side, 0.32f, 0.03f), new Vector3(0.03f, 0.28f, 0.03f), new Vector3(8f, 0f, 0f), k);
                if (extraArms)
                    Part(PrimitiveType.Capsule, new Vector3(0.17f * side, 1.2f, 0.05f), new Vector3(0.025f, 0.3f, 0.025f), new Vector3(-25f, 0f, 30f * side), k);
            }

            eyes = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(eye.GetComponent<Collider>());
                eye.name = "Eye";
                eye.layer = Layers.Entities;
                eye.transform.SetParent(visual, false);
                eye.transform.localPosition = new Vector3((i == 0 ? -0.05f : 0.05f) * k, 2.26f * k, 0.33f * k);
                eye.transform.localScale = new Vector3(0.05f, 0.032f, 0.03f) * k;
                eye.GetComponent<Renderer>().sharedMaterial = eyeMat;
                eyes[i] = eye.transform;
            }
            eyeScale = eyes[0].localScale;

            var glow = new GameObject("Eye Glow");
            glow.transform.SetParent(visual, false);
            glow.transform.localPosition = new Vector3(0f, 2.26f * k, 0.5f * k);
            eyeLightData = glow.AddHDLight(LightType.Point);
            eyeLight = glow.GetComponent<Light>();
            eyeLight.shadows = LightShadows.None;
            glow.AddComponent<FogGlowLight>().strength = 1.5f;

            renderers = visual.GetComponentsInChildren<Renderer>();
        }

        void Part(PrimitiveType type, Vector3 pos, Vector3 scale, Vector3 euler, float k)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.layer = Layers.Entities;
            go.transform.SetParent(visual, false);
            go.transform.localPosition = pos * k;
            go.transform.localScale = scale * k;
            go.transform.localRotation = Quaternion.Euler(euler);
            go.GetComponent<Renderer>().sharedMaterial = bodyMat;
        }

        void OnDestroy()
        {
            if (bodyMat != null) Destroy(bodyMat);
            if (eyeMat != null) Destroy(eyeMat);
        }

        public void SetVisible(bool visible)
        {
            Visible = visible;
            foreach (var r in renderers) r.enabled = visible;
            if (eyeLight != null) eyeLight.enabled = visible;
        }

        /// <summary>Materializing: 0 nothing, 1 whole. It pulls together from a smear in the air.</summary>
        public void SetMaterialize(float t)
        {
            float s = Mathf.SmoothStep(0f, 1f, t);
            visual.localScale = new Vector3(Mathf.Lerp(2.5f, 1f, s), Mathf.Lerp(0.05f, 1f, s), Mathf.Lerp(2.5f, 1f, s));
            visual.localPosition = Vector3.up * (1f - s) * height * 0.5f + Random.insideUnitSphere * 0.15f * (1f - s);
        }

        /// <summary>Shudder by this many metres (and stretch: 0 none, 1 the crescendo's peak).</summary>
        public void Shudder(float amount, float stretch = 0f)
        {
            visual.localPosition = Random.insideUnitSphere * amount;
            visual.localScale = new Vector3(1f - 0.15f * stretch, 1f + 0.2f * stretch, 1f - 0.15f * stretch);
        }

        public void SetGrowth(float growth) => transform.localScale = Vector3.one * growth;

        /// <summary>Eyes: widen (1 normal), glow (HDR), blue (0 red, 1 the cold blue of being driven off), shine (warm beam flare).</summary>
        public void SetEyes(float widen, float glow, float blue, float shine, float lightStrength, float lightRange)
        {
            Color c = Color.Lerp(Color.Lerp(Red, Warm, shine), Blue, blue);
            RuntimeMaterials.SetGlow(eyeMat, c * glow);
            foreach (var e in eyes) e.localScale = new Vector3(eyeScale.x * widen, eyeScale.y * Mathf.Lerp(1f, widen, 0.6f), eyeScale.z);
            if (eyeLight == null) return;
            eyeLight.color = Color.Lerp(new Color(1f, 0.12f, 0.05f), Blue, blue);
            eyeLight.intensity = glow * lightStrength;
            eyeLight.range = lightRange;
        }

        /// <summary>Turn to face a point (yaw only).</summary>
        public void Face(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(d);
        }
    }
}
