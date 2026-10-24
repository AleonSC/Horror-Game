using Game.Audio;
using Game.Core;
using Game.Player;
using Game.Rendering;
using Game.World.Build;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// The white moth (section 6.13): now and then one turns up fluttering in the dark near you, trailing a fine
    /// white dust. Catch it in your flashlight and it follows the beam; lead it to a lit lamp and it's yours, a
    /// little more sprint time for the run and your breath back. Let it slip out of your light and it gives up,
    /// wandering slowly off somewhere (you can catch it again if you find it).
    /// </summary>
    public sealed class WhiteMoth : MonoBehaviour
    {
        enum State { None, Waiting, Following, Lost, AtLamp }

        ForestLifeConfig config;
        PlayerController player;
        State state;
        Transform moth, wingL, wingR;
        Renderer halo;
        Vector3 at, velocity, wanderDir;
        Lamp lamp;
        float next, stateTime, outOfLight, phase, seed;
        bool led;

        // The dust trail: little white specks left behind, sinking and shrinking.
        const int TrailMax = 64;
        readonly Vector3[] dust = new Vector3[TrailMax];
        readonly float[] dustAge = new float[TrailMax];
        readonly Matrix4x4[] dustDraw = new Matrix4x4[TrailMax];
        int dustNext;
        float dustTimer;
        Mesh speck;
        Material dustMat;
        const float DustLife = 1.2f;

        ForestLifeSettings S => config.settings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (!Services.TryGet<PlayerController>(out _) || !Services.TryGet<WorldBuilder>(out _)) return;
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponentInChildren<WhiteMoth>() == null)
                new GameObject("White moth").AddComponent<WhiteMoth>().transform.SetParent(root.transform, false);
        }

        void Awake()
        {
            config = Services.Get<GameSetup>().Get<ForestLifeConfig>();
            if (config == null) config = ScriptableObject.CreateInstance<ForestLifeConfig>();
            next = Time.time + Random.Range(S.minWhiteMoth, S.maxWhiteMoth) * 0.5f;
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            speck = sphere.GetComponent<MeshFilter>().sharedMesh;
            Destroy(sphere);
            dustMat = RuntimeMaterials.Glow("Moth dust", new Color(0.9f, 0.93f, 1f) * 3f);
            dustMat.enableInstancing = true;
            for (int i = 0; i < TrailMax; i++) dustAge[i] = DustLife;
        }

        void OnDestroy()
        {
            if (dustMat != null) Destroy(dustMat);
        }

        void Update()
        {
            if (player == null && !Services.TryGet(out player)) return;
            float dt = Time.deltaTime;
            if (state == State.None)
            {
                if (!GameFlow.IsPlaying || PlayerPlace.InDarkRoom || Time.time < next) return;
                Spawn();
            }
            if (moth == null) { state = State.None; return; }
            stateTime += dt;
            Think(dt);
            if (moth == null) return;
            Flutter(dt);
            Trail(dt);
        }

        // ------------------------------------------------------------------ what it does

        // Somewhere in the dark ahead of you, a good way off, at about head height.
        void Spawn()
        {
            var cam = Camera.main;
            Vector3 ahead = cam != null ? Flat(cam.transform.forward).normalized : Vector3.forward;
            Vector3 dir = Quaternion.Euler(0f, Random.Range(-60f, 60f), 0f) * ahead;
            at = OnGround(player.transform.position + dir * Random.Range(9f, 14f)) + Vector3.up * Random.Range(1.2f, 2f);
            moth = AnimalModels.WhiteMoth(transform).transform;
            moth.position = at;
            wingL = moth.Find("WingL");
            wingR = moth.Find("WingR");
            halo = Effects.Glow(moth, Vector3.zero, 0.4f, new Color(0.85f, 0.9f, 1f) * 0.35f, 0.6f, 2.5f, "Moth glow");
            velocity = Vector3.zero;
            seed = Random.value * 100f;
            led = false;
            lamp = null;
            SetState(State.Waiting);
        }

        void SetState(State s)
        {
            state = s;
            stateTime = 0f;
            outOfLight = 0f;
            if (s == State.Lost)
            {
                wanderDir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                led = false; // (drifting into a lamp on its own doesn't count)
            }
        }

        void Think(float dt)
        {
            var s = S;
            bool lit = InBeam(at, out var beamFrom, out var beamDir);
            Vector3 target = at;
            float speed = s.whiteMothSpeed;

            switch (state)
            {
                case State.Waiting:
                    // Hanging about where it is, until the light finds it (or it tires of waiting).
                    if (lit) { SetState(State.Following); led = true; break; }
                    target = at + new Vector3(Mathf.Sin(Time.time * 0.4f + seed), Mathf.Sin(Time.time * 0.7f + seed) * 0.3f, Mathf.Cos(Time.time * 0.33f + seed)) * 0.6f;
                    speed *= 0.3f;
                    if (stateTime > 60f) SetState(State.Lost);
                    break;

                case State.Following:
                    // Out along the beam, or into a lit lamp's light if one's close.
                    var near = NearLitLamp(at, 7f);
                    if (near != null) { lamp = near; target = near.LightPoint; }
                    else target = beamFrom + beamDir * s.whiteMothFollow;
                    if (!lit && near == null)
                    {
                        outOfLight += dt;
                        if (outOfLight > s.whiteMothLost) SetState(State.Lost);
                    }
                    else outOfLight = 0f;
                    break;

                case State.Lost:
                    // Drifting off slowly, turning a little; catch it again and it follows again.
                    if (lit && stateTime > 0.5f) { SetState(State.Following); led = true; break; }
                    wanderDir = Quaternion.Euler(0f, (Mathf.PerlinNoise(seed, Time.time * 0.2f) - 0.5f) * 40f * dt, 0f) * wanderDir;
                    target = at + wanderDir * 2f;
                    target.y = Mathf.Lerp(target.y, OnGround(at).y + 1.6f, 0.3f);
                    speed *= 0.3f;
                    if (stateTime > 30f || (at - player.transform.position).sqrMagnitude > 50f * 50f) { Remove(); return; }
                    break;

                case State.AtLamp:
                    // Circling the glass, then gone into the light.
                    if (lamp == null) { Remove(); return; }
                    float a = stateTime * 4f + seed;
                    target = lamp.LightPoint + new Vector3(Mathf.Cos(a) * 0.35f, Mathf.Sin(a * 1.7f) * 0.15f, Mathf.Sin(a) * 0.35f);
                    speed *= 1.5f;
                    if (stateTime > 6f)
                    {
                        float fade = Mathf.Clamp01(1f - (stateTime - 6f) / 1.5f);
                        moth.localScale = Vector3.one * fade;
                        if (fade <= 0f) { Remove(); return; }
                    }
                    break;
            }

            // Into a lit lamp's light while you were leading it: it's found what it wanted.
            if (state == State.Following || state == State.Lost)
            {
                var reached = NearLitLamp(at, 1.2f);
                if (reached != null)
                {
                    lamp = reached;
                    if (led) Reward();
                    SetState(State.AtLamp);
                }
            }

            // Fly toward the target, quick to turn, never faster than its speed.
            Vector3 want = target - at;
            Vector3 wantVel = want.magnitude > 0.05f ? want.normalized * Mathf.Min(speed, want.magnitude * 3f) : Vector3.zero;
            velocity = Vector3.MoveTowards(velocity, wantVel, dt * 8f);
            at += velocity * dt;
        }

        void Reward()
        {
            led = false;
            player.Stamina.Bonus += S.whiteMothBonus;
            player.Stamina.Refill();
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(Sound.Chirp, at, MixerNames.World, 0.5f, 1.6f, 15f);
            if (player.Hotbar != null) player.Hotbar.Say("The moth found the light. You feel lighter on your feet.", 3.5f);
            // A puff of its dust round the lamp.
            for (int i = 0; i < 24; i++) Emit(at + Random.insideUnitSphere * 0.3f);
        }

        void Remove()
        {
            if (moth != null) Destroy(moth.gameObject);
            moth = null;
            state = State.None;
            next = Time.time + Random.Range(Mathf.Min(S.minWhiteMoth, S.maxWhiteMoth), Mathf.Max(S.minWhiteMoth, S.maxWhiteMoth));
        }

        // ------------------------------------------------------------------ how it looks

        // Fluttering: jinking about its path, wings beating fast, facing the way it's going.
        void Flutter(float dt)
        {
            float t = Time.time;
            Vector3 jink = new Vector3(Mathf.PerlinNoise(seed, t * 3f) - 0.5f, Mathf.PerlinNoise(seed + 5f, t * 3.5f) - 0.5f, Mathf.PerlinNoise(seed + 9f, t * 3f) - 0.5f) * 0.3f;
            moth.position = at + jink;
            Vector3 heading = velocity + jink * 2f;
            if (heading.sqrMagnitude > 0.0004f) moth.rotation = Quaternion.Slerp(moth.rotation, Quaternion.LookRotation(heading), dt * 6f);
            phase += dt * 38f;
            float flap = 15f + Mathf.Sin(phase) * 55f;
            if (wingL != null) wingL.localRotation = Quaternion.Euler(0f, 0f, -flap);
            if (wingR != null) wingR.localRotation = Quaternion.Euler(0f, 0f, flap);
            if (halo != null) Effects.SetGlow(halo, new Color(0.85f, 0.9f, 1f) * (state == State.Following ? 0.5f : 0.35f) * moth.localScale.x);
        }

        void Trail(float dt)
        {
            dustTimer += dt;
            while (dustTimer > 0.035f)
            {
                dustTimer -= 0.035f;
                Emit(moth.position - moth.forward * 0.03f + Random.insideUnitSphere * 0.02f);
            }
            int n = 0;
            for (int i = 0; i < TrailMax; i++)
            {
                if (dustAge[i] >= DustLife) continue;
                dustAge[i] += dt;
                dust[i] += (Vector3.down * 0.12f + new Vector3(Mathf.Sin(dustAge[i] * 7f + i), 0f, Mathf.Cos(dustAge[i] * 6f + i)) * 0.03f) * dt;
                float size = 0.012f * (1f - dustAge[i] / DustLife);
                if (size <= 0f) continue;
                dustDraw[n++] = Matrix4x4.TRS(dust[i], Quaternion.identity, Vector3.one * size);
            }
            if (n > 0) InstancedDraw.Draw(speck, 0, dustMat, dustDraw, n, new Bounds(at, Vector3.one * 20f), Layers.Effects, false);
        }

        void Emit(Vector3 p)
        {
            dust[dustNext] = p;
            dustAge[dustNext] = 0f;
            dustNext = (dustNext + 1) % TrailMax;
        }

        // ------------------------------------------------------------------ bits

        // Is this inside your flashlight's beam (on, within its reach, inside its cone)? And where's the beam from.
        bool InBeam(Vector3 p, out Vector3 from, out Vector3 dir)
        {
            var light = player.Flashlight;
            from = player.transform.position + Vector3.up * 1.5f;
            dir = Camera.main != null ? Camera.main.transform.forward : player.transform.forward;
            if (light == null || light.Beam == null) return false;
            from = light.Beam.position;
            dir = light.Beam.forward;
            if (!light.IsOn) return false;
            Vector3 to = p - from;
            float d = to.magnitude;
            if (d > light.Settings.range * 1.2f || d < 0.01f) return false;
            return Vector3.Angle(dir, to) < light.Settings.beamAngle * 0.5f + 4f;
        }

        static Lamp NearLitLamp(Vector3 p, float within)
        {
            Lamp best = null;
            float bestD = within * within;
            foreach (var l in Lamp.All)
            {
                if (l == null || !l.Lit) continue;
                float d = (l.LightPoint - p).sqrMagnitude;
                if (d < bestD) { bestD = d; best = l; }
            }
            return best;
        }

        static Vector3 OnGround(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out var hit, 80f, Layers.Mask(Layers.Ground), QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
            return p;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z + (v.x == 0f && v.z == 0f ? 0.001f : 0f));
    }
}
