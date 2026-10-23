using Game.Core;

namespace Game.Audio
{
    /// <summary>
    /// The sound states from section 5.1 of the plan. Each one is a snapshot of the same name in the
    /// AudioMixer, so the names must match exactly.
    /// </summary>
    public enum AudioState { Outdoors, Sheltered, DarkRoom, Dread, Hush, Paused }

    /// <summary>What the audio state is worked out from.</summary>
    public struct AudioStateInputs
    {
        public bool Paused;
        public bool InDarkRoom;
        public bool Sheltered;
        public ThreatLevel Threat;
    }

    public static class AudioStateRules
    {
        /// <summary>
        /// Pick the one state that applies. The strongest wins: Paused, then the dark room (nothing outside can
        /// be heard there), then Hush, Dread, Sheltered, and Outdoors when nothing else applies. A threat beats
        /// shelter because it's the more urgent thing to hear.
        /// </summary>
        public static AudioState Resolve(in AudioStateInputs inputs)
        {
            if (inputs.Paused) return AudioState.Paused;
            if (inputs.InDarkRoom) return AudioState.DarkRoom;
            if (inputs.Threat == ThreatLevel.Hush) return AudioState.Hush;
            if (inputs.Threat == ThreatLevel.Dread) return AudioState.Dread;
            if (inputs.Sheltered) return AudioState.Sheltered;
            return AudioState.Outdoors;
        }
    }

    /// <summary>The state moved from one to another (the session recorder logs this).</summary>
    public readonly struct AudioStateChanged : IGameEvent
    {
        public readonly AudioState From;
        public readonly AudioState To;
        public AudioStateChanged(AudioState from, AudioState to) { From = from; To = to; }
        public override string ToString() => $"Sound: {From} -> {To}";
    }
}
