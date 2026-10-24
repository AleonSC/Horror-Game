using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.Audio;

namespace Game.Audio
{
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
        AudioConfig config;
        int next;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponent<SoundPlayer>() == null)
                root.gameObject.AddComponent<SoundPlayer>();
        }

        void Awake()
        {
            config = Services.Get<GameSetup>().Get<AudioConfig>();
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
            Add(Sound.Pickup, SoundSynth.Pickup(12));
            Add(Sound.Ignite, SoundSynth.Ignite(13));
            Add(Sound.Lighter, SoundSynth.Lighter(14));
            Add(Sound.FlareShot, SoundSynth.FlareShot(15));
            Add(Sound.FlareBurst, SoundSynth.FlareBurst(16));
            Add(Sound.Crack, SoundSynth.Crack(17));
            Add(Sound.Shutter, SoundSynth.Shutter(18));
            Add(Sound.Chirp, SoundSynth.Chirp(19));
            Add(Sound.DoorOpen, SoundSynth.DoorOpen(20));
            Add(Sound.DoorShut, SoundSynth.DoorShut(21));
            Add(Sound.Clunk, SoundSynth.Clunk(22));
            Add(Sound.Eat, SoundSynth.Eat(23));
            Add(Sound.Step, SoundSynth.Step(24));
            Add(Sound.Thunder, SoundSynth.Thunder(25));
            Add(Sound.Boom, SoundSynth.Boom(26));
            Add(Sound.GiantStep, SoundSynth.GiantStep(27));
            Add(Sound.ChainJolt, SoundSynth.ChainJolt(28));
            Add(Sound.Rustle, SoundSynth.Rustle(29));
            Add(Sound.Twig, SoundSynth.Twig(30));
            Add(Sound.Creak, SoundSynth.Creak(31));
            Add(Sound.WindLoop, SoundSynth.WindLoop(32));
            Add(Sound.Drone, SoundSynth.Drone(33));
            Add(Sound.TensionBed, SoundSynth.TensionBed(34));
            Add(Sound.RainLoop, SoundSynth.RainLoop(35));
        }

        void Add(Sound sound, float[] samples)
        {
            var clip = AudioClip.Create(sound.ToString(), samples.Length, 1, SoundSynth.SampleRate, false);
            clip.SetData(samples, 0);
            clips[sound] = clip;
        }

        /// <summary>The clip for a sound: one of your recordings if AudioConfig has any for it, else the built-in one.</summary>
        public AudioClip Clip(Sound sound)
        {
            var custom = config != null ? config.OverrideFor(sound) : null;
            if (custom != null)
            {
                var pick = custom[Random.Range(0, custom.Length)];
                if (pick != null) return pick;
            }
            return clips.TryGetValue(sound, out var c) ? c : null;
        }

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
