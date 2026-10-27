using System;
using System.Globalization;
using System.Net;
using System.Text;

namespace Game.DevTools
{
    /// <summary>
    /// A run's timeline as one HTML page to open in a browser: the numbers that say how it was paced, a pressure
    /// curve over the run, a lane for each threat and for the player (colour by state, hover for what it was),
    /// the lamps as lines down through it all with the scares, appearances and catches marked, and a table of the
    /// run lamp by lamp. Self-contained (no scripts, no downloads). Plain C# so it can be tested.
    /// </summary>
    public static class TimelineReport
    {
        const float Width = 1180f, Left = 150f, Right = 20f, LaneH = 18f, LaneGap = 6f;
        static float PlotW => Width - Left - Right;

        public static string ToHtml(SessionTimeline timeline, string title)
        {
            var s = timeline.Analyse();
            var sb = new StringBuilder();
            sb.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">");
            sb.Append("<title>").Append(Esc(title)).Append("</title><style>").Append(Css).Append("</style></head><body><main>");
            sb.Append("<h1>").Append(Esc(title)).Append("</h1>");
            if (!string.IsNullOrEmpty(timeline.Header)) sb.Append("<p class=\"muted\">").Append(Esc(timeline.Header)).Append("</p>");

            if (timeline.Duration <= 0f)
            {
                sb.Append("<p>Nothing was recorded.</p></main></body></html>");
                return sb.ToString();
            }

            // The numbers.
            sb.Append("<section class=\"cards\">");
            Card(sb, "Length", Time(s.Duration), $"{s.Lamps} of 10 lamps lit");
            Card(sb, "Hunted", Pct(s.Hunted), "of the run, something was coming for you");
            Card(sb, "Under pressure", Pct(s.Pressure), "something out, or the dread / hush");
            Card(sb, "Overlap", Pct(s.Overlap), "two or more threats out at once");
            Card(sb, "Quiet", Pct(s.Quiet), $"longest {Time(s.LongestQuiet)}, {s.QuietStretches} stretch{(s.QuietStretches == 1 ? "" : "es")} of {SessionTimeline.QuietWorthNoting:0}s+");
            Card(sb, "Between beats", Time(s.MedianGap), $"typical gap between scares / arrivals; longest {Time(s.LongestGap)}");
            Card(sb, "Scares", s.Scares.ToString(CultureInfo.InvariantCulture), $"{s.Appeared} arrivals, caught {s.Caught}×");
            Card(sb, "Stood still", Pct(s.Still), $"longest {Time(s.LongestStill)}; flashlight on {Pct(s.FlashlightOn)}");
            sb.Append("</section>");

            sb.Append("<p class=\"muted\">Read it for contrast: horror needs stretches of quiet between the peaks. Long flat runs of pressure, lots of overlap, or a short gap between beats usually mean it's going numb. Long stretches of standing still can be fear, or being lost.</p>");

            Svg(sb, timeline, s);
            Legend(sb);
            Chapters(sb, s);
            sb.Append("</main></body></html>");
            return sb.ToString();
        }

        // ------------------------------------------------------------------ the chart

