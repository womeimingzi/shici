using System;
using System.Text.RegularExpressions;

namespace Shici
{
    internal static class TextTools
    {
        internal const int MaxLength = 100000;
        internal static string Normalize(string raw)
        {
            // Preview only. The captured original is never overwritten.
            string value = raw.Replace("\r\n", "\n").Replace('\r', '\n');
            // U+00AD is explicitly a discretionary hyphen. Visible '-' is ambiguous.
            value = Regex.Replace(value, "(?<=[A-Za-z])\u00AD[ \t]*\n[ \t]*(?=[a-z])", "");
            value = value.Replace("\u00AD", "");
            string[] paragraphs = Regex.Split(value, "\n[ \t]*\n+");
            for (int i = 0; i < paragraphs.Length; i++)
            {
                string text = paragraphs[i];
                // Keep lines that look like lists, table cells, headings, or formulas intact.
                if (Regex.IsMatch(text, @"(?m)^\s*(?:[-*•]|\d+[.)])\s|\t|[=∑∫]") ) continue;
                text = Regex.Replace(text, @"(?<=[\u3400-\u9fff])[ \t]*\n[ \t]*(?=[\u3400-\u9fff])", "");
                text = Regex.Replace(text, @"(?<=[A-Za-z,;])[ \t]*\n[ \t]*(?=[a-z])", " ");
                paragraphs[i] = text;
            }
            return string.Join("\n\n", paragraphs);
        }
        internal static bool IsFresh(uint before, uint after, uint sourceProcess, uint ownerProcess)
        {
            return before != after && sourceProcess != 0 && sourceProcess == ownerProcess;
        }
        internal static bool CanRestore(uint capturedSequence, uint currentSequence)
        {
            return capturedSequence == currentSequence;
        }
    }
}
