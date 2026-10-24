using System.Collections.Generic;
using UnityEngine;

namespace Game.World.Generation
{
    public enum ClearingKind { Start, Lamp, Glade, OldTree, Structure }
    public enum StructureKind { Shack, Cabin, Campsite, Watchtower, Car, LandmarkTree }
    public enum TreeKind { Pine, Dead }
    public enum BushKind { Mound, Upright, Leaning }

    /// <summary>A round open space with a ragged edge; trees thin back in over <see cref="Thinning"/> of its radius.</summary>
    /// <summary>What can be found in the places (section 3.7). Snacks are eaten where they're found.</summary>
    public enum LootKind { Battery, Torch, Lighter, KeyFob, Snack, FlareGun, Glowsticks, Camera, Map, Radio }
    /// <summary>Where in a place it is.</summary>
    public enum LootPlace { Table, Tent, Backpack, GloveBox, Trunk, Deck, BackSeat }

    /// <summary>Something to find: which place (an index into the plan's structures), where in it, what and how many.</summary>
    public struct Loot
    {
        public int Structure;
        public LootPlace Place;
        public LootKind Kind;
        public int Count;
    }

    public sealed class Clearing
    {
        public ClearingKind Kind;
        public Vector2 Centre;
        public float Radius;
        public float Ragged;    // how far the edge wanders in and out (share of the radius)
        public float Thinning;  // trees thin back in over this share of the radius past the edge
        public int Seed;

        /// <summary>The edge's distance from the centre in this direction (radians).</summary>
        public float RadiusAt(float angle) => Radius * (1f + Ragged * (PeriodicNoise.Around(angle, 9, Seed) * 2f - 1f));

        /// <summary>0 in the open middle, rising to 1 where the forest is full again.</summary>
        public float TreeFactor(Vector2 p, float mapSize)
        {
            Vector2 d = Wrap.Delta(Centre, p, mapSize);
            float r = RadiusAt(Mathf.Atan2(d.y, d.x));
            float dist = d.magnitude;
            if (dist <= r) return 0f;
            return Thinning <= 0f ? 1f : Mathf.Clamp01((dist - r) / (r * Thinning));
        }
    }

    public sealed class Hill
    {
        public Vector2 Centre;
        public float Height, Radius;
        public Vector2 Axis = Vector2.right;   // the long way, for ridges
        public float Stretch = 1f;              // 1 round, up to 2.2 a ridge
        public int Seed;

        /// <summary>0 off the hill, 1 on its flat top (the plan's profile: flat inner 15%, smoothstep down).</summary>
        public float Profile(Vector2 p, float mapSize)
        {
            Vector2 d = Wrap.Delta(Centre, p, mapSize);
            float along = Vector2.Dot(d, Axis) / Stretch;
            float across = Vector2.Dot(d, new Vector2(-Axis.y, Axis.x));
            float dist = Mathf.Sqrt(along * along + across * across);
            float r = Radius * (1f + 0.15f * (PeriodicNoise.Around(Mathf.Atan2(across, along), 7, Seed) * 2f - 1f));
            float t = dist / Mathf.Max(0.01f, r);
            if (t >= 1f) return 0f;
            if (t <= 0.15f) return 1f;
            return 1f - Mathf.SmoothStep(0f, 1f, (t - 0.15f) / 0.85f);
        }
    }

    public sealed class Pond
    {
        public Vector2 Centre;
        public float Radius;
        public float Water;   // the water's height
    }

    public sealed class LampSite
    {
        public int Index;
        public Vector2 Position;
        public Clearing Clearing;
    }

    public sealed class StructureSite
    {
        public StructureKind Kind;
        public Vector2 Position;
        public float Yaw;          // degrees
        public Vector2 HalfSize;   // the keep-out box nothing grows into

