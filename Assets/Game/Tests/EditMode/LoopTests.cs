using Game.Core;
using Game.Entities;
using Game.Player;
using NUnit.Framework;

namespace Game.Tests
{
    public class InventoryTests
    {
        [Test]
        public void StartsWithJustTheFlashlight_Held()
        {
            var inv = new Inventory();
            Assert.AreEqual(ItemKind.Flashlight, inv.HeldKind);
            Assert.AreEqual(1, inv.CountOf(ItemKind.Flashlight));
            for (int i = 1; i < Inventory.SlotCount; i++) Assert.IsTrue(inv[i].Empty);
        }

        [Test]
        public void BatteriesAndGlowsticks_StackInOneSlot()
        {
            var inv = new Inventory();
            inv.Add(ItemKind.Batteries, 2);
            inv.Add(ItemKind.Batteries, 1);
            inv.Add(ItemKind.Glowsticks, 10);
            Assert.AreEqual(3, inv.CountOf(ItemKind.Batteries));
            Assert.AreEqual(1, inv.Find(ItemKind.Batteries));
            Assert.AreEqual(2, inv.Find(ItemKind.Glowsticks));
            Assert.IsTrue(inv[3].Empty);
        }

        [Test]
        public void SingleItems_TakeASlotEach_UntilFull()
        {
            var inv = new Inventory();
            for (int i = 0; i < 7; i++) Assert.IsTrue(inv.Add(ItemKind.Lighter));
            Assert.IsFalse(inv.Add(ItemKind.Torch));
            Assert.IsFalse(inv.HasRoomFor(ItemKind.Torch));
            Assert.AreEqual(7, inv.CountOf(ItemKind.Lighter));
        }

        [Test]
        public void TakingTheLast_EmptiesTheSlot()
        {
            var inv = new Inventory();
            inv.Add(ItemKind.Batteries, 1);
            inv.Take(1);
            Assert.IsTrue(inv[1].Empty);
            Assert.AreEqual(-1, inv.Find(ItemKind.Batteries));
        }

        [Test]
        public void Wheel_SkipsEmptySlots_AndWraps()
        {
            var inv = new Inventory();
            inv.Add(ItemKind.Torch);        // slot 1
            inv.Add(ItemKind.Camera);       // slot 2
            inv.Cycle(1);
            Assert.AreEqual(1, inv.Selected);
            inv.Cycle(1);
            Assert.AreEqual(2, inv.Selected);
            inv.Cycle(1);
            Assert.AreEqual(0, inv.Selected); // (round to the flashlight)
            inv.Cycle(-1);
            Assert.AreEqual(2, inv.Selected);
        }

        [Test]
        public void Reset_GoesBackToTheStart()
        {
            var inv = new Inventory();
            inv.Add(ItemKind.Torch);
            inv.Select(1);
            inv.Reset();
            Assert.AreEqual(0, inv.Selected);
            Assert.AreEqual(-1, inv.Find(ItemKind.Torch));
        }
    }

    public class DifficultyTests
    {
        [Test]
        public void Presets_MatchThePlan()
        {
            var easy = DifficultySettings.Easy();
            var hard = DifficultySettings.Hard();
            Assert.AreEqual(1.5f, easy.batteryLife);
            Assert.AreEqual(1.35f, easy.crankSpeed);
            Assert.AreEqual(3, easy.crawlersFromLamp);
            Assert.AreEqual(0.75f, hard.batteryLife);
            Assert.AreEqual(1.4f, hard.extraScours);
            Assert.AreEqual(1, hard.crawlersFromLamp);
            Assert.AreEqual(1f, DifficultySettings.Normal().faceDownTime);
        }

        [Test]
        public void Custom_UsesThePlayersOwnValues()
        {
            var prefs = new PlayerPreferences();
            var d = new Difficulty(prefs);
            Assert.AreEqual("Normal", d.Level);
            d.Level = "Custom";
            prefs.customDifficulty.crankSpeed = 2f;
            Assert.AreEqual(2f, d.Current.crankSpeed);
            d.Level = "Hard";
            Assert.AreEqual(0.8f, d.Current.crankSpeed);
            d.Level = "Nonsense";
            Assert.AreEqual("Hard", d.Level);
        }
    }

    public class ScourCountTests
    {
        static readonly ScourSettings S = new ScourSettings();

        [Test]
        public void OneAfterTheCalm_ThenOneAMinute_ForTenMinutes()
        {
            Assert.AreEqual(0, ScourCount.FromClock(30f, S, 1f, 1f));
            Assert.AreEqual(0, ScourCount.FromClock(60f + 59f, S, 1f, 1f));
            Assert.AreEqual(1, ScourCount.FromClock(60f + 60f, S, 1f, 1f));
            Assert.AreEqual(10, ScourCount.FromClock(60f + 600f, S, 1f, 1f));
            Assert.AreEqual(10, ScourCount.FromClock(5000f, S, 1f, 1f));
        }

        [Test]
        public void PlusOnePerLamp_NoCap()
        {
            Assert.AreEqual(1, ScourCount.Total(0f, 0, S, 1f, 1f));
            Assert.AreEqual(1 + 10 + 10, ScourCount.Total(5000f, 10, S, 1f, 1f));
        }

        [Test]
        public void Difficulty_ScalesTheClock()
        {
            // Easy: every 90 s, 0.6 as many (6 of the 10).
            Assert.AreEqual(0, ScourCount.FromClock(60f + 89f, S, 1.5f, 0.6f));
            Assert.AreEqual(6, ScourCount.FromClock(60f + 900f, S, 1.5f, 0.6f));
            // Hard: every 42 s, 1.4 as many (14).
            Assert.AreEqual(14, ScourCount.FromClock(60f + 420f, S, 0.7f, 1.4f));
        }
    }
}
