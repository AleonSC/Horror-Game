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
        readonly List<AudioLowPassFilter> filters = new List<AudioLowPassFilter>();
        readonly List<bool> outside = new List<bool>();
        readonly List<bool> ui = new List<bool>();
        readonly HashSet<int> shelters = new HashSet<int>();
        readonly List<System.IDisposable> subs = new List<System.IDisposable>();

        /// <summary>
        /// How muffled the world outside is (0 not at all, 1 fully): inside the car, a shack, the cabin or any
        /// shelter, sounds from outside come through dull and low, as through walls. Loops elsewhere use it via
        /// <see cref="Muffled"/>.
        /// </summary>
        public static float Muffle { get; private set; }

        /// <summary>
        /// How deafened you are (0 not, 1 fully): stunned by something huge (the giant's shockwave), everything
        /// comes through dull and distant, clearing as your ears come back. The UI's sounds (and whatever's played on
        /// its group to cut through, like the ringing) aren't touched. Set by <see cref="Hearing"/>.
        /// </summary>
        public static float Deafened { get; set; }

        /// <summary>The cut-off for a sound muffled this much (22 kHz: clear; 550 Hz: through the walls).</summary>
        public static float CutoffFor(float muffle) => Mathf.Lerp(22000f, 550f, Mathf.Pow(Mathf.Clamp01(muffle), 0.35f));
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
                var filter = go.AddComponent<AudioLowPassFilter>();
                filter.cutoffFrequency = 22000f;
                filters.Add(filter);
                outside.Add(false);
                ui.Add(false);
            }
            Muffle = 0f;
            Deafened = 0f;
            Services.Register(this);
        }

        void Start()
        {
            if (!Services.TryGet<EventBus>(out var events)) return;
            subs.Add(events.Subscribe<EnteredShelter>(e => shelters.Add(e.ShelterId)));
            subs.Add(events.Subscribe<ExitedShelter>(e => shelters.Remove(e.ShelterId)));
            subs.Add(events.Subscribe<RunReset>(_ => shelters.Clear()));
        }

        void Update()
        {
            // Closing in round you quickly as a door shuts behind you; opening out a little slower.
            float target = shelters.Count > 0 ? 1f : 0f;
            Muffle = Mathf.MoveTowards(Muffle, target, Time.unscaledDeltaTime / (target > Muffle ? 0.35f : 0.8f));
            for (int i = 0; i < pool.Count; i++)
                if (pool[i].isPlaying) filters[i].cutoffFrequency = CutoffAt(i);
        }

        void OnDestroy()
        {
            foreach (var sub in subs) sub.Dispose();
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
            Add(Sound.Hoot, SoundSynth.Hoot(36));
            Add(Sound.Croak, SoundSynth.Croak(37));
            Add(Sound.Squeak, SoundSynth.Squeak(38));
            Add(Sound.Caw, SoundSynth.Caw(39));
            Add(Sound.Splash, SoundSynth.Splash(40));
            Add(Sound.Hooves, SoundSynth.Hooves(41));
            Add(Sound.Rotors, SoundSynth.Rotors(42));
            Add(Sound.Static, SoundSynth.Static(43));
            Add(Sound.RadioSong, SoundSynth.RadioSong(44));
            Add(Sound.DeerGrunt, SoundSynth.DeerGrunt(45));
            Add(Sound.DeerBleat, SoundSynth.DeerBleat(46));
            Add(Sound.DeerSnort, SoundSynth.DeerSnort(47));
            Add(Sound.DeerAlarm, SoundSynth.DeerAlarm(48));
            Add(Sound.Gallop, SoundSynth.Gallop(49));
            // (More footsteps, each a little different.)
            Add(Sound.Step2, SoundSynth.Step(50));
            Add(Sound.Step3, SoundSynth.Step(51));
            Add(Sound.Step4, SoundSynth.Step(52));
            Add(Sound.Step5, SoundSynth.Step(53));
            Add(Sound.Step6, SoundSynth.Step(54));
            Add(Sound.Screech, SoundSynth.Screech(55));
            Add(Sound.LeavesLoop, SoundSynth.LeavesLoop(56));
            Add(Sound.CarAlarm, SoundSynth.CarAlarm(57));
            Add(Sound.FireLoop, SoundSynth.FireLoop(58));
            Add(Sound.Disperse, SoundSynth.Disperse(59));
            Add(Sound.ScourClick, SoundSynth.ScourClick(60));
            Add(Sound.RadioCreep, SoundSynth.RadioCreep(61));
            Add(Sound.BushRustle, SoundSynth.BushRustle(62));
            Add(Sound.ScourSnap, SoundSynth.ScourSnap(63));
            Add(Sound.TreeBreak, SoundSynth.TreeBreak(64));
            Add(Sound.TreeRush, SoundSynth.TreeRush(65));
            Add(Sound.TreeImpact, SoundSynth.TreeImpact(66));
            Add(Sound.PhoneRing, SoundSynth.PhoneRing(67));
            Add(Sound.TvStatic, SoundSynth.TvStatic(68));
            Add(Sound.SprayHiss, SoundSynth.SprayHiss(69));
            Add(Sound.GiantGroan, SoundSynth.GiantGroan(70));
            Add(Sound.ChainSnap, SoundSynth.ChainSnap(71));
            Add(Sound.EarRing, SoundSynth.EarRing(72));
            Add(Sound.FlierCry, SoundSynth.FlierCry(73));
            Add(Sound.DoorThud, SoundSynth.DoorThud(74));
            Add(Sound.StoneWake, SoundSynth.StoneWake(75));
            Add(Sound.BlindRoar, SoundSynth.BlindRoar(76));
            Add(Sound.BlindGrowl, SoundSynth.BlindGrowl(77));
            Add(Sound.CrawlerScreech, SoundSynth.CrawlerScreech(78));
            Add(Sound.CrawlerSkitter, SoundSynth.CrawlerSkitter(79));
            Add(Sound.CrawlerBreath, SoundSynth.CrawlerBreath(80));
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
            // Outside sounds (the forest, things out there, anything in the world more than a few metres off)
            // come through muffled when you're under cover; your own, the music and the menus never do.
            int index = pool.IndexOf(source);
            if (index >= 0)
            {
                var listener = Camera.main != null ? Camera.main.transform.position : Vector3.zero;
                outside[index] = group == MixerNames.Outside || group == MixerNames.Entities
                                 || (group == MixerNames.World && spatial && (position - listener).sqrMagnitude > 16f);
                ui[index] = group == MixerNames.UI;
                filters[index].cutoffFrequency = CutoffAt(index);
            }
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

        // Muffled under cover (if it's from outside), and dulled by deafness (unless it's the UI's).
        float CutoffAt(int i) => ui[i] ? 22000f : CutoffFor(Mathf.Max(outside[i] ? Muffle : 0f, Deafened));

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
