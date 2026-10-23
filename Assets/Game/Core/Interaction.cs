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
}
