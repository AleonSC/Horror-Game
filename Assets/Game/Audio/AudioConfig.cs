using Game.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace Game.Audio
{
    /// <summary>Recorded clips to play instead of one of the built-in sounds.</summary>
    [System.Serializable]
    public struct SoundOverride
    {
        public Sound sound;
        public AudioClip[] clips;
    }

    /// <summary>Your own tracks for a music mood (several: one after another, shuffled).</summary>
    [System.Serializable]
    public struct MoodMusic
    {
        public MusicMood mood;
        public AudioClip[] tracks;
    }

    /// <summary>Your own theme for a big entrance: an intro scored to the entrance, then a loop while it hunts.</summary>
    [System.Serializable]
    public struct BossMusic
    {
        public EntityKind entity;
        public AudioClip intro;
        public AudioClip loop;
    }

    /// <summary>The AudioMixer and how quickly each sound state fades in (Assets/Config/Audio/AudioConfig.asset).</summary>
    [CreateAssetMenu(menuName = "Game/Config/Audio", fileName = "AudioConfig")]
    public sealed class AudioConfig : GameConfig
    {
        [Tooltip("The game's AudioMixer (Assets/Audio/GameMixer.mixer). Its snapshots must be named after the sound states.")]
        public AudioMixer mixer;

        [Tooltip("Your own recordings for any of the game's sounds (several clips: one is picked at random each time).")]
        public SoundOverride[] soundOverrides = new SoundOverride[0];

        [Header("The forest")]
        [Range(0f, 1f), Tooltip("How loud the wind is (it rises and falls with the gusts under this).")]
        public float windVolume = 0.45f;
        [Range(0f, 1f), Tooltip("How loud the leaves rustling in the canopy are.")]
        public float leavesVolume = 0.35f;

        [Header("Music")]
        [Tooltip("Your own tracks per mood. A mood with none uses the built-in music (or tracks in Resources/Music/<Mood>/).")]
        public MoodMusic[] music = new MoodMusic[0];
        [Tooltip("Your own entrance themes. Empty: the built-in ones.")]
        public BossMusic[] bossThemes = new BossMusic[0];
        [Range(0f, 1f)] public float musicVolume = 0.7f;
        [Min(0f), Tooltip("Seconds to fade toward a worse mood.")]
        public float fadeToWorse = 1f;
        [Min(0f), Tooltip("Seconds to fade back toward calm.")]
        public float fadeToCalm = 6f;

        [Header("Seconds to fade into each state")]
        [Min(0f)] public float toOutdoors = 0.6f;
        [Min(0f)] public float toSheltered = 0.6f;
        [Min(0f)] public float toDarkRoom = 2f;
        [Min(0f)] public float toDread = 1f;
        [Min(0f)] public float toHush = 0.4f;
        [Min(0f)] public float toPaused = 0f;

        /// <summary>Your clips for this sound, or null to use the built-in one.</summary>
        public AudioClip[] OverrideFor(Sound sound)
        {
            if (soundOverrides == null) return null;
            foreach (var o in soundOverrides)
                if (o.sound == sound && o.clips != null && o.clips.Length > 0) return o.clips;
            return null;
        }

        public AudioClip[] TracksFor(MusicMood mood)
        {
            if (music == null) return null;
            foreach (var m in music)
                if (m.mood == mood && m.tracks != null && m.tracks.Length > 0) return m.tracks;
            return null;
        }

        public bool ThemeFor(EntityKind entity, out BossMusic theme)
        {
            theme = default;
            if (bossThemes == null) return false;
            foreach (var b in bossThemes)
                if (b.entity == entity && (b.intro != null || b.loop != null)) { theme = b; return true; }
            return false;
        }

        public float FadeSeconds(AudioState state)
        {
            switch (state)
            {
                case AudioState.Sheltered: return toSheltered;
                case AudioState.DarkRoom: return toDarkRoom;
                case AudioState.Dread: return toDread;
                case AudioState.Hush: return toHush;
                case AudioState.Paused: return toPaused;
                default: return toOutdoors;
            }
        }
    }
}
