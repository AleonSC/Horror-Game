using System.Collections.Generic;
using System.Text;

namespace Game.DevTools
{
    /// <summary>The session recorder's text: timestamped lines under a header. Plain C# so it can be tested.</summary>
    public sealed class SessionLog
    {
        public readonly struct Entry
        {
            public readonly float Time;
            public readonly string Text;
            public Entry(float time, string text) { Time = time; Text = text; }
        }

        /// <summary>Oldest lines are dropped past this, so a very long session can't use unbounded memory.</summary>
        public const int MaxEntries = 50000;

        readonly List<Entry> entries = new List<Entry>();

        public string Header { get; set; } = "";
        public IReadOnlyList<Entry> Entries => entries;
        public int Count => entries.Count;

        public void Add(float time, string text)
        {
            if (entries.Count >= MaxEntries) entries.RemoveAt(0);
            entries.Add(new Entry(time, text));
        }

        public void Clear() => entries.Clear();

        public string ToText()
        {
            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(Header)) sb.AppendLine(Header).AppendLine();
            foreach (var e in entries) sb.Append('[').Append(FormatTime(e.Time)).Append("] ").AppendLine(e.Text);
            return sb.ToString();
        }

        /// <summary>Minutes and seconds to a tenth, e.g. 3:07.4.</summary>
        public static string FormatTime(float seconds)
        {
            if (seconds < 0f) seconds = 0f;
            int tenths = (int)(seconds * 10f);
            return $"{tenths / 600}:{tenths / 10 % 60:00}.{tenths % 10}";
        }
    }
}
