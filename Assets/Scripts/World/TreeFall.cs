using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// One tree coming down: a crack, a shudder, then a creaking topple under real physics, pivoting on its
/// stump. When it hits the ground (or another tree) it splinters: the trunk breaks into sections that
/// bounce, roll and settle into the lie of the land, throwing off shards of wood. It leaves a stump, and
/// the broken pieces block the way. Several can fall at once. Started with TreeFall.Fell (by
/// FallingTreeEvent's random falls, and by the blind entity barging through the forest).
/// </summary>
public class TreeFall : MonoBehaviour
{
    public float volume = 1f;
    public int shardCount = 14;
    public float settleTime = 10f;

    enum Phase { Cracking, Falling, Settling, Done }

    Phase phase = Phase.Cracking;
    DarkForestGenerator generator;
    FirstPersonController player;
    float timer, push = 0.25f;

    Transform tree, sway;
    Quaternion restRotation;
    Vector3 fallAxis;      // in the tree's local space
    float height, angle;
    AudioSource audioSource;
    Collider stumpCollider;

    readonly List<Rigidbody> pieces = new List<Rigidbody>();
    readonly List<Joint> joints = new List<Joint>();
    readonly List<float> breakHeights = new List<float>(); // in sway space, between pieces
    readonly List<Collider> crowns = new List<Collider>();  // the bulky colliders round the boughs
    Material barkMaterial;
    bool splintered;
    float shake;

    /// <summary>True if this tree is still standing and can be felled.</summary>
    public static bool CanFell(Transform swayPivot)
    {
        if (swayPivot == null || swayPivot.parent == null || !swayPivot.gameObject.activeInHierarchy) return false;
        var trunk = swayPivot.parent.GetComponent<CapsuleCollider>();
        return trunk != null && trunk.enabled && swayPivot.parent.GetComponent<TreeFall>() == null;
    }

    /// <summary>
    /// Brings a tree down, falling toward fallDir. crackTime: how long it shudders after the crack before it
    /// goes. push: how hard it's shoved over (radians per second to start with).
    /// </summary>
    public static TreeFall Fell(Transform swayPivot, Vector3 fallDir, float crackTime = 0.7f, float push = 0.25f,
        float volume = 1f, int shardCount = 14, float settleTime = 10f)
    {
        if (!CanFell(swayPivot)) return null;
        var fall = swayPivot.parent.gameObject.AddComponent<TreeFall>();
        fall.volume = volume;
        fall.shardCount = shardCount;
        fall.settleTime = settleTime;
        fall.Begin(swayPivot, fallDir, crackTime, push);
        return fall;
    }

    void Begin(Transform swayPivot, Vector3 fallDir, float crackTime, float pushStrength)
    {
        generator = FindAnyObjectByType<DarkForestGenerator>();
        player = FindAnyObjectByType<FirstPersonController>();
        sway = swayPivot;
        tree = swayPivot.parent;
        push = pushStrength;
        restRotation = sway.localRotation;
        var trunk = tree.GetComponent<CapsuleCollider>();
        height = trunk != null ? trunk.height : 8f;

        // Stop the wind from swaying it; this script animates it now.
        if (generator != null) generator.SwayPivots.Remove(sway);

        fallDir.y = 0f;
        if (fallDir.sqrMagnitude < 0.001f) fallDir = Vector3.forward;
        fallAxis = tree.InverseTransformDirection(Vector3.Cross(Vector3.up, fallDir.normalized)).normalized;
        angle = 0f;

        AddStump(trunk);

        audioSource = ProceduralAudio.AddSource(tree.gameObject, null, false, true, volume, 10f, 120f);
        audioSource.PlayOneShot(ProceduralAudio.TreeSnap, volume);

        phase = Phase.Cracking;
        timer = crackTime;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        switch (phase)
        {
            case Phase.Cracking:
                // A moment of shuddering after the crack, before it starts to go.
                timer -= dt;
                angle = Mathf.Sin(Time.time * 40f) * 0.4f + 1f;
                sway.localRotation = Quaternion.AngleAxis(angle, fallAxis) * restRotation;
                if (timer <= 0f) StartFalling();
                break;

            case Phase.Falling:
                // Physics has it now. It splinters when it hits something, or once it's tipped well over
                // (a soft landing, a slide down a slope, or caught on another tree), or failing all that, in time.
                timer -= dt;
                if (pieces.Count > 0 && pieces[0] != null && Vector3.Angle(pieces[0].transform.up, Vector3.up) > 62f)
                    Splinter(pieces[pieces.Count > 1 ? 1 : 0].worldCenterOfMass, Mathf.Max(6f, pieces[0].angularVelocity.magnitude * height * 0.5f));
                else if (timer <= 0f) Splinter(sway.position, 6f);
                break;

            case Phase.Settling:
                timer -= dt;
                bool asleep = true;
                foreach (var body in pieces)
                {
                    if (body == null || body.isKinematic) continue;
                    // A log doesn't stay balanced on its end: nudge any that come to rest upright over.
                    if (IsLog(body) && Upright(body) && body.linearVelocity.sqrMagnitude < 0.25f && !tipped.Contains(body))
                    {
                        tipped.Add(body);
                        body.WakeUp();
                        body.AddTorque(Vector3.Cross(Vector3.up, Flat(Random.insideUnitSphere)) * body.mass * 2.5f, ForceMode.Impulse);
                        timer = Mathf.Max(timer, 3f);
                    }
                    if (!body.IsSleeping()) asleep = false;
                }
                if (asleep || timer <= 0f) Finish();
                break;
        }

        if (shake > 0f && player != null)
        {
            player.AddCameraShake(shake);
            shake = Mathf.MoveTowards(shake, 0f, dt * 5f);
        }
    }

