using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Windows.Forms;

namespace VbeLineNumbers
{
    internal sealed class LineNumberOverlay : Form
    {
        private const int RightPadding = 4;
        private const int HorizontalPadding = 8;

        private int _firstLine = 1;
        private int _visibleLineCount = 1;
        private float _lineHeight = 16.0f;
        private float _topPadding;
        private readonly Color _backgroundColor;
        private WindowHandleOwner _ownerWindow;

        public LineNumberOverlay()
        {
            _backgroundColor = LoadBackgroundColor();

            FormBorderStyle = FormBorderStyle.None;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = false;

            BackColor = Color.Fuchsia;
            TransparencyKey = Color.Fuchsia;
            ForeColor = Color.DarkRed;

            Font = new Font(
                "Consolas",
                9.0f,
                FontStyle.Regular,
                GraphicsUnit.Point);

            DoubleBuffered = true;
        }

        protected override bool ShowWithoutActivation
        {
            get { return true; }
        }

        protected override CreateParams CreateParams
        {
            get
            {
                const int WS_EX_TOOLWINDOW = 0x00000080;
                const int WS_EX_NOACTIVATE = 0x08000000;
                const int WS_EX_TRANSPARENT = 0x00000020;

                CreateParams parameters = base.CreateParams;

                parameters.ExStyle |=
                    WS_EX_TOOLWINDOW |
                    WS_EX_NOACTIVATE |
                    WS_EX_TRANSPARENT;

                return parameters;
            }
        }

        public void SetLines(
            int firstLine,
            int visibleLineCount,
            float lineHeight,
            float topPadding)
        {
            firstLine = Math.Max(1, firstLine);
            visibleLineCount = Math.Max(1, visibleLineCount);
            lineHeight = Math.Max(1.0f, lineHeight);
            topPadding = Math.Max(0.0f, topPadding);

            if (_firstLine == firstLine &&
                _visibleLineCount == visibleLineCount &&
                Math.Abs(_lineHeight - lineHeight) < 0.01f &&
                Math.Abs(_topPadding - topPadding) < 0.01f)
            {
                return;
            }

            _firstLine = firstLine;
            _visibleLineCount = visibleLineCount;
            _lineHeight = lineHeight;
            _topPadding = topPadding;

            Invalidate();
        }

        public void SetFontFromHandle(IntPtr fontHandle)
        {
            if (fontHandle == IntPtr.Zero)
            {
                return;
            }

            try
            {
                using (Font sourceFont = Font.FromHfont(fontHandle))
                {
                    if (IsSameFont(sourceFont))
                    {
                        return;
                    }

                    ReplaceFont(
                        new Font(
                            sourceFont.FontFamily,
                            sourceFont.SizeInPoints,
                            sourceFont.Style,
                            GraphicsUnit.Point));
                }

                Invalidate();
            }
            catch (ArgumentException)
            {
                System.Diagnostics.Debug.WriteLine(
                    "VbeLineNumbers: WM_GETFONT returned an unsupported HFONT.");
            }
        }

        public void SetFontFromEditorSettings(
            string fontFace,
            float fontSizeInPoints)
        {
            if (string.IsNullOrWhiteSpace(fontFace) ||
                fontSizeInPoints <= 0.0f)
            {
                return;
            }

            try
            {
                using (Font sourceFont = new Font(
                    fontFace,
                    fontSizeInPoints,
                    FontStyle.Regular,
                    GraphicsUnit.Point))
                {
                    if (IsSameFont(sourceFont))
                    {
                        return;
                    }

                    ReplaceFont(
                        new Font(
                            sourceFont.FontFamily,
                            sourceFont.SizeInPoints,
                            sourceFont.Style,
                            GraphicsUnit.Point));
                }

                Invalidate();
            }
            catch (ArgumentException)
            {
                System.Diagnostics.Debug.WriteLine(
                    "VbeLineNumbers: VBE editor font setting is unsupported.");
            }
        }

        public void ShowOwnedBy(IntPtr ownerWindowHandle)
        {
            if (Visible)
            {
                return;
            }

            if (ownerWindowHandle == IntPtr.Zero)
            {
                Show();
                return;
            }

            _ownerWindow = new WindowHandleOwner(ownerWindowHandle);
            Show(_ownerWindow);
        }

        public int GetPreferredWidth(int largestLineNumber)
        {
            largestLineNumber = Math.Max(1, largestLineNumber);

            string sample = new string(
                '8',
                largestLineNumber.ToString().Length);

            Size size = TextRenderer.MeasureText(
                sample,
                Font,
                Size.Empty,
                TextFormatFlags.NoPadding |
                TextFormatFlags.NoClipping |
                TextFormatFlags.SingleLine);

            return Math.Max(
                28,
                size.Width + HorizontalPadding);
        }

