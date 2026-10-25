using System.Collections.Generic;
using Game.Audio;
using Game.Core;
using Game.Player;
using Game.Rendering;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering.HighDefinition;

namespace Game.World
{
    /// <summary>
    /// Snake, the game itself (no Unity in it): a grid, the snake, one piece of food. Each step the snake moves a
    /// cell the way it's heading; eating the food grows it and puts new food somewhere empty; running into a wall
    /// or itself ends it. Left alone it can play itself (the attract mode): heading for the food, never into
    /// anything if it can help it.
    /// </summary>
    public sealed class SnakeGame
    {
        public readonly int Width, Height;
        public readonly List<Vector2Int> Body = new List<Vector2Int>(); // (head first)
        public Vector2Int Food { get; private set; }
        public Vector2Int Heading { get; private set; } = Vector2Int.right;
        public bool Over { get; private set; }
        public int Score { get; private set; }
        public bool AteLastStep { get; private set; }

        readonly System.Random rng;
        Vector2Int wanted = Vector2Int.right;

        public SnakeGame(int width, int height, int seed)
        {
            Width = width;
            Height = height;
            rng = new System.Random(seed);
            Reset();
        }

        public void Reset()
        {
            Body.Clear();
            var mid = new Vector2Int(Width / 3, Height / 2);
            for (int i = 0; i < 4; i++) Body.Add(mid - new Vector2Int(i, 0));
            Heading = wanted = Vector2Int.right;
            Over = false;
            Score = 0;
            PlaceFood();
        }

        /// <summary>Turn (the next step): never straight back on itself.</summary>
        public void Steer(Vector2Int dir)
        {
            if (dir == Vector2Int.zero || dir == -Heading) return;
            wanted = dir;
        }

        public void Step()
        {
            AteLastStep = false;
            if (Over) return;
            Heading = wanted;
            var head = Body[0] + Heading;
            bool eating = head == Food;
            // (the tail moves out of the way this step unless it's growing)
            if (Blocked(head, eating ? 0 : 1))
            {
                Over = true;
                return;
            }
            Body.Insert(0, head);
            if (eating)
            {
                Score++;
                AteLastStep = true;
                PlaceFood();
            }
            else Body.RemoveAt(Body.Count - 1);
        }

        bool Blocked(Vector2Int c, int tailFree)
        {
            if (c.x < 0 || c.y < 0 || c.x >= Width || c.y >= Height) return true;
            for (int i = 0; i < Body.Count - tailFree; i++) if (Body[i] == c) return true;
            return false;
        }

        void PlaceFood()
        {
            for (int tries = 0; tries < 500; tries++)
            {
                var c = new Vector2Int(rng.Next(Width), rng.Next(Height));
                if (!Body.Contains(c)) { Food = c; return; }
            }
            Food = new Vector2Int(-1, -1); // (no room left: it's won, more or less)
        }

