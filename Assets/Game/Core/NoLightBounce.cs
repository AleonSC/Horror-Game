using UnityEngine;

namespace Game.Core
{
    /// <summary>
    /// On something the flashlight's bounce light shouldn't come back off (logs, stumps: wood, like the trees,
    /// which don't either). Its children count too.
    /// </summary>
    public sealed class NoLightBounce : MonoBehaviour { }
}
