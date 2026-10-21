using UnityEngine;

/// <summary>
/// A door standing on its own in the forest, shut and locked, between two lit lamp posts. It unlocks once
/// lampsToUnlock (10) different lamps have been lit (with a heavy clunk heard wherever you are); then
/// press E at it to open it (and again to shut it). Through the doorway there's no forest: there's a vast, dark room. Walk round the
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
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() { PlayerInRoom = false; RoomQuiet = 0f; }

    [Tooltip("Seconds for the forest's sounds to fade away once you're in the room.")]
    public float quietFadeIn = 2.5f;
    [Tooltip("Seconds for them to come back once you're out.")]
    public float quietFadeOut = 1.5f;

    [Tooltip("Different lamps that must be lit before the door unlocks (never more than there are lamps).")]
    public int lampsToUnlock = 10;
    [Tooltip("How far below the door the room is built.")]
    public float roomDepth = 400f;
    [Header("Maze")]
    [Tooltip("Cells across (and deep) of the small maze out in the room, in front of the door.")]
    public int mazeCells = 7;
    public float mazeCellSize = 3.2f;
    public float mazeWallHeight = 3.4f;
    [Tooltip("How far in front of the door the maze's entrance is.")]
    public float mazeDistance = 12f;
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
        postBulb = MaterialFactory.NoFog(PostWarm * 3f);
        Shader beamShader = Shader.Find("Horror/Beam");
        postBeamMat = beamShader != null ? new Material(beamShader) : MaterialFactory.NoFog(Color.white * 0.2f, 3000, false);
        for (int side = -1; side <= 1; side += 2)
            postLights.Add(BuildLampPost(transform, new Vector3(side * 1.6f, 0f, 0.35f), postBulb, postBeamMat, postBeams));

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
        var paint = MaterialFactory.Lit(new Color(0.2f, 0.035f, 0.03f), 0.42f);   // old oxblood gloss, worn
        var brass = MaterialFactory.Lit(new Color(0.5f, 0.37f, 0.15f), 0.8f);
        var pale = MaterialFactory.Lit(new Color(0.42f, 0.4f, 0.36f), 0.12f);     // weathered limestone
        var ivy = MaterialFactory.Lit(new Color(0.05f, 0.15f, 0.05f), 0.25f);

        // Frame: two posts and a lintel (solid), with a moulded architrave round the front and back.
        for (int side = -1; side <= 1; side += 2)
        {
            Part(PrimitiveType.Cube, frameWood, door, new Vector3(side * (DoorWidth * 0.5f + 0.06f), DoorHeight * 0.5f - 0.09f, 0f), new Vector3(0.12f, DoorHeight + 0.42f, 0.2f), true); // posts run down into the ground
            foreach (float z in new[] { -0.105f, 0.105f })
                Part(PrimitiveType.Cube, frameWood, door, new Vector3(side * (DoorWidth * 0.5f + 0.07f), DoorHeight * 0.5f + 0.02f, z), new Vector3(0.08f, DoorHeight + 0.08f, 0.02f), false);
        }
        Part(PrimitiveType.Cube, frameWood, door, new Vector3(0f, DoorHeight + 0.06f, 0f), new Vector3(DoorWidth + 0.24f, 0.12f, 0.2f), true);

        // A stone doorcase round it: pilasters on plinths with capitals, a deep lintel, a cornice, and a
        // keystone. Ivy creeping up one side.
        const float pilasterX = DoorWidth * 0.5f + 0.27f, top = DoorHeight + 0.12f;
        for (int side = -1; side <= 1; side += 2)
        {
            float x = side * pilasterX;
            Part(PrimitiveType.Cube, pale, door, new Vector3(x, top * 0.5f - 0.2f, 0f), new Vector3(0.28f, top + 0.4f, 0.3f), true);
            Part(PrimitiveType.Cube, pale, door, new Vector3(x, 0.05f, 0f), new Vector3(0.36f, 0.5f, 0.38f), false);          // plinth
            Part(PrimitiveType.Cube, pale, door, new Vector3(x, 0.33f, 0f), new Vector3(0.32f, 0.05f, 0.34f), false);
            Part(PrimitiveType.Cube, pale, door, new Vector3(x, top - 0.06f, 0f), new Vector3(0.34f, 0.06f, 0.36f), false);    // capital
            foreach (float z in new[] { -0.155f, 0.155f }) // a recessed panel down each face
                Part(PrimitiveType.Cube, stone, door, new Vector3(x, top * 0.5f + 0.1f, z), new Vector3(0.16f, top - 0.6f, 0.01f), false);
        }
        float lintelWidth = pilasterX * 2f + 0.36f;
        Part(PrimitiveType.Cube, pale, door, new Vector3(0f, top + 0.17f, 0f), new Vector3(lintelWidth, 0.34f, 0.32f), true);
        Part(PrimitiveType.Cube, pale, door, new Vector3(0f, top + 0.37f, 0f), new Vector3(lintelWidth + 0.14f, 0.07f, 0.42f), false); // cornice
        Part(PrimitiveType.Cube, pale, door, new Vector3(0f, top + 0.44f, 0f), new Vector3(lintelWidth + 0.04f, 0.07f, 0.36f), false);
        Part(PrimitiveType.Cube, pale, door, new Vector3(0f, top + 0.15f, 0f), new Vector3(0.2f, 0.38f, 0.37f), false); // keystone
        var leafRng = new System.Random(name.GetHashCode() ^ (inForest ? 1 : 2));
        float R(float a, float b) => a + (float)leafRng.NextDouble() * (b - a);
        for (int i = 0; i < 46; i++)
        {
            // Thick at the foot of the left pilaster, thinning out as it climbs and creeps along the lintel.
            float u = Mathf.Pow(R(0f, 1f), 1.6f);
            Vector3 at = u < 0.8f
                ? new Vector3(-pilasterX + R(-0.17f, 0.17f), u / 0.8f * (top + 0.3f), R(-0.17f, 0.17f))
                : new Vector3(-pilasterX + (u - 0.8f) / 0.2f * 0.9f, top + R(0f, 0.35f), R(-0.17f, 0.17f));
            float sz = Mathf.Lerp(0.16f, 0.07f, u);
            Part(PrimitiveType.Sphere, ivy, door, at, new Vector3(sz, sz * 0.8f, sz), false);
        }

        // Stone steps up to it, deep enough to run down into the earth so they never sit on air on a slope.
        if (inForest)
        {
            Part(PrimitiveType.Cube, stone, door, new Vector3(0f, -0.3f, 0f), new Vector3(DoorWidth + 0.5f, 0.64f, 0.6f), false);
            Part(PrimitiveType.Cube, pale, door, new Vector3(0f, -0.45f, 0.5f), new Vector3(DoorWidth + 0.9f, 0.7f, 0.5f), false);
            Part(PrimitiveType.Cube, pale, door, new Vector3(0f, -0.3f, -0.5f), new Vector3(DoorWidth + 0.5f, 0.64f, 0.4f), false);
        }

        // The door itself, shut, hinged on the right post. Press E at it to swing it open. Painted, with four
        // raised panels each side, iron strap hinges, a brass knob, keyhole and knocker.
        var hinge = new GameObject("Hinge").transform;
        hinge.SetParent(door, false);
        hinge.localPosition = new Vector3(DoorWidth * 0.5f, 0f, 0.08f);
        var swing = hinge.gameObject.AddComponent<ShackDoor>();
        swing.openAngle = 100f;
        swing.locked = true; // until every lamp has been lit
        const float cx = -DoorWidth * 0.5f;
        Part(PrimitiveType.Cube, paint, hinge, new Vector3(cx, DoorHeight * 0.5f, 0f), new Vector3(DoorWidth, DoorHeight - 0.02f, 0.05f), true);
        foreach (float face in new[] { -1f, 1f })
        {
            float z = face * 0.027f;
            for (int row = 0; row < 2; row++)
                for (int col = 0; col < 2; col++)
                {
                    float px = cx + (col == 0 ? -0.22f : 0.22f);
                    float py = row == 0 ? DoorHeight * 0.27f : DoorHeight * 0.7f;
                    float ph = row == 0 ? DoorHeight * 0.36f : DoorHeight * 0.42f;
                    Part(PrimitiveType.Cube, paint, hinge, new Vector3(px, py, z), new Vector3(0.36f, ph, 0.012f), false);          // moulding
                    Part(PrimitiveType.Cube, paint, hinge, new Vector3(px, py, z + face * 0.008f), new Vector3(0.28f, ph - 0.08f, 0.012f), false); // raised field
                }
            // Brass: knob on a plate, a keyhole escutcheon.
            float hx = -DoorWidth + 0.1f;
            Part(PrimitiveType.Cube, brass, hinge, new Vector3(hx, 1.0f, face * 0.03f), new Vector3(0.06f, 0.2f, 0.008f), false);
            // (The knob on the back is set in a little: the doorway's view sits just behind the shut door.)
            Part(PrimitiveType.Sphere, brass, hinge, new Vector3(hx, 1.04f, face > 0f ? 0.06f : -0.045f), new Vector3(0.065f, 0.065f, 0.04f), false);
            Part(PrimitiveType.Cylinder, brass, hinge, new Vector3(hx, 1.04f, face * 0.04f), new Vector3(0.02f, 0.02f, 0.02f), false).localRotation = Quaternion.Euler(90f, 0f, 0f);
            Part(PrimitiveType.Cube, metal, hinge, new Vector3(hx, 0.94f, face * 0.035f), new Vector3(0.012f, 0.03f, 0.004f), false);
            // Strap hinges.
            foreach (float y in new[] { 0.3f, DoorHeight - 0.3f })
                Part(PrimitiveType.Cube, metal, hinge, new Vector3(-0.2f, y, face * 0.03f), new Vector3(0.4f, 0.045f, 0.006f), false);
        }
        // A heavy brass knocker on the front, at eye height.
        Part(PrimitiveType.Cube, brass, hinge, new Vector3(cx, 1.55f, 0.034f), new Vector3(0.08f, 0.08f, 0.012f), false);
        var ring = Part(PrimitiveType.Cylinder, brass, hinge, new Vector3(cx, 1.47f, 0.045f), new Vector3(0.14f, 0.006f, 0.14f), false);
        ring.localRotation = Quaternion.Euler(90f, 0f, 0f);
        return door;
    }

    /// <summary>The garden round the door (once it's stood on the ground). DarkForestGenerator calls this.</summary>
    public void BuildGarden(DarkForestGenerator generator) => DoorGarden.Build(this, generator);

    static readonly Color PostWarm = new Color(1f, 0.8f, 0.5f);
    readonly System.Collections.Generic.List<Light> postLights = new System.Collections.Generic.List<Light>();
    readonly System.Collections.Generic.List<Transform> postBeams = new System.Collections.Generic.List<Transform>();
    Material postBulb, postBeamMat;
    float postGlow;

    /// <summary>
    /// Once every lamp is lit (the door unlocked), the lamp posts by the door blaze intense white, each
    /// throwing a beam of light straight up into the sky, so the door can be found from anywhere.
    /// </summary>
    void UpdatePosts(bool unlocked)
    {
        postGlow = Mathf.MoveTowards(postGlow, unlocked ? 1f : 0f, Time.deltaTime / 3f);
        float g = Mathf.SmoothStep(0f, 1f, postGlow);
        float pulse = 1f + 0.06f * Mathf.Sin(Time.time * 2.5f) * g;
        foreach (var light in postLights)
        {
            light.color = Color.Lerp(new Color(1f, 0.78f, 0.5f), Color.white, g);
            light.intensity = Mathf.Lerp(2.2f, 9f, g) * pulse;
            light.range = Mathf.Lerp(9f, 35f, g);
        }
        MaterialFactory.SetColor(postBulb, Color.Lerp(PostWarm * 3f, Color.white * 12f, g) * pulse);
        foreach (var beam in postBeams) beam.gameObject.SetActive(g > 0.02f);
        if (postBeamMat != null) postBeamMat.SetColor("_Color", new Color(1f, 1f, 1f, 0.18f * g * pulse));
    }

    /// <summary>A lamp post (the same old Victorian lamp as the ones in the forest), lit. Returns its light.</summary>
    static Light BuildLampPost(Transform parent, Vector3 at, Material bulb, Material beamMat, System.Collections.Generic.List<Transform> beams)
    {
        var post = new GameObject("Lamp Post").transform;
        post.SetParent(parent, false);
        post.localPosition = at;
        Lamp.BuildModel(post, bulb);

        // The beam into the sky, for when it blazes.
        var beam = new GameObject("Sky Beam");
        beam.transform.SetParent(post, false);
        beam.transform.localPosition = new Vector3(0f, Lamp.BulbHeight + 0.7f, 0f); // from just above the finial
        beam.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
        beam.transform.localScale = new Vector3(1.2f, 1.2f, 90f);
        beam.AddComponent<MeshFilter>().sharedMesh = WatcherEvent.BuildBeamMesh();
        var br = beam.AddComponent<MeshRenderer>();
        br.sharedMaterial = beamMat;
        br.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        br.receiveShadows = false;
        beam.SetActive(false);
        beams.Add(beam.transform);

        var light = new GameObject("Lamp Light").AddComponent<Light>();
        light.transform.SetParent(post, false);
        light.transform.localPosition = new Vector3(0f, Lamp.BulbHeight - 0.15f, 0f);
        light.type = LightType.Point;
        light.color = new Color(1f, 0.78f, 0.5f);
        light.range = 9f;
        light.intensity = 2.2f;
        light.shadows = LightShadows.None;
        return light;
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
                if (InMaze(x, z, 2f)) continue; // (none in the maze)
                Part(PrimitiveType.Cube, black, room, new Vector3(x, roomHeight * 0.5f, z), new Vector3(1.4f, roomHeight, 1.4f), true);
            }
        }

        BuildMaze(room, black, rng);
        MeshMerge.Merge(room); // floor, walls, pillars and maze: a few draws instead of hundreds (colliders stay)

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

    /// <summary>True if a point (room space) is within the maze's footprint, give or take margin.</summary>
    bool InMaze(float x, float z, float margin)
    {
        float size = mazeCells * mazeCellSize;
        return Mathf.Abs(x) < size * 0.5f + margin && z > mazeDistance - margin && z < mazeDistance + size + margin;
    }

    /// <summary>
    /// A small maze out in the dark in front of the door: black walls a little over head height, open
    /// above to the room's blackness, its way in facing the door. Carved fresh each run (a winding maze
    /// with no loops), and at its heart a single faint, cold light to walk toward.
    /// </summary>
    void BuildMaze(Transform room, Material wallMat, System.Random rng)
    {
        int n = Mathf.Max(3, mazeCells);
        float cell = mazeCellSize, size = n * cell;
        float x0 = -size * 0.5f, z0 = mazeDistance;
        // Walls still standing: east of each cell, and north of each cell (the outer south and west edges separately).
        var east = new bool[n, n];
        var north = new bool[n, n];
        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
                east[i, j] = north[i, j] = true;

        // Carve it: a random walk that backtracks when it's boxed in, visiting every cell once.
        var visited = new bool[n, n];
        var stack = new System.Collections.Generic.Stack<Vector2Int>();
        var start = new Vector2Int(n / 2, 0);
        visited[start.x, start.y] = true;
        stack.Push(start);
        var options = new System.Collections.Generic.List<Vector2Int>(4);
        while (stack.Count > 0)
        {
            var c = stack.Peek();
            options.Clear();
            if (c.x > 0 && !visited[c.x - 1, c.y]) options.Add(new Vector2Int(c.x - 1, c.y));
            if (c.x < n - 1 && !visited[c.x + 1, c.y]) options.Add(new Vector2Int(c.x + 1, c.y));
            if (c.y > 0 && !visited[c.x, c.y - 1]) options.Add(new Vector2Int(c.x, c.y - 1));
            if (c.y < n - 1 && !visited[c.x, c.y + 1]) options.Add(new Vector2Int(c.x, c.y + 1));
            if (options.Count == 0) { stack.Pop(); continue; }
            var next = options[rng.Next(options.Count)];
            if (next.x != c.x) east[Mathf.Min(c.x, next.x), c.y] = false;
            else north[c.x, Mathf.Min(c.y, next.y)] = false;
            visited[next.x, next.y] = true;
            stack.Push(next);
        }

        const float thick = 0.3f;
        float h = mazeWallHeight;
        void Wall(Vector3 center, Vector3 scale) => Part(PrimitiveType.Cube, wallMat, room, center + Vector3.up * h * 0.5f, scale, true);

        // Outer walls: south (toward the door) with the way in, then west.
        for (int i = 0; i < n; i++)
        {
            if (i != start.x) Wall(new Vector3(x0 + (i + 0.5f) * cell, 0f, z0), new Vector3(cell + thick, h, thick));
            Wall(new Vector3(x0, 0f, z0 + (i + 0.5f) * cell), new Vector3(thick, h, cell + thick));
        }
        // Inner walls, and the north and east edges.
        for (int i = 0; i < n; i++)
        {
            for (int j = 0; j < n; j++)
            {
                if (east[i, j]) Wall(new Vector3(x0 + (i + 1) * cell, 0f, z0 + (j + 0.5f) * cell), new Vector3(thick, h, cell + thick));
                if (north[i, j]) Wall(new Vector3(x0 + (i + 0.5f) * cell, 0f, z0 + (j + 1) * cell), new Vector3(cell + thick, h, thick));
            }
        }

        // At its heart, a faint, cold light.
        var heart = new GameObject("Maze Heart Light").AddComponent<Light>();
        heart.transform.SetParent(room, false);
        heart.transform.localPosition = new Vector3(x0 + (n / 2 + 0.5f) * cell, h + 0.8f, z0 + (n / 2 + 0.5f) * cell);
        heart.type = LightType.Point;
        heart.color = new Color(0.6f, 0.7f, 0.95f);
        heart.range = cell * 2.2f;
        heart.intensity = 0.9f;
        heart.shadows = LightShadows.None;
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
        int needed = Mathf.Min(lampsToUnlock, Lamp.All.Count); // never more lamps than there are
        int remaining = Mathf.Max(0, needed - Lamp.LitCount);
        bool unlocked = Lamp.All.Count > 0 && remaining == 0;
        if (unlocked && forestPanel.locked) Unlock();
        UpdatePosts(unlocked);
        if (!unlocked)
        {
            forestPanel.locked = roomPanel.locked = true;
            forestPanel.lockedMessage = remaining == 1 ? "Locked. Light one more lamp." : $"Locked. Light {remaining} more lamps.";
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
