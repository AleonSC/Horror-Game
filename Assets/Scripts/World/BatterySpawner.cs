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
    void Awake() => Tuning.Apply(this); // saved tuning (Save Tuning on the component's menu)

    [Tooltip("Batteries scattered through the woods. More lie at campsites, sometimes on shack tables, and and deer drop one every time they're startled.")]
    public int batteryCount = 30;
    [Tooltip("Keep batteries at least this far from the middle of the map (where the player spawns).")]
    public float minDistanceFromCenter = 8f;
    public float minSpacing = 12f;
    [Tooltip("Keep batteries at least this far from any tree.")]
    public float treeClearance = 1.2f;
    [Tooltip("Map size when there's no DarkForestGenerator in the scene.")]
    public float areaSize = 320f;
    [Range(0f, 1f), Tooltip("Unused: each battery in the hotbar refills Hotbar.batteryCharge of the flashlight.")]
    public float chargePerBattery = 0.1f;

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

        int count = Mathf.RoundToInt(batteryCount * Difficulty.Batteries);
        for (int attempt = 0; attempt < count * 200 && placed.Count < count; attempt++)
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

    /// <summary>A startled deer drops a battery where it stood (the run reset clears it with the rest).</summary>
    public void DropFromDeer(Vector3 at)
    {
        var generator = FindAnyObjectByType<DarkForestGenerator>();
        at.y = GroundHeight(generator, at);
        var battery = Create(null, at, Random.Range(0f, 360f), chargePerBattery);
        battery.transform.SetParent(transform, true);
        batteries.Add(battery);
        WorldWrap.RegisterStatic(battery.transform);
        ProceduralAudio.PlayAt(ProceduralAudio.TwigSnap, at, 0.5f, 1.6f, 3f); // a soft clatter as it lands
    }

    /// <summary>Makes a battery lying at a point (in the parent's space if there is one).</summary>
    public static BatteryPickup Create(Transform parent, Vector3 localPos, float yaw, float charge = 0.25f)
    {
        var battery = new GameObject("Battery").AddComponent<BatteryPickup>();
        if (parent != null) battery.transform.SetParent(parent, false);
        battery.transform.localPosition = localPos;
        battery.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        battery.charge = charge;
        return battery;
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
