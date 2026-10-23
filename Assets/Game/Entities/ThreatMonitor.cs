using Game.Audio;
using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.Entities
{
    /// <summary>
    /// Works out how close danger is, for the sound states (section 5.1): Dread when something visible is
    /// within a few metres (the world ducks, slow breathing behind your head), Hush when something is close
    /// enough to take you (everything but your heartbeat fades).
    /// </summary>
    public sealed class ThreatMonitor : MonoBehaviour
    {
        const float DreadDistance = 7f;

        ThreatLevel level;
        float nextBeat, nextBreath;

        public ThreatLevel Level => level;

        void Update()
        {
            if (!Services.TryGet<ScourSpawner>(out var spawner)) return;
            var next = ThreatLevel.None;
            float hushDistance = spawner.Settings.killDistance + 0.5f;
            foreach (var scour in spawner.Scours)
            {
                if (scour == null || !scour.Visible) continue;
                float d = scour.DistanceToPlayer;
                if (scour.State == ScourState.Taking || scour.State == ScourState.Charging || d <= hushDistance)
                {
                    next = ThreatLevel.Hush;
                    break;
                }
                if (d <= DreadDistance) next = ThreatLevel.Dread;
            }

            if (next != level)
            {
                level = next;
                if (Services.TryGet<EventBus>(out var events)) events.Publish(new ThreatLevelChanged(level));
            }
            PlayBody();
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
