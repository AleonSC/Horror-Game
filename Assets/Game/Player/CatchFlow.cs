using System.Collections;
using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Being taken, and waking up again (section 6.12). Whatever took the player calls <see cref="Begin"/> as it
    /// drags them away: the screen fades to black, a message, then the player wakes (groggy) by the last lamp
    /// they lit, or where they started. Progress is kept. Lives on the player.
    /// </summary>
    public sealed class CatchFlow : MonoBehaviour
    {
        const float FadeOutTime = 4f;
        const float MessageTime = 3f;
        const float FadeInTime = 2.5f;
        const float BatteryOnWaking = 0.3f;

        PlayerController player;
        System.IDisposable lampSub, resetSub;
        Vector3? lastLamp;

        /// <summary>True from being taken until the player is awake again (nothing else may take them).</summary>
        public bool Active { get; private set; }

        /// <summary>Dev: nothing can take you.</summary>
        public static bool Invincible;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => Invincible = false;

        /// <summary>Can something start taking you now? (Not while already being taken, nor while invincible.)</summary>
        public bool CanTake => !Active && !Invincible;

        public static readonly string[] Messages = { "IT TOOK YOU.", "YOU WERE SEEN.", "THEY DON'T LET GO.", "WAKE UP." };

        void Awake()
        {
            player = GetComponent<PlayerController>();
            Services.Register(this);
        }

        void Start()
        {
            if (!Services.TryGet<EventBus>(out var events)) return;
            lampSub = events.Subscribe<LampLit>(e => lastLamp = e.Position);
            resetSub = events.Subscribe<RunReset>(_ => OnRestart());
        }

        void OnDestroy()
        {
            Services.Unregister(this);
            lampSub?.Dispose();
            resetSub?.Dispose();
        }

        /// <summary>The player has been taken: fade out, then wake them again.</summary>
        /// <param name="hardCut">Straight to black, no fade (the blind one).</param>
        public void Begin(EntityKind by, int entityId, bool hardCut = false)
        {
            if (Active) return;
            Active = true;
            if (Services.TryGet<EventBus>(out var events)) events.Publish(new PlayerCaught(by, entityId, transform.position));
            StartCoroutine(Run(hardCut));
        }

        IEnumerator Run(bool hardCut)
        {
            var fade = Services.Get<ScreenFade>();
            fade.Color = Color.black;
            fade.Message = Messages[Random.Range(0, Messages.Length)];
            if (hardCut) fade.Alpha = 1f;

            for (float t = 0f; t < FadeOutTime && !hardCut; t += Time.deltaTime)
            {
                fade.Alpha = Mathf.Max(fade.Alpha, Mathf.SmoothStep(0f, 1f, t / FadeOutTime));
                yield return null;
            }
            fade.Alpha = 1f;
            for (float t = 0f; t < MessageTime; t += Time.deltaTime)
            {
                fade.MessageAlpha = Mathf.Clamp01(Mathf.Min(t / 0.8f, (MessageTime - t) / 0.8f));
                yield return null;
            }
            fade.MessageAlpha = 0f;

            WakeUp();

            for (float t = 0f; t < FadeInTime; t += Time.deltaTime)
            {
                fade.Alpha = 1f - Mathf.SmoothStep(0f, 1f, t / FadeInTime);
                yield return null;
            }
            fade.Clear();
            Active = false;
        }

        void WakeUp()
        {
            Vector3 at = player.SpawnPoint;
            Quaternion facing = player.SpawnFacing;
            if (lastLamp.HasValue)
            {
                // Beside the lamp, facing it.
                Vector3 lamp = lastLamp.Value;
                Vector3 away = player.SpawnPoint - lamp;
                away.y = 0f;
                away = away.sqrMagnitude > 0.01f ? away.normalized : Vector3.back;
                at = Ground(lamp + away * 3f);
                facing = Quaternion.LookRotation(-away);
            }

            player.SetHeld(false);
            player.Teleport(at, facing);
            player.Stamina.Refill();
            if (player.Flashlight != null) player.Flashlight.Battery.AtLeast(BatteryOnWaking);
            player.StartGroggy();
            if (Services.TryGet<EventBus>(out var events)) events.Publish(new PlayerRespawned(at));
        }

        // Restart: back to where the run began.
        void OnRestart()
        {
            StopAllCoroutines();
            lastLamp = null;
            Active = false;
            if (Services.TryGet<ScreenFade>(out var fade)) fade.Clear();
            player.SetHeld(false);
            player.Teleport(player.SpawnPoint, player.SpawnFacing);
            player.Stamina.Refill();
        }

        static Vector3 Ground(Vector3 p)
        {
            if (Physics.Raycast(p + Vector3.up * 20f, Vector3.down, out var hit, 60f, Layers.Mask(Layers.Ground), QueryTriggerInteraction.Ignore))
                return hit.point + Vector3.up * 0.05f;
            return p;
        }
    }
}
