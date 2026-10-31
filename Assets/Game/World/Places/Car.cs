using System.Collections;
using Game.Audio;
using Game.Core;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.World
{
    /// <summary>
    /// The abandoned car (section 6.5). Y by any of its doors gets you into that seat (the world outside goes
    /// muffled), Y again moves you round the seats, E gets you out by your seat's door. Each door opens (E), and
    /// the trunk lid and the glove box's little door click open to be searched. The headlights (click a headlight,
    /// or the switch on the dashboard) throw two beams 16 m long that are a safe light. Its key fob: the left button
    /// chirps it (two chirps and the lights flashing; things nearby hear it), the right sets its alarm off (the horn
    /// blaring, the lights flashing, heard far off) and stops it. The headlights and the alarm both run off its
    /// battery, the alarm much faster.
    /// </summary>
    public sealed class Car : MonoBehaviour, ISeat, ISeatSwitcher
    {
        static int nextShelterId = 5000;

        [Tooltip("How far off things hear the chirp (metres).")]
        public float heard = 60f;
        [Tooltip("How far off things hear the alarm (metres).")]
        public float alarmHeard = 150f;
        [Tooltip("Seconds of battery with the headlights on (the alarm uses it faster).")]
        public float headlightBattery = 120f;
        [Tooltip("The alarm drains the battery this many times faster than the headlights.")]
        public float alarmDrain = 3f;
        [Tooltip("How far the headlights' safe light reaches (metres).")]
        public float beamLength = 16f;
        [Tooltip("The seats (driver's first, then round), in this object's space.")]
        public Vector3[] seats =
        {
            new Vector3(-0.38f, 0.35f, 0.15f), new Vector3(0.38f, 0.35f, 0.15f),
            new Vector3(0.38f, 0.35f, -0.85f), new Vector3(-0.38f, 0.35f, -0.85f),
        };
        [Tooltip("Where you stand getting in and out of each seat (by its door), in this object's space.")]
        public Vector3[] doors =
        {
            new Vector3(-1.5f, 0f, 0.55f), new Vector3(1.5f, 0f, 0.55f),
            new Vector3(1.5f, 0f, -0.65f), new Vector3(-1.5f, 0f, -0.65f),
        };
        static readonly string[] SeatNames = { "The driver's seat", "The passenger seat", "The back seat, right", "The back seat, left" };

        Light[] indicators, headlights;
        Renderer[] headlightLenses = new Renderer[0], indicatorLenses = new Renderer[0], tailLenses = new Renderer[0];
        Transform seatPoint;
        SafeZone beam;
        AudioSource alarmSound;
        float battery, nextAlarmNoise, flash;
        bool lightsOn, alarmOn, seated, chirping;

        [Tooltip("Crashed: its left headlight's stayed on, weak and yellow, flickering now and then (and not on its battery).")]
        public bool oneLightOn;
        [Tooltip("How bright that one weak headlight is (candela).")]
        public float weakLight = 140f;
        Light weak;
        int seatIndex, shelterId;
        readonly System.Collections.Generic.List<System.IDisposable> subs = new System.Collections.Generic.List<System.IDisposable>();

        public Transform SeatPoint => seatPoint;
        public Vector3 ExitPoint => transform.TransformPoint(doors[Mathf.Clamp(seatIndex, 0, doors.Length - 1)]) + Vector3.up * 0.05f;
        public string SeatName => SeatNames[Mathf.Clamp(seatIndex, 0, SeatNames.Length - 1)];
        public float BatteryLeft => battery / Mathf.Max(1f, headlightBattery);
        public bool AlarmOn => alarmOn;

        // Near enough to one of its doors to get in: that's the seat you'll take.
        public bool CanSit(Vector3 from)
        {
            int best = -1;
            float bestD = 2.2f;
            for (int i = 0; i < doors.Length; i++)
            {
                float d = Vector3.Distance(from, transform.TransformPoint(doors[i]));
                if (d < bestD) { bestD = d; best = i; }
            }
            if (best < 0) return false;
            if (!seated) seatIndex = best;
            return true;
        }

        void Start()
        {
            shelterId = nextShelterId++;
            battery = headlightBattery;
            seatPoint = new GameObject("Seat").transform;
            seatPoint.SetParent(transform, false);
            seatPoint.localPosition = seats[0];

            indicators = new Light[2];
            headlights = new Light[2];
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -0.62f : 0.62f;
                indicators[i] = MakeLight(LightType.Point, new Vector3(x, 0.7f, 2.4f), new Color(1f, 0.7f, 0.3f), 8f, 12f);
                headlights[i] = MakeLight(LightType.Spot, new Vector3(x, 0.83f, 2.38f), new Color(1f, 0.95f, 0.8f), 30f, 900f);
                headlights[i].spotAngle = 55f;
                headlights[i].innerSpotAngle = 30f;
                headlights[i].transform.localRotation = Quaternion.Euler(4f, 0f, 0f);
            }
            beam = new SafeZone("Car headlights", transform.position, beamLength * 0.5f) { Active = false, Beam = true, Length = beamLength, HalfWidth = 2.6f };
            if (Services.TryGet<SafeZones>(out var zones)) zones.Add(beam);

            // The lenses that glow (the built-in model's, or yours, by name).
            headlightLenses = Lenses("Headlight L", "Headlight R");
            // (The headlights' own lights where the lenses are: on a crashed car they've been pushed back.)
            for (int i = 0; i < headlightLenses.Length && i < 2; i++)
                headlights[i].transform.position = headlightLenses[i].transform.position + transform.forward * 0.03f;
            if (oneLightOn && headlightLenses.Length > 0)
            {
                weak = MakeLight(LightType.Spot, Vector3.zero, new Color(1f, 0.82f, 0.55f), 22f, weakLight);
                weak.transform.position = headlightLenses[0].transform.position + transform.forward * 0.03f;
                weak.transform.localRotation = Quaternion.Euler(6f, -14f, 0f); // (knocked a little aside and down)
                weak.spotAngle = 50f;
                weak.innerSpotAngle = 20f;
            }
            indicatorLenses = Lenses("Indicator L", "Indicator R");
            tailLenses = Lenses("Tail light L", "Tail light R");
            // The headlights themselves can be clicked, and so can the switch on the dashboard.
            foreach (var r in headlightLenses)
            {
                var col = r.GetComponent<Collider>();
                if (col == null) col = r.gameObject.AddComponent<SphereCollider>();
                r.gameObject.AddComponent<HeadlightSwitch>().car = this;
            }
            var sw = new GameObject("Headlight switch") { layer = Layers.Props };
            sw.transform.SetParent(transform, false);
            sw.transform.localPosition = new Vector3(-0.12f, 0.97f, 0.83f);
            var swCol = sw.AddComponent<BoxCollider>();
            swCol.size = new Vector3(0.14f, 0.1f, 0.08f);
            swCol.isTrigger = true;
            sw.AddComponent<HeadlightSwitch>().car = this;

            // The trunk and glove box: what's in them can only be reached with their lids open.
            Link("Trunk lid", "Search the trunk");
            Link("Glove box lid", "Search the glove box");

            Blood();
            Seats.Add(this);
            if (Services.TryGet<EventBus>(out var events))
            {
                subs.Add(events.Subscribe<KeyFobPressed>(_ => { if (!chirping) StartCoroutine(Chirp()); }));
                subs.Add(events.Subscribe<KeyFobAlarm>(_ => SetAlarm(!alarmOn)));
                subs.Add(events.Subscribe<RunReset>(_ => { SetAlarm(false); lightsOn = false; battery = headlightBattery; }));
            }
        }

        void OnDestroy()
        {
            foreach (var s in subs) s.Dispose();
            Seats.Remove(this);
            if (Services.TryGet<SafeZones>(out var zones)) zones.Remove(beam);
            if (seated) Sat(false);
        }

        Renderer[] Lenses(params string[] names)
        {
            var found = new System.Collections.Generic.List<Renderer>();
            foreach (var r in GetComponentsInChildren<Renderer>(true))
                foreach (var n in names)
                    if (r.name == n) { found.Add(r); break; }
            foreach (var r in found) r.material = new Material(r.sharedMaterial); // (each its own, to glow)
            return found.ToArray();
        }

        void Link(string lidName, string containerLabel)
        {
            Lid lid = null;
            foreach (var l in GetComponentsInChildren<Lid>(true)) if (l.name == lidName) { lid = l; break; }
            if (lid == null) return;
            var inside = new System.Collections.Generic.List<Collider>();
            foreach (var c in GetComponentsInChildren<Container>(true))
                if (c.label == containerLabel) inside.AddRange(c.GetComponents<Collider>());
            lid.reveals = inside.ToArray();
            foreach (var c in inside) c.enabled = false;
        }

        Light MakeLight(LightType type, Vector3 at, Color color, float range, float intensity)
        {
            var go = new GameObject(type == LightType.Spot ? "Headlight" : "Indicator light");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = at;
            go.AddHDLight(type);
            var l = go.GetComponent<Light>();
            l.color = color;
            l.range = range;
            l.intensity = intensity;
            l.shadows = type == LightType.Spot ? LightShadows.Soft : LightShadows.None;
            l.enabled = false;
            return l;
        }

        public void Sat(bool now)
        {
            seated = now;
            if (now) seatPoint.localPosition = seats[Mathf.Clamp(seatIndex, 0, seats.Length - 1)];
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(Sound.DoorShut, ExitPoint + Vector3.up, MixerNames.World, 0.8f, 0.8f, 25f);
            if (!Services.TryGet<EventBus>(out var events)) return;
            if (now) events.Publish(new EnteredShelter(shelterId, "the car"));
            else events.Publish(new ExitedShelter(shelterId, "the car"));
        }

        public void NextSeat()
        {
            seatIndex = (seatIndex + 1) % seats.Length;
            seatPoint.localPosition = seats[seatIndex];
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(Sound.Rustle, seatPoint.position + Vector3.up * 0.5f, MixerNames.World, 0.4f, 1.3f, 8f);
        }

        public void ToggleLights()
        {
            if (!lightsOn && battery <= 0f) return;
            lightsOn = !lightsOn;
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(Sound.Click, transform.position + Vector3.up, MixerNames.World, 0.7f, 0.7f, 10f);
        }

        void SetAlarm(bool on)
        {
            if (on && battery <= 0f) on = false;
            alarmOn = on;
            if (on && alarmSound == null && Services.TryGet<SoundPlayer>(out var sounds))
            {
                alarmSound = gameObject.AddComponent<AudioSource>();
                alarmSound.clip = sounds.Clip(Sound.CarAlarm);
                alarmSound.loop = true;
                alarmSound.spatialBlend = 1f;
                alarmSound.rolloffMode = AudioRolloffMode.Logarithmic;
                alarmSound.minDistance = 6f;
                alarmSound.maxDistance = alarmHeard * 1.5f;
                alarmSound.dopplerLevel = 0f;
                alarmSound.outputAudioMixerGroup = sounds.Group(MixerNames.World);
            }
            if (alarmSound == null) return;
            if (on && !alarmSound.isPlaying) alarmSound.Play();
            if (!on) alarmSound.Stop();
            nextAlarmNoise = 0f;
        }

        void Update()
        {
            float dt = Time.deltaTime;
            // The battery: the headlights use it, the alarm much faster.
            if (lightsOn || alarmOn)
            {
                battery -= dt * ((lightsOn ? 1f : 0f) + (alarmOn ? alarmDrain : 0f));
                if (battery <= 0f)
                {
                    battery = 0f;
                    lightsOn = false;
                    SetAlarm(false);
                }
            }
            // The alarm: heard far off, and the lights flash with it.
            flash = alarmOn ? (Mathf.Repeat(Time.time, 0.7f) < 0.35f ? 1f : 0f) : 0f;
            if (alarmOn && Time.time >= nextAlarmNoise)
            {
                nextAlarmNoise = Time.time + 1.2f;
                if (Services.TryGet<EventBus>(out var events)) events.Publish(new NoiseEmitted(transform.position, alarmHeard, "car alarm"));
            }
            // Dimming and flickering as the battery dies.
            float level = !lightsOn ? 0f : battery > 10f ? 1f : (battery / 10f) * (Random.value < 0.1f ? 0.3f : 1f);
            float shown = Mathf.Max(level, flash);
            foreach (var h in headlights)
            {
                h.enabled = shown > 0f;
                h.intensity = 900f * shown;
            }
            foreach (var r in headlightLenses) Game.Rendering.RuntimeMaterials.SetGlow(r.material, new Color(1f, 0.95f, 0.8f) * (6f * shown));
            if (weak != null)
            {
                // The one that's stayed on: weak, yellow, now and then flickering (a loose wire).
                float flicker = Random.value < 0.04f ? Random.Range(0.1f, 0.5f) : 0.85f + 0.15f * Mathf.PerlinNoise(Time.time * 2f, 0.4f);
                weak.enabled = shown <= 0f;
                weak.intensity = weakLight * flicker;
                if (shown <= 0f && headlightLenses.Length > 0) Game.Rendering.RuntimeMaterials.SetGlow(headlightLenses[0].material, new Color(1f, 0.8f, 0.5f) * (0.8f * flicker));
            }
            if (!chirping)
            {
                foreach (var l in indicators) l.enabled = flash > 0f;
                foreach (var r in indicatorLenses) Game.Rendering.RuntimeMaterials.SetGlow(r.material, new Color(1f, 0.55f, 0.1f) * (4f * flash));
            }
            foreach (var r in tailLenses) Game.Rendering.RuntimeMaterials.SetGlow(r.material, new Color(1f, 0.05f, 0.03f) * (2.5f * Mathf.Max(level * 0.6f, flash)));
            beam.Active = lightsOn && level > 0f;
            beam.Center = transform.TransformPoint(new Vector3(0f, 0f, 2.3f));
            beam.Forward = transform.forward;
        }

        IEnumerator Chirp()
        {
            chirping = true;
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(Sound.Chirp, transform.position + Vector3.up, MixerNames.World, 1f, 1f, heard * 2f);
            if (Services.TryGet<EventBus>(out var events)) events.Publish(new NoiseEmitted(transform.position, heard, "car chirp"));
            for (int blink = 0; blink < 2; blink++)
            {
                SetIndicators(true);
                yield return new WaitForSeconds(0.14f);
                SetIndicators(false);
                yield return new WaitForSeconds(0.11f);
            }
            chirping = false;
        }

        void SetIndicators(bool on)
        {
            foreach (var l in indicators) l.enabled = on;
            foreach (var r in indicatorLenses) Game.Rendering.RuntimeMaterials.SetGlow(r.material, new Color(1f, 0.55f, 0.1f) * (on ? 4f : 0f));
        }

        // Blood: dark smears down the doors and pooled on the ground.
        void Blood()
        {
            var mat = Game.Rendering.RuntimeMaterials.Lit("Blood", new Color(0.12f, 0.005f, 0.005f), 0.75f);
            var p = transform.position;
            var rng = new System.Random(Mathf.RoundToInt(p.x * 73f + p.z * 151f)); // (the same blood for the same world)
            for (int i = 0; i < 7; i++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(go.GetComponent<Collider>());
                go.name = "Blood";
                go.transform.SetParent(transform, false);
                bool ground = i >= 4;
                float side = rng.Next(2) == 0 ? -1f : 1f;
                if (ground)
                {
                    go.transform.localPosition = new Vector3(side * (1.2f + (float)rng.NextDouble()), 0.01f, (float)rng.NextDouble() * 3f - 1.5f);
                    go.transform.localScale = new Vector3(0.6f + (float)rng.NextDouble(), 0.01f, 0.5f + (float)rng.NextDouble());
                    go.transform.localRotation = Quaternion.Euler(0f, (float)rng.NextDouble() * 180f, 0f);
                }
                else
                {
                    // (On the body behind the front wheel and on the rear quarter, which don't open.)
                    go.transform.localPosition = new Vector3(side * 0.905f, 0.6f + (float)rng.NextDouble() * 0.3f, rng.Next(2) == 0 ? 1.5f + (float)rng.NextDouble() * 0.5f : -1.5f - (float)rng.NextDouble() * 0.5f);
                    go.transform.localScale = new Vector3(0.01f, 0.2f + (float)rng.NextDouble() * 0.4f, 0.15f + (float)rng.NextDouble() * 0.3f);
                }
                go.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }
    }
}
