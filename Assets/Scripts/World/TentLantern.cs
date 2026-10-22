using UnityEngine;

/// <summary>
/// A tiny lantern hanging from a tent's ridge pole. Click it (LampInteractor) to turn it on or off: a small
/// warm flame behind its glass, and a soft light that just fills the tent. It sways a little on its cord.
/// Campsite builds it.
/// </summary>
public class TentLantern : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Inspector changes made in Play are kept)

    public Color lightColor = new Color(1f, 0.68f, 0.35f);
    public float intensity = 1.3f;
    public float range = 2.6f;

    public bool IsOn { get; private set; }
    public string Prompt => IsOn ? "Turn off the lantern" : "Turn on the lantern";

    Light glow;
    Renderer flame;
    Material flameOn, flameOff;
    AudioSource sfx;
    float seed;

    /// <summary>Builds it hanging below the point it's placed at (its cord's top).</summary>
    public static TentLantern Build(Transform parent, Vector3 localPos)
    {
        var go = new GameObject("Tent Lantern");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        var lantern = go.AddComponent<TentLantern>();
        lantern.Make();
        return lantern;
    }

    void Make()
    {
        seed = Random.Range(0f, 100f);
        var metal = MaterialFactory.Lit(new Color(0.12f, 0.1f, 0.08f), 0.5f);
        var cord = MaterialFactory.Lit(new Color(0.2f, 0.17f, 0.12f), 0.05f);
        var glass = MaterialFactory.LitTransparent(Texture2D.whiteTexture, new Color(1f, 0.9f, 0.75f, 0.3f), 0.9f);
        flameOff = MaterialFactory.Lit(new Color(0.15f, 0.12f, 0.1f), 0.2f);
        flameOn = MaterialFactory.NoFog(new Color(1f, 0.62f, 0.25f) * 3f);

        // The cord, then the lantern under it: a ring to hang by, a little cap, the glass, the base.
        Piece(PrimitiveType.Cylinder, cord, new Vector3(0f, -0.06f, 0f), new Vector3(0.004f, 0.06f, 0.004f));
        Piece(PrimitiveType.Sphere, metal, new Vector3(0f, -0.125f, 0f), new Vector3(0.02f, 0.012f, 0.004f));
        Piece(PrimitiveType.Cylinder, metal, new Vector3(0f, -0.145f, 0f), new Vector3(0.05f, 0.01f, 0.05f));
        Piece(PrimitiveType.Sphere, metal, new Vector3(0f, -0.14f, 0f), new Vector3(0.04f, 0.02f, 0.04f));
        var glassPiece = Piece(PrimitiveType.Cylinder, glass, new Vector3(0f, -0.185f, 0f), new Vector3(0.042f, 0.03f, 0.042f));
        glassPiece.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        for (int i = 0; i < 4; i++) // the little frame round the glass
        {
            float a = i * Mathf.PI * 0.5f + Mathf.PI * 0.25f;
            Piece(PrimitiveType.Cube, metal, new Vector3(Mathf.Cos(a) * 0.022f, -0.185f, Mathf.Sin(a) * 0.022f), new Vector3(0.004f, 0.06f, 0.004f));
        }
        Piece(PrimitiveType.Cylinder, metal, new Vector3(0f, -0.218f, 0f), new Vector3(0.05f, 0.006f, 0.05f));
        flame = Piece(PrimitiveType.Sphere, flameOff, new Vector3(0f, -0.19f, 0f), new Vector3(0.012f, 0.02f, 0.012f)).GetComponent<Renderer>();
        flame.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        glow = new GameObject("Lantern Light").AddComponent<Light>();
        glow.transform.SetParent(transform, false);
        glow.transform.localPosition = new Vector3(0f, -0.19f, 0f);
        glow.type = LightType.Point;
        glow.color = lightColor;
        glow.range = range;
        glow.intensity = intensity;
        glow.shadows = LightShadows.None;
        glow.enabled = false;

        // Easy to click: a box round the whole lantern.
        var aim = gameObject.AddComponent<BoxCollider>();
        aim.isTrigger = true;
        aim.center = new Vector3(0f, -0.17f, 0f);
        aim.size = new Vector3(0.2f, 0.18f, 0.2f);
        sfx = ProceduralAudio.AddSource(gameObject, null, false, true, 0.5f, 1f, 10f);
    }

    Transform Piece(PrimitiveType type, Material mat, Vector3 pos, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go.transform;
    }

    public void Toggle() => SetOn(!IsOn, true);

    public void SetOn(bool on, bool withSound = false)
    {
        IsOn = on;
        if (glow != null) glow.enabled = on;
        if (flame != null) flame.sharedMaterial = on ? flameOn : flameOff;
        if (withSound && sfx != null)
        {
            sfx.pitch = on ? 1.3f : 1f;
            sfx.PlayOneShot(ProceduralAudio.LampClick, 0.5f);
            if (on) sfx.PlayOneShot(ProceduralAudio.LighterFlick, 0.35f);
        }
    }

    void Update()
    {
        // Swaying a touch on its cord; the flame flickering.
        float t = Time.time + seed;
        transform.localRotation = Quaternion.Euler(Mathf.Sin(t * 0.8f) * 2.5f, 0f, Mathf.Sin(t * 0.63f + 1f) * 2.5f);
        if (IsOn && glow != null)
            glow.intensity = intensity * (0.88f + 0.18f * Mathf.PerlinNoise(t * 6f, 0.4f));
    }

    void OnDestroy()
    {
        if (flameOn != null) Destroy(flameOn);
    }
}
