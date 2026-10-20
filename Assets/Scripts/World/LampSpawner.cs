using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scatters unlit lamps around the map when the game starts, away from trees, the spawn
/// clearing, shacks and each other. Also gives the player a LampInteractor (so they can light them),
/// a Hotbar and Footsteps.
///
/// Every lamp's settings live here ("Lamp Settings"), so all the lamps can be tuned in one place. The lamps
/// themselves only exist while playing: change these during Play and every lamp updates straight away
/// (but Play-mode changes are lost when you stop). To keep settings, add a Lamp Spawner to the scene
/// yourself (GameObject > Create Empty, Add Component > Lamp Spawner) and set them there before pressing
/// Play; DarkForestGenerator uses that one instead of making its own.
/// </summary>
public class LampSpawner : MonoBehaviour
{
    public int lampCount = 24;
    [Tooltip("Keep lamps at least this far from the middle of the map (where the player spawns).")]
    public float minDistanceFromCenter = 15f;
    public float minLampSpacing = 25f;
    [Tooltip("Keep lamps at least this far from any tree.")]
    public float treeClearance = 2f;
    [Tooltip("Map size when there's no DarkForestGenerator in the scene.")]
    public float areaSize = 320f;

    [Header("Lamp Settings: Charging")]
    [Range(0.01f, 1f), Tooltip("How much each click fills the meter.")]
    public float chargePerClick = 0.06f;
    [Tooltip("Seconds without clicking before the meter starts draining.")]
    public float drainDelay = 1.5f;
    [Tooltip("Meter lost per second while draining.")]
    public float drainRate = 0.08f;

    [Header("Lamp Settings: When Lit")]
    [Tooltip("Radius of the safe zone (the light reaches a bit further).")]
    public float safeRadius = 10f;
    public float lightIntensity = 4f;
    public Color lightColor = new Color(1f, 0.75f, 0.45f);
    [Tooltip("Summon another entity each time a lamp is lit.")]
    public bool spawnEntityWhenLit = true;

    [Header("Lamp Settings: Energy")]
    [Tooltip("Seconds a lit lamp lasts before going out.")]
    public float energyDuration = 90f;
    [Range(0f, 1f), Tooltip("Below this much energy the light starts flickering.")]
    public float flickerBelow = 0.35f;
    [Tooltip("Seconds after going out before the lamp can be lit again.")]
    public float cooldownTime = 30f;
    [Tooltip("Colour of the sparks while a lamp recharges.")]
    public Color sparkColor = new Color(0.45f, 0.65f, 1f);
    [Tooltip("Average sparks per second while recharging.")]
    public float sparksPerSecond = 1.2f;

    readonly List<Lamp> lamps = new List<Lamp>();

    IEnumerator Start()
    {
        yield return null; // let the forest generate first
        SpawnLamps();
        EnsureInteractor();
    }

    public void SpawnLamps()
    {
        foreach (var lamp in lamps) if (lamp != null) Destroy(lamp.gameObject);
        lamps.Clear();

        var generator = FindAnyObjectByType<DarkForestGenerator>();
        float size = generator != null ? generator.areaSize : areaSize;
        float half = size * 0.5f - 6f;
        var placed = new List<Vector3>();

        for (int attempt = 0; attempt < lampCount * 200 && placed.Count < lampCount; attempt++)
        {
            var p = new Vector3(Random.Range(-half, half), 0f, Random.Range(-half, half));
            if (new Vector2(p.x, p.z).magnitude < minDistanceFromCenter) continue;
            if (TooClose(p, placed)) continue;
            if (generator != null && (NearTree(generator, p) || generator.IsNearStructure(p, 7f))) continue;

            p.y = GroundHeight(generator, p);
            placed.Add(p);

            var lamp = new GameObject("Lamp").AddComponent<Lamp>();
            lamp.transform.SetParent(transform, false);
            lamp.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            ApplySettings(lamp);
            LampPaths.Build(lamp.transform, generator);
            lamps.Add(lamp);
            WorldWrap.RegisterStatic(lamp.transform);
        }
    }

    /// <summary>Copies the Lamp Settings onto a lamp.</summary>
    void ApplySettings(Lamp lamp)
    {
        lamp.chargePerClick = chargePerClick;
        lamp.drainDelay = drainDelay;
        lamp.drainRate = drainRate;
        lamp.safeRadius = safeRadius;
        lamp.lightIntensity = lightIntensity;
        lamp.lightColor = lightColor;
        lamp.spawnEntityWhenLit = spawnEntityWhenLit;
        lamp.energyDuration = energyDuration;
        lamp.flickerBelow = flickerBelow;
        lamp.cooldownTime = cooldownTime;
        lamp.sparkColor = sparkColor;
        lamp.sparksPerSecond = sparksPerSecond;
        lamp.RefreshSettings();
    }

    /// <summary>Changing the settings in the Inspector during Play updates every lamp at once.</summary>
    void OnValidate()
    {
        if (!Application.isPlaying) return;
        foreach (var lamp in lamps)
            if (lamp != null) ApplySettings(lamp);
    }

    bool TooClose(Vector3 p, List<Vector3> placed)
    {
        foreach (var q in placed)
            if ((q - p).sqrMagnitude < minLampSpacing * minLampSpacing) return true;
        return false;
    }

    bool NearTree(DarkForestGenerator generator, Vector3 p)
    {
        var flat = new Vector2(p.x, p.z);
        foreach (var tree in generator.TreePositions)
            if ((tree - flat).sqrMagnitude < treeClearance * treeClearance) return true;
        return false;
    }

    static float GroundHeight(DarkForestGenerator generator, Vector3 p)
    {
        if (generator != null) return generator.GetGroundHeight(p.x, p.z);
        if (Physics.Raycast(p + Vector3.up * 100f, Vector3.down, out var hit, 200f, ~0, QueryTriggerInteraction.Ignore))
            return hit.point.y;
        return 0f;
    }

    static void EnsureInteractor()
    {
        var player = FindAnyObjectByType<FirstPersonController>();
        if (player == null) return;
        if (player.GetComponent<LampInteractor>() == null) player.gameObject.AddComponent<LampInteractor>();
        if (player.GetComponent<Hotbar>() == null) player.gameObject.AddComponent<Hotbar>();
        if (player.GetComponent<Footsteps>() == null) player.gameObject.AddComponent<Footsteps>();
    }
}