    // ---------------------------------------------------------------- Physics

    /// <summary>
    /// Cuts the tree into sections stacked up the trunk, each a rigid body, held together by joints and
    /// hinged on the stump, and gives it a push in the fall direction.
    /// </summary>
    void StartFalling()
    {
        phase = Phase.Falling;
        timer = 12f;
        splintered = false;
        pieces.Clear();
        joints.Clear();
        breakHeights.Clear();

        var standing = tree.GetComponent<CapsuleCollider>();
        float radius = standing != null ? standing.radius : 0.25f;
        if (standing != null) standing.enabled = false;

        audioSource.pitch = 0.6f;
        audioSource.PlayOneShot(ProceduralAudio.TreeCreak, volume);

        // Where it breaks: into several shorter sections (one every two to three metres, 3 to 6 of them),
        // the breaks somewhere in the middle of each span, so the crown and its branches come down in pieces.
        int count = Mathf.Clamp(Mathf.RoundToInt(height / Random.Range(2.2f, 3f)), 3, 6);
        // The bottom of the trunk and its roots stay put as the stump: the first section starts at the cut.
        var bounds = new List<float> { StumpHeight };
        for (int i = 1; i < count; i++) bounds.Add(height * (i + Random.Range(-0.25f, 0.25f)) / count);
        bounds.Add(height * 3f); // everything above the last break, foliage and all
        for (int i = 1; i < count; i++) breakHeights.Add(bounds[i]);

        // The meshes to cut up, in sway space.
        var sources = new List<(Mesh mesh, Matrix4x4 toSway, Material mat)>();
        foreach (var filter in sway.GetComponentsInChildren<MeshFilter>())
        {
            var renderer = filter.GetComponent<MeshRenderer>();
            if (renderer == null || filter.sharedMesh == null || !filter.sharedMesh.isReadable) continue;
            sources.Add((filter.sharedMesh, sway.worldToLocalMatrix * filter.transform.localToWorldMatrix, renderer.sharedMaterial));
            if (filter.name == "Trunk" || filter.name == "Bark") barkMaterial = renderer.sharedMaterial;
        }

        BuildStumpBase(sources);

        Vector3 swayAxisWorld = tree.TransformDirection(fallAxis);
        for (int i = 0; i < count; i++)
        {
            float lo = bounds[i], hi = bounds[i + 1];
            var piece = new GameObject("Fallen Section");
            piece.transform.SetParent(tree, true);
            piece.transform.SetPositionAndRotation(sway.position, sway.rotation);

            float foliageReach = 0f;
            foreach (var (mesh, toSway, mat) in sources)
            {
                Mesh part = Slice(mesh, toSway, lo, hi);
                if (part == null) continue;
                var go = new GameObject(mat == barkMaterial ? "Bark" : "Foliage");
                go.transform.SetParent(piece.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = part;
                go.AddComponent<MeshRenderer>().sharedMaterial = mat;
                // The wood gets a collider of its real shape (a convex hull round trunk and branches), so a
                // branchy top rests on its branches instead of rolling on its trunk with them through the ground.
                if (mat == barkMaterial && part.vertexCount >= 4)
                {
                    var hull = go.AddComponent<MeshCollider>();
                    hull.sharedMesh = part;
                    hull.convex = true;
                }
                if (mat != barkMaterial) foliageReach = Mathf.Max(foliageReach, Mathf.Max(part.bounds.extents.x, part.bounds.extents.z));
            }

            // Its broken ends: torn wood, so a break never shows the trunk as an empty tube.
            AddBreakEnd(piece.transform, lo, sources, radius, false);
            if (i < count - 1) AddBreakEnd(piece.transform, hi, sources, radius, true);

            // Collision: the trunk section, plus a fatter core where there are boughs (crushed when it lands).
            float bottom = Mathf.Max(lo, StumpHeight), top = Mathf.Min(hi, height);
            float sectionRadius = radius * Mathf.Lerp(1f, 0.45f, (bottom + top) * 0.5f / height);
            var trunkCollider = piece.AddComponent<CapsuleCollider>();
            trunkCollider.direction = 1;
            trunkCollider.radius = sectionRadius;
            trunkCollider.height = top - bottom + sectionRadius * 2f;
            trunkCollider.center = new Vector3(0f, (bottom + top) * 0.5f, 0f);
            if (foliageReach > 0.5f)
            {
                var crown = piece.AddComponent<CapsuleCollider>();
                crown.direction = 1;
                crown.radius = foliageReach * 0.4f;
                crown.height = Mathf.Max(top - bottom, crown.radius * 2f);
                crown.center = trunkCollider.center;
                crowns.Add(crown);
            }

            var body = piece.AddComponent<Rigidbody>();
            body.mass = Mathf.Max(20f, 400f * sectionRadius * sectionRadius * (top - bottom));
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            body.angularDamping = 0.4f;
            piece.AddComponent<FallenSection>().owner = this;

            if (i == 0)
            {
                // Pivots on the stump until it hits the ground.
                var hinge = piece.AddComponent<HingeJoint>();
                hinge.anchor = new Vector3(0f, StumpHeight, 0f);
                hinge.axis = piece.transform.InverseTransformDirection(swayAxisWorld);
                joints.Add(hinge);
            }
            else
            {
                var weld = piece.AddComponent<FixedJoint>();
                weld.connectedBody = pieces[i - 1];
                joints.Add(weld);
            }
            pieces.Add(body);
        }

        // Pieces don't collide with each other or the stump.
        var colliders = new List<Collider>();
        foreach (var body in pieces) colliders.AddRange(body.GetComponentsInChildren<Collider>());
        for (int a = 0; a < colliders.Count; a++)
        {
            if (stumpCollider != null) Physics.IgnoreCollision(colliders[a], stumpCollider);
            for (int b = a + 1; b < colliders.Count; b++) Physics.IgnoreCollision(colliders[a], colliders[b]);
        }

        // The whole tree tips over in the fall direction.
        foreach (var body in pieces) body.angularVelocity = swayAxisWorld * push;

        sway.gameObject.SetActive(false); // the sections replace it
    }

    /// <summary>The triangles of a mesh whose centres lie between two heights (sway space), as a new mesh.</summary>
    static Mesh Slice(Mesh source, Matrix4x4 toSway, float lo, float hi)
    {
        Vector3[] verts = source.vertices;
        Vector3[] normals = source.normals;
        Vector4[] tangents = source.tangents;
        Vector2[] uvs = source.uv;
        int[] tris = source.triangles;

        var map = new Dictionary<int, int>();
        var outVerts = new List<Vector3>();
        var outNormals = new List<Vector3>();
        var outTangents = new List<Vector4>();
        var outUVs = new List<Vector2>();
        var outTris = new List<int>();

        for (int t = 0; t < tris.Length; t += 3)
        {
            Vector3 a = toSway.MultiplyPoint3x4(verts[tris[t]]);
            Vector3 b = toSway.MultiplyPoint3x4(verts[tris[t + 1]]);
            Vector3 c = toSway.MultiplyPoint3x4(verts[tris[t + 2]]);
            float y = (a.y + b.y + c.y) / 3f;
            if (y < lo || y >= hi) continue;

            for (int k = 0; k < 3; k++)
            {
                int src = tris[t + k];
                if (!map.TryGetValue(src, out int dst))
                {
                    dst = outVerts.Count;
                    map[src] = dst;
                    outVerts.Add(toSway.MultiplyPoint3x4(verts[src]));
                    if (normals.Length > 0) outNormals.Add(toSway.MultiplyVector(normals[src]).normalized);
                    if (tangents.Length > 0)
                    {
                        Vector3 tan = toSway.MultiplyVector(tangents[src]).normalized;
                        outTangents.Add(new Vector4(tan.x, tan.y, tan.z, tangents[src].w));
                    }
                    if (uvs.Length > 0) outUVs.Add(uvs[src]);
                }
                outTris.Add(dst);
            }
        }
        if (outTris.Count == 0) return null;

        var mesh = new Mesh { name = source.name + " Section" };
        if (outVerts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(outVerts);
        if (outNormals.Count == outVerts.Count) mesh.SetNormals(outNormals);
        if (outTangents.Count == outVerts.Count) mesh.SetTangents(outTangents);
        if (outUVs.Count == outVerts.Count) mesh.SetUVs(0, outUVs);
        mesh.SetTriangles(outTris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>Called by a section when it hits something.</summary>
    public void OnSectionHit(Collision collision)
    {
        if (phase != Phase.Falling || splintered) return;
        if (collision.relativeVelocity.magnitude < 1.5f) return;
        Splinter(collision.GetContact(0).point, collision.relativeVelocity.magnitude);
    }

    /// <summary>The tree breaks apart where it hits: joints snap, shards fly, and each section finds its own rest.</summary>
    void Splinter(Vector3 impactPoint, float impactSpeed)
    {
        if (splintered) return;
        splintered = true;
        foreach (var joint in joints)
            if (joint != null) Destroy(joint);
        joints.Clear();
        ReleaseLeaves();

        if (impactSpeed > 0f)
        {
            ProceduralAudio.PlayAt(ProceduralAudio.TreeImpact, impactPoint, volume, Random.Range(0.9f, 1.05f), 10f);
            ProceduralAudio.PlayAt(ProceduralAudio.TreeSnap, impactPoint, volume * 0.6f, Random.Range(1.2f, 1.5f), 10f);
            float dist = player != null ? Vector3.Distance(impactPoint, player.transform.position) : 50f;
            shake = Mathf.Lerp(3f, 0.5f, Mathf.InverseLerp(5f, 30f, dist));

            // Shards burst out from each break, and a few from the impact itself.
            Rigidbody basePiece = pieces.Count > 0 ? pieces[0] : null;
            var origins = new List<Vector3> { impactPoint };
            if (basePiece != null)
                foreach (float h in breakHeights) origins.Add(basePiece.transform.TransformPoint(new Vector3(0f, h, 0f)));
            for (int i = 0; i < shardCount; i++) SpawnShard(origins[i % origins.Count], impactSpeed);

            // A little shove apart so the breaks show.
            foreach (var body in pieces)
                body.AddTorque(Random.insideUnitSphere * body.mass * 0.6f, ForceMode.Impulse);
        }

        // Down: the pieces settle rather than tumbling end over end.
        foreach (var body in pieces)
        {
            if (body == null || !IsLog(body)) continue;
            body.angularDamping = 3f;
            body.linearDamping = 0.3f;
            body.maxAngularVelocity = 4f;
        }

        phase = Phase.Settling;
        timer = settleTime;
    }

    /// <summary>
    /// The boughs are gone in the crash: each section's foliage vanishes in a burst of loose needles and leaves
    /// that drift and tumble on the wind, slowly settling, and the bulky colliders round them go too.
    /// </summary>
    void ReleaseLeaves()
    {
        foreach (var body in pieces)
        {
            if (body == null) continue;
            foreach (Transform child in body.transform)
            {
                if (child.name != "Foliage") continue;
                var renderer = child.GetComponent<Renderer>();
                if (renderer != null) LeafBurst.Spawn(renderer.bounds);
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
        }
        foreach (var crown in crowns)
            if (crown != null) Destroy(crown);
        crowns.Clear();
    }

    void SpawnShard(Vector3 at, float speed)
    {
        var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shard.name = "Splinter";
        shard.transform.SetParent(tree, true);
        shard.transform.SetPositionAndRotation(at + Random.insideUnitSphere * 0.2f, Random.rotation);
        float length = Random.Range(0.15f, 0.6f);
        shard.transform.localScale = new Vector3(Random.Range(0.02f, 0.06f), length, Random.Range(0.02f, 0.05f));
        var renderer = shard.GetComponent<Renderer>();
        renderer.sharedMaterial = Random.value < 0.5f && barkMaterial != null ? barkMaterial : SplinterMaterial;

        var body = shard.AddComponent<Rigidbody>();
        body.mass = 0.5f;
        body.linearVelocity = (Random.onUnitSphere + Vector3.up * 1.2f).normalized * Random.Range(2f, 4f + speed * 0.4f);
        body.angularVelocity = Random.onUnitSphere * Random.Range(5f, 20f);
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        foreach (var piece in pieces)
            foreach (var col in piece.GetComponentsInChildren<Collider>())
                Physics.IgnoreCollision(shard.GetComponent<Collider>(), col);
        pieces.Add(body); // frozen with the rest once settled
    }

    static Material splinterMaterial;
    static Material SplinterMaterial => splinterMaterial != null ? splinterMaterial
        : (splinterMaterial = MaterialFactory.Lit(new Color(0.42f, 0.33f, 0.22f), 0.05f)); // fresh pale wood inside

    void AddStump(CapsuleCollider trunk)
    {
        float radius = trunk != null ? trunk.radius : 0.25f;
        Renderer bark = null;
        foreach (var r in sway.GetComponentsInChildren<Renderer>())
            if (r.name == "Trunk" || r.name == "Bark") { bark = r; break; }

        var stump = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        stump.name = "Stump";
        stump.transform.SetParent(tree, false);
        stump.transform.localPosition = new Vector3(0f, 0.35f, 0f);
        stump.transform.localRotation = Quaternion.Euler(Random.Range(-4f, 4f), 0f, Random.Range(-4f, 4f));
        stump.transform.localScale = new Vector3(radius * 2.1f, 0.35f, radius * 2.1f);
        if (bark != null) stump.GetComponent<Renderer>().sharedMaterial = bark.sharedMaterial;
        stumpCollider = stump.GetComponent<Collider>();
    }

    readonly HashSet<Rigidbody> tipped = new HashSet<Rigidbody>();

    const float StumpHeight = 0.7f;

    static bool IsLog(Rigidbody body) => body.GetComponent<FallenSection>() != null;
    static bool Upright(Rigidbody body) => Vector3.Angle(body.transform.up, Vector3.up) < 45f;
    static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward; }

    /// <summary>
    /// A log still standing on end when everything stops: laid down flat where it is, falling the way it
    /// leans, resting on the ground.
    /// </summary>
    void LayDown(Rigidbody body)
    {
        Vector3 com = body.worldCenterOfMass;
        Vector3 dir = Flat(body.transform.up + Random.insideUnitSphere * 0.1f);
        Vector3 axis = Vector3.Cross(body.transform.up, dir).normalized;
        float tilt = Vector3.Angle(body.transform.up, dir);
        body.transform.RotateAround(com, axis, tilt);
        float ground = generator != null ? generator.GetGroundHeight(com.x, com.z) : com.y;
        float lowest = float.MaxValue;
        foreach (var col in body.GetComponentsInChildren<Collider>()) lowest = Mathf.Min(lowest, col.bounds.min.y);
        if (lowest < float.MaxValue) body.transform.position += Vector3.up * (ground - lowest - 0.03f);
    }

    /// <summary>
    /// The stump, built to fit: a short bark-covered trunk flaring into the ground, its top torn off in a
    /// jagged crown of splinters (taller on the side away from the fall, where the wood ripped), the broken
    /// wood face inside following that ragged rim, and a few roots spreading out into the earth. The plain
    /// cylinder from AddStump stays only as its collider.
    /// </summary>
    void BuildStumpBase(List<(Mesh mesh, Matrix4x4 toSway, Material mat)> sources)
    {
        var standing = tree.GetComponent<CapsuleCollider>();
        float r = standing != null ? standing.radius : 0.25f;
        Material bark = barkMaterial != null ? barkMaterial
            : MaterialFactory.LitTextured(ProceduralTextures.Bark, ProceduralTextures.BarkNormal, new Color(0.55f, 0.45f, 0.35f), 0.05f);

        // Which way it fell, in the tree's own space: the torn splinters stand tallest on the far side.
        Vector3 fellToward = Vector3.Cross(fallAxis, Vector3.up).normalized;

        const int around = 12;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var barkTris = new List<int>();
        var woodTris = new List<int>();
        float[] rimHeight = new float[around + 1];
        float[] rimRadius = new float[around + 1];
        for (int j = 0; j <= around; j++)
        {
            int k = j % around;
            float a = k / (float)around * Mathf.PI * 2f;
            Vector3 dir = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            float back = Mathf.Clamp01(-Vector3.Dot(dir, fellToward)); // 1 on the side away from the fall
            float jag = (k % 2 == 0 ? Random.Range(0.05f, 0.22f) : Random.Range(-0.06f, 0.04f)) * (0.4f + back);
            rimHeight[j] = j == around ? rimHeight[0] : StumpHeight + jag + back * 0.12f;
            rimRadius[j] = j == around ? rimRadius[0] : r * Random.Range(0.94f, 1.04f);
        }
        // The bark sides: into the ground, the flared foot, the rim.
        float[] ringHeight = { -0.25f, 0.12f, 0f };
        float[] ringScale = { 1.35f, 1.12f, 1f };
        for (int ring = 0; ring < 3; ring++)
            for (int j = 0; j <= around; j++)
            {
                float a = (j % around) / (float)around * Mathf.PI * 2f;
                float h = ring == 2 ? rimHeight[j] : ringHeight[ring];
                float rad = ring == 2 ? rimRadius[j] : r * ringScale[ring];
                verts.Add(new Vector3(Mathf.Cos(a) * rad, h, Mathf.Sin(a) * rad));
                uvs.Add(new Vector2(j / (float)around * 2f, h * 1.5f));
            }
        int row = around + 1;
        for (int ring = 0; ring < 2; ring++)
            for (int j = 0; j < around; j++)
            {
                int q = ring * row + j;
                barkTris.AddRange(new[] { q, q + row, q + 1, q + 1, q + row, q + row + 1 });
            }
        // The broken face: from a little below the rim in the middle, up to the jagged edge.
        int centre = verts.Count;
        verts.Add(new Vector3(0f, StumpHeight - 0.03f, 0f));
        uvs.Add(new Vector2(0.5f, 0.5f));
        int rimStart = verts.Count;
        for (int j = 0; j <= around; j++)
        {
            float a = (j % around) / (float)around * Mathf.PI * 2f;
            float rad = rimRadius[j] * 0.96f;
            verts.Add(new Vector3(Mathf.Cos(a) * rad, rimHeight[j] - 0.01f, Mathf.Sin(a) * rad));
            uvs.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f * rad / r, 0.5f + Mathf.Sin(a) * 0.5f * rad / r));
        }
        for (int j = 0; j < around; j++)
            woodTris.AddRange(new[] { centre, rimStart + j + 1, rimStart + j });

        var mesh = new Mesh { name = "Stump" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(barkTris, 0);
        mesh.SetTriangles(woodTris, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();

        var stumpBase = new GameObject("Stump Base");
        stumpBase.transform.SetParent(tree, false);
        stumpBase.AddComponent<MeshFilter>().sharedMesh = mesh;
        stumpBase.AddComponent<MeshRenderer>().sharedMaterials = new[] { bark, StumpTopMaterial };

        // Roots, spreading out and down into the earth.
        int roots = Random.Range(4, 6);
        for (int i = 0; i < roots; i++)
        {
            float a = (i + Random.Range(-0.25f, 0.25f)) / roots * 360f;
            var root = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            root.name = "Root";
            Destroy(root.GetComponent<Collider>());
            root.transform.SetParent(stumpBase.transform, false);
            Quaternion turn = Quaternion.Euler(0f, a, 0f);
            root.transform.localPosition = turn * new Vector3(0f, 0.02f, r * 1.05f);
            root.transform.localRotation = turn * Quaternion.Euler(12f, 0f, 0f);
            root.transform.localScale = new Vector3(r * 0.75f, r * 0.5f, r * Random.Range(1.6f, 2.4f));
            root.GetComponent<Renderer>().sharedMaterial = bark;
        }

        if (stumpCollider != null)
        {
            var cylinder = stumpCollider.GetComponent<Renderer>();
            if (cylinder != null) cylinder.enabled = false;
        }
    }

    /// <summary>
    /// A broken end of a fallen section, at a height up the trunk (its local space), facing up (the top of the
    /// section) or down (its foot): a short sleeve of bark torn into a jagged crown of splinters round a pale
    /// face of broken wood, sized to the trunk there.
    /// </summary>
    /// <summary>
    /// Where the trunk really is at a height (sway space), and how thick: from the bark mesh's own vertices
    /// round that height, ignoring branches (they reach further out than the trunk's surface).
    /// </summary>
    Vector2 TrunkAt(List<(Mesh mesh, Matrix4x4 toSway, Material mat)> sources, float at, float guessRadius, out float trunkRadius)
    {
        var near = new List<Vector2>();
        foreach (var (mesh, toSway, mat) in sources)
        {
            if (mat != barkMaterial) continue;
            foreach (var v in mesh.vertices)
            {
                Vector3 p = toSway.MultiplyPoint3x4(v);
                if (Mathf.Abs(p.y - at) < 0.35f) near.Add(new Vector2(p.x, p.z));
            }
        }
        trunkRadius = guessRadius * Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(at / Mathf.Max(height, 0.01f)));
        if (near.Count < 6) return Vector2.zero;

        // The middle: the average of what's close to the axis (branches left out), then its radius: a low
        // percentile of the distances, which is the trunk's surface rather than a bough's tip.
        Vector2 centre = Vector2.zero;
        int n = 0;
        foreach (var p in near)
            if (p.magnitude < guessRadius * 2f) { centre += p; n++; }
        if (n > 0) centre /= n;
        var dist = new List<float>(near.Count);
        foreach (var p in near) dist.Add((p - centre).magnitude);
        dist.Sort();
        trunkRadius = Mathf.Max(dist[Mathf.Clamp(Mathf.RoundToInt(dist.Count * 0.4f), 0, dist.Count - 1)], 0.03f);
        return centre;
    }

    /// <summary>
    /// The torn end of a section where the trunk snapped: a short sleeve of bark fitted to the trunk's real
    /// size, its edge a little ragged, closed by a face of broken wood, with a few long splinters standing out
    /// round the edge. So a break never shows the trunk as an empty tube.
    /// </summary>
    void AddBreakEnd(Transform section, float at, List<(Mesh mesh, Matrix4x4 toSway, Material mat)> sources, float baseRadius, bool facingUp)
    {
        Vector2 c = TrunkAt(sources, at, baseRadius, out float r);
        r *= 1.02f; // just over the bark, so no gap shows
        float dir = facingUp ? 1f : -1f;
        float size = Mathf.Max(r / 0.25f, 0.5f);
        const int around = 14;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var barkTris = new List<int>();
        var woodTris = new List<int>();
        Vector3 P(float x, float y, float z) => new Vector3(c.x + x, y, c.y + z);

        float[] jag = new float[around + 1];
        for (int j = 0; j < around; j++) jag[j] = Random.Range(0.005f, 0.045f) * size;
        jag[around] = jag[0];
        // The sleeve: from a little inside the section out to the ragged rim.
        for (int ring = 0; ring < 2; ring++)
            for (int j = 0; j <= around; j++)
            {
                float a = j / (float)around * Mathf.PI * 2f;
                float h = ring == 0 ? -0.1f : jag[j];
                verts.Add(P(Mathf.Cos(a) * r, at + dir * h, Mathf.Sin(a) * r));
                uvs.Add(new Vector2(j / (float)around, h * 2f));
            }
        int row = around + 1;
        for (int j = 0; j < around; j++)
        {
            int q = j;
            if (facingUp) barkTris.AddRange(new[] { q, q + row, q + 1, q + 1, q + row, q + row + 1 });
            else barkTris.AddRange(new[] { q, q + 1, q + row, q + 1, q + row + 1, q + row });
        }
        // The broken face: nearly flat, just short of the rim.
        int centre = verts.Count;
        verts.Add(P(0f, at + dir * 0.012f * size, 0f));
        uvs.Add(new Vector2(0.5f, 0.5f));
        int rim = verts.Count;
        for (int j = 0; j <= around; j++)
        {
            float a = j / (float)around * Mathf.PI * 2f;
            verts.Add(P(Mathf.Cos(a) * r * 0.97f, at + dir * Mathf.Min(jag[j], 0.015f * size), Mathf.Sin(a) * r * 0.97f));
            uvs.Add(new Vector2(0.5f + Mathf.Cos(a) * 0.5f, 0.5f + Mathf.Sin(a) * 0.5f));
        }
        for (int j = 0; j < around; j++)
        {
            if (facingUp) woodTris.AddRange(new[] { centre, rim + j + 1, rim + j });
            else woodTris.AddRange(new[] { centre, rim + j, rim + j + 1 });
        }
        // Splinters: long, thin slivers of wood torn up from the edge, leaning a little out.
        int splinters = Random.Range(3, 6);
        for (int k = 0; k < splinters; k++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f);
            float w = Random.Range(0.12f, 0.25f) * r;
            float len = Random.Range(0.06f, 0.16f) * size;
            float inset = Random.Range(0.75f, 0.95f) * r;
            Vector3 radial = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            Vector3 side = new Vector3(-radial.z, 0f, radial.x) * (w * 0.5f);
            Vector3 foot = P(radial.x * inset, at, radial.z * inset);
            Vector3 tip = foot + Vector3.up * (dir * len) + radial * (len * Random.Range(0.1f, 0.35f)) + side * Random.Range(-0.6f, 0.6f);
            // Both sides, each with its own corners: shared ones would get the two sides' normals averaged to
            // nothing, which lights as garbage (and the bloom spreads it into a glaring white blob).
            for (int face = 0; face < 2; face++)
            {
                int b = verts.Count;
                verts.Add(foot - side);
                verts.Add(foot + side);
                verts.Add(tip);
                uvs.Add(new Vector2(0.3f, 0.2f));
                uvs.Add(new Vector2(0.7f, 0.2f));
                uvs.Add(new Vector2(0.5f, 0.9f));
                woodTris.AddRange(face == 0 ? new[] { b, b + 2, b + 1 } : new[] { b, b + 1, b + 2 });
            }
        }

        var mesh = new Mesh { name = "Break" };
        mesh.SetVertices(verts);
        mesh.SetUVs(0, uvs);
        mesh.subMeshCount = 2;
        mesh.SetTriangles(barkTris, 0);
        mesh.SetTriangles(woodTris, 1);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        var go = new GameObject("Break End");
        go.transform.SetParent(section, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        Material bark = barkMaterial != null ? barkMaterial : SplinterMaterial;
        go.AddComponent<MeshRenderer>().sharedMaterials = new[] { bark, BreakWoodMaterial };
    }

    static Material breakWoodMaterial;
    /// <summary>Freshly broken wood: paler than the bark, but not bright: it's night, and it's damp.</summary>
    static Material BreakWoodMaterial => breakWoodMaterial != null ? breakWoodMaterial
        : (breakWoodMaterial = MaterialFactory.LitTextured(ProceduralTextures.EndGrain, null, new Color(0.42f, 0.34f, 0.25f), 0.05f));

    static Material stumpTopMaterial;
    static Material StumpTopMaterial => stumpTopMaterial != null ? stumpTopMaterial
        : (stumpTopMaterial = MaterialFactory.LitTextured(ProceduralTextures.EndGrain, null, new Color(0.75f, 0.62f, 0.45f), 0.05f));

    /// <summary>Freezes the pieces where they lie: they stay as solid obstacles.</summary>
    void Finish()
    {
        phase = Phase.Done;
        foreach (var body in pieces)
        {
            if (body == null) continue;
            if (IsLog(body) && Upright(body)) LayDown(body);
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
        }
        pieces.Clear();
        Destroy(this); // its work is done; the pieces stay
    }
}

/// <summary>A section of a falling tree: tells its TreeFall when it hits something.</summary>
public class FallenSection : MonoBehaviour
{
    public TreeFall owner;

    void OnCollisionEnter(Collision collision)
    {
        if (owner != null) owner.OnSectionHit(collision);
    }
}
