using Game.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Player
{
    /// <summary>
    /// A simple way to move round test scenes until the real player arrives (milestone 2): WASD to walk on the
    /// ground (Shift faster), mouse to look, G to fly (Space up, Ctrl down). Click to capture the mouse, Esc
    /// to free it. No collisions.
    /// </summary>
    public sealed class LookWalker : MonoBehaviour
    {
        public float walkSpeed = 3f;
        public float runSpeed = 6f;
        public float eyeHeight = 1.65f;
        public float mouseSpeed = 0.1f;

        float yaw, pitch;
        bool flying;

        void Start()
        {
            var e = transform.eulerAngles;
            yaw = e.y;
            pitch = e.x > 180f ? e.x - 360f : e.x;
        }

        void Update()
        {
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;

            bool uiOpen = Services.TryGet<UiState>(out var ui) && ui.AnyOpen;
            if (keyboard.escapeKey.wasPressedThisFrame) SetCaptured(false);
            else if (mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked && !uiOpen)
                SetCaptured(true);
            bool keysTaken = ui != null && ui.KeysTaken;
            if (keysTaken) return;
            if (keyboard.gKey.wasPressedThisFrame) flying = !flying;

            if (Cursor.lockState == CursorLockMode.Locked && !uiOpen)
            {
                float sensitivity = Services.TryGet<PlayerPreferences>(out var prefs) ? prefs.mouseSensitivity : 1f;
                Vector2 d = mouse.delta.ReadValue() * mouseSpeed * sensitivity;
                yaw += d.x;
                pitch = Mathf.Clamp(pitch - d.y, -85f, 85f);
                transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }

            Vector3 input = Vector3.zero;
            if (keyboard.wKey.isPressed) input.z += 1f;
            if (keyboard.sKey.isPressed) input.z -= 1f;
            if (keyboard.dKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed) input.x -= 1f;
            if (flying && keyboard.spaceKey.isPressed) input.y += 1f;
            if (flying && keyboard.leftCtrlKey.isPressed) input.y -= 1f;

            float speed = keyboard.leftShiftKey.isPressed ? runSpeed : walkSpeed;
            Vector3 forward = flying ? transform.forward : Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            Vector3 move = forward * input.z + right * input.x + Vector3.up * input.y;
            transform.position += Vector3.ClampMagnitude(move, 1f) * speed * Time.deltaTime;

            if (!flying && Physics.Raycast(transform.position + Vector3.up * 2f, Vector3.down, out var hit, 50f, Layers.Mask(Layers.Ground), QueryTriggerInteraction.Ignore))
            {
                var p = transform.position;
                p.y = Mathf.Lerp(p.y, hit.point.y + eyeHeight, 1f - Mathf.Exp(-15f * Time.deltaTime));
                transform.position = p;
            }
        }

        static void SetCaptured(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }
    }
}
