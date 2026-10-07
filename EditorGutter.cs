using System;

namespace VbeLineNumbers
{
    // Reserve real space for the overlay instead of covering the adjacent docked
    // project window. All saved coordinates are relative to the native parent.
    internal sealed class EditorGutter
    {
        private IntPtr _window;
        private NativeMethods.RECT _original;
        private NativeMethods.RECT _reserved;
        private int _width;

        internal bool Reserve(IntPtr window, int width, out int screenLeft)
        {
            screenLeft = 0;
            if (_window != window) Restore();
            if (!TryGetRelativeBounds(window, out NativeMethods.RECT current)) return false;

            if (_window == window && SameBounds(current, _reserved))
            {
                if (_width == width)
                    return TryGetScreenLeft(window, width, out screenLeft);
                current = _original;
            }

            if (width <= 0 || current.Width - width < 120) return false;
            var reserved = current;
            reserved.Left += width;
            if (!Move(window, reserved)) return false;
            // Some host layouts reject a child-window move. Do not draw outside
            // the editor unless the requested space was actually reserved.
            if (!TryGetRelativeBounds(window, out NativeMethods.RECT actual) ||
                !SameBounds(actual, reserved)) return false;

            _window = window;
            _original = current;
            _reserved = reserved;
            _width = width;
            return TryGetScreenLeft(window, width, out screenLeft);
        }

        internal void Restore()
        {
            if (_window != IntPtr.Zero &&
                TryGetRelativeBounds(_window, out NativeMethods.RECT current) &&
                SameBounds(current, _reserved))
                Move(_window, _original);
            _window = IntPtr.Zero;
            _width = 0;
        }

        private static bool TryGetScreenLeft(IntPtr window, int width, out int left)
        {
            left = 0;
            if (!NativeMethods.GetWindowRect(window, out NativeMethods.RECT bounds)) return false;
            left = bounds.Left - width;
            return true;
        }

        private static bool TryGetRelativeBounds(IntPtr window, out NativeMethods.RECT bounds)
        {
            bounds = default(NativeMethods.RECT);
            if (!NativeMethods.IsWindow(window) ||
                !NativeMethods.GetWindowRect(window, out NativeMethods.RECT screen)) return false;
            IntPtr parent = NativeMethods.GetParent(window);
            var origin = new NativeMethods.POINT { X = screen.Left, Y = screen.Top };
            if (parent == IntPtr.Zero || !NativeMethods.ScreenToClient(parent, ref origin)) return false;
            bounds = new NativeMethods.RECT
            {
                Left = origin.X, Top = origin.Y,
                Right = origin.X + screen.Width, Bottom = origin.Y + screen.Height
            };
            return true;
        }

        private static bool SameBounds(NativeMethods.RECT a, NativeMethods.RECT b)
        {
            return a.Left == b.Left && a.Top == b.Top && a.Right == b.Right && a.Bottom == b.Bottom;
        }

        private static bool Move(IntPtr window, NativeMethods.RECT bounds)
        {
            return NativeMethods.SetWindowPos(window, IntPtr.Zero,
                bounds.Left, bounds.Top, bounds.Width, bounds.Height,
                NativeMethods.SWP_NOZORDER | NativeMethods.SWP_NOACTIVATE);
        }
    }
}
