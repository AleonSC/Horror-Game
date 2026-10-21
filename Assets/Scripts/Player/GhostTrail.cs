using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A sixth sense for where you've already been. Everywhere you walk leaves an invisible trail; step back
/// onto it later and a few metres of it wake up round your feet, each way: a ribbon of pale spectral light
/// along the ground with streaks drifting along it the way you went, motes of light drifting with them, and
/// a soft shimmer of sound. Step off and it fades. Only what you're touching shows, never the whole map.
/// (Your own fresh footsteps, just behind you, don't count.) Hotbar adds this to the player.
/// </summary>
public class GhostTrail : MonoBehaviour
{
    [Tooltip("A point is laid down each time you've gone this far.")]
    public float spacing = 0.5f;
    [Tooltip("How close you have to be to an old stretch of trail to feel it.")]
    public float touchRadius = 1.1f;
    [Tooltip("How much of it shows each way from where you touch it (metres along the trail).")]
    public float revealLength = 4.5f;
    [Tooltip("The last this-many metres you've walked don't count (they're always under your feet).")]
    public float ignoreRecent = 14f;
    public Color color = new Color(0.55f, 0.85f, 1f);
    [Range(0f, 2f)] public float intensity = 0.9f;
    [Range(0f, 1f)] public float volume = 0.35f;
    public int maxPoints = 8000;

    readonly List<Vector3> points = new List<Vector3>();
    readonly List<float> along = new List<float>();   // distance along the trail at each point
    readonly List<bool> breakBefore = new List<bool>(); // a gap (teleported: the map's edge, a respawn, the door)
    float total;
    Vector3 lastFeet;
    bool haveLast, gap;
    float searchTimer;
    DarkForestGenerator generator;
    AudioSource sfx;
    Material lineMat, moteMat;

    class Window
    {
        public float center = -1f;    // where along the trail it's centred
        public float reveal, target;  // 0 hidden to 1 shown
        public LineRenderer line;
        public readonly List<Transform> motes = new List<Transform>();
        public readonly List<float> moteOffset = new List<float>();
    }
    readonly Window[] windows = new Window[3];
    readonly List<float> contacts = new List<float>();

    void Start()
    {
        generator = FindAnyObjectByType<DarkForestGenerator>();
        sfx = ProceduralAudio.AddSource(gameObject, null, false, false, volume);
        Shader trail = Shader.Find("Horror/Trail"), glow = Shader.Find("Horror/Glow");
        if (trail == null) { enabled = false; return; }
        lineMat = new Material(trail);
        lineMat.SetColor("_Color", color);
        lineMat.SetFloat("_Intensity", intensity);
        if (glow != null)
        {
            moteMat = new Material(glow);
            moteMat.SetColor("_Color", color);
        }
        for (int w = 0; w < windows.Length; w++) windows[w] = MakeWindow(w);
    }

