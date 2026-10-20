using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// A very tall, rusty lattice watchtower with floodlights around the top. Climb the ladder, press the
/// button, and the floodlights flood the area with light for a while: every entity's hunt is reset
/// (they're blasted away and start over from a distance) and the lit area is safe ground.
/// Afterwards the lights need time to recharge. DarkForestGenerator places one somewhere on the map.
/// </summary>
public class Watchtower : MonoBehaviour, SafeZones.ISource
{
    public static readonly List<Watchtower> All = new List<Watchtower>();

    [Tooltip("Seconds the floodlights stay on.")]
    public float lightsOnTime = 60f;
    [Tooltip("Seconds before they can be turned on again after going off.")]
    public float rechargeTime = 120f;
    [Tooltip("Radius of the lit, safe area around the tower.")]
    public float safeRadius = 45f;
    public float floodlightIntensity = 10f;
    public Color floodlightColor = new Color(0.85f, 0.9f, 1f);

    const float Height = 18f, BaseHalf = 2f, TopHalf = 1.5f, DeckHalf = 2.2f;

    public bool LightsOn { get; private set; }
    public float TimeLeft { get; private set; }
    public float RechargeLeft { get; private set; }
    public bool IsRecharging => RechargeLeft > 0f;

    bool SafeZones.ISource.IsActive => LightsOn;
    Vector3 SafeZones.ISource.Center => transform.position;
    float SafeZones.ISource.Radius => safeRadius;

    [SerializeField] List<Light> floodlights = new List<Light>();
    [SerializeField] List<Renderer> lenses = new List<Renderer>();
    [SerializeField] Renderer buttonRenderer;
    Material lensOff, lensOn, buttonReady, buttonOn, buttonWait;
    AudioSource hum, sfx;
    float switchTimer;
    int lightsSwitched;

    void OnEnable()
    {
        All.Add(this);
        SafeZones.Register(this);
    }

    void OnDisable()
    {
        All.Remove(this);
        SafeZones.Unregister(this);
    }

    void Start()
    {
        lensOff = MaterialFactory.Unlit(new Color(0.06f, 0.06f, 0.07f));
        lensOn = MaterialFactory.Unlit(floodlightColor * 5f);
        buttonReady = MaterialFactory.Unlit(new Color(0.9f, 0.1f, 0.05f) * 1.5f);
        buttonOn = MaterialFactory.Unlit(new Color(0.2f, 1f, 0.3f) * 1.5f);
        buttonWait = MaterialFactory.Unlit(new Color(0.15f, 0.05f, 0.04f));

        var deck = new GameObject("Tower Audio");
        deck.transform.SetParent(transform, false);
        deck.transform.localPosition = Vector3.up * Height;
        hum = ProceduralAudio.AddSource(deck, ProceduralAudio.LampHum, true, true, 0f, 6f, 80f);
        hum.pitch = 0.7f;
        sfx = ProceduralAudio.AddSource(deck, null, false, true, 1f, 10f, 120f);
        ApplyVisuals();
    }

    /// <summary>The tower the player is up (on its ladder, or above the ground within its footprint), if any.</summary>
    public static Watchtower TowerPlayerIsOn(Vector3 playerPosition, FirstPersonController player)
    {
        foreach (var tower in All)
        {
            if (player != null && player.CurrentLadder != null && player.CurrentLadder.transform.IsChildOf(tower.transform))
                return tower;
            Vector3 local = tower.transform.InverseTransformPoint(playerPosition);
            if (Mathf.Abs(local.x) < DeckHalf + 1.2f && Mathf.Abs(local.z) < DeckHalf + 1.2f && local.y > 2.5f)
                return tower;
        }
        return null;
    }

    /// <summary>Grabbing the ladder calls every entity: the waiting ones appear right away.</summary>
    public void OnLadderTouched()
    {
        foreach (var entity in ShadowEntity.All) entity.Summon();
    }

