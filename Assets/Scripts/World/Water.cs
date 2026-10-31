using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where the water is: ponds (sunk into the ground, with a water level) and shallow puddles. Footsteps
/// splash in it, wading slows you down (FirstPersonController), and the splashing carries (Noise).
/// DarkForestGenerator fills it in when it builds the forest; WaterRipples animates the surfaces.
/// </summary>
public static class Water
{
    struct Pond { public Vector3 center; public float radius, level; }
    struct Puddle { public Vector3 center; public float radius; }

    static readonly List<Pond> ponds = new List<Pond>();
    static readonly List<Puddle> puddles = new List<Puddle>();

    public static void Clear()
    {
        ponds.Clear();
        puddles.Clear();
    }

    public static void AddPond(Vector3 center, float radius, float level) => ponds.Add(new Pond { center = center, radius = radius, level = level });
    /// <summary>How many ponds there are, and each one's middle (at its water level), radius and level.</summary>
    public static int PondCount => ponds.Count;
    public static void GetPond(int i, out Vector3 center, out float radius, out float level)
    {
        center = ponds[i].center;
        radius = ponds[i].radius;
        level = ponds[i].level;
    }

    public static void AddPuddle(Vector3 center, float radius) => puddles.Add(new Puddle { center = center, radius = radius });

    /// <summary>True if feet at this point are in water, and how deep (a puddle is ankle-deep at most).</summary>
    public static bool InWater(Vector3 feet, out float depth)
    {
        foreach (var pond in ponds)
        {
            Vector3 c = WorldWrap.NearestImage(pond.center, feet);
            float dx = feet.x - c.x, dz = feet.z - c.z;
            if (dx * dx + dz * dz < pond.radius * pond.radius * 1.1f && feet.y < pond.level) // (the water stops at the rim)
            {
                depth = pond.level - feet.y;
                return true;
            }
        }
        foreach (var puddle in puddles)
        {
            Vector3 c = WorldWrap.NearestImage(puddle.center, feet);
            float dx = feet.x - c.x, dz = feet.z - c.z;
            if (dx * dx + dz * dz < puddle.radius * puddle.radius)
            {
                depth = 0.03f;
                return true;
            }
        }
        depth = 0f;
        return false;
    }

    /// <summary>How close a point is to a pond: 1 over the water, falling to 0 `margin` metres beyond its edge.</summary>
    public static float NearPond(Vector3 p, float margin)
    {
        float best = 0f;
        foreach (var pond in ponds)
        {
            Vector3 c = WorldWrap.NearestImage(pond.center, p);
            float d = new Vector2(p.x - c.x, p.z - c.z).magnitude - pond.radius;
            best = Mathf.Max(best, 1f - Mathf.Clamp01(d / Mathf.Max(margin, 0.01f)));
        }
        return best;
    }

    /// <summary>How much wading slows you: knee-deep and more, a lot.</summary>
    public static float SpeedFactor(Vector3 feet) =>
        InWater(feet, out float depth) ? Mathf.Lerp(1f, 0.5f, Mathf.InverseLerp(0.1f, 0.6f, depth)) : 1f;
}

/// <summary>Slowly drifts the ripples across every water surface (they share one material).</summary>
public class WaterRipples : MonoBehaviour
{
    public Material material;
    public Vector2 drift = new Vector2(0.012f, 0.007f);
    Vector2 offset;

    void Update()
    {
        if (material == null) return;
        offset += drift * Time.deltaTime;
        material.mainTextureOffset = offset;
        if (material.HasProperty("_BumpMap")) material.SetTextureOffset("_BumpMap", offset);
    }
}
