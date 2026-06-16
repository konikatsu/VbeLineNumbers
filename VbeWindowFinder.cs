using Microsoft.Vbe.Interop;
using System;
using System.Runtime.InteropServices;
using System.Text;

namespace VbeLineNumbers
{
    internal static class VbeWindowFinder
    {
        private const int MinimumEditorWidth = 120;
        private const int MinimumEditorHeight = 80;

        internal sealed class CodeWindowInfo
        {
            internal IntPtr OwnerWindowHandle { get; set; }

            internal IntPtr BoundsWindowHandle { get; set; }

            internal IntPtr FontWindowHandle { get; set; }

            internal NativeMethods.RECT Bounds { get; set; }

            internal uint Dpi { get; set; }

            internal bool NeedsCodeHeaderOffset { get; set; }
        }

        private sealed class Candidate
        {
            internal IntPtr WindowHandle { get; set; }

            internal IntPtr FontHandle { get; set; }

            internal NativeMethods.RECT Bounds { get; set; }

            internal NativeMethods.RECT RootBounds { get; set; }

            internal int Score { get; set; }
        }

        internal static CodeWindowInfo GetActiveCodeWindowInfo(VBE vbe)
        {
            IntPtr mainWindowHandle = GetVbeMainWindowHandle(vbe);

            if (mainWindowHandle == IntPtr.Zero)
            {
                return null;
            }

            IntPtr mdiClientHandle = FindMdiClient(mainWindowHandle);

            if (mdiClientHandle == IntPtr.Zero)
            {
                return null;
            }

            IntPtr searchRootHandle = GetActiveMdiChild(mdiClientHandle);

            if (searchRootHandle == IntPtr.Zero)
            {
                searchRootHandle = mdiClientHandle;
            }

            Candidate candidate = FindBestEditorCandidate(searchRootHandle);

            if (candidate == null && searchRootHandle != mdiClientHandle)
            {
                candidate = FindBestEditorCandidate(mdiClientHandle);
            }

            if (candidate == null)
            {
                return null;
            }

            return new CodeWindowInfo
            {
                OwnerWindowHandle = mainWindowHandle,
                BoundsWindowHandle = candidate.WindowHandle,
                FontWindowHandle = candidate.FontHandle == IntPtr.Zero
                    ? candidate.WindowHandle
                    : candidate.WindowHandle,
                Bounds = candidate.Bounds,
                Dpi = GetDpi(candidate.WindowHandle, mdiClientHandle),
                NeedsCodeHeaderOffset =
                    candidate.Bounds.Top <= candidate.RootBounds.Top + 10
            };
        }

        private static IntPtr FindMdiClient(IntPtr mainWindowHandle)
        {
            IntPtr result = IntPtr.Zero;

            NativeMethods.EnumChildWindows(
                mainWindowHandle,
                delegate (IntPtr windowHandle, IntPtr parameter)
                {
                    if (!NativeMethods.IsWindowVisible(windowHandle))
                    {
                        return true;
                    }

                    string className = GetClassName(windowHandle);

                    if (!string.Equals(
                            className,
                            "MDIClient",
                            StringComparison.Ordinal))
                    {
                        return true;
                    }

                    if (!TryGetUsableRect(windowHandle, out NativeMethods.RECT rect))
                    {
                        return true;
                    }

                    result = windowHandle;
                    return false;
                },
                IntPtr.Zero);

            return result;
        }

        private static Candidate FindBestEditorCandidate(IntPtr parentWindowHandle)
        {
            Candidate bestCandidate = null;

            NativeMethods.EnumChildWindows(
                parentWindowHandle,
                delegate (IntPtr windowHandle, IntPtr parameter)
                {
                    Candidate candidate = CreateCandidate(
                        windowHandle,
                        parentWindowHandle);

                    if (candidate != null &&
                        (bestCandidate == null ||
                         candidate.Score > bestCandidate.Score))
                    {
                        bestCandidate = candidate;
                    }

                    return true;
                },
                IntPtr.Zero);

            return bestCandidate;
        }

