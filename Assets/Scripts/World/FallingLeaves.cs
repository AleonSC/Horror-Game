using UnityEngine;

/// <summary>
/// Leaves drifting down from the canopy around the player: they tumble and flutter as they fall,
/// get carried along by the wind (WeatherSystem), settle on the ground, and get picked up and
/// skittered along again by strong gusts. Leaves that end up far behind are recycled near the player.
/// DarkForestGenerator adds this automatically.
/// </summary>
public class FallingLeaves : MonoBehaviour
{
    public int leafCount = 140;
    [Tooltip("Leaves fall within this distance of the player.")]
    public float radius = 25f;
    public float minFallHeight = 6f;
    public float maxFallHeight = 14f;
    [Tooltip("How fast leaves sink, in metres per second.")]
    public float fallSpeed = 0.9f;
    [Tooltip("How strongly the wind carries them.")]
    public float windCarry = 2.5f;
    [Tooltip("Seconds a leaf lies on the ground before being recycled.")]
    public float minRestTime = 6f;
    public float maxRestTime = 20f;

    enum State { Falling, Resting, Blowing }

    class Leaf
    {
        public Transform transform;
        public State state;
        public float timer, phase, flutter, spin;
        public Vector3 drift;
    }

    Leaf[] leaves;
    DarkForestGenerator generator;
    WeatherSystem weather;
    Transform player;

    void Start()
    {
        generator = FindAnyObjectByType<DarkForestGenerator>();
        weather = FindAnyObjectByType<WeatherSystem>();

        // A few autumn shades, dark to suit the night.
        var materials = new[]
        {
            MaterialFactory.Lit(new Color(0.16f, 0.08f, 0.03f), 0.1f),
            MaterialFactory.Lit(new Color(0.22f, 0.12f, 0.03f), 0.1f),
            MaterialFactory.Lit(new Color(0.12f, 0.1f, 0.04f), 0.1f),
            MaterialFactory.Lit(new Color(0.2f, 0.05f, 0.02f), 0.1f),
        };

        leaves = new Leaf[leafCount];
        for (int i = 0; i < leafCount; i++)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Leaf";
            go.transform.SetParent(transform, false);
            float size = Random.Range(0.6f, 1.3f);
            go.transform.localScale = new Vector3(0.06f * size, 0.002f, 0.045f * size); // thin, so both faces show
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = materials[Random.Range(0, materials.Length)];
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            Destroy(go.GetComponent<Collider>());
            WorldWrap.RegisterDynamic(go.transform);
            leaves[i] = new Leaf { transform = go.transform, state = State.Resting, timer = -1f };
        }
    }

    void Update()
    {
        if (player == null)
        {
            var controller = FindAnyObjectByType<FirstPersonController>();
            if (controller == null) return;
            player = controller.transform;
            // Start them part-way through falling so the air is already full of them.
            foreach (var leaf in leaves) Respawn(leaf, Random.Range(0.5f, maxFallHeight));
        }
        if (weather == null) weather = FindAnyObjectByType<WeatherSystem>();

        float dt = Time.deltaTime;
        Vector3 wind = weather != null ? weather.WindVector * weather.CurrentWind : Vector3.zero;

        foreach (var leaf in leaves)
        {
            Vector3 pos = leaf.transform.position;
            Vector3 fromPlayer = pos - player.position;
            fromPlayer.y = 0f;
            if (fromPlayer.magnitude > radius * 1.4f) { Respawn(leaf, Random.Range(minFallHeight, maxFallHeight)); continue; }

            float ground = GroundHeight(pos);
            switch (leaf.state)
            {
                case State.Falling:
                {
                    // Sink with a side-to-side flutter, carried by the wind, tumbling.
                    leaf.phase += dt * leaf.flutter;
                    Vector3 sway = new Vector3(Mathf.Sin(leaf.phase), 0f, Mathf.Cos(leaf.phase * 0.7f)) * 0.6f;
                    pos += (wind * windCarry + leaf.drift + sway - Vector3.up * fallSpeed * (0.7f + 0.3f * Mathf.Sin(leaf.phase * 2f))) * dt;
                    leaf.transform.rotation *= Quaternion.Euler(leaf.spin * dt, leaf.spin * 0.6f * dt, leaf.spin * 0.3f * dt);
                    if (pos.y <= ground + 0.01f) Land(leaf, ref pos, ground);
                    break;
                }

                case State.Resting:
                    pos.y = ground + 0.005f;
                    leaf.timer -= dt;
                    // A strong gust can pick it up again.
                    if (weather != null && weather.CurrentWind > 0.85f && Random.value < 0.15f * dt)
                    {
                        leaf.state = State.Blowing;
                        leaf.timer = Random.Range(0.6f, 2f);
                    }
                    else if (leaf.timer <= 0f)
                    {
                        Respawn(leaf, Random.Range(minFallHeight, maxFallHeight));
                        continue;
                    }
                    break;

                case State.Blowing:
                    // Skitter along the ground in little hops.
                    leaf.timer -= dt;
                    leaf.phase += dt * 9f;
                    pos += (wind.normalized * 3f + leaf.drift) * dt;
                    pos.y = ground + 0.01f + Mathf.Abs(Mathf.Sin(leaf.phase)) * 0.25f;
                    leaf.transform.rotation *= Quaternion.Euler(400f * dt, 250f * dt, 0f);
                    if (leaf.timer <= 0f) Land(leaf, ref pos, ground);
                    break;
            }

            leaf.transform.position = pos;
        }
    }

    void Land(Leaf leaf, ref Vector3 pos, float ground)
    {
        pos.y = ground + 0.005f;
        leaf.state = State.Resting;
        leaf.timer = Random.Range(minRestTime, maxRestTime);
        leaf.transform.rotation = Quaternion.Euler(Random.Range(-6f, 6f), Random.Range(0f, 360f), Random.Range(-6f, 6f));
    }

    void Respawn(Leaf leaf, float height)
    {
        Vector2 offset = Random.insideUnitCircle * radius;
        Vector3 p = player.position + new Vector3(offset.x, 0f, offset.y);
        p.y = GroundHeight(p) + height;
        leaf.transform.SetPositionAndRotation(p, Random.rotation);
        leaf.state = State.Falling;
        leaf.phase = Random.Range(0f, 10f);
        leaf.flutter = Random.Range(1.5f, 3.5f);
        leaf.spin = Random.Range(60f, 240f) * (Random.value < 0.5f ? -1f : 1f);
        leaf.drift = new Vector3(Random.Range(-0.3f, 0.3f), 0f, Random.Range(-0.3f, 0.3f));
    }

    float GroundHeight(Vector3 p) => generator != null ? generator.GetGroundHeight(p.x, p.z) : 0f;
}
