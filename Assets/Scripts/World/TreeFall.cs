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

        // Where it breaks: 2 or 3 sections, the breaks somewhere in the middle of each span.
        int count = height > 6f && Random.value < 0.6f ? 3 : 2;
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
                if (mat != barkMaterial) foliageReach = Mathf.Max(foliageReach, Mathf.Max(part.bounds.extents.x, part.bounds.extents.z));
            }

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
        foreach (var body in pieces) colliders.AddRange(body.GetComponents<Collider>());
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
            foreach (var col in piece.GetComponents<Collider>())
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
        foreach (var col in body.GetComponents<Collider>()) lowest = Mathf.Min(lowest, col.bounds.min.y);
        if (lowest < float.MaxValue) body.transform.position += Vector3.up * (ground - lowest - 0.03f);
    }

    /// <summary>
    /// The stump: the bottom of the real trunk (its roots and flare) kept where it stood, cut off at
    /// StumpHeight with a pale ring of fresh wood on top and a few splinters sticking up from the break.
    /// The plain cylinder from AddStump stays only as its collider.
    /// </summary>
    void BuildStumpBase(List<(Mesh mesh, Matrix4x4 toSway, Material mat)> sources)
    {
        var stumpBase = new GameObject("Stump Base");
        stumpBase.transform.SetParent(tree, true);
        stumpBase.transform.SetPositionAndRotation(sway.position, sway.rotation);
        bool any = false;
        foreach (var (mesh, toSway, mat) in sources)
        {
            if (mat != barkMaterial) continue;
            Mesh part = Slice(mesh, toSway, -10f, StumpHeight);
            if (part == null) continue;
            var go = new GameObject("Bark");
            go.transform.SetParent(stumpBase.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = part;
            go.AddComponent<MeshRenderer>().sharedMaterial = mat;
            any = true;
        }
        if (!any) { Destroy(stumpBase); return; }
        if (stumpCollider != null)
        {
            var r = stumpCollider.GetComponent<Renderer>();
            if (r != null) r.enabled = false;
        }

        var standing = tree.GetComponent<CapsuleCollider>();
        float radius = standing != null ? standing.radius : 0.25f;
        var cap = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cap.name = "Stump Top";
        Destroy(cap.GetComponent<Collider>());
        cap.transform.SetParent(stumpBase.transform, false);
        cap.transform.localPosition = new Vector3(0f, StumpHeight - 0.01f, 0f);
        cap.transform.localScale = new Vector3(radius * 2f, 0.012f, radius * 2f);
        cap.GetComponent<Renderer>().sharedMaterial = StumpTopMaterial;
        for (int i = 0; i < 6; i++)
        {
            float a = Random.Range(0f, Mathf.PI * 2f), d = Random.Range(0.3f, 0.9f) * radius;
            var spike = GameObject.CreatePrimitive(PrimitiveType.Cube);
            spike.name = "Splinter";
            Destroy(spike.GetComponent<Collider>());
            spike.transform.SetParent(stumpBase.transform, false);
            float h = Random.Range(0.06f, 0.22f);
            spike.transform.localPosition = new Vector3(Mathf.Cos(a) * d, StumpHeight + h * 0.4f, Mathf.Sin(a) * d);
            spike.transform.localRotation = Quaternion.Euler(Random.Range(-15f, 15f), Random.Range(0f, 360f), Random.Range(-15f, 15f));
            spike.transform.localScale = new Vector3(Random.Range(0.03f, 0.07f), h, Random.Range(0.02f, 0.05f));
            spike.GetComponent<Renderer>().sharedMaterial = SplinterMaterial;
        }
    }

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
