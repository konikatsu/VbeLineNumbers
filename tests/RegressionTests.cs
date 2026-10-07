using System;
using System.Drawing;
using System.Windows.Forms;
using VbeLineNumbers;

internal static class RegressionTests
{
    private static int _assertions;

    private static void Equal(int expected, int actual, string label)
    {
        _assertions++;
        if (expected != actual) throw new Exception(label + ": expected " + expected + ", got " + actual);
    }

    internal static void Run()
    {
        Equal(21, LineLayout.GetLineHeight(21.333f, 737, 35), "integer pitch at 16pt");
        Equal(26, LineLayout.GetLineHeight(21.333f, 1048, 40), "use editor geometry when DPI metrics differ");
        Equal(21, LineLayout.GetLineHeight(21.333f, 0, 0), "font fallback");
        Equal(50, LineLayout.GetDrawnLineCount(506, 1000, 1048, 21), "fill the entire editor");
        Equal(3, LineLayout.GetDrawnLineCount(998, 1000, 1048, 21), "stop at end of module");
        Equal(0, LineLayout.GetDrawnLineCount(1001, 1000, 1048, 21), "no fictitious lines");
        Equal(1, LineLayout.GetDrawnLineCount(1, 0, 100, 21), "empty module editing line");
        Equal(0, LineLayout.GetDrawnLineCount(1, 100, 0, 21), "zero-height editor");

        using (var overlay = new LineNumberOverlay())
        {
            foreach (uint dpi in new uint[] { 96, 120, 144, 192 })
            foreach (int points in new int[] { 9, 14, 16, 18 })
            {
                overlay.SetFontFromEditorSettings("ＭＳ ゴシック", points, dpi);
                int width = overlay.GetPreferredWidth(10000, dpi);
                overlay.SetBounds(0, 0, width, 400);
                overlay.SetLines(506, 20, 21, 0);
                using (var bitmap = new Bitmap(width, 400)) overlay.DrawToBitmap(bitmap, new Rectangle(0, 0, width, 400));
                Equal((int)AutoScaleMode.None, (int)overlay.AutoScaleMode, "explicit pixel coordinates");
                if (width < 28 * dpi / 96) throw new Exception("number gutter is too narrow");
                _assertions++;
            }
        }

        using (var parent = new Form())
        using (var editor = new Panel())
        {
            parent.Controls.Add(editor);
            editor.SetBounds(100, 20, 600, 300);
            IntPtr parentHandle = parent.Handle;
            IntPtr editorHandle = editor.Handle;
            var gutter = new EditorGutter();
            for (int i = 0; i < 20; i++)
            {
                if (!gutter.Reserve(editorHandle, 48, out int left)) throw new Exception("reserve failed");
                NativeMethods.GetWindowRect(editorHandle, out NativeMethods.RECT bounds);
                Equal(552, bounds.Width, "repeated ticks must not shrink the editor");
                Equal(48, bounds.Left - left, "gutter is adjacent to editor");
            }
            gutter.Reserve(editorHandle, 64, out int unused);
            NativeMethods.GetWindowRect(editorHandle, out NativeMethods.RECT resized);
            Equal(536, resized.Width, "changing digit/font width replaces the old reservation");
            gutter.Restore();
            NativeMethods.GetWindowRect(editorHandle, out NativeMethods.RECT restored);
            Equal(600, restored.Width, "restore original width");

            gutter.Reserve(editorHandle, 48, out unused);
            editor.SetBounds(200, 30, 700, 320); // Native host performs a fresh layout.
            gutter.Reserve(editorHandle, 48, out unused);
            NativeMethods.GetWindowRect(editorHandle, out resized);
            Equal(652, resized.Width, "reserve after host layout");
            gutter.Restore();
            NativeMethods.GetWindowRect(editorHandle, out restored);
            Equal(700, restored.Width, "restore latest host layout");
        }
        Console.WriteLine("PASS: " + _assertions + " regression assertions");
    }
}
