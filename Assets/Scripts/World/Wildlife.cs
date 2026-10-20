using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Small creatures that make the forest feel alive (and make you jump):
/// - Beetles: little black bugs crawling around on the ground near the player.
/// - Rats: sniff around, dart between spots, and scatter, squeaking, if you get close.
/// - Moths: flutter around the bulbs of lit lamps.
/// - Bats: every so often a few swoop overhead with a flurry of wing beats.
/// - Crows: flocks that fly over high in the sky now and then, gliding between wingbeats and cawing.
/// - Deer now and then grunt, bleat or snort while grazing, and snort when they notice you.
/// - Owls: dark owls perched on branches of some trees, white eyes, heads turning to stare at you,
///   and now and then one nearby calls out (a low hooting, or rarely a rasping screech).
/// - Deer: pitch-black deer that wander and graze, look up when you come near, and gallop off,
///   snorting, if you get too close. A spooked deer sets off the rest of its herd.
///
/// Creatures live in a circle around the player and are quietly moved back near them when they
/// drift too far away, so there's always something nearby without simulating the whole forest.
/// DarkForestGenerator adds this automatically.
/// </summary>
public class Wildlife : MonoBehaviour
{
    [Header("Beetles")]
    public int beetleCount = 40;
    [Tooltip("Beetles stay within this distance of the player.")]
    public float beetleRange = 18f;

    [Header("Rats")]
    public int ratCount = 6;
    public float ratRange = 28f;
    [Tooltip("Rats scatter when the player gets this close.")]
    public float ratFleeDistance = 5f;
    public float ratSpeed = 4.5f;

    [Header("Moths")]
    public int mothsPerLamp = 7;

    [Header("Bats")]
    public float minBatInterval = 15f;
    public float maxBatInterval = 45f;
    public int maxBatsPerFlock = 3;

    [Header("Crows")]
    public float minCrowInterval = 20f;
    public float maxCrowInterval = 60f;
    public int minCrowsPerFlock = 3;
    public int maxCrowsPerFlock = 8;
    [Tooltip("Size of the crows (1 = life size).")]
    public float crowSize = 1.6f;

    [Header("Owls")]
    [Tooltip("How many trees get an owl on a branch.")]
    public int owlCount = 45;
    [Tooltip("Owls within this distance turn their heads to stare at the player.")]
    public float owlStareDistance = 45f;
    public Color owlEyeColor = new Color(1.6f, 1.6f, 1.5f);
    [Tooltip("Seconds between owl calls (picked at random between the two). One owl near the player calls each time.")]
    public float minOwlCallInterval = 25f;
    public float maxOwlCallInterval = 70f;
    [Tooltip("Only owls within this distance call.")]
    public float owlCallDistance = 70f;
    [Range(0f, 1f)] public float owlCallVolume = 0.55f;
    [Range(0f, 1f), Tooltip("Chance a call is the rasping screech instead of hooting.")]
    public float owlScreechChance = 0.2f;

    [Header("Deer")]
    public int deerCount = 6;
    [Tooltip("Deer roam within this distance of the player.")]
    public float deerRange = 70f;
    [Tooltip("Deer stop and watch the player inside this distance.")]
    public float deerAlertDistance = 24f;
    [Tooltip("Deer bolt when the player gets this close.")]
    public float deerFleeDistance = 14f;
    public float deerWalkSpeed = 1.1f;
    public float deerGallopSpeed = 10f;
    [Tooltip("Seconds between deer calls (picked at random between the two). A grazing deer near the player grunts, bleats or snorts.")]
    public float minDeerCallInterval = 30f;
    public float maxDeerCallInterval = 80f;
    [Tooltip("Only deer within this distance call.")]
    public float deerCallDistance = 60f;

    [Header("Audio")]
    [Range(0f, 1f)] public float volume = 0.7f;
    [Tooltip("Leave clips empty to use the built-in synthesized sounds.")]
    public AudioClip squeakSound;
    public AudioClip skitterSound;
    public AudioClip wingSound;
    public AudioClip deerAlarmSound;
    public AudioClip cawSound;
    public AudioClip gallopLoop;

    class Critter
    {
        public Transform root;
        public Transform body;
        public Transform[] parts; // legs / wings / tail, depending on the creature
        public AudioSource audio;
        public AudioSource loop;
        public float neckAngle;
        public Vector3 heading = Vector3.forward;
        public Vector3 target;
        public float speed;
        public float timer;
        public float phase;
        public float seed; // per-creature randomness
        public int mode; // creature-specific state
    }

    class Bat
    {
        public Critter critter;
        public Vector3 start, end;
        public float progress, duration, wobble;
        public bool active, heard;
    }

    readonly List<Critter> beetles = new List<Critter>();
    readonly List<Critter> rats = new List<Critter>();
    readonly List<Critter> moths = new List<Critter>();
    readonly List<Bat> bats = new List<Bat>();
    readonly List<Bat> crows = new List<Bat>(); // same flight data as bats
    float crowTimer;
    readonly List<Critter> deer = new List<Critter>();

    class Owl
    {
        public Transform head;
        public float tilt, tiltTimer;
    }
    readonly List<Owl> owls = new List<Owl>();
    float owlCallTimer = -1f;

    DarkForestGenerator generator;
    Transform player;
    float batTimer;
    Material beetleMat, ratMat, eyeMat, mothMat, batMat, deerMat, deerEyeMat, owlMat, owlEyeMat, pupilMat;

    const int RatIdle = 0, RatDash = 1, RatFlee = 2;
    const int DeerGraze = 0, DeerWalk = 1, DeerAlert = 2, DeerFlee = 3;
    // Indices into a deer's parts array.
    const int FrontLeft = 0, FrontRight = 1, BackLeft = 2, BackRight = 3, Neck = 4, Head = 5, Tail = 6;

