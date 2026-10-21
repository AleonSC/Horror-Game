using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

/// <summary>
/// Thin input wrapper so the game works with either the new Input System package
/// or the legacy Input Manager (whichever is enabled in Player Settings).
///
/// Controls:
///   WASD / Arrows  - move
///   Mouse          - look
///   Left Shift     - run
///   Space          - jump
///   Left Ctrl / C  - crouch
///   F              - use the item in hand (flashlight on/off, light torch, flick lighter)
///   E              - open / close doors (shacks, the car's doors and glove box, the lone door)
///   Left click     - everything else: crank lamps, pick things up, switches
///   1 / 2 / 3 / 4  - hotbar: flashlight / torch / lighter / flare gun
///   Esc            - pause menu
/// </summary>
public static class HorrorInput
{
#if ENABLE_INPUT_SYSTEM
    // Scales raw pixel delta so sensitivity feels like the legacy "Mouse X/Y" axes.
    const float MouseDeltaScale = 0.1f;

    static Keyboard Kb => Keyboard.current;
    static Mouse Ms => Mouse.current;

    public static Vector2 Move
    {
        get
        {
            if (Kb == null) return Vector2.zero;
            float x = (Kb.dKey.isPressed || Kb.rightArrowKey.isPressed ? 1f : 0f)
                    - (Kb.aKey.isPressed || Kb.leftArrowKey.isPressed ? 1f : 0f);
            float y = (Kb.wKey.isPressed || Kb.upArrowKey.isPressed ? 1f : 0f)
                    - (Kb.sKey.isPressed || Kb.downArrowKey.isPressed ? 1f : 0f);
            return new Vector2(x, y);
        }
    }

    public static Vector2 Look => Ms != null ? Ms.delta.ReadValue() * MouseDeltaScale : Vector2.zero;

    public static bool JumpPressed => Kb != null && Kb.spaceKey.wasPressedThisFrame;
    public static bool RunHeld => Kb != null && Kb.leftShiftKey.isPressed;
    public static bool CrouchHeld => Kb != null && (Kb.leftCtrlKey.isPressed || Kb.cKey.isPressed);
    public static bool CrouchPressed => Kb != null && (Kb.leftCtrlKey.wasPressedThisFrame || Kb.cKey.wasPressedThisFrame);
    public static bool FlashlightPressed => Kb != null && Kb.fKey.wasPressedThisFrame;
    /// <summary>F1: the debug overlay of where everything spawned.</summary>
    public static bool DebugOverlayPressed => Kb != null && Kb.f1Key.wasPressedThisFrame;
    public static bool DevToolsPressed => Kb != null && Kb.f9Key.wasPressedThisFrame;
    /// <summary>Hotbar slot (1-5) pressed this frame, or 0.</summary>
    public static int SlotPressed =>
        Kb == null ? 0 :
        Kb.digit1Key.wasPressedThisFrame || Kb.numpad1Key.wasPressedThisFrame ? 1 :
        Kb.digit2Key.wasPressedThisFrame || Kb.numpad2Key.wasPressedThisFrame ? 2 :
        Kb.digit3Key.wasPressedThisFrame || Kb.numpad3Key.wasPressedThisFrame ? 3 :
        Kb.digit4Key.wasPressedThisFrame || Kb.numpad4Key.wasPressedThisFrame ? 4 :
        Kb.digit5Key.wasPressedThisFrame || Kb.numpad5Key.wasPressedThisFrame ? 5 :
        Kb.digit6Key.wasPressedThisFrame || Kb.numpad6Key.wasPressedThisFrame ? 6 :
        Kb.digit7Key.wasPressedThisFrame || Kb.numpad7Key.wasPressedThisFrame ? 7 :
        Kb.digit8Key.wasPressedThisFrame || Kb.numpad8Key.wasPressedThisFrame ? 8 : 0;
    public static bool ReleaseCursorPressed => Kb != null && Kb.escapeKey.wasPressedThisFrame;
    public static bool InventoryPressed => Kb != null && Kb.tabKey.wasPressedThisFrame;
    public static bool ClickPressed => Ms != null && Ms.leftButton.wasPressedThisFrame;
    /// <summary>E: open and close doors (and the car's doors and glove box).</summary>
    public static bool InteractPressed => Kb != null && Kb.eKey.wasPressedThisFrame;
    /// <summary>Mouse wheel this frame: positive scrolled up, negative down, 0 none.</summary>
    public static float Scroll => Ms != null ? Ms.scroll.ReadValue().y : 0f;
#else
    public static Vector2 Move => new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
    public static Vector2 Look => new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));

    public static bool JumpPressed => Input.GetKeyDown(KeyCode.Space);
    public static bool RunHeld => Input.GetKey(KeyCode.LeftShift);
    public static bool CrouchHeld => Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C);
    public static bool CrouchPressed => Input.GetKeyDown(KeyCode.LeftControl) || Input.GetKeyDown(KeyCode.C);
    public static bool FlashlightPressed => Input.GetKeyDown(KeyCode.F);
    /// <summary>F1: the debug overlay of where everything spawned.</summary>
    public static bool DebugOverlayPressed => Input.GetKeyDown(KeyCode.F1);
    public static bool DevToolsPressed => Input.GetKeyDown(KeyCode.F9);
    /// <summary>Hotbar slot (1-5) pressed this frame, or 0.</summary>
    public static int SlotPressed =>
        Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1) ? 1 :
        Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2) ? 2 :
        Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3) ? 3 :
        Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4) ? 4 :
        Input.GetKeyDown(KeyCode.Alpha5) || Input.GetKeyDown(KeyCode.Keypad5) ? 5 :
        Input.GetKeyDown(KeyCode.Alpha6) || Input.GetKeyDown(KeyCode.Keypad6) ? 6 :
        Input.GetKeyDown(KeyCode.Alpha7) || Input.GetKeyDown(KeyCode.Keypad7) ? 7 :
        Input.GetKeyDown(KeyCode.Alpha8) || Input.GetKeyDown(KeyCode.Keypad8) ? 8 : 0;
    public static bool ReleaseCursorPressed => Input.GetKeyDown(KeyCode.Escape);
    public static bool InventoryPressed => Input.GetKeyDown(KeyCode.Tab);
    public static bool ClickPressed => Input.GetMouseButtonDown(0);
    /// <summary>E: open and close doors (and the car's doors and glove box).</summary>
    public static bool InteractPressed => Input.GetKeyDown(KeyCode.E);
    /// <summary>Mouse wheel this frame: positive scrolled up, negative down, 0 none.</summary>
    public static float Scroll => Input.mouseScrollDelta.y;
#endif
}
