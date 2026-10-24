using System.Collections.Generic;
using Game.Audio;
using Game.Core;
using Game.Player;
using Game.World.Build;
using UnityEngine;

namespace Game.Entities
{
    public enum BlindState { Absent, Arriving, Roaming, Listening, Rushing, Searching, Taking, Away }

    /// <summary>
    /// The blind one, "the Auscult" (section 6.8). It comes at the 3rd lamp, breaking out of the ground 45 m away
    /// under its storm, with its theme. It roams near you, stopping to cock its head and click. It hunts by sound:
    /// a noise it hears (running carries far, walking a little, crouching barely; very close it hears anything)
    /// sends it rushing to the spot, where it searches. Close enough, it takes you (it leans its face down to
    /// yours; a hard cut). Lights stop it. It shoulders through trees and knocks them down; rocks and buildings
    /// it steps round. Get far away for long and it catches up.
    /// </summary>
    public sealed class BlindOne : MonoBehaviour, IThreat
    {
        BlindConfig config;
        BlindSettings s;
        PlayerController player;
        BlindBody body;
        Storm storm;
        readonly List<System.IDisposable> subs = new List<System.IDisposable>();
        readonly Collider[] nearby = new Collider[16];

        Vector3 target, searchSpot;
        float timer, stateTime, nextListen, lostFor, deafFor, speed, roar, lean, listen;
        float lastStep;
        bool arrived;

        public BlindState State { get; private set; } = BlindState.Absent;
        public BlindSettings Settings => s;
        public float DistanceToPlayer => player != null ? PlayerSense.FlatDistance(transform.position, player.transform.position) : float.MaxValue;

