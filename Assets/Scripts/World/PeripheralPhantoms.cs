using UnityEngine;

/// <summary>
/// Things that shouldn't be in the woods, seen only at the edge of your sight. Now and then, as you turn
/// your head, something's put down just past the edge of the view on the side you're turning toward, a few
/// metres off in the dark (a wooden chair, a stop sign, a hospital gurney, an old television, a mailbox, a
/// child's tricycle): you sweep past it. Look away, and when you look back it's gone. If you never catch
/// sight of it, it quietly goes anyway. DarkForestGenerator adds this.
/// </summary>
public class PeripheralPhantoms : MonoBehaviour
{
    void Awake() => Tuning.Apply(this);

    [Tooltip("Seconds between them (at least, then a random wait up to the max).")]
    public float minInterval = 50f;
    public float maxInterval = 130f;
    [Tooltip("How fast you have to be turning (degrees a second) for one to be put in your way.")]
    public float turnSpeed = 70f;
    [Tooltip("How far off they stand.")]
    public float minDistance = 7f;
    public float maxDistance = 12f;
    [Tooltip("Lamps lit before they start.")]
    public int fromLamp = 1;

    enum Kind { Chair, StopSign, Gurney, Television, Mailbox, Tricycle }

    Transform current;
    bool seen;
    float timer, life, lastYaw;
    DarkForestGenerator generator;

    void Start() => timer = Random.Range(minInterval, maxInterval);

    void Update()
    {
        var cam = Camera.main;
        if (cam == null) return;
        float dt = Time.deltaTime;
        float yaw = cam.transform.eulerAngles.y;
        float turning = dt > 0f ? Mathf.DeltaAngle(lastYaw, yaw) / dt : 0f;
        lastYaw = yaw;

        if (current != null)
        {
            UpdateCurrent(cam, dt);
            return;
        }

        timer -= dt;
        bool allowed = Lamp.TimesLit >= fromLamp && !MainMenu.Showing && !GameOverScreen.IsActive && !PortalDoor.PlayerInRoom
                       && !DroneUnit.Piloting && !PauseMenu.IsPaused;
        if (timer > 0f || !allowed || Mathf.Abs(turning) < turnSpeed) return;
        if (TrySpawn(cam, Mathf.Sign(turning))) timer = Random.Range(minInterval, maxInterval);
    }

    /// <summary>
    /// Watching for you to see it and then look away (then it's gone); it goes anyway if you never see it, or
    /// after a while.
    /// </summary>
    void UpdateCurrent(Camera cam, float dt)
    {
        life += dt;
        bool inView = InView(cam, current.position + Vector3.up * 0.6f);
        if (inView) seen = true;
        bool gone = (seen && !inView) || (!seen && life > 5f) || (life > 25f && !inView);
        if (gone || GameOverScreen.IsActive)
        {
            Destroy(current.gameObject);
            current = null;
        }
    }

    static bool InView(Camera cam, Vector3 p)
    {
        Vector3 v = cam.WorldToViewportPoint(p);
        return v.z > 0f && v.x > -0.05f && v.x < 1.05f && v.y > -0.1f && v.y < 1.1f;
    }

