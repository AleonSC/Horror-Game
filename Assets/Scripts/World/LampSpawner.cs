using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scatters unlit lamps around the map when the game starts, away from trees, the spawn
/// clearing, shacks and each other. Also gives the player a LampInteractor (so they can light them),
/// a Hotbar and Footsteps.
/// DarkForestGenerator adds this automatically.
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

    [Header("Lamp Settings")]
    public float safeRadius = 10f;
    [Range(0.01f, 1f)] public float chargePerClick = 0.06f;

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
            lamp.safeRadius = safeRadius;
            lamp.chargePerClick = chargePerClick;
            LampPaths.Build(lamp.transform, generator);
            lamps.Add(lamp);
            WorldWrap.RegisterStatic(lamp.transform);
        }
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