        private static Candidate CreateCandidate(
            IntPtr windowHandle,
            IntPtr rootWindowHandle)
        {
            if (windowHandle == IntPtr.Zero ||
                !NativeMethods.IsWindowVisible(windowHandle))
            {
                return null;
            }

            if (!IsDescendantOf(windowHandle, rootWindowHandle))
            {
                return null;
            }

            if (!TryGetUsableRect(windowHandle, out NativeMethods.RECT rect))
            {
                return null;
            }

            if (!TryGetUsableRect(rootWindowHandle, out NativeMethods.RECT rootRect))
            {
                return null;
            }

            if (IsNearlySameBounds(rect, rootRect))
            {
                return null;
            }

            string className = GetClassName(windowHandle);

            if (IsExcludedClass(className))
            {
                return null;
            }

            IntPtr fontHandle = NativeMethods.SendMessage(
                windowHandle,
                NativeMethods.WM_GETFONT,
                IntPtr.Zero,
                IntPtr.Zero);

            long style = NativeMethods.GetWindowLongPtr(
                windowHandle,
                NativeMethods.GWL_STYLE).ToInt64();

            bool hasVerticalScroll = (style & NativeMethods.WS_VSCROLL) != 0;
            bool hasHorizontalScroll = (style & NativeMethods.WS_HSCROLL) != 0;
            int score = rect.Width * rect.Height / 1000;
            int depth = GetAncestorDepth(windowHandle, rootWindowHandle);
            int leftOffset = Math.Max(0, rect.Left - rootRect.Left);

            if (!hasVerticalScroll && fontHandle == IntPtr.Zero)
            {
                score -= 10000;
            }

            if (fontHandle != IntPtr.Zero)
            {
                score += 8000;
            }

            if (hasVerticalScroll)
            {
                score += 12000;
            }

            if (hasHorizontalScroll)
            {
                score += 3000;
            }

            if (className.IndexOf("Vba", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                score += 1000;
            }

            if (className.IndexOf("Edit", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                score += 1000;
            }

            if (depth > 0)
            {
                score += Math.Min(depth, 5) * 2500;
            }

            if (rect.Top <= rootRect.Top + 10)
            {
                score -= 4000;
            }

            score -= leftOffset * 200;

            return new Candidate
            {
                WindowHandle = windowHandle,
                FontHandle = fontHandle,
                Bounds = rect,
                RootBounds = rootRect,
                Score = score
            };
        }

        private static bool TryGetUsableRect(
            IntPtr windowHandle,
            out NativeMethods.RECT rect)
        {
            if (!NativeMethods.GetWindowRect(windowHandle, out rect))
            {
                return false;
            }

            return rect.Width >= MinimumEditorWidth &&
                   rect.Height >= MinimumEditorHeight;
        }

        private static bool IsNearlySameBounds(
            NativeMethods.RECT first,
            NativeMethods.RECT second)
        {
            return Math.Abs(first.Left - second.Left) <= 4 &&
                   Math.Abs(first.Top - second.Top) <= 4 &&
                   Math.Abs(first.Right - second.Right) <= 4 &&
                   Math.Abs(first.Bottom - second.Bottom) <= 4;
        }

        private static bool IsDescendantOf(
            IntPtr windowHandle,
            IntPtr ancestorWindowHandle)
        {
            if (windowHandle == ancestorWindowHandle)
            {
                return false;
            }

            IntPtr current = windowHandle;

            while (current != IntPtr.Zero)
            {
                current = NativeMethods.GetAncestor(
                    current,
                    NativeMethods.GA_PARENT);

                if (current == ancestorWindowHandle)
                {
                    return true;
                }
            }

            return false;
        }

        private static int GetAncestorDepth(
            IntPtr windowHandle,
            IntPtr ancestorWindowHandle)
        {
            int depth = 0;
            IntPtr current = windowHandle;

            while (current != IntPtr.Zero)
            {
                current = NativeMethods.GetAncestor(
                    current,
                    NativeMethods.GA_PARENT);

                depth++;

                if (current == ancestorWindowHandle)
                {
                    return depth;
                }
            }

            return 0;
        }

        private static bool IsExcludedClass(string className)
        {
            return string.Equals(className, "ComboBox", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(className, "ScrollBar", StringComparison.OrdinalIgnoreCase) ||
                   className.IndexOf("Combo", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   className.IndexOf("ScrollBar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   className.IndexOf("Toolbar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                   className.IndexOf("Status", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static IntPtr GetActiveMdiChild(IntPtr mdiClientHandle)
        {
            return NativeMethods.SendMessage(
                mdiClientHandle,
                NativeMethods.WM_MDIGETACTIVE,
                IntPtr.Zero,
                IntPtr.Zero);
        }

        private static uint GetDpi(
            IntPtr preferredWindowHandle,
            IntPtr fallbackWindowHandle)
        {
            uint dpi = NativeMethods.GetDpiForWindow(preferredWindowHandle);

            if (dpi == 0 && fallbackWindowHandle != IntPtr.Zero)
            {
                dpi = NativeMethods.GetDpiForWindow(fallbackWindowHandle);
            }

            return dpi == 0 ? 96U : dpi;
        }

        private static string GetClassName(IntPtr windowHandle)
        {
            StringBuilder className = new StringBuilder(256);

            NativeMethods.GetClassName(
                windowHandle,
                className,
                className.Capacity);

            return className.ToString();
        }

        private static IntPtr GetVbeMainWindowHandle(VBE vbe)
        {
            try
            {
                if (vbe == null || vbe.MainWindow == null)
                {
                    return IntPtr.Zero;
                }

                return new IntPtr(vbe.MainWindow.HWnd);
            }
            catch (COMException)
            {
                return IntPtr.Zero;
            }
        }
    }
}
