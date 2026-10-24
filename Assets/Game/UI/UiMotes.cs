using System.Collections.Generic;
using Game.Core;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game.UI
{
    /// <summary>
    /// Menus coming together and apart the way a scour does (section 7): <see cref="Materialize"/> draws a page's
    /// parts in out of drifting motes, one after another down the page, each fading in as its motes arrive;
    /// <see cref="Dissolve"/> breaks them back into motes that lift and blow away. Runs on unscaled time (the
    /// game is paused under the pause menu).
    /// </summary>
    public sealed class UiMotes : MonoBehaviour
    {
        const int Pool = 700;
        const float Arrive = 0.5f, Stagger = 0.07f, FadeIn = 0.3f;

        struct Mote
        {
            public Vector2 From, To, Vel;
            public float Start, Life, Size;
            public bool Out; // blowing away rather than coming together
        }

        struct Reveal
        {
            public VisualElement Part;
            public float Start;
            public bool Spawned, Out;
        }

        VisualElement layer;
        readonly List<VisualElement> dots = new List<VisualElement>();
        readonly Mote[] motes = new Mote[Pool];
        readonly bool[] used = new bool[Pool];
        readonly List<Reveal> reveals = new List<Reveal>();
        float now;
        Vector2 wind = new Vector2(1f, -0.35f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void AttachToGame()
        {
            if (Services.TryGet<GameRoot>(out var root) && root.GetComponent<UiMotes>() == null)
                root.gameObject.AddComponent<UiMotes>();
        }

        void Awake() => Services.Register(this);
        void OnDestroy() => Services.Unregister(this);

        void Start()
        {
            var ui = UiRoot.Get();
            if (ui == null) return;
            layer = UiStyle.Fill(new VisualElement { name = "Motes", pickingMode = PickingMode.Ignore });
            ui.Top.Add(layer);
            for (int i = 0; i < Pool; i++)
            {
                var d = new VisualElement { pickingMode = PickingMode.Ignore };
                d.style.position = Position.Absolute;
                d.style.left = 0;
                d.style.top = 0;
                d.style.width = 4;
                d.style.height = 4;
                d.style.backgroundColor = UiStyle.Ink;
                d.style.display = DisplayStyle.None;
                layer.Add(d);
                dots.Add(d);
            }
        }

        /// <summary>Bring this page's parts in out of motes, top to bottom.</summary>
        public void Materialize(VisualElement page) => Queue(page, false);

        /// <summary>Break this page's parts up into motes that blow away.</summary>
        public void Dissolve(VisualElement page) => Queue(page, true);

        void Queue(VisualElement page, bool away)
        {
            if (page == null) return;
            // (A page coming back in replaces anything still happening to it.)
            reveals.RemoveAll(r => r.Part.parent == page);
            float angle = Random.Range(-25f, 25f) * Mathf.Deg2Rad;
            wind = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) - 0.3f).normalized;
            int i = 0;
            foreach (var part in page.Children())
            {
                if (part.resolvedStyle.display == DisplayStyle.None) continue;
                if (!away) part.style.opacity = 0f;
                reveals.Add(new Reveal { Part = part, Start = now + (away ? 0f : i * Stagger), Out = away });
                i++;
            }
        }

        void Update()
        {
            if (layer == null) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            now += dt;

            for (int i = reveals.Count - 1; i >= 0; i--)
            {
                var r = reveals[i];
                var part = r.Part;
                if (!r.Spawned && now >= r.Start)
                {
                    // (Its place on screen is known once the page has been laid out: wait for that.)
                    var box = part.worldBound;
                    if (box.width < 1f || float.IsNaN(box.width))
                    {
                        if (now - r.Start > 0.5f) { part.style.opacity = 1f; reveals.RemoveAt(i); }
                        continue;
                    }
                    Spawn(box, r.Out);
                    r.Spawned = true;
                    if (r.Out) part.style.opacity = 0f;
                    reveals[i] = r;
                }
                if (r.Out)
                {
                    if (r.Spawned) reveals.RemoveAt(i);
                    continue;
                }
                float k = Mathf.Clamp01((now - r.Start - (Arrive - FadeIn * 0.5f)) / FadeIn);
                part.style.opacity = k * k * (3f - 2f * k);
                if (k >= 1f) reveals.RemoveAt(i);
            }

            for (int i = 0; i < Pool; i++)
            {
                if (!used[i]) continue;
                ref var m = ref motes[i];
                float t = (now - m.Start) / m.Life;
                var d = dots[i];
                if (t >= 1f)
                {
                    used[i] = false;
                    d.style.display = DisplayStyle.None;
                    continue;
                }
                if (t < 0f) continue;
                Vector2 p;
                float alpha, size;
                if (!m.Out)
                {
                    // Drifting in on the air, slowing as it settles into place, then gone into the shape.
                    float e = 1f - (1f - t) * (1f - t) * (1f - t);
                    Vector2 curl = new Vector2(Mathf.Sin(t * 5f + i), Mathf.Cos(t * 4f + i * 1.3f)) * (1f - e) * 14f;
                    p = Vector2.Lerp(m.From, m.To, e) + curl;
                    alpha = Mathf.Clamp01(t * 4f) * (1f - Mathf.Clamp01((t - 0.75f) * 4f));
                    size = m.Size * Mathf.Lerp(1.4f, 0.6f, t);
                }
                else
                {
                    // Lifting off and blowing away, shrinking (as a dismissed scour does).
                    m.Vel += (wind * 260f + new Vector2(Mathf.Sin(now * 3f + i) * 60f, -40f)) * dt;
                    m.Vel *= 1f - dt * 1.2f;
                    m.From += m.Vel * dt;
                    p = m.From;
                    alpha = 1f - t;
                    size = m.Size * (1f - t * t);
                }
                if (d.style.display != DisplayStyle.Flex) d.style.display = DisplayStyle.Flex;
                Vector2 local = layer.WorldToLocal(p);
                d.style.translate = new Translate(local.x - size * 0.5f, local.y - size * 0.5f);
                d.style.width = size;
                d.style.height = size;
                d.style.opacity = alpha * 0.85f;
            }
        }

        // Motes for one part: as many as its size calls for, spread over its box.
        void Spawn(Rect box, bool away)
        {
            int count = Mathf.Clamp(Mathf.RoundToInt(box.width * box.height / 260f), 14, 160);
            for (int n = 0; n < count; n++)
            {
                int i = Free();
                if (i < 0) return;
                Vector2 to = new Vector2(Random.Range(box.xMin, box.xMax), Random.Range(box.yMin, box.yMax));
                float delay = Random.Range(0f, 0.12f);
                if (!away)
                {
                    // From upwind and scattered, as if blown together.
                    Vector2 from = to - wind * Random.Range(80f, 320f) + Random.insideUnitCircle * 90f;
                    motes[i] = new Mote { From = from, To = to, Start = now + delay, Life = Arrive - delay * 0.5f, Size = Random.Range(2f, 5f) };
                }
                else
                {
                    motes[i] = new Mote { From = to, Vel = Random.insideUnitCircle * 60f + Vector2.down * Random.Range(20f, 80f), Start = now + delay * 0.5f, Life = Random.Range(0.45f, 0.8f), Size = Random.Range(2f, 5f), Out = true };
                }
                used[i] = true;
            }
        }

        int Free()
        {
            for (int i = 0; i < Pool; i++) if (!used[i]) return i;
            return -1;
        }
    }
}
