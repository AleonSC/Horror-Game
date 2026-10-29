using System.Collections.Generic;
using Game.Audio;
using Game.Core;
using Game.Player;
using Game.Rendering;
using Game.World.Build;
using UnityEngine;

namespace Game.Entities
{
    public enum BlindState { Absent, Arriving, Roaming, Listening, Rushing, Searching, Taking, Away, Teleporting }

    /// <summary>
    /// The blind one, "the Auscult" (section 6.8). It comes at the 3rd lamp, breaking out of the ground 45 m away
    /// under its storm, with its theme. It roams near you, stopping to cock its head and click. It hunts by sound:
    /// a noise it hears (running carries far, walking a little, crouching barely; very close it hears anything)
    /// sends it rushing to the spot, where it searches. Close enough, it takes you (it leans its face down to
    /// yours; a hard cut). Lights stop it. It tramples through trees, which burst into splinters; rocks and buildings
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
        // Where you've been (a point every 2 s, the last couple of minutes): it follows the trail.
        readonly Queue<Vector3> trail = new Queue<Vector3>();
        float nextTrail, nextHardListen = -1f;
        bool hardListening;
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
            heat = Heat.Add(body.transform, s.height, s.height * 0.12f);
        }

        Heat.Source heat;

        void OnDestroy()
        {
            foreach (var sub in subs) sub.Dispose();
            Threats.Remove(this);
            Heat.Remove(heat);
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
            // Your eyes are dragged to where it breaks out, and held on it through its roar.
            player.LockGaze(() => transform.position + Vector3.up * s.height * Mathf.Lerp(0.15f, 0.75f, Mathf.Clamp01((stateTime - 3.5f) / 1.2f)), 10.2f);
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
            Play(Sound.BlindRoar, 1f, Random.Range(1f, 1.1f), 80f);
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
            // Coming for you: its music, whatever's come since it arrived.
            if (State != BlindState.Rushing && Services.TryGet<EventBus>(out var bus)) bus.Publish(new BossThemeFocus(EntityKind.Blind));
            if (State != BlindState.Rushing) Play(Sound.BlindGrowl, 0.9f, Random.Range(0.92f, 1.05f), 60f);
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

            // A radio playing within reach: it smashes it.
            if (State != BlindState.Absent && State != BlindState.Away && State != BlindState.Taking)
                for (int i = PlacedRadio.Playing.Count - 1; i >= 0; i--)
                {
                    var radio = PlacedRadio.Playing[i];
                    if (PlayerSense.FlatDistance(transform.position, radio.transform.position) <= radio.SmashDistance) radio.Smash();
                }

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
                case BlindState.Teleporting: UpdateTeleporting(dt); return;
                case BlindState.Taking: UpdateTaking(dt); return;
            }

            if (Time.time >= nextTrail)
            {
                nextTrail = Time.time + 2f;
                trail.Enqueue(player.transform.position);
                while (trail.Count > 60) trail.Dequeue();
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
                Play(Sound.BlindRoar, 1f, Random.Range(0.85f, 0.93f), 200f);
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

        // Roaming: on your trail, where you were a little while ago, at a steady walk (quicker as the night goes on).
        // (Teleporting, instead: it stands where it is, listening, and in a while goes in a strike of lightning.)
        void UpdateRoaming(float dt)
        {
            if (s.teleports)
            {
                Perch(dt);
                return;
            }
            roar = Mathf.MoveTowards(roar, 0f, dt);
            listen = Mathf.MoveTowards(listen, 0f, dt * 2f);
            target = TrailPoint();
            int lamps = Services.TryGet<GameRun>(out var run) ? Mathf.Max(0, run.LampsLit - s.arriveAtLamp) : 0;
            MoveToward(target, s.trailSpeed * (1f + s.trailSpeedPerLamp * lamps), dt);
            nextListen -= dt;
            if (nextHardListen < 0f) nextHardListen = Time.time + Random.Range(s.minHardListen, s.maxHardListen);
            if (Time.time >= nextHardListen)
            {
                // Stops dead and listens hard: anything moving within reach, it hears.
                nextHardListen = Time.time + Random.Range(s.minHardListen, s.maxHardListen);
                hardListening = true;
                Play(Sound.Click, 1f, 0.3f, 60f);
                SetState(BlindState.Listening);
            }
            else if (nextListen <= 0f)
            {
                nextListen = Random.Range(s.minListenEvery, s.maxListenEvery);
                hardListening = false;
                SetState(BlindState.Listening);
            }
            HearClose();
        }

        // ------------------------------------------------------------------ teleporting

        float perchLeft = -1f, goneFor;
        Vector3 comeTo;
        bool struckOut, struckIn;
        StrikeScorch scorch;

        // Standing where it came in, still, head cocked, listening (it hears you as ever: and runs at you), and in
        // a while it goes.
        void Perch(float dt)
        {
            speed = 0f;
            roar = Mathf.MoveTowards(roar, 0f, dt);
            lean = Mathf.MoveTowards(lean, 0f, dt * 0.8f); // (rising from its crouch, after coming in)
            listen = Mathf.MoveTowards(listen, 0.7f + 0.3f * Mathf.Sin(Time.time * 0.7f), dt * 1.5f);
            Face(player.transform.position);
            if (perchLeft < 0f) perchLeft = Random.Range(s.perchMin, Mathf.Max(s.perchMin, s.perchMax));
            perchLeft -= dt;
            // Now and then a click, listening.
            if (Time.time - lastStep > 1.4f)
            {
                lastStep = Time.time;
                Play(Sound.Click, 0.7f, Random.Range(0.32f, 0.45f), 35f);
            }
            if (nextHardListen < 0f) nextHardListen = Time.time + Random.Range(s.minHardListen, s.maxHardListen);
            if (Time.time >= nextHardListen)
            {
                nextHardListen = Time.time + Random.Range(s.minHardListen, s.maxHardListen);
                hardListening = true;
                Play(Sound.Click, 1f, 0.3f, 60f);
                SetState(BlindState.Listening);
                return;
            }
            HearClose();
            if (State == BlindState.Roaming && perchLeft <= 0f) StartTeleport();
        }

        void StartTeleport()
        {
            perchLeft = -1f;
            struckOut = struckIn = false;
            goneFor = Random.Range(s.goneMin, Mathf.Max(s.goneMin, s.goneMax));
            comeTo = TeleportSpot();
            SetState(BlindState.Teleporting);
        }

        // Going: a fit of convulsing (half a second), then red lightning strikes it, a blinding flash, and it's
        // gone, a red glow where it stood fading. Gone a few seconds, its storm drifting overhead to where it'll
        // be. Coming: a strike there, and it's there, hunched low in the glare, rising, with a low roar and a thud
        // you feel; then it stands and listens.
        void UpdateTeleporting(float dt)
        {
            float t = stateTime;
            const float fit = 0.55f;
            if (!struckOut)
            {
                // The fit: twitching, head jerking, arms up.
                speed = 0f;
                roar = Mathf.Clamp01(t / fit) * 0.6f;
                listen = Random.value;
                body.transform.localPosition = Random.insideUnitSphere * 0.12f * (t / fit);
                if (t - dt <= 0f) Play(Sound.BlindRoar, 0.7f, 0.8f, 60f);
                if (t >= fit)
                {
                    struckOut = true;
                    body.transform.localPosition = Vector3.zero;
                    storm.StrikeAt(transform.position);
                    Afterglow(transform.position);
                    body.gameObject.SetActive(false);
                    float near = 1f - Mathf.Clamp01(DistanceToPlayer / 60f);
                    player.AddShake(0.4f + 1.2f * near);
                    // Its storm moves over to where it'll come in (it follows this object).
                    transform.position = comeTo;
                }
                body.Pose(0f, s.height, listen, roar, 0f, dt);
                return;
            }
            if (!struckIn)
            {
                if (t < fit + goneFor) return;
                // Coming in: the strike, and it's there, low, in the glare.
                struckIn = true;
                if (Services.TryGet<EventBus>(out var focusBus)) focusBus.Publish(new BossThemeFocus(EntityKind.Blind)); // (it's here: its music)
                transform.position = comeTo;
                Face(player.transform.position);
                transform.rotation = Quaternion.LookRotation(Flat(player.transform.position - comeTo).sqrMagnitude > 0.01f ? Flat(player.transform.position - comeTo) : Vector3.forward);
                storm.StrikeAt(comeTo);
                Afterglow(comeTo);
                body.gameObject.SetActive(true);
                lean = 1f;
                roar = 0.5f;
                Play(Sound.GiantStep, 1f, 1.1f, 120f);
                Play(Sound.BlindGrowl, 0.85f, 0.85f, 80f);
                float near = 1f - Mathf.Clamp01(DistanceToPlayer / 50f);
                player.AddShake(0.6f + 1.4f * near);
            }
            // Rising from the crouch, a moment, then it listens.
            lean = Mathf.MoveTowards(lean, 0.4f, dt * 0.8f);
            roar = Mathf.MoveTowards(roar, 0f, dt * 0.6f);
            body.Pose(0f, s.height, 0.3f, roar, lean, dt);
            if (t >= fit + goneFor + 1.4f)
            {
                SetState(BlindState.Roaming);
                perchLeft = Random.Range(s.perchMin, Mathf.Max(s.perchMin, s.perchMax));
            }
        }

        // Where it comes in: round you, between near and far; sometimes where you're looking; not in a light, not
        // in a building (the ground there, and nothing solid in the way of standing).
        Vector3 TeleportSpot()
        {
            Vector3 you = player.transform.position;
            Vector3 ahead = Vector3.ProjectOnPlane(player.Head.forward, Vector3.up).normalized;
            if (ahead.sqrMagnitude < 0.01f) ahead = Vector3.forward;
            var zones = Services.TryGet<SafeZones>(out var z) ? z : null;
            for (int tries = 0; tries < 12; tries++)
            {
                float bearing = Random.value < s.teleportInView ? Random.Range(-35f, 35f) : Random.Range(0f, 360f);
                Vector3 dir = Quaternion.Euler(0f, bearing, 0f) * ahead;
                Vector3 p = you + dir * Random.Range(s.teleportNear, Mathf.Max(s.teleportNear, s.teleportFar));
                p.y = PlayerSense.GroundHeight(p);
                if (zones != null && zones.Contains(p, 4f)) continue;
                if (Physics.CheckSphere(p + Vector3.up * 2f, 1.2f, Layers.Mask(Layers.Props), QueryTriggerInteraction.Ignore)) continue;
                return p;
            }
            return RoamPoint();
        }

        // Burn lines left glowing on the ground where a bolt struck, cooling over a few seconds.
        void Afterglow(Vector3 at)
        {
            if (scorch != null) Destroy(scorch.gameObject);
            scorch = StrikeScorch.Burn(at, s.lightningColor, 4f, transform.parent);
        }

        // Where you were trailBehind seconds ago (or, with no trail yet, round where you are).
        Vector3 TrailPoint()
        {
            int back = Mathf.Clamp(Mathf.RoundToInt(s.trailBehind / 2f), 0, trail.Count - 1);
            if (trail.Count == 0) return RoamPoint();
            Vector3 p = default;
            int i = 0, want = trail.Count - 1 - back;
            foreach (var q in trail) { if (i++ == want) { p = q; break; } }
            // (once it's there, a little on toward where you went next)
            if (Flat(p - transform.position).magnitude < 3f) p = player.transform.position + Flat(p - player.transform.position) * 0.5f;
            p.y = PlayerSense.GroundHeight(p);
            return p;
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
            // Listening hard: you moving anywhere near, crouched or not, and it's coming.
            if (hardListening && player.Moving && DistanceToPlayer <= s.hardListenReach * Difficulty.Now.blindHearing && deafFor <= 0f)
            {
                hardListening = false;
                RushTo(player.transform.position);
                return;
            }
            if (stateTime >= s.listenTime * (hardListening ? 1.5f : 1f))
            {
                hardListening = false;
                SetState(BlindState.Roaming);
            }
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
            if (flow == null || !flow.CanTake) return;
            if (Services.TryGet<SafeZones>(out var zones) && zones.Contains(player.transform.position)) return;
            StartTaking();
        }

        // Get far from it for long and it catches up, somewhere behind you.
        void KeepUp(float dt)
        {
            if (s.teleports) { lostFor = 0f; return; } // (it comes in round you anyway)
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
            Play(Sound.BlindGrowl, 1f, 0.8f, 20f);
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

        // It barges through the forest: trees in its way burst into splinters.
        void Shoulder(Vector3 dir)
        {
            if (!Services.TryGet<WorldBuilder>(out var world)) return;
            int n = Physics.OverlapSphereNonAlloc(transform.position + dir * 1.2f + Vector3.up * 2f, 1.3f, nearby, Layers.Mask(Layers.Trees), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < n; i++)
            {
                var tree = nearby[i].GetComponent<ForestTree>();
                if (tree != null) tree.Shatter(world.Forest, transform.position);
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
