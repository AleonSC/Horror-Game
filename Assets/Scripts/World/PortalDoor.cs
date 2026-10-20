using UnityEngine;

/// <summary>
/// A door standing on its own in the forest, shut and locked, between two lit lamp posts. It unlocks once
/// every lamp in the forest has been lit (with a heavy clunk heard wherever you are); then click it to
/// open it (and again to shut it). Through the doorway there's no forest: there's a vast, dark room. Walk round the
/// back of it, or past its sides, and it's just a door frame in the trees. Step through and you're in the room: black floor, black walls, black pillars
/// receding into the dark, one dim light. The door stands in the middle of it, and through it you can see
/// the forest; look round the back of it and there's only more room. Step back through to leave. Both
/// sides are the same door: shut it from inside and it's shut outside too. In the room the forest's sounds
/// fade away to an echoing silence, just your own footsteps (RoomQuiet).
///
/// The room is built far underground below the door. Each doorway shows a render texture from a camera
/// placed at the matching spot by the other door, clipped to the doorway's plane; crossing a doorway moves
/// the player to the other one. Entities can't follow you in: while you're in the room they're gone.
/// DarkForestGenerator places one somewhere at random.
/// </summary>
public class PortalDoor : MonoBehaviour
{
    /// <summary>True while the player is in the dark room.</summary>
    public static bool PlayerInRoom { get; private set; }

    /// <summary>0 in the forest, fading up to 1 in the room: the outside world's sounds duck by this.</summary>
    public static float RoomQuiet { get; private set; }

    [Tooltip("Seconds for the forest's sounds to fade away once you're in the room.")]
    public float quietFadeIn = 2.5f;
    [Tooltip("Seconds for them to come back once you're out.")]
    public float quietFadeOut = 1.5f;

    [Tooltip("How far below the door the room is built.")]
    public float roomDepth = 400f;
    [Tooltip("Width and length of the room.")]
    public float roomSize = 120f;
    public float roomHeight = 16f;
    [Tooltip("The views through the doorways are only drawn within this distance.")]
    public float viewDistance = 90f;

    const float DoorWidth = 1.05f, DoorHeight = 2.15f;
    const int PortalLayer = 30; // the doorway screens; the portal cameras don't see them

    Transform forestDoor, roomDoor;
    ShackDoor forestPanel, roomPanel;
    Renderer forestScreen, roomScreen;
    Camera forestViewCam, roomViewCam; // forestViewCam shows the forest (seen from the room), roomViewCam the room
    RenderTexture forestView, roomView;
    FirstPersonController player;
    Camera playerCam;
    int lastSide;
    bool lastInRoom;

    // ---------------------------------------------------------------- Building

    /// <summary>Builds the door, its lamp posts and the room. The pivot is on the ground at the threshold.</summary>
    public void Build(System.Random rng)
    {
        var wood = MaterialFactory.Lit(new Color(0.36f, 0.24f, 0.14f), 0.35f);
        var frameWood = MaterialFactory.Lit(new Color(0.3f, 0.2f, 0.12f), 0.3f);
        var metal = MaterialFactory.Lit(new Color(0.12f, 0.12f, 0.13f), 0.5f);
        var stone = MaterialFactory.Lit(new Color(0.15f, 0.15f, 0.15f), 0.1f);

        forestDoor = BuildDoorway("Door", transform, wood, frameWood, metal, stone, true);
        forestPanel = forestDoor.GetComponentInChildren<ShackDoor>();
        for (int side = -1; side <= 1; side += 2)
            BuildLampPost(transform, new Vector3(side * 1.6f, 0f, 0.35f), metal);

        // The room, far below, lined up exactly with the door above it.
        var room = new GameObject("Dark Room").transform;
        room.SetParent(transform, false);
        room.localPosition = Vector3.down * roomDepth;
        BuildRoom(room, rng);
        roomDoor = BuildDoorway("Door", room, wood, frameWood, metal, stone, false);
        roomPanel = roomDoor.GetComponentInChildren<ShackDoor>();
        forestPanel.linked = roomPanel;
        roomPanel.linked = forestPanel;

        forestScreen = BuildScreen(forestDoor);
        roomScreen = BuildScreen(roomDoor);
    }

