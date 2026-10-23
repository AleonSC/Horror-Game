using Game.Core;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// The lone door standing in the forest (section 6.5): locked until all ten lamps are lit. What's through it
    /// (the dark room) comes with milestone 5.
    /// </summary>
    [RequireComponent(typeof(Door))]
    public sealed class LoneDoor : MonoBehaviour
    {
        Door door;
        System.IDisposable sub;

        void Start()
        {
            door = GetComponent<Door>();
            door.locked = !(Services.TryGet<GameRun>(out var run) && run.LampsLit >= GameRun.LampCount);
            if (Services.TryGet<EventBus>(out var events)) sub = events.Subscribe<DoorUnlocked>(_ => door.locked = false);
        }

        void OnDestroy() => sub?.Dispose();
    }
}
