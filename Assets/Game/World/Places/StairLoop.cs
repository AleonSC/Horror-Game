using Game.Core;
using Game.Player;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// The bunker's stairs that go down for ever. Walk down them and every few steps you're quietly put back up by
    /// exactly that many (the steps ahead the same, the dark ahead the same: nothing to see it by); so you feel
    /// yourself going down and down, but turn round and the top's right there behind you, as near as it ever was.
    /// Walk back up and you're out. (In the bunker's own space: the stairs go down along -Z from the top step.)
    /// </summary>
    public sealed class StairLoop : MonoBehaviour
    {
        [Tooltip("Steps taken back up each time.")]
        public int loopSteps = 4;
        [Tooltip("How many steps down you get before you're taken back.")]
        public int stepsDown = 6;

        Vector3 top;        // the top of the stairs (the edge of the first step), in the bunker's space
        float half, rise, run;
        PlayerController player;

        /// <summary>Where the stairs start (local), half their width, each step's rise and run.</summary>
        public void Set(Vector3 topEdge, float halfWidth, float stepRise, float stepRun)
        {
            top = topEdge;
            half = halfWidth;
            rise = stepRise;
            run = stepRun;
        }

        void Update()
        {
            if (run <= 0f || (player == null && !Services.TryGet(out player))) return;
            Vector3 local = transform.InverseTransformPoint(player.transform.position);
            if (Mathf.Abs(local.x - top.x) > half + 0.3f) return;
            float down = top.z - local.z; // (metres down the stairs from the top step's edge)
            if (down < stepsDown * run || local.y > top.y + 0.5f) return;
            int back = Mathf.Max(1, Mathf.Min(loopSteps, stepsDown - 1));
            player.Shift(transform.rotation * new Vector3(0f, back * rise, back * run));
        }
    }
}
