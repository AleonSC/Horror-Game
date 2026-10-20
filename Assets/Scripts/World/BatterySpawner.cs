using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Scatters flashlight batteries at random spots around the map each time the game starts
/// (and again when the run resets), away from trees and from each other.
/// DarkForestGenerator adds this automatically.
/// </summary>
public class BatterySpawner : MonoBehaviour
{
    public int batteryCount = 50;
    [Tooltip("Keep batteries at least this far from the middle of the map (where the player spawns).")]
    public float minDistanceFromCenter = 8f;
    public float minSpacing = 12f;
    [Tooltip("Keep batteries at least this far from any tree.")]
    public float treeClearance = 1.2f;
    [Tooltip("Map size when there's no DarkForestGenerator in the scene.")]
    public float areaSize = 320f;
    [Range(0f, 1f), Tooltip("How much of the flashlight each battery refills.")]
    public float chargePerBattery = 0.25f;

    readonly List<BatteryPickup> batteries = new List<BatteryPickup>();

    IEnumerator Start()
    {
        yield return null; // let the forest generate first
        SpawnBatteries();
    }

    /// <summary>Removes any remaining batteries and scatters a fresh set.</summary>
    public void SpawnBatteries()
    {
        foreach (var battery in batteries) if (battery != null) Destroy(battery.gameObject);
        batteries.Clear();

        var generator = FindAnyObjectByType<DarkForestGenerator>();
        float size = generator != null ? generator.areaSize : areaSize;
        float half = size * 0.5f - 4f;
        var placed = new List<Vector3>();

        for (int attempt = 0; attempt < batteryCount * 200 && placed.Count < batteryCount; attempt++)
        {
            var p = new Vector3(Random.Range(-half, half), 0f, Random.Range(-half, half));
            if (new Vector2(p.x, p.z).magnitude < minDistanceFromCenter) continue;
            if (TooClose(p, placed)) continue;
            if (generator != null && (NearTree(generator, p) || generator.IsNearStructure(p, 7f))) continue;

            p.y = GroundHeight(generator, p);
            placed.Add(p);

            var battery = new GameObject("Battery").AddComponent<BatteryPickup>();
            battery.transform.SetParent(transform, false);
            battery.transform.SetPositionAndRotation(p, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            battery.charge = chargePerBattery;
            batteries.Add(battery);
            WorldWrap.RegisterStatic(battery.transform);
        }
    }

    bool TooClose(Vector3 p, List<Vector3> placed)
    {
        foreach (var q in placed)
            if ((q - p).sqrMagnitude < minSpacing * minSpacing) return true;
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
}
