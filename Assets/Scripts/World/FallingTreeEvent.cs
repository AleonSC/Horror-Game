using UnityEngine;

/// <summary>
/// A tree near the player snaps at the base and comes crashing down: a loud crack, a slow
/// creaking topple that speeds up, a heavy impact that shakes the camera, a bounce, and a roll
/// as it settles. It leaves a stump, and the fallen trunk blocks the way.
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

    enum Phase { Waiting, Cracking, Falling, Settling }

    Phase phase = Phase.Waiting;
    DarkForestGenerator generator;
    FirstPersonController player;
    float clock, timer, nextFall = -1f;

    Transform tree, sway;
    Quaternion restRotation;
    Vector3 fallAxis;      // in the tree's local space
    float height, angle, angularVelocity, roll, rollTarget, impactAngle, shake;
    int bounces;
    AudioSource audioSource;

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
                if (timer <= 0f)
                {
                    phase = Phase.Falling;
                    angle = 2f;
                    angularVelocity = 0f;
                    audioSource.pitch = 0.6f;
                    audioSource.PlayOneShot(ProceduralAudio.TreeCreak, volume);
                }
                break;

            case Phase.Falling:
            {
                // A rod pivoting at its base: it starts slowly and speeds up as it leans further.
                float accel = 1.5f * 9.81f / Mathf.Max(height, 1f) * Mathf.Sin(angle * Mathf.Deg2Rad) * Mathf.Rad2Deg;
                angularVelocity += accel * dt;
                angle += angularVelocity * dt;

                if (angle >= impactAngle)
                {
                    angle = impactAngle;
                    bounces++;
                    if (bounces == 1)
                    {
                        Vector3 impactPoint = sway.position + sway.up * (height * 0.6f);
                        ProceduralAudio.PlayAt(ProceduralAudio.TreeImpact, impactPoint, volume, Random.Range(0.9f, 1.05f), 10f);
                        float dist = Vector3.Distance(impactPoint, player.transform.position);
                        shake = Mathf.Lerp(3f, 0.5f, Mathf.InverseLerp(5f, 30f, dist));
                    }
                    angularVelocity = -angularVelocity * 0.22f; // bounce
                    if (bounces >= 3 || Mathf.Abs(angularVelocity) < 4f)
                    {
                        phase = Phase.Settling;
                        timer = 1.5f;
                    }
                }
                break;
            }

            case Phase.Settling:
                // Rolls a little on its branches as it comes to rest.
                timer -= dt;
                roll = Mathf.Lerp(roll, rollTarget, 1f - Mathf.Exp(-3f * dt));
                if (timer <= 0f) Finish();
                break;
        }

        if (sway != null && (phase == Phase.Cracking || phase == Phase.Falling || phase == Phase.Settling))
            sway.localRotation = Quaternion.AngleAxis(angle, fallAxis) * Quaternion.AngleAxis(roll, Vector3.up) * restRotation;

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
        impactAngle = Random.Range(84f, 89f);
        rollTarget = Random.Range(-25f, 25f);
        angle = roll = angularVelocity = 0f;
        bounces = 0;

        AddStump(trunk);

        audioSource = ProceduralAudio.AddSource(tree.gameObject, null, false, true, volume, 10f, 120f);
        audioSource.PlayOneShot(ProceduralAudio.TreeSnap, volume);

        phase = Phase.Cracking;
        timer = 0.7f;
        return true;
    }

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
    }

    void Finish()
    {
        phase = Phase.Waiting;

        // Swap the standing trunk collider for one that lies with the fallen tree.
        var standing = tree.GetComponent<CapsuleCollider>();
        float radius = standing != null ? standing.radius : 0.25f;
        if (standing != null) standing.enabled = false;

        var fallen = sway.gameObject.AddComponent<CapsuleCollider>();
        fallen.direction = 1; // along the trunk
        fallen.radius = radius;
        fallen.height = height;
        fallen.center = new Vector3(0f, height * 0.5f, 0f);
    }
}