    Window MakeWindow(int index)
    {
        var win = new Window();
        var go = new GameObject("Ghost Trail " + index);
        win.line = go.AddComponent<LineRenderer>();
        win.line.sharedMaterial = lineMat;
        win.line.useWorldSpace = true;
        win.line.alignment = LineAlignment.TransformZ;
        go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f); // lying flat on the ground, facing up
        win.line.textureMode = LineTextureMode.Tile;
        win.line.widthMultiplier = 0.45f;
        win.line.numCornerVertices = 2;
        win.line.numCapVertices = 2;
        win.line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        win.line.receiveShadows = false;
        win.line.enabled = false;
        if (moteMat != null)
            for (int i = 0; i < 10; i++)
            {
                var mote = GameObject.CreatePrimitive(PrimitiveType.Quad);
                mote.name = "Trail Mote";
                Destroy(mote.GetComponent<Collider>());
                mote.transform.SetParent(go.transform, false);
                var r = mote.GetComponent<Renderer>();
                r.sharedMaterial = moteMat;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mote.SetActive(false);
                win.motes.Add(mote.transform);
                win.moteOffset.Add(Random.Range(0f, 1f));
            }
        return win;
    }

    void OnDestroy()
    {
        foreach (var win in windows) if (win != null && win.line != null) Destroy(win.line.gameObject);
    }

    float Ground(Vector3 p) => generator != null ? generator.GetGroundHeight(p.x, p.z) : p.y;

    void Update()
    {
        Vector3 feet = transform.position;
        Record(feet);

        searchTimer -= Time.deltaTime;
        if (searchTimer <= 0f)
        {
            searchTimer = 0.1f;
            FindContacts(feet);
            MatchWindows();
        }
        foreach (var win in windows) Draw(win, feet);
    }

    /// <summary>Lays down the trail as you go (none in the dark room; a jump leaves a gap).</summary>
    void Record(Vector3 feet)
    {
        if (PortalDoor.PlayerInRoom) { gap = true; haveLast = false; return; }
        if (!haveLast) { lastFeet = feet; haveLast = true; Add(feet); return; }
        Vector3 step = feet - lastFeet;
        step.y = 0f;
        if (step.magnitude > 3f) { gap = true; lastFeet = feet; Add(feet); return; } // teleported
        if (step.magnitude < spacing) return;
        total += step.magnitude;
        lastFeet = feet;
        Add(feet);
    }

    void Add(Vector3 feet)
    {
        Vector3 p = new Vector3(feet.x, Ground(feet) + 0.05f, feet.z);
        points.Add(p);
        along.Add(total);
        breakBefore.Add(gap || points.Count == 1);
        gap = false;
        if (points.Count > maxPoints)
        {
            int drop = maxPoints / 8;
            points.RemoveRange(0, drop);
            along.RemoveRange(0, drop);
            breakBefore.RemoveRange(0, drop);
            foreach (var win in windows) if (win != null) win.center = -1f; // old windows lose their place
        }
    }

    /// <summary>The places (as distances along the trail) where you're standing on an old stretch of it.</summary>
    void FindContacts(Vector3 feet)
    {
        contacts.Clear();
        float recentFrom = total - ignoreRecent;
        float bestDist = float.MaxValue;
        int best = -1;
        // Up to three separate crossings: the nearest point of each run of nearby points.
        void Emit()
        {
            if (best >= 0 && contacts.Count < windows.Length) contacts.Add(along[best]);
            best = -1;
            bestDist = float.MaxValue;
        }
        for (int i = 0; i < points.Count; i++)
        {
            if (along[i] > recentFrom) break;
            if (breakBefore[i]) Emit();
            Vector3 d = WorldWrap.NearestImage(points[i], feet) - feet;
            d.y = 0f;
            float dist = d.magnitude;
            if (dist < touchRadius) { if (dist < bestDist) { bestDist = dist; best = i; } }
            else Emit();
        }
        Emit();
    }

    /// <summary>Keeps each lit stretch with the crossing it belongs to; new crossings wake a free one.</summary>
    void MatchWindows()
    {
        foreach (var win in windows) win.target = 0f;
        foreach (float c in contacts)
        {
            Window match = null;
            foreach (var win in windows)
                if (win.center >= 0f && Mathf.Abs(win.center - c) < revealLength * 1.5f) { match = win; break; }
            if (match == null)
                foreach (var win in windows)
                    if (win.reveal <= 0.01f) { match = win; win.center = c; win.reveal = 0f; if (sfx != null) { sfx.pitch = Random.Range(0.9f, 1.15f); sfx.PlayOneShot(ProceduralAudio.TrailChime, volume); } break; }
            if (match == null) continue;
            match.center = Mathf.Lerp(match.center, c, 0.5f);
            match.target = 1f;
        }
    }

    void Draw(Window win, Vector3 feet)
    {
        win.reveal = Mathf.MoveTowards(win.reveal, win.target, Time.deltaTime / (win.target > 0f ? 0.6f : 1.2f));
        bool show = win.reveal > 0.01f && win.center >= 0f && points.Count > 1;
        if (win.line.enabled != show) win.line.enabled = show;
        if (!show)
        {
            foreach (var m in win.motes) if (m.gameObject.activeSelf) m.gameObject.SetActive(false);
            if (win.reveal <= 0.01f && win.target <= 0f) win.center = -1f;
            return;
        }

        // The stretch round the centre, spreading out as it wakes, never across a gap.
        float half = revealLength * Mathf.SmoothStep(0.2f, 1f, win.reveal);
        int c = IndexAt(win.center);
        int lo = c, hi = c;
        while (lo > 0 && !breakBefore[lo] && win.center - along[lo - 1] <= half) lo--;
        while (hi < points.Count - 1 && !breakBefore[hi + 1] && along[hi + 1] - win.center <= half) hi++;
        int count = hi - lo + 1;
        if (count < 2) { win.line.enabled = false; return; }
        var positions = new Vector3[count];
        for (int i = 0; i < count; i++) positions[i] = WorldWrap.NearestImage(points[lo + i], feet);
        win.line.positionCount = count;
        win.line.SetPositions(positions);

        // Faded at both ends, the whole thing breathing.
        float a = Mathf.SmoothStep(0f, 1f, win.reveal) * (0.85f + 0.15f * Mathf.Sin(Time.time * 2.3f));
        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(a, 0.3f), new GradientAlphaKey(a, 0.7f), new GradientAlphaKey(0f, 1f) });
        win.line.colorGradient = gradient;

        // Motes of light drifting along it the way you went, rising and falling.
        float length = along[hi] - along[lo];
        var cam = Camera.main;
        for (int k = 0; k < win.motes.Count; k++)
        {
            var mote = win.motes[k];
            if (!mote.gameObject.activeSelf) mote.gameObject.SetActive(true);
            float u = Mathf.Repeat(win.moteOffset[k] + Time.time * 0.25f, 1f);
            Vector3 at = PointAlong(positions, lo, along[lo] + u * length);
            float bob = 0.15f + 0.35f * Mathf.Abs(Mathf.Sin(Time.time * 1.3f + k * 1.7f));
            mote.position = at + Vector3.up * bob;
            if (cam != null) mote.rotation = cam.transform.rotation;
            float fade = Mathf.Sin(Mathf.PI * u) * win.reveal;
            mote.localScale = Vector3.one * 0.14f * fade;
        }
        if (moteMat != null) moteMat.SetFloat("_Intensity", 0.5f);
    }

    /// <summary>The index of the point nearest a distance along the trail.</summary>
    int IndexAt(float distance)
    {
        int lo = 0, hi = along.Count - 1;
        while (lo < hi)
        {
            int mid = (lo + hi) / 2;
            if (along[mid] < distance) lo = mid + 1; else hi = mid;
        }
        return lo;
    }

    /// <summary>A point a distance along the trail, within the stretch drawn (positions start at index first).</summary>
    Vector3 PointAlong(Vector3[] positions, int first, float distance)
    {
        for (int i = 0; i < positions.Length - 1; i++)
        {
            float a = along[first + i], b = along[first + i + 1];
            if (distance <= b || i == positions.Length - 2)
                return Vector3.Lerp(positions[i], positions[i + 1], b > a ? Mathf.Clamp01((distance - a) / (b - a)) : 0f);
        }
        return positions[0];
    }
}
