using Game.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace Game.Audio
{
    /// <summary>The AudioMixer and how quickly each sound state fades in (Assets/Config/Audio/AudioConfig.asset).</summary>
    [CreateAssetMenu(menuName = "Game/Config/Audio", fileName = "AudioConfig")]
    public sealed class AudioConfig : GameConfig
    {
        [Tooltip("The game's AudioMixer (Assets/Audio/GameMixer.mixer). Its snapshots must be named after the sound states.")]
        public AudioMixer mixer;

        [Header("Seconds to fade into each state")]
        [Min(0f)] public float toOutdoors = 0.6f;
        [Min(0f)] public float toSheltered = 0.6f;
        [Min(0f)] public float toDarkRoom = 2f;
        [Min(0f)] public float toDread = 1f;
        [Min(0f)] public float toHush = 0.4f;
        [Min(0f)] public float toPaused = 0f;

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
