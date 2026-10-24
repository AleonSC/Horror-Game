using System.Text;

namespace Game.UI
{
    /// <summary>The narration's markup, turned into rich text (section 7). Kept apart so the tests can reach it.</summary>
    public static class NarrationText
    {
        const string RedTag = "<color=#D9140D>";

        // *word* red (any number of stars), in capitals.
        public static string Intro(string line)
        {
            var sb = new StringBuilder();
            bool red = false;
            for (int i = 0; i < line.Length; i++)
            {
                if (line[i] == '*')
                {
                    while (i + 1 < line.Length && line[i + 1] == '*') i++;
                    sb.Append(red ? "</color>" : RedTag);
                    red = !red;
                    continue;
                }
                sb.Append(char.ToUpperInvariant(line[i]));
            }
            if (red) sb.Append("</color>");
            return sb.ToString();
        }

        // Inside "quotes": "-word" red, "-WORD-" red, other quoted words in italics. The quote marks aren't shown.
        public static string Lamp(string line, bool frantic)
        {
            var sb = new StringBuilder();
            if (frantic) sb.Append(RedTag);
            bool quoted = false;
            // (A line that's all one quote just loses its quote marks.)
            bool whole = line.Length > 1 && line[0] == '"' && line.IndexOf('"', 1) == line.Length - 1;
            int i = 0;
            while (i < line.Length)
            {
                char c = line[i];
                if (c == '"')
                {
                    quoted = !quoted;
                    // A quoted stretch starting with '-' is red to its end.
                    if (quoted && i + 1 < line.Length && line[i + 1] == '-' && !frantic)
                    {
                        int end = line.IndexOf('"', i + 1);
                        if (end < 0) end = line.Length;
                        sb.Append(RedTag).Append(line, i + 2, end - i - 2).Append("</color>");
                        i = end + 1;
                        quoted = false;
                        continue;
                    }
                    if (!whole && !frantic) sb.Append(quoted ? "<i>" : "</i>");
                    i++;
                    continue;
                }
                // -WORD- inside a line: that word red (in a frantic line, white against the red).
                if (c == '-' && i + 1 < line.Length && char.IsLetter(line[i + 1]))
                {
                    int end = line.IndexOf('-', i + 1);
                    if (end > i)
                    {
                        sb.Append(frantic ? "<color=#FFFFFF>" : RedTag).Append(line, i + 1, end - i - 1).Append("</color>");
                        i = end + 1;
                        continue;
                    }
                }
                sb.Append(c);
                i++;
            }
            if (frantic) sb.Append("</color>");
            return sb.ToString();
        }
    }
}
