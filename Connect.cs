using Extensibility;
using Microsoft.Vbe.Interop;
using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace VbeLineNumbers
{
    [ComVisible(true)]
    [Guid("D457A338-6013-4AD7-A725-1D2B88D3D39B")]
    [ProgId("VbeLineNumbers.Connect")]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class Connect : IDTExtensibility2
    {
        private const string AddinRegistryPath32 =
            @"Software\Microsoft\VBA\VBE\6.0\Addins\VbeLineNumbers.Connect";

        private const string AddinRegistryPath64 =
            @"Software\Microsoft\VBA\VBE\6.0\Addins64\VbeLineNumbers.Connect";

        private const int TimerIntervalMilliseconds = 100;

        private static readonly string[] VbaCommonRegistryPaths =
        {
            @"Software\Microsoft\VBA\7.1\Common",
            @"Software\Microsoft\VBA\7.0\Common",
            @"Software\Microsoft\VBA\6.0\Common"
        };

        private VBE _vbe;
        private LineNumberOverlay _overlay;
        private Timer _timer;
        private readonly EditorGutter _gutter = new EditorGutter();
        private IntPtr _metricsWindow;
        private float _metricsFontHeight;
        private int _lineHeight;
        private int _textTopOffset;
        private DateTime _lastExceptionLogUtc = DateTime.MinValue;

        public void OnConnection(
            object application,
            ext_ConnectMode connectMode,
            object addInInst,
            ref Array custom)
        {
            _vbe = application as VBE;

            if (_vbe == null)
            {
                Debug.WriteLine("VbeLineNumbers: VBE application object is unavailable.");
                return;
            }

            _overlay = new LineNumberOverlay();

            _timer = new Timer();
            _timer.Interval = TimerIntervalMilliseconds;
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        public void OnDisconnection(
            ext_DisconnectMode removeMode,
            ref Array custom)
        {
            Cleanup();
        }

        public void OnAddInsUpdate(ref Array custom)
        {
        }

        public void OnStartupComplete(ref Array custom)
        {
        }

        public void OnBeginShutdown(ref Array custom)
        {
            Cleanup();
        }

        [ComRegisterFunction]
        public static void RegisterAddin(Type type)
        {
            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(
                RegistryHive.CurrentUser,
                RegistryView.Registry64))
            using (RegistryKey key = baseKey.CreateSubKey(GetAddinRegistryPath()))
            {
                if (key == null)
                {
                    throw new InvalidOperationException(
                        "Could not create the VBE add-in registry key.");
                }

                key.SetValue(
                    "FriendlyName",
                    "VBE Line Numbers",
                    RegistryValueKind.String);

                key.SetValue(
                    "Description",
                    "Displays line numbers next to the VBE code editor without changing code.",
                    RegistryValueKind.String);

                key.SetValue(
                    "LoadBehavior",
                    3,
                    RegistryValueKind.DWord);

                key.SetValue(
                    "CommandLineSafe",
                    0,
                    RegistryValueKind.DWord);
            }
        }

        [ComUnregisterFunction]
        public static void UnregisterAddin(Type type)
        {
            using (RegistryKey baseKey = RegistryKey.OpenBaseKey(
                RegistryHive.CurrentUser,
                RegistryView.Registry64))
            {
                baseKey.DeleteSubKeyTree(
                    GetAddinRegistryPath(),
                    false);
            }
        }

        private static string GetAddinRegistryPath()
        {
            return Environment.Is64BitProcess
                ? AddinRegistryPath64
                : AddinRegistryPath32;
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            try
            {
                UpdateOverlay();
            }
            catch (COMException exception)
            {
                HideOverlay();
                LogThrottled(exception);
            }
            catch (InvalidComObjectException exception)
            {
                HideOverlay();
                LogThrottled(exception);
            }
            catch (ExternalException exception)
            {
                HideOverlay();
                LogThrottled(exception);
            }
        }

        private void UpdateOverlay()
        {
            if (_vbe == null || _overlay == null)
            {
                return;
            }

            CodePane pane = null;

            try
            {
                pane = _vbe.ActiveCodePane;

                if (pane == null)
                {
                    HideOverlay();
                    _gutter.Restore();
                    return;
                }

                VbeWindowFinder.CodeWindowInfo codeWindowInfo =
                    VbeWindowFinder.GetActiveCodeWindowInfo(_vbe);

                if (codeWindowInfo == null ||
                    codeWindowInfo.Bounds.Width <= 0 ||
                    codeWindowInfo.Bounds.Height <= 0)
                {
                    HideOverlay();
                    _gutter.Restore();
                    return;
                }

                // Owned top-level forms also need explicit hiding when another app
                // or a modal VBE dialog has focus.
                if (NativeMethods.GetAncestor(NativeMethods.GetForegroundWindow(), NativeMethods.GA_ROOT) !=
                    codeWindowInfo.OwnerWindowHandle)
                {
                    HideOverlay();
                    return;
                }

                IntPtr fontHandle = NativeMethods.SendMessage(
                    codeWindowInfo.EditorWindowHandle,
                    NativeMethods.WM_GETFONT,
                    IntPtr.Zero,
                    IntPtr.Zero);

                if (!TrySetEditorFontFromRegistry(_overlay, codeWindowInfo.Dpi))
                {
                    _overlay.SetFontFromHandle(fontHandle);
                }

                int visibleLineCount = Math.Max(1, pane.CountOfVisibleLines);
                int firstLine = Math.Max(1, pane.TopLine);
                int moduleLines = GetModuleLineCount(pane, firstLine + visibleLineCount - 1);
                int overlayWidth = _overlay.GetPreferredWidth(moduleLines, codeWindowInfo.Dpi);

                if (!_gutter.Reserve(codeWindowInfo.LayoutWindowHandle, overlayWidth, out int overlayLeft))
                {
                    HideOverlay();
                    _gutter.Restore();
                    return;
                }

                // Reserving the gutter resizes a maximized MDI child synchronously.
                // Read its client/scrollbar/caret positions again after layout.
                codeWindowInfo = VbeWindowFinder.GetActiveCodeWindowInfo(_vbe);
                if (codeWindowInfo == null)
                {
                    HideOverlay();
                    _gutter.Restore();
                    return;
                }

                float fontLineHeight = _overlay.GetTextLineHeight();
                if (_metricsWindow != codeWindowInfo.EditorWindowHandle ||
                    Math.Abs(_metricsFontHeight - fontLineHeight) > 0.01f)
                {
                    _metricsWindow = codeWindowInfo.EditorWindowHandle;
                    _metricsFontHeight = fontLineHeight;
                    _lineHeight = LineLayout.GetLineHeight(
                        fontLineHeight, codeWindowInfo.Bounds.Height, visibleLineCount);
                    _textTopOffset = -(int)Math.Round(3 * codeWindowInfo.Dpi / 96.0f);
                }

                NativeMethods.RECT caret = codeWindowInfo.CaretBounds;
                if (caret.Height > 0 && caret.Height <= codeWindowInfo.Bounds.Height)
                {
                    _lineHeight = caret.Height;
                    pane.GetSelection(out int startLine, out int startColumn,
                        out int endLine, out int endColumn);
                    if (startLine == endLine)
                    {
                        int offset = caret.Top - (startLine - firstLine) * _lineHeight -
                            codeWindowInfo.Bounds.Top;
                        if (Math.Abs(offset) <= _lineHeight / 2)
                            _textTopOffset = offset;
                    }
                }

                int overlayTop = codeWindowInfo.Bounds.Top + _textTopOffset;
                int overlayHeight = codeWindowInfo.Bounds.Bottom - overlayTop;
                int drawnLines = LineLayout.GetDrawnLineCount(
                    firstLine, moduleLines, overlayHeight, _lineHeight);
                if (drawnLines == 0)
                {
                    HideOverlay();
                    return;
                }

                _overlay.SetBounds(
                    overlayLeft,
                    overlayTop,
                    overlayWidth,
                    overlayHeight);

                _overlay.SetLines(
                    firstLine,
                    drawnLines,
                    _lineHeight,
                    0);

                if (!_overlay.Visible)
                {
                    _overlay.ShowOwnedBy(codeWindowInfo.OwnerWindowHandle);
                }
            }
            finally
            {
                if (pane != null && Marshal.IsComObject(pane))
                {
                    Marshal.ReleaseComObject(pane);
                }
            }
        }

        private static int GetModuleLineCount(
            CodePane pane,
            int visibleLastLine)
        {
            CodeModule module = null;

            try
            {
                module = pane.CodeModule;

                if (module != null)
                {
                    return Math.Max(1, module.CountOfLines);
                }
            }
            catch (COMException exception)
            {
                Debug.WriteLine(
                    "VbeLineNumbers: Could not read CodeModule.CountOfLines. " +
                    exception.Message);
            }
            finally
            {
                if (module != null && Marshal.IsComObject(module))
                {
                    Marshal.ReleaseComObject(module);
                }
            }

            return visibleLastLine;
        }

        private static bool TrySetEditorFontFromRegistry(
            LineNumberOverlay overlay, uint dpi)
        {
            foreach (string path in VbaCommonRegistryPaths)
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(path))
                {
                    if (key == null)
                    {
                        continue;
                    }

                    string fontFace = key.GetValue("FontFace") as string;
                    object fontHeightValue = key.GetValue("FontHeight");

                    if (string.IsNullOrWhiteSpace(fontFace) ||
                        fontHeightValue == null)
                    {
                        continue;
                    }

                    if (!TryConvertToSingle(
                            fontHeightValue,
                            out float fontHeight))
                    {
                        continue;
                    }

                    overlay.SetFontFromEditorSettings(
                        fontFace,
                        fontHeight, dpi);

                    return true;
                }
            }

            return false;
        }

        private static bool TryConvertToSingle(
            object value,
            out float result)
        {
            try
            {
                result = Convert.ToSingle(value);
                return result > 0.0f;
            }
            catch (FormatException)
            {
            }
            catch (InvalidCastException)
            {
            }
            catch (OverflowException)
            {
            }

            result = 0.0f;
            return false;
        }

        private void HideOverlay()
        {
            if (_overlay != null && _overlay.Visible)
            {
                _overlay.Hide();
            }
        }

        private void LogThrottled(Exception exception)
        {
            DateTime now = DateTime.UtcNow;

            if ((now - _lastExceptionLogUtc).TotalSeconds < 5.0)
            {
                return;
            }

            _lastExceptionLogUtc = now;
            Debug.WriteLine(
                "VbeLineNumbers: " +
                exception.GetType().Name +
                ": " +
                exception.Message);
        }

        private void Cleanup()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= Timer_Tick;
                _timer.Dispose();
                _timer = null;
            }

            _gutter.Restore();

            if (_overlay != null)
            {
                _overlay.Hide();
                _overlay.Dispose();
                _overlay = null;
            }

            if (_vbe != null)
            {
                try
                {
                    if (Marshal.IsComObject(_vbe))
                    {
                        Marshal.ReleaseComObject(_vbe);
                    }
                }
                catch (InvalidComObjectException exception)
                {
                    Debug.WriteLine(
                        "VbeLineNumbers: VBE COM object was already released. " +
                        exception.Message);
                }

                _vbe = null;
            }
        }
    }
}
