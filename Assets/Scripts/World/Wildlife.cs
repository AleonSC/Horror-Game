using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Small creatures that make the forest feel alive (and make you jump):
/// - Beetles: little black bugs crawling around on the ground near the player.
/// - Rats: sniff around, dart between spots, and scatter, squeaking, if you get close.
/// - Moths: flutter around the bulbs of lit lamps.
/// - Bats: every so often a few swoop overhead with a flurry of wing beats.
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

    [Header("Audio")]
    [Range(0f, 1f)] public float volume = 0.7f;
    [Tooltip("Leave clips empty to use the built-in synthesized sounds.")]
    public AudioClip squeakSound;
    public AudioClip skitterSound;
    public AudioClip wingSound;

    class Critter
    {
        public Transform root;
        public Transform body;
        public Transform[] parts; // legs / wings / tail, depending on the creature
        public AudioSource audio;
        public Vector3 heading = Vector3.forward;
        public Vector3 target;
        public float speed;
        public float timer;
        public float phase;
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

    DarkForestGenerator generator;
    Transform player;
    float batTimer;
    Material beetleMat, ratMat, eyeMat, mothMat, batMat;

    const int RatIdle = 0, RatDash = 1, RatFlee = 2;

    void Start()
    {
        generator = FindAnyObjectByType<DarkForestGenerator>();

        beetleMat = MaterialFactory.Lit(new Color(0.02f, 0.02f, 0.025f), 0.7f);
        ratMat = MaterialFactory.Lit(new Color(0.06f, 0.05f, 0.045f), 0.15f);
        eyeMat = MaterialFactory.Unlit(new Color(0.5f, 0.15f, 0.1f));
        mothMat = MaterialFactory.Lit(new Color(0.12f, 0.1f, 0.08f), 0.05f);
        batMat = MaterialFactory.Lit(new Color(0.02f, 0.02f, 0.02f), 0.1f);

        for (int i = 0; i < beetleCount; i++) beetles.Add(BuildBeetle());
        for (int i = 0; i < ratCount; i++) rats.Add(BuildRat());
        for (int i = 0; i < maxBatsPerFlock; i++) bats.Add(new Bat { critter = BuildBat() });

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
        }

        float dt = Time.deltaTime;
        foreach (var b in beetles) UpdateBeetle(b, dt);
        foreach (var r in rats) UpdateRat(r, dt);
        UpdateMoths();
        UpdateBats(dt);
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
            c.heading = Quaternion.Euler(0f, (Mathf.PerlinNoise(c.phase * 0.7f, c.root.GetInstanceID() * 0.01f) - 0.5f) * 180f * dt, 0f) * c.heading;
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
        return new Critter { root = root };
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