        public bool Contains(Vector2 p, float mapSize, float margin = 0f)
        {
            Vector2 d = Wrap.Delta(Position, p, mapSize);
            float a = Yaw * Mathf.Deg2Rad; // (local x and z the way Unity turns it: yaw degrees about up)
            float lx = d.x * Mathf.Cos(a) - d.y * Mathf.Sin(a);
            float lz = d.x * Mathf.Sin(a) + d.y * Mathf.Cos(a);
            return Mathf.Abs(lx) <= HalfSize.x + margin && Mathf.Abs(lz) <= HalfSize.y + margin;
        }
    }

    public sealed class Trail
    {
        public readonly List<Vector2> Points = new List<Vector2>();
        /// <summary>Per point: is the trail worn here (false where it peters out)?</summary>
        public readonly List<bool> Worn = new List<bool>();
        public float Width = 1.4f;
    }

    public struct TreeSpot
    {
        public Vector2 Position;
        public float Scale;
        public float Yaw;
        public TreeKind Kind;
        public byte Variant;
        public bool OldGiant;
        public bool Removed;   // cut by a later stage (sightlines, the sky above a lamp)
    }

    /// <summary>A plant, rock or log: where, how big, which way, and which kind.</summary>
    public struct Spot
    {
        public Vector2 Position;
        public float Size;
        public float Yaw;
        public byte Kind;
        public byte Variant;
    }

    /// <summary>
    /// Everything about one world, as plain data: made by the generation stages (section 3), then turned into
    /// meshes and objects by the build stage. Positions are (x, z) in metres on the wrapping map.
    /// </summary>
    public sealed class WorldPlan
    {
        public int Seed;
        public float Size;
        public Heightfield Height;

        public Vector2 Start;
        public float StartYaw;       // degrees: which way you face when you wake
        public float GiantBearing;   // degrees: where the hanging giant is

        public readonly List<Hill> Hills = new List<Hill>();
        public readonly List<Pond> Ponds = new List<Pond>();
        public readonly List<Vector2> Puddles = new List<Vector2>();
        public readonly List<Clearing> Clearings = new List<Clearing>();
        public readonly List<LampSite> Lamps = new List<LampSite>();
        public readonly List<StructureSite> Structures = new List<StructureSite>();
        public Vector2 Door;
        public float DoorYaw;
        public readonly List<Trail> Trails = new List<Trail>();

        public readonly List<TreeSpot> Trees = new List<TreeSpot>();
        public readonly List<Spot> Bushes = new List<Spot>();
        public readonly List<Spot> Grass = new List<Spot>();
        public readonly List<Spot> TallGrass = new List<Spot>();
        public readonly List<Spot> Rocks = new List<Spot>();
        public readonly List<Spot> Boulders = new List<Spot>();
        public readonly List<Spot> Logs = new List<Spot>();
        public readonly List<Vector2> Batteries = new List<Vector2>();
        public readonly List<Loot> Loot = new List<Loot>();

        public Clearing StartClearing => Clearings.Count > 0 ? Clearings[0] : null;

        public float Distance(Vector2 a, Vector2 b) => Wrap.Distance(a, b, Size);

        /// <summary>The ground height at a point.</summary>
        public float GroundAt(Vector2 p) => Height.Sample(p);

        /// <summary>Is the point under a pond's water?</summary>
        public bool InWater(Vector2 p)
        {
            foreach (var pond in Ponds)
                if (Distance(p, pond.Centre) < pond.Radius && GroundAt(p) < pond.Water) return true;
            return false;
        }

        /// <summary>How much of a hill top a point is on (0 none, 1 the flat top).</summary>
        public float HillTop(Vector2 p)
        {
            float top = 0f;
            foreach (var h in Hills) top = Mathf.Max(top, h.Profile(p, Size));
            return top;
        }

        /// <summary>Is the point inside any structure's keep-out box?</summary>
        public bool InStructure(Vector2 p, float margin = 0f)
        {
            foreach (var s in Structures) if (s.Contains(p, Size, margin)) return true;
            return false;
        }
    }
}
