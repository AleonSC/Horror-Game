using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

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

    [Header("Stamina")]
    [Tooltip("Seconds of running on full stamina.")]
    public float sprintTime = 9f;
    [Tooltip("Seconds to get it all back, standing still (walking, a bit longer).")]
    public float recoverTime = 7f;
    [Tooltip("Each lamp lit makes getting your breath back this much slower: the night wears you down.")]
    public float recoverSlowdownPerLamp = 0.05f;
    [Tooltip("Run it right out and you can't run again until it's back to this.")]
    [Range(0f, 1f)] public float windedUntil = 0.35f;

    /// <summary>Stamina, 1 fresh to 0 spent. Running uses it; it comes back slowly.</summary>
    public float Stamina { get; private set; } = 1f;
    /// <summary>Run right out of breath: no running until you've got some back.</summary>
    public bool Winded { get; private set; }
    AudioSource breathing;
    float pantNoise;

    /// <summary>Uses up some stamina (struggling with something).</summary>
    public void SpendStamina(float amount)
    {
        Stamina = Mathf.Max(0f, Stamina - amount);
        if (Stamina <= 0f) Winded = true;
    }

    /// <summary>
    /// Stamina: running drains it, resting brings it back (slower the more lamps are lit). Out of breath, you
    /// pant (and the blind one can hear it, close by), and can't run until you've recovered a bit.
    /// </summary>
    void UpdateStamina(float dt)
    {
        if (IsRunning)
        {
            Stamina = Mathf.Max(0f, Stamina - dt / Mathf.Max(sprintTime, 0.1f));
            if (Stamina <= 0f) Winded = true;
        }
        else
        {
            float slow = 1f + recoverSlowdownPerLamp * Lamp.TimesLit;
            float moving = horizontalVelocity.magnitude > 0.5f ? 0.6f : 1f;
            Stamina = Mathf.Min(1f, Stamina + dt / Mathf.Max(recoverTime * slow, 0.1f) * moving);
        }
        if (Winded && Stamina >= windedUntil) Winded = false;

        // Breathing: heavier the more spent you are.
        if (breathing == null)
        {
            breathing = gameObject.AddComponent<AudioSource>();
            breathing.clip = ProceduralAudio.Breath;
            breathing.loop = true;
            breathing.spatialBlend = 0f;
            breathing.playOnAwake = false;
            breathing.volume = 0f;
        }
        float heavy = Mathf.Clamp01((0.6f - Stamina) / 0.6f);
        breathing.volume = Mathf.MoveTowards(breathing.volume, heavy * 0.55f, dt * 0.8f);
        breathing.pitch = Mathf.Lerp(0.95f, 1.35f, heavy);
        if (breathing.volume > 0.01f && !breathing.isPlaying) breathing.Play();
        else if (breathing.volume <= 0.01f && breathing.isPlaying) breathing.Stop();
        // Panting, badly winded: something close by could hear it.
        if (Stamina < 0.3f)
        {
            pantNoise -= dt;
            if (pantNoise <= 0f) { pantNoise = 1.5f; Noise.Emit(transform.position, 6f); }
        }
    }

    [Header("Waking Up")]
    [Tooltip("Seconds after spawning that the player is groggy: slow, heavy movement and blurred, swaying vision, clearing as it wears off.")]
    public float wakeTime = 9f;
    [Range(0f, 1f), Tooltip("Movement speed at the start, as a fraction of normal.")]
    public float wakeSpeed = 0.35f;
    [Range(0f, 1f), Tooltip("How blurred the view is at the start.")]
    public float wakeBlur = 0.7f;

    /// <summary>0 when fully awake, 1 just after spawning.</summary>
    public float Grogginess { get; private set; }

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
    /// <summary>Held fast by something (a crawler has your ankle): no walking or jumping until it's cleared.</summary>
    public bool Snared { get; set; }

    /// <summary>Sitting somewhere (a seat in the car): held there, free to look round, not walking.</summary>
    public bool Seated => seat != null;
    Transform seat;

    /// <summary>Sits the player down with their eyes at a point (a seat's eye point), held there until StandUp.</summary>
    public void SitAt(Transform eyes)
    {
        seat = eyes;
        if (controller != null) controller.enabled = false;
        horizontalVelocity = moveVelocity = Vector3.zero;
        verticalVelocity = 0f;
        HoldInSeat();
    }

    /// <summary>Gets up and steps out to a spot (on the ground).</summary>
    public void StandUp(Vector3 at)
    {
        seat = null;
        transform.position = at;
        if (controller != null) controller.enabled = !freeFlight;
    }

    void HoldInSeat()
    {
        if (seat == null) return;
        float eye = cameraRoot != null ? cameraRoot.localPosition.y : 1.6f;
        transform.position = seat.position - transform.up * eye;
    }

    /// <summary>Flying free (the dev tools' stick): no gravity, through anything, WASD along the view, Space up, Ctrl down, Shift fast.</summary>
    public bool FreeFlight
    {
        get => freeFlight;
        set
        {
            if (freeFlight == value) return;
            freeFlight = value;
            if (controller != null) controller.enabled = !value;
            verticalVelocity = 0f;
            horizontalVelocity = moveVelocity = Vector3.zero;
        }
    }
    bool freeFlight;
    [Tooltip("Free flight speed (Shift: three times as fast).")]
    public float freeFlightSpeed = 10f;
    /// <summary>How fast and which way the player is walking (horizontal).</summary>
    public Vector3 Velocity => moveVelocity;

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
        Tuning.Apply(this); // saved tuning (Save Tuning on the component's menu), before anything uses it
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
        wakeTimer = wakeTime;
    }

    float wakeTimer;
    Volume wakeVolume;

    /// <summary>
    /// Groggy for a few seconds after spawning: it wears off over wakeTime (only once the opening black screen
    /// has lifted, so you actually feel it). The blur is a depth of field over everything, fading out.
    /// </summary>
    void UpdateWaking()
    {
        if (IntroText.BlackScreen < 0.3f) wakeTimer = Mathf.Max(0f, wakeTimer - Time.deltaTime);
        float g = wakeTime > 0f ? wakeTimer / wakeTime : 0f;
        Grogginess = g * g * (3f - 2f * g); // clears slowly at first, then quickly

        if (Grogginess > 0f && wakeVolume == null)
        {
            wakeVolume = new GameObject("Waking Blur").AddComponent<Volume>();
            wakeVolume.isGlobal = true;
            wakeVolume.priority = 90f;
            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            wakeVolume.profile = profile;
            var dof = profile.Add<DepthOfField>(true);
            dof.focusMode.Override(DepthOfFieldMode.Manual);
            dof.nearFocusStart.Override(0f);
            dof.nearFocusEnd.Override(0f);
            dof.farFocusStart.Override(0.2f); // everything past arm's length blurred
            dof.farFocusEnd.Override(3f);
        }
        if (wakeVolume != null) wakeVolume.weight = Grogginess * wakeBlur;
    }

    /// <summary>Moves the player back to where they started and clears any movement effects.</summary>
    /// <summary>Moves the player by an offset, keeping their momentum and view (used by the door to the dark room).</summary>
    public void TeleportBy(Vector3 offset)
    {
        controller.enabled = false; // CharacterController overrides teleports while enabled
        transform.position += offset;
        controller.enabled = true;
    }

    public void ResetToSpawn() => RespawnAt(spawnPosition, spawnRotation);

    /// <summary>Puts the player somewhere (after being caught), clearing any movement effects, waking up groggy.</summary>
    public void RespawnAt(Vector3 position, Quaternion rotation)
    {
        seat = null; // (out of any seat)
        Stamina = 1f;
        Winded = false;
        controller.enabled = false; // CharacterController overrides teleports while enabled
        transform.SetPositionAndRotation(position, rotation);
        controller.enabled = true;

        horizontalVelocity = moveVelocity = Vector3.zero;
        verticalVelocity = 0f;
        pitch = 0f;
        approachLimits.Clear();
        pendingShake = 0f;
        ladder = null;
        ladderContacts = 0;
        wakeTimer = wakeTime; // waking up again
        Snared = false;
    }

    void Update()
    {
        if (PauseMenu.IsPaused || InventoryScreen.IsOpen) return;
        HandleCursor();
        UpdateWaking();
        if (!DroneUnit.Piloting) // (flying the drone: you stand still, the mouse and keys are its)
        {
            HandleLook();
            if (!freeFlight && !Seated) HandleCrouch();
        }
        if (Seated) HoldInSeat(); // (looking round freely, going nowhere)
        else if (freeFlight && !DroneUnit.Piloting) Fly();
        else HandleMovement();
        UpdateCamera();
    }

    void HandleCursor()
    {
        // Esc opens the pause menu (which frees the mouse); clicking back into the game recaptures it.
        if (!lockCursor || DevTools.IsOpen) return; // (the dev tools window needs the mouse)
        if (HorrorInput.ClickPressed && Cursor.lockState != CursorLockMode.Locked && !Hotbar.StickMenuOpen) SetCursorLocked(true);
    }

    static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    void HandleLook()
    {
        // The mouse only turns the view while it's captured (not while the dev tools window has it)...
        if (!lockCursor || Cursor.lockState == CursorLockMode.Locked)
        {
            // While a boss has your eye, the mouse barely moves the view: the moment comes first.
            float held = attractTimer > 0f && attractTarget != null ? 0.1f : 1f;
            Vector2 look = HorrorInput.Look * mouseSensitivity * Mathf.Lerp(1f, 0.5f, Grogginess) * held; // heavy-headed
            transform.Rotate(Vector3.up, look.x, Space.Self);

            pitch += invertY ? look.y : -look.y;
            pitch = Mathf.Clamp(pitch, -maxLookAngle, maxLookAngle);
        }

        // ...but a boss drawing your eye does it regardless (it used to wait for the mouse to be captured,
        // so with the dev tools open it never happened).
        // Something big happening: the view is drawn toward it, and held there until the moment is over.
        if (attractTimer > 0f && attractTarget != null)
        {
            attractTimer -= Time.deltaTime;
            float elapsed = attractDuration - attractTimer;
            float envelope = Mathf.SmoothStep(0f, 1f, elapsed / 0.6f) * Mathf.SmoothStep(0f, 1f, attractTimer / 1f);
            Vector3 to = attractTarget() - (cameraRoot != null ? cameraRoot.position : transform.position);
            if (to.sqrMagnitude > 0.01f)
            {
                float yawWanted = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                float pitchWanted = -Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
                float k = (1f - Mathf.Exp(-3.5f * Time.deltaTime)) * envelope * Mathf.Max(attractStrength, 0.85f);
                float yawStep = Mathf.DeltaAngle(transform.eulerAngles.y, yawWanted) * k;
                transform.Rotate(Vector3.up, yawStep, Space.Self);
                // Only part of the way up or down: a glance, not a stare.
                pitch = Mathf.Clamp(Mathf.Lerp(pitch, Mathf.Clamp(pitchWanted, -45f, 45f), k), -maxLookAngle, maxLookAngle);
            }
        }
    }

    /// <summary>True while a boss is holding the player's view (narration waits for it to finish).</summary>
    public static bool AttentionHeld => active != null && active.attractTimer > 0f && active.attractTarget != null;
    static FirstPersonController active;
    /// <summary>Clears this class's static state (StaticReset calls it when Play starts).</summary>
    internal static void ResetStatics() => active = null;
    void OnEnable() { active = this; }

    System.Func<Vector3> attractTarget;
    float attractTimer, attractDuration, attractStrength;
    [Range(0.1f, 1f), Tooltip("A boss's arrival holds your view on it for this share of the time it asks for (half).")]
    public float attentionHold = 0.5f;

    /// <summary>
    /// Draws the view toward something (a boss arriving), easing in and out, every time. The target is read
    /// every frame, so it can follow something moving. Strength 1 turns fully toward it. It's held for only
    /// attentionHold of the seconds asked for (half), then let go.
    /// </summary>
    public void DrawAttention(System.Func<Vector3> target, float seconds, float strength = 0.6f)
    {
        if (AttentionHeld) return; // one at a time: nothing cuts a boss's moment short
        seconds *= attentionHold;
        attractTarget = target;
        attractDuration = attractTimer = Mathf.Max(seconds, 1.7f); // (time to ease in, look, and ease out)
        attractStrength = Mathf.Clamp01(strength);
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

    void Fly()
    {
        Vector2 input = Vector2.ClampMagnitude(HorrorInput.Move, 1f);
        Transform view = cameraRoot != null ? cameraRoot : transform;
        Vector3 dir = view.forward * input.y + view.right * input.x
                      + Vector3.up * ((HorrorInput.JumpHeld ? 1f : 0f) - (HorrorInput.CrouchHeld ? 1f : 0f));
        float speed = freeFlightSpeed * (HorrorInput.RunHeld ? 3f : 1f);
        transform.position += Vector3.ClampMagnitude(dir, 1f) * speed * Time.deltaTime;
    }

    void HandleMovement()
    {
        bool grounded = controller.isGrounded;

        Vector2 input = Snared || DroneUnit.Piloting ? Vector2.zero : Vector2.ClampMagnitude(HorrorInput.Move, 1f); // held fast by the ankle: nowhere to go
        bool hasInput = input.sqrMagnitude > 0.01f;

        IsRunning = HorrorInput.RunHeld && !IsCrouching && hasInput && input.y > 0f && Grogginess < 0.3f // can't run yet
                    && !Winded && Stamina > 0f;                                                              // or out of breath
        UpdateStamina(Time.deltaTime);

        float targetSpeed = (IsCrouching ? crouchSpeed : IsRunning ? runSpeed : walkSpeed) * Water.SpeedFactor(transform.position) // wading is slow
                            * Mathf.Lerp(1f, wakeSpeed, Grogginess);                                                       // and so is waking up
        Vector3 desired = (transform.right * input.x + transform.forward * input.y) * targetSpeed;

        float accel = (grounded ? acceleration : acceleration * airControl) * Mathf.Lerp(1f, 0.35f, Grogginess); // sluggish to start and stop
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

            if (grounded && !IsCrouching && !Snared && !DroneUnit.Piloting && HorrorInput.JumpPressed)
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

        // Jumping off, partway up: let go and push back off it, falling from there.
        if (HorrorInput.JumpPressed && !atTop && !grounded && !Snared && !DroneUnit.Piloting)
        {
            Vector3 away = -transform.forward;
            away.y = 0f;
            var tower = ladder.GetComponentInParent<Watchtower>();
            if (tower != null)
            {
                Vector3 fromTower = transform.position - tower.transform.position;
                fromTower.y = 0f;
                if (fromTower.sqrMagnitude > 0.01f) away = fromTower;
            }
            away = away.sqrMagnitude > 0.001f ? away.normalized : -transform.forward;
            ladder = null;
            ladderLetGoUntil = Time.time + 0.8f; // it doesn't catch you again as you drop past it
            horizontalVelocity = moveVelocity = away * 3.5f;
            verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity) * 0.6f;
            return;
        }

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

    float ladderLetGoUntil = -10f;

    // Still within a ladder after jumping off it (and the moment's passed): back on.
    void OnTriggerStay(Collider other)
    {
        if (ladder != null || Time.time < ladderLetGoUntil) return;
        var l = other.GetComponent<Ladder>();
        if (l != null && ladderContacts > 0) ladder = l;
    }

    void OnTriggerEnter(Collider other)
    {
        var l = other.GetComponent<Ladder>();
        if (l == null) return;
        if (Time.time < ladderLetGoUntil) { ladderContacts++; return; } // just jumped off: not yet
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
        // Groggy: the head lolls and sways a little.
        float t = Time.time;
        Vector3 sway = new Vector3(Mathf.Sin(t * 0.9f) * 2f, Mathf.Sin(t * 0.6f + 1f) * 2.5f, Mathf.Sin(t * 0.7f + 2f) * 4f) * Grogginess;
        cameraRoot.localRotation = Quaternion.Euler(pitch + shake.x + sway.x, shake.y + sway.y, shake.z + sway.z);
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
