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
    /// you, stopping now and then to scan; but it never reaches you, only ever seeming to come on. At the
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
        Material chainMat, eyeMat, pupilMat, skyEyeMat;
        DustRing shockDust, landingDust;
        float landedAt = -1f;
        Vector2 look, lookTo;
        float glanceIn;
        // The chain snapping: how long since (-1: not), where it broke and where its top is (from the player), and
        // the links flung loose.
        float snapT = -1f, snapLength;
        Vector3 snapBreak, snapTop, snapAxis;
        readonly List<Transform> looseLinks = new List<Transform>();
        readonly List<Vector3> looseAt = new List<Vector3>(), looseVel = new List<Vector3>(), looseSpin = new List<Vector3>();
        MeshRenderer snapFlash;
        MaterialPropertyBlock looseBlock;
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
        bool eyeDone, searching, hiddenForRoom;
        int lastStep;
        float walkIn;

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
            // The chains fade out into the sky as they climb (or, without that shader, plain silhouettes).
            var skyFade = Effects.GameShader("Game/SkyFade");
            chainMat = skyFade != null ? new Material(skyFade) { name = "Giant chain" } : RuntimeMaterials.Silhouette("Giant chain");
            chain = MakeChain("Chain", false);
            BuildScan();
            // The eye: a living eye in a swirl of cloud (its own shader), or failing that a plain red one.
            var eyeShader = Effects.GameShader("Game/SkyEye");
            if (eyeShader != null)
            {
                skyEyeMat = new Material(eyeShader) { name = "The eye" };
                eye = Bar("The eye", skyEyeMat);
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                eye.GetComponent<MeshFilter>().sharedMesh = quad.GetComponent<MeshFilter>().sharedMesh;
                Destroy(quad);
            }
            else
            {
                eyeMat = RuntimeMaterials.Haze("The eye", Color.red);
                pupilMat = RuntimeMaterials.Haze("The eye's pupil", Color.black);
                RuntimeMaterials.DrawAfter(eyeMat, 1);  // (over the body, which darkens what's behind it)
                RuntimeMaterials.DrawAfter(pupilMat, 2);
                eye = Ball("The eye", eyeMat);
                pupil = Ball("Pupil", pupilMat);
                pupil.SetParent(eye, false);
                pupil.localPosition = new Vector3(0f, 0f, -0.45f);
                pupil.localScale = new Vector3(0.28f, 0.9f, 0.2f);
            }
            eye.gameObject.SetActive(false);
            shockDust = DustRing.Create(transform, "Shockwave dust", 60f);
            landingDust = DustRing.Create(transform, "Landing dust", 9f);
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
                // The intro's last line said, a moment, and it groans, far off, from where it hangs.
                subs.Add(events.Subscribe<IntroFinished>(_ => StartCoroutine(GroanSoon())));
            }
            Threats.Add(this);
            Rehang();
        }

        void OnDestroy()
        {
            foreach (var sub in subs) sub.Dispose();
            Threats.Remove(this);
            Services.Unregister(this);
            foreach (var m in new[] { chainMat, eyeMat, pupilMat, skyEyeMat }) if (m != null) Destroy(m);
            SetWorldRed(0f);
        }

        System.Collections.IEnumerator GroanSoon()
        {
            yield return new WaitForSeconds(Random.Range(1.5f, 3f));
            if (!Services.TryGet<PlayerController>(out var p) || !Services.TryGet<SoundPlayer>(out var sounds)) yield break;
            Vector3 to = ShownAt - p.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude < 1f) to = Vector3.forward;
            Vector3 from = p.transform.position + to.normalized * 160f + Vector3.up * 60f;
            sounds.Play(Sound.GiantGroan, from, MixerNames.Outside, 1f, Random.Range(0.92f, 1f), 700f);
            p.AddShake(0.35f);
        }

        // ------------------------------------------------------------------ the run

        void Rehang()
        {
            if (State != GiantState.Hanging && Services.TryGet<EventBus>(out var events)) events.Publish(new BossThemeEnded(EntityKind.Giant));
            State = GiantState.Hanging;
            bearing = Services.TryGet<WorldBuilder>(out var world) && world.Plan != null ? world.Plan.GiantBearing : 0f;
            eyeDone = searching = false;
            eyesOpen = 0f; // (shut again, hanging)
            timer = 0f;
            swing = swingSpeed = 0f;
            shockRadius = -1f;
            landedAt = -1f;
            EndSnap();
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
                emptyChains.Add(MakeChain("Empty chain", true));
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
            if (hiddenForRoom) { if (scanRoot != null) scanRoot.gameObject.SetActive(false); return; }
            float dt = Time.deltaTime;
            // Its eye open in the sky: the far distance comes pin sharp (no softening it away), clearing over a
            // couple of seconds as it opens, and back to the player's own blur after.
            if (Services.TryGet<LookController>(out var sharpen))
            {
                // The far blur holds while it stares, and lifts the moment the chain snaps (quickly, so you see it
                // fall and land); once it's up and walking it comes back, slowly.
                bool clear = State == GiantState.Falling || State == GiantState.Landed;
                sharpen.FarSharp = Mathf.MoveTowards(sharpen.FarSharp, clear ? 1f : 0f, dt / (clear ? 0.4f : 4f));
            }
            Vector3 p = PlayerFeet;

            // Swinging on the chain (jolts), damped.
            swingSpeed += -swing * 0.6f * dt;
            swingSpeed *= Mathf.Exp(-0.25f * dt);
            swing += swingSpeed * dt;

            switch (State)
            {
                case GiantState.Hanging: Hang(p); break;
                case GiantState.Staring: Hang(p); Stare(dt); break;
                case GiantState.Falling: Fall(dt); break;
                case GiantState.Landed: Landed(dt, p); break;
                case GiantState.Walking: Walk(dt, p); break;
                case GiantState.Resting:
                    timer -= dt;
                    walkIn = 0f; // (it'll ease back into its stride)
                    body.PoseWalking(stride, Mathf.Sin(Time.time * 0.3f), 0f);
                    if (timer <= 0f) State = GiantState.Walking;
                    break;
            }

            Shockwave(dt, p);
            Draw(p);
            Scan(dt, p);
            UpdateSearchlights(dt, p);
            UpdateCrowd(dt, p);
        }

        Vector3 PlayerFeet => player.transform.position;

        void Hang(Vector3 p)
        {
            truePos = p + Bearing(bearing) * s.HangDistance;
            footHeight = s.HangDistance * Mathf.Tan(s.feetElevation * Mathf.Deg2Rad);
            body.PoseHanging(Mathf.Sin(Time.time * 0.37f) * 0.6f + swing * 0.05f);
            // The moon hangs behind it, at its chest, its rays streaming out round it (and the moonlight comes from there).
            if (s.moonBehind)
            {
                float el = Mathf.Lerp(s.feetElevation, s.neckElevation, s.moonAt) * Mathf.Deg2Rad;
                NightSky.MoonOverride = Bearing(bearing) * Mathf.Cos(el) + Vector3.up * Mathf.Sin(el);
            }
            else NightSky.MoonOverride = null;
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
            if (skyEyeMat != null)
            {
                // The clouds gather and start to turn before it opens, and drift apart after it shuts.
                eye.localScale = Vector3.one * (EyeSize / 0.36f);
                float cloud = timer < open + stare ? Mathf.SmoothStep(0f, 1f, timer / (open * 0.8f)) : Mathf.Sqrt(lid);
                skyEyeMat.SetFloat("_Open", lid);
                skyEyeMat.SetFloat("_Cloud", cloud);
                skyEyeMat.SetFloat("_Spin", s.eyeSwirl);
                // Its pupil: wide as it opens, narrowing on you; then small darting looks, never far off you.
                glanceIn -= dt;
                if (glanceIn <= 0f)
                {
                    glanceIn = Random.Range(0.5f, 2.2f);
                    lookTo = Random.insideUnitCircle * 0.1f;
                }
                look = Vector2.Lerp(look, lookTo, 1f - Mathf.Exp(-22f * dt));
                skyEyeMat.SetVector("_Look", new Vector4(look.x, look.y, 0f, 0f));
                float narrow = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((timer - open * 0.5f) / 2.5f));
                skyEyeMat.SetFloat("_Pupil", Mathf.Lerp(0.62f, 0.34f, narrow) + Mathf.Sin(Time.time * 0.7f) * 0.025f);
            }
            else eye.localScale = new Vector3(EyeSize, EyeSize * Mathf.Max(0.02f, 0.55f * lid), EyeSize * 0.35f);

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
                // The chain snaps, and it falls.
                StartSnap();
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

        // ------------------------------------------------------------------ the chain snapping

        float snapThickness = 1f;
        const float SnapTime = 4f;

        // A crack like a gunshot the size of the sky, a flash where it parts; the chain whips back up, lashing
        // from side to side, and is hauled away up into the dark; a handful of links flung loose come tumbling
        // down after the giant, spinning.
        void StartSnap()
        {
            Vector3 p = PlayerFeet;
            Vector3 neck = body.ChainAnchor != null ? body.ChainAnchor.position : bodyRoot.TransformPoint(body.NeckLocal);
            Vector3 top = ChainTop(p, neck, Vector3.Distance(p, neck) * 1.5f);
            snapBreak = neck + Vector3.up * snapThickness * 1.2f - p;
            snapTop = top - p;
            snapLength = Mathf.Max(1f, (top - neck).magnitude);
            Vector3 toward = Flat(neck - p);
            snapAxis = Vector3.Cross(Vector3.up, toward.sqrMagnitude > 0.01f ? toward.normalized : Vector3.forward);
            snapT = 0f;

            if (Services.TryGet<SoundPlayer>(out var sounds))
            {
                sounds.Play2D(Sound.ChainSnap, MixerNames.Outside, 1f, Random.Range(0.95f, 1.02f));
                sounds.Play2D(Sound.Thunder, MixerNames.Outside, 0.7f, 0.7f);
            }
            player.AddShake(3f);
            player.AddRumble(0.6f, 1.5f);

            // The flash where it parts.
            if (snapFlash == null)
            {
                snapFlash = Effects.Glow(transform, Vector3.zero, 1f, Color.black, 0f, 2f, "Chain snap flash");
            }
            if (snapFlash != null)
            {
                snapFlash.gameObject.SetActive(true);
                snapFlash.transform.position = p + snapBreak;
                snapFlash.transform.localScale = Vector3.one * snapThickness * 40f;
            }

            // The loose links.
            for (int i = 0; i < 9; i++)
            {
                Transform l;
                if (i < looseLinks.Count) l = looseLinks[i];
                else
                {
                    l = ChainPart(transform, "Loose link", ChainMeshes.Link);
                    looseLinks.Add(l);
                    looseAt.Add(Vector3.zero); looseVel.Add(Vector3.zero); looseSpin.Add(Vector3.zero);
                }
                l.gameObject.SetActive(true);
                looseBlock ??= new MaterialPropertyBlock();
                looseBlock.SetFloat(FadeStartId, 1e5f); // (no fading into the sky for these: they're falling)
                looseBlock.SetFloat(FadeEndId, 2e5f);
                l.GetComponent<MeshRenderer>().SetPropertyBlock(looseBlock);
                l.localScale = Vector3.one * snapThickness * 1.6f;
                l.rotation = Random.rotation;
                looseAt[i] = snapBreak + Random.insideUnitSphere * snapThickness * 2f;
                Vector3 fling = Random.insideUnitSphere;
                fling.y = Mathf.Abs(fling.y) * 0.8f + 0.2f;
                looseVel[i] = fling * snapThickness * Random.Range(6f, 16f);
                looseSpin[i] = Random.insideUnitSphere * Random.Range(180f, 720f);
            }
        }

        void UpdateSnap(Vector3 p)
        {
            float dt = Time.deltaTime;
            snapT += dt;
            float t = snapT;
            // Recoiling up, fast and then slower, and hauled on up out of sight.
            float rise = snapLength * (0.28f * (1f - Mathf.Exp(-4f * t)) + 0.11f * t * t);
            Vector3 top = p + snapTop;
            Vector3 bottom = p + snapBreak + Vector3.up * rise;
            if (rise >= snapLength * 0.97f || t >= SnapTime)
            {
                EndSnap();
                return;
            }
            PlaceChain(chain, bottom, top, snapThickness);
            // Lashing from side to side as it goes, dying away (and a little toward and away from you).
            float lash = 22f * Mathf.Exp(-1.1f * t) * Mathf.Sin(7f * t + 0.4f);
            float sway = 9f * Mathf.Exp(-0.9f * t) * Mathf.Sin(4.3f * t + 1.1f);
            chain.RotateAround(top, Vector3.Cross(snapAxis, Vector3.up), lash);
            chain.RotateAround(top, snapAxis, sway);

            if (snapFlash != null && snapFlash.gameObject.activeSelf)
            {
                float f = Mathf.Clamp01(1f - t / 0.5f);
                Effects.SetGlow(snapFlash, new Color(1f, 0.75f, 0.45f) * 40f * f * f);
                if (f <= 0f) snapFlash.gameObject.SetActive(false);
            }

            // The loose links falling (about as fast as the giant falls, at the scale it's drawn), tumbling.
            float g = snapThickness * 9f;
            for (int i = 0; i < looseLinks.Count; i++)
            {
                var l = looseLinks[i];
                if (!l.gameObject.activeSelf) continue;
                looseVel[i] += Vector3.down * g * dt;
                looseAt[i] += looseVel[i] * dt;
                l.position = p + looseAt[i];
                l.Rotate(looseSpin[i] * dt, Space.Self);
                if (l.position.y < p.y - 5f) l.gameObject.SetActive(false);
            }
        }

        void EndSnap()
        {
            snapT = -1f;
            if (chain != null && State != GiantState.Hanging && State != GiantState.Staring) chain.gameObject.SetActive(false);
            if (snapFlash != null) snapFlash.gameObject.SetActive(false);
            foreach (var l in looseLinks) if (l != null) l.gameObject.SetActive(false);
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
            landedAt = Time.time;
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play2D(Sound.Boom, MixerNames.Outside, 1f, 0.8f);
            player.AddRumble(0.8f, 2.5f); // (the first of it, through the ground; the wave itself follows)
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
            // It hits the ground in a crouch, sinks a little further as it takes the weight, holds there a moment,
            // then slowly rises to its full height.
            float rise = s.shockwaveAfter + 2f;
            float sink = Mathf.Sin(Mathf.Clamp01(timer / 0.7f) * Mathf.PI) * 0.18f;
            float up = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((timer - 1.2f) / Mathf.Max(0.5f, rise - 1.2f)));
            body.PoseLanding((1f + sink) * (1f - up));
            if (shockRadius < 0f && timer >= s.shockwaveAfter)
            {
                shockRadius = 0f;
                // It whips the trees and grass over as it rolls out.
                if (Services.TryGet<Wind>(out var wind)) wind.AddShock(truePos, Time.time, s.shockwaveSpeed);
            }
            if (timer >= s.shockwaveAfter + 2f)
            {
                State = GiantState.Walking;
                walkIn = 0f;
                nextScan = Random.Range(s.minScanEvery, s.maxScanEvery);
            }
        }

        // It never reaches you: it only ever seems to be coming. It keeps out near the horizon where it landed (it
        // gains only a little, slowing as it does), still striding as if it were coming on, and if you run at it,
        // it stays just as far off.
        void Walk(float dt, Vector3 p)
        {
            Vector3 to = Flat(p - truePos);
            Distance = to.magnitude;
            Vector3 toward = Distance > 0.01f ? to / Distance : Vector3.forward;
            float closest = s.ClosestDistance;
            if (Distance < closest)
            {
                truePos = Vector3.MoveTowards(truePos, p - toward * closest, s.walkSpeed * 3f * dt);
                Distance = Flat(p - truePos).magnitude;
            }
            // Easing into its stride (from standing, or from stopping to scan) rather than snapping.
            walkIn = Mathf.MoveTowards(walkIn, scanLeft > 0f ? 0f : 1f, dt / (scanLeft > 0f ? 1.5f : 3f));
            float moving = Mathf.SmoothStep(0f, 1f, walkIn);
            if (scanLeft > 0f)
            {
                scanLeft -= dt;
                scan = Mathf.Lerp(scan, Mathf.Sin(Time.time * 0.8f), 1f - Mathf.Exp(-2f * dt));
                stride += dt / Mathf.Max(0.5f, s.strideTime) * 0.3f * moving; // (its last step settling)
                body.PoseWalking(stride, scan, moving);
            }
            else
            {
                scan = Mathf.MoveTowards(scan, 0f, dt);
                stride += dt / Mathf.Max(0.5f, s.strideTime) * Mathf.Lerp(0.3f, 1f, moving);
                float approach = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Distance - closest) / Mathf.Max(1f, s.HangDistance - closest)));
                truePos += toward * Mathf.Min(Mathf.Max(0f, Distance - closest), s.walkSpeed * approach * moving * dt);
                body.PoseWalking(stride, scan, moving);
                nextScan -= dt;
                if (nextScan <= 0f)
                {
                    nextScan = Random.Range(s.minScanEvery, s.maxScanEvery);
                    scanLeft = Random.Range(s.minScanTime, s.maxScanTime);
                }
                // A step every half stride, as each foot comes down (a quarter of the way into each half): a thud,
                // and the ground heaving under you, growing as it nears.
                int step = Mathf.FloorToInt(stride * 2f + 0.5f);
                if (step != lastStep)
                {
                    lastStep = step;
                    float near = 1f - Mathf.Clamp01((Distance - closest) / Mathf.Max(1f, s.HangDistance - closest));
                    if (Services.TryGet<SoundPlayer>(out var sounds))
                    {
                        sounds.Play2D(Sound.GiantStep, MixerNames.Outside, Mathf.Lerp(0.45f, 1f, near) * moving, Random.Range(0.82f, 0.95f));
                        // (and a deep boom under it, swelling as it nears)
                        sounds.Play2D(Sound.Boom, MixerNames.Outside, Mathf.Lerp(0.12f, 0.5f, near) * moving, Random.Range(0.5f, 0.6f));
                    }
                    player.AddShake(s.stepShake * Mathf.Lerp(0.3f, 1f, near) * moving);
                    player.AddRumble(s.stepRumble * Mathf.Lerp(0.35f, 1f, near) * moving, s.stepRumbleTime);
                }
            }
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
                // It hits: the ground heaves, the view's knocked about, a roar of wind and dust, and you're dazed.
                player.AddShake(s.shockwaveRumble * 2f);
                player.AddRumble(s.shockwaveRumble, 3.5f);
                if (s.shockwaveDaze > 0f) player.StartGroggy(s.shockwaveDaze, 0.6f);
                if (Services.TryGet<SoundPlayer>(out var sounds))
                {
                    sounds.Play2D(Sound.Thunder, MixerNames.Outside, 1f, 0.5f);
                    sounds.Play2D(Sound.Boom, MixerNames.Outside, 0.8f, 0.45f);
                    sounds.Play2D(Sound.TreeRush, MixerNames.Outside, 0.7f, 0.6f);
                }
            }
            if (shockRadius > d + 300f) shockRadius = -1f;
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
                body.SetShade(s.solidBlack ? 0f : s.haze, fog, s.solidBlack ? 0f : s.beamThrough);
                RuntimeMaterials.SetSilhouette(chainMat, s.solidBlack ? 0f : s.haze * 0.8f, fog, s.solidBlack ? 0f : s.beamThrough);
                body.SetEyes(eyesOpen, s.ownEyeColor.linear * s.ownEyeGlow / mul);
                if (skyEyeMat != null) skyEyeMat.SetFloat("_Bright", s.eyeGlow);
                else
                {
                    RuntimeMaterials.SetHazeColor(eyeMat, s.eyeColor.linear * 8f * s.eyeGlow / mul);
                    RuntimeMaterials.SetHazeColor(pupilMat, Color.black);
                }
                DrawDust(p, fog, mul);
            }

            if (snapT >= 0f) UpdateSnap(p);
            else if (chain.gameObject.activeSelf)
            {
                Vector3 neck = body.ChainAnchor != null ? body.ChainAnchor.position : bodyRoot.TransformPoint(body.NeckLocal);
                PlaceChain(chain, neck, ChainTop(p, neck, r * 1.5f), s.height * k * 0.008f);
                snapThickness = s.height * k * 0.008f;
            }
            body.ShowBrokenChain(State != GiantState.Hanging && State != GiantState.Staring);
            for (int i = 0; i < emptyChains.Count; i++)
            {
                Vector3 baseDir = Bearing(emptyBearings[i]);
                float rr = RenderDistance;
                float el = (25f + 8f * Mathf.Sin(i * 2.3f)) * Mathf.Deg2Rad;
                Vector3 bottom = p + baseDir * rr * Mathf.Cos(el) + Vector3.up * rr * Mathf.Sin(el);
                bottom += Vector3.Cross(Vector3.up, baseDir) * Mathf.Sin(Time.time * 0.21f + i) * rr * 0.004f;
                PlaceChain(emptyChains[i], bottom, ChainTop(p, bottom, rr * 1.5f), rr * 0.006f);
            }
            if (eye.gameObject.activeSelf)
            {
                eye.position = EyeWorld(p);
                eye.rotation = Quaternion.LookRotation(eye.position - player.Head.position);
            }
        }

        // The dust: a cloud thrown up round its feet as it lands, slowly settling; and the wall of it the shockwave
        // drives out across the forest, passing you and rolling on (thinning as it goes).
        void DrawDust(Vector3 p, Color fog, float mul)
        {
            Color dust = (s.dustColor.linear + fog * 0.5f) / mul;
            if (landingDust != null)
            {
                float since = landedAt < 0f ? -1f : Time.time - landedAt;
                float thick = since < 0f ? 0f : s.landingDust * Mathf.Clamp01(since / 0.6f) * Mathf.Clamp01(1f - (since - 6f) / 14f);
                float grow = since < 0f ? 0f : 1f - Mathf.Exp(-since / 3f);
                float size = bodyRoot.localScale.y; // (its height as drawn)
                landingDust.SetSoftness(4f);
                landingDust.Place(bodyRoot.position + Vector3.down * size * 0.02f, size * Mathf.Lerp(0.08f, 0.45f, grow), size * Mathf.Lerp(0.12f, 0.32f, grow), dust, thick);
            }
            if (shockDust != null)
            {
                float d = Flat(p - truePos).magnitude;
                float thick = shockRadius < 0f ? 0f : s.shockwaveDust * Mathf.Clamp01(shockRadius / 40f) * Mathf.Clamp01(1f - (shockRadius - d) / 300f);
                Vector3 centre = new Vector3(truePos.x, p.y - 8f, truePos.z);
                shockDust.SetSoftness(8f);
                shockDust.Place(centre, Mathf.Max(0f, shockRadius), s.shockwaveHeight * Mathf.Lerp(1.6f, 1f, Mathf.Clamp01(shockRadius / Mathf.Max(1f, d))), dust, thick);
            }
        }

        // ------------------------------------------------------------------ awake: its eyes and its search ray

        Transform scanRoot;
        Light scanLight;
        Renderer scanShaft;
        float eyesOpen;

        void BuildScan()
        {
            scanRoot = new GameObject("Search ray").transform;
            scanRoot.SetParent(transform, false);
            scanRoot.gameObject.AddHDLight(LightType.Spot);
            scanLight = scanRoot.GetComponent<Light>();
            scanLight.shadows = LightShadows.None;
            scanLight.enabled = false;
            scanShaft = Effects.Beam(scanRoot, 1f, 1f, Color.black, 0.2f, 0.3f, "Search ray shaft");
            if (scanShaft != null) scanShaft.sharedMaterial.SetFloat("_Falloff", 0.5f);
            scanRoot.gameObject.SetActive(false);
        }

        // Once it's down and walking, its eyes open and a ray from them sweeps the forest in the distance, back and
        // forth as it walks, round with its head when it stops to scan. (The 10th lamp's searchlights take over.)
        void Scan(float dt, Vector3 p)
        {
            bool awake = (State == GiantState.Landed && timer > s.shockwaveAfter) || State == GiantState.Walking || State == GiantState.Resting;
            eyesOpen = Mathf.MoveTowards(eyesOpen, awake ? 1f : 0f, dt / Mathf.Max(0.1f, s.eyesOpenTime));
            bool on = eyesOpen > 0.01f && !searching && body.HeadJoint != null;
            if (scanRoot.gameObject.activeSelf != on) scanRoot.gameObject.SetActive(on);
            if (!on) return;

            Vector3 from = bodyRoot.TransformPoint(body.EyesLocal);
            Vector3 gaze = body.HeadJoint.forward;
            float yaw = Mathf.Atan2(gaze.x, gaze.z) * Mathf.Rad2Deg;
            float sweep = State == GiantState.Walking && scanLeft <= 0f ? Mathf.Sin(Time.time * s.scanSweepSpeed * Mathf.PI * 2f * 0.25f) * s.scanSweep : 0f;
            Vector3 dir = Quaternion.Euler(s.scanPitch, yaw + sweep, 0f) * Vector3.forward;
            float reach = Physics.Raycast(from, dir, out var hit, 2000f, Layers.Mask(Layers.Ground) | Layers.Mask(Layers.Trees), QueryTriggerInteraction.Ignore) ? hit.distance : Vector3.Distance(from, p) * 1.3f;
            scanRoot.SetPositionAndRotation(from, Quaternion.LookRotation(dir));
            float open = eyesOpen * eyesOpen;
            scanLight.enabled = true;
            scanLight.spotAngle = s.scanAngle;
            scanLight.innerSpotAngle = s.scanAngle * 0.7f;
            scanLight.range = reach + 60f;
            scanLight.color = s.scanColor;
            scanLight.intensity = s.scanIntensity * open;
            if (scanShaft != null)
            {
                float radius = reach * Mathf.Tan(s.scanAngle * 0.5f * Mathf.Deg2Rad);
                scanShaft.transform.localScale = new Vector3(radius, radius, reach);
                Effects.SetGlow(scanShaft, s.scanColor * (s.scanShaft * open));
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
            chain.gameObject.SetActive(State == GiantState.Hanging || State == GiantState.Staring || snapT >= 0f);
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

        // A chain of heavy links (in runs, as many as its length needs), and for an empty one the open shackle
        // hanging from its end.
        Transform MakeChain(string name, bool emptyShackle)
        {
            var root = new GameObject(name) { layer = Layers.Effects }.transform;
            root.SetParent(transform, false);
            if (emptyShackle)
            {
                var shackle = ChainPart(root, "Open shackle", ChainMeshes.OpenShackle);
                shackle.localScale = Vector3.one * 2f; // (it held a neck as big as the giant's)
                shackle.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
            return root;
        }

        Transform ChainPart(Transform parent, string name, Mesh mesh)
        {
            var go = new GameObject(name) { layer = Layers.Effects };
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = chainMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            return go.transform;
        }

        // Lay a chain from its bottom up to its top: its links are <paramref name="thickness"/> x 1.6 wide.
        void PlaceChain(Transform root, Vector3 from, Vector3 to, float thickness)
        {
            Vector3 d = to - from;
            float length = d.magnitude;
            float unit = thickness * 1.6f;
            root.position = from;
            root.rotation = Quaternion.FromToRotation(Vector3.up, length > 1e-3f ? d / length : Vector3.up);
            root.localScale = Vector3.one * unit;
            // Thinning out from partway up into nothing just short of its top.
            fadeBlock ??= new MaterialPropertyBlock();
            fadeBlock.SetFloat(FadeStartId, from.y + d.y * s.chainFadeFrom);
            fadeBlock.SetFloat(FadeEndId, from.y + d.y * s.chainFadeTo);
            int runs = Mathf.CeilToInt(length / Mathf.Max(1e-3f, unit) / ChainMeshes.RunLength);
            int have = 0;
            foreach (Transform c in root)
            {
                if (c.name != "Links") continue;
                bool on = have < runs;
                if (c.gameObject.activeSelf != on) c.gameObject.SetActive(on);
                have++;
            }
            for (; have < runs; have++)
                ChainPart(root, "Links", ChainMeshes.Run).localPosition = Vector3.up * (have * ChainMeshes.RunLength);
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>()) r.SetPropertyBlock(fadeBlock);
        }

        MaterialPropertyBlock fadeBlock;
        static readonly int FadeStartId = Shader.PropertyToID("_FadeStart"), FadeEndId = Shader.PropertyToID("_FadeEnd");

        Transform Ball(string name, Material m)
        {
            var t = Bar(name, m);
            var mf = t.GetComponent<MeshFilter>();
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mf.sharedMesh = sphere.GetComponent<MeshFilter>().sharedMesh;
            Destroy(sphere);
            return t;
        }

    }
}
