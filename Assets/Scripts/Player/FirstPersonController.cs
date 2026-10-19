using UnityEngine;

/// <summary>
/// First person controller with walk, run, jump and crouch, built on CharacterController.
///
/// Setup (or let DarkForestGenerator spawn a player for you):
///   1. Create an empty GameObject "Player" (its pivot is at the feet).
///   2. Add this component (a CharacterController is added automatically).
///   3. Make a Camera a child of Player and assign it to Camera Root.
///   4. Optionally add a Flashlight component to the camera.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Camera (or camera pivot) that rotates up/down. Should be a child of this object.")]
    public Transform cameraRoot;

    [Header("Movement")]
    public float walkSpeed = 3f;
    public float runSpeed = 6f;
    public float crouchSpeed = 1.5f;
    [Tooltip("How quickly the player reaches target speed on the ground.")]
    public float acceleration = 12f;
    [Range(0f, 1f), Tooltip("Fraction of ground acceleration available while airborne.")]
    public float airControl = 0.3f;

    [Header("Jump & Gravity")]
    public float jumpHeight = 1.1f;
    public float gravity = -20f;

    [Header("Crouch")]
    public float standingHeight = 1.8f;
    public float crouchHeight = 1.0f;
    [Tooltip("Distance from the top of the capsule down to the eyes.")]
    public float eyeOffsetFromTop = 0.15f;
    public float crouchTransitionSpeed = 6f;
    [Tooltip("Off = hold to crouch, On = press to toggle crouch.")]
    public bool toggleCrouch = false;
    [Tooltip("Layers that block the player from standing up.")]
    public LayerMask ceilingMask = ~0;

    [Header("Look")]
    public float mouseSensitivity = 2f;
    public float maxLookAngle = 85f;
    public bool invertY = false;
    public bool lockCursor = true;

    [Header("Head Bob")]
    public bool enableHeadBob = true;
    public float bobFrequency = 1.8f;
    public float bobAmplitude = 0.05f;

    public bool IsGrounded => controller.isGrounded;
    public bool IsRunning { get; private set; }
    public bool IsCrouching { get; private set; }
    public float CurrentSpeed => horizontalVelocity.magnitude;

    CharacterController controller;
    Vector3 horizontalVelocity;
    float verticalVelocity;
    float pitch;
    float currentHeight;
    bool crouchToggled;
    float bobTimer;

    void Awake()
    {
        controller = GetComponent<CharacterController>();

        if (cameraRoot == null)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam != null) cameraRoot = cam.transform;
        }

        currentHeight = standingHeight;
        ApplyHeight(currentHeight);
    }

    void Start()
    {
        if (lockCursor) SetCursorLocked(true);
    }

    void Update()
    {
        HandleCursor();
        HandleLook();
        HandleCrouch();
        HandleMovement();
        UpdateCamera();
    }

    void HandleCursor()
    {
        if (!lockCursor) return;
        if (HorrorInput.ReleaseCursorPressed) SetCursorLocked(false);
        else if (HorrorInput.ClickPressed && Cursor.lockState != CursorLockMode.Locked) SetCursorLocked(true);
    }

    static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void HandleLook()
    {
        if (lockCursor && Cursor.lockState != CursorLockMode.Locked) return;

        Vector2 look = HorrorInput.Look * mouseSensitivity;
        transform.Rotate(Vector3.up, look.x, Space.Self);

        pitch += invertY ? look.y : -look.y;
        pitch = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);
    }

    void HandleCrouch()
    {
        bool wantsCrouch;
        if (toggleCrouch)
        {
            if (HorrorInput.CrouchPressed) crouchToggled = !crouchToggled;
            wantsCrouch = crouchToggled;
        }
        else
        {
            wantsCrouch = HorrorInput.CrouchHeld;
        }

        // Stay crouched if something is overhead.
        if (!wantsCrouch && IsCrouching && !CanStandUp())
            wantsCrouch = true;

        IsCrouching = wantsCrouch;

        float targetHeight = IsCrouching ? crouchHeight : standingHeight;
        if (!Mathf.Approximately(currentHeight, targetHeight))
        {
            currentHeight = Mathf.MoveTowards(currentHeight, targetHeight,
                crouchTransitionSpeed * (standingHeight - crouchHeight) * Time.deltaTime);
            ApplyHeight(currentHeight);
        }
    }

    bool CanStandUp()
    {
        float radius = controller.radius * 0.95f;
        Vector3 origin = transform.position + Vector3.up * (currentHeight - controller.radius);
        float distance = standingHeight - currentHeight + controller.skinWidth;
        return !Physics.SphereCast(origin, radius, Vector3.up, out _, distance, ceilingMask, QueryTriggerInteraction.Ignore);
    }

    void ApplyHeight(float height)
    {
        controller.height = height;
        controller.center = Vector3.up * (height * 0.5f);
    }

    void HandleMovement()
    {
        bool grounded = controller.isGrounded;

        Vector2 input = Vector2.ClampMagnitude(HorrorInput.Move, 1f);
        bool hasInput = input.sqrMagnitude > 0.01f;

        IsRunning = HorrorInput.RunHeld && !IsCrouching && hasInput && input.y > 0f;

        float targetSpeed = IsCrouching ? crouchSpeed : IsRunning ? runSpeed : walkSpeed;
        Vector3 desired = (transform.right * input.x + transform.forward * input.y) * targetSpeed;

        float accel = grounded ? acceleration : acceleration * airControl;
        horizontalVelocity = Vector3.Lerp(horizontalVelocity, desired, 1f - Mathf.Exp(-accel * Time.deltaTime));

        if (grounded && verticalVelocity < 0f)
            verticalVelocity = -2f; // keep the controller snapped to the ground

        if (grounded && !IsCrouching && HorrorInput.JumpPressed)
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

        verticalVelocity += gravity * Time.deltaTime;

        Vector3 motion = (horizontalVelocity + Vector3.up * verticalVelocity) * Time.deltaTime;
        CollisionFlags flags = controller.Move(motion);

        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f; // bonked head
    }

    void UpdateCamera()
    {
        if (cameraRoot == null) return;

        float eyeHeight = currentHeight - eyeOffsetFromTop;
        Vector3 bobOffset = Vector3.zero;

        if (enableHeadBob && controller.isGrounded && CurrentSpeed > 0.1f)
        {
            bobTimer += Time.deltaTime * bobFrequency * (CurrentSpeed / walkSpeed);
            bobOffset.y = Mathf.Sin(bobTimer * Mathf.PI * 2f) * bobAmplitude;
            bobOffset.x = Mathf.Cos(bobTimer * Mathf.PI) * bobAmplitude * 0.5f;
        }
        else
        {
            bobTimer = 0f;
        }

        Vector3 target = new Vector3(0f, eyeHeight, 0f) + bobOffset;
        cameraRoot.localPosition = Vector3.Lerp(cameraRoot.localPosition, target, 1f - Mathf.Exp(-15f * Time.deltaTime));
        cameraRoot.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }
}
