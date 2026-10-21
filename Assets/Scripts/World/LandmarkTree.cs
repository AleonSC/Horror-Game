using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The landmark pine (DarkForestGenerator plants one): so it can be seen from anywhere, over the trees and
/// through the fog, as a dark shape against the sky to find your way by.
///
/// Up close it's an ordinary (enormous) tree in the fog like the rest. Out past where the fog swallows
/// everything, it's drawn instead as a silhouette in the Horror/Haze shader, which ignores the scene fog and
/// does its own: thick round its foot, so it rises out of the mist, thinner up its crown. The silhouette
/// starts out the fog's own colour where the swap happens, so there's no pop, and darkens with distance to
/// a clear shape. Further off than the camera can draw, it's drawn nearer and smaller, so it looks the same.
/// </summary>
public class LandmarkTree : MonoBehaviour
{
    public Color color = new Color(0.004f, 0.005f, 0.007f);
    [Range(0f, 1f), Tooltip("How much haze is left on it far off (lower = a clearer shape).")]
    public float farHaze = 0.4f;
    [Range(0f, 1f), Tooltip("Extra mist round its foot, thinning up the trunk.")]
    public float groundHaze = 0.6f;
    [Tooltip("Height of the mist up the trunk, in metres.")]
    public float hazeHeight = 30f;
    [Tooltip("Metres past the fog's edge over which it darkens to its far look.")]
    public float darkenOver = 40f;

    readonly List<Renderer> real = new List<Renderer>();
    Transform silhouette;
    Material mat;
    bool showingSilhouette;

    void Start()
    {
        foreach (var r in GetComponentsInChildren<MeshRenderer>()) real.Add(r);

        Shader shader = Shader.Find("Horror/Haze");
        mat = shader != null ? new Material(shader) : MaterialFactory.NoFog(color);
        mat.SetColor("_Color", color);

        // A copy of the tree's meshes, on its own, so it can be drawn anywhere at any size.
        silhouette = new GameObject("Landmark Silhouette").transform;
        foreach (var filter in GetComponentsInChildren<MeshFilter>())
        {
            if (filter.sharedMesh == null) continue;
            var part = new GameObject(filter.name).transform;
            part.SetParent(silhouette, false);
            part.localPosition = transform.InverseTransformPoint(filter.transform.position);
            part.localRotation = Quaternion.Inverse(transform.rotation) * filter.transform.rotation;
            Vector3 s = filter.transform.lossyScale, own = transform.lossyScale;
            part.localScale = new Vector3(s.x / own.x, s.y / own.y, s.z / own.z);
            part.gameObject.AddComponent<MeshFilter>().sharedMesh = filter.sharedMesh;
            var renderer = part.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = mat;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }
        silhouette.gameObject.SetActive(false);
    }

    void LateUpdate()
    {
        var cam = Camera.main;
        if (cam == null || silhouette == null) return;
        Vector3 eye = cam.transform.position;
        Vector3 at = WorldWrap.NearestImage(transform.position, eye);
        Vector3 flat = at - eye;
        flat.y = 0f;
        float distance = flat.magnitude;

        // Where the fog has swallowed everything (nearly all of it: exp-squared fog under 1%).
        float density = RenderSettings.fog ? RenderSettings.fogDensity : 0f;
        float fogEdge = density > 0.0001f ? 2.15f / density : float.MaxValue;
        bool far = distance > fogEdge;
        if (far != showingSilhouette)
        {
            showingSilhouette = far;
            silhouette.gameObject.SetActive(far);
            foreach (var r in real) if (r != null) r.forceRenderingOff = far;
        }
        if (!far) return;

        // Fog-coloured where it takes over from the real tree, darkening to a clear shape further off.
        float haze = Mathf.Lerp(1f, farHaze, Mathf.SmoothStep(0f, 1f, (distance - fogEdge) / Mathf.Max(darkenOver, 1f)));
        mat.SetColor("_HazeColor", RenderSettings.fogColor);
        mat.SetFloat("_Haze", haze);
        mat.SetFloat("_GroundHaze", groundHaze);

        // Beyond what the camera draws, it's brought nearer and shrunk to match.
        Vector3 toTree = at - eye;
        float reach = cam.farClipPlane * 0.8f;
        float shrink = toTree.magnitude > reach ? reach / toTree.magnitude : 1f;
        silhouette.SetPositionAndRotation(eye + toTree * shrink, transform.rotation);
        silhouette.localScale = transform.lossyScale * shrink;
        mat.SetFloat("_HazeBase", eye.y + (at.y - eye.y) * shrink);
        mat.SetFloat("_HazeHeight", hazeHeight * shrink);
        mat.SetFloat("_WispScale", 0.03f / shrink);
    }

    void OnDestroy()
    {
        if (silhouette != null) Destroy(silhouette.gameObject);
        if (mat != null) Destroy(mat);
    }
}
