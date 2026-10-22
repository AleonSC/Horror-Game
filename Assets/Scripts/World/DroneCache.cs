using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Where the drone turns up: there's only one, and it's at whichever of the cabin, the watchtower or the
/// broken car the player comes across first (in the cabin's store room, up on the tower's deck, or on the
/// car's back seat), put there as they draw near, before they can see in. The others never have it. A new
/// run starts the search again. The cabin, tower and car register their spots; DarkForestGenerator adds this.
/// </summary>
public class DroneCache : MonoBehaviour
{
    struct Site { public Transform spot; public float reach; }

    static readonly List<Site> sites = new List<Site>();
    static bool placed;
    static Transform placedAt;
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() { sites.Clear(); placed = false; placedAt = null; }

    /// <summary>A place the drone could be: the spot it'd sit, and how near the player has to come.</summary>
    public static void Register(Transform spot, float reach)
    {
        if (spot != null) sites.Add(new Site { spot = spot, reach = reach });
    }

    /// <summary>A new run: wherever it was, it's gone, and it'll be wherever they come across first again.</summary>
    public static void ResetRun()
    {
        foreach (var site in sites)
        {
            if (site.spot == null) continue;
            foreach (var p in site.spot.GetComponentsInChildren<ItemPickup>(true))
                if (p.item == Hotbar.Item.Drone) Destroy(p.gameObject);
        }
        placed = false;
        placedAt = null;
    }

    Transform player;
    float check;

    void Update()
    {
        if (placed || sites.Count == 0) return;
        check -= Time.deltaTime;
        if (check > 0f) return;
        check = 0.25f;
        if (player == null)
        {
            var c = FindAnyObjectByType<FirstPersonController>();
            if (c == null) return;
            player = c.transform;
        }
        if (MainMenu.Showing) return;
        foreach (var site in sites)
        {
            if (site.spot == null) continue;
            Vector3 d = WorldWrap.NearestImage(site.spot.position, player.position) - player.position;
            d.y = 0f;
            if (d.magnitude > site.reach) continue;
            ItemPickup.CreateDrone(site.spot, Vector3.zero, Random.Range(-40f, 40f));
            placed = true;
            placedAt = site.spot;
            return;
        }
    }
}
