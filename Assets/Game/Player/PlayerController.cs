using Game.Core;
using UnityEngine;

namespace Game.Player
{
    /// <summary>
    /// The first-person player (section 6.1): walk, run on stamina, crouch, jump, look, head bob, and use things
    /// with a left click. The camera is a child; the flashlight sits on the camera. Other systems can slow the
    /// player near something (facing a scour down), hold them still (being taken), shake the view, or move
    /// them (waking up again).
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Tooltip("Optional: settings to use instead of the ones in the GameSetup.")]
        [SerializeField] PlayerConfig configOverride;

        PlayerConfig config;
        CharacterController body;
        PlayerInputActions input;
        Transform head;
        Camera view;
        Vector3 velocity;
        float yaw, pitch;
        float height;
        float bobPhase;
        float shake;
        float groggy;
        Vector3 limitPoint;
        float limitSlowFrom, limitStopAt;
        int limitFrame = -10;

        public PlayerSettings Settings => config.settings;
        public Camera View => view;
        public Transform Head => head;
        public Flashlight Flashlight { get; private set; }
        public Interactor Interactor { get; private set; }
        public Stamina Stamina { get; } = new Stamina();
        public bool Crouching { get; private set; }
        public bool Running { get; private set; }
        public bool Moving { get; private set; }
        /// <summary>Held still: no moving or looking (something else is moving the camera).</summary>
        public bool Held { get; private set; }
        public Vector3 SpawnPoint { get; private set; }
        public Quaternion SpawnFacing { get; private set; }

        void Awake()
        {
            config = configOverride;
            if (config == null && Services.TryGet<GameSetup>(out var setup)) config = setup.Get<PlayerConfig>();
            if (config == null)
            {
                Debug.LogWarning("No PlayerConfig found; using defaults.");
                config = ScriptableObject.CreateInstance<PlayerConfig>();
            }

            body = GetComponent<CharacterController>();
            view = GetComponentInChildren<Camera>();
            head = view != null ? view.transform : transform;
            Flashlight = head.GetComponent<Flashlight>();
            if (Flashlight != null) Flashlight.HandleInput = false;
            Interactor = new Interactor();
            input = new PlayerInputActions();
            if (GetComponent<CatchFlow>() == null) gameObject.AddComponent<CatchFlow>();

            height = config.settings.standingHeight;
            yaw = transform.eulerAngles.y;
            SpawnPoint = transform.position;
            SpawnFacing = transform.rotation;

            Stamina.WindedChanged += winded =>
            {
                if (Services.TryGet<EventBus>(out var events)) events.Publish(new PlayerWinded(winded));
            };
            Services.Register(this);
        }

        void OnEnable() => input.Enable();
        void OnDisable() => input.Disable();

        void OnDestroy()
        {
            Services.Unregister(this);
            input.Dispose();
        }

        void Start() => SetCursorCaptured(true);

        void Update()
        {
            float dt = Time.deltaTime;
            var s = config.settings;
            bool uiOpen = Services.TryGet<UiState>(out var ui) && ui.AnyOpen;
            bool keysTaken = ui != null && ui.KeysTaken;
            shake = Mathf.MoveTowards(shake, 0f, dt * Mathf.Max(2f, shake * 3f));
            groggy = Mathf.Max(0f, groggy - dt);

            if (Held)
            {
                Moving = Running = false;
                return;
            }

            UpdateCursor(uiOpen);
            if (Cursor.lockState == CursorLockMode.Locked && !uiOpen) Look(s);

            if (!keysTaken && input.Use.WasPressedThisFrame() && Flashlight != null) Flashlight.Toggle();
            if (!uiOpen && input.Interact.WasPressedThisFrame() && Cursor.lockState == CursorLockMode.Locked) Interactor.TryInteract();

            Move(s, dt, keysTaken);
            Interactor.Update(head, s.reach, s.aimAssist);
        }

        void LateUpdate()
        {
            if (Held) return;
            var s = config.settings;
            // Head: eye height (crouching lowers it), bob while walking, and any shake.
            float eye = height - s.eyeBelowTop;
            Vector3 bob = Vector3.zero;
            if (Moving && body.isGrounded)
            {
                float speed = new Vector2(velocity.x, velocity.z).magnitude;
                bobPhase += Time.deltaTime * s.bobFrequency * Mathf.PI * 2f * Mathf.Clamp(speed / Mathf.Max(s.walkSpeed, 0.1f), 0.4f, 1.8f);
                bob = new Vector3(Mathf.Cos(bobPhase * 0.5f) * s.bobAmount * 0.5f, Mathf.Sin(bobPhase) * s.bobAmount, 0f);
            }
            head.localPosition = Vector3.Lerp(head.localPosition, new Vector3(0f, eye, 0f) + bob, 1f - Mathf.Exp(-12f * Time.deltaTime));

            Vector3 wobble = Vector3.zero;
            if (shake > 0f) wobble += Random.insideUnitSphere * shake;
            if (groggy > 0f)
            {
                float g = groggy / Mathf.Max(0.01f, s.groggyTime);
                float t = Time.time;
                wobble += new Vector3(Mathf.Sin(t * 0.9f) * 3f, Mathf.Sin(t * 0.7f + 1f) * 4f, Mathf.Sin(t * 0.5f) * 5f) * g;
            }
            head.localRotation = Quaternion.Euler(pitch, 0f, 0f) * Quaternion.Euler(wobble);
            if (view != null) view.fieldOfView = Mathf.Lerp(view.fieldOfView, s.fieldOfView, 1f - Mathf.Exp(-6f * Time.deltaTime));
        }

        void Look(PlayerSettings s)
        {
            float sensitivity = Services.TryGet<PlayerPreferences>(out var prefs) ? prefs.mouseSensitivity : 1f;
            Vector2 d = input.Look.ReadValue<Vector2>() * s.mouseSpeed * sensitivity;
            yaw += d.x;
            pitch = Mathf.Clamp(pitch - d.y, -85f, 85f);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }

        void Move(PlayerSettings s, float dt, bool keysTaken)
        {
            Vector2 move = keysTaken ? Vector2.zero : input.Move.ReadValue<Vector2>();
            if (move.sqrMagnitude > 1f) move.Normalize();
            Moving = move.sqrMagnitude > 0.01f;

            // Crouch while held down (stand back up only if there's room).
            bool wantCrouch = !keysTaken && input.Crouch.IsPressed();
            if (!wantCrouch && Crouching && !RoomToStand(s)) wantCrouch = true;
            Crouching = wantCrouch;
            float targetHeight = Crouching ? s.crouchingHeight : s.standingHeight;
            height = Mathf.MoveTowards(height, targetHeight, dt * 4f);
            body.height = height;
            body.center = new Vector3(0f, height * 0.5f, 0f);

            int lamps = Services.TryGet<GameRun>(out var run) ? run.LampsLit : 0;
            bool wantSprint = !keysTaken && input.Sprint.IsPressed() && !Crouching && move.y > 0.1f;
            Running = Stamina.Tick(dt, wantSprint, Moving, s, lamps);

            float speed = Crouching ? s.crouchSpeed : Running ? s.runSpeed : s.walkSpeed;
            if (groggy > 0f) speed *= Mathf.Lerp(1f, s.groggySpeed, groggy / Mathf.Max(0.01f, s.groggyTime));
            Vector3 wish = (transform.forward * move.y + transform.right * move.x) * speed;
            wish = ApplyApproachLimit(wish);

            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            float accel = s.acceleration * (body.isGrounded ? 1f : s.airControl);
            flat = Vector3.MoveTowards(flat, wish, accel * dt);
            velocity.x = flat.x;
            velocity.z = flat.z;

            if (body.isGrounded)
            {
                if (velocity.y < 0f) velocity.y = -2f; // (keeps it pressed to slopes)
                if (!keysTaken && !Crouching && input.Jump.WasPressedThisFrame())
                    velocity.y = Mathf.Sqrt(2f * s.jumpHeight * -s.gravity);
            }
            velocity.y += s.gravity * dt;
            body.Move(velocity * dt);
        }

        bool RoomToStand(PlayerSettings s)
        {
            float r = body.radius * 0.9f;
            Vector3 from = transform.position + Vector3.up * (height - r);
            return !Physics.SphereCast(from, r, Vector3.up, out _, s.standingHeight - height + 0.05f, ~Layers.Mask(Layers.PlayerBody), QueryTriggerInteraction.Ignore);
        }

        // Facing something down (a scour): the player slows as they close in and can't get nearer than stopAt.
        Vector3 ApplyApproachLimit(Vector3 wish)
        {
            if (Time.frameCount - limitFrame > 1) return wish;
            Vector3 to = limitPoint - transform.position;
            to.y = 0f;
            float dist = to.magnitude;
            if (dist < 0.001f) return wish;
            Vector3 dir = to / dist;
            float toward = Vector3.Dot(wish, dir);
            if (toward <= 0f) return wish;
            float factor = Mathf.Clamp01((dist - limitStopAt) / Mathf.Max(0.01f, limitSlowFrom - limitStopAt));
            return wish - dir * toward * (1f - factor);
        }

        /// <summary>Slow the player approaching a point from slowFrom metres, stopping at stopAt. Call every frame.</summary>
        public void LimitApproach(Vector3 point, float slowFrom, float stopAt)
        {
            limitPoint = point;
            limitSlowFrom = slowFrom;
            limitStopAt = stopAt;
            limitFrame = Time.frameCount;
        }

        /// <summary>Shake the view by about this many degrees (it dies away).</summary>
        public void AddShake(float degrees) => shake = Mathf.Max(shake, degrees);

        public void SetHeld(bool held)
        {
            if (Held == held) return;
            Held = held;
            if (held)
            {
                velocity = Vector3.zero;
                body.enabled = false; // (whoever holds the player moves them)
            }
            else
            {
                // Pick up the view where the camera was left.
                yaw = head.eulerAngles.y;
                float p = head.localEulerAngles.x;
                pitch = Mathf.Clamp(p > 180f ? p - 360f : p, -85f, 85f);
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                body.enabled = true;
            }
        }

        /// <summary>Move the player (waking up, restarting).</summary>
        public void Teleport(Vector3 position, Quaternion facing)
        {
            bool wasEnabled = body.enabled;
            body.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, facing.eulerAngles.y, 0f));
            yaw = transform.eulerAngles.y;
            pitch = 0f;
            velocity = Vector3.zero;
            body.enabled = wasEnabled;
        }

        public void StartGroggy() => groggy = config.settings.groggyTime;

        /// <summary>Where the run starts (and where you wake if you haven't lit a lamp yet).</summary>
        public void SetSpawn(Vector3 position, Quaternion facing)
        {
            SpawnPoint = position;
            SpawnFacing = facing;
        }

        void UpdateCursor(bool uiOpen)
        {
            if (uiOpen) return;
            var keyboard = UnityEngine.InputSystem.Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) SetCursorCaptured(false);
            else if (Cursor.lockState != CursorLockMode.Locked && input.Interact.WasPressedThisFrame()) SetCursorCaptured(true);
        }

        static void SetCursorCaptured(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }
    }
}
