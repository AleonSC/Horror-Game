using Game.DevTools;
using NUnit.Framework;

namespace Game.Tests
{
    public class SessionTimelineTests
    {
        // A 100 s run: scours out 10-30 s and hunting 20-30; the blind one hunting 25-40; still 0-50, walking after.
        static SessionTimeline Sample()
        {
            var t = new SessionTimeline();
            t.ThreatLane("Scours");
            t.ThreatLane("Blind one");
            t.ThreatLane("Giant", oneIsAmbient: true);
            t.LaneFor(SessionTimeline.MoveLane, SessionTimeline.Player, SessionTimeline.MoveValues);
            t.Set("Scours", 0f, 0); t.Set("Scours", 10f, 1); t.Set("Scours", 20f, 2); t.Set("Scours", 30f, 0);
            t.Set("Blind one", 25f, 2); t.Set("Blind one", 40f, 0);
            t.Set("Giant", 50f, 1); // (background: never counts)
            t.Set(SessionTimeline.MoveLane, 0f, 0); t.Set(SessionTimeline.MoveLane, 50f, 1);
            t.Mark(15f, SessionTimeline.MarkKind.Scare, "phantom");
            t.Mark(60f, SessionTimeline.MarkKind.Lamp, "Lamp 3 lit (1 of 10)");
            t.Mark(70f, SessionTimeline.MarkKind.Caught, "Caught by Scour #1");
            t.Extend(100f);
            return t;
        }

        [Test]
        public void Analyse_SharesOfTheRun()
        {
            var s = Sample().Analyse();
            Assert.AreEqual(100f, s.Duration, 1e-3f);
            Assert.AreEqual(0.20f, s.Hunted, 1e-3f);   // 20-40
            Assert.AreEqual(0.30f, s.Pressure, 1e-3f); // 10-40 (the giant's background doesn't count)
            Assert.AreEqual(0.05f, s.Overlap, 1e-3f);  // 25-30
            Assert.AreEqual(0.50f, s.Still, 1e-3f);
            Assert.AreEqual(60f, s.LongestQuiet, 1e-3f); // 40-100
            Assert.AreEqual(1, s.QuietStretches);        // (0-10 is too short to count)
            Assert.AreEqual(1, s.Scares);
            Assert.AreEqual(1, s.Caught);
            Assert.AreEqual(1, s.Lamps);
        }

        [Test]
        public void Analyse_SplitsTheRunAtEachLamp()
        {
            var s = Sample().Analyse();
            Assert.AreEqual(2, s.Chapters.Count);
            Assert.AreEqual(60f, s.Chapters[0].Length, 1e-3f);
            Assert.AreEqual(20f / 60f, s.Chapters[0].Hunted, 1e-3f);
            Assert.AreEqual(1, s.Chapters[0].Scares);
            Assert.AreEqual(1, s.Chapters[1].Caught);
            Assert.AreEqual(0f, s.Chapters[1].Hunted, 1e-3f);
        }

        [Test]
        public void Lane_KeepsOnlyChanges_AndReadsBack()
        {
            var t = new SessionTimeline();
            var lane = t.ThreatLane("Scours");
            lane.Set(0f, 0); lane.Set(0.5f, 0); lane.Set(1f, 2); lane.Set(1.5f, 2); lane.Set(3f, 0);
            Assert.AreEqual(3, lane.Count);
            Assert.AreEqual(0, lane.At(0.9f));
            Assert.AreEqual(2, lane.At(2f));
            Assert.AreEqual(0, lane.At(5f));
        }

        [Test]
        public void PressureCurve_PeaksWhereItsHunted()
        {
            var curve = Sample().PressureCurve(10f);
            Assert.AreEqual(10, curve.Length);
            Assert.AreEqual(0f, curve[0], 1e-3f);
            Assert.AreEqual(2f / 3f, curve[2], 1e-3f); // 20-30: hunting
            Assert.AreEqual(0f, curve[6], 1e-3f);      // the giant far off: background
        }

        [Test]
        public void Report_IsOnePageWithTheNumbers()
        {
            var html = TimelineReport.ToHtml(Sample(), "Test run");
            StringAssert.StartsWith("<!doctype html>", html);
            StringAssert.Contains("<svg", html);
            StringAssert.Contains("20%", html);       // hunted
            StringAssert.Contains("Lamp by lamp", html);
            StringAssert.DoesNotContain("<script", html);
        }

        [TestCase(0f, "0:00")]
        [TestCase(75.4f, "1:15")]
        [TestCase(3725f, "1:02:05")]
        public void Time(float seconds, string expected) => Assert.AreEqual(expected, TimelineReport.Time(seconds));
    }
}
