using System.Collections.Generic;
using Game.Audio;
using Game.Core;
using Game.Player;
using Game.World.Generation;
using UnityEngine;

namespace Game.World.Build
{
    /// <summary>
    /// The bushes have no colliders (section 3.6): you push through them. They lean away (the renderer does that)
    /// and rustle as you go, and the rustle is a noise that things hunting by sound can hear (louder running,
    /// barely anything creeping).
    /// </summary>
    public sealed class Undergrowth
    {
        const float Cell = 4f;
        readonly Dictionary<long, List<Spot>> cells = new Dictionary<long, List<Spot>>();
        readonly float mapSize;
        float lastRustle;

        public Undergrowth(WorldPlan plan)
        {
            mapSize = plan.Size;
            foreach (var b in plan.Bushes) Add(b);
        }

        void Add(Spot s)
        {
            long key = Key(Mathf.FloorToInt(s.Position.x / Cell), Mathf.FloorToInt(s.Position.y / Cell));
            if (!cells.TryGetValue(key, out var list)) cells[key] = list = new List<Spot>();
            list.Add(s);
        }

        long Key(int x, int z)
        {
            int n = Mathf.CeilToInt(mapSize / Cell);
            x = ((x % n) + n) % n;
            z = ((z % n) + n) % n;
            return (long)z * n + x;
        }

        /// <summary>Is this point (in the world, any copy of the map) inside a bush? Gives how big it is.</summary>
        public bool InBush(Vector3 p, out float size)
        {
            size = 0f;
            var m = Wrap.Into(new Vector2(p.x, p.z), mapSize);
            int cx = Mathf.FloorToInt(m.x / Cell), cz = Mathf.FloorToInt(m.y / Cell);
            for (int dz = -1; dz <= 1; dz++)
            for (int dx = -1; dx <= 1; dx++)
            {
                if (!cells.TryGetValue(Key(cx + dx, cz + dz), out var list)) continue;
                foreach (var b in list)
                {
                    float r = b.Size * 0.55f + 0.25f;
                    if (Wrap.SqrDistance(b.Position, m, mapSize) < r * r)
                    {
                        size = b.Size;
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>Each frame: rustle (and make a noise) while the player moves through a bush.</summary>
        public void Update(PlayerController player)
        {
            if (player == null || !player.Moving || player.Seat != null) return;
            if (!InBush(player.transform.position, out float size)) return;
            float every = player.Running ? 0.25f : player.Crouching ? 0.9f : 0.45f;
            if (Time.time - lastRustle < every) return;
            lastRustle = Time.time;
            float loud = player.Running ? 1f : player.Crouching ? 0.25f : 0.6f;
            if (Services.TryGet<SoundPlayer>(out var sounds))
                sounds.Play(Sound.Rustle, player.transform.position + Vector3.up * 0.6f, MixerNames.Player, loud * Mathf.Clamp(size, 0.5f, 1.2f), Random.Range(0.85f, 1.2f), 14f);
            if (Services.TryGet<EventBus>(out var events))
                events.Publish(new NoiseEmitted(player.transform.position, player.Running ? 14f : player.Crouching ? 1.5f : 6f, "rustling"));
        }
    }
}
