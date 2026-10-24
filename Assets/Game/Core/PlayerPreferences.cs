using System;
using System.IO;
using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// The player's own choices (volume, sensitivity, graphics, difficulty), saved as a small JSON file in the
    /// player's data folder. Tuning numbers don't go here: they're config assets.
    /// </summary>
    [Serializable]
    public sealed class PlayerPreferences
    {
        public const string FileName = "settings.json";

        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float effectsVolume = 1f;
        [Range(0f, 1f)] public float musicVolume = 1f;
        [Range(0.1f, 5f)] public float mouseSensitivity = 1f;
        public bool motionBlur = true;
        [Range(0f, 1f)] public float motionBlurStrength = 0.5f;
        public bool classicLook;
        public bool rayTracing;
        public string difficulty = "Normal";
        [Tooltip("Your own values for the Custom difficulty.")]
        public DifficultySettings customDifficulty = new DifficultySettings();
        public string devTab = "Game";
        [Tooltip("The dev window's size (drag its edge or corner); height 0 is the full screen.")]
        public float devWidth = 520f;
        public float devHeight;

        public static string DefaultPath => Path.Combine(Application.persistentDataPath, FileName);

        /// <summary>Read the file, or return defaults if it's missing or unreadable.</summary>
        public static PlayerPreferences Load(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    var loaded = JsonUtility.FromJson<PlayerPreferences>(File.ReadAllText(path));
                    if (loaded != null) return loaded;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Couldn't read settings from {path}, using defaults. ({e.Message})");
            }
            return new PlayerPreferences();
        }

        /// <summary>Write the file. Written to a temporary file first so a crash can't leave it half-written.</summary>
        public bool Save(string path)
        {
            try
            {
                var directory = Path.GetDirectoryName(path);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                var temp = path + ".tmp";
                File.WriteAllText(temp, JsonUtility.ToJson(this, true));
                if (File.Exists(path)) File.Delete(path);
                File.Move(temp, path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Couldn't save settings to {path}. ({e.Message})");
                return false;
            }
        }
    }
}
