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
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() => All.Clear();

    [Tooltip("Seconds the floodlights stay on.")]
    public float lightsOnTime = 60f;
    [Tooltip("Seconds before they can be turned on again after going off.")]
    public float rechargeTime = 120f;
    [Tooltip("Radius of the lit, safe area around the tower.")]
    public float safeRadius = 45f;
    [Tooltip("Brightness of each floodlight. Lights fall off with the square of distance, and the tower is tall, so this is big.")]
    public float floodlightIntensity = 1400f;
    [Tooltip("How far the floodlights reach.")]
    public float floodlightRange = 120f;
    [Range(0f, 1f), Tooltip("Brightness of the visible beams through the night air.")]
    public float beamBrightness = 0.12f;
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
    [SerializeField] List<Renderer> beams = new List<Renderer>();
    [SerializeField] Light fill;
    Material beamMaterial;
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
        foreach (var flier in new List<FlyingEntity>(FlyingEntity.All)) flier.Repel(transform.position);
    }

    /// <summary>Lights off and ready again (used when the run resets).</summary>
    public void ResetTower()
    {
        LightsOn = false;
        RechargeLeft = 0f;
        lightsSwitched = 0;
        LightsOff();
        if (hum != null) hum.Stop();
        ApplyVisuals();
        SpawnSupplies(); // fresh supplies up top
    }

    void Update()
    {
        if (LightsOn)
        {
            // Lights slam on one after another.
            switchTimer -= Time.deltaTime;
            if (lightsSwitched < lenses.Count && switchTimer <= 0f)
            {
                // Each light covers a pair of lamps: it comes on with the first of them. The fill under the
                // deck comes on with the last.
                lenses[lightsSwitched].sharedMaterial = lensOn;
                if (lightsSwitched < beams.Count) beams[lightsSwitched].enabled = true;
                int light = lightsSwitched / 2;
                if (lightsSwitched % 2 == 0 && light < floodlights.Count) floodlights[light].enabled = true;
                if (lightsSwitched == lenses.Count - 1 && fill != null) fill.enabled = true;
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
            float flicker = dying && Random.value < 0.15f ? Random.Range(0.1f, 0.6f) : 1f;
            foreach (var l in floodlights) l.intensity = floodlightIntensity * flicker;
            if (fill != null) fill.intensity = floodlightIntensity * 0.45f * flicker;
            if (beamMaterial != null)
                beamMaterial.SetColor("_Color", new Color(floodlightColor.r, floodlightColor.g, floodlightColor.b, beamBrightness * flicker));

            if (TimeLeft <= 0f)
            {
                LightsOn = false;
                RechargeLeft = rechargeTime;
                LightsOff();
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
        BuildSupplies(wood);
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

    void LightsOff()
    {
        foreach (var l in floodlights) if (l != null) l.enabled = false;
        foreach (var b in beams) if (b != null) b.enabled = false;
        if (fill != null) fill.enabled = false;
    }

    /// <summary>
    /// Eight lamps around the deck, aimed outward and down over the forest, each throwing a visible beam.
    /// The actual light comes from four wide, strong spots (one per pair of lamps: fewer, brighter lights
    /// stay within the renderer's per-object light limit) and a broad fill straight down from the deck,
    /// so the whole clearing round the tower is lit, from its foot out past the safe radius.
    /// </summary>
    void BuildFloodlights(Material housing)
    {
        Shader beamShader = Shader.Find("Horror/Beam");
        beamMaterial = beamShader != null ? new Material(beamShader) : MaterialFactory.NoFog(floodlightColor * beamBrightness, 3000, false);
        beamMaterial.SetColor("_Color", new Color(floodlightColor.r, floodlightColor.g, floodlightColor.b, beamBrightness));
        Mesh cone = WatcherEvent.BuildBeamMesh();
        const float pitch = 35f, beamHalfAngle = 16f;
        float beamLength = (Height + 2.2f) / Mathf.Sin(pitch * Mathf.Deg2Rad) * 1.05f; // reaching down to the ground

        for (int i = 0; i < 4; i++)
        {
            var spot = new GameObject("Floodlight").AddComponent<Light>();
            spot.transform.SetParent(transform, false);
            spot.transform.localPosition = new Vector3(0f, Height + 2.4f, 0f);
            spot.transform.localRotation = Quaternion.Euler(0f, i * 90f + 45f, 0f) * Quaternion.Euler(40f, 0f, 0f);
            spot.type = LightType.Spot;
            spot.spotAngle = 115f;
            spot.innerSpotAngle = 70f;
            spot.range = floodlightRange;
            spot.intensity = floodlightIntensity;
            spot.color = floodlightColor;
            spot.shadows = LightShadows.Soft; // the trees and shacks' planks throw stripes of shadow
            spot.enabled = false;
            floodlights.Add(spot);
        }

        fill = new GameObject("Floodlight Fill").AddComponent<Light>();
        fill.transform.SetParent(transform, false);
        fill.transform.localPosition = new Vector3(0f, Height - 0.6f, 0f); // under the deck, so the deck doesn't shadow it
        fill.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        fill.type = LightType.Spot;
        fill.spotAngle = 150f;
        fill.innerSpotAngle = 100f;
        fill.range = floodlightRange * 0.6f;
        fill.intensity = floodlightIntensity * 0.45f;
        fill.color = floodlightColor;
        fill.shadows = LightShadows.Soft; // the tower's legs and lattice cast long shadows round its foot
        fill.enabled = false;

        // The lamps themselves: two on each side, hung in yokes from a crossbar between the roof posts.
        var metal = MaterialFactory.Lit(new Color(0.13f, 0.1f, 0.085f), 0.3f);
        var chrome = MaterialFactory.Lit(new Color(0.55f, 0.56f, 0.58f), 0.8f);
        const float barY = Height + 2.42f, pivotY = Height + 2.0f;
        for (int side = 0; side < 4; side++)
        {
            Quaternion rot = Quaternion.Euler(0f, side * 90f, 0f);
            Beam(metal, rot * new Vector3(-DeckHalf, barY, DeckHalf), rot * new Vector3(DeckHalf, barY, DeckHalf), 0.07f, false);
            for (int k = -1; k <= 1; k += 2)
            {
                Quaternion yaw = Quaternion.Euler(0f, side * 90f + k * 18f, 0f); // splayed apart a little
                Vector3 pivot = rot * new Vector3(k * 0.95f, pivotY, DeckHalf + 0.06f);

                // The yoke: two straps down from the bar to bolts on the lamp's sides, and its power cable.
                foreach (float x in new[] { -0.36f, 0.36f })
                    Beam(metal, pivot + yaw * new Vector3(x, 0f, 0f), pivot + yaw * new Vector3(x, barY - pivotY, 0f), 0.035f, false);
                Beam(metal, pivot + yaw * new Vector3(-0.36f, barY - pivotY, 0f), pivot + yaw * new Vector3(0.36f, barY - pivotY, 0f), 0.04f, false);
                Beam(housing, pivot + yaw * new Vector3(0.12f, -0.05f, -0.24f), pivot + yaw * new Vector3(0.3f, barY - pivotY, -0.04f), 0.02f, false);

                var head = new GameObject("Floodlight").transform;
                head.SetParent(transform, false);
                head.localPosition = pivot;
                head.localRotation = yaw * Quaternion.Euler(35f, 0f, 0f); // tipped down over the forest

                // A deep, square housing with a rounded back, cooling fins, a chrome bezel, the lens behind a
                // wire guard, and bolts where it swings in the yoke.
                Box(housing, Vector3.zero, new Vector3(0.6f, 0.44f, 0.3f), false, head);
                Prim(PrimitiveType.Sphere, housing, head, new Vector3(0f, 0f, -0.13f), new Vector3(0.54f, 0.4f, 0.22f), Quaternion.identity);
                for (int f = 0; f < 5; f++)
                    Box(housing, new Vector3(0f, -0.16f + f * 0.08f, -0.2f), new Vector3(0.5f, 0.022f, 0.09f), false, head);
                Box(chrome, new Vector3(0f, 0.235f, 0.16f), new Vector3(0.66f, 0.04f, 0.045f), false, head);
                Box(chrome, new Vector3(0f, -0.235f, 0.16f), new Vector3(0.66f, 0.04f, 0.045f), false, head);
                Box(chrome, new Vector3(0.31f, 0f, 0.16f), new Vector3(0.04f, 0.47f, 0.045f), false, head);
                Box(chrome, new Vector3(-0.31f, 0f, 0.16f), new Vector3(0.04f, 0.47f, 0.045f), false, head);
                var lens = Box(housing, new Vector3(0f, 0f, 0.165f), new Vector3(0.58f, 0.43f, 0.012f), false, head);
                lenses.Add(lens.GetComponent<Renderer>());
                foreach (float gy in new[] { -0.1f, 0.1f })
                    Box(housing, new Vector3(0f, gy, 0.178f), new Vector3(0.58f, 0.012f, 0.012f), false, head);
                Box(housing, new Vector3(0f, 0f, 0.178f), new Vector3(0.012f, 0.43f, 0.012f), false, head);
                foreach (float x in new[] { -0.33f, 0.33f })
                    Prim(PrimitiveType.Cylinder, chrome, head, new Vector3(x, 0f, 0f), new Vector3(0.07f, 0.02f, 0.07f), Quaternion.Euler(0f, 0f, 90f));

                // The visible beam: a long, faint cone of light through the air.
                var beam = new GameObject("Floodlight Beam");
                beam.transform.SetParent(head, false);
                beam.transform.localPosition = new Vector3(0f, 0f, 0.2f);
                float radius = beamLength * Mathf.Tan(beamHalfAngle * Mathf.Deg2Rad);
                beam.transform.localScale = new Vector3(radius, radius, beamLength);
                beam.AddComponent<MeshFilter>().sharedMesh = cone;
                var beamRenderer = beam.AddComponent<MeshRenderer>();
                beamRenderer.sharedMaterial = beamMaterial;
                beamRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                beamRenderer.receiveShadows = false;
                beamRenderer.enabled = false;
                beams.Add(beamRenderer);
            }
        }
    }

    void Prim(PrimitiveType type, Material mat, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = rot;
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        RemoveCollider(go);
    }

    // ---------------------------------------------------------------- Supplies

    Transform crateTop, deckFloor;

    /// <summary>A crate on the deck, in the back corner, with someone's supplies left on and round it.</summary>
    void BuildSupplies(Material wood)
    {
        var crate = Box(wood, new Vector3(-1.25f, Height + 0.075f + 0.22f, -1.25f), new Vector3(0.75f, 0.44f, 0.55f), true);
        crate.localRotation = Quaternion.Euler(0f, 8f, 0f);
        var slat = MaterialFactory.Lit(new Color(0.11f, 0.08f, 0.06f), 0.05f);
        foreach (float y in new[] { -0.3f, 0.3f })
            Box(slat, new Vector3(0f, y, 0.505f), new Vector3(1.02f, 0.12f, 0.02f), false, crate);
        crateTop = new GameObject("Crate Top").transform;
        crateTop.SetParent(crate, false);
        crateTop.localPosition = new Vector3(0f, 0.5f, 0f);
        crateTop.localScale = new Vector3(1f / 0.75f, 1f / 0.44f, 1f / 0.55f); // (undo the crate's own scale)
        deckFloor = new GameObject("Supplies").transform;
        deckFloor.SetParent(transform, false);
        deckFloor.localPosition = new Vector3(-1.2f, Height + 0.075f, -0.55f);
        SpawnSupplies();
        // Where the drone might be, if this is where they come first: on the deck by the crate.
        var droneSpot = new GameObject("Drone Spot").transform;
        droneSpot.SetParent(transform, false);
        droneSpot.localPosition = new Vector3(-0.35f, Height + 0.075f, -1.15f);
        DroneCache.Register(droneSpot, 30f);
    }

    /// <summary>The map and the flare gun on the crate; glowsticks and batteries on the boards beside it.</summary>
    void SpawnSupplies()
    {
        if (crateTop == null || deckFloor == null) return;
        foreach (var root in new[] { crateTop, deckFloor })
        {
            foreach (var old in root.GetComponentsInChildren<ItemPickup>()) Destroy(old.gameObject);
            foreach (var old in root.GetComponentsInChildren<BatteryPickup>()) Destroy(old.gameObject);
        }
        ItemPickup.CreateMap(crateTop, new Vector3(-0.12f, 0f, 0f), Random.Range(-15f, 15f));
        ItemPickup.CreateFlareGun(crateTop, new Vector3(0.16f, 0f, 0.06f), Random.Range(0f, 360f));
        ItemPickup.CreateGlowsticks(deckFloor, new Vector3(0.1f, 0.02f, 0f), Random.Range(0f, 360f));
        ItemPickup.CreateGlowsticks(deckFloor, new Vector3(-0.15f, 0.02f, 0.25f), Random.Range(0f, 360f));
        BatterySpawner.Create(deckFloor, new Vector3(0.35f, 0f, 0.2f), Random.Range(0f, 360f));
        BatterySpawner.Create(deckFloor, new Vector3(0.45f, 0f, -0.05f), Random.Range(0f, 360f));
        BatterySpawner.Create(deckFloor, new Vector3(0.25f, 0f, -0.2f), Random.Range(0f, 360f));
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
