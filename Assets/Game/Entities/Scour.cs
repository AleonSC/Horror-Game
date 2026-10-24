using System.Collections.Generic;
using Game.Audio;
using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.Entities
{
    public enum ScourState { Hidden, Appearing, Stalking, FacedDown, Retreating, Charging, Taking }

    /// <summary>
    /// The main threat (section 6.7). It appears 22-32 m off, ahead of you. It creeps closer whenever you look
    /// away. Near you in the dark it grows and comes for you. Walk at it with the beam on it and you face it
    /// down: it's drawn back through a hole in the air. Flash the light at it and it charges. Reach you and it
    /// takes you. It never enters a safe light. (The take sequence is in Scour.Take.cs.)
    /// </summary>
    public sealed partial class Scour : MonoBehaviour, IThreat
    {
        ScourSettings s;
        PlayerController player;
        ScourLook body;
        ScourPortal portal;
        int id;
        float timer;
        float creepTimer;
        float growth = 1f;
        float huntProgress;
        float eyeFlare;
        float eyeshine;
        float blue;
        float lastLitTime = -10f;
        float lastPound;
        int aggression;
        int seenToggles = -1;
        readonly Queue<float> flashes = new Queue<float>();
        Collider peekTree;
        bool ducker;
        float peekOut = 1f;
        float eyesSeen = 1f;
        float appearedAt;
        bool everAppeared;

        // A ducker appears tucked mostly behind its trunk, just an edge of it showing.
        const float PeekOnAppear = 0.35f;
        // For a moment after it appears, it lets itself be seen before ducking.
        const float DuckGrace = 1.5f;

        public ScourState State { get; private set; } = ScourState.Hidden;
        public int Id => id;
        public bool Visible => State != ScourState.Hidden && body.Visible;
        public Vector3 Position => transform.position;
        public float DistanceToPlayer => player != null ? PlayerSense.FlatDistance(transform.position, player.transform.position) : float.MaxValue;

        public bool ThreatVisible => Visible;
        public bool ThreatTaking => State == ScourState.Taking || State == ScourState.Charging;
        /// <summary>Growing and coming for you in the dark, charging, or faced down.</summary>
        public bool ThreatHunting => ThreatTaking || State == ScourState.FacedDown || (State == ScourState.Stalking && growth > 1.02f);
        public bool ThreatWatching => false;

        void OnEnable() => Threats.Add(this);
        void OnDisable() => Threats.Remove(this);

        public void Init(int scourId, ScourConfig config, float firstDelay, System.Random rng)
        {
            id = scourId;
            s = config.settings;
            gameObject.layer = Layers.Entities;
            body = ScourLook.AddTo(gameObject, config);
            body.Build(config, rng);
            Hide(firstDelay);
        }

        void Update()
        {
            if (player == null && !Services.TryGet(out player)) return;
            if (!GameFlow.IsPlaying) return; // (nothing hunts under the main menu)
            body.EyeLightOnSurfaces = s.eyeLightOnSurfaces;
            // Through the lone door nothing can follow: it keeps away until you come back.
            if (PlayerPlace.InDarkRoom)
            {
                if (State == ScourState.Hidden) timer = Mathf.Max(timer, 3f);
                else if (State != ScourState.Taking) Hide(Random.Range(s.minReturnDelay, s.maxReturnDelay));
                return;
            }
            var catchFlow = player.GetComponent<CatchFlow>();
            if (catchFlow != null && catchFlow.Active && State != ScourState.Taking)
            {
                if (State != ScourState.Hidden) Hide(s.returnAfterCaught);
                return;
            }

            switch (State)
            {
                case ScourState.Hidden:
                    timer -= Time.deltaTime;
                    if (timer <= 0f) StartAppearing();
                    break;
                case ScourState.Appearing: UpdateAppearing(); break;
                case ScourState.Stalking: UpdateStalking(); break;
                case ScourState.FacedDown: UpdateFacedDown(); break;
                case ScourState.Retreating: UpdateRetreating(); break;
                case ScourState.Charging: UpdateCharging(); break;
                case ScourState.Taking: UpdateTaking(); break;
            }
        }

        void SetState(ScourState next, string note = null)
        {
            State = next;
            timer = 0f;
            if (Services.TryGet<EventBus>(out var events)) events.Publish(new EntityStateChanged(EntityKind.Scour, id, note ?? next.ToString()));
        }

        // ------------------------------------------------------------------ Hidden and appearing

        /// <summary>Gone for a while; it comes back somewhere new.</summary>
        public void Hide(float seconds)
        {
            if (portal != null) portal.Close();
            portal = null;
            if (blinkTime >= 0f) { blinkArrive = null; blinkTime = -1f; body.SetMaterialize(1f); body.Shudder(0f); }
            body.SetVisible(false);
            body.ShutMouth();
            body.SetPose(ScourPose.Neutral, 1, true);
            growth = 1f;
            body.SetGrowth(1f);
            if (State == ScourState.Taking)
            {
                // Let go part way: back on your feet, and the world back.
                if (player != null)
                {
                    player.SetHeld(false);
                    if (liftBy > 0f) player.Teleport(takeStart, player.transform.rotation, keepPitch: true);
                }
                StopWorldGoing();
            }
            SetState(ScourState.Hidden, seconds > 0f ? $"gone for {seconds:0} s" : "gone");
            timer = seconds;
        }

        /// <summary>
        /// Driven off by something else (a flare in the sky, a camera flash): it's snatched back through a hole and
        /// gone for at least <paramref name="seconds"/>. One already gone stays gone at least that long.
        /// </summary>
        public void DriveOff(float seconds)
        {
            if (State == ScourState.Taking) return;
            if (State == ScourState.Hidden)
            {
                timer = Mathf.Max(timer, seconds);
                return;
            }
            Play(Sound.Portal, 0.8f, 1.2f, 35f);
            Hide(seconds);
        }

        void StartAppearing()
        {
            if (!TryPickSpawn(out var spot))
            {
                timer = 1f; // nowhere good right now: try again shortly
                return;
            }
            transform.position = spot;
            body.Face(player.transform.position);
            body.SetVisible(true);
            body.SetMaterialize(0f);
            if (ducker) body.SetPose(ScourPose.Peek, 1, true);
            else body.SetPose(CreepPose(), RandomSide(), true);
            huntProgress = 0f;
            eyeFlare = 1f;
            blue = 0f;      // (red again, whatever happened last time)
            eyesSeen = PlayerSense.Occluded(player.Head.position, spot + Vector3.up * s.height) ? 0f : 1f;
            eyeshine = 0f;
            UpdateEyes(0f, false); // (set its eyes now, not next frame: no flash of whatever they were)
            Play(Sound.Appear, 0.8f, 1f, 45f);
            SetState(ScourState.Appearing, ducker ? "appearing (behind a tree)" : "appearing");
            if (!everAppeared && Services.TryGet<EventBus>(out var events)) events.Publish(new EntitySpawned(EntityKind.Scour, id, spot));
            everAppeared = true;
        }

        void UpdateAppearing()
        {
            timer += Time.deltaTime;
            float t = timer / Mathf.Max(0.05f, s.materializeTime);
            body.SetMaterialize(t);
            UpdateEyes(0f, false); // (pure red: blue and eyeshine were reset when it began appearing)
            if (t < 1f) return;
            body.Shudder(0f);
            appearedAt = Time.time;
            creepTimer = NextCreepDelay();
            SetState(ScourState.Stalking, "watching");
        }

        bool TryPickSpawn(out Vector3 spot)
        {
            var eye = player.Head;
            Vector3 forward = Vector3.ProjectOnPlane(eye.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.01f) forward = player.transform.forward;
            var zones = Services.TryGet<SafeZones>(out var z) ? z : null;
            float closer = 1f - 0.08f * aggression;
            for (int i = 0; i < 24; i++)
            {
                float angle = Random.Range(-s.spawnViewAngle, s.spawnViewAngle);
                float dist = Random.Range(s.minSpawnDistance, s.maxSpawnDistance) * closer;
                Vector3 p = player.transform.position + Quaternion.Euler(0f, angle, 0f) * forward * dist;
                p.y = PlayerSense.GroundHeight(p);
                if (zones != null && zones.Contains(p, s.ringMargin)) continue;
                if (Physics.CheckSphere(p + Vector3.up * 1f, 0.45f, Layers.Mask(Layers.Trees) | Layers.Mask(Layers.Props), QueryTriggerInteraction.Ignore)) continue;

                ducker = Random.value < s.duckerChance && FindTreeNear(p, out peekTree);
                if (!ducker) peekTree = null;
                peekOut = PeekOnAppear;
                spot = ducker ? PeekSpot(peekOut) : p;
                return true;
            }
            spot = default;
            return false;
        }

        // ------------------------------------------------------------------ Stalking

        void UpdateStalking()
        {
            if (UpdateBlink()) return;
            float dt = Time.deltaTime;
            Vector3 target = player.transform.position;
            float dist = PlayerSense.FlatDistance(transform.position, target);
            var zones = Services.TryGet<SafeZones>(out var z) ? z : null;

            if (dist > s.relocateDistance)
            {
                Hide(Random.Range(1f, 3f));
                return;
            }
            // A light lit right where it stands drives it off.
            if (zones != null && zones.Contains(transform.position))
            {
                Play(Sound.Portal, 0.7f, 1.3f, 30f);
                Hide(Random.Range(s.minReturnDelay, s.maxReturnDelay));
                return;
            }

            if (ducker && peekTree != null) UpdatePeeking(dt);
            body.Face(target);
            eyeFlare = Mathf.MoveTowards(eyeFlare, 0f, dt / Mathf.Max(0.01f, s.eyeDimTime));

            SafeZone safe = null;
            bool playerSafe = zones != null && zones.Contains(target, out safe);
            bool lit = LitByBeam(s.beamMargin);
            bool advancing = false;

            if (playerSafe)
            {
                if (!LookedAt()) CircleLight(safe, dt);
            }
            else if (lit && dist < s.reactDistance)
            {
                // Your light on it, anywhere in range: the crescendo starts building.
                StartFacedDown();
                return;
            }
            else if (!lit && dist < s.reactDistance)
            {
                // Near you in the dark: it grows and comes for you.
                advancing = true;
                if (dist <= s.touchDistance)
                {
                    StartTaking();
                    return;
                }
                Vector3 step = (target - transform.position);
                step.y = 0f;
                MoveTo(transform.position + step.normalized * s.advanceSpeed * (1f + 0.3f * aggression) * dt);
                PoundCue(dist);
            }

            growth = advancing ? Mathf.Min(s.maxGrowth, growth + s.growRate * dt) : Mathf.MoveTowards(growth, 1f, s.growRate * 2f * dt);
            body.SetGrowth(growth);
            float closeness = Mathf.Clamp01(Mathf.InverseLerp(s.reactDistance, s.stopDistance, dist));
            body.Shudder(s.vibration * Mathf.Max(closeness, advancing ? 0.6f : 0f));

            // How far along the hunt is: how close it's got, or how big it's grown. It only rises.
            float progress = Mathf.Max(Mathf.InverseLerp(s.maxSpawnDistance, s.killDistance, dist), Mathf.InverseLerp(1f, s.maxGrowth, growth));
            if (progress > huntProgress) huntProgress = Mathf.MoveTowards(huntProgress, progress, dt * 0.4f);

            // The last few metres: its mouth drops open, red light glaring out of it.
            body.SetMouth(Mathf.InverseLerp(s.mouthOpenDistance, s.mouthFullDistance, dist));

            bool eyesLit = PlayerSense.InBeam(player, body.EyePoint, 0f, ducker);
            if (WatchForFlashing(eyesLit || lit)) return;
            UpdateEyes(closeness, eyesLit);
            blue = Mathf.MoveTowards(blue, lit && dist < s.reactDistance ? 0.6f : 0f, dt * 2f);

            if (!playerSafe)
            {
                creepTimer -= dt;
                if (creepTimer <= 0f) TryCreep(dist, target);
            }
        }

        bool LookedAt() => PlayerSense.IsLookedAt(player, body.Chest, s.lookAngle) || PlayerSense.IsLookedAt(player, body.EyePoint, s.lookAngle);

        float NextCreepDelay() => Random.Range(s.minCreepInterval, s.maxCreepInterval) / (1f + 0.5f * aggression);

        void TryCreep(float dist, Vector3 target)
        {
            // Watching it holds it still, but in the dark (light off) only if the setting says so.
            bool lightOn = player.Flashlight != null && player.Flashlight.IsOn;
            bool held = LookedAt() && (lightOn || !s.creepsWhenWatchedInDark);
            if (held)
            {
                creepTimer = Random.Range(1f, 2f); // watched: try again shortly
                return;
            }
            creepTimer = NextCreepDelay();
            if (dist <= s.killDistance)
            {
                StartTaking();
                return;
            }
            // A creep stops just short of reach; only once it's right there does it take you.
            float stepLength = s.creepStep * (1f + 0.25f * aggression);
            float step = Mathf.Min(stepLength, dist - s.killDistance * 0.85f);
            Vector3 dir = target - transform.position;
            dir.y = 0f;
            dir.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, dir) * Random.Range(-1f, 1f) * Mathf.Clamp01(step / stepLength);
            Vector3 next = transform.position + dir * step + side;
            var zones = Services.TryGet<SafeZones>(out var z) ? z : null;
            if (zones != null && zones.Contains(next, s.ringMargin)) return;

            // Sometimes it slips behind a tree on the way instead, to peek out at you from there.
            if (Random.value < s.creepToTreeChance && FindTreeOnTheWay(next, dist, stepLength, target, out var tree))
            {
                peekTree = tree;
                ducker = true;
                peekOut = 0f;            // arrives tucked right behind the trunk, out of sight
                appearedAt = -DuckGrace; // (no grace: it's hiding, not showing itself)
                Vector3 spot = PeekSpot(peekOut);
                if (zones == null || !zones.Contains(spot, s.ringMargin))
                {
                    Blink(() =>
                    {
                        transform.position = spot;
                        body.Face(target);
                        body.SetPose(ScourPose.Peek, 1, true);
                        if (Services.TryGet<EventBus>(out var ev)) ev.Publish(new EntityStateChanged(EntityKind.Scour, id, $"crept behind a tree at {PlayerSense.FlatDistance(transform.position, target):0.0} m"));
                    });
                    return;
                }
                peekTree = null;
            }

            peekTree = null; // stepping out into the open
            Blink(() =>
            {
                MoveTo(next);
                body.Face(target);
                body.SetPose(CreepPose(), RandomSide(), true);
                if (Services.TryGet<EventBus>(out var events)) events.Publish(new EntityStateChanged(EntityKind.Scour, id, $"crept to {PlayerSense.FlatDistance(transform.position, target):0.0} m"));
            });
        }

        // A creep: it comes apart where it stands and pulls back together at the new spot, in a blink.
        System.Action blinkArrive;
        float blinkTime = -1f;

        void Blink(System.Action arrive)
        {
            Play(Sound.Creep, 0.7f, Random.Range(0.85f, 1.15f), 25f);
            if (!s.creepBlink)
            {
                arrive();
                return;
            }
            blinkArrive = arrive;
            blinkTime = 0f;
        }

        /// <summary>Mid-blink: true while it's still coming apart or pulling together (nothing else happens then).</summary>
        bool UpdateBlink()
        {
            if (blinkTime < 0f) return false;
            blinkTime += Time.deltaTime;
            float gone = Mathf.Max(0.01f, s.blinkOut), back = Mathf.Max(0.01f, s.blinkIn);
            if (blinkTime < gone)
            {
                body.SetMaterialize(1f - blinkTime / gone);
                return true;
            }
            if (blinkArrive != null)
            {
                blinkArrive();
                blinkArrive = null;
            }
            float t = (blinkTime - gone) / back;
            if (t < 1f)
            {
                body.SetMaterialize(t);
                return true;
            }
            body.SetMaterialize(1f);
            body.Shudder(0f);
            blinkTime = -1f;
            return false;
        }

        // Can't come in: it prowls just outside the light while you're not looking.
        void CircleLight(SafeZone zone, float dt)
        {
            Vector3 from = transform.position - zone.Center;
            from.y = 0f;
            if (from.sqrMagnitude < 0.01f) from = Vector3.forward;
            float angle = Mathf.Atan2(from.z, from.x) + s.circleSpeed * dt / Mathf.Max(1f, zone.Radius);
            float r = zone.Radius + s.ringMargin;
            Vector3 want = zone.Center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * r;
            MoveTo(Vector3.MoveTowards(transform.position, want, (s.circleSpeed + 1f) * dt));
        }

        void PoundCue(float dist)
        {
            float interval = Mathf.Lerp(0.9f, 0.45f, Mathf.InverseLerp(1f, s.maxGrowth, growth));
            if (Time.time - lastPound < interval) return;
            lastPound = Time.time;
            Play(Sound.Pound, Mathf.Lerp(0.4f, 1f, Mathf.InverseLerp(s.reactDistance, 2f, dist)), Random.Range(0.9f, 1.05f), 30f);
        }

        // ------------------------------------------------------------------ Eyes and the flashlight

        void UpdateEyes(float closeness, bool eyesLit)
        {
            float hunt = Mathf.Lerp(s.startEyeGlow, s.endEyeGlow, huntProgress * huntProgress);
            float flash = s.spawnEyeGlow * Mathf.Lerp(0.1f, 1f, huntProgress);
            float glow = Mathf.Lerp(hunt, Mathf.Max(hunt, flash), eyeFlare);
            eyeshine = Mathf.MoveTowards(eyeshine, eyesLit ? 1f : 0f, Time.deltaTime * 6f);
            // The glow round its eyes is a light with no shadows: it would shine through a trunk into the
            // fog. So it fades out while the eyes are hidden from you, and back when they're in sight.
            bool hidden = PlayerSense.Occluded(player.Head.position, body.EyePoint);
            eyesSeen = Mathf.MoveTowards(eyesSeen, hidden ? 0f : 1f, Time.deltaTime * 4f);
            body.SetEyes(Mathf.Lerp(1f, 1.6f, closeness), Mathf.Max(glow, eyeshine * s.eyeshine), blue, eyeshine * (1f - blue), s.eyeLight * eyesSeen, s.eyeLightRange);
        }

        /// <summary>Counts flicks of the flashlight while it's on it. Enough, quickly enough, and it charges.</summary>
        bool WatchForFlashing(bool aimedAt)
        {
            var light = player.Flashlight;
            if (light == null) return false;
            if (aimedAt) lastLitTime = Time.time;
            if (seenToggles < 0) seenToggles = light.ToggleCount;
            while (seenToggles < light.ToggleCount)
            {
                seenToggles++;
                // On: it has to be in the beam now. Off: it had to be a moment ago.
                bool counts = light.IsOn ? aimedAt : Time.time - lastLitTime < 0.4f;
                if (counts) flashes.Enqueue(Time.time);
            }
            while (flashes.Count > 0 && Time.time - flashes.Peek() > s.flashWindow) flashes.Dequeue();
            if (flashes.Count < s.flashesToProvoke) return false;
            flashes.Clear();
            StartCharging();
            return true;
        }

        // ------------------------------------------------------------------ Faced down

        float CrescendoDuration
        {
            get
            {
                int lamps = Services.TryGet<GameRun>(out var run) ? run.LampsLit : 0;
                float time = s.crescendoTime / (1f + s.crescendoSpeedupPerLamp * lamps);
                return Mathf.Max(s.minCrescendoTime, time) * Difficulty.Now.faceDownTime;
            }
        }

        float faceDown;   // 0-1: how far the crescendo has built

        /// <summary>Your beam on it, anywhere on its body (chest, head or hips), with nothing in the way.</summary>
        bool LitByBeam(float margin)
        {
            return PlayerSense.InBeam(player, body.Chest, margin)
                || PlayerSense.InBeam(player, body.EyePoint, margin)
                || PlayerSense.InBeam(player, transform.position + Vector3.up * s.height * growth * 0.35f, margin);
        }

        void StartFacedDown()
        {
            SetState(ScourState.FacedDown, "faced down");
            faceDown = 0f;
            lastLitTime = Time.time;
            Play(Sound.Crescendo, 0.9f, 1f, 30f);
        }

        // Hold the light on it and the crescendo builds (faster the closer you are). Lose it for a moment and the
        // build holds; lose it for longer and it drains away, and when it's gone it's back to stalking you.
        void UpdateFacedDown()
        {
            float dt = Time.deltaTime;
            body.Face(player.transform.position);
            float dist = DistanceToPlayer;
            // You can't walk into it (slowing over the last stretch only).
            player.LimitApproach(transform.position, s.stopDistance + 1.5f, s.stopDistance);

            if (LitByBeam(s.beamMargin + 3f)) lastLitTime = Time.time;
            bool lit = Time.time - lastLitTime < 0.05f;
            float closeness = Mathf.Clamp01(Mathf.InverseLerp(s.reactDistance, s.stopDistance, dist));
            if (lit) faceDown += dt / CrescendoDuration * Mathf.Lerp(s.buildFar, s.buildNear, closeness);
            else if (Time.time - lastLitTime > s.faceDownGrace) faceDown -= dt * s.faceDownDrain;
            if (faceDown <= 0f && !lit)
            {
                SetState(ScourState.Stalking, "the light left it");
                creepTimer = NextCreepDelay();
                blue = 0f;
                return;
            }
            faceDown = Mathf.Clamp01(faceDown);

            float c = faceDown;
            float e = c * c; // slow build, violent finish
            body.Shudder(Mathf.Lerp(s.vibration, s.crescendoVibration, e), e);
            body.SetMouth(Mathf.Lerp(0.7f, 1.5f, e)); // stretching wider and wider
            blue = Mathf.SmoothStep(0f, 1f, c);
            // The blue stays in the eyes: they burn brighter, but the light they throw barely rises.
            body.SetEyes(Mathf.Lerp(1.6f, 3f, c), Mathf.Lerp(s.endEyeGlow, s.spawnEyeGlow * 0.5f, e), blue, 0f, s.eyeLight * (1f + 0.6f * blue), s.eyeLightRange);
            player.AddShake(s.cameraShake * e);
            if (c >= 1f) Dispel();
        }

        // Driven off: it comes apart into dark motes that scatter on the wind. The light pays for it.
        void Dispel()
        {
            if (player.Flashlight != null) player.Flashlight.Battery.Spend(s.dismissCost);
            body.ShutMouth();
            Play(Sound.Appear, 0.9f, 0.6f, 35f);
            SetState(ScourState.Retreating, "driven off");
        }

        // A last shudder, then it bursts apart (the motes carry on by themselves) and it's gone.
        void UpdateRetreating()
        {
            const float last = 0.25f;
            timer += Time.deltaTime;
            body.Shudder(s.crescendoVibration * (1f - timer / last), 1f - timer / last);
            if (timer < last) return;
            ScourMotes.Burst(body, s.scatterTime, blue);
            Hide(Random.Range(s.minReturnDelay, s.maxReturnDelay));
        }

        // ------------------------------------------------------------------ Provoked

        void StartCharging()
        {
            peekTree = null;
            body.SetPose(ScourPose.Lunge, 1, false, 12f);
            Play(Sound.Shriek, 1f, 1.2f, 50f);
            SetState(ScourState.Charging, "provoked: charging");
        }

        void UpdateCharging()
        {
            timer += Time.deltaTime;
            Vector3 to = player.transform.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            body.Face(player.transform.position);
            body.Shudder(s.crescendoVibration);
            body.SetEyes(2.8f, s.spawnEyeGlow * 1.5f, 0f, 0f, s.eyeLight, s.eyeLightRange);
            body.SetMouth(1.6f);
            player.AddShake(Mathf.Lerp(0.3f, s.cameraShake * 1.5f, Mathf.InverseLerp(15f, s.chargeStopDistance, dist)));

            if (dist <= s.chargeStopDistance || timer > 3f)
            {
                // Right in your face... and gone. Back soon, angrier.
                player.AddShake(s.cameraShake * 2f);
                aggression = Mathf.Min(s.maxAggression, aggression + 1);
                Play(Sound.Portal, 0.8f, 1.4f, 20f);
                Hide(Random.Range(s.minChargeReturn, s.maxChargeReturn));
                return;
            }
            float step = Mathf.Min(s.chargeSpeed * Time.deltaTime, dist - s.chargeStopDistance);
            MoveTo(transform.position + to / dist * step);
        }

        // ------------------------------------------------------------------ Helpers

        static int RandomSide() => Random.value < 0.5f ? -1 : 1;

        void MoveTo(Vector3 p)
        {
            p.y = PlayerSense.GroundHeight(p);
            transform.position = p;
        }

        // Standing still and upright, facing you; or, if ScourConfig says so, a new unsettling pose each creep.
        ScourPose CreepPose() => s.creepPoses ? PoseShape.RandomCreepPose(() => Random.value) : ScourPose.Neutral;

        void Play(Sound sound, float volume, float pitch, float range)
        {
            if (Services.TryGet<SoundPlayer>(out var sounds))
                sounds.Play(sound, transform.position + Vector3.up * 1.6f, MixerNames.Entities, volume, pitch, range);
        }
    }
}