    /// <summary>Turns the floodlights on (if they're ready) and resets every entity's hunt.</summary>
    public void Activate()
    {
        if (LightsOn || IsRecharging)
        {
            sfx.PlayOneShot(ProceduralAudio.FlashlightClick, 0.6f); // nothing happens
            return;
        }

        LightsOn = true;
        TimeLeft = lightsOnTime;
        lightsSwitched = 0;
        switchTimer = 0f;
        sfx.PlayOneShot(ProceduralAudio.LampClick, 0.8f);

        foreach (var entity in new List<ShadowEntity>(ShadowEntity.All))
            entity.ResetHunt();
        if (WatcherEvent.Instance != null) WatcherEvent.Instance.PushBack(); // even the giant is driven back
    }

    /// <summary>Lights off and ready again (used when the run resets).</summary>
    public void ResetTower()
    {
        LightsOn = false;
        RechargeLeft = 0f;
        lightsSwitched = 0;
        foreach (var l in floodlights) l.enabled = false;
        if (hum != null) hum.Stop();
        ApplyVisuals();
    }

    void Update()
    {
        if (LightsOn)
        {
            // Lights slam on one after another.
            switchTimer -= Time.deltaTime;
            if (lightsSwitched < floodlights.Count && switchTimer <= 0f)
            {
                floodlights[lightsSwitched].enabled = true;
                lenses[lightsSwitched].sharedMaterial = lensOn;
                lightsSwitched++;
                switchTimer = 0.18f;
                sfx.pitch = Random.Range(0.9f, 1.1f);
                sfx.PlayOneShot(ProceduralAudio.Floodlight, 0.9f);
                if (!hum.isPlaying) hum.Play();
            }

            hum.volume = Mathf.MoveTowards(hum.volume, 0.5f, Time.deltaTime);
            TimeLeft -= Time.deltaTime;

            // Flicker as they're about to cut out.
            bool dying = TimeLeft < 5f;
            for (int i = 0; i < lightsSwitched; i++)
                floodlights[i].intensity = floodlightIntensity * (dying && Random.value < 0.15f ? Random.Range(0.1f, 0.6f) : 1f);

            if (TimeLeft <= 0f)
            {
                LightsOn = false;
                RechargeLeft = rechargeTime;
                foreach (var l in floodlights) l.enabled = false;
                hum.Stop();
                sfx.pitch = 0.8f;
                sfx.PlayOneShot(ProceduralAudio.LampPowerDown, 1f);
            }
        }
        else if (IsRecharging)
        {
            RechargeLeft = Mathf.Max(0f, RechargeLeft - Time.deltaTime);
        }

        ApplyVisuals();
    }

    void ApplyVisuals()
    {
        if (buttonRenderer == null || buttonReady == null) return;
        buttonRenderer.sharedMaterial = LightsOn ? buttonOn : IsRecharging ? buttonWait : buttonReady;
        if (!LightsOn)
            foreach (var lens in lenses) lens.sharedMaterial = lensOff;
    }

    // ---------------------------------------------------------------- Building

