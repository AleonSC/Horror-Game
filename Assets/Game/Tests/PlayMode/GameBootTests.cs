using System.Collections;
using Game.Audio;
using Game.Core;
using Game.DevTools;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    /// <summary>Enter Play mode and check the game starts up cleanly: the M0 smoke test.</summary>
    public class GameBootTests
    {
        [UnityTest]
        public IEnumerator GameStarts_AndRunsWithoutErrors()
        {
            // Let the game create itself and run for a moment.
            for (int i = 0; i < 30; i++) yield return null;

            Assert.IsTrue(Services.TryGet<GameRoot>(out var root), "GameRoot wasn't created");
            Assert.IsNotNull(root.Run, "No run started");
            Assert.Greater(root.Run.Elapsed, 0f, "Run time isn't counting");
            Assert.IsTrue(Services.TryGet<AudioStateController>(out _), "Sound controller wasn't attached");
            Assert.IsTrue(Services.TryGet<SessionRecorder>(out _), "Session recorder wasn't attached");
            Assert.IsTrue(Services.TryGet<DevWindow>(out _), "Dev window wasn't attached");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator Restart_StartsAFreshRun_AndIsRecorded()
        {
            yield return null;
            var root = Services.Get<GameRoot>();
            var recorder = Services.Get<SessionRecorder>();
            int before = root.Run.RunNumber;

            root.Restart();
            yield return null;

            Assert.AreEqual(before + 1, root.Run.RunNumber);
            Assert.AreSame(root.Run, Services.Get<GameRun>());
            StringAssert.Contains($"Run {before} reset", recorder.Log.ToText());
            StringAssert.Contains($"Run {before + 1} started", recorder.Log.ToText());
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator SoundState_FollowsEvents()
        {
            yield return null;
            var root = Services.Get<GameRoot>();
            var audio = Services.Get<AudioStateController>();

            root.Events.Publish(new EnteredShelter(99, "Test shack"));
            Assert.AreEqual(AudioState.Sheltered, audio.Current);
            root.Events.Publish(new ExitedShelter(99, "Test shack"));
            Assert.AreEqual(AudioState.Outdoors, audio.Current);
            yield return null;
            LogAssert.NoUnexpectedReceived();
        }
    }
}
