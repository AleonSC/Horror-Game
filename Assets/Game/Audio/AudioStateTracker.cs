using System;
using System.Collections.Generic;
using Game.Core;

namespace Game.Audio
{
    /// <summary>
    /// Listens to game events and keeps track of which audio state applies. Plain C# (no mixer), so it can be
    /// unit tested; <see cref="AudioStateController"/> applies its result to the AudioMixer.
    /// </summary>
    public sealed class AudioStateTracker : IDisposable
    {
        readonly EventBus events;
        readonly List<IDisposable> subscriptions = new List<IDisposable>();
        readonly HashSet<int> shelters = new HashSet<int>();
        AudioStateInputs inputs;

        public AudioState Current { get; private set; } = AudioState.Outdoors;
        public AudioStateInputs Inputs => inputs;

        /// <summary>Raised with (from, to) whenever the state changes. Also published as <see cref="AudioStateChanged"/>.</summary>
        public event Action<AudioState, AudioState> Changed;

        public AudioStateTracker(EventBus events)
        {
            this.events = events ?? throw new ArgumentNullException(nameof(events));
            subscriptions.Add(events.Subscribe<EnteredShelter>(e => { shelters.Add(e.ShelterId); Refresh(); }));
            subscriptions.Add(events.Subscribe<ExitedShelter>(e => { shelters.Remove(e.ShelterId); Refresh(); }));
            subscriptions.Add(events.Subscribe<EnteredDarkRoom>(_ => { inputs.InDarkRoom = true; Refresh(); }));
            subscriptions.Add(events.Subscribe<ExitedDarkRoom>(_ => { inputs.InDarkRoom = false; Refresh(); }));
            subscriptions.Add(events.Subscribe<GamePaused>(_ => { inputs.Paused = true; Refresh(); }));
            subscriptions.Add(events.Subscribe<GameResumed>(_ => { inputs.Paused = false; Refresh(); }));
            subscriptions.Add(events.Subscribe<ThreatLevelChanged>(e => { inputs.Threat = e.Level; Refresh(); }));
            subscriptions.Add(events.Subscribe<RunReset>(_ => ResetRunState()));
        }

        // A restart puts the player back outside with nothing near. Pausing is left alone: the pause menu
        // resumes after a restart.
        void ResetRunState()
        {
            shelters.Clear();
            inputs.InDarkRoom = false;
            inputs.Threat = ThreatLevel.None;
            Refresh();
        }

        void Refresh()
        {
            inputs.Sheltered = shelters.Count > 0;
            var next = AudioStateRules.Resolve(inputs);
            if (next == Current) return;
            var previous = Current;
            Current = next;
            Changed?.Invoke(previous, next);
            events.Publish(new AudioStateChanged(previous, next));
        }

        public void Dispose()
        {
            foreach (var s in subscriptions) s.Dispose();
            subscriptions.Clear();
        }
    }
}
