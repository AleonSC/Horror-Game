using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Plays the soundtrack. The game is always in one musical mood, worked out from what's happening:
///
///   Calm     - nothing around, just you and the woods
///   Intro    - the opening lines are on screen
///   Tension  - an entity is out there watching you (within tensionDistance)
///   Danger   - one is right on you: close by, in its crescendo, charging, or taking you
///   Watcher  - the eye in the sky has opened, or the giant is walking
///   GameOver - you've been taken
///
/// Each mood has its own playlist. Tracks crossfade when the mood changes (quickly when things get
/// worse, slowly when they calm down) and when a track ends, picking the next one at random without
/// repeating. A mood with no tracks falls back to a calmer one (Danger -> Tension -> Calm), and GameOver
/// with no tracks fades to silence. The music ducks with EntityDread, so it drops away when one is close.
///
/// Adding music, either way works (both are merged):
///   - put audio files in Assets/Scripts/Resources/Music/Calm, /Intro, /Tension, /Danger, /Watcher or
///     /GameOver (any file Unity imports as an AudioClip), or
///   - assign clips to the playlists on this component in the Inspector.
///   One-off stings (PlayStinger) can go in Resources/Music/Stingers.
///
/// With no music added it stays silent; the procedural ambience carries the game.
/// DarkForestGenerator adds this automatically. Volume is in the pause menu.
/// </summary>
public class MusicManager : MonoBehaviour
{
    public enum Mood { Calm, Intro, Tension, Danger, Watcher, GameOver }

    public static MusicManager Instance { get; private set; }
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() => Instance = null;

    const string VolumeKey = "Horror.MusicVolume";
    const string ResourceFolder = "Music";

    [Header("Volume")]
    [Range(0f, 1f)] public float volume = 0.6f;
    [Tooltip("Mood volumes relative to the main music volume.")]
    [Range(0f, 1f)] public float calmVolume = 0.7f;
    [Range(0f, 1f)] public float introVolume = 0.8f;
    [Range(0f, 1f)] public float tensionVolume = 0.85f;
    [Range(0f, 1f)] public float dangerVolume = 1f;
    [Range(0f, 1f)] public float watcherVolume = 1f;
    [Range(0f, 1f)] public float gameOverVolume = 0.9f;
    [Range(0f, 1f)] public float stingerVolume = 1f;
    [Tooltip("How much the music drops away when an entity is right on you (EntityDread's silence).")]
    [Range(0f, 1f)] public float dreadDuck = 0.8f;

    [Header("Fading")]
    [Tooltip("Crossfade time when things get worse.")]
    public float escalateFade = 1.2f;
    [Tooltip("Crossfade time when things calm down.")]
    public float calmDownFade = 5f;
    [Tooltip("Crossfade between tracks in the same mood.")]
    public float trackFade = 3f;
    [Tooltip("The mood has to hold this long before the music calms down (stops it flip-flopping).")]
    public float calmDownDelay = 6f;

    [Header("Mood Triggers")]
    [Tooltip("A visible entity within this distance makes the music tense.")]
    public float tensionDistance = 30f;
    [Tooltip("A visible entity within this distance is danger.")]
    public float dangerDistance = 8f;

    [Header("Playlists (added to whatever's in Resources/Music/<Mood>)")]
    public AudioClip[] calmTracks;
    public AudioClip[] introTracks;
    public AudioClip[] tensionTracks;
    public AudioClip[] dangerTracks;
    public AudioClip[] watcherTracks;
    public AudioClip[] gameOverTracks;
    public AudioClip[] stingers;

    /// <summary>The mood the music is playing (after fallbacks).</summary>
    public Mood CurrentMood { get; private set; } = Mood.Calm;

    readonly Dictionary<Mood, List<AudioClip>> playlists = new Dictionary<Mood, List<AudioClip>>();
    readonly Dictionary<Mood, AudioClip> lastPlayed = new Dictionary<Mood, AudioClip>();
    readonly List<AudioClip> stingerClips = new List<AudioClip>();
    AudioSource[] decks;      // two sources to crossfade between
    AudioSource stingerSource;
    int activeDeck;
    float[] deckGain = new float[2];
    float[] deckTarget = new float[2];
    float[] deckFadeSpeed = new float[2];
    bool hasMood;
    Mood? overrideMood;
    Mood pendingCalmer;
    float calmerTimer;

