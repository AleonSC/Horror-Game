using System;
using System.Collections.Generic;

namespace Game.DevTools
{
    /// <summary>
    /// A run as a timeline, for judging its pacing afterwards: lanes of state over time (each threat, the threat
    /// level, how the player was moving, the flashlight, being under cover), stored only where they change, and
    /// markers for moments (lamps lit, being caught, things appearing, scares). <see cref="Analyse"/> sums it up:
    /// how much of the run something was hunting, how often threats overlapped, the quiet stretches between, how
    /// long the player stood still, and the same per lamp. Plain C# so it can be tested.
    /// </summary>
    public sealed class SessionTimeline
    {
        public enum MarkKind { Lamp, Caught, Appeared, Scare, HuntsReset, Floodlights, Theme, Run }

        public readonly struct Marker
        {
            public readonly float Time;
            public readonly MarkKind Kind;
            public readonly string Label;
            public Marker(float time, MarkKind kind, string label) { Time = time; Kind = kind; Label = label; }
        }

        /// <summary>One thing's state over time: a value from 0 (nothing) up, kept only where it changes.</summary>
        public sealed class Lane
        {
            public readonly string Name, Group;
            /// <summary>What each value means (index = value).</summary>
            public readonly string[] Values;
            /// <summary>A threat: counts toward being hunted, overlap and quiet.</summary>
            public readonly bool IsThreat;
            /// <summary>Its value 1 is background (the giant walking far off): not counted as pressure or overlap.</summary>
            public readonly bool OneIsAmbient;
            readonly List<float> times = new List<float>();
            readonly List<int> values = new List<int>();

            public Lane(string name, string group, string[] valueNames, bool isThreat, bool oneIsAmbient)
            {
                Name = name; Group = group; Values = valueNames; IsThreat = isThreat; OneIsAmbient = oneIsAmbient;
            }

            public int Count => times.Count;
            public float TimeAt(int i) => times[i];
            public int ValueAt(int i) => values[i];

            public void Set(float time, int value)
            {
                if (values.Count > 0 && values[values.Count - 1] == value) return;
                if (times.Count > 0 && time < times[times.Count - 1]) time = times[times.Count - 1];
                // (Two changes at the same moment: the later one stands.)
                if (times.Count > 0 && time == times[times.Count - 1])
                {
                    values[values.Count - 1] = value;
                    if (values.Count > 1 && values[values.Count - 2] == value) { times.RemoveAt(times.Count - 1); values.RemoveAt(values.Count - 1); }
                    return;
                }
                times.Add(time);
                values.Add(value);
            }

            /// <summary>Its value at this time (0 before anything was recorded).</summary>
            public int At(float time)
            {
                int lo = 0, hi = times.Count - 1, found = -1;
                while (lo <= hi)
                {
                    int mid = (lo + hi) / 2;
                    if (times[mid] <= time) { found = mid; lo = mid + 1; }
                    else hi = mid - 1;
                }
                return found < 0 ? 0 : values[found];
            }

            /// <summary>How the value counts as pressure (0 none): ambient background doesn't.</summary>
            public int Pressure(float time)
            {
                int v = At(time);
                return OneIsAmbient && v <= 1 ? 0 : v;
            }
        }

        public static readonly string[] ThreatValues = { "away", "out / watching", "hunting", "taking you" };
        public static readonly string[] LevelValues = { "calm", "dread", "hush" };
        public static readonly string[] MoveValues = { "still", "walking", "running", "crouching", "being taken", "flying the drone" };
        public static readonly string[] OnOffValues = { "off", "on" };
        public static readonly string[] CoverValues = { "in the open", "in a light", "under cover" };

        public const string Threats = "Threats", Player = "Player";
        public const string LevelLane = "Threat level", MoveLane = "Moving", LightLane = "Flashlight", CoverLane = "Cover";

        readonly List<Lane> lanes = new List<Lane>();
        readonly Dictionary<string, Lane> byName = new Dictionary<string, Lane>();
        readonly List<Marker> markers = new List<Marker>();

