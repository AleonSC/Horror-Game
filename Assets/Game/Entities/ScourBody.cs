using Game.Core;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.Entities
{
    /// <summary>
    /// How a scour looks and moves (section 6.7): the rigged figure from <see cref="ScourRig"/>, its burning
    /// eyes, the throat that drops open and glares red, poses it holds, legs that swing and trail, and small
    /// twitches. The scour's logic says what to show; this does the rest every frame.
    /// </summary>
    public sealed class ScourBody : MonoBehaviour
    {
        static readonly Color Red = new Color(1f, 0.01f, 0f);
        static readonly Color Blue = new Color(0.15f, 0.45f, 1f);
        static readonly Color Warm = new Color(1f, 0.55f, 0.35f);
        static readonly Color Glare = new Color(1f, 0.1f, 0.04f);

        ScourSettings s;
        Transform visual;
        ScourRig rig;
        Renderer[] renderers;
        Material bodyMat, eyeMat, throatMat, fangMat;
        Transform[] eyes;
        Vector3 eyeScale;
        Light eyeLight, glare;
        PoseShape pose, target;
        float poseRate = 4f;
        float mouth;
        Vector3 lastPosition;
        Vector3 legSwing;
        float twitchTimer = 2f;
        Vector3 twitchHead, twitchFingers;

        public Vector3 EyePoint => (eyes[0].position + eyes[1].position) * 0.5f;
        /// <summary>Roughly the middle of the body (for "is the beam on it").</summary>
        public Vector3 Chest => rig.SpineMid.position;
        public bool Visible { get; private set; } = true;
        public ScourPose Pose { get; private set; }

        public void Build(ScourSettings settings, System.Random rng)
        {
            s = settings;
            visual = new GameObject("Visual").transform;
            visual.SetParent(transform, false);
            visual.gameObject.layer = Layers.Entities;

            bodyMat = s.litBody ? RuntimeMaterials.Lit("Scour body", new Color(0.02f, 0.02f, 0.022f), 0.35f, true)
                                : RuntimeMaterials.Unlit("Scour body", Color.black, true);
            eyeMat = RuntimeMaterials.Glow("Scour eyes", Red);
            throatMat = RuntimeMaterials.Glow("Scour throat", Glare);
            fangMat = RuntimeMaterials.Lit("Scour fangs", new Color(0.55f, 0.5f, 0.44f), 0.5f);

            int extraPairs = rng.NextDouble() < s.extraArmsChance ? (rng.NextDouble() < 0.5 ? 1 : 2) : 0;
            rig = new ScourRig();
            rig.Build(visual, s.height, extraPairs, bodyMat, rng);

            eyes = new Transform[2];
            for (int i = 0; i < 2; i++)
            {
                var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                Destroy(eye.GetComponent<Collider>());
                eye.name = "Eye";
                eye.layer = Layers.Entities;
                eye.transform.SetParent(rig.Head, false);
                eye.transform.localPosition = i == 0 ? rig.EyeLeft : rig.EyeRight;
                eye.transform.localScale = new Vector3(0.034f, 0.022f, 0.02f) * rig.Scale;
                eye.GetComponent<Renderer>().sharedMaterial = eyeMat;
                eyes[i] = eye.transform;
            }
            eyeScale = eyes[0].localScale;
            BuildMouth();

            var glow = new GameObject("Eye Glow");
            glow.transform.SetParent(rig.Head, false);
            glow.transform.localPosition = (rig.EyeLeft + rig.EyeRight) * 0.5f + Vector3.forward * 0.15f * rig.Scale;
            glow.AddHDLight(LightType.Point);
            eyeLight = glow.GetComponent<Light>();
            eyeLight.shadows = LightShadows.None;
            glow.AddComponent<FogGlowLight>().strength = 1.5f;

            renderers = visual.GetComponentsInChildren<Renderer>();
            pose = target = PoseShape.For(ScourPose.Neutral, 1);
            lastPosition = transform.position;
        }

        // The throat: a glowing hollow behind the lips, fangs above and below, and the red glare out of it.
        void BuildMouth()
        {
            float k = rig.Scale;
            var throat = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Destroy(throat.GetComponent<Collider>());
            throat.name = "Throat";
            throat.layer = Layers.Entities;
            throat.transform.SetParent(rig.Head, false);
            throat.transform.localPosition = rig.ThroatCentre;
            throat.transform.localScale = new Vector3(0.06f, 0.05f, 0.05f) * k;
            throat.GetComponent<Renderer>().sharedMaterial = throatMat;

            Fangs("Upper Fangs", rig.Head, rig.MouthFront + new Vector3(0f, 0.005f, -0.01f) * k, -1f, k);
            Fangs("Lower Fangs", rig.Jaw, new Vector3(0f, -0.035f, 0.07f) * k, 1f, k);

            var go = new GameObject("Mouth Glare");
            go.transform.SetParent(rig.Head, false);
            go.transform.localPosition = rig.MouthFront;
            go.transform.localRotation = Quaternion.Euler(28f, 0f, 0f); // out and down: a red pool on the ground
            go.AddHDLight(LightType.Spot);
            glare = go.GetComponent<Light>();
            glare.shadows = LightShadows.None;
            glare.color = Glare;
            glare.enabled = false;
            go.AddComponent<FogGlowLight>().strength = 0.8f;
        }

        void Fangs(string name, Transform bone, Vector3 at, float dir, float k)
        {
            var b = new SkinnedBuilder();
            for (int i = 0; i < 6; i++)
            {
                float x = (i - 2.5f) / 2.5f * 0.035f * k;
                float len = (i == 1 || i == 4 ? 0.035f : 0.02f) * k;
                Vector3 root = at + new Vector3(x, 0f, -Mathf.Abs(x) * 0.6f);
                b.Cone(root, root + new Vector3(0f, len * dir, 0.004f * k), 0.004f * k, 0);
            }
            var mesh = b.ToStaticMesh(name);
            var go = new GameObject(name) { layer = Layers.Entities };
            go.transform.SetParent(bone, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = fangMat;
        }

        void OnDestroy()
        {
            foreach (var m in new[] { bodyMat, eyeMat, throatMat, fangMat }) if (m != null) Destroy(m);
            if (rig?.Renderer != null) Destroy(rig.Renderer.sharedMesh);
            foreach (var mf in GetComponentsInChildren<MeshFilter>()) if (mf.sharedMesh != null && mf.sharedMesh.name.EndsWith("Fangs")) Destroy(mf.sharedMesh);
        }

        // ------------------------------------------------------------------ what the logic sets

        public void SetVisible(bool visible)
        {
            Visible = visible;
            foreach (var r in renderers) r.enabled = visible;
            if (eyeLight != null) eyeLight.enabled = visible;
            if (glare != null) glare.enabled = visible && mouth > 0.01f;
            if (visible) lastPosition = transform.position;
        }

        /// <summary>Hold a pose. Snap (when it can't be seen, after a creep) or ease into it.</summary>
        public void SetPose(ScourPose next, int side, bool snap, float rate = 4f)
        {
            Pose = next;
            target = PoseShape.For(next, side);
            poseRate = rate;
            if (snap) pose = target;
        }

        /// <summary>The mouth: 0 shut, 1 open, up to 1.6 gaping wide.</summary>
        public void SetMouth(float open) => mouth = Mathf.MoveTowards(mouth, Mathf.Max(0f, open), Time.deltaTime * 2.5f);
        public void ShutMouth() => mouth = 0f;

        /// <summary>Materializing: 0 nothing, 1 whole. It pulls together from a smear in the air.</summary>
        public void SetMaterialize(float t)
        {
            float v = Mathf.SmoothStep(0f, 1f, t);
            visual.localScale = new Vector3(Mathf.Lerp(2.5f, 1f, v), Mathf.Lerp(0.05f, 1f, v), Mathf.Lerp(2.5f, 1f, v));
            visual.localPosition = Vector3.up * (1f - v) * s.height * 0.5f + Random.insideUnitSphere * 0.15f * (1f - v);
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

        // ------------------------------------------------------------------ every frame

        void LateUpdate()
        {
            if (!Visible) return;
            float dt = Time.deltaTime;
            float blend = 1f - Mathf.Exp(-poseRate * dt);
            pose = Lerp(pose, target, blend);
            UpdateTwitch(dt);
            UpdateLegs(dt);
            ApplyPose();
            ApplyMouth();
        }

        void ApplyPose()
        {
            rig.Root.localPosition = Vector3.down * pose.Drop * rig.Scale;
            rig.Hips.localRotation = Quaternion.Euler(pose.Hips);
            rig.SpineLow.localRotation = Quaternion.Euler(pose.Spine * 0.5f);
            rig.SpineMid.localRotation = Quaternion.Euler(pose.Spine * 0.5f);
            rig.Chest.localRotation = Quaternion.Euler(pose.Chest);
            rig.Neck1.localRotation = Quaternion.Euler(pose.Neck * 0.5f);
            rig.Neck2.localRotation = Quaternion.Euler(pose.Neck * 0.5f);
            rig.Head.localRotation = Quaternion.Euler(pose.Head + twitchHead);
            rig.Shoulder[0].localRotation = Quaternion.Euler(pose.ShoulderL);
            rig.Shoulder[1].localRotation = Quaternion.Euler(pose.ShoulderR);
            rig.Elbow[0].localRotation = Quaternion.Euler(pose.ElbowL);
            rig.Elbow[1].localRotation = Quaternion.Euler(pose.ElbowR);
            for (int i = 0; i < 2; i++)
            for (int f = 0; f < ScourRig.Fingers; f++)
            for (int j = 0; j < ScourRig.FingerJoints; j++)
                rig.Finger[i, f, j].localRotation = Quaternion.Euler(-(8f + pose.Curl * 38f) - twitchFingers.x * (f + 1) * 0.3f, 0f, (f - 1.5f) * 4f * (1f - pose.Curl));
            foreach (var e in rig.ExtraShoulders) e.localRotation = Quaternion.Euler(pose.Extra);
            for (int i = 0; i < 2; i++)
            {
                rig.Thigh[i].localRotation = Quaternion.Euler(legSwing.x + (i == 0 ? legSwing.z : -legSwing.z), 0f, (i == 0 ? -1f : 1f) * 2f);
                rig.Knee[i].localRotation = Quaternion.Euler(legSwing.x * 0.7f + 4f, 0f, 0f);
            }
        }

        // The legs hang loose: they trail behind when it moves and sway a little when it doesn't.
        void UpdateLegs(float dt)
        {
            Vector3 v = dt > 0f ? (transform.position - lastPosition) / dt : Vector3.zero;
            lastPosition = transform.position;
            if (v.sqrMagnitude > 400f) v = Vector3.zero; // (a creep is a jump, not a run)
            float forward = Vector3.Dot(v, transform.forward);
            float t = Time.time;
            Vector3 want = new Vector3(Mathf.Clamp(forward * 22f, -35f, 55f) + Mathf.Sin(t * 1.3f) * 4f, 0f, Mathf.Sin(t * 0.9f) * 5f);
            legSwing = Vector3.Lerp(legSwing, want, 1f - Mathf.Exp(-3f * dt));
        }

        // Every few seconds, a jerk of the head or a spasm of the fingers.
        void UpdateTwitch(float dt)
        {
            twitchTimer -= dt;
            if (twitchTimer <= 0f)
            {
                twitchTimer = Random.Range(1.5f, 5f);
                if (Random.value < 0.6f) twitchHead = new Vector3(Random.Range(-14f, 14f), Random.Range(-18f, 18f), Random.Range(-20f, 20f));
                else twitchFingers = new Vector3(Random.Range(20f, 40f), 0f, 0f);
            }
            twitchHead = Vector3.Lerp(twitchHead, Vector3.zero, 1f - Mathf.Exp(-9f * dt));
            twitchFingers = Vector3.Lerp(twitchFingers, Vector3.zero, 1f - Mathf.Exp(-6f * dt));
        }

        void ApplyMouth()
        {
            rig.Jaw.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 40f, Mathf.Min(mouth, 1f)) + Mathf.Max(0f, mouth - 1f) * 25f, 0f, 0f);
            float open = Mathf.Clamp01(mouth);
            RuntimeMaterials.SetGlow(throatMat, Glare * s.throatGlow * open);
            glare.enabled = open > 0.01f;
            glare.intensity = s.mouthGlare * open;
            glare.range = s.mouthGlareRange;
            glare.spotAngle = 55f;
        }

        static PoseShape Lerp(PoseShape a, PoseShape b, float t)
        {
            return new PoseShape
            {
                Hips = Vector3.Lerp(a.Hips, b.Hips, t), Spine = Vector3.Lerp(a.Spine, b.Spine, t), Chest = Vector3.Lerp(a.Chest, b.Chest, t),
                Neck = Vector3.Lerp(a.Neck, b.Neck, t), Head = Vector3.Lerp(a.Head, b.Head, t),
                ShoulderL = Vector3.Lerp(a.ShoulderL, b.ShoulderL, t), ShoulderR = Vector3.Lerp(a.ShoulderR, b.ShoulderR, t),
                ElbowL = Vector3.Lerp(a.ElbowL, b.ElbowL, t), ElbowR = Vector3.Lerp(a.ElbowR, b.ElbowR, t),
                Drop = Mathf.Lerp(a.Drop, b.Drop, t), Curl = Mathf.Lerp(a.Curl, b.Curl, t), Extra = Vector3.Lerp(a.Extra, b.Extra, t),
            };
        }
    }
}