        static void Svg(StringBuilder sb, SessionTimeline t, SessionTimeline.Summary s)
        {
            float duration = t.Duration;
            float X(float time) => Left + time / duration * PlotW;

            const float curveTop = 28f, curveH = 70f;
            float y = curveTop + curveH + 26f;
            int laneCount = t.Lanes.Count;
            float lanesTop = y;
            float height = lanesTop + laneCount * (LaneH + LaneGap) + 46f;

            sb.Append("<div class=\"chart\"><svg viewBox=\"0 0 ").Append(F(Width)).Append(' ').Append(F(height)).Append("\" role=\"img\" aria-label=\"The run's timeline\">");

            // Time axis: a line a minute (or every 5 or 10 in a long run).
            float every = duration > 3600f ? 600f : duration > 1200f ? 300f : 60f;
            for (float m = 0f; m <= duration + 0.01f; m += every)
            {
                float x = X(m);
                sb.Append("<line class=\"grid\" x1=\"").Append(F(x)).Append("\" x2=\"").Append(F(x)).Append("\" y1=\"").Append(F(curveTop)).Append("\" y2=\"").Append(F(height - 30f)).Append("\"/>");
                sb.Append("<text class=\"axis\" x=\"").Append(F(x)).Append("\" y=\"").Append(F(height - 14f)).Append("\" text-anchor=\"middle\">").Append(Time(m)).Append("</text>");
            }

            // The pressure curve (10 s bins).
            const float bin = 10f;
            var curve = t.PressureCurve(bin);
            sb.Append("<text class=\"lane-name\" x=\"").Append(F(Left - 10f)).Append("\" y=\"").Append(F(curveTop + curveH * 0.5f + 4f)).Append("\" text-anchor=\"end\">Pressure</text>");
            sb.Append("<rect class=\"curve-bg\" x=\"").Append(F(Left)).Append("\" y=\"").Append(F(curveTop)).Append("\" width=\"").Append(F(PlotW)).Append("\" height=\"").Append(F(curveH)).Append("\"/>");
            var path = new StringBuilder();
            path.Append('M').Append(F(Left)).Append(',').Append(F(curveTop + curveH));
            for (int i = 0; i < curve.Length; i++)
            {
                float x0 = X(Math.Min(duration, i * bin)), x1 = X(Math.Min(duration, (i + 1) * bin));
                float yy = curveTop + curveH - curve[i] * curveH;
                path.Append(" L").Append(F(x0)).Append(',').Append(F(yy)).Append(" L").Append(F(x1)).Append(',').Append(F(yy));
            }
            path.Append(" L").Append(F(X(duration))).Append(',').Append(F(curveTop + curveH)).Append(" Z");
            sb.Append("<path class=\"curve\" d=\"").Append(path).Append("\"/>");

            // The lanes.
            string group = null;
            for (int i = 0; i < laneCount; i++)
            {
                var lane = t.Lanes[i];
                float ly = lanesTop + i * (LaneH + LaneGap);
                if (lane.Group != group)
                {
                    group = lane.Group;
                    sb.Append("<text class=\"group\" x=\"8\" y=\"").Append(F(ly + LaneH - 5f)).Append("\">").Append(Esc(group)).Append("</text>");
                }
                sb.Append("<text class=\"lane-name\" x=\"").Append(F(Left - 10f)).Append("\" y=\"").Append(F(ly + LaneH - 5f)).Append("\" text-anchor=\"end\">").Append(Esc(lane.Name)).Append("</text>");
                sb.Append("<rect class=\"lane-bg\" x=\"").Append(F(Left)).Append("\" y=\"").Append(F(ly)).Append("\" width=\"").Append(F(PlotW)).Append("\" height=\"").Append(F(LaneH)).Append("\"/>");
                for (int k = 0; k < lane.Count; k++)
                {
                    int v = lane.ValueAt(k);
                    if (v == 0 && lane.Group == SessionTimeline.Threats) continue; // (away: nothing drawn)
                    float a = lane.TimeAt(k), b = k + 1 < lane.Count ? lane.TimeAt(k + 1) : duration;
                    if (b <= a) continue;
                    string what = v < lane.Values.Length ? lane.Values[v] : v.ToString(CultureInfo.InvariantCulture);
                    sb.Append("<rect class=\"").Append(ClassFor(lane, v)).Append("\" x=\"").Append(F(X(a))).Append("\" y=\"").Append(F(ly)).Append("\" width=\"").Append(F(Math.Max(0.6f, X(b) - X(a)))).Append("\" height=\"").Append(F(LaneH)).Append("\">");
                    sb.Append("<title>").Append(Esc(lane.Name)).Append(": ").Append(Esc(what)).Append(", ").Append(Time(a)).Append("–").Append(Time(b)).Append(" (").Append(Time(b - a)).Append(")</title></rect>");
                }
            }

            // The moments: lamps as lines down through everything, the rest as ticks over the curve.
            float bottom = lanesTop + laneCount * (LaneH + LaneGap) - LaneGap;
            foreach (var m in t.Markers)
            {
                float x = X(m.Time);
                string tip = $"{Time(m.Time)} {m.Label}";
                switch (m.Kind)
                {
                    case SessionTimeline.MarkKind.Lamp:
                        sb.Append("<g><line class=\"lamp\" x1=\"").Append(F(x)).Append("\" x2=\"").Append(F(x)).Append("\" y1=\"").Append(F(curveTop - 10f)).Append("\" y2=\"").Append(F(bottom)).Append("\"/>");
                        sb.Append("<circle class=\"lamp-dot\" cx=\"").Append(F(x)).Append("\" cy=\"").Append(F(curveTop - 14f)).Append("\" r=\"8\"/>");
                        sb.Append("<text class=\"lamp-num\" x=\"").Append(F(x)).Append("\" y=\"").Append(F(curveTop - 10.5f)).Append("\" text-anchor=\"middle\">").Append(LampNumber(m.Label)).Append("</text>");
                        sb.Append("<title>").Append(Esc(tip)).Append("</title></g>");
                        break;
                    case SessionTimeline.MarkKind.Caught:
                        sb.Append("<path class=\"caught\" d=\"M").Append(F(x - 6f)).Append(',').Append(F(curveTop + curveH + 16f)).Append(" L").Append(F(x + 6f)).Append(',').Append(F(curveTop + curveH + 16f)).Append(" L").Append(F(x)).Append(',').Append(F(curveTop + curveH + 5f)).Append(" Z\"><title>").Append(Esc(tip)).Append("</title></path>");
                        break;
                    default:
                        string cls = m.Kind == SessionTimeline.MarkKind.Scare ? "tick scare" : m.Kind == SessionTimeline.MarkKind.Appeared ? "tick appeared" : "tick other";
                        sb.Append("<line class=\"").Append(cls).Append("\" x1=\"").Append(F(x)).Append("\" x2=\"").Append(F(x)).Append("\" y1=\"").Append(F(curveTop + curveH + 3f)).Append("\" y2=\"").Append(F(curveTop + curveH + 17f)).Append("\"><title>").Append(Esc(tip)).Append("</title></line>");
                        break;
                }
            }
            sb.Append("</svg></div>");
        }