    void Start()
    {
        generator = FindAnyObjectByType<DarkForestGenerator>();

        beetleMat = MaterialFactory.Lit(new Color(0.02f, 0.02f, 0.025f), 0.7f);
        ratMat = MaterialFactory.Lit(new Color(0.06f, 0.05f, 0.045f), 0.15f);
        eyeMat = MaterialFactory.Unlit(new Color(0.5f, 0.15f, 0.1f));
        mothMat = MaterialFactory.Lit(new Color(0.12f, 0.1f, 0.08f), 0.05f);
        batMat = MaterialFactory.Lit(new Color(0.02f, 0.02f, 0.02f), 0.1f);
        deerMat = MaterialFactory.Lit(new Color(0.025f, 0.025f, 0.03f), 0.2f);
        owlMat = MaterialFactory.Lit(new Color(0.035f, 0.03f, 0.028f), 0.1f);
        owlEyeMat = MaterialFactory.Unlit(owlEyeColor);
        pupilMat = MaterialFactory.Unlit(Color.black);
        deerEyeMat = MaterialFactory.Unlit(new Color(0.35f, 0.45f, 0.4f)); // eyeshine

        for (int i = 0; i < beetleCount; i++) beetles.Add(BuildBeetle());
        for (int i = 0; i < ratCount; i++) { rats.Add(BuildRat()); GroundLeaves.Register(rats[rats.Count - 1].root, 0.15f); }
        for (int i = 0; i < maxBatsPerFlock; i++) bats.Add(new Bat { critter = BuildBat() });
        for (int i = 0; i < maxCrowsPerFlock; i++) crows.Add(new Bat { critter = BuildCrow() });
        crowTimer = Random.Range(minCrowInterval * 0.5f, maxCrowInterval);
        for (int i = 0; i < deerCount; i++) { deer.Add(BuildDeer()); GroundLeaves.Register(deer[deer.Count - 1].root, 0.45f); }

        batTimer = Random.Range(minBatInterval, maxBatInterval);
    }

    void Update()
    {
        if (player == null)
        {
            var controller = FindAnyObjectByType<FirstPersonController>();
            if (controller == null) return;
            player = controller.transform;
            foreach (var b in beetles) Place(b, Random.Range(2f, beetleRange));
            foreach (var r in rats) Place(r, Random.Range(6f, ratRange));
            for (int i = 0; i < deer.Count; i++) PlaceDeer(i);
            PlaceOwls(); // the forest is definitely built by the time there's a player
        }

        float dt = Time.deltaTime;
        foreach (var b in beetles) UpdateBeetle(b, dt);
        foreach (var r in rats) UpdateRat(r, dt);
        UpdateMoths();
        UpdateBats(dt);
        UpdateCrows(dt);
        for (int i = 0; i < deer.Count; i++) UpdateDeer(i, dt);
        UpdateDeerCalls(dt);
        UpdateOwls(dt);
    }

    // ---------------------------------------------------------------- Beetles

    void UpdateBeetle(Critter c, float dt)
    {
        Vector3 pos = c.root.position;
        Vector3 away = pos - player.position;
        away.y = 0f;
        float dist = away.magnitude;

        if (dist > beetleRange * 1.3f)
        {
            Place(c, Random.Range(4f, beetleRange));
            return;
        }

        bool scared = dist < 1.2f;
        c.timer -= dt;
        if (c.timer <= 0f)
        {
            c.mode = c.mode == 0 ? 1 : 0; // alternate crawling and pausing
            c.timer = c.mode == 1 ? Random.Range(0.5f, 3f) : Random.Range(0.3f, 2.5f);
            c.heading = Quaternion.Euler(0f, Random.Range(-90f, 90f), 0f) * c.heading;
            c.speed = Random.Range(0.08f, 0.25f);
        }

        if (scared)
        {
            c.mode = 1;
            c.heading = away.normalized;
            c.speed = 0.5f;
        }

        if (c.mode == 1)
        {
            // Wander: slowly curve left and right.
            c.phase += dt;
            c.heading = Quaternion.Euler(0f, (Mathf.PerlinNoise(c.phase * 0.7f, c.seed) - 0.5f) * 180f * dt, 0f) * c.heading;
            pos += c.heading * (c.speed * dt);
            c.body.localPosition = new Vector3(0f, 0.012f + Mathf.Abs(Mathf.Sin(c.phase * 40f)) * 0.002f, 0f);
        }

        pos.y = GroundHeight(pos);
        c.root.SetPositionAndRotation(pos, Quaternion.LookRotation(c.heading));
    }

    Critter BuildBeetle()
    {
        var c = NewCritter("Beetle");
        c.body = new GameObject("Body").transform; // unscaled, so the legs aren't squashed by the shell's scale
        c.body.SetParent(c.root, false);
        c.body.localPosition = new Vector3(0f, 0.012f, 0f);
        Part(PrimitiveType.Sphere, beetleMat, c.body, Vector3.zero, new Vector3(0.04f, 0.022f, 0.06f));
        Part(PrimitiveType.Sphere, beetleMat, c.body, new Vector3(0f, 0.002f, 0.035f), new Vector3(0.022f, 0.016f, 0.02f));
        for (int side = -1; side <= 1; side += 2)
        {
            var antenna = Part(PrimitiveType.Cube, beetleMat, c.body, new Vector3(side * 0.008f, 0.006f, 0.055f), new Vector3(0.002f, 0.002f, 0.03f));
            antenna.localRotation = Quaternion.Euler(-20f, side * 25f, 0f);
            for (int leg = -1; leg <= 1; leg++)
            {
                var l = Part(PrimitiveType.Cube, beetleMat, c.body, new Vector3(side * 0.025f, -0.006f, leg * 0.016f), new Vector3(0.025f, 0.002f, 0.003f));
                l.localRotation = Quaternion.Euler(0f, leg * 20f * side, side * -25f);
            }
        }
        c.heading = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
        c.timer = Random.Range(0f, 2f);
        return c;
    }

    // ---------------------------------------------------------------- Rats

