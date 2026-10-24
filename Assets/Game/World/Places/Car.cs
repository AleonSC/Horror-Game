using System.Collections;
using Game.Audio;
using Game.Core;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.World
{
    /// <summary>
    /// The abandoned car (section 6.5). Y gets you into the driver's seat (the muffled Sheltered sound) and out
    /// again. A switch on the dashboard turns the headlights on: two beams 16 m long that are a safe light, for 120 s
    /// of battery. It answers its key fob: two chirps and the lights flashing, wherever it is (things nearby hear it).
    /// The glove box and trunk are searched like any container.
    /// </summary>
    public sealed class Car : MonoBehaviour, ISeat
    {
        static int nextShelterId = 5000;

        [Tooltip("How far off things hear the chirp (metres).")]
        public float heard = 60f;
        [Tooltip("Seconds of headlight battery.")]
        public float headlightBattery = 120f;
        [Tooltip("How far the headlights' safe light reaches (metres).")]
        public float beamLength = 16f;
        [Tooltip("The driver's seat, in this object's space.")]
        public Vector3 seat = new Vector3(-0.4f, 0.35f, 0.1f);
        [Tooltip("Where you stand getting in and out (the driver's door), in this object's space.")]
        public Vector3 door = new Vector3(-1.5f, 0f, 0.1f);

        Light[] indicators, headlights;
        Transform seatPoint;
        SafeZone beam;
        float battery;
        bool lightsOn, seated;
        int shelterId;
        System.IDisposable fobSub;

        public Transform SeatPoint => seatPoint;
        public Vector3 ExitPoint => transform.TransformPoint(door) + Vector3.up * 0.05f;
        public bool CanSit(Vector3 from) => Vector3.Distance(from, transform.TransformPoint(door)) < 2.2f;
        public float BatteryLeft => battery / Mathf.Max(1f, headlightBattery);

        void Start()
        {
            shelterId = nextShelterId++;
            battery = headlightBattery;
            seatPoint = new GameObject("Driver's seat").transform;
            seatPoint.SetParent(transform, false);
            seatPoint.localPosition = seat;

            indicators = new Light[2];
            headlights = new Light[2];
            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? -0.65f : 0.65f;
                indicators[i] = MakeLight(LightType.Point, new Vector3(x, 0.75f, 2.25f), new Color(1f, 0.7f, 0.3f), 8f, 12f);
                headlights[i] = MakeLight(LightType.Spot, new Vector3(x, 0.7f, 2.22f), new Color(1f, 0.95f, 0.8f), 30f, 900f);
                headlights[i].spotAngle = 55f;
                headlights[i].innerSpotAngle = 30f;
                headlights[i].transform.localRotation = Quaternion.Euler(4f, 0f, 0f);
            }
            beam = new SafeZone("Car headlights", transform.position, beamLength * 0.5f) { Active = false, Beam = true, Length = beamLength, HalfWidth = 2.6f };
            if (Services.TryGet<SafeZones>(out var zones)) zones.Add(beam);

            // The headlight switch on the dashboard (click it from the seat).
            var sw = new GameObject("Headlight switch") { layer = Layers.Props };
            sw.transform.SetParent(transform, false);
            sw.transform.localPosition = new Vector3(-0.15f, 0.95f, 0.85f);
            var col = sw.AddComponent<BoxCollider>();
            col.size = new Vector3(0.2f, 0.12f, 0.12f);
            col.isTrigger = true;
            sw.AddComponent<HeadlightSwitch>().car = this;

            Blood();
            Seats.Add(this);
            if (Services.TryGet<EventBus>(out var events)) fobSub = events.Subscribe<KeyFobPressed>(_ => StartCoroutine(Chirp()));
        }

        void OnDestroy()
        {
            fobSub?.Dispose();
            Seats.Remove(this);
            if (Services.TryGet<SafeZones>(out var zones)) zones.Remove(beam);
            if (seated) Sat(false);
        }

        Light MakeLight(LightType type, Vector3 at, Color color, float range, float intensity)
        {
            var go = new GameObject(type == LightType.Spot ? "Headlight" : "Indicator");
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
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(Sound.DoorShut, ExitPoint + Vector3.up, MixerNames.World, 0.8f, 0.8f, 25f);
            if (!Services.TryGet<EventBus>(out var events)) return;
            if (now) events.Publish(new EnteredShelter(shelterId, "the car"));
            else events.Publish(new ExitedShelter(shelterId, "the car"));
        }

        public void ToggleLights()
        {
            if (!lightsOn && battery <= 0f) return;
            lightsOn = !lightsOn;
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(Sound.Click, transform.position + Vector3.up, MixerNames.World, 0.7f, 0.7f, 10f);
        }

        void Update()
        {
            if (lightsOn)
            {
                battery -= Time.deltaTime;
                if (battery <= 0f)
                {
                    battery = 0f;
                    lightsOn = false;
                }
            }
            // Dimming and flickering as the battery dies.
            float level = !lightsOn ? 0f : battery > 10f ? 1f : (battery / 10f) * (Random.value < 0.1f ? 0.3f : 1f);
            foreach (var h in headlights)
            {
                h.enabled = level > 0f;
                h.intensity = 900f * level;
            }
            beam.Active = lightsOn;
            beam.Center = transform.TransformPoint(new Vector3(0f, 0f, 2.3f));
            beam.Forward = transform.forward;
        }

        IEnumerator Chirp()
        {
            if (Services.TryGet<SoundPlayer>(out var sounds)) sounds.Play(Sound.Chirp, transform.position + Vector3.up, MixerNames.World, 1f, 1f, heard * 2f);
            if (Services.TryGet<EventBus>(out var events)) events.Publish(new NoiseEmitted(transform.position, heard, "car chirp"));
            for (int blink = 0; blink < 2; blink++)
            {
                foreach (var l in indicators) l.enabled = true;
                yield return new WaitForSeconds(0.14f);
                foreach (var l in indicators) l.enabled = false;
                yield return new WaitForSeconds(0.11f);
            }
        }

        // Blood everywhere: dark smears down the doors and pooled on the ground.
        void Blood()
        {
            var mat = Game.Rendering.RuntimeMaterials.Lit("Blood", new Color(0.12f, 0.005f, 0.005f), 0.75f);
            var rng = new System.Random(GetInstanceID());
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
                    go.transform.localPosition = new Vector3(side * 0.905f, 0.6f + (float)rng.NextDouble() * 0.3f, (float)rng.NextDouble() * 2.4f - 1.2f);
                    go.transform.localScale = new Vector3(0.01f, 0.2f + (float)rng.NextDouble() * 0.4f, 0.15f + (float)rng.NextDouble() * 0.3f);
                }
                go.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }
    }
}
