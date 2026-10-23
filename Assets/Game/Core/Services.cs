using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// Where systems find each other (the event bus, the current run, configs...). The only global in the game:
    /// it's emptied when Play starts, so nothing survives from a previous Play session even with domain reload
    /// turned off.
    /// </summary>
    public static class Services
    {
        static readonly Dictionary<Type, object> registry = new Dictionary<Type, object>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => registry.Clear();

        public static void Register<T>(T service) where T : class
        {
            if (service == null) throw new ArgumentNullException(nameof(service));
            registry[typeof(T)] = service;
        }

        /// <summary>Remove <paramref name="service"/>, but only if it's still the one registered.</summary>
        public static void Unregister<T>(T service) where T : class
        {
            if (registry.TryGetValue(typeof(T), out var current) && ReferenceEquals(current, service))
                registry.Remove(typeof(T));
        }

        public static bool TryGet<T>(out T service) where T : class
        {
            if (registry.TryGetValue(typeof(T), out var s) && !IsDestroyed(s))
            {
                service = (T)s;
                return true;
            }
            service = null;
            return false;
        }

        public static T Get<T>() where T : class
        {
            if (TryGet<T>(out var service)) return service;
            throw new InvalidOperationException($"No {typeof(T).Name} is registered. Is the GameRoot running?");
        }

        /// <summary>Empty the registry (tests use this).</summary>
        public static void Clear() => registry.Clear();

        // A destroyed Unity object still exists as a C# object; treat it as gone.
        static bool IsDestroyed(object o) => o is UnityEngine.Object uo && uo == null;
    }
}
