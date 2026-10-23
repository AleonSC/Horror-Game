using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shown when an entity catches the player: fades to black, shows a message, then brings them back and
/// fades in. Progress is kept: every lamp stays lit, everything they're carrying stays with them, the
/// world stays as they left it. They wake (groggy) by the lamp they lit last (or at the start, if none), the
/// scours melt away and come back to hunt afresh after a while, the blind one and the flier are gone for a
/// bit, and the giant's back at the horizon. The pause menu's Restart starts the whole run over (RestartRun).
/// </summary>
public class GameOverScreen : MonoBehaviour
{
    void Awake() => Tuning.Apply(this); // saved tuning (Inspector changes made in Play are kept)

    const float DefaultFadeOutTime = 1.2f;
    const float HoldTime = 3f;
    const float FadeInTime = 1.5f;

    static GameOverScreen instance;
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() => instance = null;
    public static bool IsActive => instance != null;

    float timer;
    bool hasReset;
    float FadeOutTime = DefaultFadeOutTime;
    GUIStyle style;

    public static void Show() => Show(DefaultFadeOutTime);

    /// <summary>Starts the game over, taking fadeOutTime seconds to fade to black (longer for a drawn-out end).</summary>
    public static void Show(float fadeOutTime)
    {
        if (instance != null) return;
        instance = new GameObject("Game Over").AddComponent<GameOverScreen>();
        instance.FadeOutTime = Mathf.Max(0.1f, fadeOutTime);
        // The sting: the moment you're taken.
        var sting = ProceduralAudio.AddSource(instance.gameObject, null, false, false, 1f);
        sting.PlayOneShot(ProceduralAudio.Taken, 0.9f);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (!hasReset && timer >= FadeOutTime + HoldTime)
        {
            hasReset = true;
            Respawn();
        }

        if (timer >= FadeOutTime + HoldTime + FadeInTime) Destroy(gameObject);
    }

    /// <summary>Starts the run over straight away (the pause menu's Restart).</summary>
    public static void RestartRun()
    {
        if (instance != null) Destroy(instance.gameObject);
        ResetRun();
    }

    /// <summary>Back from being caught, keeping all progress (see the class summary).</summary>
    static void Respawn()
    {
        foreach (var entity in new List<ShadowEntity>(ShadowEntity.All))
            entity.Retreat(Random.Range(20f, 35f));
        foreach (var blind in new List<BlindEntity>(BlindEntity.All))
            blind.Retreat(30f);
        foreach (var flier in new List<FlyingEntity>(FlyingEntity.All))
            flier.Banish(30f);
        Shockwave.Cancel();

        var player = FindAnyObjectByType<FirstPersonController>();
        if (player != null)
        {
            // By the last lamp they lit, in its light; or back at the start.
            var lamp = Lamp.LastLit;
            if (lamp != null)
            {
                Vector2 off = Random.insideUnitCircle.normalized * 2.4f;
                Vector3 at = lamp.transform.position + new Vector3(off.x, 0f, off.y);
                var generator = FindAnyObjectByType<DarkForestGenerator>();
                if (generator != null) at.y = generator.GetGroundHeight(at.x, at.z) + 0.1f;
                Vector3 face = lamp.transform.position - at;
                face.y = 0f;
                player.RespawnAt(at, Quaternion.LookRotation(face.sqrMagnitude > 0.01f ? -face : Vector3.forward));
            }
            else player.ResetToSpawn();
            player.enabled = true;
        }

        var flashlight = FindAnyObjectByType<Flashlight>();
        if (flashlight != null) flashlight.EnsureCharge(0.3f); // enough to get going again

        if (WatcherEvent.Instance != null) WatcherEvent.Instance.AfterDeath();
    }

    /// <summary>Everything back to the very start (the pause menu's Restart).</summary>
    static void ResetRun()
    {
        foreach (var entity in new List<ShadowEntity>(ShadowEntity.All))
            Destroy(entity.gameObject);
        foreach (var blind in new List<BlindEntity>(BlindEntity.All))
            Destroy(blind.gameObject);
        foreach (var flier in new List<FlyingEntity>(FlyingEntity.All))
            Destroy(flier.gameObject);

        foreach (var lamp in Lamp.All)
            lamp.ResetLamp();
        Lamp.ResetTimesLit();
        ScourClock.ResetRun();
        BlindEntitySpawner.ResetRun();

        var batteries = FindAnyObjectByType<BatterySpawner>();
        if (batteries != null) batteries.SpawnBatteries();

        var flashlight = FindAnyObjectByType<Flashlight>();
        if (flashlight != null) flashlight.ResetBattery();
        var runner = FindAnyObjectByType<FirstPersonController>();
        if (runner != null) runner.ResetStaminaBonus(); // (the snacks are back in the tents)

        var hotbar = FindAnyObjectByType<Hotbar>();
        if (hotbar != null) hotbar.ResetInventory();

        foreach (var shack in Shack.All)
            shack.ResetShack();
        DroneCache.ResetRun(); // the drone: wherever they come across first again

        Glowstick.ClearAll();
        Polaroid.ClearAll();
        foreach (var car in BrokenCar.All)
            car.ResetCar();
        var door = FindAnyObjectByType<PortalDoor>();
        if (door != null) door.ResetDoor();
        foreach (var camp in Campsite.All)
            camp.ResetCampsite();

        foreach (var tower in Watchtower.All)
            tower.ResetTower();

        if (WatcherEvent.Instance != null) WatcherEvent.Instance.ResetEvent();
        if (ScreamEvent.Instance != null) ScreamEvent.Instance.ResetEvent();

        var player = FindAnyObjectByType<FirstPersonController>();
        if (player != null)
        {
            player.ResetToSpawn();
            player.enabled = true;
        }

        ShadowEntity.Spawn(ShadowEntity.FirstEntityDelay);
    }

    void OnGUI()
    {
        float alpha = !hasReset
            ? Mathf.Clamp01(timer / FadeOutTime)
            : 1f - Mathf.Clamp01((timer - FadeOutTime - HoldTime) / FadeInTime);

        GUI.depth = -1000;
        GUI.color = new Color(0f, 0f, 0f, alpha);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

        if (!hasReset && timer > FadeOutTime * 0.6f)
        {
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 42, fontStyle = FontStyle.Bold };
                style.normal.textColor = Color.white;
            }
            float textAlpha = Mathf.Clamp01((timer - FadeOutTime * 0.6f) / 1.5f);
            GUI.color = new Color(0.7f, 0.05f, 0.05f, textAlpha);
            GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), "YOU'VE BEEN FOUND", style);
        }

        GUI.color = Color.white;
    }
}
