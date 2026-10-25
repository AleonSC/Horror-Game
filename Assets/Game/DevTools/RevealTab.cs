using System.Collections.Generic;
using Game.Core;
using Game.Entities;
using Game.Player;
using Game.World;
using Game.World.Build;
using Game.World.Generation;
using UnityEngine;

namespace Game.DevTools
{
    /// <summary>
    /// The Reveal tab: tick what to show and it's marked on the screen wherever it is, through trees and walls,
    /// with its name and how far off it is (off the screen, a marker at the edge points the way). The marks stay
    /// with the window shut.
    /// </summary>
    sealed class RevealTab : IDevTab
    {
        public string Title => "Reveal";

        public void Draw()
        {
            GUILayout.Label("Tick what to mark. Marks show through everything, with how far off each one is, and stay with this window shut.", DevStyle.MutedLabel);
            var cats = Reveal.Categories;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("All", DevStyle.Button)) foreach (var c in cats) c.On = true;
            if (GUILayout.Button("None", DevStyle.Button)) foreach (var c in cats) c.On = false;
            GUILayout.EndHorizontal();
            string group = null;
            foreach (var c in cats)
            {
                if (c.Group != group)
                {
                    group = c.Group;
                    GUILayout.Label(group, DevStyle.Section);
                }
                c.On = DevStyle.Switch($"{c.Name}   ({c.Count} found)", c.On);
                if (c.On) List(c);
            }
            Reveal.EnsureOverlay();
        }