    /// <summary>Puts one just past the edge of the view on the side you're turning to.</summary>
    bool TrySpawn(Camera cam, float side)
    {
        if (generator == null) generator = FindAnyObjectByType<DarkForestGenerator>();
        if (generator == null) return false;
        float halfH = Camera.VerticalToHorizontalFieldOfView(cam.fieldOfView, cam.aspect) * 0.5f;
        Vector3 forward = cam.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.01f) return false;
        forward.Normalize();
        for (int attempt = 0; attempt < 6; attempt++)
        {
            float angle = side * (halfH + Random.Range(8f, 22f));
            Vector3 dir = Quaternion.Euler(0f, angle, 0f) * forward;
            Vector3 p = cam.transform.position + dir * Random.Range(minDistance, maxDistance);
            p.y = generator.GetGroundHeight(p.x, p.z);
            if (SafeZones.Contains(p) || Water.InWater(p, out _)) continue;
            if (Physics.CheckSphere(p + Vector3.up * 0.9f, 0.6f, ~0, QueryTriggerInteraction.Ignore)) continue; // in a tree
            Vector3 toYou = cam.transform.position - p;
            toYou.y = 0f;
            var kind = (Kind)Random.Range(0, 6);
            current = Build(kind, p, Quaternion.LookRotation(toYou.normalized) * Quaternion.Euler(0f, Random.Range(-35f, 35f), 0f));
            seen = false;
            life = 0f;
            return true;
        }
        return false;
    }

    // ---------------------------------------------------------------- The things

    Transform Build(Kind kind, Vector3 at, Quaternion facing)
    {
        var root = new GameObject("Phantom " + kind).transform;
        root.SetPositionAndRotation(at, facing);
        var wood = MaterialFactory.Lit(new Color(0.22f, 0.15f, 0.09f), 0.1f);
        var metal = MaterialFactory.Lit(new Color(0.35f, 0.35f, 0.36f), 0.6f);
        var dark = MaterialFactory.Lit(new Color(0.05f, 0.05f, 0.05f), 0.3f);
        switch (kind)
        {
            case Kind.Chair:
                // A plain wooden kitchen chair, standing as if someone just got up from it.
                P(PrimitiveType.Cube, wood, root, new Vector3(0f, 0.46f, 0f), new Vector3(0.44f, 0.04f, 0.42f));
                foreach (float x in new[] { -0.19f, 0.19f })
                    foreach (float z in new[] { -0.18f, 0.18f })
                        P(PrimitiveType.Cube, wood, root, new Vector3(x, 0.22f, z), new Vector3(0.035f, 0.46f, 0.035f));
                foreach (float x in new[] { -0.19f, 0.19f })
                    P(PrimitiveType.Cube, wood, root, new Vector3(x, 0.72f, -0.18f), new Vector3(0.035f, 0.5f, 0.035f));
                for (int i = 0; i < 3; i++)
                    P(PrimitiveType.Cube, wood, root, new Vector3(0f, 0.6f + i * 0.13f, -0.18f), new Vector3(0.4f, 0.05f, 0.02f));
                break;

            case Kind.StopSign:
            {
                // A stop sign on its post, leaning a little, out here in the trees.
                root.rotation *= Quaternion.Euler(0f, 0f, Random.Range(-5f, 5f));
                P(PrimitiveType.Cylinder, metal, root, new Vector3(0f, 1.1f, 0f), new Vector3(0.05f, 1.1f, 0.05f));
                var red = MaterialFactory.Lit(new Color(0.55f, 0.04f, 0.03f), 0.4f);
                var white = MaterialFactory.Lit(new Color(0.75f, 0.75f, 0.72f), 0.4f);
                for (int i = 0; i < 4; i++)
                {
                    // An octagon from four overlapping bars, white edged.
                    var rim = P(PrimitiveType.Cube, white, root, new Vector3(0f, 2.1f, 0.035f), new Vector3(0.66f, 0.28f, 0.02f));
                    rim.localRotation = Quaternion.Euler(0f, 0f, i * 45f);
                    var face = P(PrimitiveType.Cube, red, root, new Vector3(0f, 2.1f, 0.05f), new Vector3(0.6f, 0.25f, 0.02f));
                    face.localRotation = Quaternion.Euler(0f, 0f, i * 45f);
                }
                P(PrimitiveType.Cube, white, root, new Vector3(0f, 2.1f, 0.062f), new Vector3(0.4f, 0.07f, 0.005f)); // the word, a pale bar
                break;
            }

            case Kind.Gurney:
            {
                // A hospital gurney, a sheet over it, and something under the sheet.
                var sheet = MaterialFactory.Lit(new Color(0.62f, 0.62f, 0.6f), 0.05f);
                P(PrimitiveType.Cube, metal, root, new Vector3(0f, 0.75f, 0f), new Vector3(0.62f, 0.05f, 1.9f));
                foreach (float x in new[] { -0.27f, 0.27f })
                    foreach (float z in new[] { -0.85f, 0.85f })
                    {
                        P(PrimitiveType.Cylinder, metal, root, new Vector3(x, 0.4f, z), new Vector3(0.03f, 0.35f, 0.03f));
                        var wheel = P(PrimitiveType.Cylinder, dark, root, new Vector3(x, 0.05f, z), new Vector3(0.09f, 0.015f, 0.09f));
                        wheel.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    }
                P(PrimitiveType.Cube, sheet, root, new Vector3(0f, 0.8f, 0f), new Vector3(0.66f, 0.03f, 1.94f));
                var shape = P(PrimitiveType.Capsule, sheet, root, new Vector3(0f, 0.9f, 0.05f), new Vector3(0.38f, 0.75f, 0.24f));
                shape.localRotation = Quaternion.Euler(90f, 0f, 0f);
                P(PrimitiveType.Sphere, sheet, root, new Vector3(0f, 0.92f, 0.82f), new Vector3(0.22f, 0.18f, 0.24f));
                break;
            }

            case Kind.Television:
            {
                // An old box television sitting on the ground, its screen dead grey.
                var plastic = MaterialFactory.Lit(new Color(0.16f, 0.13f, 0.1f), 0.3f);
                var glass = MaterialFactory.Lit(new Color(0.12f, 0.14f, 0.13f), 0.85f);
                P(PrimitiveType.Cube, plastic, root, new Vector3(0f, 0.26f, 0f), new Vector3(0.6f, 0.48f, 0.5f));
                P(PrimitiveType.Cube, glass, root, new Vector3(-0.06f, 0.27f, 0.25f), new Vector3(0.42f, 0.34f, 0.01f));
                foreach (float y in new[] { 0.36f, 0.26f })
                    P(PrimitiveType.Cylinder, dark, root, new Vector3(0.22f, y, 0.255f), new Vector3(0.05f, 0.01f, 0.05f)).localRotation = Quaternion.Euler(90f, 0f, 0f);
                var aerial = P(PrimitiveType.Cylinder, metal, root, new Vector3(-0.08f, 0.68f, 0f), new Vector3(0.008f, 0.22f, 0.008f));
                aerial.localRotation = Quaternion.Euler(0f, 0f, 30f);
                var aerial2 = P(PrimitiveType.Cylinder, metal, root, new Vector3(0.08f, 0.68f, 0f), new Vector3(0.008f, 0.22f, 0.008f));
                aerial2.localRotation = Quaternion.Euler(0f, 0f, -30f);
                break;
            }

            case Kind.Mailbox:
            {
                // A rural mailbox on a post, its flag up.
                P(PrimitiveType.Cube, wood, root, new Vector3(0f, 0.55f, 0f), new Vector3(0.09f, 1.1f, 0.09f));
                var box = P(PrimitiveType.Capsule, metal, root, new Vector3(0f, 1.2f, 0f), new Vector3(0.22f, 0.25f, 0.22f));
                box.localRotation = Quaternion.Euler(90f, 0f, 0f);
                P(PrimitiveType.Cube, MaterialFactory.Lit(new Color(0.5f, 0.05f, 0.03f), 0.3f), root, new Vector3(0.13f, 1.33f, -0.05f), new Vector3(0.015f, 0.18f, 0.06f));
                break;
            }

            case Kind.Tricycle:
            {
                // A child's tricycle, red paint flaking.
                var red = MaterialFactory.Lit(new Color(0.42f, 0.06f, 0.04f), 0.4f);
                var front = P(PrimitiveType.Cylinder, dark, root, new Vector3(0f, 0.22f, 0.32f), new Vector3(0.44f, 0.02f, 0.44f));
                front.localRotation = Quaternion.Euler(0f, 0f, 90f);
                foreach (float x in new[] { -0.2f, 0.2f })
                {
                    var back = P(PrimitiveType.Cylinder, dark, root, new Vector3(x, 0.13f, -0.2f), new Vector3(0.26f, 0.015f, 0.26f));
                    back.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
                P(PrimitiveType.Cylinder, metal, root, new Vector3(0f, 0.13f, -0.2f), new Vector3(0.02f, 0.2f, 0.02f)).localRotation = Quaternion.Euler(0f, 0f, 90f);
                var frame = P(PrimitiveType.Cylinder, red, root, new Vector3(0f, 0.3f, 0.05f), new Vector3(0.04f, 0.28f, 0.04f));
                frame.localRotation = Quaternion.Euler(70f, 0f, 0f);
                P(PrimitiveType.Cube, dark, root, new Vector3(0f, 0.42f, -0.05f), new Vector3(0.16f, 0.04f, 0.2f)); // seat
                P(PrimitiveType.Cylinder, red, root, new Vector3(0f, 0.45f, 0.3f), new Vector3(0.03f, 0.22f, 0.03f));
                var bars = P(PrimitiveType.Cylinder, metal, root, new Vector3(0f, 0.66f, 0.28f), new Vector3(0.02f, 0.18f, 0.02f));
                bars.localRotation = Quaternion.Euler(0f, 0f, 90f);
                root.rotation *= Quaternion.Euler(0f, 0f, Random.value < 0.3f ? 75f : 0f); // sometimes on its side
                break;
            }
        }
        return root;
    }

    static Transform P(PrimitiveType type, Material mat, Transform parent, Vector3 pos, Vector3 scale)
    {
        var go = GameObject.CreatePrimitive(type);
        Destroy(go.GetComponent<Collider>());
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        return go.transform;
    }
}