        public bool ThreatVisible => State != BlindState.Absent && State != BlindState.Away;
        public bool ThreatTaking => State == BlindState.Taking;
        public bool ThreatHunting => State == BlindState.Rushing || State == BlindState.Taking;
        public bool ThreatWatching => false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (!Services.TryGet<PlayerController>(out _) || !Services.TryGet<WorldBuilder>(out _)) return;
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponentInChildren<BlindOne>() == null)
                new GameObject("Blind one").AddComponent<BlindOne>().transform.SetParent(root.transform, false);
        }

        void Awake()
        {
            config = Services.Get<GameSetup>().Get<BlindConfig>();
            if (config == null)
            {
                Debug.LogWarning("No BlindConfig in the GameSetup asset; using defaults.");
                config = ScriptableObject.CreateInstance<BlindConfig>();
            }
            s = config.settings;
            body = BlindBody.Create(transform, config);
            body.transform.localScale = Vector3.one * s.height;
            storm = Storm.Create(s);
            body.gameObject.SetActive(false);
            Services.Register(this);
        }

        void Start()
        {
            storm.transform.SetParent(transform.parent, true);
            if (Services.TryGet<EventBus>(out var events))
            {
                subs.Add(events.Subscribe<LampLit>(e => { if (!arrived && e.TimesLit >= s.arriveAtLamp) Arrive(); }));
                subs.Add(events.Subscribe<NoiseEmitted>(OnNoise));
                subs.Add(events.Subscribe<RunReset>(_ => Remove()));
                subs.Add(events.Subscribe<PlayerRespawned>(_ => { if (arrived) GoAway(45f); }));
                subs.Add(events.Subscribe<HuntsReset>(_ => LoseInterest(15f)));
                subs.Add(events.Subscribe<FlashBurst>(OnFlash));
            }
            Threats.Add(this);
        }

        void OnDestroy()
        {
            foreach (var sub in subs) sub.Dispose();
            Threats.Remove(this);
            Services.Unregister(this);
            if (storm != null) Destroy(storm.gameObject);
        }

        // ------------------------------------------------------------------ arriving and leaving

        /// <summary>It breaks out of the ground 45 m from you, ahead, under its storm (also the dev button).</summary>
        public void Arrive()
        {
            if (!Services.TryGet(out player)) return;
            arrived = true;
            Vector3 ahead = Vector3.ProjectOnPlane(player.Head.forward, Vector3.up).normalized;
            if (ahead.sqrMagnitude < 0.01f) ahead = player.transform.forward;
            Vector3 dir = Quaternion.Euler(0f, Random.Range(-40f, 40f), 0f) * ahead;
            Vector3 at = player.transform.position + dir * s.arriveDistance;
            at.y = PlayerSense.GroundHeight(at);
            transform.position = at - Vector3.up * s.height; // (under the ground: it breaks out)
            Face(player.transform.position);
            storm.transform.position = at + Vector3.up * s.stormHeight;
            body.gameObject.SetActive(true);
            SetState(BlindState.Arriving);
            if (Services.TryGet<EventBus>(out var events))
            {
                events.Publish(new BossThemeStarted(EntityKind.Blind));
                events.Publish(new EntitySpawned(EntityKind.Blind, 1, at));
            }
        }

        void GoAway(float seconds)
        {
            if (State == BlindState.Away)
            {
                timer = Mathf.Max(timer, seconds);
                return;
            }
            // (After taking you it doesn't let go: waking up does that.)
            body.gameObject.SetActive(false);
            SetState(BlindState.Away);
            timer = seconds;
        }

        void Remove()
        {
            if (arrived && Services.TryGet<EventBus>(out var events)) events.Publish(new BossThemeEnded(EntityKind.Blind));
            arrived = false;
            body.gameObject.SetActive(false);
            storm.Strength = 0f;
            SetState(BlindState.Absent);
        }

        void LoseInterest(float deafSeconds)
        {
            if (State == BlindState.Absent || State == BlindState.Away || State == BlindState.Arriving || State == BlindState.Taking) return;
            deafFor = deafSeconds;
            SetState(BlindState.Roaming);
            target = RoamPoint();
        }

        // A camera flash in its face drives it off for a while, like the scours.
        void OnFlash(FlashBurst e)
        {
            if (!ThreatVisible || State == BlindState.Taking || State == BlindState.Arriving) return;
            Vector3 head = transform.position + Vector3.up * s.height * 0.85f;
            Vector3 to = head - e.Position;
            if (to.magnitude > e.Range || Vector3.Angle(e.Direction, to) > e.HalfAngle || PlayerSense.Occluded(e.Position, head)) return;
            Play(Sound.Shriek, 1f, 0.45f, 80f);
            GoAway(e.Seconds);
        }

        // ------------------------------------------------------------------ hearing

        void OnNoise(NoiseEmitted e)
        {
            if (deafFor > 0f || e.Source == "tree falling") return;
            if (State != BlindState.Roaming && State != BlindState.Listening && State != BlindState.Searching && State != BlindState.Rushing) return;
            float heard = e.Radius * Difficulty.Now.blindHearing * (State == BlindState.Listening ? 1.3f : 1f);
            if (Vector3.Distance(transform.position, e.Position) > heard) return;
            RushTo(e.Position);
        }

        void RushTo(Vector3 spot)
        {
            if (State != BlindState.Rushing) Play(Sound.Moan, 0.9f, 0.5f, 60f);
            target = spot;
            SetState(BlindState.Rushing);
        }

        // ------------------------------------------------------------------ every frame

        void Update()
        {
            if (player == null && !Services.TryGet(out player)) return;
            if (PlayerPlace.InDarkRoom) return; // (it can't follow you there: it waits)
            float dt = Time.deltaTime;
            deafFor = Mathf.Max(0f, deafFor - dt);
            stateTime += dt;
            storm.Strength = Mathf.MoveTowards(storm.Strength, ThreatVisible ? 1f : 0f, dt * (State == BlindState.Arriving ? 0.3f : 0.15f));
            if (storm.Strength > 0f) storm.Follow(transform.position, dt);

            switch (State)
            {
                case BlindState.Absent: return;
                case BlindState.Away:
                    timer -= dt;
                    if (timer <= 0f) Reappear();
                    return;
                case BlindState.Arriving: UpdateArriving(dt); break;
                case BlindState.Roaming: UpdateRoaming(dt); break;
                case BlindState.Listening: UpdateListening(dt); break;
                case BlindState.Rushing: UpdateRushing(dt); break;
                case BlindState.Searching: UpdateSearching(dt); break;
                case BlindState.Taking: UpdateTaking(dt); return;
            }

            CheckTake();
            KeepUp(dt);
            body.Pose(speed, s.height, listen, roar, lean, dt);
        }

        // Timed to its theme: the storm gathers, it bursts out of the ground at 3.5 s, roars at 7.6 s, stamps twice at 13.1 s.
        void UpdateArriving(float dt)
        {
            float t = stateTime;
            Vector3 p = transform.position;
            float ground = PlayerSense.GroundHeight(p);
            if (t >= 3.5f)
            {
                float rise = Mathf.Clamp01((t - 3.5f) / 0.7f);
                p.y = ground - s.height * (1f - Mathf.SmoothStep(0f, 1f, rise));
                if (t - dt < 3.5f)
                {
                    player.AddShake(1.5f);
                    Play(Sound.Boom, 0.8f, 1.6f, 120f);
                }
            }
            transform.position = p;
            roar = t > 7.6f && t < 10f ? Mathf.Sin((t - 7.6f) / 2.4f * Mathf.PI) : 0f;
            if (t >= 7.6f && t - dt < 7.6f)
            {
                Play(Sound.Shriek, 1f, 0.35f, 200f);
                player.AddShake(1f);
            }
            if ((t >= 13.1f && t - dt < 13.1f) || (t >= 13.55f && t - dt < 13.55f))
            {
                Play(Sound.GiantStep, 1f, 1.3f, 120f);
                player.AddShake(1.2f);
            }
            speed = 0f;
            Face(player.transform.position);
            if (t >= 14.5f)
            {
                SetState(BlindState.Roaming);
                target = RoamPoint();
            }
        }

        void UpdateRoaming(float dt)
        {
            roar = Mathf.MoveTowards(roar, 0f, dt);
            listen = Mathf.MoveTowards(listen, 0f, dt * 2f);
            MoveToward(target, s.roamSpeed, dt);
            if (Flat(target - transform.position).magnitude < 2f) target = RoamPoint();
            nextListen -= dt;
            if (nextListen <= 0f)
            {
                nextListen = Random.Range(s.minListenEvery, s.maxListenEvery);
                SetState(BlindState.Listening);
            }
            HearClose();
        }

        // It stops, cocks its head and clicks.
        void UpdateListening(float dt)
        {
            speed = 0f;
            listen = Mathf.MoveTowards(listen, 1f, dt * 3f);
            if (Time.time - lastStep > 0.45f)
            {
                lastStep = Time.time;
                Play(Sound.Click, 0.8f, Random.Range(0.35f, 0.5f), 30f);
            }
            HearClose();
            if (stateTime >= s.listenTime) SetState(BlindState.Roaming);
        }

        void UpdateRushing(float dt)
        {
            listen = Mathf.MoveTowards(listen, 0f, dt * 4f);
            MoveToward(target, s.rushSpeed, dt);
            HearClose();
            if (Flat(target - transform.position).magnitude < 1.5f)
            {
                searchSpot = target;
                SetState(BlindState.Searching);
            }
        }

        // At the spot it stands and listens, then casts about round it, slowly.
        void UpdateSearching(float dt)
        {
            if (stateTime < s.searchSpotTime)
            {
                speed = 0f;
                listen = Mathf.MoveTowards(listen, 1f, dt * 3f);
            }
            else
            {
                listen = Mathf.MoveTowards(listen, 0f, dt * 2f);
                MoveToward(target, s.roamSpeed, dt);
                if (Flat(target - transform.position).magnitude < 1.5f)
                {
                    var off = Random.insideUnitCircle * s.searchAreaRadius;
                    target = searchSpot + new Vector3(off.x, 0f, off.y);
                }
            }
            HearClose();
            if (stateTime >= s.searchSpotTime + s.searchAreaTime)
            {
                SetState(BlindState.Roaming);
                target = RoamPoint();
            }
        }

        // Very close, it hears you whatever you do, unless you're creeping.
        void HearClose()
        {
            if (deafFor > 0f || player.Crouching || !player.Moving) return;
            if (DistanceToPlayer <= s.closeHearing * Difficulty.Now.blindHearing) RushTo(player.transform.position);
        }

        void CheckTake()
        {
            if (DistanceToPlayer > s.takeDistance) return;
            var flow = player.GetComponent<CatchFlow>();
            if (flow == null || flow.Active) return;
            if (Services.TryGet<SafeZones>(out var zones) && zones.Contains(player.transform.position)) return;
            StartTaking();
        }

        // Get far from it for long and it catches up, somewhere behind you.
        void KeepUp(float dt)
        {
            lostFor = DistanceToPlayer > s.lostDistance ? lostFor + dt : 0f;
            if (lostFor < s.lostTime) return;
            lostFor = 0f;
            Vector3 behind = -Vector3.ProjectOnPlane(player.Head.forward, Vector3.up).normalized;
            Vector3 at = player.transform.position + Quaternion.Euler(0f, Random.Range(-50f, 50f), 0f) * behind * s.catchUpTo;
            at.y = PlayerSense.GroundHeight(at);
            transform.position = at;
            storm.transform.position = at + Vector3.up * s.stormHeight;
            SetState(BlindState.Roaming);
            target = RoamPoint();
        }

        void Reappear()
        {
            Vector3 dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            Vector3 at = player.transform.position + dir * s.arriveDistance;
            at.y = PlayerSense.GroundHeight(at);
            transform.position = at;
            storm.transform.position = at + Vector3.up * s.stormHeight;
            body.gameObject.SetActive(true);
            SetState(BlindState.Roaming);
            target = RoamPoint();
        }

        // ------------------------------------------------------------------ taking you

        Vector3 takeFrom;

        void StartTaking()
        {
            player.SetHeld(true);
            takeFrom = transform.position;
            body.Trigger("Take");
            Play(Sound.Moan, 1f, 0.4f, 20f);
            SetState(BlindState.Taking);
        }

        // It leans its face right down to yours... then black.
        void UpdateTaking(float dt)
        {
            const float leanTime = 1.6f;
            lean = Mathf.Clamp01(stateTime / leanTime);
            speed = 0f;
            Vector3 toPlayer = Flat(player.transform.position - transform.position);
            if (toPlayer.magnitude > 1.2f) transform.position += toPlayer.normalized * Mathf.Min(toPlayer.magnitude - 1.2f, 2f * dt);
            Face(player.transform.position);
            body.Pose(0f, s.height, 0f, 0f, lean, dt);
            var cam = player.Head;
            Vector3 face = transform.TransformPoint(body.transform.localPosition + Vector3.Scale(body.HeadLocal, body.transform.localScale)) - Vector3.up * s.height * 0.25f * lean;
            cam.rotation = Quaternion.Slerp(cam.rotation, Quaternion.LookRotation(face - cam.position), 1f - Mathf.Exp(-4f * dt));
            player.AddShake(0.6f * lean);
            if (stateTime >= leanTime)
            {
                var flow = player.GetComponent<CatchFlow>();
                if (flow != null) flow.Begin(EntityKind.Blind, 1, hardCut: true);
                GoAway(s.awayAfterTaking);
            }
        }

        // ------------------------------------------------------------------ moving

        void MoveToward(Vector3 goal, float maxSpeed, float dt)
        {
            Vector3 pos = transform.position;
            Vector3 to = Flat(goal - pos);
            float dist = to.magnitude;
            if (dist < 0.05f) { speed = 0f; return; }
            Vector3 dir = to / dist;

            // Lights stop it: it won't step into a safe circle.
            if (Services.TryGet<SafeZones>(out var zones) && zones.Contains(pos + dir * 1.5f, 1f)) { speed = 0f; return; }

            // Rocks, boulders and buildings it steps round (trees it goes through).
            int props = Layers.Mask(Layers.Props);
            if (Physics.SphereCast(pos + Vector3.up * 1.5f, 1f, dir, out var hit, 3f, props, QueryTriggerInteraction.Ignore))
            {
                Vector3 side = Vector3.Cross(Vector3.up, dir);
                if (Vector3.Dot(side, hit.point - pos) > 0f) side = -side;
                dir = (dir * 0.3f + side).normalized;
            }

            speed = Mathf.MoveTowards(speed, maxSpeed, dt * 4f);
            Vector3 next = pos + dir * Mathf.Min(dist, speed * dt);
            next.y = Mathf.Lerp(pos.y, PlayerSense.GroundHeight(next), 1f - Mathf.Exp(-10f * dt));
            transform.position = next;
            Face(next + dir);
            if (s.knocksTrees) Shoulder(dir);
            Steps();
        }

        // It barges through the forest: trees in its way go over.
        void Shoulder(Vector3 dir)
        {
            if (!Services.TryGet<WorldBuilder>(out var world)) return;
            int n = Physics.OverlapSphereNonAlloc(transform.position + dir * 1.2f + Vector3.up * 2f, 1.3f, nearby, Layers.Mask(Layers.Trees), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var tree = nearby[i].GetComponent<ForestTree>();
                if (tree != null) tree.KnockDown(world.Forest, transform.position);
            }
        }

        void Steps()
        {
            float stepEvery = Mathf.Lerp(1.4f, 0.55f, Mathf.InverseLerp(s.roamSpeed, s.rushSpeed, speed));
            if (speed < 0.2f || Time.time - lastStep < stepEvery) return;
            lastStep = Time.time;
            Play(Sound.GiantStep, Mathf.Lerp(0.35f, 0.8f, speed / s.rushSpeed), Random.Range(1.6f, 1.9f), 60f);
            float near = 1f - Mathf.Clamp01(DistanceToPlayer / 25f);
            if (near > 0f) player.AddShake(0.6f * near);
        }

        Vector3 RoamPoint()
        {
            var off = Random.insideUnitCircle * s.roamRadius;
            var p = player.transform.position + new Vector3(off.x, 0f, off.y);
            p.y = PlayerSense.GroundHeight(p);
            return p;
        }

        void Face(Vector3 point)
        {
            Vector3 d = Flat(point - transform.position);
            if (d.sqrMagnitude > 0.001f) transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(d), 1f - Mathf.Exp(-5f * Time.deltaTime));
        }

        void SetState(BlindState next)
        {
            State = next;
            stateTime = 0f;
            if (Services.TryGet<EventBus>(out var events)) events.Publish(new EntityStateChanged(EntityKind.Blind, 1, next.ToString()));
        }

        void Play(Sound sound, float volume, float pitch, float range)
        {
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(sound, transform.position + Vector3.up * s.height * 0.8f, MixerNames.Entities, volume, pitch, range);
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);
    }
}
