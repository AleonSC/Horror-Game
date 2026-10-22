using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Spider webs strung between trees that stand close together, at about head height and below: radial
/// threads and a spiral, sagging a little, nearly invisible until the flashlight catches them. Walk into one
/// and it tears across your face: a dry crackle, and strands clinging over your view for a moment. A torn
/// web is gone, and another is spun somewhere else a while later. DarkForestGenerator adds this.
/// </summary>
public class SpiderWebs : MonoBehaviour
{
    void Awake() => Tuning.Apply(this);

    public int webCount = 45;
    [Tooltip("Seconds before a torn web is replaced (somewhere else).")]
    public float respinTime = 90f;

    class Web
    {
        public Transform root;
        public Vector3 a, b; // the two anchor points (world, canonical)
        public float halfWidth, halfHeight;
    }

    readonly List<Web> webs = new List<Web>();
    DarkForestGenerator generator;
    Transform player;
    Material silk;
    float respin, faceWeb = 0f;
    readonly List<Vector4> strands = new List<Vector4>(); // across your view after walking into one
    AudioSource sfx;

    void Start()
    {
        generator = FindAnyObjectByType<DarkForestGenerator>();
        silk = MaterialFactory.LitTransparent(Texture2D.whiteTexture, new Color(0.85f, 0.85f, 0.82f, 0.32f), 0.9f);
        sfx = ProceduralAudio.AddSource(gameObject, null, false, false, 0.5f);
        for (int i = 0; i < webCount; i++) Spin();
    }

    /// <summary>Strings a new web between two trees that stand close together.</summary>
    bool Spin()
    {
        if (generator == null || generator.TreePositions.Count < 2) return false;
        var trees = generator.TreePositions;
        for (int attempt = 0; attempt < 60; attempt++)
        {
            Vector2 t1 = trees[Random.Range(0, trees.Count)];
            // A neighbour a step or two away.
            Vector2 t2 = Vector2.zero;
            bool found = false;
            int seen = 0;
            foreach (var o in trees)
            {
                float d = (o - t1).magnitude;
                if (d <= 1.2f || d >= 3.4f) continue;
                seen++;
                if (Random.value * seen < 1f) { t2 = o; found = true; } // (any one of them, evenly)
            }
            if (!found) continue;
            Vector3 a = new Vector3(t1.x, 0f, t1.y), b = new Vector3(t2.x, 0f, t2.y);
            Vector3 along = (b - a).normalized;
            // From trunk surface to trunk surface (roughly), at about head height or lower.
            a += along * 0.3f;
            b -= along * 0.3f;
            float ground = Mathf.Max(generator.GetGroundHeight(a.x, a.z), generator.GetGroundHeight(b.x, b.z));
            float centreY = ground + Random.Range(0.9f, 1.7f);
            a.y = b.y = centreY;
            Build(a, b);
            return true;
        }
        return false;
    }

