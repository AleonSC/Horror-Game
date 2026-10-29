using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Core;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// The music (section 5.3). Follows the mood (<see cref="MusicMoodChanged"/>): Calm, Tension, Danger, Watcher,
    /// GameOver. A big entrance plays its theme instead (an intro, then a loop until it's gone). Fades quickly
    /// toward worse and slowly back toward calm. A mood with no music falls back down the chain (Watcher to
    /// Danger to Tension to silence). Your own tracks (AudioConfig > Music, or Resources/Music/&lt;Mood&gt;/) beat
    /// the built-in ones, which are made in code on a background thread at startup.
    /// </summary>
    public sealed class MusicManager : MonoBehaviour
    {
        AudioConfig config;
        readonly AudioSource[] decks = new AudioSource[2];
        int live;
        float[] deckTarget = { 0f, 0f };
        float fadeTime = 1f;

        readonly Dictionary<MusicMood, AudioClip> builtInMoods = new Dictionary<MusicMood, AudioClip>();
        readonly Dictionary<EntityKind, (AudioClip intro, AudioClip loop)> builtInThemes = new Dictionary<EntityKind, (AudioClip, AudioClip)>();
        Task<Dictionary<string, float[]>> synth;
        readonly List<System.IDisposable> subs = new List<System.IDisposable>();
        readonly List<EntityKind> bosses = new List<EntityKind>();
        readonly Dictionary<MusicMood, AudioClip[]> fromResources = new Dictionary<MusicMood, AudioClip[]>();

        MusicMood mood = MusicMood.Calm;
        string playingKey = "";
        AudioClip[] playlist;
        int playlistIndex;

        public MusicMood Mood => mood;
        /// <summary>The built-in music has been made (it's made on a background thread at startup).</summary>
        public bool Ready => synth == null;
        /// <summary>What's playing, for the dev window.</summary>
        public string Playing => string.IsNullOrEmpty(playingKey) ? "nothing" : playingKey;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponent<MusicManager>() == null)
                root.gameObject.AddComponent<MusicManager>();
        }

        void Awake()
        {
            config = Services.Get<GameSetup>().Get<AudioConfig>();
            for (int i = 0; i < 2; i++)
            {
                var go = new GameObject(i == 0 ? "Music A" : "Music B");
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                src.volume = 0f;
                decks[i] = src;
            }
            // Made off the main thread: it takes a moment, and nothing needs it straight away.
            synth = Task.Run(() => new Dictionary<string, float[]>
            {
                ["Tension"] = MusicSynth.Tension(1), ["Danger"] = MusicSynth.Danger(2), ["Watcher"] = MusicSynth.Watcher(3),
                ["BlindIntro"] = MusicSynth.BlindIntro(4), ["BlindLoop"] = MusicSynth.BlindLoop(5),
                ["GiantIntro"] = MusicSynth.GiantIntro(6), ["GiantLoop"] = MusicSynth.GiantLoop(7),
                ["FlierIntro"] = MusicSynth.FlierIntro(8), ["FlierLoop"] = MusicSynth.FlierLoop(9),
            });
            Services.Register(this);
        }

        void Start()
        {
            var mixerGroup = Services.TryGet<SoundPlayer>(out var sounds) ? sounds.Group(MixerNames.Music) : null;
            foreach (var d in decks) d.outputAudioMixerGroup = mixerGroup;
            if (!Services.TryGet<EventBus>(out var events)) return;
            subs.Add(events.Subscribe<MusicMoodChanged>(e => { mood = e.Mood; Refresh(); }));
            subs.Add(events.Subscribe<BossThemeStarted>(e => { bosses.Remove(e.Kind); bosses.Add(e.Kind); Refresh(true); }));
            subs.Add(events.Subscribe<BossThemeEnded>(e => { bosses.Remove(e.Kind); Refresh(); }));
            subs.Add(events.Subscribe<BossThemeFocus>(e => Focus(e.Kind)));
            subs.Add(events.Subscribe<RunReset>(_ => { bosses.Clear(); mood = MusicMood.Calm; Refresh(); }));
        }

        void OnDestroy()
        {
            foreach (var s in subs) s.Dispose();
            Services.Unregister(this);
            foreach (var c in builtInMoods.Values) Destroy(c);
            foreach (var t in builtInThemes.Values) { Destroy(t.intro); Destroy(t.loop); }
        }

        void Update()
        {
            if (synth != null && synth.IsCompleted) MakeClips();

            float master = config != null ? config.musicVolume : 0.7f;
            for (int i = 0; i < 2; i++)
            {
                var d = decks[i];
                float target = deckTarget[i] * master;
                d.volume = Mathf.MoveTowards(d.volume, target, Time.unscaledDeltaTime / Mathf.Max(0.05f, fadeTime) * master);
                if (d.volume <= 0.0001f && deckTarget[i] <= 0f && d.isPlaying) d.Stop();
            }

            // Your playlists move on to the next track when one ends.
            var liveDeck = decks[live];
            if (playlist != null && playlist.Length > 0 && !liveDeck.isPlaying && !AudioListener.pause && deckTarget[live] > 0f)
            {
                playlistIndex = (playlistIndex + 1) % playlist.Length;
                liveDeck.clip = playlist[playlistIndex];
                liveDeck.loop = playlist.Length == 1;
                liveDeck.Play();
            }
        }

        void MakeClips()
        {
            var data = synth.Result;
            synth = null;
            AudioClip Clip(string name) => MakeClip(name, data[name]);
            builtInMoods[MusicMood.Tension] = Clip("Tension");
            builtInMoods[MusicMood.Danger] = Clip("Danger");
            builtInMoods[MusicMood.Watcher] = Clip("Watcher");
            builtInThemes[EntityKind.Blind] = (Clip("BlindIntro"), Clip("BlindLoop"));
            builtInThemes[EntityKind.Giant] = (Clip("GiantIntro"), Clip("GiantLoop"));
            builtInThemes[EntityKind.Flier] = (Clip("FlierIntro"), Clip("FlierLoop"));
            Refresh();
        }

        static AudioClip MakeClip(string name, float[] samples)
        {
            var clip = AudioClip.Create(name, samples.Length, 1, MusicSynth.SampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // Work out what should be playing and fade to it if it's changed.
        void Refresh(bool restartTheme = false)
        {
            if (bosses.Count > 0 && mood != MusicMood.GameOver)
            {
                var boss = bosses[bosses.Count - 1];
                string key = $"{boss} theme";
                if (key == playingKey && !restartTheme) return;
                if (!Theme(boss, out var intro, out var loop)) { PlayMood(); return; }
                PlayTheme(key, intro, loop);
                return;
            }
            PlayMood();
        }

        // Its theme back on top (it's started already, and something's come since): into its loop, not its intro.
        void Focus(EntityKind kind)
        {
            if (!bosses.Contains(kind) || bosses[bosses.Count - 1] == kind || mood == MusicMood.GameOver) return;
            bosses.Remove(kind);
            bosses.Add(kind);
            if (!Theme(kind, out var intro, out var loop)) return;
            PlayTheme($"{kind} theme", null, loop != null ? loop : intro);
            fadeTime = 1.2f;
        }

        void PlayMood()
        {
            var m = mood;
            AudioClip[] tracks = null;
            // Down the chain until something has music.
            while (true)
            {
                tracks = Tracks(m);
                if (tracks != null || m == MusicMood.Calm || m == MusicMood.GameOver || m == MusicMood.Intro) break;
                m = m == MusicMood.Watcher ? MusicMood.Danger : m == MusicMood.Danger ? MusicMood.Tension : MusicMood.Calm;
            }
            string key = tracks != null ? $"{m}" : "";
            if (key == playingKey) return;
            bool worse = Rank(m) > Rank(MoodOf(playingKey));
            fadeTime = config == null ? 1f : worse ? config.fadeToWorse : config.fadeToCalm;
            playingKey = key;
            playlist = tracks != null && tracks.Length > 1 ? tracks : null;
            playlistIndex = tracks != null ? Random.Range(0, tracks.Length) : 0;
            StartDeck(tracks != null ? tracks[playlistIndex] : null, tracks != null && tracks.Length == 1, null);
        }

        void PlayTheme(string key, AudioClip intro, AudioClip loop)
        {
            fadeTime = 0.4f;
            playingKey = key;
            playlist = null;
            StartDeck(intro != null ? intro : loop, intro == null, intro != null ? loop : null);
        }

        // Fade the live deck out and start this on the other one. `then` follows `clip` seamlessly (a theme's loop).
        void StartDeck(AudioClip clip, bool loop, AudioClip then)
        {
            deckTarget[live] = 0f;
            if (clip == null) return;
            live = 1 - live;
            var d = decks[live];
            d.Stop();
            d.clip = clip;
            d.loop = loop;
            d.volume = 0f;
            deckTarget[live] = 1f;
            double start = AudioSettings.dspTime + 0.05;
            d.PlayScheduled(start);
            if (then != null) StartCoroutine(FollowWith(d, then, start + (double)clip.samples / clip.frequency));
        }

        System.Collections.IEnumerator FollowWith(AudioSource deck, AudioClip loop, double at)
        {
            var intro = deck.clip;
            // Just before the intro ends, hand over to the loop on the same deck (no gap, no fade).
            while (AudioSettings.dspTime < at - 0.2)
            {
                if (deck.clip != intro) yield break; // (something else took the deck)
                yield return null;
            }
            if (deck.clip != intro) yield break;
            deck.SetScheduledEndTime(at);
            yield return new WaitForSecondsRealtime(0.25f);
            if (deck.clip != intro) yield break;
            deck.clip = loop;
            deck.loop = true;
            deck.Play();
        }

        AudioClip[] Tracks(MusicMood m)
        {
            var yours = config != null ? config.TracksFor(m) : null;
            if (yours != null) return yours;
            if (!fromResources.TryGetValue(m, out var res))
            {
                res = Resources.LoadAll<AudioClip>($"Music/{m}");
                fromResources[m] = res = res != null && res.Length > 0 ? res : null;
            }
            if (res != null) return res;
            return builtInMoods.TryGetValue(m, out var clip) ? new[] { clip } : null;
        }

        bool Theme(EntityKind kind, out AudioClip intro, out AudioClip loop)
        {
            intro = loop = null;
            if (config != null && config.ThemeFor(kind, out var yours))
            {
                intro = yours.intro;
                loop = yours.loop;
                return true;
            }
            if (!builtInThemes.TryGetValue(kind, out var built)) return false;
            intro = built.intro;
            loop = built.loop;
            return true;
        }

        static MusicMood MoodOf(string key) => System.Enum.TryParse<MusicMood>(key, out var m) ? m : MusicMood.Calm;

        static int Rank(MusicMood m) => (int)m;
    }
}
