namespace Game.Core
{
    /// <summary>
    /// Everything about the current run (one attempt, from waking up until restart). Made fresh for every run,
    /// so restarting can't leave stale state behind. Being caught doesn't start a new run: progress is kept.
    /// </summary>
    public sealed class GameRun
    {
        public const int LampCount = 10;

        public int RunNumber { get; }
        public int Seed { get; }

        /// <summary>Seconds of play in this run. Stops while paused.</summary>
        public float Elapsed { get; private set; }

        /// <summary>Lamps lit so far ("TimesLit" in the plan); drives escalation.</summary>
        public int LampsLit { get; private set; }

        public GameRun(int runNumber, int seed)
        {
            RunNumber = runNumber;
            Seed = seed;
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime > 0f) Elapsed += deltaTime;
        }

        /// <summary>Count one more lamp lit and return the new total.</summary>
        public int RecordLampLit()
        {
            if (LampsLit < LampCount) LampsLit++;
            return LampsLit;
        }
    }
}
