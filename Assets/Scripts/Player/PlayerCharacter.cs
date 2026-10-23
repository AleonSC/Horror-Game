using UnityEngine;

/// <summary>
/// The player's own character model and its animations: an asset at Resources/PlayerCharacter (Horror >
/// Player Character > Open Settings makes it). With a model set, PlayerBody uses it instead of the simple
/// built figure: seen from the drone, through rifts and the dark room's door, never through your own eyes.
/// Any animation left empty falls back to a near one (crouch walk to walk, run to walk, and so on), so only
/// Idle and Walk are really needed. Looping clips loop whether or not Loop Time is ticked on import.
/// </summary>
[CreateAssetMenu(fileName = "PlayerCharacter", menuName = "Horror/Player Character")]
public class PlayerCharacter : ScriptableObject
{
    [Header("Model")]
    [Tooltip("The character prefab (or model). A humanoid rig gets the drone remote put in its right hand.")]
    public GameObject model;
    [Tooltip("Scale it so it stands this tall (metres). 0: leave it at its own size.")]
    public float height = 1.75f;
    [Tooltip("Turn it round if it faces the wrong way (degrees about up).")]
    public float yawOffset = 0f;
    [Tooltip("Shift it if it floats or sinks (metres; y is up).")]
    public Vector3 offset = Vector3.zero;
    [Tooltip("It throws shadows (seen from the drone; not in your own view).")]
    public bool castShadows = true;

    [Header("Animations")]
    public AnimationClip idle;
    public AnimationClip walk;
    public AnimationClip run;
    public AnimationClip crouchIdle;
    public AnimationClip crouchWalk;
    [Tooltip("In the air (jumping or falling).")]
    public AnimationClip jump;
    [Tooltip("Standing still out of breath (stamina gone).")]
    public AnimationClip winded;
    [Tooltip("Flying the drone: holding the remote, looking down at it.")]
    public AnimationClip droneControl;
    [Tooltip("Sitting (in the car).")]
    public AnimationClip seated;

    [Header("Playback")]
    [Tooltip("The speed the walk clip is made for (metres a second): it plays faster or slower to match how fast you go.")]
    public float walkClipSpeed = 1.6f;
    public float runClipSpeed = 4.5f;
    public float crouchWalkClipSpeed = 1f;
    [Tooltip("How quickly it blends from one animation to the next (seconds).")]
    public float blendTime = 0.2f;

    /// <summary>The character in Resources (null if there isn't one).</summary>
    public static PlayerCharacter Load() => Resources.Load<PlayerCharacter>("PlayerCharacter");
}
