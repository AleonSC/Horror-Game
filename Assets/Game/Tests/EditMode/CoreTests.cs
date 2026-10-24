using System.IO;
using Game.Core;
using Game.DevTools;
using NUnit.Framework;
using UnityEngine;

namespace Game.Tests
{
    public class GameRunTests
    {
        [Test]
        public void Tick_AddsTime_IgnoringNegatives()
        {
            var run = new GameRun(1, 42);
            run.Tick(0.5f);
            run.Tick(-1f);
            run.Tick(0.25f);
            Assert.AreEqual(0.75f, run.Elapsed, 1e-5f);
            Assert.AreEqual(42, run.Seed);
        }

        [Test]
        public void LampsLit_StopsAtTen()
        {
            var run = new GameRun(1, 0);
            for (int i = 0; i < 12; i++) run.RecordLampLit();
            Assert.AreEqual(GameRun.LampCount, run.LampsLit);
        }
    }

    public class PlayerPreferencesTests
    {
        string folder;

        [SetUp]
        public void SetUp()
        {
            folder = Path.Combine(Path.GetTempPath(), "GamePrefsTest_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(folder)) Directory.Delete(folder, true);
        }

        [Test]
        public void SaveThenLoad_KeepsValues()
        {
            var path = Path.Combine(folder, "settings.json");
            var prefs = new PlayerPreferences { masterVolume = 0.3f, mouseSensitivity = 2.5f, classicLook = true, difficulty = "Hard" };
            Assert.IsTrue(prefs.Save(path));

            var loaded = PlayerPreferences.Load(path);
            Assert.AreEqual(0.3f, loaded.masterVolume, 1e-5f);
            Assert.AreEqual(2.5f, loaded.mouseSensitivity, 1e-5f);
            Assert.IsTrue(loaded.classicLook);
            Assert.AreEqual("Hard", loaded.difficulty);
        }

        [Test]
        public void MissingFile_GivesDefaults()
        {
            var loaded = PlayerPreferences.Load(Path.Combine(folder, "nothing.json"));
            Assert.AreEqual(1f, loaded.masterVolume);
            Assert.AreEqual("Normal", loaded.difficulty);
        }

        [Test]
        public void BrokenFile_GivesDefaults()
        {
            var path = Path.Combine(folder, "settings.json");
            File.WriteAllText(path, "{ this is not json");
            var loaded = PlayerPreferences.Load(path);
            Assert.AreEqual(1f, loaded.masterVolume);
        }
    }

    public class SessionLogTests
    {
        [TestCase(0f, "0:00.0")]
        [TestCase(7.46f, "0:07.4")]
        [TestCase(187.4f, "3:07.4")]
        [TestCase(-3f, "0:00.0")]
        public void FormatTime(float seconds, string expected) => Assert.AreEqual(expected, SessionLog.FormatTime(seconds));

        [Test]
        public void ToText_HasHeaderAndTimestampedLines()
        {
            var log = new SessionLog { Header = "Session test" };
            log.Add(1f, "Run 1 started (seed 5)");
            log.Add(65.5f, "Lamp 3 lit (1 of 10)");

            var text = log.ToText();
            StringAssert.StartsWith("Session test", text);
            StringAssert.Contains("[0:01.0] Run 1 started (seed 5)", text);
            StringAssert.Contains("[1:05.5] Lamp 3 lit (1 of 10)", text);
        }

        [Test]
        public void OldestLinesDrop_PastTheLimit()
        {
            var log = new SessionLog();
            for (int i = 0; i < SessionLog.MaxEntries + 5; i++) log.Add(i, "line " + i);
            Assert.AreEqual(SessionLog.MaxEntries, log.Count);
            Assert.AreEqual("line 5", log.Entries[0].Text);
        }

        [Test]
        public void Clear_EmptiesIt()
        {
            var log = new SessionLog();
            log.Add(0f, "x");
            log.Clear();
            Assert.AreEqual(0, log.Count);
        }
    }

    public class SafeZoneShapeTests
    {
        [Test]
        public void Beam_IsABoxAhead()
        {
            // Car headlights: 16 m long, 2.6 m each side, pointing along +x from (0, 0).
            var z = new SafeZone("Headlights", new Vector3(0f, 0f, 0f), 8f) { Beam = true, Forward = new Vector3(1f, 0f, 0f), Length = 16f, HalfWidth = 2.6f };
            Assert.IsTrue(z.Contains(new Vector3(8f, 0f, 1f)));
            Assert.IsTrue(z.Contains(new Vector3(15.9f, 0f, -2.5f)));
            Assert.IsFalse(z.Contains(new Vector3(-1f, 0f, 0f)));   // behind the car
            Assert.IsFalse(z.Contains(new Vector3(8f, 0f, 3f)));    // off to the side
            Assert.IsFalse(z.Contains(new Vector3(17f, 0f, 0f)));   // past the end
        }
    }
}
