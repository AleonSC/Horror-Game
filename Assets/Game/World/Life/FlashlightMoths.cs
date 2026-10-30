using System.Collections.Generic;
using Game.Core;
using Game.Player;
using Game.Rendering;
using Game.World.Build;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// Moths drawn to your flashlight (section 6.13): the longer it's on, the more come in out of the dark and
    /// flutter about in the beam, trailing after it as you swing it. Turn it off and they're lost, milling about
    /// where they were; turn it on again and the ones still close find it again (where you stand, most of them;
    /// switched off and on while running, you've left them behind and few do). Lost ones give up after a few
    /// seconds and are gone. And a few always wander the dark round you, light or none, fluttering about their
    /// own spots: sweep your beam over one and it's drawn in, and follows the beam with the rest.
    /// </summary>
    public sealed class FlashlightMoths : MonoBehaviour
    {
        sealed class Moth
        {
            public Vector3 At, Velocity;
            public float Along, Out, Phase, Seed, GiveUp, Fade;
            public bool Lost, Leaving;
            public bool Wild;     // wandering on its own (not drawn to your light yet)
            public Vector3 Home;
        }

        readonly List<Moth> moths = new List<Moth>();
        ForestLifeConfig config;
        PlayerController player;
        Mesh wing;
        Material wingMat;
        readonly Matrix4x4[] left = new Matrix4x4[InstancedDraw.MaxPerCall], right = new Matrix4x4[InstancedDraw.MaxPerCall], bodies = new Matrix4x4[InstancedDraw.MaxPerCall];
        Mesh body;
        float onTime, spawnTimer;
        bool wasOn;

        ForestLifeSettings S => config.settings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (!Services.TryGet<PlayerController>(out _) || !Services.TryGet<WorldBuilder>(out _)) return;
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponentInChildren<FlashlightMoths>() == null)
                new GameObject("Flashlight moths").AddComponent<FlashlightMoths>().transform.SetParent(root.transform, false);
        }

        void Awake()
        {
            config = Services.Get<GameSetup>().Get<ForestLifeConfig>();
            if (config == null) config = ScriptableObject.CreateInstance<ForestLifeConfig>();
            // A wing: a rounded triangle out to +X from the body (drawn mirrored for the other side).
            wing = new Mesh { name = "Moth wing" };
            wing.SetVertices(new List<Vector3> { Vector3.zero, new Vector3(1f, 0f, 0.55f), new Vector3(1.05f, 0f, -0.15f), new Vector3(0.55f, 0f, -0.65f) });
            wing.SetTriangles(new[] { 0, 1, 2, 0, 2, 3 }, 0);
            wing.RecalculateNormals();
            wing.RecalculateBounds();
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            body = sphere.GetComponent<MeshFilter>().sharedMesh;
            Destroy(sphere);
            wingMat = RuntimeMaterials.Lit("Moth wings", new Color(0.5f, 0.45f, 0.36f), 0.15f, true);
            wingMat.enableInstancing = true;
        }

        void OnDestroy()
        {
            if (wing != null) Destroy(wing);
            if (wingMat != null) Destroy(wingMat);
        }

        void Update()
        {
            if (player == null && !Services.TryGet(out player)) return;
            if (PlayerPlace.InDarkRoom) return; // (none down there: they wait where they were)
            float dt = Time.deltaTime;
            var light = player.Flashlight;
            bool on = light != null && light.IsOn && light.Beam != null && GameFlow.IsPlaying && !PlayerPlace.InDarkRoom;
            var s = S;
            float gather = Mathf.Max(5f, s.mothGatherTime * Difficulty.Now.mothTime);

            if (on != wasOn)
            {
                if (!on) LoseThem(s);
                else FindThemAgain(light, s);
                wasOn = on;
            }

            // The wanderers: a few always about round you in the dark (new ones in further off, out of sight).
            int wild = 0;
            foreach (var m in moths) if (m.Wild) wild++;
            for (; wild < s.wanderingMoths; wild++) SpawnWild(s, wild < s.wanderingMoths / 2 && !placedWild);
            placedWild = true;

            if (on)
            {
                // The longer it's on, the more there are (up to the most).
                onTime = Mathf.Min(gather, onTime + dt);
                int want = Mathf.RoundToInt(s.flashlightMoths * Mathf.SmoothStep(0f, 1f, onTime / gather));
                int following = 0;
                foreach (var m in moths) if (!m.Lost && !m.Wild) following++;
                spawnTimer -= dt;
                if (following < want && spawnTimer <= 0f)
                {
                    spawnTimer = Random.Range(0.4f, 1.6f) * (gather / 120f);
                    Spawn(light);
                }
            }

            Vector3 from = light != null && light.Beam != null ? light.Beam.position : player.transform.position;
            Vector3 dir = light != null && light.Beam != null ? light.Beam.forward : player.transform.forward;
            float coneTan = light != null ? Mathf.Tan(light.Settings.beamAngle * 0.5f * Mathf.Deg2Rad) : 0.3f;
            for (int i = moths.Count - 1; i >= 0; i--)
            {
                var m = moths[i];
                Vector3 target;
                float speed = s.mothSpeed;
                if (m.Wild)
                {
                    // Wandering: fluttering about its own spot, low, slowly drifting.
                    if ((m.Home - player.transform.position).sqrMagnitude > s.wanderingMothRange * s.wanderingMothRange * 2f) { moths.RemoveAt(i); continue; }
                    target = m.Home + new Vector3(Mathf.Sin(Time.time * 0.5f + m.Seed * 9f) * 2f, Mathf.Sin(Time.time * 0.8f + m.Seed * 5f) * 0.6f, Mathf.Cos(Time.time * 0.4f + m.Seed * 7f) * 2f);
                    speed *= 0.6f;
                    m.Fade = Mathf.MoveTowards(m.Fade, 1f, dt);
                    // Your beam on it: drawn in, it joins the others in the light.
                    if (on && InLight(m.At, from, dir, coneTan, light))
                    {
                        m.Wild = false;
                        m.Lost = false;
                        m.Along = Mathf.Clamp(Vector3.Dot(m.At - from, dir), 1.2f, Mathf.Min(7f, light.Settings.range * 0.4f));
                        m.Out = Random.Range(0.1f, 0.9f);
                    }
                }
                else if (!m.Lost)
                {
                    // Its own spot in the beam: so far along it, so far out from the middle, wobbling round.
                    float a = m.Seed * 10f + Time.time * (0.8f + m.Seed);
                    Vector3 side = Vector3.Cross(dir, Vector3.up).normalized, up = Vector3.Cross(side, dir);
                    float r = m.Out * m.Along * coneTan;
                    target = from + dir * m.Along + (side * Mathf.Cos(a) + up * Mathf.Sin(a * 1.3f)) * r;
                    m.Fade = Mathf.MoveTowards(m.Fade, 1f, dt * 2f);
                }
                else
                {
                    // Lost: milling about where it was, then giving up and drifting off into the dark.
                    m.GiveUp -= dt;
                    if (m.GiveUp <= 0f) m.Leaving = true;
                    target = m.At + new Vector3(Mathf.Sin(Time.time * 1.7f + m.Seed * 9f), Mathf.Sin(Time.time * 2.3f + m.Seed * 5f) * 0.4f, Mathf.Cos(Time.time * 1.3f + m.Seed * 7f)) * 0.6f;
                    speed *= 0.5f;
                    if (m.Leaving)
                    {
                        target = m.At + (m.At - player.transform.position).normalized * 3f + Vector3.up;
                        m.Fade = Mathf.MoveTowards(m.Fade, 0f, dt * 0.7f);
                        if (m.Fade <= 0f) { moths.RemoveAt(i); continue; }
                    }
                }
                // Jinking flight toward it.
                Vector3 jink = new Vector3(Mathf.PerlinNoise(m.Seed * 30f, Time.time * 4f) - 0.5f, Mathf.PerlinNoise(Time.time * 4f, m.Seed * 30f) - 0.5f, Mathf.PerlinNoise(m.Seed * 17f, Time.time * 4.5f) - 0.5f) * 3f;
                Vector3 want = (target - m.At) * 2.5f + jink;
                if (want.magnitude > speed) want = want.normalized * speed;
                m.Velocity = Vector3.MoveTowards(m.Velocity, want, dt * 12f);
                m.At += m.Velocity * dt;
                m.Phase += dt * (30f + m.Seed * 10f);
            }
            Draw();
        }

        bool placedWild;

        // In your beam (its cone, out to where it fades), with a little to spare: a moth there sees it.
        static bool InLight(Vector3 p, Vector3 from, Vector3 dir, float coneTan, Flashlight light)
        {
            Vector3 to = p - from;
            float along = Vector3.Dot(to, dir);
            if (along < 0.3f || along > light.Settings.range * 0.85f) return false;
            float off = (to - dir * along).magnitude;
            return off <= along * coneTan * 1.3f + 0.4f;
        }

        // A wanderer, somewhere round you (at the start, anywhere; after, further off, coming in out of the dark).
        void SpawnWild(ForestLifeSettings s, bool anywhere)
        {
            Vector3 you = player.transform.position;
            float a = Random.Range(0f, Mathf.PI * 2f);
            float r = s.wanderingMothRange * (anywhere ? Mathf.Sqrt(Random.Range(0.1f, 1f)) : Random.Range(0.75f, 1f));
            var home = new Vector3(you.x + Mathf.Cos(a) * r, 0f, you.z + Mathf.Sin(a) * r);
            home.y = (Physics.Raycast(home + Vector3.up * 30f, Vector3.down, out var hit, 80f, Layers.Mask(Layers.Ground), QueryTriggerInteraction.Ignore) ? hit.point.y : you.y) + Random.Range(0.8f, 3f);
            moths.Add(new Moth { At = home, Home = home, Wild = true, Seed = Random.value, Phase = Random.value * 10f, Fade = 0f });
        }

        void Spawn(Flashlight light)
        {
            var s = S;
            // In out of the dark round the beam, a little off to one side.
            Vector3 from = light.Beam.position, dir = light.Beam.forward;
            Vector3 at = from + dir * Random.Range(4f, 9f) + Random.onUnitSphere * Random.Range(3f, 6f);
            moths.Add(new Moth
            {
                At = at,
                Along = Random.Range(1.2f, Mathf.Min(7f, light.Settings.range * 0.4f)),
                Out = Random.Range(0.1f, 0.9f),
                Seed = Random.value,
                Phase = Random.value * 10f,
                Fade = 0f,
            });
        }

        // The light's gone: they're lost where they are, each to give up in its own time.
        void LoseThem(ForestLifeSettings s)
        {
            foreach (var m in moths)
            {
                if (m.Lost || m.Wild) continue;
                m.Lost = true;
                m.Leaving = false;
                m.GiveUp = Random.Range(Mathf.Min(s.mothGiveUp.x, s.mothGiveUp.y), Mathf.Max(s.mothGiveUp.x, s.mothGiveUp.y));
            }
        }

        // The light's back: the ones still near enough may find it again (fewer the further they are from you:
        // stand still and most do; run off and you leave them behind). Those that don't stay lost.
        void FindThemAgain(Flashlight light, ForestLifeSettings s)
        {
            Vector3 from = light.Beam.position;
            int kept = 0, before = 0;
            foreach (var m in moths)
            {
                if (!m.Lost) continue;
                before++;
                if (m.Leaving) continue;
                float near = 1f - Mathf.Clamp01((m.At - from).magnitude / Mathf.Max(1f, s.mothComeBackRange));
                if (Random.value < s.mothComeBack * Mathf.Sqrt(near))
                {
                    m.Lost = false;
                    kept++;
                }
            }
            // The swarm builds again from what came back.
            float gather = Mathf.Max(5f, s.mothGatherTime * Difficulty.Now.mothTime);
            onTime = s.flashlightMoths > 0 ? gather * Mathf.Clamp01(kept / (float)s.flashlightMoths) : 0f;
        }

        void Draw()
        {
            int n = 0;
            foreach (var m in moths)
            {
                if (n >= InstancedDraw.MaxPerCall) break;
                float size = 0.035f * m.Fade;
                if (size <= 0.001f) continue;
                Vector3 heading = m.Velocity.sqrMagnitude > 0.0001f ? m.Velocity : Vector3.forward;
                var rot = Quaternion.LookRotation(heading);
                float flap = Mathf.Sin(m.Phase) * 65f;
                right[n] = Matrix4x4.TRS(m.At, rot * Quaternion.Euler(0f, 0f, flap), Vector3.one * size);
                left[n] = Matrix4x4.TRS(m.At, rot * Quaternion.Euler(0f, 0f, -flap), new Vector3(-size, size, size));
                bodies[n] = Matrix4x4.TRS(m.At, rot, new Vector3(0.012f, 0.012f, 0.03f) * m.Fade);
                n++;
            }
            if (n == 0) return;
            var bounds = new Bounds(player.transform.position, Vector3.one * 60f);
            InstancedDraw.Draw(wing, 0, wingMat, right, n, bounds, Layers.Effects, false);
            InstancedDraw.Draw(wing, 0, wingMat, left, n, bounds, Layers.Effects, false);
            InstancedDraw.Draw(body, 0, wingMat, bodies, n, bounds, Layers.Effects, false);
        }
    }
}
