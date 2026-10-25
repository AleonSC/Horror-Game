using UnityEngine;

namespace Game.Core
{
    /// <summary>Something the player can use with a left click: a lamp to crank, a pickup, a switch.</summary>
    public interface IInteractable
    {
        /// <summary>The word shown in the prompt, e.g. "Crank".</summary>
        string Prompt { get; }
        bool CanInteract { get; }
        /// <summary>Where the crosshair aims for it (aim assist uses this).</summary>
        Vector3 FocusPoint { get; }
        void Interact();
    }

    /// <summary>An interactable with a meter to show while it's used (a lamp's crank).</summary>
    public interface IProgressInteractable : IInteractable
    {
        float Progress { get; }
    }

    /// <summary>A door (shack, cabin, tent flap, the lone door): used with E, never with a click, so frantic clicking never swings one.</summary>
    public interface IDoor : IInteractable { }

    /// <summary>Somewhere to sit (the car's seat): Y gets in and out.</summary>
    public interface ISeat
    {
        /// <summary>Near enough to get in from here?</summary>
        bool CanSit(Vector3 from);
        Transform SeatPoint { get; }
        /// <summary>Where you stand when you get out.</summary>
        Vector3 ExitPoint { get; }
        void Sat(bool seated);
    }

    /// <summary>
    /// A seat you get into and out of by its own doorway (inside a tent): E on the doorway gets you in; inside,
    /// E on its flap opens or shuts it, and E anywhere else gets you out (if the way's open). Y does nothing.
    /// </summary>
    public interface IDoorwaySeat : ISeat
    {
        /// <summary>How high your eye sits above the seat point (sitting on a tent's floor: low).</summary>
        float EyeHeight { get; }
        /// <summary>Can you get out now (is the flap open)?</summary>
        bool CanLeave { get; }
        /// <summary>What to say when you can't.</summary>
        string CantLeave { get; }
    }

    /// <summary>A seat that holds your view on one point while you're in it (playing the game on a TV).</summary>
    public interface ILockedView
    {
        Vector3 LookAt { get; }
    }

    /// <summary>A seat with others beside it (the car): Y moves you along to the next.</summary>
    public interface ISeatSwitcher
    {
        /// <summary>Move to the next seat (its point and way out change).</summary>
        void NextSeat();
        /// <summary>Which seat you're in, for the hint ("the driver's seat").</summary>
        string SeatName { get; }
    }

    /// <summary>Every seat in the world (they add themselves).</summary>
    public static class Seats
    {
        static readonly System.Collections.Generic.List<ISeat> all = new System.Collections.Generic.List<ISeat>();
        public static void Add(ISeat seat) { if (!all.Contains(seat)) all.Add(seat); }
        public static void Remove(ISeat seat) => all.Remove(seat);

        public static ISeat Near(Vector3 from)
        {
            foreach (var s in all) if (s != null && s.CanSit(from)) return s;
            return null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => all.Clear();
    }

    /// <summary>Where the player is, for the hunters: through the lone door, nothing can follow.</summary>
    public static class PlayerPlace
    {
        public static bool InDarkRoom;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay() => InDarkRoom = false;
    }
}
