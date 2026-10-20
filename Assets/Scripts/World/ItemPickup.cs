using UnityEngine;

/// <summary>
/// An item lying in the world (a torch or a lighter) that goes into the player's hotbar when clicked.
/// Shack places these on its table; LampInteractor handles the clicking.
/// </summary>
public class ItemPickup : MonoBehaviour
{
    public static readonly System.Collections.Generic.List<ItemPickup> All = new System.Collections.Generic.List<ItemPickup>();

    public Hotbar.Item item;

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    public string DisplayName =>
        item == Hotbar.Item.Torch ? "torch" :
        item == Hotbar.Item.Lighter ? "lighter" :
        item == Hotbar.Item.FlareGun ? "flare gun" :
        item == Hotbar.Item.Glowstick ? "glowsticks" :
        item == Hotbar.Item.Camera ? "camera" :
        item == Hotbar.Item.KeyFob ? "car key fob" : "item";

    /// <summary>Puts the item in the hotbar and removes it from the world.</summary>
    public void PickUp(Hotbar hotbar)
    {
        if (hotbar == null) return;
        hotbar.AddItem(item);
        ProceduralAudio.PlayAt(ProceduralAudio.Pickup, transform.position, 0.8f, 1f, 2f);
        Destroy(gameObject);
    }

    /// <summary>Builds a torch (PropModels.Torch) lying on its side.</summary>
    public static ItemPickup CreateTorch(Transform parent, Vector3 localPos, float yaw)
    {
        var pickup = Create("Torch Pickup", Hotbar.Item.Torch, parent, localPos, yaw);
        var model = PropModels.Torch(pickup.transform, true, out _);
        model.localRotation = Quaternion.Euler(0f, 0f, -90f); // lying along +x, the head to the right
        model.localPosition = new Vector3(-0.25f, 0.035f, 0f);
        return pickup;
    }

    /// <summary>Builds a flare gun (PropModels.FlareGun) lying on its side, label up.</summary>
    public static ItemPickup CreateFlareGun(Transform parent, Vector3 localPos, float yaw)
    {
        var pickup = Create("Flare Gun Pickup", Hotbar.Item.FlareGun, parent, localPos, yaw);
        var model = PropModels.FlareGun(pickup.transform, true, out _);
        // On its side: the gun's right flank up, the barrel resting on the ground.
        model.localRotation = Quaternion.Euler(0f, 0f, 90f);
        model.localPosition = new Vector3(0.05f, 0.022f, -0.04f);
        return pickup;
    }

    /// <summary>Builds an old film camera with a flash gun (PropModels.FilmCamera), lying flat.</summary>
    public static ItemPickup CreateCamera(Transform parent, Vector3 localPos, float yaw)
    {
        var pickup = Create("Camera Pickup", Hotbar.Item.Camera, parent, localPos, yaw);
        PropModels.FilmCamera(pickup.transform, true, out _);
        return pickup;
    }

    /// <summary>Builds a car key fob on its ring (PropModels.KeyFob).</summary>
    public static ItemPickup CreateKeyFob(Transform parent, Vector3 localPos, float yaw)
    {
        var pickup = Create("Key Fob Pickup", Hotbar.Item.KeyFob, parent, localPos, yaw);
        PropModels.KeyFob(pickup.transform, true, out _);
        return pickup;
    }

    /// <summary>Builds a bundle of yellow glowsticks held together with a band.</summary>
    public static ItemPickup CreateGlowsticks(Transform parent, Vector3 localPos, float yaw)
    {
        var pickup = Create("Glowsticks Pickup", Hotbar.Item.Glowstick, parent, localPos, yaw);
        var yellow = MaterialFactory.Lit(Glowstick.GlowColor * 0.6f, 0.6f);
        var band = MaterialFactory.Lit(new Color(0.08f, 0.08f, 0.08f), 0.2f);
        for (int i = 0; i < 5; i++)
        {
            float x = (i - 2) * 0.024f, y = 0.012f + (i % 2) * 0.016f;
            Visual(PrimitiveType.Capsule, yellow, pickup.transform, new Vector3(x, y, 0f), new Vector3(0.022f, 0.08f, 0.022f), new Vector3(90f, 0f, 0f));
        }
        Visual(PrimitiveType.Cube, band, pickup.transform, new Vector3(0f, 0.02f, 0f), new Vector3(0.13f, 0.045f, 0.02f), Vector3.zero);
        return pickup;
    }

    /// <summary>Builds a lighter (PropModels.Lighter) standing shut.</summary>
    public static ItemPickup CreateLighter(Transform parent, Vector3 localPos, float yaw)
    {
        var pickup = Create("Lighter Pickup", Hotbar.Item.Lighter, parent, localPos, yaw);
        PropModels.Lighter(pickup.transform, true, out _, out _);
        return pickup;
    }

    static ItemPickup Create(string name, Hotbar.Item item, Transform parent, Vector3 localPos, float yaw)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPos;
        go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        var pickup = go.AddComponent<ItemPickup>();
        pickup.item = item;

        // Generous invisible box so small items are easy to click.
        var aim = go.AddComponent<BoxCollider>();
        aim.isTrigger = true;
        aim.center = new Vector3(0.05f, 0.15f, 0f);
        aim.size = new Vector3(0.65f, 0.35f, 0.5f);
        return pickup;
    }

    static void Visual(PrimitiveType type, Material mat, Transform parent, Vector3 pos, Vector3 scale, Vector3 euler)
    {
        var go = GameObject.CreatePrimitive(type);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localRotation = Quaternion.Euler(euler);
        go.transform.localScale = scale;
        go.GetComponent<Renderer>().sharedMaterial = mat;
        var col = go.GetComponent<Collider>();
        if (Application.isPlaying) Destroy(col);
        else DestroyImmediate(col);
    }
}
