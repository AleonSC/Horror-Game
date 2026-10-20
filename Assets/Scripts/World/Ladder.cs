using UnityEngine;

/// <summary>
/// A climbable ladder. Put this on a trigger collider covering the ladder.
/// While the player is inside it, FirstPersonController climbs instead of walking:
/// W climbs up (or down, if looking down), S climbs down. At the top the player can step off
/// onto whatever the ladder leads to; at the bottom, backing away or strafing leaves the ladder.
/// </summary>
public class Ladder : MonoBehaviour
{
    [Tooltip("Where the player should line up while climbing, local to this object (only x and z are used).")]
    public Vector3 localClimbPoint;
    [Tooltip("Height of the top of the ladder (the floor it leads to), local to this object.")]
    public float localTopY;

    // Worked out from the local values so they stay right when the looping map moves the ladder.
    public Vector3 ClimbPoint => transform.TransformPoint(localClimbPoint);
    public float TopY => transform.TransformPoint(new Vector3(0f, localTopY, 0f)).y;
    public float climbSpeed = 2.5f;
}
