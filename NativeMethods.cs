using System;
using System.Runtime.InteropServices;
using System.Text;

namespace VbeLineNumbers
{
    internal static class NativeMethods
    {
        internal delegate bool EnumWindowsProc(
            IntPtr windowHandle,
            IntPtr parameter);

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;

            public int Width
            {
                get { return Right - Left; }
            }

            public int Height
            {
                get { return Bottom - Top; }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct POINT
        {
            internal int X;
            internal int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct GUITHREADINFO
        {
            internal int Size;
            internal uint Flags;
            internal IntPtr Active;
            internal IntPtr Focus;
            internal IntPtr Capture;
            internal IntPtr MenuOwner;
            internal IntPtr MoveSize;
            internal IntPtr Caret;
            internal RECT CaretBounds;
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetClientRect(IntPtr windowHandle, out RECT rectangle);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ClientToScreen(IntPtr windowHandle, ref POINT point);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool ScreenToClient(IntPtr windowHandle, ref POINT point);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetParent(IntPtr windowHandle);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindow(IntPtr windowHandle);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsIconic(IntPtr windowHandle);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsChild(IntPtr parentWindowHandle, IntPtr windowHandle);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetWindowPos(
            IntPtr windowHandle, IntPtr insertAfter,
            int x, int y, int width, int height, uint flags);

        [DllImport("user32.dll")]
        internal static extern uint GetWindowThreadProcessId(IntPtr windowHandle, IntPtr processId);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetGUIThreadInfo(uint threadId, ref GUITHREADINFO info);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool EnumChildWindows(
            IntPtr parentWindow,
            EnumWindowsProc callback,
            IntPtr parameter);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetWindowRect(
            IntPtr windowHandle,
            out RECT rectangle);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool IsWindowVisible(
            IntPtr windowHandle);

        [DllImport(
            "user32.dll",
            CharSet = CharSet.Auto,
            SetLastError = true)]
        internal static extern int GetClassName(
            IntPtr windowHandle,
            StringBuilder className,
            int maximumLength);

        [DllImport("user32.dll")]
        internal static extern uint GetDpiForWindow(
            IntPtr windowHandle);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetAncestor(
            IntPtr windowHandle,
            uint flags);

        [DllImport("user32.dll")]
        internal static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        internal static extern IntPtr GetWindowLongPtr64(
            IntPtr windowHandle,
            int index);

        [DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        internal static extern IntPtr GetWindowLongPtr32(
            IntPtr windowHandle,
            int index);

        internal static IntPtr GetWindowLongPtr(
            IntPtr windowHandle,
            int index)
        {
            return IntPtr.Size == 8
                ? GetWindowLongPtr64(windowHandle, index)
                : GetWindowLongPtr32(windowHandle, index);
        }

        [DllImport("user32.dll")]
        internal static extern IntPtr SendMessage(
            IntPtr windowHandle,
            uint message,
            IntPtr wParam,
            IntPtr lParam);

        internal const uint WM_GETFONT = 0x0031;

        internal const uint WM_MDIGETACTIVE = 0x0229;

        internal const uint GA_ROOT = 2;

        internal const uint GA_PARENT = 1;

        internal const int GWL_STYLE = -16;

        internal const long WS_MAXIMIZE = 0x01000000L;

        internal const long SBS_VERT = 0x0001L;

        internal const uint SWP_NOZORDER = 0x0004;

        internal const uint SWP_NOACTIVATE = 0x0010;

        internal const long WS_VSCROLL = 0x00200000L;

        internal const long WS_HSCROLL = 0x00100000L;
    }
}
