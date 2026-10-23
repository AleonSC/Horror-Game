using Game.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace Game.Audio
{
    /// <summary>
    /// The one place that changes the mixer's state: it moves to the snapshot named after the current
    /// <see cref="AudioState"/>, and sets the volumes from the player's preferences. A new sound only needs
    /// the right mixer group; no script should change volumes for "inside" or "dark room" itself.
    /// </summary>
    public sealed class AudioStateController : MonoBehaviour
    {
        AudioStateTracker tracker;
        AudioConfig config;
        AudioMixerSnapshot[] snapshots;

        public AudioState Current => tracker?.Current ?? AudioState.Outdoors;
        public AudioStateInputs Inputs => tracker?.Inputs ?? default;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponent<AudioStateController>() == null)
                root.gameObject.AddComponent<AudioStateController>();
        }

        void Awake()
        {
            var events = Services.Get<EventBus>();
            config = Services.Get<GameSetup>().Get<AudioConfig>();
            if (config == null)
                Debug.LogWarning("No AudioConfig in the GameSetup asset; sound states won't change the mixer.");
            else if (config.mixer == null)
                Debug.LogWarning("AudioConfig has no mixer assigned; sound states won't change the mixer.");
            else
                FindSnapshots(config.mixer);

            tracker = new AudioStateTracker(events);
            tracker.Changed += OnStateChanged;
            Services.Register(this);
        }

        // The mixer ignores SetFloat during Awake, so the starting volumes and snapshot are set here.
        void Start()
        {
            if (Services.TryGet<PlayerPreferences>(out var prefs)) ApplyVolumes(prefs);
            Apply(tracker.Current, 0f);
        }

        void OnDestroy()
        {
            Services.Unregister(this);
            tracker?.Dispose();
        }

        void FindSnapshots(AudioMixer mixer)
        {
            // Snapshot fades must keep running while the game is paused (time scale 0).
            mixer.updateMode = AudioMixerUpdateMode.UnscaledTime;

            var states = (AudioState[])System.Enum.GetValues(typeof(AudioState));
            snapshots = new AudioMixerSnapshot[states.Length];
            foreach (var state in states)
            {
                var snapshot = mixer.FindSnapshot(state.ToString());
                if (snapshot == null) Debug.LogWarning($"The mixer has no snapshot named '{state}'.");
                snapshots[(int)state] = snapshot;
            }
        }

        void OnStateChanged(AudioState from, AudioState to)
        {
            // Pausing stops every sound except the UI (whose sources ignore the listener pause).
            AudioListener.pause = to == AudioState.Paused;
            Apply(to, config != null ? config.FadeSeconds(to) : 0f);
        }

        void Apply(AudioState state, float seconds)
        {
            var snapshot = snapshots?[(int)state];
            if (snapshot != null) snapshot.TransitionTo(seconds);
        }

        /// <summary>Set the mixer's volumes from the preferences (0-1 sliders to decibels).</summary>
        public void ApplyVolumes(PlayerPreferences prefs)
        {
            var mixer = config != null ? config.mixer : null;
            if (mixer == null) return;
            mixer.SetFloat(MixerNames.MasterVolume, ToDecibels(prefs.masterVolume));
            mixer.SetFloat(MixerNames.EffectsVolume, ToDecibels(prefs.effectsVolume));
            mixer.SetFloat(MixerNames.MusicVolume, ToDecibels(prefs.musicVolume));
        }

        public static float ToDecibels(float volume) => volume <= 0.0001f ? -80f : Mathf.Log10(volume) * 20f;
    }
}