    Transform BuildDoorway(string name, Transform parent, Material wood, Material frameWood, Material metal, Material stone, bool inForest)
    {
        var door = new GameObject(name).transform;
        door.SetParent(parent, false);

        // Frame: two posts and a lintel (solid), and a worn stone threshold.
        for (int side = -1; side <= 1; side += 2)
            Part(PrimitiveType.Cube, frameWood, door, new Vector3(side * (DoorWidth * 0.5f + 0.06f), DoorHeight * 0.5f - 0.09f, 0f), new Vector3(0.12f, DoorHeight + 0.42f, 0.2f), true); // posts run down into the ground
        Part(PrimitiveType.Cube, frameWood, door, new Vector3(0f, DoorHeight + 0.06f, 0f), new Vector3(DoorWidth + 0.24f, 0.12f, 0.2f), true);
        // A stone step, deep enough to run down into the earth so it never sits on air on a slope.
        if (inForest) Part(PrimitiveType.Cube, stone, door, new Vector3(0f, -0.3f, 0f), new Vector3(DoorWidth + 0.5f, 0.64f, 0.6f), false);

        // The door itself, shut, hinged on the right post. Click it to swing it open.
        var hinge = new GameObject("Hinge").transform;
        hinge.SetParent(door, false);
        hinge.localPosition = new Vector3(DoorWidth * 0.5f, 0f, 0.08f);
        var swing = hinge.gameObject.AddComponent<ShackDoor>();
        swing.openAngle = 100f;
        swing.locked = true; // until every lamp has been lit
        Part(PrimitiveType.Cube, wood, hinge, new Vector3(-DoorWidth * 0.5f, DoorHeight * 0.5f, 0f), new Vector3(DoorWidth, DoorHeight - 0.02f, 0.05f), true);
        for (int panel = 0; panel < 2; panel++)
            Part(PrimitiveType.Cube, frameWood, hinge, new Vector3(-DoorWidth * 0.5f, DoorHeight * (panel == 0 ? 0.3f : 0.72f), 0.03f), new Vector3(DoorWidth * 0.7f, DoorHeight * (panel == 0 ? 0.36f : 0.36f), 0.015f), false);
        Part(PrimitiveType.Cube, metal, hinge, new Vector3(-DoorWidth + 0.09f, 1.0f, 0.05f), new Vector3(0.12f, 0.025f, 0.04f), false); // handle
        return door;
    }

    /// <summary>A decorative lamp post, lit.</summary>
    static void BuildLampPost(Transform parent, Vector3 at, Material metal)
    {
        var post = new GameObject("Lamp Post").transform;
        post.SetParent(parent, false);
        post.localPosition = at;
        Part(PrimitiveType.Cylinder, metal, post, new Vector3(0f, 1.5f, 0f), new Vector3(0.09f, 1.7f, 0.09f), true);   // runs a little into the ground
        Part(PrimitiveType.Cylinder, metal, post, new Vector3(0f, 0.05f, 0f), new Vector3(0.22f, 0.15f, 0.22f), false); // base, sunk slightly
        Part(PrimitiveType.Cube, metal, post, new Vector3(0f, 3.25f, 0f), new Vector3(0.3f, 0.06f, 0.3f), false);
        Part(PrimitiveType.Sphere, MaterialFactory.NoFog(new Color(1f, 0.8f, 0.5f) * 3f), post, new Vector3(0f, 3.12f, 0f), new Vector3(0.18f, 0.2f, 0.18f), false);

        var light = new GameObject("Lamp Light").AddComponent<Light>();
        light.transform.SetParent(post, false);
        light.transform.localPosition = new Vector3(0f, 3f, 0f);
        light.type = LightType.Point;
        light.color = new Color(1f, 0.78f, 0.5f);
        light.range = 9f;
        light.intensity = 2.2f;
        light.shadows = LightShadows.None;
    }

    /// <summary>A huge room in black: glossy floor, walls and ceiling lost in the dark, rows of pillars, one dim light.</summary>
    void BuildRoom(Transform room, System.Random rng)
    {
        var floor = MaterialFactory.Lit(new Color(0.018f, 0.018f, 0.02f), 0.8f); // glossy: the light glints off it
        var black = MaterialFactory.Lit(new Color(0.02f, 0.02f, 0.022f), 0.25f);
        float half = roomSize * 0.5f;

        Part(PrimitiveType.Cube, floor, room, new Vector3(0f, -0.5f, 0f), new Vector3(roomSize, 1f, roomSize), true);
        Part(PrimitiveType.Cube, black, room, new Vector3(0f, roomHeight + 0.5f, 0f), new Vector3(roomSize, 1f, roomSize), true);
        for (int side = -1; side <= 1; side += 2)
        {
            Part(PrimitiveType.Cube, black, room, new Vector3(side * (half + 0.5f), roomHeight * 0.5f, 0f), new Vector3(1f, roomHeight, roomSize), true);
            Part(PrimitiveType.Cube, black, room, new Vector3(0f, roomHeight * 0.5f, side * (half + 0.5f)), new Vector3(roomSize, roomHeight, 1f), true);
        }

        // Pillars in rows, fading off into the dark (none right by the door).
        const float spacing = 14f;
        for (float x = -half + spacing * 0.5f; x < half; x += spacing)
        {
            for (float z = -half + spacing * 0.5f; z < half; z += spacing)
            {
                if (Mathf.Abs(x) < 8f && Mathf.Abs(z) < 8f) continue;
                Part(PrimitiveType.Cube, black, room, new Vector3(x, roomHeight * 0.5f, z), new Vector3(1.4f, roomHeight, 1.4f), true);
            }
        }

        // The one light: high above the door, dim and cold. Enough to make out shapes nearby.
        var light = new GameObject("Room Light").AddComponent<Light>();
        light.transform.SetParent(room, false);
        light.transform.localPosition = new Vector3(0f, roomHeight - 2f, -3f);
        light.type = LightType.Point;
        light.color = new Color(0.75f, 0.8f, 0.9f);
        light.range = 55f;
        light.intensity = 1.4f;
        light.shadows = LightShadows.Soft;
    }

