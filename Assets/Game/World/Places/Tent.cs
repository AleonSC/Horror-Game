using Game.Audio;
using Game.Core;
using Game.Player;
using Game.Rendering;
using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace Game.World
{
    /// <summary>
    /// A tent's door flaps (E, from inside or out): two triangles of canvas that drop shut across the doorway or
    /// are swept back and tied against the sides. Shut, they're solid; open, you can crawl in. The front of the
    /// tent is +Z of this object, its doorway a triangle from (±halfWidth, 0) up to the ridge.
    /// </summary>
    public sealed class TentFlap : MonoBehaviour, IDoor
    {
        float halfWidth = 0.85f, ridge = 1.18f;

        Mesh mesh;
        Vector3[] verts;
        BoxCollider shut;
        float open;     // 0 shut, 1 tied back
        bool opening = true;

        public bool IsOpen => opening && open > 0.9f;
        public string Prompt => opening ? "Close the flap" : "Open the flap";
        public bool CanInteract => true;

        // Shut: the middle of the doorway. Open: the tied-back flap on the side you're looking toward.
        public Vector3 FocusPoint
        {
            get
            {
                if (!opening) return transform.TransformPoint(new Vector3(0f, ridge * 0.4f, 0f));
                float side = 1f;
                if (Services.TryGet<PlayerController>(out var player) && player.Head != null)
                {
                    Vector3 local = transform.InverseTransformDirection(player.Head.forward);
                    Vector3 from = transform.InverseTransformPoint(player.Head.position);
                    // (which side of the middle the view crosses the doorway at)
                    float t = Mathf.Abs(local.z) > 0.01f ? -from.z / local.z : 0f;
                    side = from.x + local.x * t >= 0f ? 1f : -1f;
                }
                return transform.TransformPoint(Tied(side) * 0.8f + new Vector3(0f, ridge * 0.15f, 0f));
            }
        }

        Vector3 Tied(float side) => new Vector3(side * (halfWidth * 0.62f + 0.1f), ridge * 0.42f, -0.3f);

        /// <summary>Make the flaps: the doorway this wide each side of the middle and this high, in this canvas.</summary>
        public void Build(float doorHalfWidth, float doorHeight, Material canvas)
        {
            halfWidth = doorHalfWidth;
            ridge = doorHeight;
            mesh = new Mesh { name = "Tent flaps" };
            mesh.MarkDynamic();
            verts = new Vector3[6];
            mesh.vertices = verts;
            mesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.up, Vector2.zero, Vector2.right, Vector2.up };
            mesh.triangles = new[] { 0, 1, 2, 3, 5, 4 };
            var r = gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = canvas;
            gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            shut = gameObject.AddComponent<BoxCollider>();
            shut.center = new Vector3(0f, ridge * 0.4f, 0.02f);
            shut.size = new Vector3(halfWidth * 1.6f, ridge * 0.8f, 0.04f);
            // (something to aim at on each tied-back flap, that doesn't block you)
            for (int side = -1; side <= 1; side += 2)
            {
                var tie = new GameObject("Tied flap") { layer = gameObject.layer };
                tie.transform.SetParent(transform, false);
                tie.transform.localPosition = Tied(side) * 0.8f + new Vector3(0f, ridge * 0.15f, 0f);
                var c = tie.AddComponent<SphereCollider>();
                c.isTrigger = true;
                c.radius = 0.25f;
            }
            open = 1f;
            Shape();
        }

        void OnDestroy()
        {
            if (mesh != null) Destroy(mesh);
        }

        public void Interact()
        {
            opening = !opening;
            if (Services.TryGet<SoundPlayer>(out var sounds))
                sounds.Play(Sound.Rustle, transform.position + Vector3.up * 0.6f, MixerNames.World, 0.7f, opening ? 1.1f : 0.9f, 12f);
        }

        void Update()
        {
            if (shut == null) return;
            float target = opening ? 1f : 0f;
            shut.enabled = !opening;
            if (Mathf.Approximately(open, target)) return;
            open = Mathf.MoveTowards(open, target, Time.deltaTime / 0.6f);
            Shape();
        }

        // Each flap: from the ridge down to its foot, its free corner swinging from the middle of the doorway
        // (shut) round and out to where it's tied against the side (open).
        void Shape()
        {
            float t = Mathf.SmoothStep(0f, 1f, open);
            for (int i = 0; i < 2; i++)
            {
                float side = i == 0 ? -1f : 1f;
                Vector3 top = new Vector3(0f, ridge, 0f), foot = new Vector3(side * halfWidth, 0f, 0f);
                Vector3 closed = new Vector3(side * 0.02f, 0.02f, 0.01f);
                Vector3 corner = Vector3.Lerp(closed, Tied(side), t) + Vector3.forward * Mathf.Sin(t * Mathf.PI) * 0.25f;
                verts[i * 3] = top;
                verts[i * 3 + 1] = foot;
                verts[i * 3 + 2] = corner;
            }
            mesh.vertices = verts;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
        }
    }

    /// <summary>
    /// A tent's doorway: with the flap open, E crawls you in to sit on the blanket inside (outside sounds come
    /// through muffled, as the tent's a shelter); inside, E on the flap opens or shuts it, and E anywhere else
    /// crawls you back out, if it's open.
    /// </summary>
    public sealed class TentDoorway : MonoBehaviour, IDoor, IDoorwaySeat
    {
        public TentFlap flap;
        [Tooltip("Where you sit inside (this object's space), facing +Z, and where you come out.")]
        public Vector3 sitAt = new Vector3(0f, 0.03f, -0.35f), outAt = new Vector3(0f, 0.05f, 1.9f);
        [Tooltip("Sitting on the floor: how high your eye is.")]
        public float eyeHeight = 0.82f;

        Transform seat;
        bool inside;

        public string Prompt => "Crawl in";
        public bool CanInteract => !inside && flap != null && flap.IsOpen;
        // (The middle of the doorway, about where you'd look at it from standing in front: not down at its foot.)
        public Vector3 FocusPoint => transform.TransformPoint(new Vector3(0f, 0.75f, flap != null ? flap.transform.localPosition.z : 1f));

        public Transform SeatPoint => seat;
        public Vector3 ExitPoint => transform.TransformPoint(outAt);
        public float EyeHeight => eyeHeight;
        public bool CanLeave => flap == null || flap.IsOpen;
        public string CantLeave => "The flap's shut.   E on it to open it";

        void Start()
        {
            seat = new GameObject("Seat").transform;
            seat.SetParent(transform, false);
            seat.localPosition = sitAt;
            // (something to aim at in the doorway, that doesn't block you)
            var door = gameObject.AddComponent<BoxCollider>();
            door.isTrigger = true;
            float front = flap != null ? flap.transform.localPosition.z : 1f;
            door.center = new Vector3(0f, 0.45f, front - 0.25f);
            door.size = new Vector3(0.9f, 0.8f, 0.5f);
        }

        public bool CanSit(Vector3 from) => false; // (in by the doorway, with E, not with Y)

        // Walking (or crouching) into the open doorway crawls you in, as E does: the tent's too low and its sides
        // too steep to stand in, so there's no walking about inside.
        void Update()
        {
            if (inside || seat == null || flap == null || !flap.IsOpen) return;
            if (!Services.TryGet<PlayerController>(out var player) || player.Seat != null || !player.Moving) return;
            Vector3 local = transform.InverseTransformPoint(player.transform.position);
            float front = flap.transform.localPosition.z;
            bool inDoorway = Mathf.Abs(local.x) < 0.55f && local.z < front + 0.35f && local.z > front - 0.9f;
            bool heading = Vector3.Dot(player.transform.forward, -transform.forward) > 0.3f;
            if (inDoorway && heading) Interact();
        }

        public void Interact()
        {
            if (!CanInteract || !Services.TryGet<PlayerController>(out var player)) return;
            player.SitIn(this, "E to crawl out   ·   E on the flap to shut it");
        }

        public void Sat(bool now)
        {
            inside = now;
            if (Services.TryGet<SoundPlayer>(out var sounds))
                sounds.Play(Sound.Rustle, transform.position + Vector3.up * 0.4f, MixerNames.World, 0.6f, 0.8f, 10f);
        }
    }
}
