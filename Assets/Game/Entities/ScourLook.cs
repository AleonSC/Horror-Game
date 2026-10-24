using Game.Core;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.Entities
{
    /// <summary>
    /// What a scour looks like, behind one set of calls the scour's logic makes (be visible, hold this pose, open
    /// the mouth this much, eyes like this, shudder). Two looks plug in: the built-in procedural figure
    /// (<see cref="ProceduralScourLook"/>) and the owner's own model (<see cref="ModelScourLook"/>), picked by
    /// ScourConfig. The parts every look shares live here: materializing and shuddering, the glowing eyes and
    /// their light, and the red glare from the mouth.
    /// </summary>
    public abstract class ScourLook : MonoBehaviour
    {
        protected static readonly Color Red = new Color(1f, 0.01f, 0f);
        protected static readonly Color Blue = new Color(0.15f, 0.45f, 1f);
        protected static readonly Color Warm = new Color(1f, 0.55f, 0.35f);
        protected static readonly Color GlareColor = new Color(1f, 0.1f, 0.04f);

        protected ScourSettings s;
        protected Transform visual;
        protected Renderer[] renderers = new Renderer[0];
        protected Transform[] eyes = new Transform[0];
        protected float mouth;
        Vector3[] eyeScales = new Vector3[0];
        Material eyeMat;
        Light eyeLight, glare;
        HDAdditionalLightData eyeLightData;
        /// <summary>Does the eyes' glow light up what's near (a trunk it's peeking round), or only the fog?</summary>
        public bool EyeLightOnSurfaces;

        /// <summary>How bright the glow round each eye is, as a share of the eyes' own glow.</summary>
        public float EyeHalo { get; set; } = 0.12f;

        /// <summary>The point between the eyes, in the world.</summary>
        public virtual Vector3 EyePoint
        {
            get
            {
                if (eyes.Length == 0) return transform.position + Vector3.up * s.height * transform.localScale.y;
                Vector3 sum = Vector3.zero;
                foreach (var e in eyes) sum += e.position;
                return sum / eyes.Length;
            }
        }

        /// <summary>Roughly the middle of the body (for "is the beam on it").</summary>
        public abstract Vector3 Chest { get; }
        public bool Visible { get; private set; } = true;
        public ScourPose Pose { get; protected set; }

        /// <summary>Pick and add the look ScourConfig asks for.</summary>
        public static ScourLook AddTo(GameObject go, ScourConfig config)
        {
            if (config.useCustomModel && config.customModel != null)
                return go.AddComponent<ModelScourLook>();
            if (config.useCustomModel) Debug.LogWarning("ScourConfig: Use Custom Model is on but no model is set; using the built-in scour.");
            return go.AddComponent<ProceduralScourLook>();
        }

        public void Build(ScourConfig config, System.Random rng)
        {
            s = config.settings;
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);
            visual.gameObject.layer = Layers.Entities;
            eyeMat = RuntimeMaterials.Glow("Scour eyes", Red);
            BuildLook(config, rng);
            renderers = visual.GetComponentsInChildren<Renderer>();
        }

        /// <summary>Make the figure under <see cref="visual"/>, and call AddEyes / AddEyeLight / AddGlare for its parts.</summary>
        protected abstract void BuildLook(ScourConfig config, System.Random rng);

        /// <summary>Hold a pose. Snap (when it can't be seen, after a creep) or ease into it.</summary>
        public abstract void SetPose(ScourPose pose, int side, bool snap, float rate = 4f);

        /// <summary>Each frame while visible: animate (pose, mouth, legs...). <paramref name="open"/> is the mouth, 0-1.6.</summary>
        protected abstract void Animate(float dt, float open);

        protected virtual void OnDestroy()
        {
            if (eyeMat != null) Destroy(eyeMat);
        }

        // ------------------------------------------------------------------ shared parts

        /// <summary>Glowing eyes at these points (parented to <paramref name="bone"/>), or glow on the model's own eye renderers.</summary>
        protected void AddEyes(Transform bone, Vector3[] localPoints, float size, Renderer[] ownEyes = null)
        {
            if (ownEyes != null && ownEyes.Length > 0)
            {
                eyes = new Transform[ownEyes.Length];
                for (int i = 0; i < ownEyes.Length; i++)
                {
                    ownEyes[i].sharedMaterial = eyeMat;
                    eyes[i] = ownEyes[i].transform;
                }
            }
            else
            {
                eyes = new Transform[localPoints.Length];
                for (int i = 0; i < localPoints.Length; i++)
                {
                    var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(eye.GetComponent<Collider>());
                    eye.name = "Eye";
                    eye.layer = Layers.Entities;
                    eye.transform.SetParent(bone, false);
                    eye.transform.localPosition = localPoints[i];
                    eye.transform.localScale = new Vector3(1f, 0.65f, 0.6f) * size;
                    eye.GetComponent<Renderer>().sharedMaterial = eyeMat;
                    eyes[i] = eye.transform;
                }
            }
            eyeScales = new Vector3[eyes.Length];
            for (int i = 0; i < eyes.Length; i++) eyeScales[i] = eyes[i].localScale;
            // A soft glow round each eye, hanging in the mist (it comes and goes with the body).
            halos = new Renderer[eyes.Length];
            for (int i = 0; i < eyes.Length; i++)
            {
                var parent = eyes[i].parent != null ? eyes[i].parent : eyes[i];
                halos[i] = Effects.Glow(parent, parent.InverseTransformPoint(eyes[i].position), 0.22f, Color.black, 0.2f, 3f, "Eye glow");
                if (halos[i] != null) halos[i].transform.localScale = Vector3.one * (0.22f / Mathf.Max(0.001f, parent.lossyScale.x));
            }
        }

        Renderer[] halos = new Renderer[0];

        /// <summary>The red glow the eyes throw into the fog.</summary>
        protected void AddEyeLight(Transform bone, Vector3 localPoint)
        {
            var go = new GameObject("Eye Glow");
            go.transform.SetParent(bone, false);
            go.transform.localPosition = localPoint;
            eyeLightData = go.AddHDLight(LightType.Point);
            eyeLight = go.GetComponent<Light>();
            eyeLight.shadows = LightShadows.None;
            eyeLight.color = Red;
            eyeLight.intensity = 0f; // (dark until the eyes are set: a new light starts bright white)
            go.AddComponent<FogGlowLight>().strength = 1.5f;
        }

        /// <summary>The red glare out of its mouth when it opens (a spot, aimed out and down).</summary>
        protected void AddGlare(Transform bone, Vector3 localPoint, Quaternion localRotation)
        {
            var go = new GameObject("Mouth Glare");
            go.transform.SetParent(bone, false);
            go.transform.localPosition = localPoint;
            go.transform.localRotation = localRotation;
            go.AddHDLight(LightType.Spot);
            glare = go.GetComponent<Light>();
            glare.shadows = LightShadows.None;
            glare.color = GlareColor;
            glare.enabled = false;
            go.AddComponent<FogGlowLight>().strength = 0.8f;
        }

        // ------------------------------------------------------------------ what the logic sets

        public void SetVisible(bool visible)
        {
            Visible = visible;
            foreach (var r in renderers) if (r != null) r.enabled = visible;
            if (eyeLight != null)
            {
                // Coming back, it starts dark: until its eyes are set it would shine whatever it last was
                // (bright blue from being faced down), a flash in the fog.
                if (visible) eyeLight.intensity = 0f;
                eyeLight.enabled = visible;
            }
            if (glare != null && visible) glare.intensity = 0f;
            if (glare != null) glare.enabled = visible && mouth > 0.01f;
            OnVisibilityChanged(visible);
        }

        protected virtual void OnVisibilityChanged(bool visible) { }

        /// <summary>The mouth: 0 shut, 1 open, up to 1.6 gaping wide. It eases there.</summary>
        public void SetMouth(float open) => mouth = Mathf.MoveTowards(mouth, Mathf.Max(0f, open), Time.deltaTime * 2.5f);
        public void ShutMouth() => mouth = 0f;

        /// <summary>Materializing: 0 nothing, 1 whole. It pulls together from a smear in the air.</summary>
        public void SetMaterialize(float t)
        {
            float v = Mathf.SmoothStep(0f, 1f, t);
            visual.localScale = new Vector3(Mathf.Lerp(2.5f, 1f, v), Mathf.Lerp(0.05f, 1f, v), Mathf.Lerp(2.5f, 1f, v));
            visual.localPosition = Vector3.up * (1f - v) * s.height * 0.5f + Random.insideUnitSphere * 0.15f * (1f - v);
            // Its pale fangs and the throat stay hidden until it's nearly whole: smeared out flat, they'd catch
            // the light as a flash of white.
            if (Visible) ShowDetails(v > 0.92f);
        }

        /// <summary>Show or hide the small light-coloured parts (fangs, throat) while it forms.</summary>
        protected virtual void ShowDetails(bool show) { }

        /// <summary>Shudder by this many metres (and stretch: 0 none, 1 the crescendo's peak).</summary>
        public void Shudder(float amount, float stretch = 0f)
        {
            visual.localPosition = Random.insideUnitSphere * amount;
            visual.localScale = new Vector3(1f - 0.15f * stretch, 1f + 0.2f * stretch, 1f - 0.15f * stretch);
            ShudderAmount = amount;
        }

        /// <summary>How hard it's shuddering right now (metres), for animators.</summary>
        protected float ShudderAmount { get; private set; }

        public void SetGrowth(float growth) => transform.localScale = Vector3.one * growth;

        /// <summary>Eyes: widen (1 normal), glow (HDR), blue (0 red, 1 the cold blue of being driven off), shine (warm beam flare).</summary>
        public void SetEyes(float widen, float glow, float blue, float shine, float lightStrength, float lightRange)
        {
            Color c = Color.Lerp(Color.Lerp(Red, Warm, shine), Blue, blue);
            RuntimeMaterials.SetGlow(eyeMat, c * glow);
            foreach (var h in halos) Effects.SetGlow(h, c * (glow * EyeHalo));
            for (int i = 0; i < eyes.Length; i++)
                eyes[i].localScale = new Vector3(eyeScales[i].x * widen, eyeScales[i].y * Mathf.Lerp(1f, widen, 0.6f), eyeScales[i].z);
            if (eyeLight == null) return;
            eyeLight.color = Color.Lerp(new Color(1f, 0.12f, 0.05f), Blue, blue);
            eyeLight.intensity = glow * lightStrength;
            eyeLight.range = lightRange;
            if (eyeLightData != null)
            {
                eyeLightData.affectDiffuse = EyeLightOnSurfaces;
                eyeLightData.affectSpecular = EyeLightOnSurfaces;
            }
        }

        /// <summary>
        /// Points on the body's surface as it stands right now, in the world (for it coming apart): taken from its
        /// skinned and plain meshes.
        /// </summary>
        public Vector3[] SamplePoints(int count)
        {
            var all = new System.Collections.Generic.List<Vector3>();
            var baked = new Mesh();
            foreach (var r in renderers)
            {
                if (r == null || !r.enabled || r.gameObject.layer == Layers.Effects) continue; // (not the glows)
                Mesh mesh = null;
                Matrix4x4 toWorld = r.transform.localToWorldMatrix;
                if (r is SkinnedMeshRenderer skinned)
                {
                    skinned.BakeMesh(baked, false); // (in its own space, unscaled: toWorld does the rest)
                    mesh = baked;
                }
                else if (r.TryGetComponent<MeshFilter>(out var mf)) mesh = mf.sharedMesh;
                if (mesh == null) continue;
                // (Skip the eyes: they get their own lingering glow.)
                if (r.sharedMaterial == eyeMat) continue;
                foreach (var v in mesh.vertices) all.Add(toWorld.MultiplyPoint3x4(v));
            }
            Destroy(baked);
            var picked = new Vector3[Mathf.Min(count, all.Count)];
            for (int i = 0; i < picked.Length; i++) picked[i] = all[Random.Range(0, all.Count)];
            return picked;
        }

        /// <summary>Turn to face a point (yaw only).</summary>
        public void Face(Vector3 point)
        {
            Vector3 d = point - transform.position;
            d.y = 0f;
            if (d.sqrMagnitude > 0.0001f) transform.rotation = Quaternion.LookRotation(d);
        }

        void LateUpdate()
        {
            if (!Visible) return;
            float open = Mathf.Clamp01(mouth);
            if (glare != null)
            {
                glare.enabled = open > 0.01f;
                glare.intensity = s.mouthGlare * open;
                glare.range = s.mouthGlareRange;
                glare.spotAngle = 55f;
            }
            Animate(Time.deltaTime, mouth);
        }
    }
}
