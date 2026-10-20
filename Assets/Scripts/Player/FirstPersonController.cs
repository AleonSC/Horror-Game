using System.Collections.Generic;
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
    public float CurrentSpeed => moveVelocity.magnitude;

    [Header("Dream Hallway")]
    [Tooltip("Field of view at the height of the dream effect (WatcherEvent drives it while the sky eye is open).")]
    public float dreamWideFov = 100f;
    [Tooltip("Extra field of view from trying to run while it holds you: the harder you go, the further everything stretches away.")]
    public float dreamEffortFov = 15f;
    [Tooltip("How much the view squeezes in from the sides at full strength, so it reads as a long corridor.")]
    [Range(0f, 0.6f)] public float dreamSqueeze = 0.12f;

    /// <summary>
    /// 0 normally, 1 at full strength: like a nightmare, the view stretches out into a long hallway and
    /// walking or running gets you nowhere. Set by WatcherEvent while the eye in the sky is open.
    /// </summary>
    public float DreamStretch { get; set; }
    float dreamEffort, baseFov = -1f;
    Camera viewCamera;

    struct ApproachLimit
    {
        public Vector3 point;
        public float slowStart;
        public float stop;
    }

    // One slow-down zone per source (e.g. each ShadowEntity), so several can apply at once.
    readonly Dictionary<Object, ApproachLimit> approachLimits = new Dictionary<Object, ApproachLimit>();
    float pendingShake;
    Ladder ladder;
    int ladderContacts;
    Vector3 spawnPosition;
    Quaternion spawnRotation;

    CharacterController controller;
    Vector3 horizontalVelocity;
    Vector3 moveVelocity; // horizontalVelocity after the approach limit
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
        spawnPosition = transform.position;
        spawnRotation = transform.rotation;
        if (lockCursor) SetCursorLocked(true);
    }

    /// <summary>Moves the player back to where they started and clears any movement effects.</summary>
    /// <summary>Moves the player by an offset, keeping their momentum and view (used by the door to the dark room).</summary>
    public void TeleportBy(Vector3 offset)
    {
        controller.enabled = false; // CharacterController overrides teleports while enabled
        transform.position += offset;
        controller.enabled = true;
    }

    public void ResetToSpawn()
    {
        controller.enabled = false; // CharacterController overrides teleports while enabled
        transform.SetPositionAndRotation(spawnPosition, spawnRotation);
        controller.enabled = true;

        horizontalVelocity = moveVelocity = Vector3.zero;
        verticalVelocity = 0f;
        pitch = 0f;
        approachLimits.Clear();
        pendingShake = 0f;
        ladder = null;
        ladderContacts = 0;
    }

    void Update()
    {
        if (PauseMenu.IsPaused) return;
        HandleCursor();
        HandleLook();
        HandleCrouch();
        HandleMovement();
        UpdateCamera();
    }

    void HandleCursor()
    {
        // Esc opens the pause menu (which frees the mouse); clicking back into the game recaptures it.
        if (!lockCursor) return;
        if (HorrorInput.ClickPressed && Cursor.lockState != CursorLockMode.Locked) SetCursorLocked(true);
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

        float targetSpeed = (IsCrouching ? crouchSpeed : IsRunning ? runSpeed : walkSpeed) * Water.SpeedFactor(transform.position); // wading is slow
        Vector3 desired = (transform.right * input.x + transform.forward * input.y) * targetSpeed;

        float accel = grounded ? acceleration : acceleration * airControl;
        horizontalVelocity = Vector3.Lerp(horizontalVelocity, desired, 1f - Mathf.Exp(-accel * Time.deltaTime));
        // Applied to this frame's movement only, so the slowdown doesn't compound frame to frame.
        moveVelocity = horizontalVelocity;
        foreach (var limit in approachLimits.Values)
            moveVelocity = ApplyApproachLimit(moveVelocity, limit);

        if (ladder != null)
        {
            Climb(input, grounded);
        }
        else
        {
            if (grounded && verticalVelocity < 0f)
                verticalVelocity = -2f; // keep the controller snapped to the ground

            if (grounded && !IsCrouching && HorrorInput.JumpPressed)
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);

            verticalVelocity += gravity * Time.deltaTime;
        }

        // In the dream you run on the spot: legs moving, footsteps falling, never any closer.
        float progress = ladder != null ? 1f : 1f - DreamStretch;
        Vector3 motion = (moveVelocity * progress + Vector3.up * verticalVelocity) * Time.deltaTime;
        CollisionFlags flags = controller.Move(motion);

        if ((flags & CollisionFlags.Above) != 0 && verticalVelocity > 0f)
            verticalVelocity = 0f; // bonked head
    }

    /// <summary>
    /// Ladder movement: no gravity; W climbs (down instead if looking down), S climbs down.
    /// Below the top the player is held against the ladder; at the top they can step off.
    /// </summary>
    void Climb(Vector2 input, bool grounded)
    {
        bool atTop = transform.position.y > ladder.TopY - 0.3f;
        bool leavingBottom = grounded && input.y <= 0f;

        float climb = input.y * ladder.climbSpeed * (pitch > 40f ? -1f : 1f);
        if (input.y < 0f) climb = -ladder.climbSpeed;
        verticalVelocity = leavingBottom ? -2f : climb;

        if (!atTop && !leavingBottom)
        {
            // Hold the player against the ladder instead of walking.
            Vector3 toLadder = ladder.ClimbPoint - transform.position;
            toLadder.y = 0f;
            moveVelocity = Vector3.ClampMagnitude(toLadder * 6f, 3f);
        }
    }

    /// <summary>The ladder the player is on, if any.</summary>
    public Ladder CurrentLadder => ladder;

    void OnTriggerEnter(Collider other)
    {
        var l = other.GetComponent<Ladder>();
        if (l == null) return;
        if (ladderContacts == 0)
        {
            var tower = l.GetComponentInParent<Watchtower>();
            if (tower != null) tower.OnLadderTouched();
        }
        ladder = l;
        ladderContacts++;
    }

    void OnTriggerExit(Collider other)
    {
        if (other.GetComponent<Ladder>() == null) return;
        ladderContacts = Mathf.Max(0, ladderContacts - 1);
        if (ladderContacts == 0)
        {
            ladder = null;
            if (verticalVelocity > 0f) verticalVelocity = 0f; // don't launch off the top
        }
    }

    /// <summary>
    /// Makes the player slow down as they near a point, starting at slowStartDistance.
    /// At stopDistance they can't move any closer (sideways and backwards still work).
    /// </summary>
    public void SetApproachLimit(Object source, Vector3 point, float slowStartDistance, float stopDistance)
    {
        approachLimits[source] = new ApproachLimit { point = point, slowStart = slowStartDistance, stop = stopDistance };
    }

    public void ClearApproachLimit(Object source) => approachLimits.Remove(source);

    /// <summary>Shakes the camera this frame by up to this many degrees. The strongest request wins.</summary>
    public void AddCameraShake(float degrees) => pendingShake = Mathf.Max(pendingShake, degrees);

    Vector3 ApplyApproachLimit(Vector3 velocity, ApproachLimit limit)
    {
        Vector3 toPoint = limit.point - transform.position;
        toPoint.y = 0f;
        float dist = toPoint.magnitude;
        if (dist < 0.001f) return velocity;

        Vector3 dir = toPoint / dist;
        float toward = Vector3.Dot(velocity, dir);
        Vector3 other = velocity - dir * toward;

        // 0 when far away, 1 at the stop distance.
        float t = Mathf.Clamp01(Mathf.InverseLerp(limit.slowStart, limit.stop, dist));

        if (toward > 0f)
        {
            // Square root so the player still reaches the stop distance instead of crawling forever.
            toward *= Mathf.Sqrt(1f - t);
            toward = Mathf.Min(toward, Mathf.Max(0f, dist - limit.stop) / Mathf.Max(Time.deltaTime, 0.0001f));
        }

        return dir * toward + other * Mathf.Lerp(1f, 0.35f, t);
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
        Vector3 shake = pendingShake > 0f ? Random.insideUnitSphere * pendingShake : Vector3.zero;
        pendingShake = 0f;
        cameraRoot.localRotation = Quaternion.Euler(pitch + shake.x, shake.y, shake.z);
        UpdateDreamView();
    }

    /// <summary>Widens and squeezes the view into a stretched-out hallway while DreamStretch is up.</summary>
    void UpdateDreamView()
    {
        if (viewCamera == null) viewCamera = cameraRoot.GetComponentInChildren<Camera>();
        if (viewCamera == null) return;

        float dream = Mathf.Clamp01(DreamStretch);
        if (dream <= 0f)
        {
            if (baseFov > 0f)
            {
                // Back to normal.
                viewCamera.fieldOfView = baseFov;
                viewCamera.ResetProjectionMatrix();
                baseFov = -1f;
                dreamEffort = 0f;
            }
            return;
        }
        if (baseFov < 0f) baseFov = viewCamera.fieldOfView;

        // Straining to get away stretches it further; giving up lets it ease back a little.
        float dt = Time.deltaTime;
        if (CurrentSpeed > 0.1f) dreamEffort = Mathf.Min(1f, dreamEffort + dt * 0.35f * CurrentSpeed / Mathf.Max(walkSpeed, 0.1f));
        else dreamEffort = Mathf.MoveTowards(dreamEffort, 0f, dt * 0.15f);

        float fov = Mathf.Lerp(baseFov, dreamWideFov, dream) + dreamEffortFov * dreamEffort * dream + Mathf.Sin(Time.time * 0.8f) * 2f * dream;
        fov = Mathf.Clamp(fov, 1f, 170f);
        viewCamera.fieldOfView = fov;
        float aspect = viewCamera.aspect * (1f - dreamSqueeze * dream);
        viewCamera.projectionMatrix = Matrix4x4.Perspective(fov, aspect, viewCamera.nearClipPlane, viewCamera.farClipPlane);
    }
}
