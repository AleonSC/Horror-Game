using Game.Core;
using Game.Rendering;
using Game.World.Generation;
using UnityEngine;

namespace Game.World.Build
{
    /// <summary>
    /// The signposts at the lamps: an old weathered post a couple of metres from each lamp, out toward the nearest
    /// other one, its plank cut to a point and aimed at it (the world wraps: the nearest the short way round). A
    /// smear of the lamps' red on the point. Your own (WorldArtConfig > signpost): pointing along +Z, its foot
    /// at the pivot.
    /// </summary>
    public sealed class SignpostFactory
    {
        readonly Material wood, plank, paint;
        readonly GameObject prefab;

        public SignpostFactory(GameObject prefab = null)
        {
            this.prefab = prefab;
            wood = RuntimeMaterials.Lit("Signpost wood", new Color(0.13f, 0.1f, 0.075f), 0.12f);
            plank = RuntimeMaterials.Lit("Signpost plank", new Color(0.21f, 0.17f, 0.12f), 0.1f);
            paint = RuntimeMaterials.Lit("Signpost paint", new Color(0.35f, 0.03f, 0.02f), 0.25f);
        }

        public Material[] Materials => new[] { wood, plank, paint };

        /// <summary>Where the sign for <paramref name="from"/> stands (flat), and which way it points, toward the nearest other lamp.</summary>
        public static bool Place(WorldPlan plan, LampSite from, out Vector2 at, out Vector2 toward)
        {
            at = from.Position;
            toward = Vector2.zero;
            float best = float.MaxValue;
            foreach (var other in plan.Lamps)
            {
                if (other == from) continue;
                Vector2 d = Wrap.Delta(from.Position, other.Position, plan.Size);
                if (d.sqrMagnitude < best) { best = d.sqrMagnitude; toward = d; }
            }
            if (best == float.MaxValue || toward.sqrMagnitude < 0.01f) return false;
            toward.Normalize();
            at = from.Position + toward * 2.4f;
            return true;
        }

        public GameObject Create(Vector3 foot, Vector2 toward, Transform parent, int seed)
        {
            var rng = new System.Random(seed);
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            var face = Quaternion.LookRotation(new Vector3(toward.x, 0f, toward.y));
            // (Old: not quite upright, nor its plank quite level.)
            var lean = Quaternion.Euler(R(-4f, 4f), 0f, R(-4f, 4f));
            if (prefab != null)
            {
                var yours = Object.Instantiate(prefab, parent, false);
                yours.name = "Signpost";
                yours.transform.SetPositionAndRotation(foot, face * lean);
                return yours;
            }
            var root = new GameObject("Signpost") { layer = Layers.Props };
            root.transform.SetParent(parent, false);
            root.transform.SetPositionAndRotation(foot, face * lean);
            var t = root.transform;
            // The post, sunk a little, its top cut at a slant.
            Part(PrimitiveType.Cube, "Post", t, new Vector3(0f, 1.15f, 0f), new Vector3(0.11f, 2.5f, 0.11f), Quaternion.identity, wood, true);
            Part(PrimitiveType.Cube, "Post top", t, new Vector3(0f, 2.4f, 0f), new Vector3(0.112f, 0.08f, 0.13f), Quaternion.Euler(28f, 0f, 0f), wood, false);
            // The plank, nailed to the side of it facing the way it points, and its pointed end.
            float tilt = R(-3f, 3f);
            var board = new GameObject("Board").transform;
            board.SetParent(t, false);
            board.localPosition = new Vector3(0.075f, 2.05f, 0f);
            board.localRotation = Quaternion.Euler(tilt, 0f, 0f);
            Part(PrimitiveType.Cube, "Plank", board, new Vector3(0f, 0f, 0.36f), new Vector3(0.035f, 0.21f, 0.86f), Quaternion.identity, plank, false);
            Part(PrimitiveType.Cube, "Point", board, new Vector3(0f, 0f, 0.79f), new Vector3(0.035f, 0.149f, 0.149f), Quaternion.Euler(45f, 0f, 0f), plank, false);
            // A smear of red on the point: the lamps' colour.
            Part(PrimitiveType.Cube, "Paint", board, new Vector3(0.02f, 0f, 0.7f), new Vector3(0.006f, 0.12f, 0.16f), Quaternion.identity, paint, false);
            // A broken-off second plank lower down, pointing nowhere now.
            Part(PrimitiveType.Cube, "Old plank", t, new Vector3(-0.075f, 1.62f, -0.12f), new Vector3(0.03f, 0.17f, 0.42f), Quaternion.Euler(R(8f, 16f), 0f, 0f), plank, false);
            return root;
        }

        static void Part(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, Quaternion rot, Material mat, bool collider)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.layer = Layers.Props;
            if (!collider) Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = mat;
        }
    }
}
