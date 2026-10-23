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
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() { Heard = null; LastSource = ""; }

    /// <summary>What made the noise being heard right now (the script and method that made it), for the session log.</summary>
    public static string LastSource { get; private set; } = "";

    public static void Emit(Vector3 position, float radius,
        [System.Runtime.CompilerServices.CallerFilePath] string file = "", [System.Runtime.CompilerServices.CallerMemberName] string member = "")
    {
        if (radius <= 0f || PortalDoor.PlayerInRoom) return;
        LastSource = System.IO.Path.GetFileNameWithoutExtension(file) + "." + member;
        SessionRecorder.Count("noise: " + LastSource);
        Heard?.Invoke(position, radius);
    }
}
