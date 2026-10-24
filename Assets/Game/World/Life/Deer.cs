using System.Collections.Generic;
using Game.Audio;
using Game.Core;
using Game.Player;
using Game.Rendering;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// The deer (section 6.13), brought over from the prototype as they were: dark brown deer (the stags with
    /// antlers) in small herds roaming round you. They wander and graze, call now and then (a grunt, a bleat,
    /// a snort), look up and snort when you come near, and gallop off with their white tails up if you get too
    /// close; one spooked sets off the herd round it, and the one you startled drops a battery. Their bodies are
    /// smooth lofted shapes, their legs jointed at the knee and hock, and they walk and gallop with a proper gait.
    /// </summary>
    public sealed class Deer : MonoBehaviour
    {
        const int Graze = 0, Walk = 1, Alert = 2, Flee = 3;
        // Indices into a deer's parts.
        const int FrontLeft = 0, FrontRight = 1, BackLeft = 2, BackRight = 3, Neck = 4, Head = 5, Tail = 6;

        sealed class One
        {
            public Transform root, body;
            public Transform[] parts, knees;
            public AudioSource loop;
            public Heat.Source heat;
            public float neckAngle, speed, timer, phase;
            public Vector3 heading = Vector3.forward, target;
            public int mode;
        }

        readonly List<One> deer = new List<One>();
        ForestLifeSettings s;
        PlayerController player;
        Material coat, bellyMat, whiteMat, hoofMat, antlerMat, eyeMat;
        readonly List<Mesh> meshes = new List<Mesh>();
        float callTimer = -1f;
        bool placed;

        public static Deer Current { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (!Services.TryGet<PlayerController>(out _) || !Services.TryGet<Game.World.Build.WorldBuilder>(out _)) return;
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponentInChildren<Deer>() == null)
                new GameObject("Deer").AddComponent<Deer>().transform.SetParent(root.transform, false);
        }

        /// <summary>A deer that's out, the nearest to a point (for the main menu's shot of them).</summary>
        public bool TryGetDeer(Vector3 near, out Transform found)
        {
            found = null;
            float best = float.MaxValue;
            foreach (var d in deer)
            {
                if (d.root == null || !d.root.gameObject.activeInHierarchy || d.mode == Flee) continue;
                float dist = (d.root.position - near).sqrMagnitude;
                if (dist < best) { best = dist; found = d.root; }
            }
            return found != null;
        }

        void Start()
        {
            Current = this;
            var config = Services.Get<GameSetup>().Get<ForestLifeConfig>();
            s = config != null ? config.settings : new ForestLifeSettings();
            coat = RuntimeMaterials.Lit("Deer coat", new Color(0.06f, 0.045f, 0.032f), 0.18f); // (a dark brown coat)
            bellyMat = RuntimeMaterials.Lit("Deer belly", new Color(0.11f, 0.09f, 0.07f), 0.12f);
            whiteMat = RuntimeMaterials.Lit("Deer white", new Color(0.42f, 0.4f, 0.36f), 0.1f);
            hoofMat = RuntimeMaterials.Lit("Hoof", new Color(0.015f, 0.014f, 0.013f), 0.55f);
            antlerMat = RuntimeMaterials.Lit("Antler", new Color(0.2f, 0.17f, 0.13f), 0.25f);
            // Eyeshine: a pale, flat colour (as it shows on screen, so it's divided by the exposure).
            float mul = Services.TryGet<LookController>(out var look) ? LookController.ExposureMultiplier(look.Config.settings.exposure) : 1f;
            eyeMat = RuntimeMaterials.Unlit("Deer eyes", new Color(0.35f, 0.45f, 0.4f) / mul);
            for (int i = 0; i < s.deer; i++) deer.Add(Build());
        }

        void OnDestroy()
        {
            if (Current == this) Current = null;
            foreach (var d in deer) Heat.Remove(d.heat);
            foreach (var m in meshes) Destroy(m);
            foreach (var m in new[] { coat, bellyMat, whiteMat, hoofMat, antlerMat, eyeMat }) if (m != null) Destroy(m);
        }

        void Update()
        {
            if (player == null && !Services.TryGet(out player)) return;
            bool away = PlayerPlace.InDarkRoom;
            foreach (var d in deer) if (d.root.gameObject.activeSelf == away) d.root.gameObject.SetActive(!away);
            if (away) return;
            if (!placed)
            {
                for (int i = 0; i < deer.Count; i++) Place(i);
                placed = true;
            }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            for (int i = 0; i < deer.Count; i++) UpdateOne(i, dt);
            Calls(dt);
        }

        Vector3 P => player.transform.position;

        // ------------------------------------------------------------------ behaviour

        void Place(int i)
        {
            var d = deer[i];
            // Most deer turn up near another one, so they come in small herds.
            if (i > 0 && Random.value < 0.6f && (deer[i - 1].root.position - P).sqrMagnitude < s.deerRange * s.deerRange)
            {
                Vector2 offset = Random.insideUnitCircle * 6f;
                d.root.position = Ground(deer[i - 1].root.position + new Vector3(offset.x, 0f, offset.y));
            }
            else
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                d.root.position = Ground(P + new Vector3(dir.x, 0f, dir.y) * Random.Range(s.deerRange * 0.5f, s.deerRange));
            }
            d.mode = Graze;
            d.timer = Random.Range(2f, 8f);
            d.heading = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
            d.root.rotation = Quaternion.LookRotation(d.heading);
        }

        // Now and then, a grazing or wandering deer near you calls: a grunt, a bleat, or a snort.
        void Calls(float dt)
        {
            if (callTimer < 0f) callTimer = Random.Range(s.minDeerCall, s.maxDeerCall);
            callTimer -= dt;
            if (callTimer > 0f) return;
            callTimer = Random.Range(s.minDeerCall, Mathf.Max(s.minDeerCall, s.maxDeerCall));
            var calm = new List<One>();
            foreach (var d in deer)
            {
                if (!d.root.gameObject.activeInHierarchy || (d.mode != Graze && d.mode != Walk)) continue;
                if ((d.root.position - P).sqrMagnitude < s.deerCallDistance * s.deerCallDistance) calm.Add(d);
            }
            if (calm.Count == 0) return;
            var caller = calm[Random.Range(0, calm.Count)];
            float roll = Random.value;
            Say(caller, roll < 0.5f ? Sound.DeerGrunt : roll < 0.8f ? Sound.DeerBleat : Sound.DeerSnort);
        }

        void UpdateOne(int i, float dt)
        {
            var d = deer[i];
            Vector3 pos = d.root.position;
            Vector3 away = pos - P;
            away.y = 0f;
            float dist = away.magnitude;

            if (dist > s.deerRange * 1.4f)
            {
                Place(i);
                return;
            }

            if (d.mode != Flee)
            {
                if (dist < s.deerFlee) StartFlee(d, true);
                else if (dist < s.deerAlert && d.mode != Alert)
                {
                    d.mode = Alert;
                    d.timer = Random.Range(1.5f, 3.5f);
                    Say(d, Sound.DeerSnort); // (head up, a sharp snort: it's noticed you)
                }
            }

            float speed = 0f, targetNeck = 35f, turnRate = 4f;
            d.timer -= dt;
            switch (d.mode)
            {
                case Graze:
                    targetNeck = 115f; // (head down to the grass)
                    if (d.timer <= 0f)
                    {
                        d.mode = Walk;
                        Vector3 dir = Quaternion.Euler(0f, Random.Range(-120f, 120f), 0f) * d.heading;
                        d.target = pos + dir * Random.Range(4f, 12f);
                        d.timer = 15f;
                    }
                    break;

                case Walk:
                {
                    Vector3 to = d.target - pos;
                    to.y = 0f;
                    if (to.magnitude < 0.5f || d.timer <= 0f)
                    {
                        d.mode = Graze;
                        d.timer = Random.Range(4f, 10f);
                    }
                    else
                    {
                        d.heading = Steer(pos, to.normalized, 3f);
                        speed = s.deerWalkSpeed;
                    }
                    break;
                }

                case Alert:
                    targetNeck = -5f; // (head up, staring)
                    d.heading = -away.normalized;
                    turnRate = 3f;
                    if (d.timer <= 0f)
                    {
                        if (dist < s.deerAlert * 0.8f) StartFlee(d, true);
                        else
                        {
                            d.mode = Graze; // (lost interest)
                            d.timer = Random.Range(2f, 5f);
                        }
                    }
                    break;

                case Flee:
                    targetNeck = 15f;
                    turnRate = 6f;
                    d.heading = Steer(pos, Vector3.Slerp(d.heading, away.normalized, dt * 0.5f).normalized, 6f);
                    speed = s.deerGallopSpeed;
                    if (d.timer <= 0f && dist > s.deerAlert * 1.5f)
                    {
                        d.mode = Walk;
                        d.target = pos + d.heading * Random.Range(5f, 10f);
                        d.timer = 15f;
                    }
                    break;
            }

            pos += d.heading * (speed * dt);
            d.root.position = Ground(pos);
            if (d.heading.sqrMagnitude > 1e-4f)
                d.root.rotation = Quaternion.Slerp(d.root.rotation, Quaternion.LookRotation(d.heading), 1f - Mathf.Exp(-turnRate * dt));

            if (d.loop != null)
            {
                d.loop.volume = Mathf.MoveTowards(d.loop.volume, d.mode == Flee ? 0.8f : 0f, dt * 2f);
                if (d.loop.volume > 0f && !d.loop.isPlaying) d.loop.Play();
                else if (d.loop.volume <= 0f && d.loop.isPlaying) d.loop.Stop();
            }
            Animate(d, speed, targetNeck, dt);
        }

        void StartFlee(One d, bool startled)
        {
            if (d.mode == Flee) return;
            Vector3 away = d.root.position - P;
            away.y = 0f;
            d.heading = Quaternion.Euler(0f, Random.Range(-30f, 30f), 0f) * away.normalized;
            d.mode = Flee;
            d.timer = Random.Range(5f, 8f);
            Say(d, Sound.DeerAlarm);
            if (!startled) return;

            if (Services.TryGet<EventBus>(out var events)) events.Publish(new NoiseEmitted(d.root.position, 30f, "deer bolting"));
            // The one you startled drops a battery as it bolts.
            if (s.deerDropBattery) Pickup.Create(ItemKind.Batteries, 1, Ground(d.root.position) + Vector3.up * 0.02f, Random.Range(0f, 360f), transform.parent, glowing: true);
            // One spooked deer sends the rest of the herd running.
            foreach (var other in deer)
                if (other != d && (other.root.position - d.root.position).sqrMagnitude < 25f * 25f)
                    StartFlee(other, false);
        }

        // Turns away from trees ahead, so they don't run straight through trunks.
        static Vector3 Steer(Vector3 pos, Vector3 heading, float lookAhead)
        {
            Vector3 origin = pos + Vector3.up * 1f;
            int mask = Layers.Mask(Layers.Trees) | Layers.Mask(Layers.Props);
            for (int attempt = 0; attempt < 4; attempt++)
            {
                if (!Physics.SphereCast(origin, 0.4f, heading, out var hit, lookAhead, mask, QueryTriggerInteraction.Ignore))
                    return heading;
                float side = Vector3.Cross(heading, hit.normal).y >= 0f ? -1f : 1f;
                heading = Quaternion.Euler(0f, side * 45f, 0f) * heading;
            }
            return heading;
        }

        static Vector3 Ground(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 20f, Vector3.down, out var hit, 60f, Layers.Mask(Layers.Ground), QueryTriggerInteraction.Ignore)) p.y = hit.point.y;
            return p;
        }

        void Say(One d, Sound sound)
        {
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(sound, d.root.position + Vector3.up * 1.2f, MixerNames.Outside, 0.9f, Random.Range(0.9f, 1.1f), 50f);
        }

        // ------------------------------------------------------------------ the gait

        void Animate(One d, float speed, float targetNeck, float dt)
        {
            Transform[] p = d.parts;
            bool galloping = speed > s.deerWalkSpeed * 2f;
            float bob = 0f, pitch = 0f, neckSwing = 0f;
            var hip = new float[4];
            var fold = new float[4];
            if (speed > 0f)
            {
                // Each leg goes round a cycle: planted (the foot pushing back under the body as the deer goes over
                // it), then lifted, the knee or hock folding up tight, and swung forward to reach for the ground.
                //   Gallop: the hind legs strike almost together and drive it on, then the forelegs reach out and
                //   land one after the other, with a moment in the air between: the body rocking nose-up as the
                //   hind legs push, nose-down as it lands on its front.
                //   Walk: one foot at a time, round the four (hind, fore on the same side, the other hind, fore).
                float cyclesPerSecond = galloping ? Mathf.Lerp(1.9f, 2.4f, Mathf.InverseLerp(s.deerWalkSpeed * 2f, s.deerGallopSpeed, speed)) : speed * 0.9f;
                d.phase += dt * cyclesPerSecond;
                float c = d.phase - Mathf.Floor(d.phase);
                float[] offset = galloping ? new[] { 0.45f, 0.55f, 0f, 0.08f } : new[] { 0.25f, 0.75f, 0f, 0.5f }; // FL, FR, BL, BR
                float stance = galloping ? 0.35f : 0.62f;
                for (int leg = FrontLeft; leg <= BackRight; leg++)
                {
                    bool back = leg >= BackLeft;
                    float reach = galloping ? (back ? 35f : 48f) : 18f;   // forward, at the end of its swing
                    float push = galloping ? (back ? 48f : 32f) : 18f;    // back, at the end of its stance
                    float u = c + offset[leg];
                    u -= Mathf.Floor(u);
                    if (u < stance)
                    {
                        hip[leg] = Mathf.Lerp(-reach, push, u / stance);
                        fold[leg] = 0f;
                    }
                    else
                    {
                        float sw = (u - stance) / (1f - stance);
                        hip[leg] = Mathf.Lerp(push, -reach, Mathf.SmoothStep(0f, 1f, sw));
                        fold[leg] = Mathf.Sin(Mathf.PI * Mathf.Min(1f, sw * 1.15f)) * (galloping ? (back ? 85f : 110f) : (back ? 35f : 50f));
                    }
                }
                if (galloping)
                {
                    bob = 0.09f * (0.5f + 0.5f * Mathf.Cos(Mathf.PI * 2f * (c - 0.3f)));   // highest in the leap after the hind legs push
                    pitch = -7f * Mathf.Cos(Mathf.PI * 2f * (c - 0.12f));                 // nose up as they push, down as it lands in front
                    neckSwing = 9f * Mathf.Cos(Mathf.PI * 2f * (c - 0.2f));               // the head and neck reaching with each bound
                }
                else bob = 0.012f * Mathf.Cos(Mathf.PI * 4f * c);
                for (int leg = FrontLeft; leg <= BackRight; leg++)
                    p[leg].localRotation = Quaternion.Slerp(p[leg].localRotation, Quaternion.Euler(hip[leg], 0f, 0f), 1f - Mathf.Exp(-30f * dt));
            }
            else
            {
                for (int leg = FrontLeft; leg <= BackRight; leg++)
                    p[leg].localRotation = Quaternion.Slerp(p[leg].localRotation, Quaternion.identity, 1f - Mathf.Exp(-8f * dt));
            }

            d.body.localPosition = Vector3.Lerp(d.body.localPosition, new Vector3(0f, 1f + bob, 0f), 1f - Mathf.Exp(-20f * dt));
            d.body.localRotation = Quaternion.Slerp(d.body.localRotation, Quaternion.Euler(pitch, 0f, 0f), 1f - Mathf.Exp(-15f * dt));

            // The knees (forelegs: folding back) and hocks (hind legs: folding forward).
            for (int leg = FrontLeft; leg <= BackRight; leg++)
            {
                Quaternion bend = Quaternion.Euler(leg >= BackLeft ? -fold[leg] : fold[leg], 0f, 0f);
                d.knees[leg].localRotation = Quaternion.Slerp(d.knees[leg].localRotation, bend, 1f - Mathf.Exp(-30f * dt));
            }

            // Neck and head: down to graze, up to watch. The head stays roughly level.
            d.neckAngle = Mathf.MoveTowards(d.neckAngle, targetNeck, dt * 90f);
            float nibble = d.mode == Graze && Mathf.Abs(d.neckAngle - targetNeck) < 1f ? Mathf.Sin(Time.time * 6f + d.phase) * 4f : 0f;
            p[Neck].localRotation = Quaternion.Euler(d.neckAngle + nibble + neckSwing, 0f, 0f);
            p[Head].localRotation = Quaternion.Euler(-d.neckAngle * 0.75f + 15f, 0f, 0f);

            // The white tail goes up when spooked.
            float tail = d.mode == Flee || d.mode == Alert ? -60f : 10f;
            p[Tail].localRotation = Quaternion.Slerp(p[Tail].localRotation, Quaternion.Euler(tail, 0f, 0f), 1f - Mathf.Exp(-6f * dt));
        }

        // ------------------------------------------------------------------ the body

        One Build()
        {
            var d = new One();
            d.root = new GameObject("Deer").transform;
            d.root.SetParent(transform, false);
            d.root.localScale = Vector3.one * Random.Range(0.9f, 1.15f);
            bool stag = Random.value < 0.5f;

            d.body = new GameObject("Body").transform;
            d.body.SetParent(d.root, false);
            d.body.localPosition = new Vector3(0f, 1f, 0f);

            // The body: one smooth shape from the rump to the brisket: round haunches, the waist tucked up under
            // the ribs, a deep chest, the withers rising over the shoulders. (x half-width, y half-height.)
            Shape(d.body, "Torso", coat, 2.3f, true, true,
                (new Vector3(0f, 0.07f, -0.68f), new Vector2(0.05f, 0.05f)),
                (new Vector3(0f, 0.06f, -0.62f), new Vector2(0.14f, 0.15f)),
                (new Vector3(0f, 0.05f, -0.5f), new Vector2(0.2f, 0.21f)),
                (new Vector3(0f, 0.04f, -0.32f), new Vector2(0.2f, 0.21f)),
                (new Vector3(0f, 0.05f, -0.1f), new Vector2(0.185f, 0.2f)),
                (new Vector3(0f, 0.04f, 0.12f), new Vector2(0.2f, 0.24f)),
                (new Vector3(0f, 0.05f, 0.32f), new Vector2(0.2f, 0.27f)),
                (new Vector3(0f, 0.08f, 0.47f), new Vector2(0.17f, 0.25f)),
                (new Vector3(0f, 0.1f, 0.57f), new Vector2(0.11f, 0.17f)),
                (new Vector3(0f, 0.12f, 0.61f), new Vector2(0.04f, 0.06f)));
            Shape(d.body, "Withers", coat, 2f, true, true,
                (new Vector3(0f, 0.2f, 0.12f), new Vector2(0.02f, 0.02f)),
                (new Vector3(0f, 0.24f, 0.3f), new Vector2(0.09f, 0.08f)),
                (new Vector3(0f, 0.24f, 0.45f), new Vector2(0.07f, 0.07f)),
                (new Vector3(0f, 0.22f, 0.55f), new Vector2(0.02f, 0.02f)));
            // A paler belly, and the white patch round the tail.
            Shape(d.body, "Belly", bellyMat, 2f, true, true,
                (new Vector3(0f, -0.1f, -0.3f), new Vector2(0.03f, 0.02f)),
                (new Vector3(0f, -0.125f, -0.15f), new Vector2(0.15f, 0.09f)),
                (new Vector3(0f, -0.15f, 0.15f), new Vector2(0.16f, 0.1f)),
                (new Vector3(0f, -0.17f, 0.35f), new Vector2(0.03f, 0.02f)));
            Part(PrimitiveType.Sphere, whiteMat, d.body, new Vector3(0f, 0.04f, -0.645f), new Vector3(0.2f, 0.22f, 0.05f));

            var parts = new Transform[7];
            d.knees = new Transform[4];

            // Legs pivot at the shoulders and hips: muscled at the top, then slim jointed bones down to small
            // black hooves. The front ones bend at the knee, the back ones at the hock (which sticks out behind).
            Vector3[] hips =
            {
                new Vector3(-0.12f, -0.06f, 0.4f), new Vector3(0.12f, -0.06f, 0.4f),
                new Vector3(-0.13f, -0.02f, -0.42f), new Vector3(0.13f, -0.02f, -0.42f),
            };
            for (int i = 0; i < 4; i++)
            {
                var hip = new GameObject("Leg").transform;
                hip.SetParent(d.body, false);
                hip.localPosition = hips[i];
                bool back = i >= 2;
                var knee = new GameObject(back ? "Hock" : "Knee").transform;
                knee.SetParent(hip, false);
                if (back)
                {
                    Shape(hip, "Thigh", coat, 2f, true, false,
                        (new Vector3(0f, 0.12f, 0.02f), new Vector2(0.09f, 0.14f)),
                        (new Vector3(0f, -0.08f, -0.01f), new Vector2(0.085f, 0.13f)),
                        (new Vector3(0f, -0.28f, -0.06f), new Vector2(0.05f, 0.07f)),
                        (new Vector3(0f, -0.46f, -0.1f), new Vector2(0.03f, 0.045f)),
                        (new Vector3(0f, -0.52f, -0.115f), new Vector2(0.028f, 0.04f)));
                    knee.localPosition = new Vector3(0f, -0.5f, -0.11f);
                    Shape(knee, "Cannon", coat, 2f, false, false,
                        (new Vector3(0f, 0.02f, -0.005f), new Vector2(0.027f, 0.038f)),
                        (new Vector3(0f, -0.2f, 0.04f), new Vector2(0.02f, 0.024f)),
                        (new Vector3(0f, -0.38f, 0.08f), new Vector2(0.019f, 0.022f)),
                        (new Vector3(0f, -0.41f, 0.09f), new Vector2(0.026f, 0.028f)));
                    Shape(knee, "Hoof", hoofMat, 2.5f, false, true,
                        (new Vector3(0f, -0.41f, 0.09f), new Vector2(0.024f, 0.026f)),
                        (new Vector3(0f, -0.445f, 0.105f), new Vector2(0.027f, 0.034f)),
                        (new Vector3(0f, -0.475f, 0.12f), new Vector2(0.028f, 0.038f)));
                }
                else
                {
                    Shape(hip, "Forearm", coat, 2f, true, false,
                        (new Vector3(0f, 0.1f, 0f), new Vector2(0.07f, 0.1f)),
                        (new Vector3(0f, -0.1f, 0.01f), new Vector2(0.06f, 0.075f)),
                        (new Vector3(0f, -0.32f, 0f), new Vector2(0.035f, 0.042f)),
                        (new Vector3(0f, -0.44f, 0f), new Vector2(0.03f, 0.036f)));
                    knee.localPosition = new Vector3(0f, -0.42f, 0f);
                    Shape(knee, "Cannon", coat, 2f, false, false,
                        (new Vector3(0f, 0.02f, 0f), new Vector2(0.028f, 0.034f)),
                        (new Vector3(0f, -0.2f, 0f), new Vector2(0.019f, 0.023f)),
                        (new Vector3(0f, -0.4f, 0.01f), new Vector2(0.019f, 0.022f)),
                        (new Vector3(0f, -0.445f, 0.015f), new Vector2(0.025f, 0.028f)));
                    Shape(knee, "Hoof", hoofMat, 2.5f, false, true,
                        (new Vector3(0f, -0.445f, 0.015f), new Vector2(0.023f, 0.026f)),
                        (new Vector3(0f, -0.478f, 0.03f), new Vector2(0.026f, 0.034f)),
                        (new Vector3(0f, -0.51f, 0.045f), new Vector2(0.027f, 0.038f)));
                }
                parts[i] = hip;
                d.knees[i] = knee;
            }

            // Neck: deep where it springs from the chest, slender up to the head, a pale throat patch.
            var neck = new GameObject("Neck").transform;
            neck.SetParent(d.body, false);
            neck.localPosition = new Vector3(0f, 0.12f, 0.48f);
            Shape(neck, "Neck", coat, 2f, false, true,
                (new Vector3(0f, -0.12f, -0.06f), new Vector2(0.14f, 0.2f)),
                (new Vector3(0f, 0.06f, 0.0f), new Vector2(0.1f, 0.14f)),
                (new Vector3(0f, 0.28f, 0.04f), new Vector2(0.07f, 0.095f)),
                (new Vector3(0f, 0.48f, 0.05f), new Vector2(0.06f, 0.08f)),
                (new Vector3(0f, 0.6f, 0.02f), new Vector2(0.055f, 0.07f)));
            Part(PrimitiveType.Sphere, whiteMat, neck, new Vector3(0f, 0.44f, 0.115f), new Vector3(0.07f, 0.09f, 0.03f));
            parts[Neck] = neck;

            // Head: the rounded skull tapering to a long, fine muzzle; black nose; big cupped ears; dark eyes
            // set wide on the sides.
            var head = new GameObject("Head").transform;
            head.SetParent(neck, false);
            head.localPosition = new Vector3(0f, 0.55f, 0f);
            Shape(head, "Skull", coat, 2f, true, false,
                (new Vector3(0f, 0.02f, -0.07f), new Vector2(0.04f, 0.04f)),
                (new Vector3(0f, 0.025f, -0.03f), new Vector2(0.07f, 0.075f)),
                (new Vector3(0f, 0.025f, 0.04f), new Vector2(0.075f, 0.075f)),
                (new Vector3(0f, 0f, 0.12f), new Vector2(0.055f, 0.06f)),
                (new Vector3(0f, -0.025f, 0.2f), new Vector2(0.04f, 0.048f)),
                (new Vector3(0f, -0.04f, 0.26f), new Vector2(0.032f, 0.038f)),
                (new Vector3(0f, -0.045f, 0.285f), new Vector2(0.026f, 0.03f)));
            Part(PrimitiveType.Sphere, hoofMat, head, new Vector3(0f, -0.04f, 0.29f), new Vector3(0.05f, 0.04f, 0.03f));     // nose
            Part(PrimitiveType.Sphere, whiteMat, head, new Vector3(0f, -0.075f, 0.25f), new Vector3(0.04f, 0.02f, 0.035f));  // pale chin
            Part(PrimitiveType.Sphere, bellyMat, head, new Vector3(0f, -0.02f, 0.265f), new Vector3(0.06f, 0.03f, 0.03f));   // the pale band round the nose
            for (int side = -1; side <= 1; side += 2)
            {
                // An ear: a long cupped leaf, its pale inside facing forward and out.
                var ear = new GameObject("Ear").transform;
                ear.SetParent(head, false);
                ear.localPosition = new Vector3(side * 0.06f, 0.07f, -0.04f);
                ear.localRotation = Quaternion.Euler(-15f, side * 30f, side * -55f);
                Shape(ear, "Ear", coat, 2f, true, true,
                    (new Vector3(0f, 0f, 0f), new Vector2(0.025f, 0.012f)),
                    (new Vector3(0f, 0.06f, 0f), new Vector2(0.05f, 0.016f)),
                    (new Vector3(0f, 0.13f, 0f), new Vector2(0.04f, 0.012f)),
                    (new Vector3(0f, 0.17f, 0f), new Vector2(0.008f, 0.004f)));
                Part(PrimitiveType.Sphere, bellyMat, ear, new Vector3(0f, 0.08f, 0.012f), new Vector3(0.07f, 0.13f, 0.008f));
                Part(PrimitiveType.Sphere, eyeMat, head, new Vector3(side * 0.058f, 0.03f, 0.07f), new Vector3(0.024f, 0.028f, 0.034f));
            }
            if (stag) Antlers(head);
            parts[Head] = head;

            // The tail: dark on top, white underneath (it flashes when it's raised).
            var tail = new GameObject("Tail").transform;
            tail.SetParent(d.body, false);
            tail.localPosition = new Vector3(0f, 0.15f, -0.66f);
            Shape(tail, "Tail", coat, 2f, true, true,
                (new Vector3(0f, 0f, 0f), new Vector2(0.035f, 0.02f)),
                (new Vector3(0f, -0.07f, -0.02f), new Vector2(0.045f, 0.025f)),
                (new Vector3(0f, -0.15f, -0.02f), new Vector2(0.025f, 0.015f)));
            Part(PrimitiveType.Sphere, whiteMat, tail, new Vector3(0f, -0.07f, 0.004f), new Vector3(0.075f, 0.15f, 0.03f));
            parts[Tail] = tail;

            d.parts = parts;
            d.neckAngle = 35f;
            d.phase = Random.Range(0f, 10f);
            if (Services.TryGet<SoundPlayer>(out var sounds))
            {
                d.loop = d.root.gameObject.AddComponent<AudioSource>();
                Muffled.On(d.loop);
                d.loop.clip = sounds.Clip(Sound.Gallop);
                d.loop.loop = true;
                d.loop.spatialBlend = 1f;
                d.loop.minDistance = 4f;
                d.loop.maxDistance = 50f;
                d.loop.volume = 0f;
                d.loop.pitch = Random.Range(0.9f, 1.1f);
                d.loop.outputAudioMixerGroup = sounds.Group(MixerNames.Outside);
            }
            d.heat = Heat.Add(d.root, 1.4f, 0.45f); // (the drone's thermal view)
            return d;
        }

        // A stag's antlers: each beam sweeping up and back then forward, with a brow tine and two more points.
        void Antlers(Transform head)
        {
            for (int side = -1; side <= 1; side += 2)
            {
                float s = side;
                Vector3 b0 = new Vector3(s * 0.05f, 0.1f, -0.01f), b1 = new Vector3(s * 0.12f, 0.25f, -0.07f),
                        b2 = new Vector3(s * 0.2f, 0.36f, -0.02f), b3 = new Vector3(s * 0.23f, 0.43f, 0.08f), tip = new Vector3(s * 0.19f, 0.46f, 0.17f);
                Bone(head, b0, b1, 0.03f);
                Bone(head, b1, b2, 0.026f);
                Bone(head, b2, b3, 0.022f);
                Bone(head, b3, tip, 0.016f);
                Bone(head, Vector3.Lerp(b0, b1, 0.35f), new Vector3(s * 0.08f, 0.21f, 0.08f), 0.015f); // (brow tine)
                Bone(head, b2, new Vector3(s * 0.18f, 0.53f, -0.04f), 0.016f);
                Bone(head, b3, new Vector3(s * 0.27f, 0.56f, 0.08f), 0.014f);
                Part(PrimitiveType.Sphere, antlerMat, head, b0, Vector3.one * 0.045f); // (the burr)
            }
        }

        void Bone(Transform parent, Vector3 a, Vector3 b, float thickness)
        {
            Vector3 d = b - a;
            var t = Part(PrimitiveType.Cylinder, antlerMat, parent, (a + b) * 0.5f, new Vector3(thickness, d.magnitude * 0.5f, thickness));
            t.localRotation = Quaternion.FromToRotation(Vector3.up, d);
        }

        // A smooth lofted shape under the parent (see Loft).
        void Shape(Transform parent, string name, Material mat, float squareness, bool capStart, bool capEnd, params (Vector3 at, Vector2 radius)[] path)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var mesh = Lofts.Tube(path, squareness, capStart, capEnd);
            meshes.Add(mesh);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterial = mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        static Transform Part(PrimitiveType type, Material mat, Transform parent, Vector3 pos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(type);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Destroy(go.GetComponent<Collider>());
            return go.transform;
        }
    }
}