    void UpdateRat(Critter c, float dt)
    {
        Vector3 pos = c.root.position;
        Vector3 away = pos - player.position;
        away.y = 0f;
        float dist = away.magnitude;

        if (dist > ratRange * 1.4f)
        {
            Place(c, Random.Range(10f, ratRange));
            c.mode = RatIdle;
            return;
        }

        if (dist < ratFleeDistance && c.mode != RatFlee)
        {
            // Scatter away from the player, veering to one side.
            Vector3 dir = Quaternion.Euler(0f, Random.Range(-40f, 40f), 0f) * away.normalized;
            c.target = pos + dir * Random.Range(6f, 10f);
            c.speed = ratSpeed * 1.4f;
            c.mode = RatFlee;
            c.timer = 3f;
            Play(c.audio, squeakSound != null ? squeakSound : ProceduralAudio.Squeak, Random.Range(0.9f, 1.2f));
        }

        c.timer -= dt;
        if (c.mode == RatIdle)
        {
            // Sniffing about: small twitchy turns.
            if (Random.value < 2f * dt)
                c.heading = Quaternion.Euler(0f, Random.Range(-50f, 50f), 0f) * c.heading;
            c.body.localPosition = new Vector3(0f, 0.045f, Mathf.Sin(Time.time * 12f + c.phase) * 0.004f);

            if (c.timer <= 0f)
            {
                Vector3 dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
                c.target = pos + dir * Random.Range(2f, 6f);
                c.speed = ratSpeed * Random.Range(0.7f, 1f);
                c.mode = RatDash;
                c.timer = 3f;
                if (Random.value < 0.5f) Play(c.audio, skitterSound != null ? skitterSound : ProceduralAudio.Skitter, Random.Range(0.9f, 1.1f));
            }
        }
        else
        {
            Vector3 to = c.target - pos;
            to.y = 0f;
            if (to.magnitude < 0.2f || c.timer <= 0f)
            {
                c.mode = RatIdle;
                c.timer = Random.Range(1f, 4f);
            }
            else
            {
                c.heading = to.normalized;
                pos += c.heading * (c.speed * dt);
                c.phase += dt * c.speed * 6f;
                c.body.localPosition = new Vector3(0f, 0.045f + Mathf.Abs(Mathf.Sin(c.phase)) * 0.02f, 0f);
            }
        }

        // Tail swish.
        c.parts[0].localRotation = Quaternion.Euler(-100f, Mathf.Sin(Time.time * (c.mode == RatIdle ? 2f : 10f) + c.phase) * 25f, 0f);

        pos.y = GroundHeight(pos);
        c.root.SetPositionAndRotation(pos, Quaternion.LookRotation(c.heading));
    }

    Critter BuildRat()
    {
        var c = NewCritter("Rat");
        c.body = new GameObject("Body").transform;
        c.body.SetParent(c.root, false);
        c.body.localPosition = new Vector3(0f, 0.045f, 0f);

        var torso = Part(PrimitiveType.Capsule, ratMat, c.body, Vector3.zero, new Vector3(0.09f, 0.08f, 0.09f));
        torso.localRotation = Quaternion.Euler(90f, 0f, 0f);
        Part(PrimitiveType.Sphere, ratMat, c.body, new Vector3(0f, 0.01f, 0.09f), new Vector3(0.06f, 0.055f, 0.07f));
        Part(PrimitiveType.Sphere, ratMat, c.body, new Vector3(0f, 0f, 0.13f), new Vector3(0.025f, 0.025f, 0.035f));
        for (int side = -1; side <= 1; side += 2)
        {
            Part(PrimitiveType.Sphere, ratMat, c.body, new Vector3(side * 0.02f, 0.04f, 0.085f), new Vector3(0.03f, 0.03f, 0.008f));
            Part(PrimitiveType.Sphere, eyeMat, c.body, new Vector3(side * 0.02f, 0.022f, 0.115f), Vector3.one * 0.008f);
        }

        // Tail, pivoting from the rump.
        var tailPivot = new GameObject("Tail").transform;
        tailPivot.SetParent(c.body, false);
        tailPivot.localPosition = new Vector3(0f, -0.005f, -0.08f);
        Part(PrimitiveType.Cylinder, ratMat, tailPivot, new Vector3(0f, 0.09f, 0f), new Vector3(0.012f, 0.09f, 0.012f));
        c.parts = new[] { tailPivot };

        c.audio = ProceduralAudio.AddSource(c.root.gameObject, null, false, true, volume, 2f, 25f);
        c.heading = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
        c.timer = Random.Range(0f, 3f);
        c.phase = Random.Range(0f, 10f);
        return c;
    }

    // ---------------------------------------------------------------- Moths

    void UpdateMoths()
    {
        int needed = 0;
        foreach (var lamp in Lamp.All) if (lamp.IsLit) needed += mothsPerLamp;
        while (moths.Count < needed) moths.Add(BuildMoth());

        int index = 0;
        float time = Time.time;
        foreach (var lamp in Lamp.All)
        {
            if (!lamp.IsLit) continue;
            Vector3 bulb = lamp.BulbPosition;

            for (int i = 0; i < mothsPerLamp; i++, index++)
            {
                Critter m = moths[index];
                if (!m.root.gameObject.activeSelf) m.root.gameObject.SetActive(true);

                // Erratic orbits around the bulb, darting in and out.
                float o = m.phase;
                float angle = time * (2f + o % 1.5f) * (i % 2 == 0 ? 1f : -1f) + o * 7f;
                float radius = 0.3f + 0.25f * Mathf.Sin(time * 1.7f + o * 3f) + 0.15f * Mathf.PerlinNoise(time * 3f, o);
                Vector3 p = bulb + new Vector3(Mathf.Cos(angle) * radius, 0.15f * Mathf.Sin(time * 2.3f + o * 5f) - 0.05f, Mathf.Sin(angle) * radius);

                Vector3 velocity = p - m.root.position;
                m.root.position = p;
                if (velocity.sqrMagnitude > 0.000001f) m.root.rotation = Quaternion.LookRotation(velocity);

                float flap = Mathf.Sin(time * 45f + o * 10f) * 60f;
                m.parts[0].localRotation = Quaternion.Euler(0f, 0f, flap);
                m.parts[1].localRotation = Quaternion.Euler(0f, 0f, -flap);
            }
        }

        for (; index < moths.Count; index++)
            if (moths[index].root.gameObject.activeSelf) moths[index].root.gameObject.SetActive(false);
    }

    Critter BuildMoth()
    {
        var c = NewCritter("Moth");
        c.body = Part(PrimitiveType.Sphere, mothMat, c.root, Vector3.zero, new Vector3(0.008f, 0.008f, 0.022f));
        var wings = new Transform[2];
        for (int side = 0; side < 2; side++)
        {
            float s = side == 0 ? -1f : 1f;
            var pivot = new GameObject("Wing").transform;
            pivot.SetParent(c.root, false);
            Part(PrimitiveType.Cube, mothMat, pivot, new Vector3(s * 0.016f, 0f, 0f), new Vector3(0.03f, 0.002f, 0.022f));
            wings[side] = pivot;
        }
        c.parts = wings;
        c.phase = Random.Range(0f, 100f);
        c.root.gameObject.SetActive(false);
        return c;
    }

    // ---------------------------------------------------------------- Bats

