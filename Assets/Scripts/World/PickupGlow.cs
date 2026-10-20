using UnityEngine;

/// <summary>
/// A slight white glow round something you can pick up, only when you're right by it: a soft halo behind
/// it and a faint light on it, fading in over the last couple of metres and breathing gently. Things in a
/// shack only glow for someone inside (that's the only place they can be taken from). ItemPickup and
/// BatteryPickup add this.
/// </summary>
public class PickupGlow : MonoBehaviour
{
    [Tooltip("Fully glowing within this distance (metres)...")]
    public float fullDistance = 1.3f;
    [Tooltip("...fading out to nothing by this distance.")]
    public float fadeDistance = 2.6f;
    [Range(0f, 2f)] public float strength = 0.35f;

    static Shader glowShader;
    Transform halo;
    Material haloMat;
    Light glow;
    Vector3 localCenter;
    float size, level, phase;
    Shack shack;

    void Start()
    {
        // Fit it round whatever the item looks like.
        var renderers = GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0) { enabled = false; return; }
        Bounds b = renderers[0].bounds;
        foreach (var r in renderers) b.Encapsulate(r.bounds);
        localCenter = transform.InverseTransformPoint(b.center);
        size = Mathf.Max(b.extents.x, b.extents.y, b.extents.z) * 2f;
        shack = GetComponentInParent<Shack>();
        phase = Random.Range(0f, 10f);

        if (glowShader == null) glowShader = Shader.Find("Horror/Glow");
        if (glowShader != null)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "Pickup Glow";
            Destroy(go.GetComponent<Collider>());
            halo = go.transform;
            halo.SetParent(transform, false);
            haloMat = new Material(glowShader);
            haloMat.SetColor("_Color", Color.white);
            var r = go.GetComponent<Renderer>();
            r.sharedMaterial = haloMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            go.SetActive(false);
        }

        glow = new GameObject("Pickup Light").AddComponent<Light>();
        glow.transform.SetParent(transform, false);
        glow.transform.localPosition = localCenter;
        glow.type = LightType.Point;
        glow.color = new Color(0.95f, 0.97f, 1f);
        glow.range = size * 1.5f + 0.5f;
        glow.shadows = LightShadows.None;
        glow.enabled = false;
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        float target = 0f;
        Vector3 center = transform.TransformPoint(localCenter);
        if (cam != null && (shack == null || shack.Contains(cam.transform.position)))
        {
            float dist = Vector3.Distance(cam.transform.position, center);
            target = 1f - Mathf.InverseLerp(fullDistance, fadeDistance, dist);
        }
        level = Mathf.MoveTowards(level, target, Time.deltaTime * 3f);
        bool on = level > 0.001f;
        if (glow.enabled != on) glow.enabled = on;
        if (halo != null && halo.gameObject.activeSelf != on) halo.gameObject.SetActive(on);
        if (!on) return;

        float breathe = 0.85f + 0.15f * Mathf.Sin((Time.time + phase) * 2f);
        float a = Mathf.SmoothStep(0f, 1f, level) * breathe * strength;
        glow.intensity = 0.6f * a;
        if (halo != null)
        {
            // Facing you, nudged toward you so it isn't buried in the item, its size kept in world metres.
            Vector3 toCam = (cam.transform.position - center).normalized;
            halo.position = center + toCam * size * 0.25f;
            halo.rotation = Quaternion.LookRotation(-toCam);
            float s = size * 2.2f + 0.2f;
            Vector3 lossy = transform.lossyScale;
            halo.localScale = new Vector3(s / Mathf.Max(lossy.x, 0.0001f), s / Mathf.Max(lossy.y, 0.0001f), 1f);
            haloMat.SetFloat("_Intensity", 0.35f * a);
        }
    }

    void OnDestroy()
    {
        if (haloMat != null) Destroy(haloMat);
    }
}
