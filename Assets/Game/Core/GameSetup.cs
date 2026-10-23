using System.Collections.Generic;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// The one asset the game loads at startup (Assets/Config/Resources/GameSetup.asset). It lists every config
    /// asset so systems can find theirs with <see cref="Get{T}"/>.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Game Setup", fileName = "GameSetup")]
    public sealed class GameSetup : ScriptableObject
    {
        public const string ResourcePath = "GameSetup";

        [Tooltip("Every config asset the game uses. Add new ones here.")]
        [SerializeField] List<GameConfig> configs = new List<GameConfig>();

        [Header("Run")]
        [Tooltip("Pick a new world seed every run. Turn off to replay the same seed.")]
        [SerializeField] bool randomizeSeedEachRun = true;

        [Tooltip("The seed used when 'Randomize Seed Each Run' is off.")]
        [SerializeField] int fixedSeed = 12345;

        public bool RandomizeSeedEachRun => randomizeSeedEachRun;
        public int FixedSeed => fixedSeed;

        public T Get<T>() where T : GameConfig
        {
            foreach (var config in configs)
                if (config is T match) return match;
            return null;
        }

        /// <summary>Add a config at runtime (tests, or a system making a default when none is set up).</summary>
        public void Add(GameConfig config)
        {
            if (config != null && !configs.Contains(config)) configs.Add(config);
        }
    }
}
