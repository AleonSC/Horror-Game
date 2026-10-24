using UnityEngine;

// The game's shared events. Each ToString() is the line the session recorder writes, so keep them readable.
// Events that only one module cares about can live in that module instead (e.g. AudioStateChanged in Audio).
namespace Game.Core
{
    public enum EntityKind { Scour, Blind, Crawler, Flier, Giant }

    /// <summary>How close the nearest threat is, for the Dread and Hush audio states.</summary>
    public enum ThreatLevel { None, Dread, Hush }

    public readonly struct RunStarted : IGameEvent
    {
        public readonly int RunNumber;
        public readonly int Seed;
        public RunStarted(int runNumber, int seed) { RunNumber = runNumber; Seed = seed; }
        public override string ToString() => $"Run {RunNumber} started (seed {Seed})";
    }

    /// <summary>The current run is being thrown away (restart from the pause menu). A RunStarted follows.</summary>
    public readonly struct RunReset : IGameEvent
    {
        public readonly int RunNumber;
        public RunReset(int runNumber) { RunNumber = runNumber; }
        public override string ToString() => $"Run {RunNumber} reset";
    }

    public readonly struct LampLit : IGameEvent
    {
        public readonly int LampIndex;
        public readonly int TimesLit;
        public readonly Vector3 Position;
        public LampLit(int lampIndex, int timesLit, Vector3 position) { LampIndex = lampIndex; TimesLit = timesLit; Position = position; }
        public override string ToString() => $"Lamp {LampIndex} lit ({TimesLit} of 10)";
    }

    public readonly struct PlayerCaught : IGameEvent
    {
        public readonly EntityKind By;
        public readonly int EntityId;
        public readonly Vector3 Position;
        public PlayerCaught(EntityKind by, int entityId, Vector3 position) { By = by; EntityId = entityId; Position = position; }
        public override string ToString() => $"Caught by {By} #{EntityId}";
    }

    /// <summary>The player woke up again after being taken (progress kept).</summary>
    public readonly struct PlayerRespawned : IGameEvent
    {
        public readonly Vector3 Position;
        public PlayerRespawned(Vector3 position) { Position = position; }
        public override string ToString() => "Woke up again";
    }

    /// <summary>Ran out of breath (or got it back).</summary>
    public readonly struct PlayerWinded : IGameEvent
    {
        public readonly bool Winded;
        public PlayerWinded(bool winded) { Winded = winded; }
        public override string ToString() => Winded ? "Winded" : "Got breath back";
    }

    /// <summary>An entity changed what it's doing (the session recorder logs these).</summary>
    public readonly struct EntityStateChanged : IGameEvent
    {
        public readonly EntityKind Kind;
        public readonly int EntityId;
        public readonly string State;
        public EntityStateChanged(EntityKind kind, int entityId, string state) { Kind = kind; EntityId = entityId; State = state; }
        public override string ToString() => $"{Kind} #{EntityId}: {State}";
    }

    public readonly struct EntitySpawned : IGameEvent
    {
        public readonly EntityKind Kind;
        public readonly int EntityId;
        public readonly Vector3 Position;
        public EntitySpawned(EntityKind kind, int entityId, Vector3 position) { Kind = kind; EntityId = entityId; Position = position; }
        public override string ToString() => $"{Kind} #{EntityId} appeared";
    }

    /// <summary>A sound that hearing AI can react to (section 6.6 of the plan).</summary>
    public readonly struct NoiseEmitted : IGameEvent
    {
        public readonly Vector3 Position;
        public readonly float Radius;
        public readonly string Source;
        public NoiseEmitted(Vector3 position, float radius, string source) { Position = position; Radius = radius; Source = source; }
        /// <summary>Footsteps and panting: too frequent for the session log.</summary>
        public bool Everyday => Source == "walking" || Source == "running" || Source == "creeping" || Source == "panting" || Source == "rustling" || Source == "drone" || Source == "radio";
        public override string ToString() => $"Noise: {Source} ({Radius:0.#} m)";
    }

    /// <summary>The player went inside a shelter (shack, cabin, tent, car seat). Each shelter has its own id.</summary>
    public readonly struct EnteredShelter : IGameEvent
    {
        public readonly int ShelterId;
        public readonly string Name;
        public EnteredShelter(int shelterId, string name) { ShelterId = shelterId; Name = name; }
        public override string ToString() => $"Entered shelter: {Name}";
    }

    public readonly struct ExitedShelter : IGameEvent
    {
        public readonly int ShelterId;
        public readonly string Name;
        public ExitedShelter(int shelterId, string name) { ShelterId = shelterId; Name = name; }
        public override string ToString() => $"Left shelter: {Name}";
    }

    public readonly struct EnteredDarkRoom : IGameEvent
    {
        public override string ToString() => "Entered the dark room";
    }