        public string Header { get; set; } = "";
        public float Duration { get; private set; }
        public IReadOnlyList<Lane> Lanes => lanes;
        public IReadOnlyList<Marker> Markers => markers;
        public bool Empty => Duration <= 0f && markers.Count == 0;

        /// <summary>A threat's lane (made the first time it's asked for). The giant's "watching" is background.</summary>
        public Lane ThreatLane(string name, bool oneIsAmbient = false) => LaneFor(name, Threats, ThreatValues, true, oneIsAmbient);

        public Lane LaneFor(string name, string group, string[] values, bool isThreat = false, bool oneIsAmbient = false)
        {
            if (byName.TryGetValue(name, out var lane)) return lane;
            lane = new Lane(name, group, values, isThreat, oneIsAmbient);
            byName[name] = lane;
            lanes.Add(lane);
            return lane;
        }

        public void Set(string lane, float time, int value)
        {
            if (byName.TryGetValue(lane, out var l)) l.Set(time, value);
            Extend(time);
        }

        public void Mark(float time, MarkKind kind, string label)
        {
            markers.Add(new Marker(time, kind, label));
            Extend(time);
        }

        /// <summary>The run went on at least this long.</summary>
        public void Extend(float time) => Duration = Math.Max(Duration, time);

        // ------------------------------------------------------------------ summing it up

        public sealed class Chapter
        {
            /// <summary>Lamps lit when it began (0: from the start to the first lamp).</summary>
            public int Lamps;
            public float Start, End;
            public float Hunted, Pressure, Still;
            public int Scares, Appeared, Caught;
            public float Length => End - Start;
        }

        public sealed class Summary
        {
            public float Duration;
            /// <summary>Shares of the run (0-1): something hunting you; anything out (not background); two or more at once.</summary>
            public float Hunted, Pressure, Overlap;
            /// <summary>Quiet: nothing out, the threat level calm.</summary>
            public float LongestQuiet, Quiet;
            public int QuietStretches; // (of 30 s or more)
            public float Still, LongestStill, FlashlightOn, UnderCover;
            public int Caught, Scares, Appeared, Lamps;
            /// <summary>Between scares and things appearing (and being caught): typical and longest gap.</summary>
            public float MedianGap, LongestGap;
            public readonly List<Chapter> Chapters = new List<Chapter>();
        }

        public const float QuietWorthNoting = 30f;