    void Awake()
    {
        Tuning.Apply(this); // saved tuning (Save Tuning on the component's menu), before anything uses it
        if (Instance != null && Instance != this) { Destroy(this); return; }
        Instance = this;
        volume = PlayerPrefs.GetFloat(VolumeKey, volume);

        decks = new AudioSource[2];
        for (int i = 0; i < 2; i++) decks[i] = MakeSource("Music Deck " + (i + 1));
        stingerSource = MakeSource("Music Stinger");

        LoadPlaylist(Mood.Calm, calmTracks);
        LoadPlaylist(Mood.Intro, introTracks);
        LoadPlaylist(Mood.Tension, tensionTracks);
        LoadPlaylist(Mood.Danger, dangerTracks);
        LoadPlaylist(Mood.Watcher, watcherTracks);
        LoadPlaylist(Mood.GameOver, gameOverTracks);
        stingerClips.AddRange(Resources.LoadAll<AudioClip>(ResourceFolder + "/Stingers"));
        if (stingers != null) foreach (var clip in stingers) if (clip != null) stingerClips.Add(clip);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    AudioSource MakeSource(string name)
    {
        var go = new GameObject(name);
        go.transform.SetParent(transform, false);
        var src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;   // music is in your head, not in the forest
        src.bypassReverbZones = true;
        src.priority = 0;
        src.ignoreListenerVolume = true; // the sound effects slider doesn't touch the music; MasterVolume does
        src.volume = 0f;
        return src;
    }

    void LoadPlaylist(Mood mood, AudioClip[] assigned)
    {
        var list = new List<AudioClip>(Resources.LoadAll<AudioClip>(ResourceFolder + "/" + mood));
        if (assigned != null) foreach (var clip in assigned) if (clip != null && !list.Contains(clip)) list.Add(clip);
        playlists[mood] = list;
    }

    // ---------------------------------------------------------------- Public API

    /// <summary>Music volume (0-1), saved between sessions. The pause menu sets this.</summary>
    public float Volume
    {
        get => volume;
        set
        {
            volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(VolumeKey, volume);
        }
    }

    /// <summary>Forces a mood regardless of what's happening (for scripted moments) until ClearOverride.</summary>
    public void SetOverride(Mood mood) => overrideMood = mood;

    public void ClearOverride() => overrideMood = null;

    /// <summary>Adds a track to a mood's playlist at runtime.</summary>
    public void AddTrack(Mood mood, AudioClip clip)
    {
        if (clip != null && !playlists[mood].Contains(clip)) playlists[mood].Add(clip);
    }

    /// <summary>Plays a one-off sting over the music (a given clip, or a random one from the stingers).</summary>
    public void PlayStinger(AudioClip clip = null)
    {
        if (clip == null && stingerClips.Count > 0) clip = stingerClips[Random.Range(0, stingerClips.Count)];
        if (clip == null) return;
        stingerSource.PlayOneShot(clip, PauseMenu.MasterVolume * volume * stingerVolume);
    }

    // ---------------------------------------------------------------- Mood

    void Update()
    {
        Mood wanted = Resolve(overrideMood ?? DetectMood());

        if (!hasMood)
        {
            hasMood = true;
            SwitchTo(wanted, 2f);
        }
        else if (wanted != CurrentMood)
        {
            if (Rank(wanted) > Rank(CurrentMood))
            {
                SwitchTo(wanted, escalateFade); // things got worse: change right away
                calmerTimer = 0f;
            }
            else
            {
                // Calming down: only once it has stayed calmer for a while.
                if (wanted != pendingCalmer) { pendingCalmer = wanted; calmerTimer = 0f; }
                calmerTimer += Time.unscaledDeltaTime;
                if (calmerTimer >= calmDownDelay) SwitchTo(wanted, calmDownFade);
            }
        }
        else calmerTimer = 0f;

        // Next track when this one is about to end (single tracks just loop).
        var deck = decks[activeDeck];
        if (deck.clip != null && !deck.loop && deck.isPlaying && deck.time >= deck.clip.length - trackFade)
            PlayNext(CurrentMood, trackFade);
        else if (deck.clip != null && !deck.loop && !deck.isPlaying && !AudioListener.pause)
            PlayNext(CurrentMood, 0.5f);

        UpdateVolumes();
    }

    Mood DetectMood()
    {
        if (GameOverScreen.IsActive) return Mood.GameOver;
        if (WatcherEvent.Instance != null && WatcherEvent.Instance.IsActive) return Mood.Watcher;

        var cam = Camera.main;
        Vector3 listener = cam != null ? cam.transform.position : Vector3.zero;
        float nearest = float.MaxValue;
        foreach (var entity in ShadowEntity.All)
        {
            if (entity == null || !entity.IsVisible) continue;
            var state = entity.CurrentState;
            if (state == ShadowEntity.State.Crescendo || state == ShadowEntity.State.Charging || state == ShadowEntity.State.Killing)
                return Mood.Danger;
            nearest = Mathf.Min(nearest, Vector3.Distance(entity.transform.position, listener));
        }
        if (nearest <= dangerDistance) return Mood.Danger;
        if (nearest <= tensionDistance) return Mood.Tension;
        if (IntroText.Playing) return Mood.Intro;
        return Mood.Calm;
    }

    /// <summary>Falls back to a calmer mood that has music, if this one has none. GameOver falls back to silence.</summary>
    Mood Resolve(Mood mood)
    {
        Mood m = mood;
        for (int i = 0; i < 6; i++)
        {
            if (playlists[m].Count > 0) return m;
            switch (m)
            {
                case Mood.Watcher: m = Mood.Danger; break;
                case Mood.Danger: m = Mood.Tension; break;
                case Mood.Tension: m = Mood.Calm; break;
                case Mood.Intro: m = Mood.Calm; break;
                default: return mood; // nothing to fall back to: plays silence
            }
        }
        return mood;
    }

    static int Rank(Mood mood)
    {
        switch (mood)
        {
            case Mood.GameOver: return 5;
            case Mood.Watcher: return 4;
            case Mood.Danger: return 3;
            case Mood.Tension: return 2;
            case Mood.Intro: return 1;
            default: return 0;
        }
    }

    void SwitchTo(Mood mood, float fade)
    {
        CurrentMood = mood;
        pendingCalmer = mood;
        calmerTimer = 0f;
        PlayNext(mood, fade);
    }

    /// <summary>Crossfades to a fresh track from a mood's playlist (or to silence if it's empty).</summary>
    void PlayNext(Mood mood, float fade)
    {
        var list = playlists[mood];
        AudioClip clip = null;
        if (list.Count > 0)
        {
            lastPlayed.TryGetValue(mood, out var last);
            clip = list[Random.Range(0, list.Count)];
            if (list.Count > 1 && clip == last) clip = list[(list.IndexOf(clip) + 1 + Random.Range(0, list.Count - 1)) % list.Count];
            lastPlayed[mood] = clip;
        }

        // Fade the current deck out...
        FadeDeck(activeDeck, 0f, fade);

        // ...and the new track in on the other one.
        activeDeck = 1 - activeDeck;
        var deck = decks[activeDeck];
        deck.Stop();
        deck.clip = clip;
        if (clip == null) return;
        deck.loop = list.Count == 1;
        deck.time = 0f;
        deck.Play();
        deckGain[activeDeck] = 0f;
        FadeDeck(activeDeck, 1f, fade);
    }

    void FadeDeck(int index, float target, float seconds)
    {
        deckTarget[index] = target;
        deckFadeSpeed[index] = 1f / Mathf.Max(seconds, 0.01f);
    }

    void UpdateVolumes()
    {
        float duck = (1f - dreadDuck * EntityDread.Silence) * (1f - PortalDoor.RoomQuiet) // silent in the dark room
                     * (1f - Mathf.Clamp01(MusicCue.Duck));                                        // out of the way of a cue
        float dt = Time.unscaledDeltaTime;
        for (int i = 0; i < 2; i++)
        {
            deckGain[i] = Mathf.MoveTowards(deckGain[i], deckTarget[i], deckFadeSpeed[i] * dt);
            // A deck carries the mood it was started for; the active one is the current mood.
            float moodVolume = MoodVolume(CurrentMood);
            decks[i].volume = PauseMenu.MasterVolume * volume * moodVolume * deckGain[i] * duck;
            if (deckGain[i] <= 0f && i != activeDeck && decks[i].isPlaying) decks[i].Stop();
        }
    }

    float MoodVolume(Mood mood)
    {
        switch (mood)
        {
            case Mood.Intro: return introVolume;
            case Mood.Tension: return tensionVolume;
            case Mood.Danger: return dangerVolume;
            case Mood.Watcher: return watcherVolume;
            case Mood.GameOver: return gameOverVolume;
            default: return calmVolume;
        }
    }
}
