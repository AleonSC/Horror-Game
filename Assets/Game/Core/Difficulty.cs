using System;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// The difficulty multipliers (section 6.14 of the plan). Each scales one existing thing; 1 is Normal.
    /// Blind one, crawler and moth values are here already so Custom keeps them when those arrive.
    /// </summary>
    [Serializable]
    public sealed class DifficultySettings
    {
        [Range(0.25f, 3f), Tooltip("Flashlight battery life.")]
        public float batteryLife = 1f;
        [Range(0f, 3f), Tooltip("How many batteries lie in the forest (new worlds).")]
        public float batteriesInForest = 1f;
        [Range(0.25f, 3f), Tooltip("Time between the extra scours that join each minute.")]
        public float timeBetweenScours = 1f;
        [Range(0f, 3f), Tooltip("How many extra scours join over time.")]
        public float extraScours = 1f;
        [Range(0.25f, 3f), Tooltip("Time it takes to face a scour down.")]
        public float faceDownTime = 1f;
        [Range(0.25f, 3f), Tooltip("How far the blind one hears.")]
        public float blindHearing = 1f;
        [Range(0.25f, 3f), Tooltip("Time for the blind one to close in.")]
        public float blindCloseIn = 1f;
        [Range(0f, 3f), Tooltip("How many crawlers.")]
        public float crawlers = 1f;
        [Range(1, 10), Tooltip("Crawlers start at this many lamps.")]
        public int crawlersFromLamp = 2;
        [Range(0.25f, 3f), Tooltip("Time for moths to gather.")]
        public float mothTime = 1f;
        [Range(0.25f, 3f), Tooltip("How fast lamps crank.")]
        public float crankSpeed = 1f;

        public DifficultySettings Clone() => (DifficultySettings)MemberwiseClone();

        public static DifficultySettings Easy() => new DifficultySettings
        {
            batteryLife = 1.5f, batteriesInForest = 1.4f, timeBetweenScours = 1.5f, extraScours = 0.6f,
            faceDownTime = 0.75f, blindHearing = 0.7f, blindCloseIn = 1.4f, crawlers = 0.5f, crawlersFromLamp = 3,
            mothTime = 1.5f, crankSpeed = 1.35f,
        };

        public static DifficultySettings Normal() => new DifficultySettings();

        public static DifficultySettings Hard() => new DifficultySettings
        {
            batteryLife = 0.75f, batteriesInForest = 0.7f, timeBetweenScours = 0.7f, extraScours = 1.4f,
            faceDownTime = 1.3f, blindHearing = 1.3f, blindCloseIn = 0.7f, crawlers = 1.5f, crawlersFromLamp = 1,
            mothTime = 0.67f, crankSpeed = 0.8f,
        };
    }

    /// <summary>
    /// The difficulty in play: one of the presets, or Custom (the player's own sliders, kept in their
    /// preferences). Systems read <see cref="Current"/> when they need a value, so a change applies at once
    /// (except batteries in the forest, which apply to the next world).
    /// </summary>
    public sealed class Difficulty
    {
        public static readonly string[] Levels = { "Easy", "Normal", "Hard", "Custom" };

        readonly PlayerPreferences prefs;
        DifficultySettings preset = DifficultySettings.Normal();
        string presetFor;

        public Difficulty(PlayerPreferences prefs) => this.prefs = prefs;

        /// <summary>"Easy", "Normal", "Hard" or "Custom".</summary>
        public string Level
        {
            get => prefs != null && Array.IndexOf(Levels, prefs.difficulty) >= 0 ? prefs.difficulty : "Normal";
            set { if (prefs != null && Array.IndexOf(Levels, value) >= 0) prefs.difficulty = value; }
        }

        public DifficultySettings Current
        {
            get
            {
                string level = Level;
                if (level == "Custom" && prefs != null)
                {
                    if (prefs.customDifficulty == null) prefs.customDifficulty = new DifficultySettings();
                    return prefs.customDifficulty;
                }
                if (presetFor != level)
                {
                    preset = For(level);
                    presetFor = level;
                }
                return preset;
            }
        }

        public static DifficultySettings For(string level)
        {
            switch (level)
            {
                case "Easy": return DifficultySettings.Easy();
                case "Hard": return DifficultySettings.Hard();
                default: return DifficultySettings.Normal();
            }
        }

        /// <summary>The current settings, or Normal when the game isn't running (tests, editor tools).</summary>
        public static DifficultySettings Now => Services.TryGet<Difficulty>(out var d) ? d.Current : Normal;
        static readonly DifficultySettings Normal = new DifficultySettings();
    }
}