        public float GetTextLineHeight()
        {
            using (Graphics graphics = CreateGraphics())
            {
                return Math.Max(1.0f, Font.GetHeight(graphics));
            }
        }

        protected override void WndProc(ref Message message)
        {
            const int WM_NCHITTEST = 0x0084;
            const int HTTRANSPARENT = -1;

            if (message.Msg == WM_NCHITTEST)
            {
                message.Result = new IntPtr(HTTRANSPARENT);
                return;
            }

            base.WndProc(ref message);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            int backgroundTop = (int)Math.Round(_topPadding);
            int backgroundHeight = Math.Max(
                1,
                (int)Math.Ceiling(_visibleLineCount * _lineHeight));

            using (Brush backgroundBrush = new SolidBrush(_backgroundColor))
            {
                e.Graphics.FillRectangle(
                    backgroundBrush,
                    0,
                    backgroundTop,
                    ClientSize.Width,
                    backgroundHeight);
            }

            for (int index = 0;
                 index < _visibleLineCount;
                 index++)
            {
                int lineNumber = _firstLine + index;
                float y = _topPadding + index * _lineHeight;

                Rectangle rectangle = new Rectangle(
                    0,
                    (int)Math.Round(y),
                    Math.Max(1, ClientSize.Width - RightPadding),
                    Math.Max(1, (int)Math.Ceiling(_lineHeight)));

                TextRenderer.DrawText(
                    e.Graphics,
                    lineNumber.ToString(),
                    Font,
                    rectangle,
                    SystemColors.ControlText,
                    TextFormatFlags.Right |
                    TextFormatFlags.Top |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.NoClipping |
                    TextFormatFlags.SingleLine);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                Font oldFont = Font;

                if (oldFont != null)
                {
                    oldFont.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        private bool IsSameFont(Font sourceFont)
        {
            return Font != null &&
                   Font.FontFamily.Name == sourceFont.FontFamily.Name &&
                   Math.Abs(Font.SizeInPoints - sourceFont.SizeInPoints) < 0.01f &&
                   Font.Style == sourceFont.Style;
        }

        private void ReplaceFont(Font newFont)
        {
            Font oldFont = Font;
            Font = newFont;

            if (oldFont != null)
            {
                oldFont.Dispose();
            }
        }

        private static Color LoadBackgroundColor()
        {
            Color defaultColor = Color.FromArgb(240, 240, 240);
            string configPath = Path.Combine(
                Path.GetDirectoryName(
                    Assembly.GetExecutingAssembly().Location),
                "VbeLineNumbers.ini");

            if (!File.Exists(configPath))
            {
                return defaultColor;
            }

            try
            {
                foreach (string rawLine in File.ReadAllLines(configPath))
                {
                    string line = rawLine.Trim();

                    if (line.Length == 0 ||
                        line.StartsWith(";", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    int separatorIndex = line.IndexOf('=');

                    if (separatorIndex <= 0)
                    {
                        continue;
                    }

                    string key = line.Substring(0, separatorIndex).Trim();

                    if (!string.Equals(
                            key,
                            "BackgroundColor",
                            StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    string value = line.Substring(separatorIndex + 1).Trim();

                    if (TryParseColor(value, out Color color))
                    {
                        return color;
                    }
                }
            }
            catch (IOException exception)
            {
                System.Diagnostics.Debug.WriteLine(
                    "VbeLineNumbers: Could not read VbeLineNumbers.ini. " +
                    exception.Message);
            }
            catch (UnauthorizedAccessException exception)
            {
                System.Diagnostics.Debug.WriteLine(
                    "VbeLineNumbers: Could not access VbeLineNumbers.ini. " +
                    exception.Message);
            }

            return defaultColor;
        }

        private static bool TryParseColor(
            string value,
            out Color color)
        {
            color = Color.Empty;

            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            try
            {
                Color parsedColor = ColorTranslator.FromHtml(value);

                if (parsedColor.A != 0 &&
                    parsedColor.ToArgb() != Color.Fuchsia.ToArgb())
                {
                    color = parsedColor;
                    return true;
                }
            }
            catch (Exception exception)
            {
                if (exception is ArgumentException ||
                    exception is FormatException)
                {
                    return false;
                }

                throw;
            }

            return false;
        }

        private sealed class WindowHandleOwner : IWin32Window
        {
            internal WindowHandleOwner(IntPtr handle)
            {
                Handle = handle;
            }

            public IntPtr Handle { get; private set; }
        }
    }
}
