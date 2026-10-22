using UnityEngine;

/// <summary>
/// The hole a dismissed scour retreats through: a ragged oval tearing open in the air behind it, black
/// inside, its edge burning red (the Horror/Rift shader with nothing to show through it). It snaps open,
/// stays while the scour is drawn back into it, then shrinks shut and is gone. ShadowEntity opens one
/// with Open and shuts it with Close.
/// </summary>
public class ScourPortal : MonoBehaviour
{
    static readonly Color RimColor = new Color(2.6f, 0.14f, 0.06f);
    static Material faceMat;
    static Texture2D black;

    Transform face;
    Light glow;
    AudioSource hum;
    Vector2 halfSize;
    float age, closeAge = -1f;
    const float OpenTime = 0.45f, CloseTime = 0.6f;

    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() { faceMat = null; black = null; }

    /// <summary>Tears one open with its middle at centre, facing along forward, halfSize across and up.</summary>
    public static ScourPortal Open(Vector3 centre, Vector3 forward, Vector2 halfSize)
    {
        if (faceMat == null)
        {
            Shader shader = Shader.Find("Horror/Rift");
            if (shader == null) return null;
            black = new Texture2D(1, 1) { name = "Scour Portal Dark" };
            black.SetPixel(0, 0, Color.black);
            black.Apply();
            faceMat = new Material(shader);
            faceMat.SetTexture("_MainTex", black);
            faceMat.SetColor("_RimColor", RimColor);
        }

        var portal = new GameObject("Scour Portal").AddComponent<ScourPortal>();
        portal.transform.SetPositionAndRotation(centre, Quaternion.LookRotation(forward));
        portal.halfSize = halfSize;

        var go = new GameObject("Face");
        go.layer = 30; // (as the rift's faces: not seen by the portal views' cameras)
        go.transform.SetParent(portal.transform, false);
        go.AddComponent<MeshFilter>().sharedMesh = RiftEvent.BuildFaceMesh();
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = faceMat;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        portal.face = go.transform;
        portal.face.localScale = Vector3.zero;

        portal.glow = new GameObject("Glow").AddComponent<Light>();
        portal.glow.transform.SetParent(portal.transform, false);
        portal.glow.transform.localPosition = Vector3.forward * 0.3f;
        portal.glow.type = LightType.Point;
        portal.glow.color = new Color(1f, 0.08f, 0.04f);
        portal.glow.range = 6f;
        portal.glow.intensity = 0f;
        portal.glow.shadows = LightShadows.None;

        portal.hum = ProceduralAudio.AddSource(portal.gameObject, ProceduralAudio.RiftHum, true, true, 0f, 2f, 25f);
        portal.hum.pitch = 0.55f;
        portal.hum.Play();
        var tear = ProceduralAudio.AddSource(portal.gameObject, null, false, true, 0.8f, 3f, 40f);
        tear.pitch = 0.5f;
        tear.PlayOneShot(ProceduralAudio.Materialize, 1f);
        return portal;
    }

    /// <summary>Shrinks it shut (with a deep suck of air) and removes it.</summary>
    public void Close()
    {
        if (closeAge >= 0f) return;
        closeAge = 0f;
        var src = ProceduralAudio.AddSource(gameObject, null, false, true, 0.8f, 3f, 40f);
        src.pitch = 0.45f;
        src.PlayOneShot(ProceduralAudio.Poof, 1f);
    }

    void Update()
    {
        age += Time.deltaTime;
        float open = Overshoot(age / OpenTime);
        if (closeAge >= 0f)
        {
            closeAge += Time.deltaTime;
            float c = Mathf.Clamp01(closeAge / CloseTime);
            open *= 1f - c * c;
            if (c >= 1f) { Destroy(gameObject); return; }
        }
        face.localScale = new Vector3(halfSize.x * open, halfSize.y * open, 0.02f);
        float flicker = 0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 7f, 0.4f);
        glow.intensity = 3f * Mathf.Clamp01(open) * flicker;
        hum.volume = 0.5f * Mathf.Clamp01(open);
    }

    static float Overshoot(float t)
    {
        t = Mathf.Clamp01(t) - 1f;
        const float s = 1.70158f;
        return t * t * ((s + 1f) * t + s) + 1f;
    }
}