        /// <summary>Playing itself: the safe turn that best closes on the food.</summary>
        public void Think()
        {
            Vector2Int best = Heading;
            float bestScore = float.MinValue;
            foreach (var d in new[] { Heading, new Vector2Int(-Heading.y, Heading.x), new Vector2Int(Heading.y, -Heading.x) })
            {
                var c = Body[0] + d;
                if (Blocked(c, 1)) continue;
                float score = -Mathf.Abs(c.x - Food.x) - Mathf.Abs(c.y - Food.y) + (d == Heading ? 0.3f : 0f);
                // (a little look ahead: not into a pocket with no way out)
                int free = 0;
                foreach (var e in new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right })
                    if (!Blocked(c + e, 1) && c + e != Body[0]) free++;
                if (free == 0) score -= 100f;
                if (score > bestScore) { bestScore = score; best = d; }
            }
            Steer(best);
        }
    }

    /// <summary>
    /// An arcade cabinet playing Snake (in the watchtower's lookout room, of all places). Its screen glows green in
    /// the dark, the game playing itself; click it to step up and play: arrows or WASD steer, Space starts again
    /// after a game over, E steps away. It remembers the best score.
    /// </summary>
    public sealed class ArcadeCabinet : MonoBehaviour, IInteractable, IDoorwaySeat
    {
        const int GridW = 20, GridH = 13, Cell = 3;
        const int TexW = 64, TexH = 48;
        static int best;

        [Tooltip("The screen (its emissive map is the game).")]
        public Renderer screen;
        [Tooltip("Where you stand to play, facing the screen.")]
        public Transform standAt;
        [Tooltip("The middle of the screen (where your eyes go as you step up).")]
        public Transform screenCentre;

        SnakeGame game;
        Texture2D tex;
        Color32[] px;
        Material mat;
        Light glow;
        bool playing, attract = true;
        float nextStep, overAt;

        public string Prompt => "Play Snake";
        public bool CanInteract => !playing;
        public Vector3 FocusPoint => screenCentre != null ? screenCentre.position : transform.position + Vector3.up * 1.3f;

        public Transform SeatPoint => standAt;
        public Vector3 ExitPoint => standAt != null ? standAt.position : transform.position + transform.forward;
        public float EyeHeight => 1.55f;
        public bool CanLeave => true;
        public string CantLeave => "";
        public bool CanSit(Vector3 from) => false; // (a click starts it, not Y)

        void Start()
        {
            game = new SnakeGame(GridW, GridH, Random.Range(0, 100000));
            tex = new Texture2D(TexW, TexH, TextureFormat.RGBA32, false) { name = "Snake screen", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
            px = new Color32[TexW * TexH];
            if (screen != null)
            {
                mat = RuntimeMaterials.Lit("Arcade screen", Color.black, 0.9f);
                mat.SetTexture("_EmissiveColorMap", tex);
                HDMaterial.SetEmissiveColor(mat, Color.white * 2.2f);
                HDMaterial.ValidateMaterial(mat);
                screen.sharedMaterial = mat;
                var go = new GameObject("Screen glow");
                go.transform.SetParent(screen.transform.parent, false);
                go.transform.position = screen.transform.position + transform.forward * 0.35f;
                glow = go.AddHDLight(LightType.Point).GetComponent<Light>();
                glow.color = new Color(0.35f, 1f, 0.5f);
                glow.range = 3.5f;
                glow.intensity = 0.5f;
                glow.shadows = LightShadows.None;
            }
            Draw();
        }

        void OnDestroy()
        {
            if (tex != null) Destroy(tex);
            if (mat != null) Destroy(mat);
        }

        public void Interact()
        {
            if (playing || !Services.TryGet<PlayerController>(out var player)) return;
            player.SitIn(this, "Arrows or WASD to steer   ·   E to step away");
            if (screenCentre != null) player.DrawGazeTo(screenCentre.position, 0.8f);
        }

        public void Sat(bool now)
        {
            playing = now;
            attract = !now;
            game.Reset();
            nextStep = Time.time + (now ? 0.8f : 0.2f);
            Beep(now ? 1.2f : 0.6f);
        }

        void Update()
        {
            if (game == null) return;
            if (playing) ReadKeys();
            if (Time.time < nextStep) return;
            if (game.Over)
            {
                // Game over: a moment, then (playing itself) round again; (you) Space or a direction to go again.
                if (attract && Time.time - overAt > 2.5f) { game.Reset(); Draw(); }
                nextStep = Time.time + 0.1f;
                return;
            }
            if (attract) game.Think();
            game.Step();
            if (game.AteLastStep)
            {
                if (playing) Beep(2.4f);
                if (!attract && game.Score > best) best = game.Score;
            }
            if (game.Over)
            {
                overAt = Time.time;
                if (playing) Beep(0.4f);
            }
            float speed = Mathf.Lerp(0.14f, 0.065f, Mathf.Clamp01(game.Score / 30f));
            nextStep = Time.time + (attract ? 0.11f : speed);
            Draw();
        }

        void ReadKeys()
        {
            var k = Keyboard.current;
            if (k == null) return;
            Vector2Int dir = Vector2Int.zero;
            if (k.upArrowKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame) dir = Vector2Int.up;
            else if (k.downArrowKey.wasPressedThisFrame || k.sKey.wasPressedThisFrame) dir = Vector2Int.down;
            else if (k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame) dir = Vector2Int.left;
            else if (k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame) dir = Vector2Int.right;
            if (game.Over)
            {
                if ((k.spaceKey.wasPressedThisFrame || dir != Vector2Int.zero) && Time.time - overAt > 0.8f)
                {
                    game.Reset();
                    nextStep = Time.time + 0.5f;
                    Beep(1.2f);
                    Draw();
                }
                return;
            }
            game.Steer(dir);
        }

        void Beep(float pitch)
        {
            if (Services.TryGet<SoundPlayer>(out var sounds))
                sounds.Play(Sound.Click, transform.position + Vector3.up * 1.2f, MixerNames.World, 0.5f, pitch, 8f);
        }

        // ------------------------------------------------------------------ the screen

        static readonly Color32 Black = new Color32(2, 6, 3, 255), Dim = new Color32(10, 60, 25, 255),
            Green = new Color32(60, 255, 110, 255), Pale = new Color32(170, 255, 190, 255), Red = new Color32(255, 50, 30, 255);

        void Draw()
        {
            if (tex == null) return;
            for (int i = 0; i < px.Length; i++) px[i] = Black;
            // The field (under a strip at the top for the score), its border.
            int fx = (TexW - GridW * Cell) / 2, fy = 1;
            Rect(fx - 1, fy - 1, GridW * Cell + 2, 1, Dim);
            Rect(fx - 1, fy + GridH * Cell, GridW * Cell + 2, 1, Dim);
            Rect(fx - 1, fy - 1, 1, GridH * Cell + 2, Dim);
            Rect(fx + GridW * Cell, fy - 1, 1, GridH * Cell + 2, Dim);
            for (int i = game.Body.Count - 1; i >= 0; i--)
            {
                var c = game.Body[i];
                Rect(fx + c.x * Cell, fy + c.y * Cell, Cell - (i == 0 ? 0 : 1), Cell - (i == 0 ? 0 : 1), i == 0 ? Pale : Green);
            }
            if (game.Food.x >= 0) Rect(fx + game.Food.x * Cell, fy + game.Food.y * Cell, Cell - 1, Cell - 1, Red);
            // The strip: the score and the best.
            int top = TexH - 6;
            Text(attract ? "SNAKE" : game.Score.ToString(), 2, top, Green);
            Text("HI " + best, TexW - 2 - Width("HI " + best), top, Dim);
            if (game.Over && !attract)
            {
                Rect(fx + 6, fy + 12, GridW * Cell - 12, 15, Black);
                Text("GAME OVER", (TexW - Width("GAME OVER")) / 2, fy + 21, Red);
                Text("SPACE", (TexW - Width("SPACE")) / 2, fy + 14, Green);
            }
            else if (attract && (int)(Time.time * 1.5f) % 2 == 0)
                Text("CLICK TO PLAY", (TexW - Width("CLICK TO PLAY")) / 2, fy + 18, Pale);
            tex.SetPixels32(px);
            tex.Apply(false);
        }

        void Rect(int x, int y, int w, int h, Color32 c)
        {
            for (int j = y; j < y + h; j++)
            for (int i = x; i < x + w; i++)
                if (i >= 0 && j >= 0 && i < TexW && j < TexH) px[j * TexW + i] = c;
        }

        // A 3x5 font (each glyph five rows, top first, three bits a row).
        static readonly Dictionary<char, int[]> font = new Dictionary<char, int[]>
        {
            ['0'] = new[] { 7, 5, 5, 5, 7 }, ['1'] = new[] { 2, 6, 2, 2, 7 }, ['2'] = new[] { 7, 1, 7, 4, 7 }, ['3'] = new[] { 7, 1, 7, 1, 7 },
            ['4'] = new[] { 5, 5, 7, 1, 1 }, ['5'] = new[] { 7, 4, 7, 1, 7 }, ['6'] = new[] { 7, 4, 7, 5, 7 }, ['7'] = new[] { 7, 1, 2, 2, 2 },
            ['8'] = new[] { 7, 5, 7, 5, 7 }, ['9'] = new[] { 7, 5, 7, 1, 7 },
            ['A'] = new[] { 2, 5, 7, 5, 5 }, ['C'] = new[] { 7, 4, 4, 4, 7 }, ['E'] = new[] { 7, 4, 6, 4, 7 }, ['G'] = new[] { 7, 4, 5, 5, 7 },
            ['H'] = new[] { 5, 5, 7, 5, 5 }, ['I'] = new[] { 7, 2, 2, 2, 7 }, ['K'] = new[] { 5, 5, 6, 5, 5 }, ['L'] = new[] { 4, 4, 4, 4, 7 },
            ['M'] = new[] { 5, 7, 7, 5, 5 }, ['N'] = new[] { 6, 5, 5, 5, 5 }, ['O'] = new[] { 7, 5, 5, 5, 7 }, ['P'] = new[] { 7, 5, 7, 4, 4 },
            ['R'] = new[] { 7, 5, 6, 5, 5 }, ['S'] = new[] { 7, 4, 7, 1, 7 }, ['T'] = new[] { 7, 2, 2, 2, 2 }, ['V'] = new[] { 5, 5, 5, 5, 2 },
            ['Y'] = new[] { 5, 5, 2, 2, 2 }, [' '] = new[] { 0, 0, 0, 0, 0 },
        };

        static int Width(string s) => s.Length * 4 - 1;

        void Text(string s, int x, int y, Color32 c)
        {
            foreach (char ch in s)
            {
                if (font.TryGetValue(ch, out var rows))
                    for (int r = 0; r < 5; r++)
                    for (int b = 0; b < 3; b++)
                        if ((rows[r] & (4 >> b)) != 0) Rect(x + b, y + 4 - r, 1, 1, c);
                x += 4;
            }
        }
    }
}
