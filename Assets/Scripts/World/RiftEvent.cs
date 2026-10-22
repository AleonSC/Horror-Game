using UnityEngine;

/// <summary>
/// A small rift tearing open in the air near the player (only on demand now: the Stick of Truth spawns one), a ragged glowing oval floating just
/// off the ground. Through it you can see another part of the forest, somewhere between exitMinDistance and
/// exitMaxDistance away, and there's a matching rift there looking back. Step through and you're there.
/// After openTime seconds it shudders and closes, wherever you are.
///
/// With firstTime 0 or more it's a random event again: the first opens about firstTime seconds into play,
/// then another every minInterval to maxInterval seconds, in front of the player where there's room for it
/// (otherwise it tries again shortly). Call Trigger() or OpenAt() to open one any time. Like PortalDoor, each face shows a render texture from a camera at the matching spot
/// by the other rift, clipped to its plane; crossing one moves the player by the offset between them.
/// DarkForestGenerator adds this automatically.
/// </summary>
public class RiftEvent : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Save Tuning on the component's menu)

    [Tooltip("Seconds into play before the first rift opens. Below 0 (the default): only when Trigger() is called (the Stick of Truth).")]
    public float firstTime = -1f;
    [Tooltip("After that, another opens every this many seconds (picked at random between the two).")]
    public float minInterval = 50f;
    public float maxInterval = 100f;
    [Tooltip("Seconds a rift stays open.")]
    public float openTime = 25f;
    [Tooltip("How far from the player it opens.")]
    public float minDistance = 5f;
    public float maxDistance = 9f;
    [Tooltip("How far away the place it opens onto is.")]
    public float exitMinDistance = 60f;
    public float exitMaxDistance = 140f;
    [Tooltip("Half the rift's width and height.")]
    public Vector2 halfSize = new Vector2(0.75f, 1.1f);
    [Tooltip("How high its middle floats above the ground.")]
    public float floatHeight = 1.55f;
    public Color glow = new Color(0.75f, 0.65f, 1f);
    [Range(0f, 1f)] public float volume = 0.8f;
    [Tooltip("The views through it are only drawn within this distance.")]
    public float viewDistance = 70f;

    const int PortalLayer = 30; // as PortalDoor: the view cameras don't see the faces

    static Mesh faceMesh;
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() => faceMesh = null;

    class End
    {
        public Transform root, face;
        public Renderer renderer;
        public Light light;
        public AudioSource hum;
        public Vector3 canonical; // folded into the main square
        public int lastSide;
    }

    DarkForestGenerator generator;
    FirstPersonController player;
    Camera playerCam, viewCam;
    RenderTexture view;
    Material faceMat;
    End a, b;
    float clock, next = -1f, age = -1f;

    /// <summary>True while a rift is open.</summary>
    public bool IsOpen => a != null;

    void Update()
    {
        if (MainMenu.Showing) return;
        if (player == null)
        {
            player = FindAnyObjectByType<FirstPersonController>();
            generator = FindAnyObjectByType<DarkForestGenerator>();
            if (player == null) return;
        }
        if (firstTime < 0f || IsOpen) return;
        if (next < 0f) next = firstTime;
        clock += Time.deltaTime;
        if (clock >= next)
        {
            clock = 0f;
            next = Trigger() ? Random.Range(minInterval, maxInterval) : 8f;
        }
    }

    /// <summary>Opens a rift in front of the player now. Returns false if there's nowhere for one.</summary>
    public bool Trigger()
    {
        if (player == null || IsOpen || PortalDoor.PlayerInRoom || GameOverScreen.IsActive || DroneUnit.Piloting) return false;
        Vector3 eye = Camera.main != null ? Camera.main.transform.position : player.transform.position + Vector3.up * 1.6f;
        Vector3 look = Camera.main != null ? Camera.main.transform.forward : player.transform.forward;
        look.y = 0f;
        if (look.sqrMagnitude < 0.001f) look = player.transform.forward;
        look.Normalize();

        for (int attempt = 0; attempt < 20; attempt++)
        {
            Vector3 dir = Quaternion.Euler(0f, Random.Range(-45f, 45f), 0f) * look;
            Vector3 spot = player.transform.position + dir * Random.Range(minDistance, maxDistance);
            if (!Ground(ref spot)) continue;
            if (Physics.Linecast(eye, spot, ~0, QueryTriggerInteraction.Ignore)) continue; // in plain view
            if (OpenAt(spot, Quaternion.LookRotation(-dir))) return true; // facing the player
        }
        return false;
    }

    /// <summary>Opens a rift with its middle at (about) this spot, facing this way. False if there's no room.</summary>
    public bool OpenAt(Vector3 spot, Quaternion facing)
    {
        if (IsOpen || !Ground(ref spot) || !Clear(spot)) return false;
        Vector3 exit = Vector3.zero;
        bool found = false;
        for (int attempt = 0; attempt < 40 && !found; attempt++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            exit = spot + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * Random.Range(exitMinDistance, exitMaxDistance);
            found = Ground(ref exit) && Clear(exit) && (generator == null || !generator.IsNearStructure(exit, 3f));
        }
        if (!found) return false;

        a = BuildEnd("Rift", spot, facing);
        b = BuildEnd("Rift Exit", exit, facing);
        age = 0f;
        var src = ProceduralAudio.AddSource(a.root.gameObject, null, false, true, volume, 3f, 40f);
        src.pitch = 0.7f;
        src.PlayOneShot(ProceduralAudio.Materialize, 1f);
        src.PlayOneShot(ProceduralAudio.DistantRumble, 0.4f);
        return true;
    }

    /// <summary>Puts the spot at floatHeight above the ground under it.</summary>
    bool Ground(ref Vector3 spot)
    {
        Vector3 folded = WorldWrap.Fold(spot);
        float ground;
        if (generator != null) ground = generator.GetGroundHeight(folded.x, folded.z);
        else if (Physics.Raycast(spot + Vector3.up * 50f, Vector3.down, out var hit, 100f, ~0, QueryTriggerInteraction.Ignore)) ground = hit.point.y;
        else return false;
        spot.y = ground + floatHeight;
        return true;
    }

    /// <summary>Room for it and for someone stepping through: nothing solid round it, and not over water.</summary>
    bool Clear(Vector3 spot)
    {
        if (Physics.CheckCapsule(spot + Vector3.down * 0.3f, spot + Vector3.up * 0.6f, 0.8f, ~0, QueryTriggerInteraction.Ignore)) return false;
        Vector3 feet = spot + Vector3.down * floatHeight;
        return Water.NearPond(feet, 2f) <= 0f;
    }

    End BuildEnd(string name, Vector3 at, Quaternion facing)
    {
        if (faceMesh == null) faceMesh = BuildFaceMesh();
        if (faceMat == null)
        {
            Shader shader = Shader.Find("Horror/Rift");
            faceMat = shader != null ? new Material(shader) : MaterialFactory.Unlit(Color.black);
            faceMat.SetColor("_RimColor", glow * 2.4f);
        }

        var end = new End { canonical = WorldWrap.Fold(at) };
        end.root = new GameObject(name).transform;
        end.root.SetParent(transform, false);
        end.root.SetPositionAndRotation(at, facing);

        var face = new GameObject("Face");
        face.layer = PortalLayer;
        face.transform.SetParent(end.root, false);
        face.AddComponent<MeshFilter>().sharedMesh = faceMesh;
        end.renderer = face.AddComponent<MeshRenderer>();
        end.renderer.sharedMaterial = faceMat;
        end.renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        end.renderer.receiveShadows = false;
        end.face = face.transform;
        end.face.localScale = Vector3.zero;

        var halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
        halo.name = "Halo";
        Destroy(halo.GetComponent<Collider>());
        halo.transform.SetParent(face.transform, false);
        halo.transform.localScale = new Vector3(3.4f, 3.4f, 1f);
        Shader glowShader = Shader.Find("Horror/Glow");
        var haloMat = glowShader != null ? new Material(glowShader) : MaterialFactory.NoFog(glow * 0.3f, 3000, false);
        haloMat.SetColor("_Color", glow);
        haloMat.SetFloat("_Intensity", 0.6f);
        var haloRenderer = halo.GetComponent<Renderer>();
        haloRenderer.sharedMaterial = haloMat;
        haloRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        halo.layer = PortalLayer;

        end.light = new GameObject("Light").AddComponent<Light>();
        end.light.transform.SetParent(end.root, false);
        end.light.type = LightType.Point;
        end.light.color = glow;
        end.light.range = 9f;
        end.light.intensity = 0f;
        end.light.shadows = LightShadows.None;

        end.hum = ProceduralAudio.AddSource(end.root.gameObject, ProceduralAudio.RiftHum, true, true, 0f, 2f, 25f);
        end.hum.Play();
        return end;
    }

    /// <summary>A thin oval slab: radius 1.15 in x and y (the shader cuts the ragged edge inside that), z from -0.5 to 0.5.</summary>
    internal static Mesh BuildFaceMesh()
    {
        const int sides = 40;
        const float radius = 1.15f;
        var verts = new System.Collections.Generic.List<Vector3>();
        var tris = new System.Collections.Generic.List<int>();
        foreach (float z in new[] { -0.5f, 0.5f })
        {
            int centre = verts.Count;
            verts.Add(new Vector3(0f, 0f, z));
            for (int k = 0; k < sides; k++)
            {
                float angle = k * Mathf.PI * 2f / sides;
                verts.Add(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, z));
            }
            for (int k = 0; k < sides; k++)
            {
                tris.Add(centre);
                tris.Add(centre + 1 + k);
                tris.Add(centre + 1 + (k + 1) % sides);
            }
        }
        for (int k = 0; k < sides; k++) // the side wall
        {
            int i0 = 1 + k, i1 = 1 + (k + 1) % sides;
            int j0 = i0 + sides + 1, j1 = i1 + sides + 1;
            tris.Add(i0); tris.Add(j0); tris.Add(i1);
            tris.Add(i1); tris.Add(j0); tris.Add(j1);
        }
        var mesh = new Mesh { name = "Rift Face" };
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    void LateUpdate()
    {
        if (!IsOpen || player == null) return;
        playerCam = Camera.main;
        if (playerCam == null) return;
        if (GameOverScreen.IsActive) { Close(); return; }

        age += Time.deltaTime;
        float opening = Mathf.Clamp01(age / 1.4f);
        float closing = Mathf.Clamp01((age - openTime) / 1.8f);
        if (closing >= 1f) { Close(); return; }
        float size = Overshoot(opening) * (1f - closing * closing);
        float flicker = (0.75f + 0.25f * Mathf.PerlinNoise(Time.time * 6f, 0.3f)) * (closing > 0f ? 0.5f + 0.5f * Mathf.PerlinNoise(Time.time * 25f, 1.7f) : 1f);
        faceMat.SetFloat("_Flicker", flicker);
        if (closing > 0f && closing - Time.deltaTime / 1.8f <= 0f)
        {
            var src = ProceduralAudio.AddSource(a.root.gameObject, null, false, true, volume, 3f, 40f);
            src.pitch = 0.6f;
            src.PlayOneShot(ProceduralAudio.Poof, 1f);
            var src2 = ProceduralAudio.AddSource(b.root.gameObject, null, false, true, volume, 3f, 40f);
            src2.pitch = 0.6f;
            src2.PlayOneShot(ProceduralAudio.Poof, 1f);
        }

        // Kept at the copy of each end nearest the player, as the map loops.
        Vector3 you = player.transform.position;
        foreach (var end in new[] { a, b })
        {
            end.root.position = WorldWrap.NearestImage(end.canonical, you);
            end.light.intensity = 2.2f * size * flicker;
            end.hum.volume = volume * 0.6f * size;
        }

        // Which end you're at; the other is where it leads.
        Vector3 eye = playerCam.transform.position;
        bool atA = (a.root.position - eye).sqrMagnitude <= (b.root.position - eye).sqrMagnitude;
        End here = atA ? a : b, there = atA ? b : a;
        Vector3 offset = there.root.position - here.root.position;

        // Stepping through while it's open enough to fit through.
        Vector3 local = here.root.InverseTransformPoint(eye);
        int side = local.z >= 0f ? 1 : -1;
        float ex = local.x / (halfSize.x * size), ey = local.y / (halfSize.y * size);
        bool inside = size > 0.4f && ex * ex + ey * ey < 1f;
        if (here.lastSide != 0 && side != here.lastSide && inside)
        {
            player.TeleportBy(offset);
            there.lastSide = side;
            here.lastSide = 0;
            var whoosh = ProceduralAudio.AddSource(player.gameObject, null, false, false, volume);
            whoosh.pitch = 0.8f;
            whoosh.PlayOneShot(ProceduralAudio.Poof, 0.8f);
            Destroy(whoosh, 3f);
            (here, there) = (there, here);
            offset = -offset;
            eye = playerCam.transform.position;
        }
        else here.lastSide = side;
        there.lastSide = 0;

        foreach (var end in new[] { a, b })
        {
            end.face.localScale = new Vector3(halfSize.x * size, halfSize.y * size, 0.02f);
            end.face.localPosition = Vector3.zero;
        }
        bool near = (here.root.position - eye).sqrMagnitude < viewDistance * viewDistance;
        bool show = near && size > 0.01f && here.renderer.isVisible;
        UpdateView(show, eye + offset, there.root);
        ProtectFromNearClip(here, eye);
    }

    static float Overshoot(float t)
    {
        t = Mathf.Clamp01(t);
        float s = 1.70158f;
        t -= 1f;
        return t * t * ((s + 1f) * t + s) + 1f;
    }

    /// <summary>As PortalDoor's: a camera at the matching spot by the other end, seeing only what's beyond it.</summary>
    void UpdateView(bool active, Vector3 position, Transform clipEnd)
    {
        if (viewCam == null)
        {
            viewCam = new GameObject("Rift View Camera").AddComponent<Camera>();
            viewCam.transform.SetParent(transform, false);
            viewCam.cullingMask = ~(1 << PortalLayer);
            viewCam.enabled = false;
        }
        viewCam.enabled = active;
        if (!active) return;

        if (view == null || view.width != Screen.width || view.height != Screen.height)
        {
            if (view != null) view.Release();
            view = new RenderTexture(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height), 24, RenderTextureFormat.DefaultHDR) { name = "Rift View" };
            viewCam.targetTexture = view;
            faceMat.SetTexture("_MainTex", view);
        }

        viewCam.transform.SetPositionAndRotation(position, playerCam.transform.rotation);
        viewCam.fieldOfView = playerCam.fieldOfView;
        viewCam.nearClipPlane = playerCam.nearClipPlane;
        viewCam.farClipPlane = playerCam.farClipPlane;
        viewCam.clearFlags = playerCam.clearFlags == CameraClearFlags.Skybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
        viewCam.backgroundColor = playerCam.backgroundColor;
        viewCam.depth = playerCam.depth - 1f;

        int dir = System.Math.Sign(Vector3.Dot(clipEnd.forward, clipEnd.position - position));
        if (dir == 0) dir = 1;
        Matrix4x4 m = viewCam.worldToCameraMatrix;
        Vector3 planePos = m.MultiplyPoint(clipEnd.position);
        Vector3 planeNormal = m.MultiplyVector(clipEnd.forward) * dir;
        float planeDist = -Vector3.Dot(planePos, planeNormal) + 0.05f;
        viewCam.projectionMatrix = Mathf.Abs(planeDist) > 0.2f
            ? playerCam.CalculateObliqueMatrix(new Vector4(planeNormal.x, planeNormal.y, planeNormal.z, planeDist))
            : playerCam.projectionMatrix;
    }

    /// <summary>Thickens the face away from the camera as you step through, so the near plane never slices it.</summary>
    void ProtectFromNearClip(End here, Vector3 eye)
    {
        float halfHeight = playerCam.nearClipPlane * Mathf.Tan(playerCam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float thickness = new Vector3(halfHeight * playerCam.aspect, halfHeight, playerCam.nearClipPlane).magnitude;
        float side = Mathf.Sign(here.root.InverseTransformPoint(eye).z);
        Vector3 s = here.face.localScale;
        here.face.localScale = new Vector3(s.x, s.y, thickness);
        here.face.localPosition = new Vector3(0f, 0f, -side * thickness * 0.5f);
    }

    /// <summary>Shuts it now (it closes on its own after openTime).</summary>
    public void Close()
    {
        foreach (var end in new[] { a, b })
            if (end != null && end.root != null) Destroy(end.root.gameObject, 1.5f);
        if (a != null) { a.renderer.enabled = false; a.light.enabled = false; a.hum.Stop(); a.face.gameObject.SetActive(false); }
        if (b != null) { b.renderer.enabled = false; b.light.enabled = false; b.hum.Stop(); b.face.gameObject.SetActive(false); }
        a = b = null;
        if (viewCam != null) viewCam.enabled = false;
    }

    void OnDestroy()
    {
        if (view != null) view.Release();
    }
}
