using System;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>The player's controls (section 14 of the plan), made in code with the Input System.</summary>
    public sealed class PlayerInputActions : IDisposable
    {
        public readonly InputAction Move = new InputAction("Move", InputActionType.Value);
        public readonly InputAction Look = new InputAction("Look", InputActionType.Value, "<Mouse>/delta");
        public readonly InputAction Sprint = new InputAction("Sprint", InputActionType.Button, "<Keyboard>/leftShift");
        public readonly InputAction Jump = new InputAction("Jump", InputActionType.Button, "<Keyboard>/space");
        public readonly InputAction Crouch = new InputAction("Crouch", InputActionType.Button, "<Keyboard>/leftCtrl");
        public readonly InputAction Interact = new InputAction("Interact", InputActionType.Button, "<Mouse>/leftButton");
        public readonly InputAction Door = new InputAction("Door", InputActionType.Button, "<Keyboard>/e");
        public readonly InputAction Use = new InputAction("Use", InputActionType.Button, "<Keyboard>/f");
        public readonly InputAction Sit = new InputAction("Sit", InputActionType.Button, "<Keyboard>/y");

        InputAction[] All => new[] { Move, Look, Sprint, Jump, Crouch, Interact, Door, Use, Sit };

        public PlayerInputActions()
        {
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            Crouch.AddBinding("<Keyboard>/c");
        }

        public void Enable()
        {
            foreach (var a in All) a.Enable();
        }

        public void Disable()
        {
            foreach (var a in All) a.Disable();
        }

        public void Dispose()
        {
            foreach (var a in All) a.Dispose();
        }
    }
}
