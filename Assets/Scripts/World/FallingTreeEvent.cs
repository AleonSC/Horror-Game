using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A tree near the player snaps at the base and comes crashing down: a loud crack, a shudder, then a
/// creaking topple under real physics, pivoting on its stump. When it hits the ground (or another tree)
/// it splinters: the trunk breaks into sections that bounce, roll and settle into the lie of the land,
/// throwing off shards of wood. It leaves a stump, and the broken pieces block the way.
///
/// A random event: the first tree falls about firstFallTime seconds into play, then another every
/// minInterval to maxInterval seconds. Call Trigger() to set one off any time.
/// Trees with an owl perched in them are left standing. DarkForestGenerator adds this automatically.
/// </summary>
public class FallingTreeEvent : MonoBehaviour
{
    [Tooltip("Seconds into play before the first tree falls. Set below 0 to only fall when Trigger() is called.")]
    public float firstFallTime = 30f;
    [Tooltip("After that, another tree falls every this many seconds (picked at random between the two).")]
    public float minInterval = 45f;
    public float maxInterval = 90f;
    public float minDistance = 12f;
    public float maxDistance = 25f;
    [Tooltip("Prefer trees within this many degrees of where the player is looking, so they see it.")]
    public float viewAngle = 60f;
    [Range(0f, 1f)] public float volume = 1f;
    [Tooltip("How many shards of wood fly off when it splinters.")]
    public int shardCount = 14;
    [Tooltip("The pieces are frozen in place after this many seconds, even if they're still rocking.")]
    public float settleTime = 10f;

    enum Phase { Waiting, Cracking, Falling, Settling }

    Phase phase = Phase.Waiting;
    DarkForestGenerator generator;
    FirstPersonController player;
    float clock, timer, nextFall = -1f;

    Transform tree, sway;
    Quaternion restRotation;
    Vector3 fallAxis;      // in the tree's local space
    float height, angle;
    AudioSource audioSource;
    Collider stumpCollider;

    readonly List<Rigidbody> pieces = new List<Rigidbody>();
    readonly List<Joint> joints = new List<Joint>();
    readonly List<float> breakHeights = new List<float>(); // in sway space, between pieces
    Material barkMaterial;
    bool splintered;
    float shake;

    void Update()
    {
        if (player == null)
        {
            player = FindAnyObjectByType<FirstPersonController>();
            generator = FindAnyObjectByType<DarkForestGenerator>();
            if (player == null) return;
        }

        float dt = Time.deltaTime;
        switch (phase)
        {
            case Phase.Waiting:
                if (firstFallTime < 0f) break;
                if (nextFall < 0f) nextFall = firstFallTime;
                clock += dt;
                if (clock >= nextFall)
                {
                    clock = 0f;
                    // No suitable tree right now: try again shortly. Otherwise wait for the next one.
                    nextFall = Trigger() ? Random.Range(minInterval, maxInterval) : 10f;
                }
                break;

            case Phase.Cracking:
                // A moment of shuddering after the crack, before it starts to go.
                timer -= dt;
                angle = Mathf.Sin(Time.time * 40f) * 0.4f + 1f;
                sway.localRotation = Quaternion.AngleAxis(angle, fallAxis) * restRotation;
                if (timer <= 0f) StartFalling();
                break;

            case Phase.Falling:
                // Physics has it now. If it never hits anything hard (a gentle slide), call it settled.
                timer -= dt;
                if (timer <= 0f) Splinter(sway.position, 0f);
                break;

            case Phase.Settling:
                timer -= dt;
                bool asleep = true;
                foreach (var body in pieces)
                    if (body != null && !body.isKinematic && !body.IsSleeping()) asleep = false;
                if (asleep || timer <= 0f) Finish();
                break;
        }

        if (shake > 0f)
        {
            player.AddCameraShake(shake);
            shake = Mathf.MoveTowards(shake, 0f, dt * 5f);
        }
    }

