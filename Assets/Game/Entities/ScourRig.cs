using System.Collections.Generic;
using Game.Core;
using UnityEngine;

namespace Game.Entities
{
    /// <summary>
    /// The scour's skeleton and its one skinned mesh, built from the plan's description (section 6.7): a 2.4 m,
    /// too-thin black figure, hunched, with knobbly vertebrae, curling ribs and collarbones; a small too-tall
    /// head on a long forward-jutting neck, with a jaw that drops open; arms hanging past the knees with
    /// backward-bending elbows and long curling fingers; loose legs whose shins thin to nothing above the
    /// ground; sometimes extra, thinner arms out of the ribs. In the bind pose every bone has no rotation,
    /// so a pose is just each bone's rotation.
    /// </summary>
    public sealed class ScourRig
    {
        public const int Fingers = 4, FingerJoints = 3;

        readonly List<Transform> bones = new List<Transform>();
        readonly Dictionary<Transform, Vector3> rest = new Dictionary<Transform, Vector3>();
        float k;

        public Transform Root { get; private set; }
        public Transform Hips, SpineLow, SpineMid, Chest, Neck1, Neck2, Head, Jaw;
        public readonly Transform[] Shoulder = new Transform[2], Elbow = new Transform[2], Wrist = new Transform[2];
        public readonly Transform[] Thigh = new Transform[2], Knee = new Transform[2];
        public readonly Transform[,,] Finger = new Transform[2, Fingers, FingerJoints];
        public readonly List<Transform> ExtraShoulders = new List<Transform>(), ExtraElbows = new List<Transform>();
        public SkinnedMeshRenderer Renderer { get; private set; }

        /// <summary>Where on the head (in the head bone's space) the eyes, mouth and throat go.</summary>
        public Vector3 EyeLeft, EyeRight, MouthFront, ThroatCentre;
        public float Scale => k;

        /// <summary>Build it under <paramref name="parent"/>, <paramref name="height"/> metres tall.</summary>
        public void Build(Transform parent, float height, int extraArmPairs, Material body, System.Random rng)
        {
            k = height / 2.4f;
            Root = new GameObject("Rig").transform;
            Root.gameObject.layer = Layers.Entities;
            Root.SetParent(parent, false);

            float lean = Range(rng, -0.02f, 0.03f); // each one a little different
            Hips = Bone("Hips", Root, P(0f, 1.1f, 0f));
            SpineLow = Bone("SpineLow", Hips, P(0f, 1.38f, -0.04f));
            SpineMid = Bone("SpineMid", SpineLow, P(0f, 1.64f, -0.03f + lean));
            Chest = Bone("Chest", SpineMid, P(0f, 1.88f, 0.07f + lean));
            Neck1 = Bone("Neck1", Chest, P(0f, 2.0f, 0.17f + lean));
            Neck2 = Bone("Neck2", Neck1, P(0f, 2.12f, 0.3f + lean));
            Head = Bone("Head", Neck2, P(0f, 2.21f, 0.4f + lean));
            Jaw = Bone("Jaw", Head, P(0f, 2.22f, 0.43f + lean));

            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                Shoulder[i] = Bone("Shoulder", Chest, P(0.2f * s, 1.92f, 0.06f));
                Elbow[i] = Bone("Elbow", Shoulder[i], P(0.24f * s, 1.42f, 0.15f));   // jutting forward...
                Wrist[i] = Bone("Wrist", Elbow[i], P(0.27f * s, 0.88f, 0.0f));        // ...the forearm bending back
                for (int f = 0; f < Fingers; f++)
                {
                    float spread = (f - 1.5f) / 1.5f;
                    var parentBone = Wrist[i];
                    Vector3 at = P(0.28f * s + spread * 0.03f * s, 0.76f, 0.0f + spread * 0.035f);
                    for (int j = 0; j < FingerJoints; j++)
                    {
                        Finger[i, f, j] = Bone("Finger", parentBone, at);
                        parentBone = Finger[i, f, j];
                        at += P(spread * 0.008f * s, -(0.12f - j * 0.02f) * Range(rng, 0.9f, 1.15f), 0.015f);
                    }
                }
                Thigh[i] = Bone("Thigh", Hips, P(0.1f * s, 1.07f, 0f));
                Knee[i] = Bone("Knee", Thigh[i], P(0.11f * s, 0.62f, 0.05f));
            }
            for (int pair = 0; pair < extraArmPairs; pair++)
            for (int i = 0; i < 2; i++)
            {
                float s = i == 0 ? -1f : 1f;
                float y = 1.62f - pair * 0.13f;
                var sh = Bone("ExtraShoulder", SpineMid, P(0.13f * s, y, 0.05f));
                var el = Bone("ExtraElbow", sh, P(0.2f * s, y - 0.3f, 0.18f));
                ExtraShoulders.Add(sh);
                ExtraElbows.Add(el);
            }

