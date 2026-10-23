using Game.Core;
using UnityEngine;

namespace Game.Entities
{
    /// <summary>
    /// The owner's own scour model (ScourConfig: Use Custom Model). Places the prefab, scales it to the scour's
    /// height, adds the glowing eyes, eye light and mouth glare at the points its <see cref="ScourModelRig"/>
    /// names, opens its jaw, and passes pose, mouth, speed and shudder to its Animator.
    /// </summary>
    public sealed class ModelScourLook : ScourLook
    {
        static readonly int PoseId = Animator.StringToHash("Pose");
        static readonly int MouthId = Animator.StringToHash("Mouth");
        static readonly int SpeedId = Animator.StringToHash("Speed");
        static readonly int ShudderId = Animator.StringToHash("Shudder");
        static readonly int SideId = Animator.StringToHash("Side");
        static readonly int AppearId = Animator.StringToHash("Appear");

        ScourModelRig rig;
        Animator animator;
        Transform model, chest, jaw;
        Quaternion jawRest;
        Vector3 lastPosition;
        float speed;
        int side = 1;
        bool hasPose, hasMouth, hasSpeed, hasShudder, hasSide, hasAppear;

        public override Vector3 Chest => chest != null ? chest.position : transform.position + Vector3.up * s.height * 0.6f * transform.localScale.y;

        protected override void BuildLook(ScourConfig config, System.Random rng)
        {
            model = Instantiate(config.customModel, visual, false).transform;
            model.name = "Model";
            model.localPosition = Vector3.zero;
            foreach (var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.Entities;
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Destroy(c); // (the scour isn't solid)

            rig = model.GetComponent<ScourModelRig>();
            if (rig == null) rig = model.gameObject.AddComponent<ScourModelRig>(); // (no rig: guess everything)
            model.localRotation = Quaternion.Euler(0f, rig.yawOffset, 0f);
            if (rig.matchHeight) FitHeight();

            animator = rig.animator != null ? rig.animator : model.GetComponentInChildren<Animator>();
            if (animator != null) FindParameters();

            var bounds = ModelBounds();
            Transform head = rig.head != null ? rig.head : model;
            chest = rig.chest;
            jaw = rig.jaw;
            if (jaw != null) jawRest = jaw.localRotation;

            // Eyes: the model's own eye meshes, the points it names, or two guessed near the top front.
            if (rig.eyeRenderers != null && rig.eyeRenderers.Length > 0) AddEyes(head, new Vector3[0], 0f, rig.eyeRenderers);
            else
            {
                Vector3[] points;
                if (rig.eyePoints != null && rig.eyePoints.Length > 0)
                {
                    points = new Vector3[rig.eyePoints.Length];
                    for (int i = 0; i < points.Length; i++) points[i] = head.InverseTransformPoint(rig.eyePoints[i].position);
                }
                else
                {
                    Vector3 top = new Vector3(bounds.center.x, bounds.max.y - bounds.size.y * 0.06f, bounds.max.z);
                    points = new[] { head.InverseTransformPoint(top + transform.right * -0.035f), head.InverseTransformPoint(top + transform.right * 0.035f) };
                }
                AddEyes(head, points, 0.034f * s.height / 2.4f / Mathf.Max(0.0001f, head.lossyScale.x));
            }
            AddEyeLight(head, head.InverseTransformPoint(EyePoint + transform.forward * 0.15f));

            Transform mouthAt = rig.mouth != null ? rig.mouth : head;
            Vector3 mouthPoint = rig.mouth != null ? Vector3.zero : head.InverseTransformPoint(EyePoint + Vector3.down * 0.12f + transform.forward * 0.05f);
            AddGlare(mouthAt, mouthPoint, Quaternion.Inverse(mouthAt.rotation) * Quaternion.LookRotation(Quaternion.AngleAxis(28f, transform.right) * transform.forward));

            lastPosition = transform.position;
        }

        // Scale the model so its height matches the scour's.
        void FitHeight()
        {
            model.localScale = Vector3.one;
            var b = ModelBounds();
            if (b.size.y > 0.01f) model.localScale = Vector3.one * (s.height / b.size.y);
            b = ModelBounds();
            model.localPosition += Vector3.up * (transform.position.y - b.min.y) / Mathf.Max(0.0001f, visual.lossyScale.y); // feet on the ground
        }

        Bounds ModelBounds()
        {
            var rs = model.GetComponentsInChildren<Renderer>(true);
            if (rs.Length == 0) return new Bounds(transform.position + Vector3.up * s.height * 0.5f, new Vector3(0.5f, s.height, 0.5f));
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        void FindParameters()
        {
            foreach (var p in animator.parameters)
            {
                if (p.nameHash == PoseId && p.type == AnimatorControllerParameterType.Int) hasPose = true;
                else if (p.nameHash == MouthId && p.type == AnimatorControllerParameterType.Float) hasMouth = true;
                else if (p.nameHash == SpeedId && p.type == AnimatorControllerParameterType.Float) hasSpeed = true;
                else if (p.nameHash == ShudderId && p.type == AnimatorControllerParameterType.Float) hasShudder = true;
                else if (p.nameHash == SideId && p.type == AnimatorControllerParameterType.Int) hasSide = true;
                else if (p.nameHash == AppearId && p.type == AnimatorControllerParameterType.Trigger) hasAppear = true;
            }
        }

        public override void SetPose(ScourPose pose, int poseSide, bool snap, float rate = 4f)
        {
            Pose = pose;
            side = poseSide < 0 ? -1 : 1;
            if (animator == null) return;
            if (hasPose) animator.SetInteger(PoseId, (int)pose);
            if (hasSide) animator.SetInteger(SideId, side);
            if (snap && animator.isActiveAndEnabled) animator.Update(0f);
        }

        protected override void OnVisibilityChanged(bool visible)
        {
            if (!visible) return;
            lastPosition = transform.position;
            if (animator != null && hasAppear) animator.SetTrigger(AppearId);
        }

        protected override void Animate(float dt, float open)
        {
            Vector3 v = dt > 0f ? (transform.position - lastPosition) / dt : Vector3.zero;
            lastPosition = transform.position;
            if (v.sqrMagnitude > 400f) v = Vector3.zero; // (a creep is a jump, not a run)
            speed = Mathf.Lerp(speed, new Vector2(v.x, v.z).magnitude, 1f - Mathf.Exp(-6f * dt));

            if (animator != null)
            {
                if (hasMouth) animator.SetFloat(MouthId, open);
                if (hasSpeed) animator.SetFloat(SpeedId, speed);
                if (hasShudder) animator.SetFloat(ShudderId, ShudderAmount);
            }
            if (jaw != null)
            {
                float angle = Mathf.Lerp(0f, rig.jawOpenAngle, Mathf.Min(open, 1f)) + Mathf.Max(0f, open - 1f) * rig.jawOpenAngle * 0.6f;
                jaw.localRotation = jawRest * Quaternion.AngleAxis(angle, rig.jawAxis);
            }
        }
    }
}
