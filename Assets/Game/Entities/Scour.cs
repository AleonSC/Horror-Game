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
    public sealed partial class Scour : MonoBehaviour
    {
        ScourSettings s;
        PlayerController player;
        ScourBody body;
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
        Vector3 retreatFrom, retreatDir;
        Collider peekTree;
        bool ducker;
        float peekOut = 1f;
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

        public void Init(int scourId, ScourSettings settings, float firstDelay, System.Random rng)
        {
            id = scourId;
            s = settings;
            gameObject.layer = Layers.Entities;
            body = gameObject.AddComponent<ScourBody>();
            body.Build(s.height, rng.NextDouble() < s.extraArmsChance, rng);
            Hide(firstDelay);
        }

        void Update()
        {
            if (player == null && !Services.TryGet(out player)) return;
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
            body.SetVisible(false);
            growth = 1f;
            body.SetGrowth(1f);
            if (State == ScourState.Taking && player != null) player.SetHeld(false);
            SetState(ScourState.Hidden, seconds > 0f ? $"gone for {seconds:0} s" : "gone");
            timer = seconds;
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
            huntProgress = 0f;
            eyeFlare = 1f;
            blue = 0f;      // (red again, whatever happened last time)
            eyeshine = 0f;
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
            bool lit = PlayerSense.InBeam(player, body.Chest, s.beamMargin);
            bool advancing = false;

            if (playerSafe)
            {
                if (!LookedAt()) CircleLight(safe, dt);
            }
            else if (lit && dist < s.reactDistance)
            {
                // Faced down: you slow as you near it, and reaching it starts the crescendo.
                player.LimitApproach(transform.position, s.reactDistance, s.stopDistance);
                if (dist <= s.stopDistance + s.triggerMargin)
                {
                    StartFacedDown();
                    return;
                }
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
            if (LookedAt())
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
            if (Services.TryGet<SafeZones>(out var zones) && zones.Contains(next, s.ringMargin)) return;
            MoveTo(next);
            peekTree = null; // stepped out into the open
            body.Face(target);
            Play(Sound.Creep, 0.7f, Random.Range(0.85f, 1.15f), 25f);
            if (Services.TryGet<EventBus>(out var events)) events.Publish(new EntityStateChanged(EntityKind.Scour, id, $"crept to {PlayerSense.FlatDistance(transform.position, target):0.0} m"));
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

        // ------------------------------------------------------------------ Peeking from behind a trunk

        bool FindTreeNear(Vector3 p, out Collider tree)
        {
            tree = null;
            var hits = Physics.OverlapSphere(p, 6f, Layers.Mask(Layers.Trees), QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            foreach (var h in hits)
            {
                float d = (h.transform.position - p).sqrMagnitude;
                if (d < best) { best = d; tree = h; }
            }
            return tree != null;
        }

        // Behind the trunk from the player; 'out' 1 leans out to one side, 0 tucked fully behind.
        Vector3 PeekSpot(float outAmount)
        {
            Vector3 trunk = peekTree.transform.position;
            Vector3 away = trunk - player.transform.position;
            away.y = 0f;
            away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.forward;
            Vector3 side = Vector3.Cross(Vector3.up, away);
            float r = peekTree is CapsuleCollider c ? c.radius * peekTree.transform.lossyScale.x : 0.35f;
            Vector3 spot = trunk + away * (r + 0.3f) + side * (r + 0.1f) * outAmount;
            spot.y = PlayerSense.GroundHeight(spot);
            return spot;
        }

        // A ducker snaps back behind its trunk when looked at, and leans out again when you look away.
        void UpdatePeeking(float dt)
        {
            bool watched = Time.time - appearedAt > DuckGrace &&
                           PlayerSense.IsLookedAt(player, peekTree.transform.position + Vector3.up * 1.6f, s.lookAngle * 0.6f);
            // Ducks back quickly (a sharp creep, not a jump), leans out again slowly.
            peekOut = Mathf.MoveTowards(peekOut, watched ? 0f : 1f, dt * (watched ? 3f : 0.6f));
            transform.position = Vector3.MoveTowards(transform.position, PeekSpot(peekOut), 2.5f * dt);
        }

        // ------------------------------------------------------------------ Eyes and the flashlight

        void UpdateEyes(float closeness, bool eyesLit)
        {
            float hunt = Mathf.Lerp(s.startEyeGlow, s.endEyeGlow, huntProgress * huntProgress);
            float flash = s.spawnEyeGlow * Mathf.Lerp(0.1f, 1f, huntProgress);
            float glow = Mathf.Lerp(hunt, Mathf.Max(hunt, flash), eyeFlare);
            eyeshine = Mathf.MoveTowards(eyeshine, eyesLit ? 1f : 0f, Time.deltaTime * 6f);
            body.SetEyes(Mathf.Lerp(1f, 1.6f, closeness), Mathf.Max(glow, eyeshine * s.eyeshine), blue, eyeshine * (1f - blue), s.eyeLight, s.eyeLightRange);
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
                return Mathf.Max(s.minCrescendoTime, s.crescendoTime / (1f + s.crescendoSpeedupPerLamp * lamps));
            }
        }

        void StartFacedDown()
        {
            SetState(ScourState.FacedDown, "faced down");
            float duration = CrescendoDuration;
            Play(Sound.Crescendo, 0.9f, s.crescendoTime / duration, 30f);
        }

        void UpdateFacedDown()
        {
            body.Face(player.transform.position);
            player.LimitApproach(transform.position, s.reactDistance, s.stopDistance);

            // The beam has to stay on it; drop it and it's back to stalking you.
            if (PlayerSense.InBeam(player, body.Chest, s.beamMargin + 3f)) lastLitTime = Time.time;
            else if (Time.time - lastLitTime > 0.6f)
            {
                SetState(ScourState.Stalking, "the light left it");
                creepTimer = NextCreepDelay();
                return;
            }

            timer += Time.deltaTime;
            float c = Mathf.Clamp01(timer / CrescendoDuration);
            float e = c * c; // slow build, violent finish
            body.Shudder(Mathf.Lerp(s.vibration, s.crescendoVibration, e), e);
            blue = Mathf.Max(blue, Mathf.SmoothStep(0f, 1f, c));
            body.SetEyes(Mathf.Lerp(1.6f, 4f, c), Mathf.Lerp(s.endEyeGlow, s.spawnEyeGlow * 1.5f, e), blue, 0f, s.eyeLight * (1f + 4f * blue), s.eyeLightRange);
            player.AddShake(s.cameraShake * e);
            if (c >= 1f) Dispel();
        }

        // Driven off: a hole tears open behind it and it's drawn back in. The light pays for it.
        void Dispel()
        {
            if (player.Flashlight != null) player.Flashlight.Battery.Spend(s.dismissCost);
            Vector3 toPlayer = player.transform.position - transform.position;
            toPlayer.y = 0f;
            retreatDir = toPlayer.sqrMagnitude > 0.001f ? -toPlayer.normalized : -transform.forward;
            retreatFrom = transform.position;
            Vector3 centre = transform.position + retreatDir * 0.45f + Vector3.up * s.height * 0.52f;
            portal = ScourPortal.Open(centre, -retreatDir, new Vector2(0.8f, 1.45f) * (s.height / 2.4f), 0.35f);
            Play(Sound.Portal, 1f, 1f, 35f);
            SetState(ScourState.Retreating, "driven off");
        }

        void UpdateRetreating()
        {
            const float delay = 0.35f, pullTime = 1.3f;
            timer += Time.deltaTime;
            float t = Mathf.Clamp01((timer - delay) / pullTime);
            float pull = t * t * t; // slow to give, then snatched away
            transform.position = retreatFrom + retreatDir * pull * 2.2f;
            body.Shudder(Mathf.Lerp(s.crescendoVibration, s.vibration, t), 1f - t);
            body.SetEyes(Mathf.Lerp(4f, 0.3f, pull), s.spawnEyeGlow * (1f - pull), 1f - t * 0.5f, 0f, s.eyeLight, s.eyeLightRange);
            player.AddShake(s.cameraShake * 0.5f * (1f - t));
            if (t >= 1f) Hide(Random.Range(s.minReturnDelay, s.maxReturnDelay));
        }

        // ------------------------------------------------------------------ Provoked

        void StartCharging()
        {
            peekTree = null;
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

        void MoveTo(Vector3 p)
        {
            p.y = PlayerSense.GroundHeight(p);
            transform.position = p;
        }

        void Play(Sound sound, float volume, float pitch, float range)
        {
            if (Services.TryGet<SoundPlayer>(out var sounds))
                sounds.Play(sound, transform.position + Vector3.up * 1.6f, MixerNames.Entities, volume, pitch, range);
        }
    }
}