    public readonly struct ExitedDarkRoom : IGameEvent
    {
        public override string ToString() => "Left the dark room";
    }

    public readonly struct GamePaused : IGameEvent
    {
        public override string ToString() => "Paused";
    }

    public readonly struct GameResumed : IGameEvent
    {
        public override string ToString() => "Resumed";
    }

    public readonly struct ThreatLevelChanged : IGameEvent
    {
        public readonly ThreatLevel Level;
        public ThreatLevelChanged(ThreatLevel level) { Level = level; }
        public override string ToString() => $"Threat: {Level}";
    }

    /// <summary>Every hunt starts over (a flare in the sky, the watchtower's floodlights): whatever was hunting backs off.</summary>
    public readonly struct HuntsReset : IGameEvent
    {
        public readonly string Why;
        public HuntsReset(string why) { Why = why; }
        public override string ToString() => $"Hunts reset ({Why})";
    }

    /// <summary>
    /// A blinding flash (the camera): every entity inside the cone, with a clear line to it, is driven off for a while.
    /// </summary>
    public readonly struct FlashBurst : IGameEvent
    {
        public readonly Vector3 Position;
        public readonly Vector3 Direction;
        public readonly float Range;
        public readonly float HalfAngle;
        public readonly float Seconds;
        public FlashBurst(Vector3 position, Vector3 direction, float range, float halfAngle, float seconds)
        {
            Position = position; Direction = direction; Range = range; HalfAngle = halfAngle; Seconds = seconds;
        }
        public override string ToString() => $"Camera flash ({Range:0} m)";
    }

    /// <summary>The key fob was pressed: the car chirps and flashes, wherever it is.</summary>
    public readonly struct KeyFobPressed : IGameEvent
    {
        public override string ToString() => "Key fob pressed";
    }

    /// <summary>All ten lamps are lit: the lone door unlocks (a heavy clunk heard everywhere).</summary>
    public readonly struct DoorUnlocked : IGameEvent
    {
        public override string ToString() => "The lone door unlocked";
    }

    /// <summary>The player picked something up (or ate it).</summary>
    public readonly struct ItemPickedUp : IGameEvent
    {
        public readonly string Item;
        public readonly int Count;
        public ItemPickedUp(string item, int count) { Item = item; Count = count; }
        public override string ToString() => Count > 1 ? $"Picked up {Item} x{Count}" : $"Picked up {Item}";
    }

    /// <summary>The player used the item in their hand.</summary>
    /// <summary>F with the map held: hold it up, or put it down.</summary>
    public readonly struct MapToggled : IGameEvent
    {
        public override string ToString() => "Map raised or lowered";
    }

    /// <summary>A photo was taken (the polaroid, numbered from 0, is in the player's Polaroids).</summary>
    public readonly struct PhotoTaken : IGameEvent
    {
        public readonly int Index;
        public PhotoTaken(int index) { Index = index; }
        public override string ToString() => $"Photo {Index + 1} taken";
    }

    public readonly struct ItemUsed : IGameEvent
    {
        public readonly string Item;
        public readonly string What;
        public ItemUsed(string item, string what) { Item = item; What = what; }
        public override string ToString() => $"{Item}: {What}";
    }

    /// <summary>The music's mood (section 5.3), worked out from what's near and what it's doing.</summary>
    public enum MusicMood { Calm, Intro, Tension, Danger, Watcher, GameOver }

    public readonly struct MusicMoodChanged : IGameEvent
    {
        public readonly MusicMood Mood;
        public MusicMoodChanged(MusicMood mood) { Mood = mood; }
        public override string ToString() => $"Music: {Mood}";
    }

    /// <summary>A big entrance (the blind one, the giant, the flier): its theme plays, scored to the entrance.</summary>
    public readonly struct BossThemeStarted : IGameEvent
    {
        public readonly EntityKind Kind;
        public BossThemeStarted(EntityKind kind) { Kind = kind; }
        public override string ToString() => $"{Kind}'s theme";
    }

    /// <summary>It's gone (driven off, left after taking you, or a restart): its theme fades out.</summary>
    public readonly struct BossThemeEnded : IGameEvent
    {
        public readonly EntityKind Kind;
        public BossThemeEnded(EntityKind kind) { Kind = kind; }
        public override string ToString() => $"{Kind}'s theme ends";
    }

    /// <summary>The watchtower's floodlights came on: hunts reset, the area is safe, the giant is driven back.</summary>
    public readonly struct FloodlightsLit : IGameEvent
    {
        public readonly Vector3 Position;
        public readonly float Seconds;
        public FloodlightsLit(Vector3 position, float seconds) { Position = position; Seconds = seconds; }
        public override string ToString() => "The watchtower's floodlights came on";
    }
}
