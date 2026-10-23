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
    void Awake() => Tuning.Apply(this); // saved tuning (Inspector changes made in Play are kept)

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
                // Physics has it now. It splinters when it hits something, or once it's tipped well over (a
                // soft landing, a slide down a slope). It never splinters still standing: caught on another
                // tree and stuck there (not tipping any further), it slips free of whatever's holding it up
                // and carries on down (Unstick).
                timer -= dt;
                if (pieces.Count == 0 || pieces[0] == null) break;
                float tilt = Vector3.Angle(pieces[0].transform.up, Vector3.up);
                if (tilt > 62f)
                {
                    Splinter(pieces[pieces.Count > 1 ? 1 : 0].worldCenterOfMass, Mathf.Max(6f, pieces[0].angularVelocity.magnitude * height * 0.5f));
                    break;
                }
                stuckFor = tilt < lastTilt + 4f * dt ? stuckFor + dt : 0f; // (tipping slower than 4 degrees a second)
                lastTilt = Mathf.Max(lastTilt, tilt);
                if (stuckFor > 0.6f || timer <= 0f)
                {
                    Unstick();
                    stuckFor = 0f;
                    timer = Mathf.Max(timer, 3f);
                }
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
        fallAxisWorld = swayAxisWorld;
        lastTilt = 0f;
        stuckFor = 0f;
        for (int i = 0; i < count; i++)
        {
            float lo = bounds[i], hi = bounds[i + 1];
            var piece = new GameObject("Fallen Section");
            piece.transform.SetParent(tree, true);
            piece.transform.SetPositionAndRotation(sway.position, sway.rotation);

            // Where the trunk is through this section (it leans and tapers a little), so the wood can be split
            // into the trunk itself and the branches off it.
            float sectionTop = Mathf.Min(hi, height);
            Vector2 centreLo = TrunkAt(sources, lo, radius, out float radiusLo);
            Vector2 centreHi = TrunkAt(sources, Mathf.Max(lo + 0.1f, sectionTop - 0.2f), radius, out float radiusHi);

            float foliageReach = 0f;
            var loEdges = new List<(Vector3, Vector3)>();
            var hiEdges = new List<(Vector3, Vector3)>();
            foreach (var (mesh, toSway, mat) in sources)
            {
                bool wood = mat == barkMaterial;
                Mesh part = Slice(mesh, toSway, lo, hi, wood ? loEdges : null, wood ? hiEdges : null);
                if (part == null) continue;
                if (mat == barkMaterial)
                {
                    // The trunk keeps a collider of its real shape; the branches are only for show: they snap off
                    // and are gone when it hits the ground, so they never prop the logs up.
                    SplitBranches(part, y =>
                    {
                        float f = Mathf.InverseLerp(lo, sectionTop, y);
                        return (Vector2.Lerp(centreLo, centreHi, f), Mathf.Lerp(radiusLo, radiusHi, f));
                    }, out Mesh trunk, out Mesh branches);
                    if (trunk != null)
                    {
                        var go = AddPart(piece.transform, "Bark", trunk, mat);
                        if (trunk.vertexCount >= 4)
                        {
                            var hull = go.AddComponent<MeshCollider>();
                            hull.sharedMesh = trunk;
                            hull.convex = true;
                        }
                    }
                    if (branches != null) AddPart(piece.transform, "Branches", branches, mat);
                    Destroy(part);
                    continue;
                }
                AddPart(piece.transform, "Foliage", part, mat);
                foliageReach = Mathf.Max(foliageReach, Mathf.Max(part.bounds.extents.x, part.bounds.extents.z));
            }

            // Its broken ends: the cut closed with a face of broken wood (exactly the trunk's shape there), and
            // a few splinters standing up out of it.
            AddCutFace(piece.transform, loEdges, centreLo, radiusLo, lo, false);
            if (i < count - 1) AddCutFace(piece.transform, hiEdges, TrunkAt(sources, hi, radius, out float rHi), rHi, hi, true);
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

    struct Vtx
    {
        public Vector3 pos, normal;
        public Vector4 tangent;
        public Vector2 uv;

        public static Vtx Lerp(Vtx a, Vtx b, float t) => new Vtx
        {
            pos = Vector3.Lerp(a.pos, b.pos, t),
            normal = Vector3.Lerp(a.normal, b.normal, t).normalized,
            tangent = Vector4.Lerp(a.tangent, b.tangent, t),
            uv = Vector2.Lerp(a.uv, b.uv, t),
        };
    }

    /// <summary>
    /// The part of a mesh between two heights (sway space), cut cleanly: triangles crossing a cut are clipped
    /// to it exactly, so neighbouring sections meet along a straight line with no gaps or overlaps. The edges
    /// the cuts leave (where the surface crosses each height) are collected, for closing the ends with wood.
    /// </summary>
    static Mesh Slice(Mesh source, Matrix4x4 toSway, float lo, float hi, List<(Vector3, Vector3)> loEdges, List<(Vector3, Vector3)> hiEdges)
    {
        Vector3[] verts = source.vertices;
        Vector3[] normals = source.normals;
        Vector4[] tangents = source.tangents;
        Vector2[] uvs = source.uv;
        int[] tris = source.triangles;
        bool hasN = normals.Length == verts.Length, hasT = tangents.Length == verts.Length, hasUV = uvs.Length == verts.Length;

        var cache = new Vtx[verts.Length];
        for (int i = 0; i < verts.Length; i++)
        {
            Vector3 tan = hasT ? toSway.MultiplyVector(tangents[i]).normalized : Vector3.right;
            cache[i] = new Vtx
            {
                pos = toSway.MultiplyPoint3x4(verts[i]),
                normal = hasN ? toSway.MultiplyVector(normals[i]).normalized : Vector3.up,
                tangent = new Vector4(tan.x, tan.y, tan.z, hasT ? tangents[i].w : 1f),
                uv = hasUV ? uvs[i] : Vector2.zero,
            };
        }

        var outV = new List<Vtx>();
        var outTris = new List<int>();
        var poly = new List<Vtx>(6);
        var clipped = new List<Vtx>(6);
        for (int t = 0; t < tris.Length; t += 3)
        {
            Vtx a = cache[tris[t]], b = cache[tris[t + 1]], c = cache[tris[t + 2]];
            float minY = Mathf.Min(a.pos.y, Mathf.Min(b.pos.y, c.pos.y)), maxY = Mathf.Max(a.pos.y, Mathf.Max(b.pos.y, c.pos.y));
            if (maxY < lo || minY >= hi) continue;
            poly.Clear();
            poly.Add(a); poly.Add(b); poly.Add(c);
            if (minY < lo) { Clip(poly, clipped, lo, true, loEdges); (poly, clipped) = (clipped, poly); }
            if (poly.Count >= 3 && maxY > hi) { Clip(poly, clipped, hi, false, hiEdges); (poly, clipped) = (clipped, poly); }
            if (poly.Count < 3) continue;
            int first = outV.Count;
            outV.AddRange(poly);
            for (int k = 1; k < poly.Count - 1; k++) { outTris.Add(first); outTris.Add(first + k); outTris.Add(first + k + 1); }
        }
        if (outTris.Count == 0) return null;

        var mesh = new Mesh { name = source.name + " Section" };
        if (outV.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        var pos = new List<Vector3>(outV.Count);
        var nrm = new List<Vector3>(outV.Count);
        var tng = new List<Vector4>(outV.Count);
        var uv = new List<Vector2>(outV.Count);
        foreach (var v in outV) { pos.Add(v.pos); nrm.Add(v.normal); tng.Add(v.tangent); uv.Add(v.uv); }
        mesh.SetVertices(pos);
        mesh.SetNormals(nrm);
        if (hasT) mesh.SetTangents(tng);
        if (hasUV) mesh.SetUVs(0, uv);
        mesh.SetTriangles(outTris, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    /// <summary>
    /// Clips a convex polygon to one side of a height (keeping above it, or below), and records the new edge
    /// along the cut.
    /// </summary>
    static void Clip(List<Vtx> input, List<Vtx> output, float h, bool keepAbove, List<(Vector3, Vector3)> edges)
    {
        output.Clear();
        bool Inside(Vtx v) => keepAbove ? v.pos.y >= h : v.pos.y <= h;
        Vector3? firstCut = null, secondCut = null;
        for (int i = 0; i < input.Count; i++)
        {
            Vtx cur = input[i], next = input[(i + 1) % input.Count];
            bool cin = Inside(cur), nin = Inside(next);
            if (cin != nin)
            {
                float t = (h - cur.pos.y) / (next.pos.y - cur.pos.y);
                Vtx cut = Vtx.Lerp(cur, next, t);
                cut.pos.y = h;
                if (cin) { output.Add(cut); }
                else { output.Add(cut); output.Add(next); }
                if (firstCut == null) firstCut = cut.pos; else secondCut = cut.pos;
            }
            else if (cin && nin) output.Add(next);
        }
        if (edges != null && firstCut.HasValue && secondCut.HasValue) edges.Add((firstCut.Value, secondCut.Value));
    }

    /// <summary>
    /// Closes a section's cut end with a face of broken wood: a fan from the trunk's middle to every edge the
    /// cut left round the trunk (the cut edges of branches further out are left alone: they snap off).
    /// </summary>
    void AddCutFace(Transform section, List<(Vector3, Vector3)> edges, Vector2 centre, float trunkRadius, float at, bool facingUp)
    {
        if (edges.Count < 3) return;
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var tris = new List<int>();
        Vector3 middle = new Vector3(centre.x, at, centre.y);
        Vector3 wanted = facingUp ? Vector3.up : Vector3.down;
        float reach = trunkRadius * 1.35f + 0.04f;
        foreach (var (p, q) in edges)
        {
            // Only the trunk's own outline: a branch's cut (further out) is left, it snaps off anyway.
            if ((new Vector2(p.x, p.z) - centre).magnitude > reach || (new Vector2(q.x, q.z) - centre).magnitude > reach) continue;
            Vector3 a = middle, b = p, c = q;
            if (Vector3.Dot(Vector3.Cross(b - a, c - a), wanted) < 0f) (b, c) = (c, b);
            int i = verts.Count;
            verts.Add(a); verts.Add(b); verts.Add(c);
            foreach (var v in new[] { a, b, c })
                uvs.Add(new Vector2(0.5f + (v.x - centre.x) / (trunkRadius * 2.2f), 0.5f + (v.z - centre.y) / (trunkRadius * 2.2f)));
            tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
        }
        if (tris.Count == 0) return;
        var normals = new List<Vector3>(verts.Count);
        for (int i = 0; i < verts.Count; i++) normals.Add(wanted);
        var mesh = new Mesh { name = "Cut" };
        mesh.SetVertices(verts);
        mesh.SetNormals(normals);
        mesh.SetUVs(0, uvs);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateBounds();
        var go = new GameObject("Cut Face");
        go.transform.SetParent(section, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = BreakWoodMaterial;
    }

    static GameObject AddPart(Transform parent, string name, Mesh mesh, Material mat)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    /// <summary>
    /// Splits a section's wood into the trunk and its branches: a triangle reaching out past the trunk's
    /// surface (trunkAt(height) gives the trunk's centre and radius there) belongs to a branch.
    /// </summary>
    static void SplitBranches(Mesh part, System.Func<float, (Vector2 centre, float radius)> trunkAt, out Mesh trunk, out Mesh branches)
    {
        Vector3[] verts = part.vertices;
        Vector3[] normals = part.normals;
        Vector2[] uvs = part.uv;
        int[] tris = part.triangles;
        var trunkTris = new List<int>();
        var branchTris = new List<int>();
        for (int t = 0; t < tris.Length; t += 3)
        {
            bool outside = false;
            for (int k = 0; k < 3 && !outside; k++)
            {
                Vector3 v = verts[tris[t + k]];
                var (centre, r) = trunkAt(v.y);
                outside = (new Vector2(v.x, v.z) - centre).magnitude > r * 1.6f + 0.08f;
            }
            (outside ? branchTris : trunkTris).AddRange(new[] { tris[t], tris[t + 1], tris[t + 2] });
        }
        trunk = MakeMesh(trunkTris, part.name);
        branches = MakeMesh(branchTris, part.name + " Branches");

        Mesh MakeMesh(List<int> keep, string name)
        {
            if (keep.Count == 0) return null;
            var map = new Dictionary<int, int>();
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var tri = new List<int>(keep.Count);
            foreach (int src in keep)
            {
                if (!map.TryGetValue(src, out int dst))
                {
                    dst = v.Count;
                    map[src] = dst;
                    v.Add(verts[src]);
                    if (normals.Length == verts.Length) n.Add(normals[src]);
                    if (uvs.Length == verts.Length) uv.Add(uvs[src]);
                }
                tri.Add(dst);
            }
            var mesh = new Mesh { name = name };
            if (v.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(v);
            if (n.Count == v.Count) mesh.SetNormals(n);
            if (uv.Count == v.Count) mesh.SetUVs(0, uv);
            mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    /// <summary>Called by a section when it hits something.</summary>
    public void OnSectionHit(Collision collision)
    {
        if (phase != Phase.Falling || splintered) return;
        if (collision.relativeVelocity.magnitude < 1.5f) return;
        Splinter(collision.GetContact(0).point, collision.relativeVelocity.magnitude);
    }

    /// <summary>The tree breaks apart where it hits: joints snap, shards fly, and each section finds its own rest.</summary>
    Vector3 fallAxisWorld;
    float lastTilt, stuckFor;

    /// <summary>
    /// Caught on something (another tree's trunk, a branch) and stuck leaning: it stops touching whatever it's
    /// caught on (everything but the ground) and gets a shove the way it was going, so it carries on down.
    /// </summary>
    void Unstick()
    {
        var own = new HashSet<Collider>();
        foreach (var body in pieces) if (body != null) foreach (var c in body.GetComponentsInChildren<Collider>()) own.Add(c);
        foreach (var body in pieces)
        {
            if (body == null) continue;
            foreach (var mine in body.GetComponentsInChildren<Collider>())
            {
                Bounds b = mine.bounds;
                foreach (var other in Physics.OverlapBox(b.center, b.extents * 1.15f + Vector3.one * 0.2f, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (own.Contains(other) || IsGround(other)) continue;
                    Physics.IgnoreCollision(mine, other);
                }
            }
            body.WakeUp();
            body.angularVelocity += fallAxisWorld * 0.6f;
        }
    }

    static bool IsGround(Collider c) =>
        c is TerrainCollider || (c is MeshCollider m && m.sharedMesh != null && m.sharedMesh.name == "Forest Ground");

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
                if (child.name == "Branches")
                {
                    BranchDebris.BreakOff(child); // snapped off: tumbles away, shrinks and is gone (after this loop)
                    continue;
                }
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

    /// <summary>A triangle facing away from a point inside the solid it belongs to, with its own corners (flat shaded).</summary>
    static void Facet(System.Collections.Generic.List<Vector3> verts, System.Collections.Generic.List<Vector2> uvs, System.Collections.Generic.List<int> tris,
        Vector3 a, Vector3 b, Vector3 c, Vector3 inside)
    {
        Vector3 normal = Vector3.Cross(b - a, c - a);
        if (Vector3.Dot(normal, (a + b + c) / 3f - inside) < 0f) (b, c) = (c, b);
        int i = verts.Count;
        verts.Add(a); verts.Add(b); verts.Add(c);
        uvs.Add(new Vector2(0.3f, 0.2f)); uvs.Add(new Vector2(0.7f, 0.2f)); uvs.Add(new Vector2(0.5f, 0.9f));
        tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
    }

    static Mesh[] splinterMeshes;

    /// <summary>
    /// A splinter (unit size: 1 long on y, about 1 across): a jagged three-sided sliver, fattest a little off
    /// its middle and tapering to a ragged point at each end, slightly bent, faceted like torn wood.
    /// </summary>
    static Mesh SplinterMesh()
    {
        if (splinterMeshes == null)
        {
            splinterMeshes = new Mesh[5];
            for (int m = 0; m < splinterMeshes.Length; m++)
            {
                var verts = new System.Collections.Generic.List<Vector3>();
                var uvs = new System.Collections.Generic.List<Vector2>();
                var tris = new System.Collections.Generic.List<int>();
                const int rings = 6;
                float fattest = Random.Range(0.3f, 0.6f), bend = Random.Range(-0.12f, 0.12f);
                var ring = new Vector3[rings + 1][];
                var centre = new Vector3[rings + 1];
                for (int k = 0; k <= rings; k++)
                {
                    float t = k / (float)rings;
                    float y = t - 0.5f;
                    float r = k == 0 || k == rings ? 0f : Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Pow(t, Mathf.Log(0.5f) / Mathf.Log(fattest))), 0.7f) * 0.5f;
                    centre[k] = new Vector3(bend * Mathf.Sin(Mathf.PI * t), y, 0f);
                    ring[k] = new Vector3[3];
                    for (int j = 0; j < 3; j++)
                    {
                        float a = j / 3f * Mathf.PI * 2f + Random.Range(-0.3f, 0.3f);
                        float rr = r * Random.Range(0.7f, 1.15f);
                        ring[k][j] = centre[k] + new Vector3(Mathf.Cos(a) * rr, Random.Range(-0.03f, 0.03f) * (r > 0f ? 1f : 0f), Mathf.Sin(a) * rr * 0.6f);
                    }
                }
                for (int k = 0; k < rings; k++)
                {
                    Vector3 inside = (centre[k] + centre[k + 1]) * 0.5f;
                    for (int j = 0; j < 3; j++)
                    {
                        int jn = (j + 1) % 3;
                        Facet(verts, uvs, tris, ring[k][j], ring[k][jn], ring[k + 1][j], inside);
                        Facet(verts, uvs, tris, ring[k][jn], ring[k + 1][jn], ring[k + 1][j], inside);
                    }
                }
                var mesh = new Mesh { name = "Splinter" };
                mesh.SetVertices(verts);
                mesh.SetUVs(0, uvs);
                mesh.SetTriangles(tris, 0);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                splinterMeshes[m] = mesh;
            }
        }
        return splinterMeshes[Random.Range(0, splinterMeshes.Length)];
    }

    void SpawnShard(Vector3 at, float speed)
    {
        var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
        shard.name = "Splinter";
        shard.GetComponent<MeshFilter>().sharedMesh = SplinterMesh(); // (the cube's box collider stays, for bouncing)
        shard.transform.SetParent(tree, true);
        shard.transform.SetPositionAndRotation(at + Random.insideUnitSphere * 0.2f, Random.rotation);
        // A sliver, not a block: long, thin and flat.
        float length = Random.Range(0.12f, 0.4f);
        shard.transform.localScale = new Vector3(Random.Range(0.02f, 0.045f), length, Random.Range(0.02f, 0.04f));
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
        // Not frozen with the logs (one still flying when they freeze would hang in the air): it lands, lies
        // there a few seconds, and shrinks away.
        shard.AddComponent<ShardFade>();
    }

    static Material splinterMaterial;
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics()
    {
        StaticReset.Drop(ref splinterMaterial);
        StaticReset.Drop(splinterMeshes);
        splinterMeshes = null;
        StaticReset.Drop(ref breakWoodMaterial);
        StaticReset.Drop(ref stumpTopMaterial);
    }
    static Material SplinterMaterial => splinterMaterial != null ? splinterMaterial
        : (splinterMaterial = MaterialFactory.LitTextured(ProceduralTextures.EndGrain, null, new Color(0.2f, 0.15f, 0.1f), 0.0f)); // fresh wood inside, damp and dull (not bright under a flashlight)

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
        // What the trunk should be here, from its taper: the measurement is only trusted near this, since the
        // branches (lots of vertices, reaching metres out) would otherwise swamp it.
        float expected = guessRadius * Mathf.Lerp(1f, 0.45f, Mathf.Clamp01(at / Mathf.Max(height, 0.01f)));
        trunkRadius = expected;
        var near = new List<Vector2>();
        foreach (var (mesh, toSway, mat) in sources)
        {
            if (mat != barkMaterial) continue;
            foreach (var v in mesh.vertices)
            {
                Vector3 p = toSway.MultiplyPoint3x4(v);
                if (Mathf.Abs(p.y - at) < 0.3f && new Vector2(p.x, p.z).magnitude < expected * 3f + 0.1f) near.Add(new Vector2(p.x, p.z));
            }
        }
        if (near.Count < 6) return Vector2.zero;

        // The middle: the average of what's close in; then its surface: the middle of the distances from there,
        // kept within reason of what the taper says.
        Vector2 centre = Vector2.zero;
        int n = 0;
        foreach (var p in near)
            if (p.magnitude < expected * 1.8f + 0.05f) { centre += p; n++; }
        if (n > 0) centre /= n;
        var dist = new List<float>(near.Count);
        foreach (var p in near)
        {
            float d = (p - centre).magnitude;
            if (d < expected * 2f + 0.05f) dist.Add(d);
        }
        if (dist.Count < 4) return centre;
        dist.Sort();
        trunkRadius = Mathf.Clamp(dist[dist.Count / 2], expected * 0.5f, expected * 1.5f);
        return centre;
    }

    /// <summary>
    /// The splinters at a section's broken end: a few long slivers of wood standing up out of the cut face
    /// (AddCutFace) round its edge, leaning a little out.
    /// </summary>
    void AddBreakEnd(Transform section, float at, List<(Mesh mesh, Matrix4x4 toSway, Material mat)> sources, float baseRadius, bool facingUp)
    {
        Vector2 c = TrunkAt(sources, at, baseRadius, out float r);
        float dir = facingUp ? 1f : -1f;
        float size = Mathf.Max(r / 0.25f, 0.5f);
        var verts = new List<Vector3>();
        var uvs = new List<Vector2>();
        var barkTris = new List<int>();
        var woodTris = new List<int>();
        Vector3 P(float x, float y, float z) => new Vector3(c.x + x, y, c.y + z);

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
            // A real sliver with some thickness (a little three-sided wedge rising to its point), each face
            // with its own corners so it lights crisply instead of as a flat sheet of paper.
            Vector3 back = foot - radial * (w * 0.45f);
            Vector3 inside = (foot - side + foot + side + back + tip) * 0.25f;
            Facet(verts, uvs, woodTris, foot - side, foot + side, tip, inside);
            Facet(verts, uvs, woodTris, foot + side, back, tip, inside);
            Facet(verts, uvs, woodTris, back, foot - side, tip, inside);
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

/// <summary>
/// Branches snapped off a fallen tree: they drop and tumble a moment, then shrink away to nothing and are
/// removed, so only the big logs are left lying there.
/// </summary>
public class BranchDebris : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Inspector changes made in Play are kept)

    Vector3 velocity, spin;
    float age;
    Vector3 startScale;
    const float FallTime = 0.9f, ShrinkTime = 1.4f;

    /// <summary>Takes a branches object off its log (at the end of this frame, as the log may be mid-loop) and lets it go.</summary>
    public static void BreakOff(Transform branches)
    {
        var debris = branches.gameObject.AddComponent<BranchDebris>();
        debris.velocity = Random.insideUnitSphere * 1.5f + Vector3.up * 1.2f;
        debris.spin = Random.insideUnitSphere * 40f;
    }

    void Start()
    {
        transform.SetParent(null, true);
        startScale = transform.localScale;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        age += dt;
        velocity += Vector3.down * 9.8f * dt;
        velocity *= 1f - 1.5f * dt; // it's light and catches the air
        transform.position += velocity * dt;
        transform.Rotate(spin * dt, Space.World);
        float shrink = Mathf.Clamp01((age - FallTime) / ShrinkTime);
        transform.localScale = startScale * (1f - shrink * shrink);
        if (shrink >= 1f) Destroy(gameObject);
    }
}

/// <summary>A wood shard from a tree's crash: lies where it lands a few seconds, then shrinks away and is removed.</summary>
public class ShardFade : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Inspector changes made in Play are kept)

    float age, life;
    Vector3 startScale;

    void Start()
    {
        life = Random.Range(3f, 5f);
        startScale = transform.localScale;
    }

    void Update()
    {
        age += Time.deltaTime;
        float shrink = Mathf.Clamp01((age - life) / 1f);
        if (shrink > 0f) transform.localScale = startScale * (1f - shrink);
        if (shrink >= 1f) Destroy(gameObject);
    }
}

/// <summary>A section of a falling tree: tells its TreeFall when it hits something.</summary>
public class FallenSection : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Inspector changes made in Play are kept)

    public TreeFall owner;

    void OnCollisionEnter(Collision collision)
    {
        if (owner != null) owner.OnSectionHit(collision);
    }
}
