using System;

namespace VbeLineNumbers
{
    internal static class LineLayout
    {
        internal static int GetLineHeight(float fontHeight, int editorHeight, int visibleLines)
        {
            int measured = Math.Max(1, (int)Math.Round(fontHeight, MidpointRounding.AwayFromZero));
            if (visibleLines <= 0 || editorHeight <= 0) return measured;

            // Visible-line counts are truncated, so dividing height by the count
            // gives a fractional pitch and cumulative drift. Use integer pixels.
            int minimum = editorHeight / (visibleLines + 1) + 1;
            int maximum = editorHeight / visibleLines;
            return maximum < minimum ? measured : Math.Max(minimum, Math.Min(maximum, measured));
        }

        internal static int GetDrawnLineCount(int firstLine, int moduleLines, int height, int lineHeight)
        {
            if (height <= 0 || lineHeight <= 0) return 0;
            int available = Math.Max(1, moduleLines) - Math.Max(1, firstLine) + 1;
            int visible = (int)Math.Ceiling((double)height / lineHeight);
            return Math.Max(0, Math.Min(available, visible));
        }
    }
}
