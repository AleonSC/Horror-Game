using UnityEngine;

/// <summary>
/// Sounds the player makes that something listening could hear: footsteps, rustling through bushes, doors,
/// lamp clicks, the flare gun... Each is a point and a radius it carries to. The blind entity hunts by these.
/// Nothing carries out of (or into) the dark room behind the lone door.
/// </summary>
public static class Noise
{
    /// <summary>Raised for every noise: where it was, and how far it carries, in metres.</summary>
    public static event System.Action<Vector3, float> Heard;

    public static void Emit(Vector3 position, float radius)
    {
        if (radius <= 0f || PortalDoor.PlayerInRoom) return;
        Heard?.Invoke(position, radius);
    }
}
