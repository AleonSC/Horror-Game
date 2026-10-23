using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>Marker for anything published on the <see cref="EventBus"/>. Events are small readonly structs.</summary>
    public interface IGameEvent { }

    /// <summary>
    /// Game events go through here: systems publish what happened and others react, instead of polling each
    /// other. One bus lives for the whole session (owned by <see cref="GameRoot"/>); runs come and go on it.
    /// Handlers can subscribe or unsubscribe while an event is being delivered, and a handler that throws is
    /// logged without stopping the others.
    /// </summary>
    public sealed class EventBus
    {
        abstract class Channel
        {
            public abstract int Count { get; }
        }

        // Handlers are kept in an array that's replaced on every change, so delivery can iterate it safely
        // while handlers subscribe or unsubscribe.
        sealed class Channel<T> : Channel where T : struct, IGameEvent
        {
            public Action<T>[] Handlers = Array.Empty<Action<T>>();
            public override int Count => Handlers.Length;
        }

        readonly Dictionary<Type, Channel> channels = new Dictionary<Type, Channel>();
        Action<IGameEvent>[] anyHandlers = Array.Empty<Action<IGameEvent>>();

        /// <summary>Call <paramref name="handler"/> whenever a <typeparamref name="T"/> is published. Dispose the result to stop.</summary>
        public IDisposable Subscribe<T>(Action<T> handler) where T : struct, IGameEvent
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            var channel = GetChannel<T>();
            channel.Handlers = Added(channel.Handlers, handler);
            return new Subscription(() => channel.Handlers = Removed(channel.Handlers, handler));
        }

        /// <summary>Call <paramref name="handler"/> for every event of any type (the session recorder uses this).</summary>
        public IDisposable SubscribeAll(Action<IGameEvent> handler)
        {
            if (handler == null) throw new ArgumentNullException(nameof(handler));
            anyHandlers = Added(anyHandlers, handler);
            return new Subscription(() => anyHandlers = Removed(anyHandlers, handler));
        }

        public void Publish<T>(T gameEvent) where T : struct, IGameEvent
        {
            if (channels.TryGetValue(typeof(T), out var c))
            {
                foreach (var handler in ((Channel<T>)c).Handlers)
                {
                    try { handler(gameEvent); }
                    catch (Exception e) { Debug.LogException(e); }
                }
            }

            var any = anyHandlers;
            if (any.Length == 0) return;
            IGameEvent boxed = gameEvent;
            foreach (var handler in any)
            {
                try { handler(boxed); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }

        /// <summary>How many handlers listen for <typeparamref name="T"/> specifically (not counting SubscribeAll).</summary>
        public int SubscriberCount<T>() where T : struct, IGameEvent =>
            channels.TryGetValue(typeof(T), out var c) ? c.Count : 0;

        Channel<T> GetChannel<T>() where T : struct, IGameEvent
        {
            if (!channels.TryGetValue(typeof(T), out var c))
            {
                c = new Channel<T>();
                channels.Add(typeof(T), c);
            }
            return (Channel<T>)c;
        }

        static TH[] Added<TH>(TH[] handlers, TH handler)
        {
            var result = new TH[handlers.Length + 1];
            Array.Copy(handlers, result, handlers.Length);
            result[handlers.Length] = handler;
            return result;
        }

        static TH[] Removed<TH>(TH[] handlers, TH handler) where TH : class
        {
            int index = Array.IndexOf(handlers, handler);
            if (index < 0) return handlers;
            var result = new TH[handlers.Length - 1];
            Array.Copy(handlers, 0, result, 0, index);
            Array.Copy(handlers, index + 1, result, index, handlers.Length - index - 1);
            return result;
        }

        sealed class Subscription : IDisposable
        {
            Action unsubscribe;
            public Subscription(Action unsubscribe) => this.unsubscribe = unsubscribe;

            public void Dispose()
            {
                unsubscribe?.Invoke();
                unsubscribe = null;
            }
        }
    }
}
