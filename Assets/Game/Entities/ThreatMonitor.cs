using Game.Audio;
using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.Entities
{
    /// <summary>
    /// Works out how close danger is, from every hunting thing (<see cref="Threats"/>), for the sound states
    /// (section 5.1): Dread when something visible is within a few metres (the world ducks, slow breathing
    /// behind your head), Hush when something is close enough to take you (everything but your heartbeat
    /// fades). And the music's mood (section 5.3): Tension with something in sight range, Danger when one is
    /// coming for you, Watcher while the sky watches, GameOver while you're being taken away.
    /// </summary>
    public sealed class ThreatMonitor : MonoBehaviour
    {
        const float DreadDistance = 7f;
        const float TensionDistance = 30f;

        ThreatLevel level;
        MusicMood mood;
        float heldSince;
        float nextBeat, nextBreath;

        public ThreatLevel Level => level;

        void Update()
        {
            if (!Services.TryGet<ScourSpawner>(out var spawner)) return;
            var next = ThreatLevel.None;
            float hushDistance = spawner.Settings.killDistance + 0.5f;
            bool near = false, hunting = false, watching = false;
            foreach (var t in Threats.All)
            {
                if (t == null) continue;
                if (t.ThreatWatching) watching = true;
                if (!t.ThreatVisible) continue;
                float d = t.DistanceToPlayer;
                if (t.ThreatHunting) hunting = true;
                if (d <= TensionDistance) near = true;
                if (t.ThreatTaking || d <= hushDistance) next = ThreatLevel.Hush;
                else if (d <= DreadDistance && next == ThreatLevel.None) next = ThreatLevel.Dread;
            }

            if (next != level)
            {
                level = next;
                if (Services.TryGet<EventBus>(out var events)) events.Publish(new ThreatLevelChanged(level));
            }
            UpdateMood(near, hunting, watching);
            PlayBody();
        }

        void UpdateMood(bool near, bool hunting, bool watching)
        {
            bool taken = Services.TryGet<CatchFlow>(out var catchFlow) && catchFlow.Active;
            var next = taken ? MusicMood.GameOver
                : watching ? MusicMood.Watcher
                : hunting ? MusicMood.Danger
                : near ? MusicMood.Tension
                : MusicMood.Calm;
            // Easing off waits a few seconds, so the music doesn't flicker as things duck in and out of view.
            if (next >= mood) heldSince = Time.time;
            else if (Time.time - heldSince < 4f && mood != MusicMood.GameOver) return;
            if (next == mood) return;
            mood = next;
            if (Services.TryGet<EventBus>(out var events)) events.Publish(new MusicMoodChanged(mood));
        }

        // Your own body: breathing under dread, a heartbeat in the hush.
        void PlayBody()
        {
            if (!Services.TryGet<SoundPlayer>(out var sounds)) return;
            float now = Time.time;
            if (level == ThreatLevel.Hush && now >= nextBeat)
            {
                nextBeat = now + 0.75f;
                sounds.Play2D(Sound.Heartbeat, MixerNames.Player, 0.9f);
            }
            else if (level == ThreatLevel.Dread && now >= nextBreath)
            {
                nextBreath = now + 3.6f;
                sounds.Play2D(Sound.Breath, MixerNames.Player, 0.6f, Random.Range(0.9f, 1.05f));
            }
        }
    }
}