        static string ClassFor(SessionTimeline.Lane lane, int v)
        {
            switch (lane.Name)
            {
                case SessionTimeline.LevelLane: return "lv" + v;
                case SessionTimeline.MoveLane: return "mv" + v;
                case SessionTimeline.LightLane: return v == 1 ? "light" : "off";
                case SessionTimeline.CoverLane: return "cv" + v;
            }
            if (lane.OneIsAmbient && v == 1) return "ambient";
            return "th" + Math.Min(v, 3);
        }

        static string LampNumber(string label)
        {
            // ("Lamp 4 lit (3 of 10)": the count, not the lamp's index.)
            int open = label.IndexOf('('), of = label.IndexOf(" of ", StringComparison.Ordinal);
            return open >= 0 && of > open ? Esc(label.Substring(open + 1, of - open - 1)) : "•";
        }

        static void Legend(StringBuilder sb)
        {
            sb.Append("<div class=\"legend\">");
            Key(sb, "th1", "threat out / watching"); Key(sb, "th2", "hunting you"); Key(sb, "th3", "taking you"); Key(sb, "ambient", "background (the giant far off)");
            Key(sb, "lv1", "dread"); Key(sb, "lv2", "hush");
            Key(sb, "mv0", "still"); Key(sb, "mv2", "running"); Key(sb, "mv3", "crouching");
            Key(sb, "cv1", "in a light"); Key(sb, "cv2", "under cover");
            sb.Append("<span class=\"key\"><i class=\"sw tick-scare\"></i>scare</span><span class=\"key\"><i class=\"sw tick-appeared\"></i>something appeared</span><span class=\"key\"><i class=\"sw caught-k\"></i>caught</span>");
            sb.Append("</div><p class=\"muted\">Hover any bar or mark for what it was and when.</p>");
        }

        static void Key(StringBuilder sb, string cls, string label)
            => sb.Append("<span class=\"key\"><svg width=\"14\" height=\"12\"><rect class=\"").Append(cls).Append("\" width=\"14\" height=\"12\" rx=\"2\"/></svg>").Append(Esc(label)).Append("</span>");

        static void Chapters(StringBuilder sb, SessionTimeline.Summary s)
        {
            sb.Append("<h2>Lamp by lamp</h2><div class=\"table-wrap\"><table><thead><tr><th>Stretch</th><th>Length</th><th>Hunted</th><th>Under pressure</th><th>Stood still</th><th>Scares</th><th>Arrivals</th><th>Caught</th></tr></thead><tbody>");
            foreach (var c in s.Chapters)
            {
                string name = c.Lamps == 0 ? "Start → lamp 1" : c.Lamps >= s.Lamps ? $"After lamp {c.Lamps}" : $"Lamp {c.Lamps} → {c.Lamps + 1}";
                sb.Append("<tr><td>").Append(Esc(name)).Append("</td><td>").Append(Time(c.Length)).Append("</td>");
                sb.Append("<td>").Append(Bar(c.Hunted)).Append("</td><td>").Append(Bar(c.Pressure)).Append("</td><td>").Append(Pct(c.Still)).Append("</td>");
                sb.Append("<td>").Append(c.Scares).Append("</td><td>").Append(c.Appeared).Append("</td><td>").Append(c.Caught).Append("</td></tr>");
            }
            sb.Append("</tbody></table></div>");
        }

        static string Bar(float share) => $"<span class=\"bar\"><i style=\"width:{F(Math.Max(0f, Math.Min(1f, share)) * 100f)}%\"></i></span>{Pct(share)}";

        static void Card(StringBuilder sb, string label, string value, string note)
            => sb.Append("<div class=\"card\"><div class=\"label\">").Append(Esc(label)).Append("</div><div class=\"value\">").Append(Esc(value)).Append("</div><div class=\"note\">").Append(Esc(note)).Append("</div></div>");

        // ------------------------------------------------------------------ formatting

