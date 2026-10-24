using System;
using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// Your own body (section 9): seen only by the drone's camera and through the portals, never your own eyes.
    /// Empty model: a built-in hooded figure, moved by code. Your model: give it an Animator (humanoid for the
    /// remote to sit in its right hand) and as many of the clips as you have; missing ones fall back to idle.
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Config/Player Character", fileName = "PlayerCharacterConfig")]
    public sealed class PlayerCharacterConfig : GameConfig
    {
        [Header("Model")]
        [Tooltip("Your character prefab, feet at its pivot, facing +Z. Empty: the built-in hooded figure.")]
        public GameObject model;
        [Range(0.5f, 3f), Tooltip("How tall it's scaled to (metres).")]
        public float height = 1.75f;
        [Range(-180f, 180f), Tooltip("Turn it this many degrees if it doesn't face forward.")]
        public float yawOffset;
        [Tooltip("Shift it from your feet (metres).")]
        public Vector3 offset;
        [Tooltip("Does it cast shadows? Off by default: your flashlight and torch start at your eyes, inside the hood, and it would block them.")]
        public bool castShadows;

        [Header("Clips (your model only)")]
        public CharacterClips clips = new CharacterClips();
    }

    [Serializable]
    public sealed class CharacterClips
    {
        public AnimationClip idle, walk, run, crouchIdle, crouchWalk, jump, winded, droneControl, seated;
        [Range(0.5f, 10f), Tooltip("The speed (m/s) your walk clip moves at, so it plays at your pace.")]
        public float walkClipSpeed = 1.6f;
        [Range(1f, 15f), Tooltip("The speed (m/s) your run clip moves at.")]
        public float runClipSpeed = 4.5f;
    }
}