        // Each one found, nearest first, with a button to go there.
        static void List(Reveal.Category c)
        {
            if (!Services.TryGet<PlayerController>(out var player) || c.Found.Count == 0) return;
            Vector3 me = player.transform.position;
            var found = new List<(Transform At, string Label, float Lift)>(c.Found);
            found.RemoveAll(f => f.At == null);
            found.Sort((a, b) => (a.At.position - me).sqrMagnitude.CompareTo((b.At.position - me).sqrMagnitude));
            foreach (var f in found)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Space(24f);
                GUILayout.Label($"{f.Label}   {Vector3.Distance(me, f.At.position):0} m", DevStyle.MutedLabel);
                if (GUILayout.Button("Go", DevStyle.Button, GUILayout.Width(70f))) Reveal.TeleportTo(player, f.At.position);
                GUILayout.EndHorizontal();
            }
        }
    }

    /// <summary>What can be revealed, and the overlay that marks it.</summary>
    sealed class Reveal : MonoBehaviour
    {
        public sealed class Category
        {
            public string Group, Name;
            public Color Colour;
            public bool On;
            public int Count;
            public readonly List<(Transform At, string Label, float Lift)> Found = new List<(Transform, string, float)>();
            public System.Action<Category> Gather;
        }

        static Reveal overlay;
        float nextGather;
        static Texture2D dot;

        public static readonly Category[] Categories =
        {
            Items("Batteries", new Color(0.4f, 1f, 0.45f), k => k == ItemKind.Batteries),
            Items("Torches and lighters", new Color(1f, 0.6f, 0.2f), k => k == ItemKind.Torch || k == ItemKind.Lighter),
            Items("Flare guns and glowsticks", new Color(1f, 0.3f, 0.3f), k => k == ItemKind.FlareGun || k == ItemKind.Glowsticks),
            Items("Other items (camera, map, radio, key fob, snacks)", new Color(0.9f, 0.9f, 0.6f), k => k != ItemKind.Batteries && k != ItemKind.Torch && k != ItemKind.Lighter && k != ItemKind.FlareGun && k != ItemKind.Glowsticks),
            Place("Camps", new Color(1f, 0.55f, 0.25f), StructureKind.Campsite),
            Place("Shacks", new Color(0.8f, 0.7f, 0.5f), StructureKind.Shack),
            Place("Cabin", new Color(0.95f, 0.8f, 0.55f), StructureKind.Cabin),
            Place("Car", new Color(0.6f, 0.8f, 1f), StructureKind.Car),
            Place("Bunker", new Color(0.7f, 0.75f, 0.7f), StructureKind.Bunker),
            Place("Watchtower", new Color(0.85f, 0.85f, 1f), StructureKind.Watchtower),
            Place("Landmark tree", new Color(0.6f, 0.9f, 0.6f), StructureKind.LandmarkTree),
            new Category
            {
                Group = "Places", Name = "Lamps", Colour = new Color(1f, 0.85f, 0.4f),
                Gather = c =>
                {
                    foreach (var l in Object.FindObjectsByType<Lamp>(FindObjectsSortMode.None)) c.Found.Add((l.transform, l.Lit ? "Lamp (lit)" : "Lamp", 3.5f));
                },
            },
            new Category
            {
                Group = "Places", Name = "The lone door", Colour = new Color(0.8f, 0.5f, 1f),
                Gather = c =>
                {
                    foreach (var d in Object.FindObjectsByType<LoneDoor>(FindObjectsSortMode.None)) c.Found.Add((d.transform, "The door", 2.5f));
                },
            },
            Hunter<Scour>("Scours", new Color(1f, 0.15f, 0.15f), "Scour"),
            Hunter<BlindOne>("The blind one", new Color(1f, 0.4f, 0.7f), "Blind one"),
            Hunter<Crawler>("Crawlers", new Color(1f, 0.5f, 0.1f), "Crawler"),
            Hunter<Flier>("The flier", new Color(0.7f, 0.4f, 1f), "Flier"),
        };

        static Category Items(string name, Color colour, System.Func<ItemKind, bool> which) => new Category
        {
            Group = "Items", Name = name, Colour = colour,
            Gather = c =>
            {
                foreach (var p in Object.FindObjectsByType<Pickup>(FindObjectsSortMode.None))
                    if (p.isActiveAndEnabled && which(p.kind)) c.Found.Add((p.transform, p.kind.ToString(), 0.3f));
            },
        };

        static Category Place(string name, Color colour, StructureKind kind) => new Category
        {
            Group = "Places", Name = name, Colour = colour,
            Gather = c =>
            {
                if (!Services.TryGet<WorldBuilder>(out var world)) return;
                foreach (var (k, t) in world.Places) if (k == kind && t != null) c.Found.Add((t, name.EndsWith("s") ? name.Substring(0, name.Length - 1) : name, 3f)); // ("Camps": each a "Camp")
            },
        };

        static Category Hunter<T>(string name, Color colour, string label) where T : MonoBehaviour => new Category
        {
            Group = "Hunters", Name = name, Colour = colour,
            Gather = c =>
            {
                foreach (var h in Object.FindObjectsByType<T>(FindObjectsSortMode.None)) if (h.isActiveAndEnabled) c.Found.Add((h.transform, label, 2f));
            },
        };

        /// <summary>
        /// Put the player a couple of metres short of a point, on the ground, facing it (out of any seat first).
        /// From the side nearest where they are now, so they don't land inside the thing.
        /// </summary>
        public static void TeleportTo(PlayerController player, Vector3 target)
        {
            if (player.Seat != null) player.LeaveSeat();
            Vector3 from = player.transform.position - target;
            from.y = 0f;
            if (from.sqrMagnitude < 0.01f) from = Vector3.back;
            from.Normalize();
            for (int i = 0; i < 8; i++)
            {
                // Round the point till there's room to stand (not in a wall, a tree or a rock).
                Vector3 spot = target + Quaternion.Euler(0f, i * 45f, 0f) * from * 2.5f;
                if (Physics.Raycast(spot + Vector3.up * 30f, Vector3.down, out var hit, 80f, Layers.Mask(Layers.Ground), QueryTriggerInteraction.Ignore)) spot.y = hit.point.y + 0.05f;
                if (Physics.CheckCapsule(spot + Vector3.up * 0.4f, spot + Vector3.up * 1.6f, 0.3f, Layers.Mask(Layers.Trees) | Layers.Mask(Layers.Props), QueryTriggerInteraction.Ignore) && i < 7) continue;
                Vector3 look = target - spot;
                look.y = 0f;
                player.Teleport(spot, Quaternion.LookRotation(look.sqrMagnitude > 0.01f ? look : Vector3.forward));
                return;
            }
        }

        /// <summary>Make sure the overlay's running (it lives on, drawing, with the window shut).</summary>
        public static void EnsureOverlay()
        {
            if (overlay != null) return;
            var go = new GameObject("Dev reveal");
            DontDestroyOnLoad(go);
            overlay = go.AddComponent<Reveal>();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetOnPlay()
        {
            overlay = null;
            foreach (var c in Categories) { c.On = false; c.Found.Clear(); c.Count = 0; }
        }

        void Update()
        {
            // Looking everything up again twice a second (things are picked up, hunters come and go).
            if (Time.unscaledTime < nextGather) return;
            nextGather = Time.unscaledTime + 0.5f;
            foreach (var c in Categories)
            {
                c.Found.Clear();
                c.Gather(c);
                c.Count = c.Found.Count;
            }
        }

        void OnGUI()
        {
            if (Event.current.type != EventType.Repaint) return;
            var cam = Camera.main;
            if (cam == null) return;
            if (dot == null)
            {
                dot = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
                dot.SetPixel(0, 0, Color.white);
                dot.Apply();
            }
            DevStyle.Ensure();
            var style = new GUIStyle(DevStyle.Label) { fontSize = 13, wordWrap = false, alignment = TextAnchor.UpperCenter };
            Vector3 eye = cam.transform.position;
            foreach (var c in Categories)
            {
                if (!c.On) continue;
                foreach (var (at, label, lift) in c.Found)
                {
                    if (at == null) continue;
                    Vector3 world = at.position + Vector3.up * lift;
                    Mark(cam, world, c.Colour, $"{label}  {Vector3.Distance(eye, world):0} m", style);
                }
            }
        }

        // A diamond on the thing with its name and distance under it; off the screen (or behind you), an arrow
        // at the edge in its direction.
        static void Mark(Camera cam, Vector3 world, Color colour, string text, GUIStyle style)
        {
            Vector3 v = cam.WorldToScreenPoint(world);
            bool behind = v.z < 0f;
            if (behind) v = -v;
            Vector2 p = new Vector2(v.x, Screen.height - v.y);
            const float edge = 24f;
            bool off = behind || p.x < edge || p.y < edge || p.x > Screen.width - edge || p.y > Screen.height - edge;
            var old = GUI.color;
            if (off)
            {
                Vector2 centre = new Vector2(Screen.width, Screen.height) * 0.5f;
                Vector2 dir = (p - centre);
                if (dir.sqrMagnitude < 1f) dir = Vector2.up;
                float scale = Mathf.Min((Screen.width * 0.5f - edge) / Mathf.Max(0.001f, Mathf.Abs(dir.x)), (Screen.height * 0.5f - edge) / Mathf.Max(0.001f, Mathf.Abs(dir.y)));
                p = centre + dir * Mathf.Min(1f, scale);
                GUI.color = new Color(colour.r, colour.g, colour.b, 0.75f);
                Diamond(p, 7f);
                GUI.color = old;
                return;
            }
            GUI.color = Color.black;
            Diamond(p, 9f);
            GUI.color = colour;
            Diamond(p, 7f);
            var size = style.CalcSize(new GUIContent(text));
            var box = new Rect(p.x - size.x * 0.5f, p.y + 10f, size.x, size.y);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(new Rect(box.x - 3f, box.y, box.width + 6f, box.height), dot);
            GUI.color = colour;
            GUI.Label(box, text, style);
            GUI.color = old;
        }

        static void Diamond(Vector2 p, float r)
        {
            var m = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, p);
            GUI.DrawTexture(new Rect(p.x - r * 0.7f, p.y - r * 0.7f, r * 1.4f, r * 1.4f), dot);
            GUI.matrix = m;
        }
    }
}