    /// <summary>Builds the tower at its current position (pivot on the ground at the centre).</summary>
    public void Build(DarkForestGenerator generator)
    {
        var metal = MaterialFactory.Lit(new Color(0.13f, 0.1f, 0.085f), 0.3f);
        var wood = MaterialFactory.Lit(new Color(0.15f, 0.11f, 0.08f), 0.05f);
        var dark = MaterialFactory.Lit(new Color(0.05f, 0.05f, 0.055f), 0.5f);

        // Footings sunk into the slope, so no leg floats.
        float lowest = float.MaxValue;
        for (int cx = -1; cx <= 1; cx += 2)
        for (int cz = -1; cz <= 1; cz += 2)
        {
            Vector3 c = transform.TransformPoint(new Vector3(cx * BaseHalf, 0f, cz * BaseHalf));
            lowest = Mathf.Min(lowest, generator.GetGroundHeight(c.x, c.z) - transform.position.y);
        }

        // Four tapering legs with X-bracing on every face.
        var corners = new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) };
        for (int i = 0; i < 4; i++)
        {
            Vector2 c = corners[i];
            Beam(metal, new Vector3(c.x * BaseHalf, lowest - 0.5f, c.y * BaseHalf), new Vector3(c.x * TopHalf, Height, c.y * TopHalf), 0.14f, true);

            Vector2 n = corners[(i + 1) % 4];
            for (int level = 0; level < 4; level++)
            {
                float y0 = level * Height / 4f, y1 = (level + 1) * Height / 4f;
                Vector3 a0 = LegPoint(c, y0), a1 = LegPoint(c, y1), b0 = LegPoint(n, y0), b1 = LegPoint(n, y1);
                Beam(metal, a0, b1, 0.05f, false);
                Beam(metal, b0, a1, 0.05f, false);
                Beam(metal, a1, b1, 0.07f, false);
            }
        }

        // Deck, railing (with a gap where the ladder arrives) and a small roof.
        Box(wood, new Vector3(0f, Height, 0f), new Vector3(DeckHalf * 2f, 0.15f, DeckHalf * 2f), true);
        for (int side = 0; side < 4; side++)
        {
            Quaternion rot = Quaternion.Euler(0f, side * 90f, 0f);
            for (int r = 0; r < 2; r++)
            {
                float y = Height + 0.5f + r * 0.55f;
                if (side == 0)
                {
                    // Front: leave a gap for the ladder.
                    RailSegment(metal, rot, -DeckHalf, -0.6f, y);
                    RailSegment(metal, rot, 0.6f, DeckHalf, y);
                }
                else RailSegment(metal, rot, -DeckHalf, DeckHalf, y);
            }
            for (int post = -1; post <= 1; post += 2)
                Beam(metal, rot * new Vector3(post * DeckHalf, Height, DeckHalf), rot * new Vector3(post * DeckHalf, Height + 2.6f, DeckHalf), 0.07f, false);
        }
        // Invisible railing walls so the player can't fall off (except at the ladder).
        RailCollider(new Vector3(0f, 0f, -DeckHalf), new Vector3(DeckHalf * 2f, 1.2f, 0.1f));
        RailCollider(new Vector3(-DeckHalf, 0f, 0f), new Vector3(0.1f, 1.2f, DeckHalf * 2f));
        RailCollider(new Vector3(DeckHalf, 0f, 0f), new Vector3(0.1f, 1.2f, DeckHalf * 2f));
        RailCollider(new Vector3(-(DeckHalf + 0.6f) * 0.5f, 0f, DeckHalf), new Vector3(DeckHalf - 0.6f, 1.2f, 0.1f));
        RailCollider(new Vector3((DeckHalf + 0.6f) * 0.5f, 0f, DeckHalf), new Vector3(DeckHalf - 0.6f, 1.2f, 0.1f));
        var roof = Box(dark, new Vector3(0f, Height + 2.75f, 0f), new Vector3(DeckHalf * 2.3f, 0.12f, DeckHalf * 2.3f), false);
        roof.localRotation = Quaternion.Euler(2f, 0f, -1.5f);

        BuildLadder(metal);
        BuildFloodlights(dark);
        BuildButton(metal);
    }

    Vector3 LegPoint(Vector2 corner, float y)
    {
        float half = Mathf.Lerp(BaseHalf, TopHalf, y / Height);
        return new Vector3(corner.x * half, y, corner.y * half);
    }

    void RailSegment(Material mat, Quaternion rot, float from, float to, float y)
    {
        Beam(mat, rot * new Vector3(from, y, DeckHalf), rot * new Vector3(to, y, DeckHalf), 0.05f, false);
    }

    void BuildLadder(Material mat)
    {
        // On the front face, just outside the deck, from the ground up past the deck.
        float z = DeckHalf + 0.35f;
        float bottom = -1f, top = Height + 1f;
        for (int side = -1; side <= 1; side += 2)
            Beam(mat, new Vector3(side * 0.25f, bottom, z), new Vector3(side * 0.25f, top, z), 0.05f, false);
        for (float y = 0.3f; y < Height + 0.2f; y += 0.32f)
            Beam(mat, new Vector3(-0.25f, y, z), new Vector3(0.25f, y, z), 0.03f, false);

        var go = new GameObject("Ladder");
        go.transform.SetParent(transform, false);
        var trigger = go.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.center = new Vector3(0f, (bottom + Height + 1.3f) * 0.5f, z + 0.1f);
        trigger.size = new Vector3(1f, Height + 1.3f - bottom, 1.6f); // reaches a little over the deck edge
        var ladder = go.AddComponent<Ladder>();
        ladder.localClimbPoint = new Vector3(0f, 0f, z + 0.5f);
        ladder.localTopY = Height;
    }

    void BuildFloodlights(Material housing)
    {
        // Eight lamps around the deck, aimed outward and down over the forest.
        for (int i = 0; i < 8; i++)
        {
            float yaw = i * 45f + 22.5f;
            Quaternion facing = Quaternion.Euler(0f, yaw, 0f);
            Vector3 pos = facing * new Vector3(0f, Height + 2.2f, DeckHalf + 0.2f);

            var mount = new GameObject("Floodlight").transform;
            mount.SetParent(transform, false);
            mount.localPosition = pos;
            mount.localRotation = facing * Quaternion.Euler(35f, 0f, 0f);

            var box = Box(housing, Vector3.zero, new Vector3(0.6f, 0.45f, 0.35f), false, mount);
            box.localPosition = Vector3.zero;
            var lens = Box(housing, new Vector3(0f, 0f, 0.18f), new Vector3(0.5f, 0.35f, 0.02f), false, mount);
            lenses.Add(lens.GetComponent<Renderer>());

            var light = new GameObject("Floodlight Beam").AddComponent<Light>();
            light.transform.SetParent(mount, false);
            light.transform.localPosition = new Vector3(0f, 0f, 0.3f);
            light.type = LightType.Spot;
            light.spotAngle = 80f;
            light.innerSpotAngle = 50f;
            light.range = 70f;
            light.intensity = floodlightIntensity;
            light.color = floodlightColor;
            light.shadows = LightShadows.None;
            light.enabled = false;
            floodlights.Add(light);
        }
    }

    void BuildButton(Material metal)
    {
        // A post with a big button, by the ladder.
        Box(metal, new Vector3(0.9f, Height + 0.55f, DeckHalf - 0.6f), new Vector3(0.25f, 1.1f, 0.25f), true);
        var button = Box(metal, new Vector3(0.9f, Height + 1.15f, DeckHalf - 0.6f), new Vector3(0.2f, 0.08f, 0.2f), false);
        button.name = "Floodlight Button";
        buttonRenderer = button.GetComponent<Renderer>();
        var trigger = button.gameObject.AddComponent<BoxCollider>();
        trigger.isTrigger = true;
        trigger.size = new Vector3(3f, 6f, 3f); // easy to click (scaled by the button's size)
        button.gameObject.AddComponent<WatchtowerButton>().tower = this;
    }

    void RailCollider(Vector3 center, Vector3 size)
    {
        var go = new GameObject("Railing Collider");
        go.transform.SetParent(transform, false);
        go.transform.localPosition = center + Vector3.up * Height;
        var box = go.AddComponent<BoxCollider>();
        box.center = new Vector3(0f, size.y * 0.5f, 0f);
        box.size = size;
    }

    void Beam(Material mat, Vector3 a, Vector3 b, float thickness, bool keepCollider)
    {
        Vector3 d = b - a;
        var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = (a + b) * 0.5f;
        go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, d);
        go.transform.localScale = new Vector3(thickness, d.magnitude * 0.5f, thickness);
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!keepCollider) RemoveCollider(go);
    }

    Transform Box(Material mat, Vector3 pos, Vector3 scale, bool keepCollider, Transform parent = null)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.SetParent(parent != null ? parent : transform, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        if (!keepCollider) RemoveCollider(go);
        return go.transform;
    }

    static void RemoveCollider(GameObject go)
    {
        var col = go.GetComponent<Collider>();
        if (Application.isPlaying) Destroy(col);
        else DestroyImmediate(col);
    }
}
