using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Every light the entities fear: lit lamps, floodlights, car headlights, and the torch or lighter in the player's hand.
/// Inside one, the player can't be harmed, and entities won't step into it.
/// </summary>
public static class SafeZones
{
    public interface ISource
    {
        bool IsActive { get; }
        Vector3 Center { get; }
        float Radius { get; }
    }

    /// <summary>
    /// A safe zone that isn't a circle (a headlight beam). Center and Radius still give a rough circle
    /// around it, which entities use to circle it; Contains decides what's actually inside.
    /// </summary>
    public interface IShapedSource : ISource
    {
        bool Contains(Vector3 point);
    }

    static readonly List<ISource> sources = new List<ISource>();

    public static void Register(ISource source)
    {
        if (!sources.Contains(source)) sources.Add(source);
    }

    public static void Unregister(ISource source) => sources.Remove(source);

    /// <summary>True if the point is inside any active safe zone.</summary>
    public static bool Contains(Vector3 point) => Contains(point, out _);

    /// <summary>True if the point is inside any active safe zone, and which one (the largest if several).</summary>
    public static bool Contains(Vector3 point, out ISource source)
    {
        source = null;
        foreach (var s in sources)
        {
            if (!s.IsActive) continue;
            bool inside;
            if (s is IShapedSource shaped) inside = shaped.Contains(point);
            else
            {
                Vector3 d = point - s.Center;
                d.y = 0f;
                inside = d.sqrMagnitude < s.Radius * s.Radius;
            }
            if (inside && (source == null || s.Radius > source.Radius))
                source = s;
        }
        return source != null;
    }
}
