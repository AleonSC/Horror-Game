using Game.Core;
using UnityEngine;

namespace Game.Audio
{
    /// <summary>
    /// What you can hear, for the big moments: the whole world's sound (music and every effect) faded slowly away
    /// to nothing, so that what comes next is heard alone (played on the UI's group, which this leaves alone); and
    /// being stunned by something huge: deafened, a high ringing in your ears, the world fading back in dull and
    /// distant and slowly clearing, the ringing dying away. Restored on a new run, and never left silent for long.
    /// </summary>
    public sealed class Hearing : MonoBehaviour
    {
        /// <summary>If it's been silent this long without being brought back, it comes back anyway.</summary>
        const float LongestSilence = 25f;

        float from = 1f, to = 1f, fadeTime, fadeFor;
        float deafFrom, deafFor, deafLeft;
        float silentFor;
        AudioSource ringing;
        float ringVolume, ringFor, ringLeft;
        System.IDisposable runSub;

        static Hearing Instance => Services.TryGet<Hearing>(out var h) ? h : null;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponent<Hearing>() == null)
                root.gameObject.AddComponent<Hearing>();
        }

        void Awake() => Services.Register(this);

        void Start()
        {
            if (Services.TryGet<EventBus>(out var events)) runSub = events.Subscribe<RunStarted>(_ => RestoreNow());
        }

        void OnDestroy()
        {
            runSub?.Dispose();
            RestoreNow();
            Services.Unregister(this);
        }

        /// <summary>Fade the world's sound away to nothing over this many seconds.</summary>
        public static void FadeOut(float seconds) => Instance?.Fade(0f, seconds);

        /// <summary>Bring the world's sound back over this many seconds.</summary>
        public static void FadeIn(float seconds) => Instance?.Fade(1f, seconds);

        /// <summary>
        /// Stunned, as by a blast: deafened at once, the ringing at this volume, the world fading back in over
        /// <paramref name="seconds"/> (dull at first, clearing), the ringing dying away over the same.
        /// </summary>
        public static void Stun(float ring, float seconds) => Instance?.StunNow(ring, seconds);

        /// <summary>Everything back to normal, now.</summary>
        public static void Restore() => Instance?.RestoreNow();

        void Fade(float target, float seconds)
        {
            from = Services.TryGet<AudioStateController>(out var c) ? c.WorldVolume : 1f;
            to = target;
            fadeFor = Mathf.Max(0.01f, seconds);
            fadeTime = 0f;
        }

        void StunNow(float ring, float seconds)
        {
            seconds = Mathf.Max(0.5f, seconds);
            Fade(1f, seconds);
            from = 0f; // (from silence, whatever it was)
            deafFrom = 1f;
            deafFor = deafLeft = seconds * 1.15f;
            SoundPlayer.Deafened = 1f;
            if (ringing != null) ringing.Stop();
            ringing = Services.TryGet<SoundPlayer>(out var sounds) ? sounds.Play2D(Sound.EarRing, MixerNames.UI, ring) : null;
            ringVolume = ring;
            ringFor = ringLeft = seconds;
        }

        void RestoreNow()
        {
            from = to = 1f;
            fadeFor = 0f;
            deafLeft = 0f;
            SoundPlayer.Deafened = 0f;
            if (ringing != null) { ringing.Stop(); ringing = null; }
            silentFor = 0f;
            if (Services.TryGet<AudioStateController>(out var c)) c.SetWorldVolume(1f);
        }

        void Update()
        {
            // (Real time: it keeps going through a pause, like the mixer does.)
            float dt = Time.unscaledDeltaTime;
            if (!Services.TryGet<AudioStateController>(out var c)) return;

            if (fadeFor > 0f)
            {
                fadeTime += dt;
                float u = Mathf.Clamp01(fadeTime / fadeFor);
                // Fading out: easing away (slowly at the last); in: a slow start, opening up.
                float e = to < from ? 1f - (1f - u) * (1f - u) : u * u;
                c.SetWorldVolume(Mathf.Lerp(from, to, e));
                if (u >= 1f) fadeFor = 0f;
            }

            if (deafLeft > 0f)
            {
                deafLeft -= dt;
                float k = Mathf.Clamp01(deafLeft / deafFor);
                SoundPlayer.Deafened = deafFrom * k * k * (3f - 2f * k); // (dull a while, then clearing)
                if (deafLeft <= 0f) SoundPlayer.Deafened = 0f;
            }

            if (ringing != null)
            {
                ringLeft -= dt;
                float k = Mathf.Clamp01(ringLeft / Mathf.Max(0.01f, ringFor));
                ringing.volume = ringVolume * k * Mathf.Sqrt(k); // (piercing, then thinning to nothing)
                if (ringLeft <= 0f || !ringing.isPlaying) { ringing.Stop(); ringing = null; }
            }

            // Never left silent: if nothing brings it back, it comes back.
            silentFor = c.WorldVolume < 0.05f && fadeFor <= 0f ? silentFor + dt : 0f;
            if (silentFor > LongestSilence) Fade(1f, 3f);
        }
    }
}
