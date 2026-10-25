using Game.Entities;
using NUnit.Framework;

namespace Game.Tests
{
    public class ScourPoseTests
    {
        [Test]
        public void CreepPoses_NeverPeekOrLunge()
        {
            for (int i = 0; i < 100; i++)
            {
                float r = i / 100f;
                var pose = PoseShape.RandomCreepPose(() => r);
                Assert.AreNotEqual(ScourPose.Peek, pose);
                Assert.AreNotEqual(ScourPose.Lunge, pose);
                Assert.AreNotEqual(ScourPose.PeekHidden, pose);
            }
            Assert.AreEqual(ScourPose.HangingHead, PoseShape.RandomCreepPose(() => 0.99999f));
        }

        [Test]
        public void OneSidedPoses_Mirror()
        {
            var left = PoseShape.For(ScourPose.HeadTilt, -1);
            var right = PoseShape.For(ScourPose.HeadTilt, 1);
            Assert.AreEqual(-left.Head.z, right.Head.z, 1e-4f);
        }

        [Test]
        public void Peek_OnTheRight_IsThePeekOnTheLeftMirrored()
        {
            var left = PoseShape.For(ScourPose.Peek, 1);
            var right = PoseShape.For(ScourPose.Peek, -1);
            // The arm that grips the trunk swaps sides; leans and tilts turn the other way.
            Assert.AreEqual(left.ShoulderL.x, right.ShoulderR.x, 1e-4f);
            Assert.AreEqual(-left.ShoulderL.y, right.ShoulderR.y, 1e-4f);
            Assert.AreEqual(-left.ShoulderL.z, right.ShoulderR.z, 1e-4f);
            Assert.AreEqual(-left.Spine.z, right.Spine.z, 1e-4f);
            Assert.AreEqual(left.Spine.x, right.Spine.x, 1e-4f);
            Assert.AreEqual(left.Curl, right.Curl, 1e-4f);
        }
    }
}
