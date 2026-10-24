namespace Game.Core
{
    /// <summary>Where the session is: the main menu (with its film), the intro narration, or playing.</summary>
    public enum GamePhase { Menu, Intro, Playing }

    public readonly struct GamePhaseChanged : IGameEvent
    {
        public readonly GamePhase Phase;
        public GamePhaseChanged(GamePhase phase) { Phase = phase; }
        public override string ToString() => $"Phase: {Phase}";
    }

    /// <summary>
    /// The session's phase (section 7). The run's clock and the hunters only run while Playing; the main menu (in
    /// the forest scene) starts things at Menu. Scenes without a menu (test scenes) are Playing from the start.
    /// </summary>
    public sealed class GameFlow
    {
        readonly EventBus events;

        public GamePhase Phase { get; private set; } = GamePhase.Playing;
        public bool Playing => Phase == GamePhase.Playing;

        public GameFlow(EventBus events) => this.events = events;

        public void Set(GamePhase phase)
        {
            if (phase == Phase) return;
            Phase = phase;
            events?.Publish(new GamePhaseChanged(phase));
        }

        /// <summary>True while playing, or when there's no flow at all (tests).</summary>
        public static bool IsPlaying => !Services.TryGet<GameFlow>(out var f) || f.Playing;
    }
}