    /// <summary>Makes a tree near the player fall now. Returns false if there's no suitable tree.</summary>
    public bool Trigger()
    {
        if (phase != Phase.Waiting) return false;
        if (generator == null || player == null) return false;

        Transform pivot = PickTree();
        if (pivot == null) return false;

        sway = pivot;
        tree = pivot.parent;
        restRotation = sway.localRotation;
        var trunk = tree.GetComponent<CapsuleCollider>();
        height = trunk != null ? trunk.height : 8f;

        // Stop the wind from swaying it; this script animates it now.
        generator.SwayPivots.Remove(sway);

        // Fall roughly away from the player, so it's a scare rather than a death.
        Vector3 away = tree.position - player.transform.position;
        away.y = 0f;
        Vector3 fallDir = Quaternion.Euler(0f, Random.Range(-60f, 60f), 0f) * away.normalized;
        fallAxis = tree.InverseTransformDirection(Vector3.Cross(Vector3.up, fallDir)).normalized;
        angle = 0f;

        AddStump(trunk);

        audioSource = ProceduralAudio.AddSource(tree.gameObject, null, false, true, volume, 10f, 120f);
        audioSource.PlayOneShot(ProceduralAudio.TreeSnap, volume);

        phase = Phase.Cracking;
        timer = 0.7f;
        return true;
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
        var bounds = new List<float> { -1f };
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
            float bottom = Mathf.Max(lo, 0.7f), top = Mathf.Min(hi, height);
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
                hinge.anchor = new Vector3(0f, 0.7f, 0f);
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
        foreach (var body in pieces) body.angularVelocity = swayAxisWorld * 0.25f;

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
        if (collision.relativeVelocity.magnitude < 2.5f) return;
        Splinter(collision.GetContact(0).point, collision.relativeVelocity.magnitude);
    }

    /// <summary>The tree breaks apart where it hits: joints snap, shards fly, and each section finds its own rest.</summary>
    void Splinter(Vector3 impactPoint, float impactSpeed)
    {
        splintered = true;
        foreach (var joint in joints)
            if (joint != null) Destroy(joint);
        joints.Clear();

        if (impactSpeed > 0f)
        {
            ProceduralAudio.PlayAt(ProceduralAudio.TreeImpact, impactPoint, volume, Random.Range(0.9f, 1.05f), 10f);
            ProceduralAudio.PlayAt(ProceduralAudio.TreeSnap, impactPoint, volume * 0.6f, Random.Range(1.2f, 1.5f), 10f);
            float dist = Vector3.Distance(impactPoint, player.transform.position);
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

    Transform PickTree()
    {
        Vector3 eye = player.transform.position;
        Vector3 look = Camera.main != null ? Camera.main.transform.forward : player.transform.forward;
        look.y = 0f;

        Transform best = null;
        float bestScore = float.MaxValue;
        foreach (var pivot in generator.SwayPivots)
        {
            if (pivot == null || pivot.parent == null || pivot.parent.GetComponent<CapsuleCollider>() == null) continue;
            if (pivot.Find("Owl Perch") != null) continue; // leave the owls be

            Vector3 to = pivot.position - eye;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist < minDistance || dist > maxDistance) continue;

            // Prefer trees in view, then a bit of randomness.
            float offView = Vector3.Angle(look, to);
            float score = (offView > viewAngle ? 1000f : offView) + Random.Range(0f, 20f);
            if (score < bestScore)
            {
                bestScore = score;
                best = pivot;
            }
        }
        return best;
    }

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

    /// <summary>Freezes the pieces where they lie: they stay as solid obstacles.</summary>
    void Finish()
    {
        phase = Phase.Waiting;
        foreach (var body in pieces)
        {
            if (body == null) continue;
            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;
        }
        pieces.Clear();
    }
}

/// <summary>A section of a falling tree: tells FallingTreeEvent when it hits something.</summary>
public class FallenSection : MonoBehaviour
{
    public FallingTreeEvent owner;

    void OnCollisionEnter(Collision collision)
    {
        if (owner != null) owner.OnSectionHit(collision);
    }
}
