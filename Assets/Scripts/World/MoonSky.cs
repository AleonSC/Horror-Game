using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A pale moon in the sky, in the same direction the moonlight comes from, with a soft halo,
/// and faint shafts of moonlight slanting down through the trees around the player. The shafts
/// slowly fade in and out in different spots, like gaps in the canopy and drifting cloud.
///
/// The moon is drawn close to the camera and kept there (so it never gets lost in the fog), but drawn
/// behind everything else, so it looks infinitely far away. DarkForestGenerator adds this automatically.
/// </summary>
public class MoonSky : MonoBehaviour
{
    [Header("Moon")]
    [Tooltip("Apparent size of the moon disc.")]
    public float moonSize = 1.2f;
    public Color moonColor = new Color(1.4f, 1.5f, 1.7f, 1f);
    public Color haloColor = new Color(0.3f, 0.38f, 0.6f, 0.5f);
    [Tooltip("How far in front of the camera the moon is drawn (keep it short so the fog doesn't swallow it).")]
    public float drawDistance = 14f;

    [Header("Light Shafts")]
    public int shaftCount = 14;
    [Tooltip("Shafts appear between these distances from the player.")]
    public float minShaftDistance = 4f;
    public float maxShaftDistance = 30f;
    public float shaftLength = 22f;
    public Color shaftColor = new Color(0.45f, 0.55f, 0.8f, 0.09f);

    class Shaft
    {
        public Transform transform;
        public Material material;
        public float width, timer, fadeIn, hold, fadeOut, flicker;
    }

    readonly List<Shaft> shafts = new List<Shaft>();
    Transform moon, halo;
    Light moonLight;
    DarkForestGenerator generator;
    Mesh shaftMesh;

    void Start()
    {
        generator = FindAnyObjectByType<DarkForestGenerator>();

        moon = Billboard("Moon", MaterialFactory.Transparent(ProceduralTextures.Moon, moonColor, false, 1000));
        halo = Billboard("Moon Halo", MaterialFactory.Transparent(ProceduralTextures.Glow, haloColor, true, 1001));

        shaftMesh = BuildShaftMesh();
        for (int i = 0; i < shaftCount; i++) shafts.Add(BuildShaft());
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null) return;
        if (moonLight == null) moonLight = FindMoonLight();
        Vector3 toMoon = moonLight != null ? -moonLight.transform.forward : new Vector3(0.3f, 0.6f, 0.7f).normalized;

        // Always the same direction from the camera: looks infinitely far away.
        Vector3 eye = cam.transform.position;
        moon.SetPositionAndRotation(eye + toMoon * drawDistance, Quaternion.LookRotation(toMoon));
        moon.localScale = Vector3.one * moonSize;
        halo.SetPositionAndRotation(eye + toMoon * (drawDistance + 0.1f), Quaternion.LookRotation(toMoon));
        halo.localScale = Vector3.one * moonSize * 5f;

        Quaternion shaftRotation = Quaternion.FromToRotation(Vector3.up, toMoon);
        foreach (var shaft in shafts) UpdateShaft(shaft, eye, shaftRotation);
    }

    void UpdateShaft(Shaft s, Vector3 eye, Quaternion rotation)
    {
        if (s.timer < 0f)
        {
            // First frame: place it, starting part-way through its cycle so they don't all fade in together.
            Place(s, eye);
            s.timer = Random.Range(0f, s.fadeIn + s.hold);
        }
        s.timer += Time.deltaTime;
        float total = s.fadeIn + s.hold + s.fadeOut;
        Vector3 offset = s.transform.position - eye;
        offset.y = 0f;

        // Faded out, or left far behind: move it somewhere new near the player.
        if (s.timer >= total || offset.magnitude > maxShaftDistance * 1.4f)
        {
            Place(s, eye);
            return;
        }

        float strength = s.timer < s.fadeIn ? s.timer / s.fadeIn
            : s.timer < s.fadeIn + s.hold ? 1f
            : 1f - (s.timer - s.fadeIn - s.hold) / s.fadeOut;
        strength = Mathf.SmoothStep(0f, 1f, strength) * (0.85f + 0.15f * Mathf.Sin(Time.time * 0.7f + s.flicker));

        Color c = shaftColor;
        c.a *= strength;
        MaterialFactory.SetColor(s.material, c);
        if (s.material.HasProperty("_TintColor")) s.material.SetColor("_TintColor", c * 0.5f);

        s.transform.rotation = rotation;
    }

    void Place(Shaft s, Vector3 eye)
    {
        Vector2 dir = Random.insideUnitCircle.normalized;
        Vector3 p = eye + new Vector3(dir.x, 0f, dir.y) * Random.Range(minShaftDistance, maxShaftDistance);
        p.y = generator != null ? generator.GetGroundHeight(p.x, p.z) : eye.y - 1.7f;

        s.transform.position = p;
        s.width = Random.Range(1.2f, 3.5f);
        s.transform.localScale = new Vector3(s.width, shaftLength, s.width);
        s.timer = 0f;
        s.fadeIn = Random.Range(3f, 6f);
        s.hold = Random.Range(6f, 16f);
        s.fadeOut = Random.Range(3f, 6f);
        s.flicker = Random.Range(0f, 10f);
        MaterialFactory.SetColor(s.material, Color.clear);
    }

    Shaft BuildShaft()
    {
        var go = new GameObject("Moon Shaft");
        go.transform.SetParent(transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = shaftMesh;
        var renderer = go.AddComponent<MeshRenderer>();
        var shaft = new Shaft
        {
            transform = go.transform,
            material = MaterialFactory.Transparent(ProceduralTextures.Shaft, Color.clear, true),
        };
        renderer.sharedMaterial = shaft.material;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        WorldWrap.RegisterDynamic(go.transform);

        shaft.timer = -1f; // placed on the first update
        return shaft;
    }

    /// <summary>Two crossed quads standing on their base (y 0 to 1), visible from any side.</summary>
    static Mesh BuildShaftMesh()
    {
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        for (int q = 0; q < 2; q++)
        {
            Vector3 across = q == 0 ? Vector3.right * 0.5f : Vector3.forward * 0.5f;
            int i = verts.Count;
            verts.Add(-across); verts.Add(across); verts.Add(-across + Vector3.up); verts.Add(across + Vector3.up);
            uvs.Add(new Vector2(0f, 0f)); uvs.Add(new Vector2(1f, 0f)); uvs.Add(new Vector2(0f, 1f)); uvs.Add(new Vector2(1f, 1f));
            tris.AddRange(new[] { i, i + 2, i + 1, i + 1, i + 2, i + 3 });
        }
        var mesh = new Mesh { name = "Light Shaft" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }

    Transform Billboard(string name, Material mat)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        go.transform.SetParent(transform, false);
        Destroy(go.GetComponent<Collider>());
        var renderer = go.GetComponent<Renderer>();
        renderer.sharedMaterial = mat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return go.transform;
    }

    static Light FindMoonLight()
    {
        if (RenderSettings.sun != null) return RenderSettings.sun;
        foreach (var light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            if (light.type == LightType.Directional && light.enabled) return light;
        return null;
    }
}
