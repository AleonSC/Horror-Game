using UnityEngine;

namespace Game.Entities
{
    /// <summary>The shapes a scour holds. Each creep shows it in a new one, so looking back is never the same.</summary>
    public enum ScourPose { Neutral, HeadTilt, Reach, Crouch, TurnedAway, ArmsUp, HangingHead, Peek, Lunge, PeekHidden }

    /// <summary>A pose: rotations (degrees) for the main bones, a drop of the whole body, and how curled the fingers are.</summary>
    public struct PoseShape
    {
        public Vector3 Hips, Spine, Chest, Neck, Head;
        public Vector3 ShoulderL, ShoulderR, ElbowL, ElbowR;
        public float Drop;      // metres the body sinks (crouching)
        public float Curl;      // 0 open, 1 clenched
        public Vector3 Extra;   // the extra arms

        /// <summary>The pose's shape. <paramref name="side"/> (-1 or 1) mirrors the one-sided poses.</summary>
        public static PoseShape For(ScourPose pose, int side)
        {
            float s = side < 0 ? -1f : 1f;
            // Left/right shoulder: positive Z swings an arm toward +x, so "out" is -Z for the left, +Z for the right.
            var p = new PoseShape { Spine = new Vector3(5f, 0f, 0f), ShoulderL = new Vector3(0f, 0f, -4f), ShoulderR = new Vector3(0f, 0f, 4f), Curl = 0.35f, Extra = new Vector3(-20f, 0f, 0f) };
            switch (pose)
            {
                case ScourPose.HeadTilt:
                    p.Head = new Vector3(5f, 0f, 38f * s);
                    p.Neck = new Vector3(10f, 0f, 8f * s);
                    break;
                case ScourPose.Reach: // one arm raised toward you, fingers spread
                    if (s > 0) p.ShoulderR = new Vector3(-75f, 0f, -8f); else p.ShoulderL = new Vector3(-75f, 0f, 8f);
                    if (s > 0) p.ElbowR = new Vector3(15f, 0f, 0f); else p.ElbowL = new Vector3(15f, 0f, 0f);
                    p.Head = new Vector3(-8f, 0f, 0f);
                    p.Curl = 0.1f;
                    break;
                case ScourPose.Crouch: // sunk low, hunched over, looking up at you
                    p.Drop = 0.35f;
                    p.Spine = new Vector3(28f, 0f, 0f);
                    p.Chest = new Vector3(12f, 0f, 0f);
                    p.Neck = new Vector3(-25f, 0f, 0f);
                    p.Head = new Vector3(-20f, 0f, 0f);
                    p.ShoulderL = new Vector3(-25f, 0f, -6f);
                    p.ShoulderR = new Vector3(-25f, 0f, 6f);
                    break;
                case ScourPose.TurnedAway: // its back to you, head turned right round over its shoulder
                    p.Hips = new Vector3(0f, 165f * s, 0f);
                    p.Neck = new Vector3(0f, -110f * s, 0f);
                    p.Head = new Vector3(0f, -55f * s, 12f * s);
                    break;
                case ScourPose.ArmsUp:
                    p.ShoulderL = new Vector3(-165f, 0f, 10f);
                    p.ShoulderR = new Vector3(-165f, 0f, -10f);
                    p.ElbowL = p.ElbowR = new Vector3(25f, 0f, 0f);
                    p.Curl = 0.75f;
                    p.Head = new Vector3(-10f, 0f, 0f);
                    break;
                case ScourPose.HangingHead: // head lolled down on its neck, limp
                    p.Neck = new Vector3(55f, 0f, 0f);
                    p.Head = new Vector3(40f, 0f, 10f * s);
                    p.Curl = 0.6f;
                    break;
                case ScourPose.Peek: // craning round a trunk: only the tilted head out past it, and one hand curled round its edge
                    return side < 0 ? Mirror(PeekLeft()) : PeekLeft();
                case ScourPose.PeekHidden: // tucked in behind a trunk, narrow, arms in close, so nothing shows past its edges
                    p.Spine = new Vector3(12f, 0f, 0f);
                    p.Chest = new Vector3(6f, 0f, 0f);
                    p.Neck = new Vector3(10f, 0f, 0f);
                    p.Head = new Vector3(-5f, 0f, 0f);
                    p.ShoulderL = new Vector3(-12f, 0f, 9f);
                    p.ShoulderR = new Vector3(-12f, 0f, -9f);
                    p.Curl = 0.5f;
                    p.Extra = new Vector3(-10f, 0f, 0f);
                    break;
                case ScourPose.Lunge: // thrown forward at you, arms out, fingers spread like claws
                    p.Spine = new Vector3(32f, 0f, 0f);
                    p.Neck = new Vector3(-15f, 0f, 0f);
                    p.Head = new Vector3(-22f, 0f, 0f);
                    p.ShoulderL = new Vector3(-85f, 0f, -18f);
                    p.ShoulderR = new Vector3(-85f, 0f, 18f);
                    p.ElbowL = p.ElbowR = new Vector3(-12f, 0f, 0f);
                    p.Curl = 0f;
                    p.Extra = new Vector3(-80f, 0f, 0f);
                    break;
            }
            return p;
        }

        // Peeking round the left side of a trunk just ahead of it, from right behind it (worked out against a
        // trunk 0.32 m across: the head ends up about 0.15 m past its edge, the left hand's fingers curled round
        // the edge at head height, and the right arm, the extra arms and the body all stay inside its outline;
        // each joint's lean undone further down, so nothing swings out the far side).
        static PoseShape PeekLeft() => new PoseShape
        {
            Hips = new Vector3(0f, 0f, 6f),
            Spine = new Vector3(10f, 0f, 22f),
            Chest = new Vector3(0f, 0f, 12f),
            Neck = new Vector3(0f, 0f, 26f),
            Head = new Vector3(0f, 0f, -30f),        // (tilted, looking round at you)
            ShoulderL = new Vector3(-104f, 34f, 33f),
            ElbowL = new Vector3(110f, 32f, -15f),
            ShoulderR = new Vector3(0f, 0f, -38f),   // (hanging straight down despite the lean)
            Curl = 0.85f,                            // (gripping)
            Extra = new Vector3(-20f, 0f, -28f),
        };

        /// <summary>The same pose on the other side (mirrored left to right).</summary>
        public static PoseShape Mirror(PoseShape p)
        {
            Vector3 M(Vector3 v) => new Vector3(v.x, -v.y, -v.z);
            return new PoseShape
            {
                Hips = M(p.Hips), Spine = M(p.Spine), Chest = M(p.Chest), Neck = M(p.Neck), Head = M(p.Head),
                ShoulderL = M(p.ShoulderR), ShoulderR = M(p.ShoulderL), ElbowL = M(p.ElbowR), ElbowR = M(p.ElbowL),
                Drop = p.Drop, Curl = p.Curl, Extra = M(p.Extra),
            };
        }

        /// <summary>Between two poses.</summary>
        public static PoseShape Lerp(PoseShape a, PoseShape b, float t)
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

        /// <summary>A random pose for after a creep (not the special ones: Peek and Lunge).</summary>
        public static ScourPose RandomCreepPose(System.Func<float> random)
        {
            ScourPose[] choices = { ScourPose.Neutral, ScourPose.HeadTilt, ScourPose.Reach, ScourPose.Crouch, ScourPose.TurnedAway, ScourPose.ArmsUp, ScourPose.HangingHead };
            return choices[Mathf.Min(choices.Length - 1, (int)(random() * choices.Length))];
        }
    }
}
