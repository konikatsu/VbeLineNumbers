using Microsoft.Vbe.Interop;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace VbeLineNumbers
{
    internal static class VbeWindowFinder
    {
        internal sealed class CodeWindowInfo
        {
            internal IntPtr OwnerWindowHandle { get; set; }
            internal IntPtr EditorWindowHandle { get; set; }
            internal IntPtr LayoutWindowHandle { get; set; }
            internal NativeMethods.RECT Bounds { get; set; }
            internal NativeMethods.RECT CaretBounds { get; set; }
            internal uint Dpi { get; set; }
        }

        internal static CodeWindowInfo GetActiveCodeWindowInfo(VBE vbe)
        {
            IntPtr main = GetVbeMainWindowHandle(vbe);
            if (main == IntPtr.Zero || !NativeMethods.IsWindowVisible(main) ||
                NativeMethods.IsIconic(main))
            {
                return null;
            }

            IntPtr mdi = IntPtr.Zero;
            NativeMethods.EnumChildWindows(main, delegate (IntPtr handle, IntPtr parameter)
            {
                if (GetClassName(handle) == "MDIClient" && NativeMethods.IsWindowVisible(handle))
                {
                    mdi = handle;
                    return false;
                }
                return true;
            }, IntPtr.Zero);
            if (mdi == IntPtr.Zero) return null;

            IntPtr editor = NativeMethods.SendMessage(
                mdi, NativeMethods.WM_MDIGETACTIVE, IntPtr.Zero, IntPtr.Zero);
            // ActiveCodePane can still refer to a hidden pane while the Object Browser
            // is active. Never search other MDI children for a substitute editor.
            if (editor == IntPtr.Zero || !NativeMethods.IsWindowVisible(editor) ||
                GetClassName(editor) != "VbaWindow" ||
                !TryGetClientBounds(editor, out NativeMethods.RECT client))
            {
                return null;
            }

            NativeMethods.RECT caret = GetCaretBounds(editor);
            NativeMethods.RECT vertical = default(NativeMethods.RECT);
            int bestScore = 0;
            NativeMethods.EnumChildWindows(editor, delegate (IntPtr handle, IntPtr parameter)
            {
                if (!NativeMethods.IsWindowVisible(handle) || GetClassName(handle) != "ScrollBar")
                    return true;

                long style = NativeMethods.GetWindowLongPtr(handle, NativeMethods.GWL_STYLE).ToInt64();
                if ((style & NativeMethods.SBS_VERT) == 0 ||
                    !NativeMethods.GetWindowRect(handle, out NativeMethods.RECT bounds) ||
                    bounds.Height <= bounds.Width)
                    return true;

                int score = bounds.Height;
                if (caret.Height > 0 && caret.Top >= bounds.Top - caret.Height &&
                    caret.Top < bounds.Bottom)
                    score += 100000;
                if (score > bestScore)
                {
                    bestScore = score;
                    vertical = bounds;
                }
                return true;
            }, IntPtr.Zero);
            if (bestScore == 0) return null;

            var codeBounds = new NativeMethods.RECT
            {
                Left = client.Left,
                Top = Math.Max(client.Top, vertical.Top),
                Right = Math.Min(client.Right, vertical.Left),
                Bottom = Math.Min(client.Bottom, vertical.Bottom)
            };
            if (codeBounds.Width <= 0 || codeBounds.Height <= 0) return null;

            long editorStyle = NativeMethods.GetWindowLongPtr(editor, NativeMethods.GWL_STYLE).ToInt64();
            uint dpi = NativeMethods.GetDpiForWindow(editor);
            return new CodeWindowInfo
            {
                OwnerWindowHandle = main,
                EditorWindowHandle = editor,
                LayoutWindowHandle = (editorStyle & NativeMethods.WS_MAXIMIZE) != 0 ? mdi : editor,
                Bounds = codeBounds,
                CaretBounds = caret,
                Dpi = dpi == 0 ? 96U : dpi
            };
        }

        internal static bool TryGetClientBounds(IntPtr handle, out NativeMethods.RECT bounds)
        {
            bounds = default(NativeMethods.RECT);
            if (!NativeMethods.GetClientRect(handle, out NativeMethods.RECT client)) return false;
            var origin = new NativeMethods.POINT();
            if (!NativeMethods.ClientToScreen(handle, ref origin)) return false;
            bounds = new NativeMethods.RECT
            {
                Left = origin.X, Top = origin.Y,
                Right = origin.X + client.Width, Bottom = origin.Y + client.Height
            };
            return true;
        }

        private static NativeMethods.RECT GetCaretBounds(IntPtr editor)
        {
            var info = new NativeMethods.GUITHREADINFO
            {
                Size = Marshal.SizeOf(typeof(NativeMethods.GUITHREADINFO))
            };
            uint threadId = NativeMethods.GetWindowThreadProcessId(editor, IntPtr.Zero);
            if (!NativeMethods.GetGUIThreadInfo(threadId, ref info) ||
                info.Caret == IntPtr.Zero ||
                (info.Caret != editor && !NativeMethods.IsChild(editor, info.Caret)))
                return default(NativeMethods.RECT);

            var origin = new NativeMethods.POINT();
            if (!NativeMethods.ClientToScreen(info.Caret, ref origin))
                return default(NativeMethods.RECT);
            var bounds = info.CaretBounds;
            bounds.Left += origin.X;
            bounds.Right += origin.X;
            bounds.Top += origin.Y;
            bounds.Bottom += origin.Y;
            return bounds;
        }

        private static string GetClassName(IntPtr handle)
        {
            var name = new StringBuilder(256);
            NativeMethods.GetClassName(handle, name, name.Capacity);
            return name.ToString();
        }

        private static IntPtr GetVbeMainWindowHandle(VBE vbe)
        {
            Window mainWindow = null;
            try
            {
                if (vbe == null) return IntPtr.Zero;
                mainWindow = vbe.MainWindow;
                return mainWindow == null ? IntPtr.Zero : new IntPtr(mainWindow.HWnd);
            }
            catch (COMException) { return IntPtr.Zero; }
            finally
            {
                if (mainWindow != null && Marshal.IsComObject(mainWindow))
                    Marshal.ReleaseComObject(mainWindow);
            }
        }
    }
}
