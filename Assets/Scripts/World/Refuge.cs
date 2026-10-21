using UnityEngine;

/// <summary>
/// Shelter: a shack, the cabin or a tent. The entities won't come within radius of it (a safe zone, like a
/// lamp's light, but always there): you can hide in it, but they keep to their routines outside, and the
/// longer you stay the more of them gather round its edge. Shack and Campsite add these.
/// </summary>
public class Refuge : MonoBehaviour, SafeZones.ISource
{
    [Tooltip("How close the entities can come to it, in metres from its middle.")]
    public float radius = 5f;

    bool SafeZones.ISource.IsActive => isActiveAndEnabled;
    Vector3 SafeZones.ISource.Center => transform.position;
    float SafeZones.ISource.Radius => radius;

    void OnEnable() => SafeZones.Register(this);
    void OnDisable() => SafeZones.Unregister(this);
}
