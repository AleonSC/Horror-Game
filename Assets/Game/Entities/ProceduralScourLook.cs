using Game.Core;
using Game.Rendering;
using UnityEngine;

namespace Game.Entities
{
    /// <summary>
    /// The built-in scour (used when ScourConfig has no custom model): the figure from <see cref="ScourRig"/>,
    /// posed, with a jaw that drops open on a glowing throat and fangs, legs that trail, and twitches.
    /// </summary>
    public sealed class ProceduralScourLook : ScourLook
    {
        ScourRig rig;
        Material bodyMat, throatMat, fangMat;
        PoseShape pose, target;
        float poseRate = 4f;
        Vector3 lastPosition;
        Vector3 legSwing;
        float twitchTimer = 2f;
        Vector3 twitchHead, twitchFingers;

        public override Vector3 Chest => rig.SpineMid.position;

        protected override void BuildLook(ScourConfig config, System.Random rng)
        {
            bodyMat = s.litBody ? RuntimeMaterials.Lit("Scour body", new Color(0.02f, 0.02f, 0.022f), 0.35f, true)
                                : RuntimeMaterials.Unlit("Scour body", Color.black, true);
            throatMat = RuntimeMaterials.Glow("Scour throat", GlareColor);
            fangMat = RuntimeMaterials.Lit("Scour fangs", new Color(0.55f, 0.5f, 0.44f), 0.5f);

            int extraPairs = rng.NextDouble() < s.extraArmsChance ? (rng.NextDouble() < 0.5 ? 1 : 2) : 0;
            rig = new ScourRig();
            rig.Build(visual, s.height, extraPairs, bodyMat, rng);
            float k = rig.Scale;

            AddEyes(rig.Head, new[] { rig.EyeLeft, rig.EyeRight }, 0.034f * k);
            AddEyeLight(rig.Head, (rig.EyeLeft + rig.EyeRight) * 0.5f + Vector3.forward * 0.15f * k);

            // The throat: a glowing hollow behind the lips, fangs above and below.
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
            AddGlare(rig.Head, rig.MouthFront, Quaternion.Euler(28f, 0f, 0f)); // out and down: a red pool on the ground

            pose = target = PoseShape.For(ScourPose.Neutral, 1);
            lastPosition = transform.position;
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
            var go = new GameObject(name) { layer = Layers.Entities };
            go.transform.SetParent(bone, false);
            go.AddComponent<MeshFilter>().sharedMesh = b.ToStaticMesh(name);
            go.AddComponent<MeshRenderer>().sharedMaterial = fangMat;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            foreach (var m in new[] { bodyMat, throatMat, fangMat }) if (m != null) Destroy(m);
            if (rig?.Renderer != null) Destroy(rig.Renderer.sharedMesh);
            foreach (var mf in GetComponentsInChildren<MeshFilter>()) if (mf.sharedMesh != null && mf.sharedMesh.name.EndsWith("Fangs")) Destroy(mf.sharedMesh);
        }

        public override void SetPose(ScourPose next, int side, bool snap, float rate = 4f)
        {
            Pose = next;
            target = PoseShape.For(next, side);
            poseRate = rate;
            if (snap) pose = target;
        }

        protected override void OnVisibilityChanged(bool visible)
        {
            if (visible) lastPosition = transform.position;
        }

        protected override void Animate(float dt, float open)
        {
            pose = Lerp(pose, target, 1f - Mathf.Exp(-poseRate * dt));
            UpdateTwitch(dt);
            UpdateLegs(dt);
            ApplyPose();
            rig.Jaw.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 40f, Mathf.Min(open, 1f)) + Mathf.Max(0f, open - 1f) * 25f, 0f, 0f);
            RuntimeMaterials.SetGlow(throatMat, GlareColor * s.throatGlow * Mathf.Clamp01(open));
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
