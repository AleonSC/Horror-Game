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
        float rumble, rumbleLeft, rumbleFor = 1f;
        Vector3 headRest;
        float groggy, groggyFull = 1f;
        float ladderFree; // (just jumped off a ladder: it doesn't catch you again till this time)
        bool blurred;
        float nextPant;
        Vector3 gazeTarget;
        float gazeLeft, gazeTime;
        Vector3 limitPoint;
        float limitSlowFrom, limitStopAt;
        int limitFrame = -10;
        float slowBy = 1f, slowNow = 1f;
        int slowFrame = -10;

        public PlayerSettings Settings => config.settings;
        public Camera View => view;
        public Transform Head => head;
        public Flashlight Flashlight { get; private set; }
        public Interactor Interactor { get; private set; }
        public Hotbar Hotbar { get; private set; }
        public Stamina Stamina { get; } = new Stamina();
        public bool Crouching { get; private set; }
        public bool Running { get; private set; }
        public bool Moving { get; private set; }
        /// <summary>Held still: no moving or looking (something else is moving the camera).</summary>
        public bool Held { get; private set; }
        /// <summary>Flying the drone: you stand still, seeing and hearing only through it.</summary>
        public bool Piloting { get; set; }
        /// <summary>How fast you're moving over the ground (metres a second).</summary>
        public float Speed => new Vector2(velocity.x, velocity.z).magnitude;
        public bool Grounded => body != null && body.isGrounded;
        /// <summary>Is this point under a pond's water? (The world sets it.)</summary>
        public static System.Func<Vector3, bool> WaterAt;
        /// <summary>Wading speed, as a share of normal (the world sets it).</summary>
        public static float WadeSpeed = 0.55f;
        /// <summary>Standing in a pond.</summary>
        public bool Wading { get; private set; }

        /// <summary>The seat you're in (the car), or null.</summary>
        public ISeat Seat { get; private set; }
        /// <summary>F held down this frame (the spray can sprays while it is).</summary>
        public bool UsingHeld { get; private set; }
        /// <summary>On a ladder right now.</summary>
        public bool Climbing { get; private set; }
        /// <summary>Space pressed this frame, even while held (struggling free of a crawler).</summary>
        public bool StrugglePressed => input.Jump.WasPressedThisFrame();
        /// <summary>0-1: the dream hallway (the giant's eye): the view stretches wide and running gets you nowhere.</summary>
        public float Dream { get; set; }
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
            // (Your own body is for the drone's camera and the portals, never your eyes.)
            if (view != null) view.cullingMask &= ~Layers.Mask(Layers.PlayerBody);
            head = view != null ? view.transform : transform;
            Flashlight = head.GetComponent<Flashlight>();
            if (Flashlight != null) Flashlight.HandleInput = false;
            Interactor = new Interactor();
            input = new PlayerInputActions();
            if (GetComponent<CatchFlow>() == null) gameObject.AddComponent<CatchFlow>();
            Hotbar = GetComponent<Hotbar>();
            if (Hotbar == null) Hotbar = gameObject.AddComponent<Hotbar>();

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
            UpdateBlur(s);

            if (Held || Piloting || !GameFlow.IsPlaying)
            {
                Moving = Running = UsingHeld = false;
                return;
            }

            UpdateCursor(uiOpen);
            UsingHeld = !keysTaken && !uiOpen && Seat == null && input.Use.IsPressed();
            if (Cursor.lockState == CursorLockMode.Locked && !uiOpen) Look(s);

            Hotbar.HandleSelection(keysTaken, uiOpen);
            if (!keysTaken && input.Use.WasPressedThisFrame()) Hotbar.Use();
            bool clicking = !uiOpen && Cursor.lockState == CursorLockMode.Locked;
            // A click uses what you're aiming at; with nothing there, the button on what you hold (the key fob).
            if (clicking && input.Interact.WasPressedThisFrame() && !Interactor.TryInteract()) Hotbar.Click(false);
            if (clicking && UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.rightButton.wasPressedThisFrame) Hotbar.Click(true);
            // Seated, E gets you out (rather than reaching for a door).
            if (!keysTaken && input.Door.WasPressedThisFrame())
            {
                if (Seat is IDoorwaySeat doorway)
                {
                    // In a tent: E on the flap opens or shuts it; anywhere else, out you crawl (if it's open).
                    if (!Interactor.TryUseDoor())
                    {
                        if (doorway.CanLeave) GetOut();
                        else if (Hotbar != null) Hotbar.Say(doorway.CantLeave, 2f);
                    }
                }
                else if (Seat != null) GetOut();
                else Interactor.TryUseDoor();
            }

            if (!keysTaken && input.Sit.WasPressedThisFrame() && !(Seat is IDoorwaySeat)) ToggleSeat();
            if (Seat != null) KeepSeated();
            else Move(s, dt, keysTaken);
            Pant(s);
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
                float before = bobPhase;
                bobPhase += Time.deltaTime * s.bobFrequency * Mathf.PI * 2f * Mathf.Clamp(speed / Mathf.Max(s.walkSpeed, 0.1f), 0.4f, 1.8f);
                // A step at the bottom of each bob.
                if (Mathf.Floor(before / Mathf.PI) != Mathf.Floor(bobPhase / Mathf.PI)) Footstep(s);
                float amount = s.bobAmount * (Running ? 1.35f : Crouching ? 0.7f : 1f); // (heavier when running, softer creeping)
                bob = new Vector3(Mathf.Cos(bobPhase * 0.5f) * amount * 0.5f, Mathf.Sin(bobPhase) * amount, 0f);
            }
            headRest = Vector3.Lerp(headRest == Vector3.zero ? head.localPosition : headRest, new Vector3(0f, eye, 0f) + bob, 1f - Mathf.Exp(-12f * Time.deltaTime));
            head.localPosition = headRest;

            Vector3 wobble = Vector3.zero;
            if (shake > 0f) wobble += Random.insideUnitSphere * shake;
            if (rumbleLeft > 0f)
            {
                // The ground heaving: a drop as it hits, then a deep tremor dying away.
                rumbleLeft -= Time.deltaTime;
                float k = Mathf.Clamp01(rumbleLeft / rumbleFor);
                float since = rumbleFor - rumbleLeft;
                float t = Time.time;
                float tremor = (Mathf.Sin(t * 57f) * 0.6f + Mathf.Sin(t * 31f + 1.3f) * 0.4f) * k * k;
                float drop = Mathf.Sin(Mathf.Clamp01(since / 0.35f) * Mathf.PI);
                head.localPosition = headRest + Vector3.up * (tremor * 0.02f - drop * 0.06f) * rumble;
                wobble += new Vector3(tremor * 0.7f + drop * 1.2f, 0f, Mathf.Sin(t * 23f) * k * k * 1.1f) * rumble;
            }
            if (groggy > 0f)
            {
                // Coming round: the head lolls and sways, settling as it clears (slowly at first, then quickly).
                float g = Ease(Groggy);
                float t = Time.time;
                wobble += new Vector3(Mathf.Sin(t * 0.9f) * 2f, Mathf.Sin(t * 0.6f + 1f) * 2.5f, Mathf.Sin(t * 0.7f + 2f) * 4f) * (s.groggySway * g);
                wobble += new Vector3(Mathf.PerlinNoise(t * 9f, 0.3f) - 0.5f, Mathf.PerlinNoise(1.7f, t * 9f) - 0.5f, Mathf.PerlinNoise(t * 7f, 4.1f) - 0.5f) * (2f * s.groggyShake * g * g);
            }
            head.localRotation = Quaternion.Euler(pitch, 0f, 0f) * Quaternion.Euler(wobble);
            float fov = Mathf.Lerp(s.fieldOfView, s.dreamFieldOfView, Mathf.SmoothStep(0f, 1f, Dream));
            if (view != null) view.fieldOfView = Mathf.Lerp(view.fieldOfView, fov, 1f - Mathf.Exp(-6f * Time.deltaTime));
        }

        // A footstep: heard by you (faintly), and by anything hunting by sound, further when running.
        void Footstep(PlayerSettings s)
        {
            float noise = Crouching ? s.crouchNoise : Running ? s.runNoise : s.walkNoise;
            float volume = s.stepVolume * (Crouching ? 0.35f : Running ? 1.25f : 1f) * Random.Range(0.8f, 1.05f);
            // Wading: splashes, and louder.
            if (Wading) { noise *= 1.6f; volume *= 1.5f; }
            if (Services.TryGet<Game.Audio.SoundPlayer>(out var sounds))
                sounds.Play(Wading ? Game.Audio.Sound.Splash : NextStep(), transform.position, Game.Audio.MixerNames.Player, volume, Random.Range(0.92f, 1.08f) * (Running ? 1.05f : 1f), 12f);
            if (noise > 0f && Services.TryGet<EventBus>(out var events))
                events.Publish(new NoiseEmitted(transform.position, noise, Crouching ? "creeping" : Running ? "running" : "walking"));
        }

        static readonly Game.Audio.Sound[] steps = { Game.Audio.Sound.Step, Game.Audio.Sound.Step2, Game.Audio.Sound.Step3, Game.Audio.Sound.Step4, Game.Audio.Sound.Step5, Game.Audio.Sound.Step6 };
        int lastStep = -1;

        // One of the footsteps, never the same one twice running.
        Game.Audio.Sound NextStep()
        {
            int i = Random.Range(0, steps.Length - 1);
            if (i >= lastStep) i++;
            if (lastStep < 0) i = Random.Range(0, steps.Length);
            lastStep = i;
            return steps[i];
        }

        // Winded: you pant (seen in your breath, no longer heard), and something close by can hear it.
        void Pant(PlayerSettings s)
        {
            if (!Stamina.Winded || Time.time < nextPant) return;
            nextPant = Time.time + 1.4f;
            if (s.pantNoise > 0f && Services.TryGet<EventBus>(out var events))
                events.Publish(new NoiseEmitted(transform.position, s.pantNoise, "panting"));
        }

        // Y: into a seat (the car); in one, along to the next (or, if it has no others, out again).
        void ToggleSeat()
        {
            if (Seat != null)
            {
                if (Seat is ISeatSwitcher switcher)
                {
                    switcher.NextSeat();
                    if (Hotbar != null) Hotbar.Say($"{switcher.SeatName}.   E to get out", 2f);
                    return;
                }
                GetOut();
                return;
            }
            var seat = Seats.Near(transform.position);
            if (seat == null) return;
            Seat = seat;
            body.enabled = false;
            velocity = Vector3.zero;
            seat.Sat(true);
            if (Hotbar != null) Hotbar.Say(seat is ISeatSwitcher sw ? $"{sw.SeatName}.   Y to move over   ·   E to get out" : "E to get out", 3f);
        }

        /// <summary>Into a seat that you get into some other way than Y (a tent's doorway). Facing the way its point faces.</summary>
        public void SitIn(ISeat seat, string hint = null)
        {
            if (seat == null || Seat != null) return;
            Seat = seat;
            body.enabled = false;
            velocity = Vector3.zero;
            seat.Sat(true);
            if (seat.SeatPoint != null)
            {
                yaw = seat.SeatPoint.eulerAngles.y;
                pitch = 0f;
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            }
            if (Hotbar != null && hint != null) Hotbar.Say(hint, 3f);
        }

        /// <summary>Out of the seat you're in (a tent's doorway uses this).</summary>
        public void LeaveSeat() => GetOut();

        // Out of the seat you're in, by its door.
        void GetOut()
        {
            if (Seat == null) return;
            var exit = Seat.ExitPoint;
            Seat.Sat(false);
            Seat = null;
            Teleport(exit, Quaternion.Euler(0f, yaw, 0f));
            body.enabled = true;
        }

        void KeepSeated()
        {
            Moving = Running = Crouching = false;
            var p = Seat.SeatPoint;
            transform.position = p.position;
            // Seated: a lower eye, and the body turns with the view.
            float seated = Seat is IDoorwaySeat low ? low.EyeHeight + config.settings.eyeBelowTop : config.settings.crouchingHeight + 0.2f;
            height = Mathf.MoveTowards(height, seated, Time.deltaTime * 4f);
        }

        System.Func<Vector3> gazeLock;
        float gazeLockFrom, gazeLockUntil;

        /// <summary>Drag the view onto something and hold it there for this long (the mouse does nothing meanwhile).</summary>
        public void LockGaze(System.Func<Vector3> target, float seconds)
        {
            gazeLock = target;
            gazeLockFrom = Time.time;
            gazeLockUntil = Time.time + seconds;
        }

        /// <summary>Gently draw the view toward a point over a few seconds (the mouse still works).</summary>
        public void DrawGazeTo(Vector3 point, float seconds)
        {
            gazeTarget = point;
            gazeTime = gazeLeft = Mathf.Max(0.1f, seconds);
        }

        void Look(PlayerSettings s)
        {
            // Held by something (the blind one bursting out of the ground): eyes dragged onto it, the mouse does
            // nothing, until it lets go.
            if (gazeLock != null && Time.time < gazeLockUntil)
            {
                Vector3 to = gazeLock() - head.position;
                float wantYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                float wantPitch = -Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
                float k = 1f - Mathf.Exp(-Mathf.Lerp(1.5f, 6f, Mathf.Clamp01((Time.time - gazeLockFrom) / 1.2f)) * Time.deltaTime);
                yaw = Mathf.LerpAngle(yaw, wantYaw, k);
                pitch = Mathf.Clamp(Mathf.Lerp(pitch, wantPitch, k), -85f, 85f);
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                return;
            }
            gazeLock = null;
            // In a seat that holds the view (a game on a TV): eyes on it, the mouse does nothing.
            if (Seat is ILockedView locked)
            {
                Vector3 to = locked.LookAt - head.position;
                float wantYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                float wantPitch = -Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
                float k = 1f - Mathf.Exp(-8f * Time.deltaTime);
                yaw = Mathf.LerpAngle(yaw, wantYaw, k);
                pitch = Mathf.Lerp(pitch, wantPitch, k);
                transform.rotation = Quaternion.Euler(0f, yaw, 0f);
                return;
            }
            float sensitivity = Services.TryGet<PlayerPreferences>(out var prefs) ? prefs.mouseSensitivity : 1f;
            Vector2 d = input.Look.ReadValue<Vector2>() * s.mouseSpeed * sensitivity * Mathf.Lerp(1f, s.groggyLook, Ease(Groggy));
            yaw += d.x;
            pitch = Mathf.Clamp(pitch - d.y, -85f, 85f);
            if (gazeLeft > 0f)
            {
                // Strongest in the middle of the pull, easing in and out.
                gazeLeft -= Time.deltaTime;
                float pull = Mathf.Sin(Mathf.Clamp01(1f - gazeLeft / gazeTime) * Mathf.PI) * 2.5f * Time.deltaTime;
                Vector3 to = gazeTarget - head.position;
                float wantYaw = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;
                float wantPitch = -Mathf.Atan2(to.y, new Vector2(to.x, to.z).magnitude) * Mathf.Rad2Deg;
                yaw = Mathf.LerpAngle(yaw, wantYaw, pull);
                pitch = Mathf.Clamp(Mathf.Lerp(pitch, wantPitch, pull), -85f, 85f);
            }
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
            bool wantSprint = !keysTaken && input.Sprint.IsPressed() && !Crouching && move.y > 0.1f && Ease(Groggy) < 0.3f; // (too groggy to run)
            Running = Stamina.Tick(dt, wantSprint, Moving, s, lamps);

            float speed = Crouching ? s.crouchSpeed : Running ? s.runSpeed : s.walkSpeed;
            if (groggy > 0f) speed *= Mathf.Lerp(1f, s.groggySpeed, Ease(Groggy)); // (slow, then picking up)
            if (Dream > 0f) speed *= Mathf.Lerp(1f, s.dreamSpeed, Dream);
            Wading = WaterAt != null && WaterAt(transform.position);
            if (Wading) speed *= WadeSpeed;
            slowNow = Mathf.MoveTowards(slowNow, Time.frameCount - slowFrame <= 1 ? slowBy : 1f, dt * 2.5f); // (eased in and out)
            speed *= slowNow;
            Vector3 wish = (transform.forward * move.y + transform.right * move.x) * speed;
            wish = ApplyApproachLimit(wish);

            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            float accel = s.acceleration * (body.isGrounded ? 1f : s.airControl) * Mathf.Lerp(1f, s.groggyAcceleration, Ease(Groggy)); // (sluggish to start and stop, waking)
            flat = Vector3.MoveTowards(flat, wish, accel * dt);
            velocity.x = flat.x;
            velocity.z = flat.z;

            // On a ladder: W climbs the way you look (down when you look down), Space jumps off it: back away
            // from it (or the way you're looking, if that's away from it) with a little hop, and it can't catch
            // you again for a moment.
            var ladder = Time.time < ladderFree ? null : Ladder.At(transform.position + Vector3.up * 0.9f);
            if (ladder != null && !keysTaken && input.Jump.WasPressedThisFrame())
            {
                Vector3 away = transform.position - ladder.Centre;
                away.y = 0f;
                away = away.sqrMagnitude > 0.0001f ? away.normalized : -transform.forward;
                Vector3 facing = transform.forward;
                Vector3 dir = Vector3.Dot(facing, away) > 0.3f ? facing : away;
                velocity = dir * s.ladderJumpOut + Vector3.up * Mathf.Sqrt(2f * s.jumpHeight * 0.6f * -s.gravity);
                ladderFree = Time.time + 0.6f;
                ladder = null;
            }
            Climbing = ladder != null;
            if (Climbing)
            {
                float way = pitch > 35f ? -1f : 1f;
                // Held to the ladder off the ground (and always near its top): no sliding round it, or under a deck.
                bool nearTop = ladder.hasTop && transform.position.y > ladder.TopWorld.y - 1.2f;
                // At the top with your back to it (having just stepped on from the deck), W takes you down.
                if (nearTop && pitch > -25f && Vector3.Dot(transform.forward, ladder.TopWorld - transform.position) < 0f) way = -1f;
                velocity.y = move.y * way * (Running ? s.climbSprintSpeed : s.climbSpeed);
                if (!body.isGrounded || nearTop)
                {
                    Vector3 hold = ladder.Centre - transform.position;
                    velocity.x = hold.x * 5f;
                    velocity.z = hold.z * 5f;
                }
                else
                {
                    velocity.x *= 0.5f;
                    velocity.z *= 0.5f;
                }
                // Over the top: up to the top's level, then across onto it.
                if (ladder.hasTop && velocity.y > 0f && transform.position.y > ladder.TopWorld.y - 0.6f)
                {
                    Vector3 top = ladder.TopWorld;
                    if (transform.position.y < top.y + 0.05f) velocity = Vector3.up * Mathf.Max(velocity.y, 1.5f);
                    else
                    {
                        Vector3 across = top - transform.position;
                        across.y = 0f;
                        velocity = across.sqrMagnitude > 0.0001f ? across.normalized * 2.4f : Vector3.zero;
                    }
                }
                body.Move(velocity * dt);
                return;
            }

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

        /// <summary>Slow the player to this share of their speed (any direction) this frame. Call every frame.</summary>
        public void SlowThisFrame(float factor)
        {
            if (slowFrame != Time.frameCount) slowBy = 1f;
            slowBy = Mathf.Min(slowBy, Mathf.Clamp01(factor));
            slowFrame = Time.frameCount;
        }

        /// <summary>Slow the player approaching a point from slowFrom metres, stopping at stopAt. Call every frame.</summary>
        public void LimitApproach(Vector3 point, float slowFrom, float stopAt)
        {
            // (Two at once: the nearer one holds you off; the last to ask used to win, so you could walk into the other.)
            if (limitFrame == Time.frameCount && (point - transform.position).sqrMagnitude - stopAt * stopAt > (limitPoint - transform.position).sqrMagnitude - limitStopAt * limitStopAt) return;
            limitPoint = point;
            limitSlowFrom = slowFrom;
            limitStopAt = stopAt;
            limitFrame = Time.frameCount;
        }

        /// <summary>Shake the view by about this many degrees (it dies away).</summary>
        public void AddShake(float degrees) => shake = Mathf.Max(shake, degrees);

        /// <summary>
        /// The ground heaving under you (a giant's step, its landing): the view drops and a deep tremor runs through
        /// it, dying away over this many seconds. <paramref name="amount"/> about 1 for a heavy step close by.
        /// </summary>
        public void AddRumble(float amount, float seconds)
        {
            if (rumbleLeft > 0f && rumble * (rumbleLeft / rumbleFor) > amount) return; // (a bigger one's still going)
            rumble = amount;
            rumbleLeft = rumbleFor = Mathf.Max(0.1f, seconds);
        }

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
        public void Teleport(Vector3 position, Quaternion facing, bool keepPitch = false)
        {
            bool wasEnabled = body.enabled;
            body.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, facing.eulerAngles.y, 0f));
            yaw = transform.eulerAngles.y;
            if (!keepPitch) pitch = 0f;
            velocity = Vector3.zero;
            body.enabled = wasEnabled;
        }

        public void StartGroggy() => StartGroggy(config.settings.groggyTime);

        /// <summary>Groggy for this many seconds: slow, swaying, slow to look round, vision blurred; all wearing off.</summary>
        public void StartGroggy(float seconds)
        {
            groggyFull = Mathf.Max(0.01f, seconds);
            groggy = groggyFull;
        }

        /// <summary>Groggy, but only this much of the way (0-1) to just woken, wearing off over this many seconds.</summary>
        public void StartGroggy(float seconds, float amount)
        {
            groggyFull = Mathf.Max(0.01f, seconds);
            groggy = groggyFull * Mathf.Clamp01(amount);
        }

        static float Ease(float x) => x * x * (3f - 2f * x);

        /// <summary>How groggy, 1 just woken to 0 clear.</summary>
        float Groggy => groggy > 0f ? groggy / groggyFull : 0f;

        void UpdateBlur(PlayerSettings s)
        {
            if (!blurred && groggy <= 0f) return;
            if (!Services.TryGet<Game.Rendering.LookController>(out var look)) return;
            float g = Groggy;
            look.Blur = s.groggyBlur * Ease(g);
            blurred = groggy > 0f;
        }

        /// <summary>Where the run starts (and where you wake if you haven't lit a lamp yet).</summary>
        public void SetSpawn(Vector3 position, Quaternion facing)
        {
            SpawnPoint = position;
            SpawnFacing = facing;
        }

        void UpdateCursor(bool uiOpen)
        {
            // (Esc opens the pause menu, which frees the mouse; a click back in the game captures it again.)
            if (uiOpen) return;
            if (Cursor.lockState != CursorLockMode.Locked && input.Interact.WasPressedThisFrame()) SetCursorCaptured(true);
        }

        public static void SetCursorCaptured(bool captured)
        {
            Cursor.lockState = captured ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !captured;
        }
    }
}
