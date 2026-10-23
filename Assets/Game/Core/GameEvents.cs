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
}