        static string F(float v) => v.ToString("0.##", CultureInfo.InvariantCulture);
        static string Pct(float share) => (share * 100f).ToString("0", CultureInfo.InvariantCulture) + "%";
        static string Esc(string s) => WebUtility.HtmlEncode(s ?? "");

        /// <summary>m:ss (or h:mm:ss).</summary>
        public static string Time(float seconds)
        {
            int t = (int)Math.Round(Math.Max(0f, seconds));
            return t >= 3600 ? $"{t / 3600}:{t / 60 % 60:00}:{t % 60:00}" : $"{t / 60}:{t % 60:00}";
        }

        const string Css = @"
:root{--bg:#0f1114;--panel:#171a1f;--ink:#e6e3dc;--muted:#9a978f;--line:#2a2e35;--accent:#e0b04a;
--th1:#6b5a8e;--th2:#d0663a;--th3:#e8333a;--amb:#3a3550;--lv1:#7d5a2e;--lv2:#b23a3a;
--mv0:#4f6d8a;--mv1:#2c333c;--mv2:#c9a13b;--mv3:#3f7a5c;--mv4:#e8333a;--mv5:#5aa0c8;
--light:#d9c88a;--off:#23272d;--cv0:#23272d;--cv1:#b07f52;--cv2:#4f8f8c}
*{box-sizing:border-box}body{margin:0;background:var(--bg);color:var(--ink);font:14px/1.45 system-ui,-apple-system,Segoe UI,Roboto,sans-serif}
main{max-width:1240px;margin:0 auto;padding:24px 16px 48px}h1{font-size:22px;margin:0 0 4px}h2{font-size:17px;margin:28px 0 10px}
.muted{color:var(--muted);margin:6px 0 14px}
.cards{display:grid;grid-template-columns:repeat(auto-fill,minmax(180px,1fr));gap:10px;margin:16px 0}
.card{background:var(--panel);border:1px solid var(--line);border-radius:8px;padding:10px 12px}
.card .label{color:var(--muted);font-size:12px;text-transform:uppercase;letter-spacing:.04em}.card .value{font-size:24px;font-weight:600;margin:2px 0}.card .note{color:var(--muted);font-size:12px}
.chart{background:var(--panel);border:1px solid var(--line);border-radius:8px;padding:8px;overflow-x:auto}.chart svg{display:block;min-width:900px;width:100%;height:auto}
.grid{stroke:var(--line);stroke-width:1}.axis{fill:var(--muted);font-size:11px}.lane-name{fill:var(--ink);font-size:12px}.group{fill:var(--accent);font-size:11px;text-transform:uppercase;letter-spacing:.05em}
.lane-bg,.curve-bg{fill:#1d2127}.curve{fill:rgba(208,102,58,.55);stroke:#d0663a;stroke-width:1.2}
.th1{fill:var(--th1)}.th2{fill:var(--th2)}.th3{fill:var(--th3)}.ambient{fill:var(--amb)}.lv0{fill:transparent}.lv1{fill:var(--lv1)}.lv2{fill:var(--lv2)}
.mv0{fill:var(--mv0)}.mv1{fill:var(--mv1)}.mv2{fill:var(--mv2)}.mv3{fill:var(--mv3)}.mv4{fill:var(--mv4)}.mv5{fill:var(--mv5)}
.light{fill:var(--light)}.off{fill:var(--off)}.cv0{fill:var(--cv0)}.cv1{fill:var(--cv1)}.cv2{fill:var(--cv2)}
.lamp{stroke:var(--accent);stroke-width:1.5;stroke-dasharray:4 3}.lamp-dot{fill:var(--accent)}.lamp-num{fill:#111;font-size:10px;font-weight:700}
.tick{stroke-width:2}.scare{stroke:#f0d060}.appeared{stroke:#a59ccf}.other{stroke:#5aa0c8}.caught{fill:#e8333a}
.legend{display:flex;flex-wrap:wrap;gap:6px 16px;margin:10px 2px 0;color:var(--muted);font-size:12px}.key{display:inline-flex;align-items:center;gap:6px}.key svg{display:block}
.sw{display:inline-block;width:3px;height:12px}.tick-scare{background:#f0d060}.tick-appeared{background:#a59ccf}.caught-k{width:10px;background:#e8333a;clip-path:polygon(0 100%,100% 100%,50% 0)}
.table-wrap{overflow-x:auto}table{border-collapse:collapse;width:100%;background:var(--panel);border:1px solid var(--line);border-radius:8px}
th,td{padding:7px 10px;border-bottom:1px solid var(--line);text-align:left;white-space:nowrap}th{color:var(--muted);font-weight:500;font-size:12px}
.bar{display:inline-block;width:70px;height:7px;background:#2a2e35;border-radius:4px;margin-right:8px;vertical-align:middle;overflow:hidden}.bar i{display:block;height:100%;background:#d0663a}
";
    }
}