    void UpdateBats(float dt)
    {
        batTimer -= dt;
        if (batTimer <= 0f)
        {
            batTimer = Random.Range(minBatInterval, maxBatInterval);
            LaunchFlock();
        }

        foreach (var bat in bats)
        {
            if (!bat.active) continue;
            Critter c = bat.critter;

            bat.progress += dt / bat.duration;
            if (bat.progress >= 1f)
            {
                bat.active = false;
                c.root.gameObject.SetActive(false);
                continue;
            }

            Vector3 dir = (bat.end - bat.start).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            float t = bat.progress * bat.duration;
            Vector3 p = Vector3.Lerp(bat.start, bat.end, bat.progress)
                + Vector3.up * Mathf.Sin(t * 3f + bat.wobble) * 0.6f
                + side * Mathf.Sin(t * 2f + bat.wobble * 2f) * 1.2f;

            Vector3 velocity = p - c.root.position;
            c.root.position = p;
            if (velocity.sqrMagnitude > 0.0001f) c.root.rotation = Quaternion.LookRotation(velocity);

            float flap = Mathf.Sin(t * 55f + bat.wobble) * 55f;
            c.parts[0].localRotation = Quaternion.Euler(0f, 0f, flap);
            c.parts[1].localRotation = Quaternion.Euler(0f, 0f, -flap);

            // A flurry of wing beats as it passes over.
            if (!bat.heard && (p - player.position).sqrMagnitude < 15f * 15f)
            {
                bat.heard = true;
                Play(c.audio, wingSound != null ? wingSound : ProceduralAudio.WingFlaps, Random.Range(0.85f, 1.15f));
            }
        }
    }

    void LaunchFlock()
    {
        Vector3 dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        int count = Random.Range(1, maxBatsPerFlock + 1);

        for (int i = 0; i < count && i < bats.Count; i++)
        {
            var bat = bats[i];
            float offset = Random.Range(-6f, 6f);
            float height = Random.Range(6f, 11f);
            Vector3 basePos = player.position + Vector3.up * height + side * offset;
            bat.start = basePos - dir * 35f + Random.insideUnitSphere * 2f;
            bat.end = basePos + dir * 35f + Random.insideUnitSphere * 2f;
            bat.duration = 70f / Random.Range(8f, 11f);
            bat.progress = -i * 0.03f; // stagger the flock slightly
            bat.wobble = Random.Range(0f, 10f);
            bat.active = true;
            bat.heard = false;
            bat.critter.root.position = bat.start;
            bat.critter.root.gameObject.SetActive(true);
        }
    }

