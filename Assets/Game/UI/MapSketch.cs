using Game.World.Generation;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// The map (section 6.3): a ranger's pencil sketch of the forest on old paper, drawn from the world plan. The
    /// trails between the lamps, the lamps as rings (inked in once lit), ponds, the buildings as little symbols.
    /// No "you are here", and not the lone door. The forest wraps, so it's one tile with the start in the middle,
    /// and lines run off one edge and on at the other.
    /// </summary>
    public sealed class MapSketch
    {
        public const int Size = 1024;

        static readonly Color32 Graphite = new Color32(52, 46, 40, 255);
        static readonly Color32 Ink = new Color32(28, 20, 18, 255);
        static readonly Color32 Water = new Color32(70, 84, 92, 255);

        readonly WorldPlan plan;
        readonly Color32[] pixels = new Color32[Size * Size];
        public Texture2D Texture { get; }

        public MapSketch(WorldPlan plan)
        {
            this.plan = plan;
            Texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { name = "Map", wrapMode = TextureWrapMode.Clamp };
            Paper();
            foreach (var pond in plan.Ponds) Ring(At(pond.Centre), pond.Radius * Scale, 1.3f, Water, 0.55f, 40 + (int)pond.Radius);
            foreach (var t in plan.Trails) Trail(t);
            foreach (var s in plan.Structures) Symbol(s);
            foreach (var l in plan.Lamps) Ring(At(l.Position), 9f, 1.6f, Graphite, 0.85f, l.Index);
            Apply();
        }

        float Scale => Size / Mathf.Max(1f, plan.Size); // (one whole tile, so lines wrap where the forest does)

        /// <summary>A lamp, inked in: lit.</summary>
        public void InkLamp(int index)
        {
            foreach (var l in plan.Lamps)
            {
                if (l.Index != index) continue;
                Vector2 c = At(l.Position);
                for (float r = 0.5f; r <= 8.5f; r += 0.9f) Ring(c, r, 1.4f, Ink, 0.9f, index * 7 + (int)(r * 3f));
                Ring(c, 11.5f, 1.2f, Ink, 0.7f, index * 13); // (a second, hurried circle round it)
            }
            Apply();
        }

        public void Apply()
        {
            Texture.SetPixels32(pixels);
            Texture.Apply(false);
        }

        // A plan position on the paper (pixels): the start in the middle.
        Vector2 At(Vector2 p)
        {
            Vector2 d = Wrap.Delta(plan.Start, p, plan.Size);
            return new Vector2(Size * 0.5f, Size * 0.5f) + d * Scale;
        }

        void Paper()
        {
            for (int y = 0; y < Size; y++)
            for (int x = 0; x < Size; x++)
            {
                float u = x / (float)Size, v = y / (float)Size;
                float blotch = Mathf.PerlinNoise(u * 4f + 3.1f, v * 4f + 7.7f) * 0.6f + Mathf.PerlinNoise(u * 19f, v * 19f) * 0.25f;
                float grain = Mathf.PerlinNoise(u * 230f, v * 230f) * 0.12f;
                float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));
                float age = 1f - 0.35f * Mathf.Clamp01(1f - edge * 9f); // darker toward the edges
                // Two folds, where it was kept in a pocket.
                float fold = Mathf.Abs(u - 0.5f) < 0.0015f || Mathf.Abs(v - 0.5f) < 0.0015f ? 0.9f : 1f;
                float k = (0.82f + blotch * 0.22f - grain) * age * fold;
                pixels[y * Size + x] = new Color32((byte)Mathf.Clamp(222 * k, 0, 255), (byte)Mathf.Clamp(204 * k, 0, 255), (byte)Mathf.Clamp(164 * k, 0, 255), 255);
            }
        }

        void Trail(Trail t)
        {
            if (t.Points.Count < 2) return;
            Vector2 prev = At(t.Points[0]);
            float along = 0f;
            for (int i = 1; i < t.Points.Count; i++)
            {
                // (Step by the wrapped difference, so a trail crossing the edge runs off and on rather than across.)
                Vector2 next = prev + Wrap.Delta(t.Points[i - 1], t.Points[i], plan.Size) * Scale;
                bool worn = i >= t.Worn.Count || t.Worn[i];
                Line(prev, next, worn ? 1.5f : 1f, Graphite, worn ? 0.7f : 0.35f, ref along);
                prev = next;
            }
        }

        void Symbol(StructureSite s)
        {
            Vector2 c = At(s.Position);
            float a = -s.Yaw * Mathf.Deg2Rad;
            Vector2 ax = new Vector2(Mathf.Cos(a), Mathf.Sin(a)), ay = new Vector2(-ax.y, ax.x);
            float along = 0f;
            void L(float x0, float y0, float x1, float y1) => Line(c + ax * x0 + ay * y0, c + ax * x1 + ay * y1, 1.4f, Graphite, 0.85f, ref along);
            switch (s.Kind)
            {
                case StructureKind.Shack:
                case StructureKind.Cabin:
                    float w = s.Kind == StructureKind.Cabin ? 9f : 6f;
                    L(-w, -w, w, -w); L(w, -w, w, w); L(w, w, -w, w); L(-w, w, -w, -w);
                    L(-w, w, 0f, w * 1.8f); L(0f, w * 1.8f, w, w); // (a roof)
                    break;
                case StructureKind.Campsite:
                    L(-7f, -5f, 7f, -5f); L(-7f, -5f, 0f, 7f); L(7f, -5f, 0f, 7f);
                    break;
                case StructureKind.Watchtower:
                    L(-6f, -6f, 6f, 6f); L(-6f, 6f, 6f, -6f); L(-6f, -6f, 6f, -6f); L(-6f, 6f, 6f, 6f);
                    break;
                case StructureKind.Car:
                    L(-4f, -8f, 4f, -8f); L(4f, -8f, 4f, 8f); L(4f, 8f, -4f, 8f); L(-4f, 8f, -4f, -8f);
                    break;
                case StructureKind.Bunker:
                    L(-6f, -4f, 6f, -4f); L(-6f, -4f, -6f, 3f); L(6f, -4f, 6f, 3f); L(-6f, 3f, 6f, 3f);
                    L(-2f, 3f, -2f, -1f); L(2f, 3f, 2f, -1f); L(-2f, -1f, 2f, -1f); // (its doorway)
                    break;
                case StructureKind.LandmarkTree:
                    L(0f, -8f, 0f, 10f); L(-7f, 0f, 0f, 10f); L(7f, 0f, 0f, 10f); L(-5f, -3f, 0f, 4f); L(5f, -3f, 0f, 4f);
                    break;
            }
        }

        // A hand-drawn ring: it wobbles, and doesn't quite meet itself.
        void Ring(Vector2 c, float radius, float width, Color32 colour, float strength, int seed)
        {
            int steps = Mathf.Max(16, Mathf.CeilToInt(radius * 6f));
            float start = seed * 1.37f, overshoot = 0.15f + (seed % 5) * 0.03f;
            Vector2 prev = Vector2.zero;
            float along = 0f;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                float ang = start + t * (Mathf.PI * 2f + overshoot);
                float r = radius * (1f + 0.08f * (Mathf.PerlinNoise(seed * 0.71f, t * 3f) - 0.5f));
                Vector2 p = c + new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)) * r;
                if (i > 0) Line(prev, p, width, colour, strength, ref along);
                prev = p;
            }
        }

        // A pencil line: a soft round point stepped along it, its pressure wavering.
        void Line(Vector2 a, Vector2 b, float width, Color32 colour, float strength, ref float along)
        {
            float len = Vector2.Distance(a, b);
            int steps = Mathf.Max(1, Mathf.CeilToInt(len / 0.6f));
            for (int i = 0; i <= steps; i++)
            {
                Vector2 p = Vector2.Lerp(a, b, i / (float)steps);
                float pressure = 0.65f + 0.35f * Mathf.PerlinNoise(along * 0.05f, 4.2f);
                Dab(p, width, colour, strength * pressure);
                along += len / steps;
            }
        }

        void Dab(Vector2 p, float radius, Color32 colour, float strength)
        {
            int r = Mathf.CeilToInt(radius + 1f);
            int cx = Mathf.RoundToInt(p.x), cy = Mathf.RoundToInt(p.y);
            for (int dy = -r; dy <= r; dy++)
            for (int dx = -r; dx <= r; dx++)
            {
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float cover = Mathf.Clamp01(radius + 0.5f - d) * strength * 0.35f; // (soft: dabs build up)
                if (cover <= 0f) continue;
                int x = ((cx + dx) % Size + Size) % Size, y = ((cy + dy) % Size + Size) % Size; // (wraps)
                int i = y * Size + x;
                var o = pixels[i];
                pixels[i] = new Color32((byte)(o.r + (colour.r - o.r) * cover), (byte)(o.g + (colour.g - o.g) * cover), (byte)(o.b + (colour.b - o.b) * cover), 255);
            }
        }
    }
}
