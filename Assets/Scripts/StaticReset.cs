using System.Reflection;
using UnityEngine;

/// <summary>
/// All of the game's static state, cleared in one place when Play starts.
///
/// With the editor's Enter Play Mode settings set to skip reloading scripts (no domain reload), anything held
/// in a static field lives on from one play to the next: lists of things that no longer exist, flags left
/// set, "the one that's taking you" still pointing at last game's scour, and cached materials, textures
/// and sounds that Unity destroyed when play stopped (a destroyed material draws solid purple). So every
/// class that keeps static state has a ResetStatics(), and this calls them all before anything else runs.
///
/// Adding static state to a class? Give it a ResetStatics() (or add to the one it has) and list it below.
/// Cached assets made at runtime go through Drop, which destroys them if they're still alive and forgets
/// them, so they're made fresh; built-in meshes and shaders are only forgotten, never destroyed.
/// </summary>
public static class StaticReset
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetAll()
    {
        // Registries of things in the world, and the events and flags that go with them.
        Lamp.ResetStatics();
        ShadowEntity.ResetStatics();
        BlindEntity.ResetStatics();
        FlyingEntity.ResetStatics();
        BrokenCar.ResetStatics();
        Shack.ResetStatics();
        Campsite.ResetStatics();
        Watchtower.ResetStatics();
        BatteryPickup.ResetStatics();
        ItemPickup.ResetStatics();
        Glowstick.ResetStatics();
        Polaroid.ResetStatics();
        Crawler.ResetStatics();
        DroneUnit.ResetStatics();
        DoorKnocks.ResetStatics();
        LampSignpost.ResetStatics();
        DroneCache.ResetStatics();
        SafeZones.ResetStatics();
        Shockwave.ResetStatics();
        GroundLeaves.ResetStatics();
        Water.Clear();
        Noise.ResetStatics();
        ScourClock.ResetStatics();
        RainDrops.ResetStatics();
        PortalDoor.ResetStatics();
        RiftEvent.ResetStatics();
        ScourPortal.ResetStatics();
        ModelEditor.ResetStatics();
        EnemyModel.ResetStatics();
        HdrpAtmosphere.ResetStatics();
        EntityDread.ResetStatics();
        MusicCue.ResetStatics();

        // Singletons and screens.
        WorldWrap.ResetStatics();
        NightSky.ResetStatics();
        Lightning.ResetStatics();
        ScreamEvent.ResetStatics();
        WatcherEvent.ResetStatics();
        MusicManager.ResetStatics();
        FirstPersonController.ResetStatics();
        GameOverScreen.ResetStatics();
        PauseMenu.ResetStatics();
        MainMenu.ResetStatics();
        DevTools.ResetStatics();
        InventoryScreen.ResetStatics();
        IntroText.ResetStatics();

        Tuning.ResetStatics();
        TrailMap.ResetStatics();

        // Cached assets made at runtime.
        DropAllStatic(typeof(ProceduralTextures));
        DropAllStatic(typeof(ProceduralAudio));
        ProceduralAudio.ResetStatics();
        LampTrails.ResetStatics();
        LampPaths.ResetStatics();
        LampGarden.ResetStatics();
        DoorGarden.ResetStatics();
        TreeFall.ResetStatics();
        Flare.ResetStatics();
        Fire.ResetStatics();
        LeafBurst.ResetStatics();
        PickupGlow.ResetStatics();
        Hotbar.ResetStatics();
    }

    /// <summary>Destroys an asset made at runtime if it's still alive, and forgets it.</summary>
    public static void Drop<T>(ref T asset) where T : Object
    {
        if (asset != null) Object.DestroyImmediate(asset);
        asset = null;
    }

    /// <summary>Drop for every asset in an array (and the array's slots are cleared).</summary>
    public static void Drop<T>(T[] assets) where T : Object
    {
        if (assets == null) return;
        for (int i = 0; i < assets.Length; i++) Drop(ref assets[i]);
    }

    /// <summary>
    /// Drop for every static asset field (and array of them) in a class that only caches assets it made itself
    /// (ProceduralTextures, ProceduralAudio), so new ones are covered without listing them here.
    /// </summary>
    static void DropAllStatic(System.Type type)
    {
        const BindingFlags flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
        foreach (var field in type.GetFields(flags))
        {
            if (field.IsLiteral) continue;
            if (typeof(Object).IsAssignableFrom(field.FieldType))
            {
                if (field.IsInitOnly) continue;
                var asset = field.GetValue(null) as Object;
                if (asset != null) Object.DestroyImmediate(asset);
                field.SetValue(null, null);
            }
            else if (field.FieldType.IsArray && typeof(Object).IsAssignableFrom(field.FieldType.GetElementType()))
            {
                if (field.GetValue(null) is Object[] array)
                    for (int i = 0; i < array.Length; i++)
                    {
                        if (array[i] != null) Object.DestroyImmediate(array[i]);
                        array[i] = null;
                    }
            }
        }
    }
}
