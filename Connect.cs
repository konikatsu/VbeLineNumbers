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
        private const string AddinRegistryPath =
            @"Software\Microsoft\VBA\VBE\6.0\Addins64\VbeLineNumbers.Connect";

        private const int TimerIntervalMilliseconds = 100;
        private const int MinimumOverlayHeight = 80;
        private const int TopCorrectionPixels = 0;
        private const int CodeHeaderHeightPixels = 62;
        private const int BottomCorrectionPixels = 0;
        private const int HorizontalCorrectionPixels = 20;
        private const int BreakpointGutterWidthPixels = 0;
        private const int TextTopPaddingPixels = 0;
        private const float LineHeightScale = 0.90f;
        private const float LineHeightCorrectionPixels = 0.0f;

        private VBE _vbe;
        private LineNumberOverlay _overlay;
        private Timer _timer;
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
            using (RegistryKey key = baseKey.CreateSubKey(AddinRegistryPath))
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
                    AddinRegistryPath,
                    false);
            }
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
                    return;
                }

                VbeWindowFinder.CodeWindowInfo codeWindowInfo =
                    VbeWindowFinder.GetActiveCodeWindowInfo(_vbe);

                if (codeWindowInfo == null ||
                    codeWindowInfo.BoundsWindowHandle == IntPtr.Zero ||
                    codeWindowInfo.Bounds.Width <= 0 ||
                    codeWindowInfo.Bounds.Height <= 0)
                {
                    HideOverlay();
                    return;
                }

                IntPtr fontHandle = NativeMethods.SendMessage(
                    codeWindowInfo.FontWindowHandle,
                    NativeMethods.WM_GETFONT,
                    IntPtr.Zero,
                    IntPtr.Zero);

                _overlay.SetFontFromHandle(fontHandle);

                int visibleLineCount = Math.Max(1, pane.CountOfVisibleLines);
                int firstLine = Math.Max(1, pane.TopLine);
                int largestLineNumber = GetLargestLineNumber(pane, firstLine + visibleLineCount);

                float fontLineHeight = _overlay.GetTextLineHeight();
                float lineHeight = CalculateLineHeight(
                    fontLineHeight,
                    codeWindowInfo.Bounds.Height,
                    visibleLineCount,
                    codeWindowInfo.Dpi);

                float dpiScale = codeWindowInfo.Dpi / 96.0f;
                int topCorrection = Scale(TopCorrectionPixels, dpiScale);

                if (codeWindowInfo.NeedsCodeHeaderOffset)
                {
                    topCorrection += Scale(CodeHeaderHeightPixels, dpiScale);
                }

                int bottomCorrection = Scale(BottomCorrectionPixels, dpiScale);
                int horizontalCorrection = Scale(HorizontalCorrectionPixels, dpiScale);
                int breakpointGutterWidth = Scale(BreakpointGutterWidthPixels, dpiScale);
                int textTopPadding = Scale(TextTopPaddingPixels, dpiScale);
                int overlayWidth = _overlay.GetPreferredWidth(largestLineNumber);
                int visibleLinesHeight = (int)Math.Ceiling(
                    visibleLineCount * lineHeight + Scale(4, dpiScale));
                int overlayHeight = Math.Max(
                    MinimumOverlayHeight,
                    Math.Min(
                        codeWindowInfo.Bounds.Height - topCorrection - bottomCorrection,
                        visibleLinesHeight));

                _overlay.SetBounds(
                    codeWindowInfo.Bounds.Left -
                        breakpointGutterWidth -
                        overlayWidth +
                        horizontalCorrection,
                    codeWindowInfo.Bounds.Top + topCorrection,
                    overlayWidth,
                    overlayHeight);

                _overlay.SetLines(
                    firstLine,
                    visibleLineCount,
                    lineHeight,
                    textTopPadding);

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

        private static float CalculateLineHeight(
            float fontLineHeight,
            int editorHeight,
            int visibleLineCount,
            uint dpi)
        {
            float dpiScale = dpi == 0 ? 1.0f : dpi / 96.0f;
            float correctedFontLineHeight =
                fontLineHeight * LineHeightScale +
                LineHeightCorrectionPixels * dpiScale;

            if (editorHeight > 0 && visibleLineCount > 1)
            {
                float viewportLineHeight = (float)editorHeight / visibleLineCount;

                if (viewportLineHeight >= correctedFontLineHeight * 0.90f &&
                    viewportLineHeight <= correctedFontLineHeight * 1.10f)
                {
                    return Math.Max(1.0f, viewportLineHeight);
                }
            }

            return Math.Max(
                1.0f,
                correctedFontLineHeight);
        }

        private static int GetLargestLineNumber(
            CodePane pane,
            int visibleLastLine)
        {
            CodeModule module = null;

            try
            {
                module = pane.CodeModule;

                if (module != null)
                {
                    return Math.Max(visibleLastLine, module.CountOfLines);
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

        private static int Scale(int value, float dpiScale)
        {
            return (int)Math.Round(value * dpiScale);
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
