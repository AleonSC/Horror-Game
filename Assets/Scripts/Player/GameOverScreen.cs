using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Shown when an entity catches the player: fades to black, shows a message, then resets
/// the run (all lamps off, fresh batteries, full flashlight, empty hotbar, restocked shacks, all entities removed, a single new
/// entity, player back at spawn) and fades back in.
/// </summary>
public class GameOverScreen : MonoBehaviour
{
    const float FadeOutTime = 1.2f;
    const float HoldTime = 3f;
    const float FadeInTime = 1.5f;

    static GameOverScreen instance;
    public static bool IsActive => instance != null;

    float timer;
    bool hasReset;
    GUIStyle style;

    public static void Show()
    {
        if (instance != null) return;
        instance = new GameObject("Game Over").AddComponent<GameOverScreen>();
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
            ResetRun();
        }

        if (timer >= FadeOutTime + HoldTime + FadeInTime) Destroy(gameObject);
    }

    /// <summary>Starts the run over straight away (the pause menu's Restart).</summary>
    public static void RestartRun()
    {
        if (instance != null) Destroy(instance.gameObject);
        ResetRun();
    }

    static void ResetRun()
    {
        foreach (var entity in new List<ShadowEntity>(ShadowEntity.All))
            Destroy(entity.gameObject);

        foreach (var lamp in Lamp.All)
            lamp.ResetLamp();
        Lamp.ResetTimesLit();

        var batteries = FindAnyObjectByType<BatterySpawner>();
        if (batteries != null) batteries.SpawnBatteries();

        var flashlight = FindAnyObjectByType<Flashlight>();
        if (flashlight != null) flashlight.ResetBattery();

        var hotbar = FindAnyObjectByType<Hotbar>();
        if (hotbar != null) hotbar.ResetInventory();

        foreach (var shack in Shack.All)
            shack.ResetShack();

        foreach (var camp in Campsite.All)
            camp.ResetCampsite();

        foreach (var tower in Watchtower.All)
            tower.ResetTower();

        if (WatcherEvent.Instance != null) WatcherEvent.Instance.ResetEvent();

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
            GUI.Label(new Rect(0f, 0f, Screen.width, Screen.height), "IT FOUND YOU", style);
        }

        GUI.color = Color.white;
    }
}