            var mesh = BuildMesh(rng);
            var smr = Root.gameObject.AddComponent<SkinnedMeshRenderer>();
            smr.sharedMesh = mesh;
            smr.bones = bones.ToArray();
            smr.rootBone = Root;
            smr.sharedMaterial = body;
            smr.updateWhenOffscreen = false;
            smr.localBounds = new Bounds(P(0f, 1.2f, 0.1f), P(2.4f, 3f, 2.4f));
            Renderer = smr;
        }

        Mesh BuildMesh(System.Random rng)
        {
            var b = new SkinnedBuilder();
            Vector3 R(Transform t) => rest[t];
            int I(Transform t) => bones.IndexOf(t);

            // Spine, pelvis and the knobs of the vertebrae down its back.
            b.Ellipsoid(R(Hips) + P(0f, 0.02f, 0f), P(0.11f, 0.07f, 0.07f), I(Hips));
            b.Tube(R(Hips), R(SpineLow), 0.045f * k, 0.04f * k, I(Hips));
            b.Tube(R(SpineLow), R(SpineMid), 0.04f * k, 0.045f * k, I(SpineLow));
            b.Tube(R(SpineMid), R(Chest), 0.045f * k, 0.05f * k, I(SpineMid));
            b.Tube(R(Chest), R(Neck1), 0.05f * k, 0.03f * k, I(Chest));
            Transform[] chain = { Hips, SpineLow, SpineMid, Chest, Neck1 };
            for (int c = 0; c < chain.Length - 1; c++)
            for (int n = 0; n < 3; n++)
            {
                Vector3 p = Vector3.Lerp(R(chain[c]), R(chain[c + 1]), n / 3f);
                Vector3 back = Vector3.Cross(Vector3.right, (R(chain[c + 1]) - R(chain[c])).normalized);
                b.Ellipsoid(p - back * 0.045f * k, P(0.022f, 0.018f, 0.022f), I(chain[c]), 4, 6);
            }

            // Ribs: thin curls round from the spine, and the collarbones.
            for (int r = 0; r < 5; r++)
            {
                Transform bone = r < 2 ? SpineMid : Chest;
                float y = Mathf.Lerp(1.56f, 1.86f, r / 4f);
                float w = Mathf.Lerp(0.12f, 0.16f, Mathf.Sin(r / 4f * Mathf.PI)) * Range(rng, 0.95f, 1.05f);
                for (int side = -1; side <= 1; side += 2)
                {
                    var pts = new List<Vector3>();
                    for (int q = 0; q <= 6; q++)
                    {
                        float a = q / 6f * Mathf.PI * 0.85f;
                        pts.Add(P(Mathf.Sin(a) * w * side, y - 0.06f * q / 6f, -0.04f + (1f - Mathf.Cos(a)) * 0.08f + 0.02f * r / 4f));
                    }
                    b.Curve(pts, 0.011f * k, 0.006f * k, I(bone), 4);
                }
            }
            for (int i = 0; i < 2; i++) b.Tube(R(Chest) + P(0f, 0.04f, 0.04f), R(Shoulder[i]), 0.016f * k, 0.014f * k, I(Chest), 4);

            // Neck and the small, too-tall skull; the jaw below it with fangs; fangs above.
            b.Tube(R(Neck1), R(Neck2), 0.03f * k, 0.028f * k, I(Neck1));
            b.Tube(R(Neck2), R(Head), 0.028f * k, 0.03f * k, I(Neck2));
            Vector3 skull = R(Head) + P(0f, 0.09f, 0.02f);
            b.Ellipsoid(skull, P(0.075f, 0.135f, 0.09f), I(Head), 8, 10);
            Vector3 jawTip = R(Jaw) + P(0f, -0.04f, 0.09f);
            b.Tube(R(Jaw) + P(-0.05f, 0f, -0.02f), jawTip, 0.018f * k, 0.01f * k, I(Jaw), 4);
            b.Tube(R(Jaw) + P(0.05f, 0f, -0.02f), jawTip, 0.018f * k, 0.01f * k, I(Jaw), 4);

            EyeLeft = Local(Head, skull + P(-0.032f, 0.03f, 0.082f));
            EyeRight = Local(Head, skull + P(0.032f, 0.03f, 0.082f));
            MouthFront = Local(Head, R(Jaw) + P(0f, 0f, 0.08f));
            ThroatCentre = Local(Head, R(Jaw) + P(0f, 0.01f, 0.03f));

            // Arms hanging past the knees, elbows bending the wrong way, long curling fingers.
            for (int i = 0; i < 2; i++)
            {
                b.Ellipsoid(R(Shoulder[i]), P(0.035f, 0.035f, 0.035f), I(Shoulder[i]), 4, 6);
                b.Tube(R(Shoulder[i]), R(Elbow[i]), 0.03f * k, 0.022f * k, I(Shoulder[i]));
                b.Ellipsoid(R(Elbow[i]), P(0.03f, 0.032f, 0.03f), I(Elbow[i]), 4, 6);
                b.Tube(R(Elbow[i]), R(Wrist[i]), 0.022f * k, 0.015f * k, I(Elbow[i]));
                for (int f = 0; f < Fingers; f++)
                {
                    b.Tube(R(Wrist[i]), R(Finger[i, f, 0]), 0.013f * k, 0.009f * k, I(Wrist[i]), 4);
                    for (int j = 0; j < FingerJoints; j++)
                    {
                        var joint = Finger[i, f, j];
                        Vector3 end = j + 1 < FingerJoints ? R(Finger[i, f, j + 1]) : R(joint) + (R(joint) - R(Finger[i, f, Mathf.Max(0, j - 1)])).normalized * 0.07f * k + P(0f, 0f, 0.02f);
                        b.Tube(R(joint), end, (0.008f - j * 0.002f) * k, (0.006f - j * 0.002f) * k + 0.0005f, I(joint), 4);
                    }
                }

                // Legs hang loose from the hips; the shins thin to nothing well above the ground.
                b.Tube(R(Thigh[i]), R(Knee[i]), 0.035f * k, 0.026f * k, I(Thigh[i]));
                b.Ellipsoid(R(Knee[i]), P(0.03f, 0.03f, 0.03f), I(Knee[i]), 4, 6);
                b.Tube(R(Knee[i]), R(Knee[i]) + P(0.01f * (i == 0 ? -1 : 1), -0.4f, -0.05f), 0.025f * k, 0.0005f, I(Knee[i]));
            }

            // Extra, thinner arms out of the ribs.
            for (int e = 0; e < ExtraShoulders.Count; e++)
            {
                var sh = ExtraShoulders[e];
                var el = ExtraElbows[e];
                b.Tube(R(sh), R(el), 0.016f * k, 0.012f * k, I(sh), 4);
                Vector3 hand = R(el) + P((R(el).x > 0 ? 0.03f : -0.03f), -0.32f, -0.08f);
                b.Tube(R(el), hand, 0.012f * k, 0.007f * k, I(el), 4);
                for (int f = 0; f < 3; f++)
                    b.Tube(hand, hand + P((f - 1) * 0.02f, -0.1f, 0.02f), 0.005f * k, 0.0005f, I(el), 3);
            }

            var binds = new Matrix4x4[bones.Count];
            for (int i = 0; i < bones.Count; i++) binds[i] = bones[i].worldToLocalMatrix * Root.localToWorldMatrix;
            return b.ToMesh("Scour", binds);
        }

        Transform Bone(string name, Transform parent, Vector3 rigPosition)
        {
            var t = new GameObject(name).transform;
            t.gameObject.layer = Layers.Entities;
            t.SetParent(parent, false);
            Vector3 parentRest = parent == Root ? Vector3.zero : rest[parent];
            t.localPosition = rigPosition - parentRest;
            bones.Add(t);
            rest[t] = rigPosition;
            return t;
        }

        // A point in rig space at this scour's scale.
        Vector3 P(float x, float y, float z) => new Vector3(x, y, z) * k;

        // A rig-space point in a bone's own space (bind pose: no rotations, so just the offset).
        Vector3 Local(Transform bone, Vector3 rigPoint) => rigPoint - rest[bone];

        static float Range(System.Random rng, float min, float max) => min + (float)rng.NextDouble() * (max - min);
    }
}
