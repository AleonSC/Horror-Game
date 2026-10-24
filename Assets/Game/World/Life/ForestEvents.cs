using Game.Audio;
using Game.Core;
using Game.Player;
using Game.World.Build;
using UnityEngine;

namespace Game.World
{
    /// <summary>
    /// The forest's events (section 6.13): every minute or so a tree in plain view cracks, shudders and topples;
    /// and as you turn your head quickly, sometimes something that shouldn't be there (a chair, a stop sign, a
    /// gurney...) is waiting just past the edge of your view. Look back and it's gone.
    /// </summary>
    public sealed class ForestEvents : MonoBehaviour
    {
        ForestLifeConfig config;
        PlayerController player;
        readonly Collider[] nearby = new Collider[64];
        float nextFall;
        float lastYaw, lastPhantom = -1000f;
        GameObject phantom;
        bool phantomSeen;
        System.IDisposable runSub;

        public ForestLifeSettings Settings => config.settings;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (!Services.TryGet<PlayerController>(out _) || !Services.TryGet<WorldBuilder>(out _)) return;
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponent<ForestEvents>() == null)
                root.gameObject.AddComponent<ForestEvents>();
        }

        void Awake()
        {
            config = Services.Get<GameSetup>().Get<ForestLifeConfig>();
            if (config == null)
            {
                Debug.LogWarning("No ForestLifeConfig in the GameSetup asset; using defaults.");
                config = ScriptableObject.CreateInstance<ForestLifeConfig>();
            }
            Services.Register(this);
        }

        void Start()
        {
            nextFall = config.settings.firstTreeFall;
            if (Services.TryGet<EventBus>(out var events)) runSub = events.Subscribe<RunStarted>(_ => { nextFall = config.settings.firstTreeFall; ClearPhantom(); });
        }

        void OnDestroy()
        {
            runSub?.Dispose();
            Services.Unregister(this);
        }

        void Update()
        {
            if (player == null && !Services.TryGet(out player)) return;
            if (PlayerPlace.InDarkRoom) return;
            float elapsed = Services.TryGet<GameRun>(out var run) ? run.Elapsed : Time.time;
            if (elapsed >= nextFall)
            {
                var s = config.settings;
                nextFall = elapsed + (FellOne() ? Random.Range(s.minTreeFall, s.maxTreeFall) : 3f);
            }
            Phantom();
        }

        // ------------------------------------------------------------------ falling trees

        /// <summary>A tree in plain view, 12-25 m off, goes over (also the dev button). False if none's in view.</summary>
        public bool FellOne()
        {
            if (player == null || !Services.TryGet<WorldBuilder>(out var world) || world.Forest == null) return false;
            var s = config.settings;
            var eye = player.Head;
            int n = Physics.OverlapSphereNonAlloc(player.transform.position, s.treeFallFar, nearby, Layers.Mask(Layers.Trees), QueryTriggerInteraction.Ignore);
            ForestTree best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < n; i++)
            {
                var tree = nearby[i].GetComponent<ForestTree>();
                if (tree == null || tree.Down || tree.HasOwl) continue;
                Vector3 trunk = nearby[i].bounds.center;
                Vector3 to = trunk - eye.position;
                float flat = new Vector2(to.x, to.z).magnitude;
                if (flat < s.treeFallNear || flat > s.treeFallFar) continue;
                float angle = Vector3.Angle(Vector3.ProjectOnPlane(eye.forward, Vector3.up), Vector3.ProjectOnPlane(to, Vector3.up));
                if (angle > 35f) continue;
                if (PlayerSenseOccluded(eye.position, trunk, nearby[i])) continue;
                if (angle < bestScore) { bestScore = angle; best = tree; }
            }
            if (best == null) return false;
            // It falls across your view, never onto you.
            Vector3 toTree = best.transform.position - player.transform.position;
            toTree.y = 0f;
            Vector3 side = Vector3.Cross(Vector3.up, toTree).normalized * (Random.value < 0.5f ? -1f : 1f);
            Vector3 pushFrom = best.transform.position - (side + toTree.normalized * 0.4f);
            return best.KnockDown(world.Forest, pushFrom, 0.7f);
        }

        static bool PlayerSenseOccluded(Vector3 from, Vector3 to, Collider self)
        {
            Vector3 d = to - from;
            if (!Physics.Raycast(from, d.normalized, out var hit, d.magnitude, Layers.Mask(Layers.Trees) | Layers.Mask(Layers.Props) | Layers.Mask(Layers.Ground), QueryTriggerInteraction.Ignore)) return false;
            return hit.collider != self;
        }

        // ------------------------------------------------------------------ phantoms

        void Phantom()
        {
            var s = config.settings;
            var eye = player.Head;
            var cam = player.View;
            float yaw = eye.eulerAngles.y;
            float turn = Mathf.DeltaAngle(lastYaw, yaw) / Mathf.Max(0.0001f, Time.deltaTime);
            lastYaw = yaw;

            if (phantom != null)
            {
                bool inView = InView(cam, phantom.transform.position + Vector3.up * 0.6f);
                if (inView) phantomSeen = true;
                // Once seen, the moment it's out of sight it's gone.
                else if (phantomSeen || Time.time - lastPhantom > 6f) ClearPhantom();
                return;
            }
            if (Mathf.Abs(turn) < s.phantomTurnSpeed || Time.time - lastPhantom < s.phantomGap) return;
            lastPhantom = Time.time;
            if (Random.value > s.phantomChance) return;

            // Just past the edge of the view, on the side you're turning toward.
            float half = cam != null ? Camera.VerticalToHorizontalFieldOfView(cam.fieldOfView, cam.aspect) * 0.5f : 50f;
            float angle = Mathf.Sign(turn) * (half + 12f);
            Vector3 dir = Quaternion.Euler(0f, yaw + angle, 0f) * Vector3.forward;
            Vector3 at = player.transform.position + dir * Random.Range(6f, 11f);
            if (Physics.CheckSphere(at + Vector3.up, 0.8f, Layers.Mask(Layers.Trees) | Layers.Mask(Layers.Props), QueryTriggerInteraction.Ignore)) return;
            if (Physics.Raycast(at + Vector3.up * 20f, Vector3.down, out var hit, 40f, Layers.Mask(Layers.Ground), QueryTriggerInteraction.Ignore)) at = hit.point;
            int which = Random.Range(0, Mathf.Max(Phantoms.Count, config.phantoms != null ? config.phantoms.Length : 0));
            var yours = config.phantoms != null && config.phantoms.Length > 0 ? config.phantoms[which % config.phantoms.Length] : null;
            phantom = yours != null ? Instantiate(yours) : Phantoms.Make(which, null);
            phantom.transform.SetPositionAndRotation(at, Quaternion.LookRotation(-dir) * Quaternion.Euler(0f, Random.Range(-30f, 30f), 0f));
            phantomSeen = false;
        }

        static bool InView(Camera cam, Vector3 p)
        {
            if (cam == null) return false;
            Vector3 v = cam.WorldToViewportPoint(p);
            return v.z > 0f && v.x > -0.02f && v.x < 1.02f && v.y > -0.1f && v.y < 1.1f;
        }

        void ClearPhantom()
        {
            if (phantom != null) Destroy(phantom);
            phantom = null;
        }
    }
}
