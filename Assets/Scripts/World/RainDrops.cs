using UnityEngine;

/// <summary>
/// A very light rain: a sparse scattering of drops falling around the player, slanted by the wind. They're
/// lit like everything else, so they mostly show up as glints crossing the flashlight beam or a lamp's
/// light, and vanish into the dark outside it. They stop at roofs (shacks, the car, the watchtower deck).
/// No sound. Drawn with GPU instancing. DarkForestGenerator adds this.
/// </summary>
public class RainDrops : MonoBehaviour
{
    [Tooltip("How many drops are falling around the player at once. Keep it low: it's barely raining.")]
    public int dropCount = 140;
    [Tooltip("Drops fall within this distance of the player.")]
    public float radius = 14f;
    public float fallSpeed = 9f;
    [Tooltip("How much the wind slants them.")]
    public float windSlant = 0.25f;
    public Vector2 dropSize = new Vector2(0.012f, 0.32f);
    public Color tint = new Color(0.75f, 0.8f, 0.85f, 0.35f);

    Vector3[] positions;
    float[] floors;   // where each drop stops: the ground, or a roof
    float[] speeds;
    Matrix4x4[] matrices;
    Mesh mesh;
    Material material;
    DarkForestGenerator generator;
    WeatherSystem weather;

    void Start()
    {
        generator = FindAnyObjectByType<DarkForestGenerator>();
        weather = FindAnyObjectByType<WeatherSystem>();
        mesh = BuildStreak();
        material = MaterialFactory.LitTransparent(MakeStreakTexture(), tint, 0.9f);
        material.enableInstancing = true;

        int n = Mathf.Clamp(dropCount, 0, 1023);
        positions = new Vector3[n];
        floors = new float[n];
        speeds = new float[n];
        matrices = new Matrix4x4[n];
        var cam = Camera.main;
        Vector3 c = cam != null ? cam.transform.position : transform.position;
        for (int i = 0; i < n; i++) Respawn(i, c, Random.Range(-2f, 12f)); // already falling, at all heights
    }

    void Update()
    {
        var cam = Camera.main;
        if (cam == null || positions == null || positions.Length == 0 || PortalDoor.PlayerInRoom) return;
        Vector3 c = cam.transform.position;
        float dt = Time.deltaTime;
        Vector3 wind = weather != null ? weather.WindVector * (windSlant * (0.5f + weather.CurrentWind)) : Vector3.zero;

        for (int i = 0; i < positions.Length; i++)
        {
            Vector3 velocity = (Vector3.down + wind) * speeds[i];
            Vector3 p = positions[i] + velocity * dt;
            Vector3 off = p - c;
            off.y = 0f;
            if (p.y <= floors[i] || off.sqrMagnitude > radius * radius * 1.4f)
            {
                Respawn(i, c, Random.Range(6f, 12f));
                p = positions[i];
            }
            positions[i] = p;

            // A thin streak along its fall, turned to face the camera.
            Vector3 along = velocity.normalized;
            Vector3 toCam = c - p;
            Vector3 facing = Vector3.ProjectOnPlane(toCam, along);
            if (facing.sqrMagnitude < 0.0001f) facing = Vector3.forward;
            matrices[i] = Matrix4x4.TRS(p, Quaternion.LookRotation(facing, along), new Vector3(dropSize.x, dropSize.y, 1f));
        }
        Graphics.DrawMeshInstanced(mesh, 0, material, matrices, matrices.Length, null,
            UnityEngine.Rendering.ShadowCastingMode.Off, false);
    }

    void Respawn(int i, Vector3 around, float height)
    {
        Vector2 d = Random.insideUnitCircle * radius;
        Vector3 p = new Vector3(around.x + d.x, around.y + height, around.z + d.y);
        float ground = generator != null ? generator.GetGroundHeight(p.x, p.z) : around.y - 2f;
        // Roofs and decks catch it.
        if (Physics.Raycast(p, Vector3.down, out var hit, height + 20f, ~0, QueryTriggerInteraction.Ignore))
            ground = Mathf.Max(ground, hit.point.y);
        positions[i] = p;
        floors[i] = ground;
        speeds[i] = fallSpeed * Random.Range(0.85f, 1.15f);
    }

    /// <summary>A unit quad (centred, along local y), two-sided.</summary>
    static Mesh BuildStreak()
    {
        var m = new Mesh { name = "Raindrop" };
        var v = new[] { new Vector3(-0.5f, -0.5f, 0f), new Vector3(0.5f, -0.5f, 0f), new Vector3(0.5f, 0.5f, 0f), new Vector3(-0.5f, 0.5f, 0f) };
        m.vertices = new[] { v[0], v[1], v[2], v[3], v[0], v[1], v[2], v[3] };
        m.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1), new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
        m.normals = new[] { Vector3.back, Vector3.back, Vector3.back, Vector3.back, Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward };
        m.triangles = new[] { 0, 2, 1, 0, 3, 2, 4, 5, 6, 4, 6, 7 };
        m.RecalculateBounds();
        return m;
    }

    /// <summary>A soft streak: brightest down the middle, fading at the edges and toward the tail.</summary>
    static Texture2D MakeStreakTexture()
    {
        const int w = 8, h = 64;
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = "Raindrop", wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = (x + 0.5f) / w * 2f - 1f, v = (y + 0.5f) / h;
                float a = (1f - u * u) * Mathf.SmoothStep(0f, 1f, v * 1.2f) * Mathf.SmoothStep(0f, 1f, (1f - v) * 5f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return tex;
    }
}
