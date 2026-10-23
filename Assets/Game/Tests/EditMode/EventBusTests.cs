using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Game.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests
{
    public class EventBusTests
    {
        [Test]
        public void Publish_ReachesSubscribersOfThatType()
        {
            var bus = new EventBus();
            int lamps = 0, caught = 0;
            bus.Subscribe<LampLit>(e => lamps += e.TimesLit);
            bus.Subscribe<PlayerCaught>(_ => caught++);

            bus.Publish(new LampLit(0, 3, Vector3.zero));

            Assert.AreEqual(3, lamps);
            Assert.AreEqual(0, caught);
        }

        [Test]
        public void Dispose_StopsDelivery()
        {
            var bus = new EventBus();
            int count = 0;
            var subscription = bus.Subscribe<GamePaused>(_ => count++);
            bus.Publish(new GamePaused());
            subscription.Dispose();
            subscription.Dispose(); // twice is harmless
            bus.Publish(new GamePaused());

            Assert.AreEqual(1, count);
            Assert.AreEqual(0, bus.SubscriberCount<GamePaused>());
        }

        [Test]
        public void SubscribeAll_SeesEveryEventInOrder()
        {
            var bus = new EventBus();
            var seen = new List<string>();
            bus.SubscribeAll(e => seen.Add(e.GetType().Name));

            bus.Publish(new GamePaused());
            bus.Publish(new GameResumed());

            CollectionAssert.AreEqual(new[] { nameof(GamePaused), nameof(GameResumed) }, seen);
        }

        [Test]
        public void ThrowingHandler_IsLogged_AndOthersStillRun()
        {
            var bus = new EventBus();
            bool secondRan = false;
            bus.Subscribe<GamePaused>(_ => throw new InvalidOperationException("boom"));
            bus.Subscribe<GamePaused>(_ => secondRan = true);

            LogAssert.Expect(LogType.Exception, new Regex("boom"));
            bus.Publish(new GamePaused());

            Assert.IsTrue(secondRan);
        }

        [Test]
        public void Unsubscribing_DuringDelivery_IsSafe()
        {
            var bus = new EventBus();
            int a = 0, b = 0;
            IDisposable first = null;
            first = bus.Subscribe<GamePaused>(_ => { a++; first.Dispose(); });
            bus.Subscribe<GamePaused>(_ => b++);

            bus.Publish(new GamePaused());
            bus.Publish(new GamePaused());

            Assert.AreEqual(1, a);
            Assert.AreEqual(2, b);
        }

        [Test]
        public void Subscribing_DuringDelivery_StartsWithTheNextEvent()
        {
            var bus = new EventBus();
            int late = 0;
            bool added = false;
            bus.Subscribe<GamePaused>(_ =>
            {
                if (added) return;
                added = true;
                bus.Subscribe<GamePaused>(__ => late++);
            });

            bus.Publish(new GamePaused());
            Assert.AreEqual(0, late);
            bus.Publish(new GamePaused());
            Assert.AreEqual(1, late);
        }
    }
}