        public Summary Analyse()
        {
            var s = new Summary { Duration = Duration };
            if (Duration <= 0f) return s;

            // Every moment anything changes: between them, everything holds still.
            var cuts = new List<float> { 0f, Duration };
            foreach (var l in lanes) for (int i = 0; i < l.Count; i++) cuts.Add(l.TimeAt(i));
            foreach (var m in markers) if (m.Kind == MarkKind.Lamp) cuts.Add(m.Time);
            cuts.Sort();

            byName.TryGetValue(LevelLane, out var level);
            byName.TryGetValue(MoveLane, out var move);
            byName.TryGetValue(LightLane, out var light);
            byName.TryGetValue(CoverLane, out var cover);

            // Chapters: from the start to the first lamp, then lamp to lamp.
            var lampTimes = new List<float>();
            foreach (var m in markers) if (m.Kind == MarkKind.Lamp) lampTimes.Add(m.Time);
            lampTimes.Sort();
            s.Lamps = lampTimes.Count;
            float from = 0f;
            for (int i = 0; i <= lampTimes.Count; i++)
            {
                float to = i < lampTimes.Count ? lampTimes[i] : Duration;
                if (to > from || i == lampTimes.Count) s.Chapters.Add(new Chapter { Lamps = i, Start = from, End = Math.Max(from, to) });
                from = to;
            }

            float quietRun = 0f, stillRun = 0f;
            for (int c = 0; c + 1 < cuts.Count; c++)
            {
                float a = cuts[c], b = cuts[c + 1], dt = b - a;
                if (dt <= 0f) continue;
                int hunting = 0, outCount = 0;
                foreach (var l in lanes)
                {
                    if (!l.IsThreat) continue;
                    int p = l.Pressure(a);
                    if (p >= 2) hunting++;
                    if (p >= 1) outCount++;
                }
                bool calm = level == null || level.At(a) == 0;
                bool hunted = hunting > 0, pressured = outCount > 0 || !calm;
                bool still = move != null && move.At(a) == 0;
                if (hunted) s.Hunted += dt;
                if (pressured) s.Pressure += dt;
                if (outCount >= 2) s.Overlap += dt;
                if (still) s.Still += dt;
                if (light != null && light.At(a) == 1) s.FlashlightOn += dt;
                if (cover != null && cover.At(a) >= 1) s.UnderCover += dt;

                if (!pressured)
                {
                    quietRun += dt;
                    s.Quiet += dt;
                }
                else EndQuiet(s, ref quietRun);
                if (still) { stillRun += dt; s.LongestStill = Math.Max(s.LongestStill, stillRun); }
                else stillRun = 0f;

                foreach (var ch in s.Chapters)
                {
                    float o = Math.Min(b, ch.End) - Math.Max(a, ch.Start);
                    if (o <= 0f) continue;
                    if (hunted) ch.Hunted += o;
                    if (pressured) ch.Pressure += o;
                    if (still) ch.Still += o;
                }
            }
            EndQuiet(s, ref quietRun);

            // The moments: scares and things appearing, and the gaps between them.
            var beats = new List<float>();
            foreach (var m in markers)
            {
                if (m.Kind == MarkKind.Caught) s.Caught++;
                else if (m.Kind == MarkKind.Scare) s.Scares++;
                else if (m.Kind == MarkKind.Appeared) s.Appeared++;
                else continue;
                beats.Add(m.Time);
                foreach (var ch in s.Chapters)
                {
                    if (m.Time < ch.Start || m.Time > ch.End) continue;
                    if (m.Kind == MarkKind.Caught) ch.Caught++;
                    else if (m.Kind == MarkKind.Scare) ch.Scares++;
                    else ch.Appeared++;
                    break;
                }
            }
            beats.Sort();
            var gaps = new List<float>();
            float last = 0f;
            foreach (var t in beats) { gaps.Add(t - last); last = t; }
            gaps.Add(Duration - last);
            gaps.Sort();
            s.MedianGap = gaps[gaps.Count / 2];
            s.LongestGap = gaps[gaps.Count - 1];

            float d = Duration;
            s.Hunted /= d; s.Pressure /= d; s.Overlap /= d; s.Still /= d; s.FlashlightOn /= d; s.UnderCover /= d; s.Quiet /= d;
            foreach (var ch in s.Chapters)
            {
                float len = Math.Max(1e-3f, ch.Length);
                ch.Hunted /= len; ch.Pressure /= len; ch.Still /= len;
            }
            return s;
        }

        static void EndQuiet(Summary s, ref float run)
        {
            if (run >= QuietWorthNoting) s.QuietStretches++;
            s.LongestQuiet = Math.Max(s.LongestQuiet, run);
            run = 0f;
        }

        /// <summary>
        /// How much was going on, in bins of this many seconds (0-1 each): the strongest threat at each moment (out a
        /// third, hunting two thirds, taking you all of it; the dread and hush count too), averaged over the bin.
        /// </summary>
        public float[] PressureCurve(float bin)
        {
            int n = Math.Max(1, (int)Math.Ceiling(Duration / Math.Max(0.1f, bin)));
            var curve = new float[n];
            byName.TryGetValue(LevelLane, out var level);
            const int steps = 10;
            for (int i = 0; i < n; i++)
            {
                float sum = 0f;
                for (int k = 0; k < steps; k++)
                {
                    float t = (i + (k + 0.5f) / steps) * bin;
                    if (t > Duration) break;
                    int top = 0;
                    foreach (var l in lanes) if (l.IsThreat) top = Math.Max(top, l.Pressure(t));
                    float v = top / 3f;
                    if (level != null) v = Math.Max(v, level.At(t) / 2f * 0.66f);
                    sum += v;
                }
                curve[i] = sum / steps;
            }
            return curve;
        }
    }
}
