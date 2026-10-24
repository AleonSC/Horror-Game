using System.Collections.Generic;
using Game.Audio;
using Game.Core;
using Game.Player;
using Game.Rendering;
using Game.World.Build;
using Game.World.Generation;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.Entities
{
    public enum GiantState { Hanging, Staring, Falling, Landed, Walking, Resting }

    /// <summary>
    /// The giant and the eye (section 6.11). From the start it hangs on a chain far off along the plan's bearing,
    /// turning slowly, with empty chains elsewhere round the horizon; each lamp lit jolts it. At the 5th lamp a red
    /// eye opens in the sky, your view is drawn up to it, the dream hallway takes hold and the world turns red. As
    /// it closes the chain snaps: the giant falls, lands (its theme), a shockwave rolls out, and it walks toward
    /// you, stopping now and then to scan. Close enough, it takes you; afterwards it's back on the horizon. At the
    /// 10th lamp searchlights from its eyes track you, and a crowd of scours circles the edge of the light.
    /// <para>It's far too big for the camera's range, so it's drawn nearer and scaled down to look the same.</para>
    /// </summary>
    public sealed class Giant : MonoBehaviour, IThreat
    {
        // Nearer than the camera's far plane (600 m), so it's always drawn.
        // (Well inside the camera's far clip, 600 m: swinging on its chain tips its feet away, and further than the
        // clip they'd be cut off whenever you look straight at it.)
        const float RenderDistance = 360f;

        GiantConfig config;
        GiantSettings s;
        PlayerController player;
        GiantBody body;
        Transform bodyRoot, chain, eye, pupil;
        readonly List<Transform> emptyChains = new List<Transform>();
        readonly List<float> emptyBearings = new List<float>();
        Material chainMat, eyeMat, pupilMat;
        Light[] searchlights;
        Renderer[] shafts;
        Vector3 searchAim;
        readonly List<ScourLook> crowd = new List<ScourLook>();
        readonly List<float> crowdAngle = new List<float>();
        readonly List<System.IDisposable> subs = new List<System.IDisposable>();

        float bearing;
        Vector3 truePos;      // where it really is (x, z), unwrapped world space
        float footHeight;     // how far its feet are above the player's (true metres)
        float timer;
        float swing, swingSpeed;
        float stride, nextScan, scanLeft, scan;
        float shockRadius = -1f;
        bool sawIt, eyeDone, searching, hiddenForRoom;
        int lastStep;

        public GiantState State { get; private set; } = GiantState.Hanging;
        public float Distance { get; private set; }
        public GiantSettings Settings => s;
        /// <summary>Its middle, where it's drawn (for the dev window's "look at it").</summary>
        public Vector3 ShownAt => bodyRoot != null && body != null ? bodyRoot.TransformPoint(body.NeckLocal * 0.6f) : transform.position;

        // IThreat: the sky watching (eye open, it walking). It never counts as close in the usual sense.
        public bool ThreatVisible => false;
        public float DistanceToPlayer => Distance;
        public bool ThreatTaking => false;
        public bool ThreatHunting => false;
        public bool ThreatWatching => State == GiantState.Staring || State == GiantState.Walking || State == GiantState.Landed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (!Services.TryGet<PlayerController>(out _) || !Services.TryGet<WorldBuilder>(out _)) return; // (the forest only)
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponent<Giant>() == null)
                new GameObject("Giant").AddComponent<Giant>().transform.SetParent(root.transform, false);
        }

        void Awake()
        {
            config = Services.Get<GameSetup>().Get<GiantConfig>();
            if (config == null)
            {
                Debug.LogWarning("No GiantConfig in the GameSetup asset; using defaults.");
                config = ScriptableObject.CreateInstance<GiantConfig>();
            }
            s = config.settings;
            bodyRoot = new GameObject("Giant (drawn)").transform;
            bodyRoot.SetParent(transform, false);
            body = GiantBody.Create(bodyRoot, config);
            chainMat = RuntimeMaterials.Silhouette("Giant chain");
            chain = Bar("Chain", chainMat);
            eyeMat = RuntimeMaterials.Haze("The eye", Color.red);
            pupilMat = RuntimeMaterials.Haze("The eye's pupil", Color.black);
            RuntimeMaterials.DrawAfter(eyeMat, 1);  // (over the body, which darkens what's behind it)
            RuntimeMaterials.DrawAfter(pupilMat, 2);
            eye = Ball("The eye", eyeMat);
            pupil = Ball("Pupil", pupilMat);
            pupil.SetParent(eye, false);
            pupil.localPosition = new Vector3(0f, 0f, -0.45f);
            pupil.localScale = new Vector3(0.28f, 0.9f, 0.2f);
            eye.gameObject.SetActive(false);
            Services.Register(this);
        }

        void Start()
        {
            if (Services.TryGet<EventBus>(out var events))
            {
                subs.Add(events.Subscribe<LampLit>(OnLampLit));
                subs.Add(events.Subscribe<RunStarted>(_ => Rehang()));
                subs.Add(events.Subscribe<PlayerRespawned>(_ => SendBack(s.restTime)));
                subs.Add(events.Subscribe<HuntsReset>(_ => { if (State == GiantState.Walking) { scanLeft = 10f; } }));
                // The watchtower's floodlights drive it back to the horizon.
                subs.Add(events.Subscribe<FloodlightsLit>(e => SendBack(s.restTime + e.Seconds)));
            }
            Threats.Add(this);
            Rehang();
        }

        void OnDestroy()
        {
            foreach (var sub in subs) sub.Dispose();
            Threats.Remove(this);
            Services.Unregister(this);
            foreach (var m in new[] { chainMat, eyeMat, pupilMat }) if (m != null) Destroy(m);
            SetWorldRed(0f);
        }

        // ------------------------------------------------------------------ the run

        void Rehang()
        {
            if (State != GiantState.Hanging && Services.TryGet<EventBus>(out var events)) events.Publish(new BossThemeEnded(EntityKind.Giant));
            State = GiantState.Hanging;
            bearing = Services.TryGet<WorldBuilder>(out var world) && world.Plan != null ? world.Plan.GiantBearing : 0f;
            sawIt = eyeDone = searching = false;
            timer = 0f;
            swing = swingSpeed = 0f;
            shockRadius = -1f;
            chain.gameObject.SetActive(true);
            eye.gameObject.SetActive(false);
            SetSearchlights(false);
            SetCrowd(false);
            SetWorldRed(0f);
            if (player != null) player.Dream = 0f;
            // The empty chains, somewhere else round the horizon.
            var rng = new System.Random(world != null && world.Plan != null ? world.Plan.Seed : 1);
            foreach (var c in emptyChains) if (c != null) Destroy(c.gameObject);
            emptyChains.Clear();
            emptyBearings.Clear();
            for (int i = 0; i < s.emptyChains; i++)
            {
                emptyChains.Add(Bar("Empty chain", chainMat));
                emptyBearings.Add(bearing + 50f + (float)rng.NextDouble() * 260f);
            }
        }

        void OnLampLit(LampLit e)
        {
            swingSpeed += s.joltSwing * (Random.value < 0.5f ? -1f : 1f);
            if (State == GiantState.Hanging && Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play2D(Sound.ChainJolt, MixerNames.Outside, 0.45f, 0.7f);
            if (e.TimesLit >= s.eyeAtLamp && State == GiantState.Hanging && !eyeDone) StartStaring();
            if (e.TimesLit >= s.searchlightsAtLamp) { SetSearchlights(true); SetCrowd(true); }
        }

        /// <summary>Back to the horizon (it took you, or the floodlights drove it off), to walk again after a rest.</summary>
        public void SendBack(float rest)
        {
            if (player == null || (State != GiantState.Walking && State != GiantState.Landed && State != GiantState.Resting)) return;
            Vector3 p = PlayerFeet;
            Vector3 dir = Flat(truePos - p);
            if (dir.sqrMagnitude < 0.01f) dir = Flat(Bearing(bearing));
            truePos = p + dir.normalized * s.HangDistance;
            footHeight = 0f;
            State = GiantState.Resting;
            timer = rest;
        }

        /// <summary>Dev: the eye opens now.</summary>
        public void OpenEyeNow()
        {
            if (State == GiantState.Hanging) StartStaring();
        }

        /// <summary>Dev: the 10th lamp's searchlights and crowd, now.</summary>
        public void SearchlightsNow()
        {
            SetSearchlights(true);
            SetCrowd(true);
        }

        // ------------------------------------------------------------------ every frame

        void Update()
        {
            if (player == null && !Services.TryGet(out player)) return;
            // Through the lone door there's no sky: it's gone from view, and it waits.
            if (PlayerPlace.InDarkRoom != hiddenForRoom) ShowForRoom(!PlayerPlace.InDarkRoom);
            if (hiddenForRoom) return;
            float dt = Time.deltaTime;
            Vector3 p = PlayerFeet;

            // Swinging on the chain (jolts), damped.
            swingSpeed += -swing * 0.6f * dt;
            swingSpeed *= Mathf.Exp(-0.25f * dt);
            swing += swingSpeed * dt;

            switch (State)
            {
                case GiantState.Hanging: Hang(p); if (GameFlow.IsPlaying) FirstSight(); break;
                case GiantState.Staring: Hang(p); Stare(dt); break;
                case GiantState.Falling: Fall(dt); break;
                case GiantState.Landed: Landed(dt, p); break;
                case GiantState.Walking: Walk(dt, p); break;
                case GiantState.Resting:
                    timer -= dt;
                    body.PoseWalking(stride, Mathf.Sin(Time.time * 0.3f), 0f);
                    if (timer <= 0f) State = GiantState.Walking;
                    break;
            }

            Shockwave(dt, p);
            Draw(p);
            UpdateSearchlights(dt, p);
            UpdateCrowd(dt, p);
        }

        Vector3 PlayerFeet => player.transform.position;

        void Hang(Vector3 p)
        {
            truePos = p + Bearing(bearing) * s.HangDistance;
            footHeight = s.HangDistance * Mathf.Tan(s.feetElevation * Mathf.Deg2Rad);
            body.PoseHanging(Mathf.Sin(Time.time * 0.37f) * 0.6f + swing * 0.05f);
        }

        // The first clear view of it (after a calm moment) draws your eyes up to it, once.
        void FirstSight()
        {
            if (sawIt || !s.drawViewOnFirstSight) return;
            if (!Services.TryGet<GameRun>(out var run) || run.Elapsed < s.firstSightAfter) return;
            var head = player.Head;
            Vector3 chest = bodyRoot.TransformPoint(new Vector3(0f, 0.7f, 0f));
            Vector3 to = chest - head.position;
            Vector3 flatLook = Flat(head.forward), flatTo = Flat(to);
            if (Vector3.Angle(flatLook, flatTo) > 50f) return;
            if (Physics.Raycast(head.position, to.normalized, 180f, Layers.Mask(Layers.Trees) | Layers.Mask(Layers.Ground) | Layers.Mask(Layers.Props), QueryTriggerInteraction.Ignore)) return;
            sawIt = true;
            player.DrawGazeTo(chest, 3.5f);
        }

        // ------------------------------------------------------------------ the eye

        void StartStaring()
        {
            State = GiantState.Staring;
            timer = 0f;
            eye.gameObject.SetActive(true);
            player.DrawGazeTo(EyeWorld(PlayerFeet), 3f);
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play2D(Sound.Moan, MixerNames.Stingers, 1f, 0.45f);
        }

        void Stare(float dt)
        {
            timer += dt;
            float open = s.eyeOpenTime, stare = s.eyeStareTime, close = s.eyeCloseTime;
            float lid = timer < open ? Mathf.SmoothStep(0f, 1f, timer / open)
                : timer < open + stare ? 1f
                : 1f - Mathf.SmoothStep(0f, 1f, (timer - open - stare) / close);
            eye.localScale = new Vector3(EyeSize, EyeSize * Mathf.Max(0.02f, 0.55f * lid), EyeSize * 0.35f);

            // The dream: after a moment, the view stretches and running gets you nowhere. It lets go as the eye shuts.
            float dream = Mathf.Clamp01((timer - s.dreamAfter) / Mathf.Max(0.1f, s.dreamRamp));
            if (timer > open + stare) dream *= lid;
            player.Dream = dream;
            SetWorldRed(lid);

            if (timer >= open + stare + close)
            {
                eye.gameObject.SetActive(false);
                eyeDone = true;
                player.Dream = 0f;
                SetWorldRed(0f);
                // The chain snaps like thunder, and it falls.
                chain.gameObject.SetActive(false);
                if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play2D(Sound.Thunder, MixerNames.Outside, 1f, 0.8f);
                player.AddShake(1.5f);
                State = GiantState.Falling;
                timer = 0f;
            }
        }

        float EyeSize => 2f * RenderDistance * Mathf.Tan(s.eyeSize * 0.5f * Mathf.Deg2Rad);

        Vector3 EyeWorld(Vector3 p)
        {
            float el = s.eyeElevation * Mathf.Deg2Rad;
            return p + Bearing(bearing) * RenderDistance * Mathf.Cos(el) + Vector3.up * RenderDistance * Mathf.Sin(el);
        }

        void SetWorldRed(float amount)
        {
            if (!Services.TryGet<LookController>(out var look)) return;
            look.FogTint = s.redFog;
            look.FogTintAmount = amount;
        }

        // ------------------------------------------------------------------ falling, landing, walking

        void Fall(float dt)
        {
            timer += dt;
            float t = Mathf.Clamp01(timer / Mathf.Max(0.1f, s.fallTime));
            footHeight = s.HangDistance * Mathf.Tan(s.feetElevation * Mathf.Deg2Rad) * (1f - t * t);
            body.PoseFalling(t);
            if (t < 1f) return;

            footHeight = 0f;
            State = GiantState.Landed;
            timer = 0f;
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play2D(Sound.Boom, MixerNames.Outside, 1f, 0.8f);
            if (Services.TryGet<EventBus>(out var events))
            {
                events.Publish(new BossThemeStarted(EntityKind.Giant));
                events.Publish(new EntitySpawned(EntityKind.Giant, 0, truePos));
            }
            player.AddShake(2f);
        }

        void Landed(float dt, Vector3 p)
        {
            timer += dt;
            body.PoseWalking(0f, 0f, 0f);
            if (shockRadius < 0f && timer >= s.shockwaveAfter)
            {
                shockRadius = 0f;
                // It whips the trees and grass over as it rolls out.
                if (Services.TryGet<Wind>(out var wind)) wind.AddShock(truePos, Time.time, s.shockwaveSpeed);
            }
            if (timer >= s.shockwaveAfter + 2f)
            {
                State = GiantState.Walking;
                nextScan = Random.Range(s.minScanEvery, s.maxScanEvery);
            }
        }

        void Walk(float dt, Vector3 p)
        {
            Vector3 to = Flat(p - truePos);
            Distance = to.magnitude;
            if (scanLeft > 0f)
            {
                scanLeft -= dt;
                scan = Mathf.Sin(Time.time * 0.8f);
                body.PoseWalking(stride, scan, 0f);
            }
            else
            {
                scan = Mathf.MoveTowards(scan, 0f, dt);
                stride += dt / Mathf.Max(0.5f, s.strideTime);
                truePos += to.normalized * Mathf.Min(Distance, s.walkSpeed * dt);
                body.PoseWalking(stride, scan, 1f);
                nextScan -= dt;
                if (nextScan <= 0f)
                {
                    nextScan = Random.Range(s.minScanEvery, s.maxScanEvery);
                    scanLeft = Random.Range(s.minScanTime, s.maxScanTime);
                }
                // A step every half stride: a thud and a shake, growing as it nears.
                int step = Mathf.FloorToInt(stride * 2f);
                if (step != lastStep)
                {
                    lastStep = step;
                    float near = 1f - Mathf.Clamp01(Distance / s.HangDistance);
                    if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play2D(Sound.GiantStep, MixerNames.Outside, Mathf.Lerp(0.25f, 1f, near), Random.Range(0.85f, 1f));
                    player.AddShake(s.stepShake * near * near);
                }
            }

            var catchFlow = player.GetComponent<CatchFlow>();
            if (Distance <= s.takeDistance && catchFlow != null && catchFlow.CanTake) catchFlow.Begin(EntityKind.Giant, 0);
        }

        // The shockwave from its landing, rolling out until it passes you: a gust of shake as it does.
        void Shockwave(float dt, Vector3 p)
        {
            if (shockRadius < 0f) return;
            float before = shockRadius;
            shockRadius += s.shockwaveSpeed * dt;
            float d = Flat(p - truePos).magnitude;
            if (before < d && shockRadius >= d)
            {
                player.AddShake(4f);
                if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play2D(Sound.Thunder, MixerNames.Outside, 0.8f, 0.55f);
            }
            if (shockRadius > d + 100f) shockRadius = -1f;
        }

        // ------------------------------------------------------------------ drawing

        // It's drawn at most RenderDistance away, scaled down by the same amount, so it looks just as big.
        void Draw(Vector3 p)
        {
            Vector3 flat = Flat(truePos - p);
            float d = Mathf.Max(1f, flat.magnitude);
            if (State != GiantState.Walking) Distance = d;
            Vector3 dir = flat / d;
            float r = Mathf.Min(d, RenderDistance);
            float k = r / d;
            float turn = State == GiantState.Hanging || State == GiantState.Staring
                ? Mathf.Sin(Time.time * Mathf.PI * 2f / Mathf.Max(1f, s.turnTime)) * s.turnAngle
                : 0f;
            bodyRoot.position = p + dir * r + Vector3.up * footHeight * k;
            bodyRoot.rotation = Quaternion.LookRotation(-dir) * Quaternion.Euler(0f, turn, 0f);
            // The chain's swing tips it about the neck.
            if (State == GiantState.Hanging || State == GiantState.Staring)
            {
                bodyRoot.RotateAround(bodyRoot.position + Vector3.up * s.height * k * body.NeckLocal.y, Vector3.Cross(Vector3.up, dir), swing);
            }
            bodyRoot.localScale = Vector3.one * s.height * k;

            // The haze: it keeps s.haze of the fog behind it (the eye glows over the top).
            if (Services.TryGet<LookController>(out var look))
            {
                var ls = look.Config.settings;
                float mul = LookController.ExposureMultiplier(ls.exposure);
                Color fog = Color.Lerp(ls.fogColor, look.FogTint, look.FogTintAmount).linear / mul;
                body.SetShade(s.haze, fog, s.beamThrough);
                RuntimeMaterials.SetSilhouette(chainMat, s.haze * 0.8f, fog, s.beamThrough);
                RuntimeMaterials.SetHazeColor(eyeMat, s.eyeColor.linear * s.eyeGlow / mul);
                RuntimeMaterials.SetHazeColor(pupilMat, Color.black);
            }

            if (chain.gameObject.activeSelf)
            {
                Vector3 neck = bodyRoot.TransformPoint(body.NeckLocal);
                PlaceBar(chain, neck, ChainTop(p, neck, r * 1.5f), s.height * k * 0.008f);
            }
            for (int i = 0; i < emptyChains.Count; i++)
            {
                Vector3 baseDir = Bearing(emptyBearings[i]);
                float rr = RenderDistance;
                float el = (25f + 8f * Mathf.Sin(i * 2.3f)) * Mathf.Deg2Rad;
                Vector3 bottom = p + baseDir * rr * Mathf.Cos(el) + Vector3.up * rr * Mathf.Sin(el);
                bottom += Vector3.Cross(Vector3.up, baseDir) * Mathf.Sin(Time.time * 0.21f + i) * rr * 0.004f;
                PlaceBar(emptyChains[i], bottom, ChainTop(p, bottom, rr * 1.5f), rr * 0.006f);
            }
            if (eye.gameObject.activeSelf)
            {
                eye.position = EyeWorld(p);
                eye.rotation = Quaternion.LookRotation(eye.position - player.Head.position);
            }
        }

        // A chain runs up from here this far, but stops short of the camera's far clip (or it's cut off overhead).
        static Vector3 ChainTop(Vector3 viewer, Vector3 from, float length)
        {
            var cam = Camera.main;
            float reach = (cam != null ? cam.farClipPlane : 600f) * 0.92f;
            Vector3 v = from - viewer;
            float room = v.y * v.y - (v.sqrMagnitude - reach * reach);
            float most = room > 0f ? -v.y + Mathf.Sqrt(room) : 0f;
            return from + Vector3.up * Mathf.Clamp(length, 0f, Mathf.Max(0f, most));
        }

        // ------------------------------------------------------------------ the 10th lamp

        void SetSearchlights(bool on)
        {
            if (on && searchlights == null)
            {
                searchlights = new Light[2];
                shafts = new Renderer[2];
                for (int i = 0; i < 2; i++)
                {
                    var go = new GameObject(i == 0 ? "Searchlight L" : "Searchlight R");
                    go.transform.SetParent(transform, false);
                    go.AddHDLight(LightType.Spot);
                    var l = go.GetComponent<Light>();
                    l.shadows = LightShadows.None;
                    searchlights[i] = l;
                    shafts[i] = Effects.Beam(go.transform, 1f, 1f, Color.black, 0.25f, 0.25f, "Searchlight shaft");
                    if (shafts[i] != null) shafts[i].sharedMaterial.SetFloat("_Falloff", 0.35f); // (bright all the way down)
                }
                searchAim = player != null ? player.transform.position + Random.insideUnitSphere * 30f : Vector3.zero;
            }
            searching = on;
            if (searchlights != null) foreach (var l in searchlights) l.enabled = on;
            if (shafts != null) foreach (var r in shafts) if (r != null) r.enabled = on;
        }

        void UpdateSearchlights(float dt, Vector3 p)
        {
            if (!searching || searchlights == null) return;
            // The circle chases you, a little faster than you can run.
            float speed = player.Settings.runSpeed * s.searchlightTracking;
            searchAim = Vector3.MoveTowards(searchAim, p, speed * dt);
            searchAim.y = p.y;
            for (int i = 0; i < 2; i++)
            {
                var l = searchlights[i];
                Vector3 from = bodyRoot.TransformPoint(body.EyesLocal + new Vector3(i == 0 ? -0.02f : 0.02f, 0f, 0f));
                float dist = Vector3.Distance(from, searchAim);
                l.transform.SetPositionAndRotation(from, Quaternion.LookRotation(searchAim - from));
                l.spotAngle = Mathf.Clamp(2f * Mathf.Atan(s.searchlightRadius / Mathf.Max(1f, dist)) * Mathf.Rad2Deg, 0.5f, 60f);
                l.innerSpotAngle = l.spotAngle * 0.8f;
                l.range = dist + 80f;
                l.color = s.searchlightColor;
                l.intensity = s.searchlightIntensity * 0.5f;
                if (shafts[i] != null)
                {
                    shafts[i].transform.localScale = new Vector3(s.searchlightRadius, s.searchlightRadius, dist);
                    Effects.SetGlow(shafts[i], s.searchlightColor * s.searchlightShaft);
                }
            }
        }

        void SetCrowd(bool on)
        {
            if (!on)
            {
                foreach (var c in crowd) if (c != null) Destroy(c.gameObject);
                crowd.Clear();
                crowdAngle.Clear();
                return;
            }
            if (crowd.Count > 0 || !Services.TryGet<GameSetup>(out var setup)) return;
            var scourConfig = setup.Get<ScourConfig>();
            if (scourConfig == null) return;
            var rng = new System.Random(7);
            for (int i = 0; i < s.crowd; i++)
            {
                var go = new GameObject($"Crowd scour {i}") { layer = Layers.Entities };
                go.transform.SetParent(transform, false);
                var look = ScourLook.AddTo(go, scourConfig);
                look.Build(scourConfig, rng);
                look.SetPose(ScourPose.Neutral, 1, true); // (standing, watching)
                crowd.Add(look);
                crowdAngle.Add(i / (float)s.crowd * Mathf.PI * 2f);
            }
        }

        // They ring the edge of the light round you, drifting slowly round, always facing in.
        void UpdateCrowd(float dt, Vector3 p)
        {
            for (int i = 0; i < crowd.Count; i++)
            {
                var c = crowd[i];
                if (c == null) continue;
                crowdAngle[i] += dt * (0.05f + 0.03f * Mathf.Sin(i * 1.7f));
                float r = s.crowdRadius + Mathf.Sin(Time.time * 0.4f + i) * 1.2f;
                Vector3 at = (searching ? searchAim : p) + new Vector3(Mathf.Cos(crowdAngle[i]), 0f, Mathf.Sin(crowdAngle[i])) * r;
                at.y = PlayerSense.GroundHeight(at);
                c.transform.position = at;
                c.Face(p);
                c.SetEyes(1f, 1.5f, 0f, 0f, 0f, 1f);
            }
        }

        void ShowForRoom(bool shown)
        {
            hiddenForRoom = !shown;
            foreach (Transform child in transform) child.gameObject.SetActive(shown);
            if (!shown)
            {
                if (player != null) player.Dream = 0f;
                SetWorldRed(0f);
                return;
            }
            chain.gameObject.SetActive(State == GiantState.Hanging || State == GiantState.Staring);
            eye.gameObject.SetActive(State == GiantState.Staring);
            if (searchlights != null) foreach (var l in searchlights) l.enabled = searching;
        }

        // ------------------------------------------------------------------ helpers

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z);

        static Vector3 Bearing(float degrees)
        {
            var d = SightlineStage.Direction(degrees);
            return new Vector3(d.x, 0f, d.y);
        }

        Transform Bar(string name, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.layer = Layers.Effects;
            Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = m;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            go.transform.SetParent(transform, false);
            return go.transform;
        }

        Transform Ball(string name, Material m)
        {
            var t = Bar(name, m);
            var mf = t.GetComponent<MeshFilter>();
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mf.sharedMesh = sphere.GetComponent<MeshFilter>().sharedMesh;
            Destroy(sphere);
            return t;
        }

        static void PlaceBar(Transform bar, Vector3 from, Vector3 to, float thickness)
        {
            Vector3 d = to - from;
            bar.position = (from + to) * 0.5f;
            bar.rotation = Quaternion.FromToRotation(Vector3.up, d.normalized);
            bar.localScale = new Vector3(thickness, d.magnitude * 0.5f, thickness);
        }
    }
}
