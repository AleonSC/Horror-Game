using Game.Core;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;

namespace Game.Player
{
    /// <summary>
    /// The player's own body (section 9), on its own layer: the drone's camera and the portals see it, your eyes
    /// never do. It walks and runs at your pace, crouches, pants when winded, sits in the car, and holds the remote
    /// while you fly the drone. Built in: a hooded figure moved by code. Yours: its clips blended by what you're
    /// doing (PlayerCharacterConfig).
    /// </summary>
    public sealed class PlayerBody : MonoBehaviour
    {
        enum Doing { Idle, Walk, Run, CrouchIdle, CrouchWalk, Jump, Winded, Drone, Seated }

        PlayerController player;
        PlayerCharacterConfig config;
        Transform root, hips, torso, head, legL, legR, armL, armR, remote;
        float phase, crouch, lean, sit, fly;
        float scale = 1f;

        // Your model's clips.
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        AnimationClipPlayable[] playing;
        readonly float[] weights = new float[9];

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (Services.TryGet<PlayerController>(out var p) && p.GetComponent<PlayerBody>() == null) p.gameObject.AddComponent<PlayerBody>();
        }

        void Start()
        {
            player = GetComponent<PlayerController>();
            config = Services.TryGet<GameSetup>(out var setup) ? setup.Get<PlayerCharacterConfig>() : null;
            if (config == null) config = ScriptableObject.CreateInstance<PlayerCharacterConfig>();
            root = new GameObject("Your body").transform;
            root.SetParent(transform, false);
            if (config.model != null) UseYours();
            else BuildFigure();
            root.localPosition = config.offset;
            root.localRotation = Quaternion.Euler(0f, config.yawOffset, 0f);
            foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.PlayerBody;
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = config.castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
                foreach (var c in r.GetComponents<Collider>()) Destroy(c);
            }
            foreach (var c in root.GetComponentsInChildren<Collider>(true)) Destroy(c);
            Heat.Add(root, config.height, 0.3f); // (warm, on the drone's thermal view)
        }

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }

        Doing Now()
        {
            if (player.Seat != null) return Doing.Seated;
            if (player.Piloting) return Doing.Drone;
            if (!player.Grounded && !player.Held) return Doing.Jump;
            if (player.Stamina.Winded && player.Speed < 1f) return Doing.Winded;
            bool moving = player.Speed > 0.3f;
            if (player.Crouching) return moving ? Doing.CrouchWalk : Doing.CrouchIdle;
            if (!moving) return Doing.Idle;
            return player.Running ? Doing.Run : Doing.Walk;
        }

        void LateUpdate()
        {
            if (player == null || root == null) return;
            var now = Now();
            if (graph.IsValid()) Blend(now);
            else Pose(now);
        }

        // ------------------------------------------------------------------ the built-in figure

        void BuildFigure()
        {
            scale = config.height / 1.75f;
            var coat = RuntimeMaterials.Lit("Your coat", new Color(0.11f, 0.12f, 0.1f), 0.25f);
            var hood = RuntimeMaterials.Lit("Your hood", new Color(0.09f, 0.1f, 0.085f), 0.2f);
            var skin = RuntimeMaterials.Lit("Your face", new Color(0.02f, 0.018f, 0.016f), 0.3f); // (lost in the hood's shadow)
            var trousers = RuntimeMaterials.Lit("Your trousers", new Color(0.07f, 0.07f, 0.08f), 0.3f);
            var boots = RuntimeMaterials.Lit("Your boots", new Color(0.05f, 0.04f, 0.03f), 0.4f);
            var black = RuntimeMaterials.Lit("Remote", new Color(0.03f, 0.03f, 0.03f), 0.5f);

            hips = Pivot(root, new Vector3(0f, 0.92f, 0f), "Hips");
            torso = Pivot(hips, Vector3.zero, "Torso");
            Part(PrimitiveType.Capsule, torso, new Vector3(0f, 0.3f, 0f), new Vector3(0.4f, 0.32f, 0.26f), coat);
            Part(PrimitiveType.Cube, torso, new Vector3(0f, 0.05f, 0f), new Vector3(0.36f, 0.16f, 0.24f), coat); // (the coat's hem)
            head = Pivot(torso, new Vector3(0f, 0.62f, 0f), "Head");
            Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.08f, 0f), new Vector3(0.24f, 0.27f, 0.25f), skin);
            Part(PrimitiveType.Sphere, head, new Vector3(0f, 0.11f, -0.02f), new Vector3(0.3f, 0.32f, 0.3f), hood); // (the hood, open at the front)
            Part(PrimitiveType.Cube, head, new Vector3(0f, 0.25f, 0.06f), new Vector3(0.24f, 0.05f, 0.12f), hood);
            for (int side = -1; side <= 1; side += 2)
            {
                var leg = Pivot(hips, new Vector3(side * 0.1f, 0f, 0f), side < 0 ? "Left leg" : "Right leg");
                Part(PrimitiveType.Capsule, leg, new Vector3(0f, -0.44f, 0f), new Vector3(0.15f, 0.42f, 0.15f), trousers);
                Part(PrimitiveType.Cube, leg, new Vector3(0f, -0.86f, 0.05f), new Vector3(0.12f, 0.1f, 0.26f), boots);
                var arm = Pivot(torso, new Vector3(side * 0.23f, 0.5f, 0f), side < 0 ? "Left arm" : "Right arm");
                Part(PrimitiveType.Capsule, arm, new Vector3(0f, -0.3f, 0f), new Vector3(0.11f, 0.31f, 0.11f), coat);
                if (side < 0) { legL = leg; armL = arm; } else { legR = leg; armR = arm; }
            }
            remote = Pivot(armR, new Vector3(0f, -0.6f, 0.06f), "Remote");
            Part(PrimitiveType.Cube, remote, Vector3.zero, new Vector3(0.14f, 0.03f, 0.09f), black);
            remote.gameObject.SetActive(false);
            root.localScale = Vector3.one * scale;
        }

        // Moved by code from what you're doing: limbs swing with your stride, and it eases between poses.
        void Pose(Doing now)
        {
            float dt = Time.deltaTime;
            float speed = player.Speed;
            phase += dt * speed * 2.6f;
            float k = 1f - Mathf.Exp(-8f * dt);
            crouch = Mathf.Lerp(crouch, now == Doing.CrouchIdle || now == Doing.CrouchWalk ? 1f : 0f, k);
            sit = Mathf.Lerp(sit, now == Doing.Seated ? 1f : 0f, k);
            fly = Mathf.Lerp(fly, now == Doing.Drone ? 1f : 0f, k);
            lean = Mathf.Lerp(lean, now == Doing.Winded ? 1f : 0f, k * 0.6f);

            float stride = Mathf.Clamp01(speed / 4.5f) * (now == Doing.Run ? 42f : 30f) * (1f - sit);
            float swing = Mathf.Sin(phase) * stride;
            float breathe = Mathf.Sin(Time.time * (lean > 0.5f ? 7f : 1.6f)) * (1f + lean * 3f);

            hips.localPosition = new Vector3(0f, Mathf.Lerp(0.92f, 0.6f, crouch) - Mathf.Abs(Mathf.Cos(phase)) * 0.03f * Mathf.Clamp01(speed / 3f) - sit * 0.4f, 0f);
            torso.localRotation = Quaternion.Euler(Mathf.Lerp(0f, 22f, crouch) + lean * 35f + (now == Doing.Run ? 8f : 0f) + breathe * 0.6f, 0f, 0f);
            head.localRotation = Quaternion.Euler(-lean * 20f + fly * 22f, 0f, 0f);
            float legLift = crouch * 55f + sit * 85f;
            legL.localRotation = Quaternion.Euler(-swing - legLift, 0f, 0f);
            legR.localRotation = Quaternion.Euler(swing - legLift, 0f, 0f);
            // Arms: swinging against the legs; down and forward, hands on knees, winded; out in front with the remote.
            float armsForward = fly * 65f + lean * 40f;
            armL.localRotation = Quaternion.Euler(swing * 0.8f - armsForward, 0f, -fly * 15f);
            armR.localRotation = Quaternion.Euler(-swing * 0.8f - armsForward, 0f, fly * 15f);
            remote.gameObject.SetActive(fly > 0.5f);
            remote.localRotation = Quaternion.Euler(armsForward, 0f, 0f);
        }

        Transform Pivot(Transform parent, Vector3 at, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.localPosition = at;
            return t;
        }

        static void Part(PrimitiveType type, Transform parent, Vector3 at, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
        }

        // ------------------------------------------------------------------ your model

        void UseYours()
        {
            var model = Instantiate(config.model, root, false);
            model.name = "Your model";
            // Scale it to the height asked for (from its own size).
            var bounds = new Bounds(model.transform.position, Vector3.zero);
            bool any = false;
            foreach (var r in model.GetComponentsInChildren<Renderer>())
            {
                if (!any) { bounds = r.bounds; any = true; }
                else bounds.Encapsulate(r.bounds);
            }
            if (any && bounds.size.y > 0.01f) model.transform.localScale *= config.height / bounds.size.y;

            var animator = model.GetComponentInChildren<Animator>();
            var c = config.clips;
            var clips = new[] { c.idle, c.walk, c.run, c.crouchIdle, c.crouchWalk, c.jump, c.winded, c.droneControl, c.seated };
            if (animator == null || c.idle == null) return; // (no animation: it just stands)
            animator.applyRootMotion = false;
            graph = PlayableGraph.Create("Your body");
            graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            mixer = AnimationMixerPlayable.Create(graph, clips.Length);
            playing = new AnimationClipPlayable[clips.Length];
            for (int i = 0; i < clips.Length; i++)
            {
                playing[i] = AnimationClipPlayable.Create(graph, clips[i] != null ? clips[i] : c.idle);
                graph.Connect(playing[i], 0, mixer, i);
                mixer.SetInputWeight(i, i == 0 ? 1f : 0f);
            }
            weights[0] = 1f;
            AnimationPlayableOutput.Create(graph, "Body", animator).SetSourcePlayable(mixer);
            graph.Play();

            // The remote, in its right hand (humanoid).
            if (animator.isHuman)
            {
                var hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
                if (hand != null)
                {
                    remote = new GameObject("Remote").transform;
                    remote.SetParent(hand, false);
                    Part(PrimitiveType.Cube, remote, new Vector3(0f, 0.03f, 0.05f), new Vector3(0.14f, 0.03f, 0.09f) / Mathf.Max(0.01f, hand.lossyScale.x), RuntimeMaterials.Lit("Remote", new Color(0.03f, 0.03f, 0.03f), 0.5f));
                    remote.gameObject.SetActive(false);
                }
            }
        }

        // Each clip's weight eases toward what you're doing; walk and run play at your pace.
        void Blend(Doing now)
        {
            float k = 1f - Mathf.Exp(-8f * Time.deltaTime);
            for (int i = 0; i < weights.Length; i++)
            {
                weights[i] = Mathf.Lerp(weights[i], i == (int)now ? 1f : 0f, k);
                mixer.SetInputWeight(i, weights[i]);
            }
            var c = config.clips;
            float speed = player.Speed;
            playing[(int)Doing.Walk].SetSpeed(Mathf.Max(0.2f, speed / Mathf.Max(0.1f, c.walkClipSpeed)));
            playing[(int)Doing.Run].SetSpeed(Mathf.Max(0.2f, speed / Mathf.Max(0.1f, c.runClipSpeed)));
            playing[(int)Doing.CrouchWalk].SetSpeed(Mathf.Max(0.2f, speed / Mathf.Max(0.1f, c.walkClipSpeed * 0.6f)));
            if (remote != null) remote.gameObject.SetActive(now == Doing.Drone);
        }
    }
}
