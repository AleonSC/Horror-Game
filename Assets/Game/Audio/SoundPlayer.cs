using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace Game.Audio
{
    /// <summary>The game's built-in sounds (made by <see cref="SoundSynth"/> at startup).</summary>
    public enum Sound { Creep, Appear, Pound, Crescendo, Portal, Shriek, Moan, Heartbeat, Breath, Click, LampOn }

    /// <summary>
    /// Plays one-shot sounds from a pool of sources, each routed to a mixer group (so the sound states apply
    /// to it automatically). Builds the procedural clips when the game starts.
    /// </summary>
    public sealed class SoundPlayer : MonoBehaviour
    {
        const int PoolSize = 32;

        readonly Dictionary<Sound, AudioClip> clips = new Dictionary<Sound, AudioClip>();
        readonly Dictionary<string, AudioMixerGroup> groups = new Dictionary<string, AudioMixerGroup>();
        readonly List<AudioSource> pool = new List<AudioSource>();
        AudioMixer mixer;
        int next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponent<SoundPlayer>() == null)
                root.gameObject.AddComponent<SoundPlayer>();
        }

        void Awake()
        {
            var config = Services.Get<GameSetup>().Get<AudioConfig>();
            mixer = config != null ? config.mixer : null;
            BuildClips();
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject("Sound");
                go.transform.SetParent(transform, false);
                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                pool.Add(source);
            }
            Services.Register(this);
        }

        void OnDestroy()
        {
            Services.Unregister(this);
            foreach (var clip in clips.Values) Destroy(clip);
        }

        void BuildClips()
        {
            Add(Sound.Creep, SoundSynth.Creep(1));
            Add(Sound.Appear, SoundSynth.Appear(2));
            Add(Sound.Pound, SoundSynth.Pound(3));
            Add(Sound.Crescendo, SoundSynth.Crescendo(4));
            Add(Sound.Portal, SoundSynth.Portal(5));
            Add(Sound.Shriek, SoundSynth.Shriek(6));
            Add(Sound.Moan, SoundSynth.Moan(7));
            Add(Sound.Heartbeat, SoundSynth.Heartbeat(8));
            Add(Sound.Breath, SoundSynth.Breath(9));
            Add(Sound.Click, SoundSynth.Click(10));
            Add(Sound.LampOn, SoundSynth.LampOn(11));
        }

        void Add(Sound sound, float[] samples)
        {
            var clip = AudioClip.Create(sound.ToString(), samples.Length, 1, SoundSynth.SampleRate, false);
            clip.SetData(samples, 0);
            clips[sound] = clip;
        }

        public AudioClip Clip(Sound sound) => clips.TryGetValue(sound, out var c) ? c : null;

        /// <summary>The mixer group with this name (see <see cref="MixerNames"/>), or null if there's no mixer.</summary>
        public AudioMixerGroup Group(string name)
        {
            if (mixer == null) return null;
            if (groups.TryGetValue(name, out var g)) return g;
            foreach (var match in mixer.FindMatchingGroups(name))
                if (match.name == name) { g = match; break; }
            groups[name] = g;
            return g;
        }

        /// <summary>Play a sound in the world. <paramref name="range"/>: metres until it's silent.</summary>
        public AudioSource Play(Sound sound, Vector3 position, string group, float volume = 1f, float pitch = 1f, float range = 30f) =>
            Play(Clip(sound), position, group, volume, pitch, range, true);

        /// <summary>Play a sound with no position (in your head: heartbeat, breathing, UI).</summary>
        public AudioSource Play2D(Sound sound, string group, float volume = 1f, float pitch = 1f) =>
            Play(Clip(sound), Vector3.zero, group, volume, pitch, 0f, false);

        public AudioSource Play(AudioClip clip, Vector3 position, string group, float volume, float pitch, float range, bool spatial)
        {
            if (clip == null) return null;
            var source = NextFree();
            source.transform.position = position;
            source.clip = clip;
            source.outputAudioMixerGroup = Group(group);
            source.volume = volume;
            source.pitch = pitch;
            source.loop = false;
            source.spatialBlend = spatial ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = Mathf.Max(1f, range * 0.08f);
            source.maxDistance = Mathf.Max(source.minDistance + 1f, range);
            source.dopplerLevel = 0f;
            source.ignoreListenerPause = group == MixerNames.UI;
            source.Play();
            return source;
        }

        // A free source, or the next one round if they're all busy.
        AudioSource NextFree()
        {
            for (int i = 0; i < pool.Count; i++)
            {
                var s = pool[(next + i) % pool.Count];
                if (!s.isPlaying)
                {
                    next = (next + i + 1) % pool.Count;
                    return s;
                }
            }
            var stolen = pool[next];
            next = (next + 1) % pool.Count;
            stolen.Stop();
            return stolen;
        }
    }
}
