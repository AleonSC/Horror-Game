using System.Collections.Generic;
using Game.Audio;
using Game.Core;
using Game.Player;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.Entities
{
    public enum FlierState { Absent, Diving, Climbing, Patrolling, Locked, Away }

    /// <summary>
    /// The flier (section 6.10): a vast ragged black bird with one searchlight eye, coming at the 7th lamp. A cry,
    /// a long dive straight over you (low enough to shake the ground), a climb, then its eye lights. It circles
    /// stretches of forest high up, sweeping its beam. If the beam passes near you it snaps onto you, and holds
    /// until you've been out of its sight (trees, a roof, a safe light) for a few seconds. Standing still in it is
    /// safe; move in it too long and it screeches, the light turns red, and every scour comes at once. It never
    /// takes you itself.
    /// </summary>
    public sealed class Flier : MonoBehaviour, IThreat
    {
        FlierConfig config;
        FlierSettings s;
        PlayerController player;
        Transform root;
        FlierBody body;
        Light beam;
        readonly List<System.IDisposable> subs = new List<System.IDisposable>();

        Vector3 diveStart, diveDir, stretch, aim;
        float stateTime, sweep, grace, hidden, moved, stretchLeft, angry, circleAngle, timer;
        bool arrived, eyeLit;

        public FlierState State { get; private set; } = FlierState.Absent;
        public FlierSettings Settings => s;
        public float DistanceToPlayer => player != null ? Vector3.Distance(root.position, player.transform.position) : float.MaxValue;
        public bool ThreatVisible => false; // (overhead: never close in the usual sense)
        public bool ThreatTaking => false;
        public bool ThreatHunting => State == FlierState.Locked;
        public bool ThreatWatching => false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (!Services.TryGet<PlayerController>(out _) || !Services.TryGet<Game.World.Build.WorldBuilder>(out _)) return;
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponentInChildren<Flier>() == null)
                new GameObject("Flier").AddComponent<Flier>().transform.SetParent(root.transform, false);
        }

        void Awake()
        {
            config = Services.Get<GameSetup>().Get<FlierConfig>();
            if (config == null)
            {
                Debug.LogWarning("No FlierConfig in the GameSetup asset; using defaults.");
                config = ScriptableObject.CreateInstance<FlierConfig>();
            }
            s = config.settings;
            root = new GameObject("Flier (flying)").transform;
            root.SetParent(transform, false);
            body = FlierBody.Create(root, config);
            body.transform.localScale = Vector3.one * s.wingspan;
            var beamGo = new GameObject("Flier searchlight");
            beamGo.transform.SetParent(transform, false);
            beamGo.AddHDLight(LightType.Spot);
            beam = beamGo.GetComponent<Light>();
            beam.shadows = LightShadows.Soft;
            beam.enabled = false;
            root.gameObject.SetActive(false);
            Services.Register(this);
        }

        void Start()
        {
            if (!Services.TryGet<EventBus>(out var events)) return;
            subs.Add(events.Subscribe<LampLit>(e => { if (!arrived && e.TimesLit >= s.arriveAtLamp) Arrive(); }));
            subs.Add(events.Subscribe<RunReset>(_ => Remove()));
            subs.Add(events.Subscribe<PlayerRespawned>(_ => { if (arrived) GoAway(s.awayAfterCaught); }));
            subs.Add(events.Subscribe<HuntsReset>(_ => { if (State == FlierState.Locked) Release(s.graceAfter * 2f); }));
            subs.Add(events.Subscribe<FloodlightsLit>(e => DriveAway(e.Seconds + 30f)));
            Threats.Add(this);
        }

        void OnDestroy()
        {
            foreach (var sub in subs) sub.Dispose();
            Threats.Remove(this);
            Services.Unregister(this);
        }

        // ------------------------------------------------------------------ arriving and leaving

        /// <summary>The cry, and the dive (also the dev button).</summary>
        public void Arrive()
        {
            if (!Services.TryGet(out player)) return;
            arrived = true;
            // It comes from ahead of you, passing low right over you at 6.85 s (its theme's hit).
            Vector3 ahead = Vector3.ProjectOnPlane(player.Head.forward, Vector3.up).normalized;
            if (ahead.sqrMagnitude < 0.01f) ahead = Vector3.forward;
            diveDir = -ahead;
            diveStart = player.transform.position - diveDir * s.diveSpeed * 6.85f + Vector3.up * (s.diveHeight + 30f);
            root.position = diveStart;
            root.rotation = Quaternion.LookRotation(diveDir);
            root.gameObject.SetActive(true);
            eyeLit = false;
            beam.enabled = false;
            SetState(FlierState.Diving);
            Play2D(Sound.Shriek, 1f, 0.3f);
            if (Services.TryGet<EventBus>(out var events))
            {
                events.Publish(new BossThemeStarted(EntityKind.Flier));
                events.Publish(new EntitySpawned(EntityKind.Flier, 1, diveStart));
            }
        }

        void GoAway(float seconds)
        {
            root.gameObject.SetActive(false);
            beam.enabled = false;
            SetState(FlierState.Away);
            timer = seconds;
        }

        void Remove()
        {
            if (arrived && Services.TryGet<EventBus>(out var events)) events.Publish(new BossThemeEnded(EntityKind.Flier));
            arrived = false;
            root.gameObject.SetActive(false);
            beam.enabled = false;
            SetState(FlierState.Absent);
        }

        /// <summary>Driven away (the watchtower's floodlights).</summary>
        public void DriveAway(float seconds)
        {
            if (State == FlierState.Absent || State == FlierState.Away) return;
            Play2D(Sound.Shriek, 0.8f, 0.35f);
            GoAway(seconds);
        }

        // ------------------------------------------------------------------ every frame

        void Update()
        {
            if (player == null && !Services.TryGet(out player)) return;
            bool away = PlayerPlace.InDarkRoom;
            if (root.gameObject.activeSelf == away && (State == FlierState.Patrolling || State == FlierState.Locked || State == FlierState.Climbing))
                root.gameObject.SetActive(!away);
            if (away)
            {
                beam.enabled = false;
                if (State == FlierState.Locked) Release(s.graceAfter);
                return;
            }
            float dt = Time.deltaTime;
            stateTime += dt;
            grace = Mathf.Max(0f, grace - dt);
            angry = Mathf.MoveTowards(angry, 0f, dt / 10f);
            switch (State)
            {
                case FlierState.Absent: return;
                case FlierState.Away:
                    timer -= dt;
                    if (timer <= 0f) Return();
                    return;
                case FlierState.Diving: Dive(dt); break;
                case FlierState.Climbing: Climb(dt); break;
                case FlierState.Patrolling: Patrol(dt); break;
                case FlierState.Locked: Locked(dt); break;
            }
            UpdateBeam();
            UpdateLook();
        }

        // Straight in, low over you (the ground shakes as it passes), then on.
        void Dive(float dt)
        {
            float t = stateTime;
            Vector3 p = player.transform.position;
            Vector3 along = diveStart + diveDir * s.diveSpeed * t;
            float over = (t - 6.85f) / 2.5f;
            float height = s.diveHeight + 30f * Mathf.Clamp01(over * over);
            along.y = p.y + height;
            root.position = along;
            root.rotation = Quaternion.LookRotation(diveDir + Vector3.down * Mathf.Clamp(-over, -0.3f, 0.3f) * 0.4f);
            body.Pose(t > 6.85f ? 0.9f : 0.15f, 0f, dt);
            if (t >= 6.85f && t - dt < 6.85f)
            {
                player.AddShake(3.5f);
                Play2D(Sound.FlareBurst, 1f, 0.6f); // (the rush of air as it passes)
            }
            if (t >= 8.4f) SetState(FlierState.Climbing);
        }

        // Up to its height, wheeling round; its eye lights at 12 s from the start (its theme's bell).
        void Climb(float dt)
        {
            Vector3 p = player.transform.position;
            Vector3 fwd = root.forward;
            fwd = Quaternion.Euler(0f, 35f * dt, 0f) * fwd;
            Vector3 pos = root.position + fwd * s.speed * 1.8f * dt;
            pos.y = Mathf.MoveTowards(pos.y, p.y + s.altitude, 12f * dt);
            root.SetPositionAndRotation(pos, Quaternion.LookRotation(new Vector3(fwd.x, 0.25f, fwd.z)));
            body.Pose(1f, 0.5f, dt);
            if (stateTime >= 12f - 8.4f)
            {
                eyeLit = true;
                grace = s.graceAtStart;
                PickStretch();
                aim = stretch;
                SetState(FlierState.Patrolling);
            }
        }

        void Return()
        {
            Vector3 p = player.transform.position;
            Vector2 r = Random.insideUnitCircle.normalized * s.patrolRange * 1.5f;
            root.position = new Vector3(p.x + r.x, p.y + s.altitude, p.z + r.y);
            root.gameObject.SetActive(true);
            eyeLit = true;
            grace = s.graceAfter;
            PickStretch();
            SetState(FlierState.Patrolling);
        }

        // Circling a stretch of forest, sweeping the beam over it. If the beam crosses near you, it has you.
        void Patrol(float dt)
        {
            stretchLeft -= dt;
            if (stretchLeft <= 0f) PickStretch();
            Circle(stretch, dt);
            sweep += dt * 0.7f;
            Vector3 swept = stretch + new Vector3(Mathf.Sin(sweep * 1.3f), 0f, Mathf.Cos(sweep * 0.9f)) * s.circleRadius * 0.9f;
            if (grace > 0f)
            {
                // At first it keeps its beam off you.
                Vector3 away = swept - player.transform.position;
                away.y = 0f;
                if (away.magnitude < s.lockDistance * 2f) swept = player.transform.position + (away.sqrMagnitude > 0.01f ? away.normalized : Vector3.right) * s.lockDistance * 2f;
            }
            aim = Vector3.MoveTowards(aim, swept, 14f * dt);
            aim.y = PlayerSense.GroundHeight(aim);

            Vector3 toPlayer = player.transform.position - aim;
            toPlayer.y = 0f;
            if (grace <= 0f && toPlayer.magnitude <= s.lockDistance && CanSee())
            {
                hidden = 0f;
                SetState(FlierState.Locked);
                Play2D(Sound.Shriek, 0.6f, 0.5f);
            }
        }

        // It has you: circling over you, the beam on you. Hide for a few seconds to lose it; keep moving and it screeches.
        void Locked(float dt)
        {
            Vector3 p = player.transform.position;
            Circle(p, dt);
            aim = Vector3.Lerp(aim, p, 1f - Mathf.Exp(-6f * dt));
            if (CanSee())
            {
                hidden = 0f;
                if (player.Moving) moved += dt;
                if (moved >= s.moveToScreech)
                {
                    moved = 0f;
                    Screech();
                }
            }
            else
            {
                hidden += dt;
                if (hidden >= s.hideToLose) Release(s.graceAfter);
            }
        }

        void Screech()
        {
            angry = 1f;
            Play2D(Sound.Shriek, 1f, 0.55f);
            player.AddShake(1f);
            if (Services.TryGet<ScourSpawner>(out var scours)) scours.AllComeNow();
            if (Services.TryGet<EventBus>(out var events)) events.Publish(new NoiseEmitted(player.transform.position, 120f, "flier screech"));
            Release(s.graceAfter);
        }

        void Release(float graceSeconds)
        {
            grace = graceSeconds;
            PickStretch();
            SetState(FlierState.Patrolling);
        }

        // Out of its sight: something between its eye and you (trees, a roof), or a safe light round you.
        bool CanSee()
        {
            if (Services.TryGet<SafeZones>(out var zones) && zones.Contains(player.transform.position)) return false;
            Vector3 eye = body.Eye != null ? body.Eye.position : root.position;
            Vector3 head = player.Head.position;
            Vector3 d = head - eye;
            int mask = Layers.Mask(Layers.Trees) | Layers.Mask(Layers.Props) | Layers.Mask(Layers.Ground);
            return !Physics.Raycast(eye, d.normalized, d.magnitude - 0.5f, mask, QueryTriggerInteraction.Ignore);
        }

        void PickStretch()
        {
            stretchLeft = s.stretchTime * Random.Range(0.7f, 1.3f);
            Vector2 r = Random.insideUnitCircle * s.patrolRange;
            Vector3 p = player.transform.position;
            stretch = new Vector3(p.x + r.x, p.y, p.z + r.y);
        }

        // Wheel round a centre at its height, banking into the turn.
        void Circle(Vector3 centre, float dt)
        {
            circleAngle += s.speed / Mathf.Max(5f, s.circleRadius) * dt;
            Vector3 want = centre + new Vector3(Mathf.Cos(circleAngle), 0f, Mathf.Sin(circleAngle)) * s.circleRadius;
            want.y = player.transform.position.y + s.altitude;
            Vector3 to = want - root.position;
            Vector3 vel = Vector3.ClampMagnitude(to, s.speed * 1.5f * dt);
            Vector3 pos = root.position + vel;
            Vector3 heading = new Vector3(-Mathf.Sin(circleAngle), 0f, Mathf.Cos(circleAngle));
            if (to.magnitude > s.circleRadius) heading = Vector3.ProjectOnPlane(to, Vector3.up).normalized;
            root.SetPositionAndRotation(pos, Quaternion.Slerp(root.rotation, Quaternion.LookRotation(heading), 1f - Mathf.Exp(-2f * dt)));
            body.Pose(0.15f + 0.1f * Mathf.Sin(Time.time * 0.3f), 0.6f, dt);
        }

        void UpdateBeam()
        {
            beam.enabled = eyeLit && root.gameObject.activeSelf;
            if (!beam.enabled) return;
            Vector3 eye = body.Eye != null ? body.Eye.position : root.position;
            beam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(aim - eye));
            float dist = Vector3.Distance(eye, aim);
            beam.spotAngle = s.beamHalfAngle * 2f;
            beam.innerSpotAngle = s.beamHalfAngle * 1.6f;
            beam.range = dist * 1.6f + 20f;
            beam.color = Color.Lerp(s.beamColor, s.angryColor, angry);
            beam.intensity = s.beamIntensity;
        }

        // A silhouette that darkens as it nears (the haze keeps the fog from hiding it).
        void UpdateLook()
        {
            float far = Mathf.Clamp01(DistanceToPlayer / 200f);
            body.SetShade(Mathf.Lerp(0.05f, 0.6f, far));
        }

        void SetState(FlierState next)
        {
            State = next;
            stateTime = 0f;
            if (Services.TryGet<EventBus>(out var events)) events.Publish(new EntityStateChanged(EntityKind.Flier, 1, next.ToString()));
        }

        void Play2D(Sound sound, float volume, float pitch)
        {
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play2D(sound, MixerNames.Entities, volume, pitch);
        }
    }
}
