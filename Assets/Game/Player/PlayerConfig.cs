using System;
using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>The player's movement and body (section 6.1 of the plan), in Assets/Config/Player/PlayerConfig.asset.</summary>
    [CreateAssetMenu(menuName = "Game/Config/Player", fileName = "PlayerConfig")]
    public sealed class PlayerConfig : GameConfig
    {
        public PlayerSettings settings = new PlayerSettings();
    }

    [Serializable]
    public sealed class PlayerSettings
    {
        [Header("Moving")]
        [Range(0.5f, 10f)] public float walkSpeed = 3f;
        [Range(1f, 15f)] public float runSpeed = 6f;
        [Range(0.3f, 5f)] public float crouchSpeed = 1.5f;
        [Range(1f, 40f), Tooltip("How quickly you reach full speed.")]
        public float acceleration = 12f;
        [Range(0f, 1f), Tooltip("How much you can steer in the air.")]
        public float airControl = 0.3f;
        [Range(0f, 3f), Tooltip("Jump height (metres).")]
        public float jumpHeight = 1.1f;
        [Range(-50f, -5f)] public float gravity = -20f;

        [Header("Body")]
        [Range(1f, 2.5f)] public float standingHeight = 1.8f;
        [Range(0.5f, 1.5f)] public float crouchingHeight = 1f;
        [Range(0f, 0.4f), Tooltip("How far below the top of your head your eyes are.")]
        public float eyeBelowTop = 0.15f;

        [Header("Stamina")]
        [Range(1f, 30f), Tooltip("Seconds of running on full stamina.")]
        public float sprintTime = 20f;
        [Range(1f, 30f), Tooltip("Seconds to recover fully, standing still.")]
        public float recoverTime = 7f;
        [Range(1f, 4f), Tooltip("Recovering while walking takes this many times longer.")]
        public float walkingRecoverSlowdown = 1.67f;
        [Range(0f, 0.3f), Tooltip("Each lamp lit makes recovering this much slower (0.05 = 5%).")]
        public float recoverSlowerPerLamp = 0.05f;
        [Range(0f, 1f), Tooltip("Run out and you can't run again until stamina is back to this.")]
        public float windedUntil = 0.35f;

        [Header("View")]
        [Range(40f, 100f)] public float fieldOfView = 60f;
        [Range(0f, 4f), Tooltip("Head bob steps per second (walking).")]
        public float bobFrequency = 1.8f;
        [Range(0f, 0.2f), Tooltip("Head bob height (metres).")]
        public float bobAmount = 0.05f;
        [Range(0.01f, 0.5f), Tooltip("Mouse speed (times the player's sensitivity setting).")]
        public float mouseSpeed = 0.1f;

        [Header("Footsteps")]
        [Range(0f, 1f), Tooltip("How loud your footsteps are to you.")]
        public float stepVolume = 0.35f;
        [Range(0f, 60f), Tooltip("How far a running step is heard by things that hunt by sound (metres, x difficulty).")]
        public float runNoise = 22f;
        [Range(0f, 30f), Tooltip("How far a walking step is heard.")]
        public float walkNoise = 5f;
        [Range(0f, 10f), Tooltip("How far a crouching step is heard (barely).")]
        public float crouchNoise = 1f;
        [Range(0f, 30f), Tooltip("How far your panting is heard while you're winded.")]
        public float pantNoise = 6f;

        [Header("Dream hallway (the giant's eye)")]
        [Range(40f, 140f)] public float dreamFieldOfView = 100f;
        [Range(0f, 1f), Tooltip("How fast you move in the dream: running gets you nowhere.")]
        public float dreamSpeed = 0.08f;

        [Header("Waking up")]
        [Range(0f, 15f), Tooltip("Seconds of grogginess after being taken.")]
        public float groggyTime = 5f;
        [Range(0.1f, 1f), Tooltip("Speed while groggy.")]
        public float groggySpeed = 0.5f;

        [Header("Interaction")]
        [Range(1f, 5f), Tooltip("How far you can reach (metres).")]
        public float reach = 2.5f;
        [Range(0f, 20f), Tooltip("Aim assist: degrees off the crosshair that still pick something up.")]
        public float aimAssist = 8f;

        public PlayerSettings Clone() => (PlayerSettings)MemberwiseClone();
    }
}
