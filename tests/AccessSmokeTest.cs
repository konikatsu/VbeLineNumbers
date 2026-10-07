using Extensibility;
using Microsoft.Vbe.Interop;
using System;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using VbeLineNumbers;

internal static class AccessSmokeTest
{
    private static readonly BindingFlags PrivateInstance = BindingFlags.NonPublic | BindingFlags.Instance;

    private static void Pump()
    {
        for (int i = 0; i < 10; i++) { System.Windows.Forms.Application.DoEvents(); Thread.Sleep(20); }
    }

    private static int Field(LineNumberOverlay overlay, string name)
    {
        return Convert.ToInt32(typeof(LineNumberOverlay).GetField(name, PrivateInstance).GetValue(overlay));
    }

    private static void Verify(Connect addin, dynamic app, string label)
    {
        Pump();
        var initialInfo = VbeWindowFinder.GetActiveCodeWindowInfo((VBE)app.VBE);
        Console.WriteLine("INFO " + label + ": editor=" + (initialInfo == null ? "missing" : initialInfo.EditorWindowHandle.ToString()) +
            " foreground=" + NativeMethods.GetForegroundWindow() + " owner=" + app.VBE.MainWindow.HWnd);
        typeof(Connect).GetMethod("UpdateOverlay", PrivateInstance).Invoke(addin, null);
        var overlay = (LineNumberOverlay)typeof(Connect).GetField("_overlay", PrivateInstance).GetValue(addin);
        if (!overlay.Visible) throw new Exception(label + ": overlay hidden");
        var info = VbeWindowFinder.GetActiveCodeWindowInfo((VBE)app.VBE);
        if (overlay.Right > info.Bounds.Left) throw new Exception(label + ": gutter covers code/breakpoints");
        dynamic pane = app.VBE.ActiveCodePane;
        int first = pane.TopLine;
        if (Field(overlay, "_firstLine") != first) throw new Exception(label + ": wrong first line");
        int pitch = Field(overlay, "_lineHeight");
        int startLine, startColumn, endLine, endColumn;
        ((CodePane)pane).GetSelection(out startLine, out startColumn, out endLine, out endColumn);
        if (info.CaretBounds.Height > 0 && startLine == endLine)
        {
            if (pitch != info.CaretBounds.Height) throw new Exception(label + ": wrong native pitch");
            int error = overlay.Top + (startLine - first) * pitch - info.CaretBounds.Top;
            if (error != 0) throw new Exception(label + ": caret/number row error=" + error);
        }
        int wanted = LineLayout.GetDrawnLineCount(first, pane.CodeModule.CountOfLines, overlay.Height, pitch);
        if (Field(overlay, "_visibleLineCount") != wanted) throw new Exception(label + ": editor not filled");
        Rectangle original = overlay.Bounds;
        NativeMethods.GetWindowRect(info.LayoutWindowHandle, out NativeMethods.RECT before);
        for (int i = 0; i < 20; i++) typeof(Connect).GetMethod("UpdateOverlay", PrivateInstance).Invoke(addin, null);
        NativeMethods.GetWindowRect(info.LayoutWindowHandle, out NativeMethods.RECT after);
        if (before.Width != after.Width || overlay.Bounds != original) throw new Exception(label + ": unstable layout");
        Console.WriteLine("PASS " + label + ": top=" + first + " pitch=" + pitch + " rows=" + wanted + " bounds=" + overlay.Bounds);
    }

    internal static void Run(string[] args)
    {
        if (args.Length != 1) throw new ArgumentException("Pass the dedicated test .accdb path.");
        dynamic app = Activator.CreateInstance(Type.GetTypeFromProgID("Access.Application"));
        var addin = new Connect();
        Array custom = new object[0];
        try
        {
            app.OpenCurrentDatabase(System.IO.Path.GetFullPath(args[0]));
            dynamic pane = app.VBE.ActiveVBProject.VBComponents.Item("LineNumberTest").CodeModule.CodePane;
            pane.Show();
            app.VBE.MainWindow.Visible = true;
            app.VBE.MainWindow.SetFocus();
            addin.OnConnection(app.VBE, ext_ConnectMode.ext_cm_AfterStartup, null, ref custom);
            pane.SetSelection(506, 1, 506, 1);
            pane.TopLine = 506;
            Verify(addin, app, "line 506");

            pane.SetSelection(540, 1, 540, 1);
            Verify(addin, app, "bottom row alignment");
            pane.SetSelection(900, 1, 900, 1);
            pane.TopLine = 880;
            Verify(addin, app, "scroll");
            pane.SetSelection(998, 1, 998, 1);
            pane.TopLine = 998;
            Verify(addin, app, "end of module");

            app.VBE.MainWindow.WindowState = vbext_WindowState.vbext_ws_Maximize;
            pane.SetSelection(506, 1, 506, 1);
            pane.TopLine = 506;
            Verify(addin, app, "maximized VBE");
            app.VBE.MainWindow.WindowState = vbext_WindowState.vbext_ws_Normal;
            Verify(addin, app, "restored VBE");

            // Capture the dedicated test window for visual inspection.
            IntPtr main = new IntPtr((int)app.VBE.MainWindow.HWnd);
            NativeMethods.GetWindowRect(main, out NativeMethods.RECT screen);
            using (var image = new Bitmap(screen.Width, screen.Height))
            {
                using (Graphics graphics = Graphics.FromImage(image))
                    graphics.CopyFromScreen(screen.Left, screen.Top, 0, 0, image.Size);
                image.Save("verification_access.png");
            }

            app.VBE.MainWindow.Visible = false;
            Pump();
            var overlay = (LineNumberOverlay)typeof(Connect).GetField("_overlay", PrivateInstance).GetValue(addin);
            if (overlay.Visible) throw new Exception("Overlay remains visible after hiding VBE");
            Console.WriteLine("PASS hidden VBE");
        }
        finally
        {
            addin.OnDisconnection(ext_DisconnectMode.ext_dm_UserClosed, ref custom);
            app.Quit(2);
        }
    }
}
