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
    }
}