    /// <summary>The surface that fills a doorway with the view from the other side.</summary>
    Renderer BuildScreen(Transform door)
    {
        var screen = GameObject.CreatePrimitive(PrimitiveType.Cube);
        screen.name = "Doorway View";
        screen.layer = PortalLayer;
        screen.transform.SetParent(door, false);
        screen.transform.localPosition = new Vector3(0f, DoorHeight * 0.5f, 0f);
        screen.transform.localScale = new Vector3(DoorWidth, DoorHeight, 0.02f);
        DestroyCollider(screen);
        var renderer = screen.GetComponent<Renderer>();
        Shader shader = Shader.Find("Horror/Portal");
        renderer.sharedMaterial = shader != null ? new Material(shader) : MaterialFactory.Unlit(Color.black);
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    static Transform Part(PrimitiveType type, Material mat, Transform parent, Vector3 pos, Vector3 scale, bool keepCollider)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!keepCollider) DestroyCollider(go);
        return go.transform;
    }

    static void DestroyCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (col == null) return;
        if (Application.isPlaying) Destroy(col);
        else DestroyImmediate(col);
    }

    // ---------------------------------------------------------------- Running

    void Start()
    {
        if (forestDoor == null) { enabled = false; return; }
        forestViewCam = MakeViewCamera("Forest View Camera");
        roomViewCam = MakeViewCamera("Room View Camera");
    }

    void OnDestroy()
    {
        PlayerInRoom = false;
        RoomQuiet = 0f;
        if (forestView != null) forestView.Release();
        if (roomView != null) roomView.Release();
    }

    Camera MakeViewCamera(string name)
    {
        var cam = new GameObject(name).AddComponent<Camera>();
        cam.transform.SetParent(transform, false);
        cam.cullingMask = ~(1 << PortalLayer);
        cam.enabled = false;
        return cam;
    }

    void LateUpdate()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<FirstPersonController>();
            if (player == null) return;
        }
        playerCam = Camera.main;
        if (playerCam == null) return;

        bool inRoom = player.transform.position.y < transform.position.y - roomDepth * 0.5f;
        if (inRoom != lastInRoom) lastSide = 0; // respawned, or came through: start tracking afresh
        lastInRoom = inRoom;
        PlayerInRoom = inRoom;

        // Locked until every lamp in the forest has been lit; then it stays unlocked for the run.
        bool unlocked = Lamp.AllHaveBeenLit;
        if (unlocked && forestPanel.locked) Unlock();
        if (!unlocked)
        {
            forestPanel.locked = roomPanel.locked = true;
            int dark = Lamp.DarkCount;
            forestPanel.lockedMessage = dark == 1 ? "Locked. One lamp is still dark." : $"Locked. {dark} lamps are still dark.";
        }

        // Shut, it's just a door: no view through it, no way through.
        forestScreen.enabled = forestPanel.OpenAmount > 0.01f;
        roomScreen.enabled = roomPanel.OpenAmount > 0.01f;

        if ((inRoom ? roomPanel : forestPanel).IsOpen) CheckCrossing(inRoom);
        else lastSide = 0;
        inRoom = PlayerInRoom;
        RoomQuiet = Mathf.MoveTowards(RoomQuiet, inRoom ? 1f : 0f, Time.deltaTime / Mathf.Max(inRoom ? quietFadeIn : quietFadeOut, 0.01f));

        // Only the doorway in the player's world needs a view, and only when it could be seen.
        Transform here = inRoom ? roomDoor : forestDoor;
        Renderer screen = inRoom ? roomScreen : forestScreen;
        bool near = (here.position - playerCam.transform.position).sqrMagnitude < viewDistance * viewDistance;
        bool show = near && screen.isVisible;

        Vector3 offset = roomDoor.position - forestDoor.position;
        UpdateView(roomViewCam, ref roomView, forestScreen, !inRoom && show, playerCam.transform.position + offset, roomDoor);
        UpdateView(forestViewCam, ref forestView, roomScreen, inRoom && show, playerCam.transform.position - offset, forestDoor);
        ProtectFromNearClip(screen, here);
    }

    /// <summary>Stepping through the doorway (between the posts, not round the side) takes you to the other door.</summary>
    void CheckCrossing(bool inRoom)
    {
        Transform door = inRoom ? roomDoor : forestDoor;
        Vector3 local = door.InverseTransformPoint(playerCam.transform.position);
        int side = local.z >= 0f ? 1 : -1;
        bool inDoorway = Mathf.Abs(local.x) < DoorWidth * 0.5f && local.y > -0.5f && local.y < DoorHeight + 0.3f;

        if (lastSide != 0 && side != lastSide && inDoorway)
        {
            Vector3 offset = roomDoor.position - forestDoor.position;
            player.TeleportBy(inRoom ? -offset : offset);
            PlayerInRoom = !inRoom;
            lastInRoom = PlayerInRoom;
        }
        lastSide = side;
    }

    /// <summary>Places a view camera to match the player's view through the other door, and renders it to the screen.</summary>
    void UpdateView(Camera cam, ref RenderTexture texture, Renderer screen, bool active, Vector3 position, Transform clipDoor)
    {
        cam.enabled = active;
        if (!active) return;

        if (texture == null || texture.width != Screen.width || texture.height != Screen.height)
        {
            if (texture != null) texture.Release();
            texture = new RenderTexture(Mathf.Max(1, Screen.width), Mathf.Max(1, Screen.height), 24, RenderTextureFormat.DefaultHDR) { name = cam.name };
            cam.targetTexture = texture;
            screen.sharedMaterial.SetTexture("_MainTex", texture);
        }

        cam.transform.SetPositionAndRotation(position, playerCam.transform.rotation);
        cam.fieldOfView = playerCam.fieldOfView;
        cam.nearClipPlane = playerCam.nearClipPlane;
        cam.farClipPlane = playerCam.farClipPlane;
        cam.clearFlags = playerCam.clearFlags == CameraClearFlags.Skybox ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
        cam.backgroundColor = playerCam.backgroundColor;
        cam.depth = playerCam.depth - 1f;

        // Clip everything between the camera and the far doorway, so it sees only what's beyond it.
        int dir = System.Math.Sign(Vector3.Dot(clipDoor.forward, clipDoor.position - position));
        if (dir == 0) dir = 1;
        Matrix4x4 view = cam.worldToCameraMatrix;
        Vector3 planePos = view.MultiplyPoint(clipDoor.position);
        Vector3 planeNormal = view.MultiplyVector(clipDoor.forward) * dir;
        float planeDist = -Vector3.Dot(planePos, planeNormal) + 0.05f;
        if (Mathf.Abs(planeDist) > 0.2f)
            cam.projectionMatrix = playerCam.CalculateObliqueMatrix(new Vector4(planeNormal.x, planeNormal.y, planeNormal.z, planeDist));
        else
            cam.projectionMatrix = playerCam.projectionMatrix;
    }

    /// <summary>The lock gives: a deep clunk, heard wherever you are in the forest.</summary>
    void Unlock()
    {
        forestPanel.locked = roomPanel.locked = false;
        var src = ProceduralAudio.AddSource(gameObject, null, false, false, 1f);
        src.pitch = 0.45f;
        src.PlayOneShot(ProceduralAudio.LampClick, 1f);
        src.PlayOneShot(ProceduralAudio.DistantRumble, 0.6f);
        Destroy(src, 4f);
    }

    /// <summary>Shut and locked again for a new run (GameOverScreen).</summary>
    public void ResetDoor()
    {
        foreach (var panel in new[] { forestPanel, roomPanel })
        {
            if (panel == null) continue;
            panel.Close();
            panel.locked = true;
        }
    }

    /// <summary>
    /// Thickens the doorway screen away from the camera as you step through, so the camera's near plane
    /// never slices through it and shows what's really behind the door for a frame.
    /// </summary>
    void ProtectFromNearClip(Renderer screen, Transform door)
    {
        float halfHeight = playerCam.nearClipPlane * Mathf.Tan(playerCam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float halfWidth = halfHeight * playerCam.aspect;
        float thickness = new Vector3(halfWidth, halfHeight, playerCam.nearClipPlane).magnitude;

        float side = Mathf.Sign(door.InverseTransformPoint(playerCam.transform.position).z);
        Transform t = screen.transform;
        t.localScale = new Vector3(DoorWidth, DoorHeight, thickness);
        t.localPosition = new Vector3(0f, DoorHeight * 0.5f, -side * thickness * 0.5f);
    }
}
