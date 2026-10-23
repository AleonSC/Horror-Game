using System.Collections.Generic;
using Game.Audio;
using Game.Core;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class AudioStateTests
    {
        EventBus bus;
        AudioStateTracker tracker;
        List<AudioStateChanged> published;

        [SetUp]
        public void SetUp()
        {
            bus = new EventBus();
            published = new List<AudioStateChanged>();
            bus.Subscribe<AudioStateChanged>(published.Add);
            tracker = new AudioStateTracker(bus);
        }

        [TearDown]
        public void TearDown() => tracker.Dispose();

        [Test]
        public void StartsOutdoors() => Assert.AreEqual(AudioState.Outdoors, tracker.Current);

        [Test]
        public void Resolve_StrongestStateWins()
        {
            var all = new AudioStateInputs { Paused = true, InDarkRoom = true, Sheltered = true, Threat = ThreatLevel.Hush };
            Assert.AreEqual(AudioState.Paused, AudioStateRules.Resolve(all));
            all.Paused = false;
            Assert.AreEqual(AudioState.DarkRoom, AudioStateRules.Resolve(all));
            all.InDarkRoom = false;
            Assert.AreEqual(AudioState.Hush, AudioStateRules.Resolve(all));
            all.Threat = ThreatLevel.Dread;
            Assert.AreEqual(AudioState.Dread, AudioStateRules.Resolve(all));
            all.Threat = ThreatLevel.None;
            Assert.AreEqual(AudioState.Sheltered, AudioStateRules.Resolve(all));
            all.Sheltered = false;
            Assert.AreEqual(AudioState.Outdoors, AudioStateRules.Resolve(all));
        }

        [Test]
        public void Shelter_EnterAndLeave()
        {
            bus.Publish(new EnteredShelter(1, "Shack"));
            Assert.AreEqual(AudioState.Sheltered, tracker.Current);
            bus.Publish(new ExitedShelter(1, "Shack"));
            Assert.AreEqual(AudioState.Outdoors, tracker.Current);
        }

        [Test]
        public void OverlappingShelters_StayShelteredUntilLeavingBoth()
        {
            bus.Publish(new EnteredShelter(1, "Cabin"));
            bus.Publish(new EnteredShelter(2, "Cabin room"));
            bus.Publish(new ExitedShelter(1, "Cabin"));
            Assert.AreEqual(AudioState.Sheltered, tracker.Current);
            bus.Publish(new ExitedShelter(2, "Cabin room"));
            Assert.AreEqual(AudioState.Outdoors, tracker.Current);
        }

        [Test]
        public void LeavingAShelterTwice_DoesNothingStrange()
        {
            bus.Publish(new ExitedShelter(5, "Tent"));
            Assert.AreEqual(AudioState.Outdoors, tracker.Current);
            Assert.AreEqual(0, published.Count);
        }

        [Test]
        public void Threat_OverridesShelter_ThenFallsBack()
        {
            bus.Publish(new EnteredShelter(1, "Shack"));
            bus.Publish(new ThreatLevelChanged(ThreatLevel.Dread));
            Assert.AreEqual(AudioState.Dread, tracker.Current);
            bus.Publish(new ThreatLevelChanged(ThreatLevel.Hush));
            Assert.AreEqual(AudioState.Hush, tracker.Current);
            bus.Publish(new ThreatLevelChanged(ThreatLevel.None));
            Assert.AreEqual(AudioState.Sheltered, tracker.Current);
        }

        [Test]
        public void DarkRoom_AndPause()
        {
            bus.Publish(new EnteredDarkRoom());
            Assert.AreEqual(AudioState.DarkRoom, tracker.Current);
            bus.Publish(new GamePaused());
            Assert.AreEqual(AudioState.Paused, tracker.Current);
            bus.Publish(new GameResumed());
            Assert.AreEqual(AudioState.DarkRoom, tracker.Current);
            bus.Publish(new ExitedDarkRoom());
            Assert.AreEqual(AudioState.Outdoors, tracker.Current);
        }

        [Test]
        public void RunReset_ClearsShelterDarkRoomAndThreat_ButNotPause()
        {
            bus.Publish(new EnteredShelter(1, "Tent"));
            bus.Publish(new EnteredDarkRoom());
            bus.Publish(new ThreatLevelChanged(ThreatLevel.Hush));
            bus.Publish(new GamePaused());

            bus.Publish(new RunReset(1));
            Assert.AreEqual(AudioState.Paused, tracker.Current);
            bus.Publish(new GameResumed());
            Assert.AreEqual(AudioState.Outdoors, tracker.Current);
        }

        [Test]
        public void Changes_ArePublished_OnlyWhenTheStateChanges()
        {
            bus.Publish(new EnteredShelter(1, "Shack"));
            bus.Publish(new EnteredShelter(2, "Shack loft"));
            bus.Publish(new ExitedShelter(2, "Shack loft"));

            Assert.AreEqual(1, published.Count);
            Assert.AreEqual(AudioState.Outdoors, published[0].From);
            Assert.AreEqual(AudioState.Sheltered, published[0].To);
        }

        [Test]
        public void Config_GivesEachStateItsFadeTime()
        {
            var config = ScriptableObject.CreateInstance<AudioConfig>();
            try
            {
                config.toSheltered = 0.6f;
                config.toDarkRoom = 2f;
                config.toPaused = 0f;
                Assert.AreEqual(0.6f, config.FadeSeconds(AudioState.Sheltered));
                Assert.AreEqual(2f, config.FadeSeconds(AudioState.DarkRoom));
                Assert.AreEqual(0f, config.FadeSeconds(AudioState.Paused));
            }
            finally { Object.DestroyImmediate(config); }
        }

        [Test]
        public void VolumeSlider_ToDecibels()
        {
            Assert.AreEqual(0f, AudioStateController.ToDecibels(1f), 1e-4f);
            Assert.AreEqual(-6.02f, AudioStateController.ToDecibels(0.5f), 0.01f);
            Assert.AreEqual(-80f, AudioStateController.ToDecibels(0f));
        }
    }
}
