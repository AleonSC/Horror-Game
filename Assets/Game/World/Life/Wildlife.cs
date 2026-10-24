using System.Collections.Generic;
using Game.Audio;
using Game.Core;
using Game.Player;
using Game.Rendering;
using Game.World.Build;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// The animals round you (section 6.13), recycled as you move: a herd of deer grazing that looks up when you're
    /// near and bolts when you're close (one spooked sets the rest off), beetles crawling and rats that flee, bats
    /// crossing overhead and crows bursting up from the trees every so often, and moths and fireflies at the lit
    /// lamps.
    /// </summary>
    public sealed class Wildlife : MonoBehaviour
    {
        ForestLifeConfig config;
        PlayerController player;

        // ------------------------------------------------------------------ deer
        sealed class Deer { public Transform Body, Head; public Transform[] Legs = new Transform[4]; public Vector3 Goal; public float Phase; public Heat.Source Heat; public Transform[] Eyes = new Transform[2]; public Renderer[] Shine = new Renderer[2]; }
        readonly List<Deer> herd = new List<Deer>();
        Vector3 herdCentre, fleeDir;
        float herdFleeing, herdAway;
        bool herdPlaced;

        // ------------------------------------------------------------------ small things
        sealed class Critter { public Transform Body; public Vector3 Goal; public float Speed, Wait; public bool Rat, Fleeing; }
        readonly List<Critter> critters = new List<Critter>();

        // ------------------------------------------------------------------ flocks
        sealed class Bird { public Transform Body, WingL, WingR; public Vector3 Velocity; public float Phase; }
        readonly List<Bird> flock = new List<Bird>();
        float nextBats, nextCrows, flockLife;

        // ------------------------------------------------------------------ at the lamps
        Mesh mote;
        Material mothMat, flyMat;
        readonly Matrix4x4[] motes = new Matrix4x4[InstancedDraw.MaxPerCall];

        public ForestLifeSettings Settings => config.settings;
        /// <summary>Where the deer are grazing (and whether they're there to see).</summary>
        public Vector3 HerdCentre => herdCentre;
        public bool HerdOut => herdPlaced && herdAway <= 0f && herdFleeing <= 0f && herd.Count > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (!Services.TryGet<PlayerController>(out _) || !Services.TryGet<WorldBuilder>(out _)) return;
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponentInChildren<Wildlife>() == null)
                new GameObject("Wildlife").AddComponent<Wildlife>().transform.SetParent(root.transform, false);
        }

        void Awake()
        {
            config = Services.Get<GameSetup>().Get<ForestLifeConfig>();
            if (config == null) config = ScriptableObject.CreateInstance<ForestLifeConfig>();
            var sphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            mote = sphere.GetComponent<MeshFilter>().sharedMesh;
            Destroy(sphere);
            mothMat = RuntimeMaterials.Lit("Moth", new Color(0.55f, 0.5f, 0.42f), 0.2f);
            mothMat.enableInstancing = true;
            flyMat = RuntimeMaterials.Glow("Firefly", new Color(0.7f, 1f, 0.3f) * 6f);
            flyMat.enableInstancing = true;
            var s = config.settings;
            nextBats = Time.time + Random.Range(s.minBats, s.maxBats);
            nextCrows = Time.time + Random.Range(s.minCrows, s.maxCrows);
            Services.Register(this);
        }

        void OnDestroy()
        {
            Services.Unregister(this);
            Destroy(mothMat);
            Destroy(flyMat);
        }

        void Update()
        {
            if (player == null && !Services.TryGet(out player)) return;
            if (PlayerPlace.InDarkRoom) return;
            float dt = Time.deltaTime;
            Herd(dt);
            Critters(dt);
            Flocks(dt);
            LampLife();
        }

        Vector3 P => player.transform.position;

        static Vector3 OnGround(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 30f, Vector3.down, out var hit, 80f, Layers.Mask(Layers.Ground), QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
            return p;
        }

        static Vector3 Around(Vector3 centre, float min, float max)
        {
            float a = Random.Range(0f, Mathf.PI * 2f), r = Random.Range(min, max);
            return OnGround(centre + new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r));
        }

        // ------------------------------------------------------------------ deer

        void Herd(float dt)
        {
            var s = config.settings;
            if (herd.Count != s.deer)
            {
                foreach (var d in herd) { Heat.Remove(d.Heat); Destroy(d.Body.gameObject); }
                herd.Clear();
                for (int i = 0; i < s.deer; i++)
                {
                    var body = AnimalModels.Deer(transform, i == 0).transform;
                    var deer = new Deer { Body = body, Head = body.Find("Head"), Phase = Random.value * 10f, Heat = Heat.Add(body, 1.4f, 0.45f) };
                    for (int l = 0; l < 4; l++) deer.Legs[l] = body.Find($"Leg{l}");
                    // Eyeshine: their eyes throw the flashlight back, pale green.
                    for (int e = 0; e < 2; e++)
                    {
                        deer.Eyes[e] = deer.Head != null ? deer.Head.Find(e == 0 ? "EyeL" : "EyeR") : null;
                        if (deer.Eyes[e] != null) deer.Shine[e] = Effects.Glow(deer.Eyes[e].parent, deer.Eyes[e].localPosition + Vector3.forward * 0.02f, 0.14f, Color.black, 0.35f, 3.5f, "Eyeshine");
                    }
                    herd.Add(deer);
                }
                herdPlaced = false;
            }
            if (herd.Count == 0) return;
            if (herdAway > 0f) { herdAway -= dt; if (herdAway <= 0f) herdPlaced = false; else return; }

            if (!herdPlaced || (herdCentre - P).magnitude > 110f)
            {
                // Somewhere you could see them: near enough for the fog, with a clear view, often ahead of you.
                herdCentre = HerdSpot(s);
                foreach (var d in herd)
                {
                    d.Body.gameObject.SetActive(true);
                    d.Body.position = Around(herdCentre, 0f, 6f);
                    d.Goal = Around(herdCentre, 0f, 7f);
                }
                herdPlaced = true;
                herdFleeing = 0f;
            }

            float nearest = float.MaxValue;
            foreach (var d in herd) nearest = Mathf.Min(nearest, (d.Body.position - P).magnitude);
            // One spooked sets the whole herd off.
            if (herdFleeing <= 0f && (nearest < s.deerBolt || (nearest < s.deerBolt * 1.6f && player.Running)))
            {
                herdFleeing = 6f;
                fleeDir = herdCentre - P;
                fleeDir.y = 0f;
                fleeDir = fleeDir.sqrMagnitude > 0.01f ? fleeDir.normalized : Vector3.forward;
                if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(Sound.Hooves, herdCentre, MixerNames.Outside, 0.9f, Random.Range(0.9f, 1.1f), 60f);
                if (Services.TryGet<EventBus>(out var events)) events.Publish(new NoiseEmitted(herdCentre, 30f, "deer bolting"));
            }

            foreach (var d in herd)
            {
                Vector3 pos = d.Body.position;
                float speed;
                bool alert = nearest < s.deerNotice;
                if (herdFleeing > 0f)
                {
                    d.Goal = pos + (fleeDir + new Vector3(Mathf.Sin(d.Phase), 0f, Mathf.Cos(d.Phase)) * 0.2f) * 10f;
                    speed = 9f;
                }
                else if (alert) speed = 0f;
                else
                {
                    if ((d.Goal - pos).magnitude < 0.5f) d.Goal = Around(herdCentre, 0f, 8f);
                    speed = 0.35f;
                }
                Vector3 to = d.Goal - pos;
                to.y = 0f;
                if (speed > 0f && to.sqrMagnitude > 0.01f)
                {
                    Vector3 step = to.normalized * speed * dt;
                    d.Body.position = OnGroundCheap(pos + step, pos.y, dt);
                    d.Body.rotation = Quaternion.Slerp(d.Body.rotation, Quaternion.LookRotation(to), 1f - Mathf.Exp(-4f * dt));
                }
                else if (alert) d.Body.rotation = Quaternion.Slerp(d.Body.rotation, Quaternion.LookRotation(Flat(P - pos)), 1f - Mathf.Exp(-1.5f * dt));

                // Grazing: head down. Alert: head up, ears to you. Running: legs a blur.
                d.Phase += dt * (speed > 3f ? 14f : speed > 0f ? 4f : 0f);
                if (d.Head != null) d.Head.localRotation = Quaternion.Slerp(d.Head.localRotation, Quaternion.Euler(alert || herdFleeing > 0f ? -5f : 65f, 0f, 0f), 1f - Mathf.Exp(-4f * dt));
                Eyeshine(d);
                for (int l = 0; l < 4; l++)
                    if (d.Legs[l] != null) d.Legs[l].localRotation = Quaternion.Euler(Mathf.Sin(d.Phase + (l == 0 || l == 3 ? 0f : Mathf.PI)) * (speed > 3f ? 40f : speed > 0f ? 15f : 0f), 0f, 0f);
            }
            if (herdFleeing > 0f)
            {
                herdFleeing -= dt;
                if (herdFleeing <= 0f)
                {
                    foreach (var d in herd) d.Body.gameObject.SetActive(false);
                    herdAway = Random.Range(config.settings.deerReturn * 0.6f, config.settings.deerReturn * 1.4f);
                }
            }
        }

        // A spot for the herd: inside what the fog lets you see (from 12 m), a clear line from your eyes to it, and
        // usually somewhere ahead. Failing that, anywhere in range.
        Vector3 HerdSpot(ForestLifeSettings s)
        {
            float freePath = Services.TryGet<LookController>(out var look) ? LookController.MeanFreePath(look.Config.settings.fogDensity) : 14f;
            float far = Mathf.Clamp(freePath * 2.2f, s.deerBolt + 4f, 40f);
            float near = Mathf.Max(s.deerBolt + 1f, far * 0.6f);
            Vector3 eye = player.Head != null ? player.Head.position : P + Vector3.up * 1.6f;
            Vector3 ahead = player.Head != null ? Flat(player.Head.forward) : Vector3.forward;
            int mask = Layers.Mask(Layers.Trees) | Layers.Mask(Layers.Props) | Layers.Mask(Layers.Ground);
            for (int i = 0; i < 24; i++)
            {
                float yaw = i < 16 ? Random.Range(-75f, 75f) : Random.Range(0f, 360f);
                Vector3 dir = Quaternion.Euler(0f, yaw, 0f) * (ahead.sqrMagnitude > 0.01f ? ahead.normalized : Vector3.forward);
                Vector3 spot = OnGround(P + dir * Random.Range(near, far));
                Vector3 to = spot + Vector3.up * 1.2f - eye;
                if (!Physics.Raycast(eye, to.normalized, to.magnitude - 1f, mask, QueryTriggerInteraction.Ignore)) return spot;
            }
            return Around(P, near, far);
        }

        // Eyeshine: bright where the flashlight's beam meets their eyes, nothing otherwise.
        void Eyeshine(Deer d)
        {
            var light = player.Flashlight;
            for (int e = 0; e < 2; e++)
            {
                var shine = d.Shine[e];
                if (shine == null) continue;
                float k = 0f;
                if (light != null && light.IsOn && d.Eyes[e] != null)
                {
                    var beam = light.Beam;
                    Vector3 to = d.Eyes[e].position - beam.position;
                    float along = Vector3.Dot(beam.forward, to.normalized);
                    float facing = Vector3.Dot(d.Head.forward, -to.normalized); // (it has to be looking your way)
                    k = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.93f, 0.995f, along)) * Mathf.Clamp01(facing * 2f) * Mathf.Clamp01(1.4f - to.magnitude / 45f);
                }
                Effects.SetGlow(shine, new Color(0.75f, 1f, 0.7f) * (config.settings.deerEyeshine * k));
            }
        }

        // The ground under a moving animal, rechecked every so often rather than every frame.
        static Vector3 OnGroundCheap(Vector3 p, float lastY, float dt)
        {
            if (Time.frameCount % 4 != 0) { p.y = lastY; return p; }
            var g = OnGround(p);
            p.y = Mathf.Lerp(lastY, g.y, 0.8f);
            return p;
        }

        static Vector3 Flat(Vector3 v) => new Vector3(v.x, 0f, v.z + (v.x == 0f && v.z == 0f ? 0.001f : 0f));

        // ------------------------------------------------------------------ beetles and rats

        void Critters(float dt)
        {
            var s = config.settings;
            int want = s.beetles + s.rats;
            while (critters.Count < want)
            {
                bool rat = critters.Count >= s.beetles;
                var c = new Critter { Rat = rat, Body = (rat ? AnimalModels.Rat(transform) : AnimalModels.Beetle(transform)).transform };
                Place(c);
                critters.Add(c);
            }
            foreach (var c in critters)
            {
                Vector3 pos = c.Body.position;
                float d = (pos - P).magnitude;
                if (d > 28f) { Place(c); continue; }
                if (c.Rat && !c.Fleeing && d < s.ratFlee)
                {
                    c.Fleeing = true;
                    c.Goal = OnGround(pos + Flat(pos - P).normalized * 15f);
                    c.Speed = 3f;
                    if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(Sound.Squeak, pos, MixerNames.Outside, 0.3f, Random.Range(0.4f, 0.6f), 10f);
                }
                if (c.Wait > 0f) { c.Wait -= dt; continue; }
                Vector3 to = c.Goal - pos;
                to.y = 0f;
                if (to.magnitude < 0.1f)
                {
                    if (c.Fleeing) { Place(c); continue; }
                    c.Goal = OnGround(pos + new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)) * (c.Rat ? 3f : 0.6f));
                    c.Wait = Random.Range(0.2f, c.Rat ? 2f : 4f);
                    continue;
                }
                Vector3 step = to.normalized * c.Speed * dt;
                c.Body.position = OnGroundCheap(pos + step, pos.y, dt);
                c.Body.rotation = Quaternion.LookRotation(to);
            }
        }

        void Place(Critter c)
        {
            c.Body.position = Around(P, c.Rat ? 8f : 4f, 24f);
            c.Goal = c.Body.position;
            c.Speed = c.Rat ? 1.2f : 0.08f;
            c.Fleeing = false;
            c.Wait = Random.Range(0f, 2f);
        }

        // ------------------------------------------------------------------ bats and crows

        void Flocks(float dt)
        {
            var s = config.settings;
            if (flock.Count == 0)
            {
                if (Time.time >= nextBats) { nextBats = Time.time + Random.Range(s.minBats, s.maxBats); StartFlock(false); }
                else if (Time.time >= nextCrows) { nextCrows = Time.time + Random.Range(s.minCrows, s.maxCrows); StartFlock(true); }
                return;
            }
            flockLife -= dt;
            foreach (var b in flock)
            {
                b.Phase += dt * 18f;
                b.Velocity += new Vector3(Random.Range(-1f, 1f), Random.Range(-0.4f, 0.6f), Random.Range(-1f, 1f)) * dt * 6f;
                b.Body.position += b.Velocity * dt;
                if (b.Velocity.sqrMagnitude > 0.01f) b.Body.rotation = Quaternion.LookRotation(b.Velocity);
                float flap = Mathf.Sin(b.Phase) * 50f;
                if (b.WingL != null) b.WingL.localRotation = Quaternion.Euler(0f, 0f, flap);
                if (b.WingR != null) b.WingR.localRotation = Quaternion.Euler(0f, 0f, -flap);
            }
            if (Random.value < dt * 2f && Services.TryGet<SoundPlayer>(out var sounds))
            {
                var b = flock[Random.Range(0, flock.Count)];
                bool crow = b.Body.name == "Crow";
                sounds.Play(crow ? Sound.Caw : Sound.Squeak, b.Body.position, MixerNames.Outside, crow ? 0.6f : 0.35f, Random.Range(0.85f, 1.15f), 60f);
            }
            if (flockLife <= 0f)
            {
                foreach (var b in flock) Destroy(b.Body.gameObject);
                flock.Clear();
            }
        }

        // Bats sweep across overhead from one side to the other; crows burst up out of the trees and away.
        void StartFlock(bool crows)
        {
            int n = crows ? Random.Range(6, 11) : Random.Range(8, 15);
            Vector3 side = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            Vector3 from = crows ? Around(P, 25f, 40f) + Vector3.up * 6f : P - side * 45f + Vector3.up * Random.Range(12f, 20f);
            Vector3 heading = crows ? (Flat(from - P).normalized + Vector3.up * 0.6f) * 7f : side * 10f;
            for (int i = 0; i < n; i++)
            {
                var body = AnimalModels.Flier(transform, crows).transform;
                body.position = from + Random.insideUnitSphere * (crows ? 3f : 5f);
                flock.Add(new Bird { Body = body, WingL = body.Find("WingL"), WingR = body.Find("WingR"), Velocity = heading * Random.Range(0.85f, 1.15f), Phase = Random.value * 10f });
            }
            flockLife = crows ? 8f : 10f;
            if (crows && Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(Sound.Rustle, from, MixerNames.Outside, 0.9f, 0.6f, 60f);
        }

        // ------------------------------------------------------------------ moths and fireflies

        void LampLife()
        {
            var s = config.settings;
            var cam = Camera.main;
            if (cam == null) return;
            Vector3 c = cam.transform.position;
            float t = Time.time;
            int moths = 0, flies = 0;
            foreach (var lamp in Lamp.All)
            {
                if (lamp == null || !lamp.Lit || (lamp.transform.position - c).sqrMagnitude > 45f * 45f) continue;
                Vector3 light = lamp.LightPoint;
                for (int i = 0; i < s.mothsPerLamp && moths < motes.Length; i++)
                {
                    float a = t * (2.5f + i * 0.37f) + i * 1.7f;
                    float r = 0.25f + 0.35f * Mathf.Abs(Mathf.Sin(t * 0.7f + i));
                    Vector3 p = light + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a * 1.3f) * 0.25f - 0.1f, Mathf.Sin(a) * r);
                    motes[moths++] = Matrix4x4.TRS(p, Quaternion.identity, Vector3.one * 0.025f);
                }
            }
            if (moths > 0) InstancedDraw.Draw(mote, 0, mothMat, motes, moths, new Bounds(c, Vector3.one * 100f), Layers.Effects, false);
            foreach (var lamp in Lamp.All)
            {
                if (lamp == null || !lamp.Lit || (lamp.transform.position - c).sqrMagnitude > 45f * 45f) continue;
                Vector3 at = lamp.transform.position;
                for (int i = 0; i < s.firefliesPerLamp && flies < motes.Length; i++)
                {
                    float a = i * 2.39996f;
                    float r = 2f + (i % 5) * 1.1f;
                    Vector3 p = at + new Vector3(Mathf.Cos(a + t * 0.07f) * r + Mathf.Sin(t * 0.5f + i) * 0.6f, 0.6f + 1.2f * Mathf.PerlinNoise(i * 0.7f, t * 0.15f), Mathf.Sin(a + t * 0.07f) * r);
                    // Each one blinks on and off slowly.
                    float blink = Mathf.Clamp01(Mathf.Sin(t * 1.3f + i * 2.1f) * 2f);
                    if (blink < 0.05f) continue;
                    motes[flies++] = Matrix4x4.TRS(p, Quaternion.identity, Vector3.one * 0.03f * blink);
                }
            }
            if (flies > 0) InstancedDraw.Draw(mote, 0, flyMat, motes, flies, new Bounds(c, Vector3.one * 100f), Layers.Effects, false);
        }
    }
}