    void Build(Vector3 a, Vector3 b)
    {
        var web = new Web { a = a, b = b };
        var root = new GameObject("Spider Web").transform;
        root.SetParent(transform, false);
        Vector3 mid = (a + b) * 0.5f;
        Vector3 along = b - a;
        root.position = mid;
        root.rotation = Quaternion.LookRotation(Vector3.Cross(along.normalized, Vector3.up), Vector3.up); // its face across the gap
        web.root = root;
        float half = along.magnitude * 0.5f;
        float r = Mathf.Min(half * 0.85f, Random.Range(0.45f, 0.8f));
        web.halfWidth = half;
        web.halfHeight = r;

        var verts = new List<Vector3>();
        var tris = new List<int>();
        const float thread = 0.0035f;
        void Strand(Vector3 p, Vector3 q)
        {
            // A thin ribbon in the web's plane, both faces.
            Vector3 d = (q - p).normalized;
            Vector3 side = Vector3.Cross(d, Vector3.forward).normalized * thread;
            for (int face = 0; face < 2; face++)
            {
                int i = verts.Count;
                verts.Add(p - side); verts.Add(p + side); verts.Add(q + side); verts.Add(q - side);
                if (face == 0) tris.AddRange(new[] { i, i + 1, i + 2, i, i + 2, i + 3 });
                else tris.AddRange(new[] { i, i + 2, i + 1, i, i + 3, i + 2 });
            }
        }
        // Centre a little off-middle; radial spokes out to an irregular frame, a spiral round them, and the
        // anchor lines off to the trunks.
        Vector3 hub = new Vector3(Random.Range(-0.1f, 0.1f), Random.Range(-0.05f, 0.1f), 0f);
        int spokes = Random.Range(10, 15);
        var ends = new Vector3[spokes];
        for (int s = 0; s < spokes; s++)
        {
            float ang = s / (float)spokes * Mathf.PI * 2f + Random.Range(-0.12f, 0.12f);
            float len = r * Random.Range(0.85f, 1.1f);
            ends[s] = hub + new Vector3(Mathf.Cos(ang) * len * (half / Mathf.Max(r, 0.01f)) * 0.75f, Mathf.Sin(ang) * len, 0f);
            Strand(hub, ends[s]);
        }
        for (int s = 0; s < spokes; s++) Strand(ends[s], ends[(s + 1) % spokes]); // the frame
        int turns = Random.Range(7, 11);
        for (int turn = 1; turn <= turns; turn++)
        {
            float f = turn / (float)(turns + 1);
            for (int s = 0; s < spokes; s++)
            {
                if (Random.value < 0.08f) continue; // broken here and there
                Vector3 p = Vector3.Lerp(hub, ends[s], f), q = Vector3.Lerp(hub, ends[(s + 1) % spokes], f + 0.5f / (turns + 1) / spokes);
                p.y -= 0.02f * Mathf.Sin(Mathf.PI * f); // a little sag
                q.y -= 0.02f * Mathf.Sin(Mathf.PI * f);
                Strand(p, q);
            }
        }
        Strand(new Vector3(-half, Random.Range(-0.1f, 0.2f), 0f), ends[spokes / 2]);
        Strand(new Vector3(half, Random.Range(-0.1f, 0.2f), 0f), ends[0]);

        var mesh = new Mesh { name = "Web" };
        if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        mesh.SetVertices(verts);
        mesh.SetTriangles(tris, 0);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        root.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        var renderer = root.gameObject.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = silk;
        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        WorldWrap.RegisterStatic(root);
        webs.Add(web);
    }

    void Update()
    {
        if (player == null)
        {
            var c = FindAnyObjectByType<FirstPersonController>();
            if (c == null) return;
            player = c.transform;
        }
        faceWeb = Mathf.MoveTowards(faceWeb, 0f, Time.deltaTime / 2.5f);

        // Walking into one: it's torn, and clings to your face.
        Vector3 head = player.position + Vector3.up * 1.5f;
        Vector3 knees = player.position + Vector3.up * 0.6f;
        for (int i = webs.Count - 1; i >= 0; i--)
        {
            var w = webs[i];
            if (w.root == null) { webs.RemoveAt(i); continue; }
            Vector3 local = w.root.InverseTransformPoint(head), localLow = w.root.InverseTransformPoint(knees);
            bool through = Mathf.Abs(local.z) < 0.35f && Mathf.Abs(local.x) < w.halfWidth && (Mathf.Abs(local.y) < w.halfHeight || (localLow.y < w.halfHeight && local.y > -w.halfHeight));
            if (!through) continue;
            Destroy(w.root.gameObject);
            webs.RemoveAt(i);
            Tear();
            respin = respinTime;
        }
        if (respin > 0f)
        {
            respin -= Time.deltaTime;
            if (respin <= 0f && webs.Count < webCount) Spin();
        }
    }

    void Tear()
    {
        sfx.pitch = Random.Range(2.2f, 2.8f);
        sfx.PlayOneShot(ProceduralAudio.BushRustle, 0.35f); // the dry crackle of silk tearing
        faceWeb = 1f;
        strands.Clear();
        for (int i = 0; i < 9; i++)
            strands.Add(new Vector4(Random.value, Random.value, Random.value, Random.value));
    }

    /// <summary>Strands of web clinging across your view, fading as you wipe them away.</summary>
    void OnGUI()
    {
        if (faceWeb <= 0f || GameOverScreen.IsActive) return;
        float w = Screen.width, h = Screen.height;
        Matrix4x4 keep = GUI.matrix;
        foreach (var s in strands)
        {
            Vector2 p = new Vector2(s.x * w, s.y * h), q = new Vector2(s.z * w, s.w * h);
            Vector2 d = q - p;
            GUI.color = new Color(0.85f, 0.85f, 0.85f, 0.22f * faceWeb);
            GUIUtility.RotateAroundPivot(Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg, p);
            GUI.DrawTexture(new Rect(p.x, p.y, d.magnitude, Mathf.Max(1f, h / 700f)), Texture2D.whiteTexture);
            GUI.matrix = keep;
        }
        GUI.color = Color.white;
    }
}