    Critter BuildBat()
    {
        var c = NewCritter("Bat");
        c.body = Part(PrimitiveType.Sphere, batMat, c.root, Vector3.zero, new Vector3(0.08f, 0.06f, 0.12f));
        Part(PrimitiveType.Sphere, batMat, c.root, new Vector3(0f, 0.01f, 0.07f), Vector3.one * 0.05f);
        for (int side = -1; side <= 1; side += 2)
            Part(PrimitiveType.Cube, batMat, c.root, new Vector3(side * 0.015f, 0.04f, 0.075f), new Vector3(0.012f, 0.03f, 0.008f));

        var wings = new Transform[2];
        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? -1f : 1f;
            var pivot = new GameObject("Wing").transform;
            pivot.SetParent(c.root, false);
            Part(PrimitiveType.Cube, batMat, pivot, new Vector3(s * 0.18f, 0f, -0.01f), new Vector3(0.34f, 0.008f, 0.16f));
            wings[i] = pivot;
        }
        c.parts = wings;
        c.audio = ProceduralAudio.AddSource(c.root.gameObject, null, false, true, volume, 3f, 40f);
        c.root.gameObject.SetActive(false);
        return c;
    }

    // ---------------------------------------------------------------- Deer

    void PlaceDeer(int i)
    {
        Critter d = deer[i];
        // Most deer turn up near another one, so they come in small herds.
        if (i > 0 && Random.value < 0.6f && (deer[i - 1].root.position - player.position).sqrMagnitude < deerRange * deerRange)
        {
            Vector2 offset = Random.insideUnitCircle * 6f;
            Vector3 p = deer[i - 1].root.position + new Vector3(offset.x, 0f, offset.y);
            p.y = GroundHeight(p);
            d.root.position = p;
        }
        else
        {
            Place(d, Random.Range(deerRange * 0.5f, deerRange));
        }
        d.mode = DeerGraze;
        d.timer = Random.Range(2f, 8f);
        d.heading = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
        d.root.rotation = Quaternion.LookRotation(d.heading);
    }

    float deerCallTimer = -1f;

    /// <summary>Now and then, a grazing or wandering deer near the player calls: a grunt, a bleat, or a snort.</summary>
    void UpdateDeerCalls(float dt)
    {
        if (deerCallTimer < 0f) deerCallTimer = Random.Range(minDeerCallInterval, maxDeerCallInterval);
        deerCallTimer -= dt;
        if (deerCallTimer > 0f) return;
        deerCallTimer = Random.Range(minDeerCallInterval, maxDeerCallInterval);
        if (player == null || PortalDoor.PlayerInRoom || EntityDread.Silence > 0.2f) return;

        var calm = new List<Critter>();
        foreach (var d in deer)
        {
            if (d.root == null || !d.root.gameObject.activeInHierarchy) continue;
            if (d.mode != DeerGraze && d.mode != DeerWalk) continue;
            if ((d.root.position - player.position).sqrMagnitude < deerCallDistance * deerCallDistance) calm.Add(d);
        }
        if (calm.Count == 0) return;

        var caller = calm[Random.Range(0, calm.Count)];
        float roll = Random.value;
        AudioClip clip = roll < 0.5f ? ProceduralAudio.DeerGrunt : roll < 0.8f ? ProceduralAudio.DeerBleat : ProceduralAudio.DeerSnort;
        Play(caller.audio, clip, Random.Range(0.9f, 1.1f));
    }

    void UpdateDeer(int i, float dt)
    {
        Critter d = deer[i];
        Vector3 pos = d.root.position;
        Vector3 away = pos - player.position;
        away.y = 0f;
        float dist = away.magnitude;

        if (dist > deerRange * 1.4f)
        {
            PlaceDeer(i);
            return;
        }

        if (d.mode != DeerFlee)
        {
            if (dist < deerFleeDistance) StartDeerFlee(d, true);
            else if (dist < deerAlertDistance && d.mode != DeerAlert)
            {
                d.mode = DeerAlert;
                d.timer = Random.Range(1.5f, 3.5f);
                // Head up, a sharp snort through the nose: it's noticed you.
                Play(d.audio, ProceduralAudio.DeerSnort, Random.Range(0.9f, 1.1f));
            }
        }

        float speed = 0f;
        float targetNeck = 35f;
        float turnRate = 4f;
        d.timer -= dt;

        switch (d.mode)
        {
            case DeerGraze:
                targetNeck = 115f; // head down to the grass
                if (d.timer <= 0f)
                {
                    d.mode = DeerWalk;
                    Vector3 dir = Quaternion.Euler(0f, Random.Range(-120f, 120f), 0f) * d.heading;
                    d.target = pos + dir * Random.Range(4f, 12f);
                    d.timer = 15f;
                }
                break;

            case DeerWalk:
            {
                Vector3 to = d.target - pos;
                to.y = 0f;
                if (to.magnitude < 0.5f || d.timer <= 0f)
                {
                    d.mode = DeerGraze;
                    d.timer = Random.Range(4f, 10f);
                }
                else
                {
                    d.heading = Steer(pos, to.normalized, 3f);
                    speed = deerWalkSpeed;
                }
                break;
            }

            case DeerAlert:
                targetNeck = -5f; // head up, staring
                d.heading = -away.normalized;
                turnRate = 3f;
                if (d.timer <= 0f)
                {
                    if (dist < deerAlertDistance * 0.8f) StartDeerFlee(d, true);
                    else
                    {
                        d.mode = DeerGraze; // lost interest
                        d.timer = Random.Range(2f, 5f);
                    }
                }
                break;

            case DeerFlee:
                targetNeck = 15f;
                turnRate = 6f;
                d.heading = Steer(pos, Vector3.Slerp(d.heading, away.normalized, dt * 0.5f).normalized, 6f);
                speed = deerGallopSpeed;
                if (d.timer <= 0f && dist > deerAlertDistance * 1.5f)
                {
                    d.mode = DeerWalk;
                    d.target = pos + d.heading * Random.Range(5f, 10f);
                    d.timer = 15f;
                }
                break;
        }

        pos += d.heading * (speed * dt);
        pos.y = GroundHeight(pos);
        d.root.position = pos;
        d.root.rotation = Quaternion.Slerp(d.root.rotation, Quaternion.LookRotation(d.heading), 1f - Mathf.Exp(-turnRate * dt));

        d.loop.volume = Mathf.MoveTowards(d.loop.volume, d.mode == DeerFlee ? volume : 0f, dt * 2f);
        if (d.loop.volume <= 0f && d.loop.isPlaying) d.loop.Stop();

        AnimateDeer(d, speed, targetNeck, dt);
    }

    BatterySpawner batterySpawner;

    void StartDeerFlee(Critter d, bool alertHerd)
    {
        if (d.mode == DeerFlee) return;

        Vector3 away = d.root.position - player.position;
        away.y = 0f;
        d.heading = Quaternion.Euler(0f, Random.Range(-30f, 30f), 0f) * away.normalized;
        d.mode = DeerFlee;
        d.timer = Random.Range(5f, 8f);
        Play(d.audio, deerAlarmSound != null ? deerAlarmSound : ProceduralAudio.DeerAlarm, Random.Range(0.85f, 1.1f));
        if (!d.loop.isPlaying) d.loop.Play();

        // A deer you startle drops a battery as it bolts (only the one you spooked, not the herd).
        if (alertHerd)
        {
            if (batterySpawner == null) batterySpawner = FindAnyObjectByType<BatterySpawner>();
            if (batterySpawner != null) batterySpawner.DropFromDeer(d.root.position);
        }

        // One spooked deer sends the rest of the herd running.
        if (!alertHerd) return;
        foreach (var other in deer)
            if (other != d && (other.root.position - d.root.position).sqrMagnitude < 25f * 25f)
                StartDeerFlee(other, false);
    }

    /// <summary>Turns away from trees ahead, so deer don't run straight through trunks.</summary>
    Vector3 Steer(Vector3 pos, Vector3 heading, float lookAhead)
    {
        Vector3 origin = pos + Vector3.up * 1f;
        for (int attempt = 0; attempt < 4; attempt++)
        {
            if (!Physics.SphereCast(origin, 0.4f, heading, out var hit, lookAhead, ~0, QueryTriggerInteraction.Ignore))
                return heading;
            float side = Vector3.Cross(heading, hit.normal).y >= 0f ? -1f : 1f;
            heading = Quaternion.Euler(0f, side * 45f, 0f) * heading;
        }
        return heading;
    }

    void AnimateDeer(Critter d, float speed, float targetNeck, float dt)
    {
        Transform[] p = d.parts;
        bool galloping = speed > deerWalkSpeed * 2f;
        float bob = 0f, pitch = 0f;

        if (galloping)
        {
            // Bounding gait: front legs together, then back legs together.
            d.phase += dt * Mathf.PI * 2f * 2.4f;
            float front = Mathf.Sin(d.phase) * 45f;
            float back = Mathf.Sin(d.phase + 2.2f) * 45f;
            p[FrontLeft].localRotation = Quaternion.Euler(front, 0f, 0f);
            p[FrontRight].localRotation = Quaternion.Euler(Mathf.Sin(d.phase + 0.3f) * 45f, 0f, 0f);
            p[BackLeft].localRotation = Quaternion.Euler(back, 0f, 0f);
            p[BackRight].localRotation = Quaternion.Euler(Mathf.Sin(d.phase + 2.5f) * 45f, 0f, 0f);
            bob = Mathf.Abs(Mathf.Sin(d.phase)) * 0.15f;
            pitch = Mathf.Sin(d.phase + 1f) * 6f;
        }
        else if (speed > 0f)
        {
            // Walk: diagonal pairs move together.
            d.phase += dt * speed * 4f;
            float swing = Mathf.Sin(d.phase) * 22f;
            p[FrontLeft].localRotation = Quaternion.Euler(swing, 0f, 0f);
            p[BackRight].localRotation = Quaternion.Euler(swing, 0f, 0f);
            p[FrontRight].localRotation = Quaternion.Euler(-swing, 0f, 0f);
            p[BackLeft].localRotation = Quaternion.Euler(-swing, 0f, 0f);
            bob = Mathf.Abs(Mathf.Sin(d.phase)) * 0.03f;
        }
        else
        {
            for (int leg = FrontLeft; leg <= BackRight; leg++)
                p[leg].localRotation = Quaternion.Slerp(p[leg].localRotation, Quaternion.identity, 1f - Mathf.Exp(-8f * dt));
        }

        d.body.localPosition = new Vector3(0f, 1f + bob, 0f);
        d.body.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        // Neck and head: down to graze, up to watch. The head stays roughly level.
        d.neckAngle = Mathf.MoveTowards(d.neckAngle, targetNeck, dt * 90f);
        float nibble = d.mode == DeerGraze && Mathf.Abs(d.neckAngle - targetNeck) < 1f ? Mathf.Sin(Time.time * 6f + d.phase) * 4f : 0f;
        p[Neck].localRotation = Quaternion.Euler(d.neckAngle + nibble, 0f, 0f);
        p[Head].localRotation = Quaternion.Euler(-d.neckAngle * 0.75f + 15f, 0f, 0f);

        // White-tail flag goes up when spooked.
        float tail = d.mode == DeerFlee || d.mode == DeerAlert ? -60f : 10f;
        p[Tail].localRotation = Quaternion.Slerp(p[Tail].localRotation, Quaternion.Euler(tail, 0f, 0f), 1f - Mathf.Exp(-6f * dt));
    }

    Critter BuildDeer()
    {
        var d = NewCritter("Deer");
        float size = Random.Range(0.9f, 1.15f);
        d.root.localScale = Vector3.one * size;

        d.body = new GameObject("Body").transform;
        d.body.SetParent(d.root, false);
        d.body.localPosition = new Vector3(0f, 1f, 0f);

        var torso = Part(PrimitiveType.Capsule, deerMat, d.body, Vector3.zero, new Vector3(0.42f, 0.6f, 0.48f));
        torso.localRotation = Quaternion.Euler(90f, 0f, 0f);

        var parts = new Transform[7];

        // Legs pivot at the hips and shoulders.
        Vector3[] hips =
        {
            new Vector3(-0.13f, -0.1f, 0.42f), new Vector3(0.13f, -0.1f, 0.42f),
            new Vector3(-0.13f, -0.1f, -0.42f), new Vector3(0.13f, -0.1f, -0.42f),
        };
        for (int i = 0; i < 4; i++)
        {
            var hip = new GameObject("Leg").transform;
            hip.SetParent(d.body, false);
            hip.localPosition = hips[i];
            bool back = i >= 2;
            Part(PrimitiveType.Capsule, deerMat, hip, new Vector3(0f, -0.15f, back ? -0.03f : 0f), new Vector3(back ? 0.16f : 0.12f, 0.2f, back ? 0.2f : 0.13f));
            Part(PrimitiveType.Cylinder, deerMat, hip, new Vector3(0f, -0.5f, 0f), new Vector3(0.06f, 0.4f, 0.06f));
            Part(PrimitiveType.Cube, deerMat, hip, new Vector3(0f, -0.88f, 0.02f), new Vector3(0.07f, 0.05f, 0.09f));
            parts[i] = hip;
        }

        // Neck, angled up and forward from the chest.
        var neck = new GameObject("Neck").transform;
        neck.SetParent(d.body, false);
        neck.localPosition = new Vector3(0f, 0.12f, 0.5f);
        Part(PrimitiveType.Cylinder, deerMat, neck, new Vector3(0f, 0.27f, 0f), new Vector3(0.14f, 0.28f, 0.16f));
        parts[Neck] = neck;

        // Head on top of the neck.
        var head = new GameObject("Head").transform;
        head.SetParent(neck, false);
        head.localPosition = new Vector3(0f, 0.55f, 0f);
        Part(PrimitiveType.Sphere, deerMat, head, new Vector3(0f, 0f, 0.08f), new Vector3(0.16f, 0.17f, 0.24f));
        Part(PrimitiveType.Sphere, deerMat, head, new Vector3(0f, -0.03f, 0.22f), new Vector3(0.09f, 0.09f, 0.16f));
        for (int side = -1; side <= 1; side += 2)
        {
            var ear = Part(PrimitiveType.Sphere, deerMat, head, new Vector3(side * 0.08f, 0.1f, -0.01f), new Vector3(0.05f, 0.13f, 0.02f));
            ear.localRotation = Quaternion.Euler(0f, 0f, side * -35f);
            Part(PrimitiveType.Sphere, deerEyeMat, head, new Vector3(side * 0.065f, 0.03f, 0.12f), Vector3.one * 0.022f);
        }
        if (Random.value < 0.5f) AddAntlers(head);
        parts[Head] = head;

        var tail = new GameObject("Tail").transform;
        tail.SetParent(d.body, false);
        tail.localPosition = new Vector3(0f, 0.12f, -0.6f);
        Part(PrimitiveType.Sphere, deerMat, tail, new Vector3(0f, -0.06f, -0.02f), new Vector3(0.07f, 0.14f, 0.05f));
        parts[Tail] = tail;

        d.parts = parts;
        d.neckAngle = 35f;
        d.phase = Random.Range(0f, 10f);
        d.audio = ProceduralAudio.AddSource(d.root.gameObject, null, false, true, volume, 4f, 50f);
        d.loop = ProceduralAudio.AddSource(d.root.gameObject, gallopLoop != null ? gallopLoop : ProceduralAudio.Gallop, true, true, 0f, 4f, 50f);
        d.loop.pitch = Random.Range(0.9f, 1.1f);
        return d;
    }

    void AddAntlers(Transform head)
    {
        for (int side = -1; side <= 1; side += 2)
        {
            var beam = new GameObject("Antler").transform;
            beam.SetParent(head, false);
            beam.localPosition = new Vector3(side * 0.05f, 0.1f, 0f);
            beam.localRotation = Quaternion.Euler(-25f, 0f, side * -30f);
            Part(PrimitiveType.Cylinder, deerMat, beam, new Vector3(0f, 0.18f, 0f), new Vector3(0.025f, 0.18f, 0.025f));
            for (int tine = 0; tine < 2; tine++)
            {
                var t = Part(PrimitiveType.Cylinder, deerMat, beam, new Vector3(0f, 0.14f + tine * 0.13f, 0.03f), new Vector3(0.018f, 0.07f, 0.018f));
                t.localRotation = Quaternion.Euler(45f, 0f, 0f);
                t.localPosition += t.localRotation * Vector3.up * 0.06f; // base of the tine sits on the beam
            }
        }
    }

    // ---------------------------------------------------------------- Crows

    void UpdateCrows(float dt)
    {
        crowTimer -= dt;
        if (crowTimer <= 0f)
        {
            crowTimer = Random.Range(minCrowInterval, maxCrowInterval);
            LaunchCrows();
        }

        foreach (var crow in crows)
        {
            if (!crow.active) continue;
            Critter c = crow.critter;

            crow.progress += dt / crow.duration;
            if (crow.progress >= 1f)
            {
                crow.active = false;
                c.root.gameObject.SetActive(false);
                continue;
            }

            float t = Mathf.Max(0f, crow.progress) * crow.duration;
            Vector3 dir = (crow.end - crow.start).normalized;
            Vector3 side = Vector3.Cross(Vector3.up, dir);
            Vector3 p = Vector3.Lerp(crow.start, crow.end, crow.progress)
                + Vector3.up * Mathf.Sin(t * 0.8f + crow.wobble) * 1.2f
                + side * Mathf.Sin(t * 0.5f + crow.wobble) * 3f;

            Vector3 velocity = p - c.root.position;
            c.root.position = p;
            if (velocity.sqrMagnitude > 0.0001f)
                c.root.rotation = Quaternion.LookRotation(velocity) * Quaternion.Euler(0f, 0f, Mathf.Sin(t * 0.5f + crow.wobble) * -15f);

            // Bursts of wingbeats, then glides with wings held out.
            bool gliding = Mathf.Sin(t * 0.9f + crow.wobble * 3f) > 0.3f;
            float flap = gliding ? 8f : Mathf.Sin(t * 22f + crow.wobble) * 45f;
            c.parts[0].localRotation = Quaternion.Euler(0f, 0f, flap);
            c.parts[1].localRotation = Quaternion.Euler(0f, 0f, -flap);

            if (!crow.heard && (p - player.position).sqrMagnitude < 50f * 50f)
            {
                crow.heard = true;
                if (Random.value < 0.6f) StartCoroutine(Caws(c.audio, Random.Range(1, 4)));
            }
        }
    }

    System.Collections.IEnumerator Caws(AudioSource source, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Play(source, cawSound != null ? cawSound : ProceduralAudio.Caw, Random.Range(0.85f, 1.1f));
            yield return new WaitForSeconds(Random.Range(0.45f, 0.9f));
        }
    }

    void LaunchCrows()
    {
        Vector3 dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, dir);
        int count = Random.Range(minCrowsPerFlock, maxCrowsPerFlock + 1);
        float height = Random.Range(18f, 32f);
        float speed = Random.Range(9f, 12f);
        Vector3 basePos = player.position + Vector3.up * height + side * Random.Range(-25f, 25f);

        for (int i = 0; i < count && i < crows.Count; i++)
        {
            var crow = crows[i];
            // A loose, straggling flock.
            Vector3 offset = side * Random.Range(-8f, 8f) + Vector3.up * Random.Range(-3f, 3f) - dir * Random.Range(0f, 12f);
            crow.start = basePos - dir * 70f + offset;
            crow.end = basePos + dir * 70f + offset;
            crow.duration = 140f / speed;
            crow.progress = -Random.Range(0f, 0.05f);
            crow.wobble = Random.Range(0f, 10f);
            crow.active = true;
            crow.heard = false;
            crow.critter.root.position = crow.start;
            crow.critter.root.gameObject.SetActive(true);
        }
    }

    Critter BuildCrow()
    {
        var c = NewCritter("Crow");
        c.root.localScale = Vector3.one * crowSize;
        c.body = Part(PrimitiveType.Sphere, batMat, c.root, Vector3.zero, new Vector3(0.14f, 0.12f, 0.32f));
        Part(PrimitiveType.Sphere, batMat, c.root, new Vector3(0f, 0.03f, 0.19f), Vector3.one * 0.1f);       // head
        Part(PrimitiveType.Cube, batMat, c.root, new Vector3(0f, 0.02f, 0.27f), new Vector3(0.025f, 0.03f, 0.08f)); // beak
        Part(PrimitiveType.Cube, batMat, c.root, new Vector3(0f, 0f, -0.24f), new Vector3(0.14f, 0.01f, 0.16f));     // tail fan

        var wings = new Transform[2];
        for (int i = 0; i < 2; i++)
        {
            float s = i == 0 ? -1f : 1f;
            var pivot = new GameObject("Wing").transform;
            pivot.SetParent(c.root, false);
            pivot.localPosition = new Vector3(s * 0.05f, 0.02f, 0.02f);
            Part(PrimitiveType.Cube, batMat, pivot, new Vector3(s * 0.28f, 0f, -0.02f), new Vector3(0.52f, 0.01f, 0.2f));
            Part(PrimitiveType.Cube, batMat, pivot, new Vector3(s * 0.5f, 0f, -0.07f), new Vector3(0.12f, 0.008f, 0.12f)); // splayed tip feathers
            wings[i] = pivot;
        }
        c.parts = wings;
        c.audio = ProceduralAudio.AddSource(c.root.gameObject, null, false, true, volume, 8f, 90f);
        c.root.gameObject.SetActive(false);
        return c;
    }

    // ---------------------------------------------------------------- Owls

    void PlaceOwls()
    {
        if (generator == null) return;

        // Only the generated pines and dead trees (they have a known shape to perch on).
        var trees = new List<Transform>();
        foreach (var pivot in generator.SwayPivots)
        {
            if (pivot == null || pivot.parent == null) continue;
            string kind = pivot.parent.name;
            if ((kind == "Pine" || kind == "Dead Tree") && pivot.parent.GetComponent<CapsuleCollider>() != null)
                trees.Add(pivot);
        }

        for (int i = 0; i < owlCount && trees.Count > 0; i++)
        {
            int pick = Random.Range(0, trees.Count);
            owls.Add(BuildOwl(trees[pick]));
            trees.RemoveAt(pick);
        }
    }

    void UpdateOwls(float dt)
    {
        var cam = Camera.main;
        if (cam == null) return;
        Vector3 eye = cam.transform.position;
        UpdateOwlCalls(eye, dt);
        float rangeSq = owlStareDistance * owlStareDistance;

        foreach (var owl in owls)
        {
            if (owl.head == null) continue;
            Vector3 to = eye - owl.head.position;
            if (to.sqrMagnitude > rangeSq) continue;

            // Every so often the head cocks to a new angle, owl-like, while the stare never breaks.
            owl.tiltTimer -= dt;
            if (owl.tiltTimer <= 0f)
            {
                owl.tiltTimer = Random.Range(1.5f, 5f);
                owl.tilt = Random.value < 0.4f ? 0f : Random.Range(-35f, 35f);
            }
            Quaternion look = Quaternion.LookRotation(to) * Quaternion.Euler(0f, 0f, owl.tilt);
            owl.head.rotation = Quaternion.Slerp(owl.head.rotation, look, 1f - Mathf.Exp(-10f * dt));
        }
    }

    /// <summary>Every so often, one owl somewhere near the player calls out from its branch.</summary>
    void UpdateOwlCalls(Vector3 eye, float dt)
    {
        if (owlCallTimer < 0f) owlCallTimer = Random.Range(minOwlCallInterval, maxOwlCallInterval);
        owlCallTimer -= dt;
        if (owlCallTimer > 0f) return;
        owlCallTimer = Random.Range(minOwlCallInterval, maxOwlCallInterval);
        if (PortalDoor.PlayerInRoom || EntityDread.Silence > 0.2f) return; // nothing calls while one of them is close

        var nearby = new List<Owl>();
        float rangeSq = owlCallDistance * owlCallDistance;
        foreach (var owl in owls)
            if (owl.head != null && (owl.head.position - eye).sqrMagnitude < rangeSq) nearby.Add(owl);
        if (nearby.Count == 0) return;

        var caller = nearby[Random.Range(0, nearby.Count)];
        bool screech = Random.value < owlScreechChance;
        ProceduralAudio.PlayAt(screech ? ProceduralAudio.OwlScreech : ProceduralAudio.OwlHoot, caller.head.position,
            owlCallVolume, Random.Range(0.92f, 1.08f), 12f);
        caller.tilt = Random.Range(-20f, 20f); // the head bobs as it calls
    }

    Owl BuildOwl(Transform sway)
    {
        Transform tree = sway.parent;
        var trunk = tree.GetComponent<CapsuleCollider>();
        float height = trunk.height, radius = trunk.radius;

        // Pines are only bare low down, below the foliage; dead trees are bare all the way up.
        bool pine = tree.name == "Pine";
        float perchHeight = height * (pine ? Random.Range(0.18f, 0.26f) : Random.Range(0.4f, 0.65f));

        Material bark = owlMat;
        foreach (var r in sway.GetComponentsInChildren<Renderer>())
            if (r.name == "Trunk" || r.name == "Bark") { bark = r.sharedMaterial; break; }

        var perch = new GameObject("Owl Perch").transform;
        perch.SetParent(sway, false);
        perch.localPosition = new Vector3(0f, perchHeight, 0f);
        perch.localRotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

        // A bare branch sticking out of the trunk, drooping slightly, with a twig.
        float length = Random.Range(0.9f, 1.5f);
        var branch = Part(PrimitiveType.Cylinder, bark, perch, new Vector3(0f, -0.04f, radius + length * 0.5f), new Vector3(0.07f, length * 0.5f, 0.07f));
        branch.localRotation = Quaternion.Euler(93f, 0f, 0f);
        var twig = Part(PrimitiveType.Cylinder, bark, perch, new Vector3(0.12f, 0.08f, radius + length * 0.85f), new Vector3(0.025f, 0.18f, 0.025f));
        twig.localRotation = Quaternion.Euler(40f, 0f, -50f);

        // The owl, near the end of the branch.
        var body = new GameObject("Owl").transform;
        body.SetParent(perch, false);
        body.localPosition = new Vector3(0f, 0f, radius + length * 0.7f);
        Part(PrimitiveType.Sphere, owlMat, body, new Vector3(0f, 0.16f, 0f), new Vector3(0.2f, 0.3f, 0.18f));
        for (int side = -1; side <= 1; side += 2)
        {
            var wing = Part(PrimitiveType.Sphere, owlMat, body, new Vector3(side * 0.09f, 0.15f, -0.01f), new Vector3(0.06f, 0.24f, 0.15f));
            wing.localRotation = Quaternion.Euler(-8f, 0f, side * 6f);
            Part(PrimitiveType.Cube, owlMat, body, new Vector3(side * 0.04f, 0.01f, 0.05f), new Vector3(0.03f, 0.02f, 0.05f)); // talons
        }
        Part(PrimitiveType.Sphere, owlMat, body, new Vector3(0f, 0.04f, -0.09f), new Vector3(0.08f, 0.12f, 0.03f)); // tail

        var head = new GameObject("Head").transform;
        head.SetParent(body, false);
        head.localPosition = new Vector3(0f, 0.34f, 0f);
        Part(PrimitiveType.Sphere, owlMat, head, Vector3.zero, new Vector3(0.18f, 0.16f, 0.16f));
        for (int side = -1; side <= 1; side += 2)
        {
            var tuft = Part(PrimitiveType.Sphere, owlMat, head, new Vector3(side * 0.055f, 0.085f, 0f), new Vector3(0.03f, 0.08f, 0.02f));
            tuft.localRotation = Quaternion.Euler(0f, 0f, side * -22f);
            Part(PrimitiveType.Sphere, owlEyeMat, head, new Vector3(side * 0.037f, 0.015f, 0.068f), Vector3.one * 0.042f);
            Part(PrimitiveType.Sphere, pupilMat, head, new Vector3(side * 0.037f, 0.015f, 0.087f), Vector3.one * 0.016f);
        }
        Part(PrimitiveType.Sphere, owlMat, head, new Vector3(0f, -0.015f, 0.08f), new Vector3(0.015f, 0.03f, 0.02f)); // beak

        return new Owl { head = head, tiltTimer = Random.Range(0f, 3f) };
    }

    // ---------------------------------------------------------------- Helpers

    /// <summary>Puts a ground creature somewhere around the player at the given distance.</summary>
    void Place(Critter c, float distance)
    {
        Vector2 dir = Random.insideUnitCircle.normalized;
        Vector3 p = player.position + new Vector3(dir.x, 0f, dir.y) * distance;
        p.y = GroundHeight(p);
        c.root.position = p;
    }

    float GroundHeight(Vector3 p)
    {
        if (generator != null) return generator.GetGroundHeight(p.x, p.z);
        if (Physics.Raycast(p + Vector3.up * 20f, Vector3.down, out var hit, 50f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return p.y;
    }

    void Play(AudioSource source, AudioClip clip, float pitch)
    {
        if (source == null || clip == null) return;
        source.pitch = pitch;
        source.PlayOneShot(clip, volume);
    }

    Critter NewCritter(string name)
    {
        var root = new GameObject(name).transform;
        root.SetParent(transform, false);
        WorldWrap.RegisterDynamic(root);
        return new Critter { root = root, seed = Random.Range(0f, 1000f) };
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
