using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace IntuneWinPackager
{
    /// <summary>
    /// Dark and light colour schemes. WinForms on .NET Framework has no built-in dark mode, so the colours are
    /// applied to each control by hand, and the title bar and scroll bars are themed through Windows.
    /// Both modes use the same flat control styles and only swap colours, so nothing changes size or moves
    /// when switching.
    /// </summary>
    static class Theme
    {
        class Palette
        {
            public Color Back, Surface, Text, MutedText, Border, Button, ButtonHover, ButtonDown, Header, Track;
        }

        static readonly Palette Dark = new Palette
        {
            Back = Color.FromArgb(32, 32, 32),
            Surface = Color.FromArgb(43, 43, 43),
            Text = Color.FromArgb(230, 230, 230),
            MutedText = Color.FromArgb(160, 160, 160),
            Border = Color.FromArgb(70, 70, 70),
            Button = Color.FromArgb(55, 55, 55),
            ButtonHover = Color.FromArgb(70, 70, 70),
            ButtonDown = Color.FromArgb(80, 80, 80),
            Header = Color.FromArgb(50, 50, 50),
            Track = Color.FromArgb(43, 43, 43)
        };

        static readonly Palette Light = new Palette
        {
            Back = Color.FromArgb(243, 243, 243),
            Surface = Color.White,
            Text = Color.FromArgb(26, 26, 26),
            MutedText = Color.FromArgb(110, 110, 110),
            Border = Color.FromArgb(200, 200, 200),
            Button = Color.FromArgb(253, 253, 253),
            ButtonHover = Color.FromArgb(229, 241, 251),
            ButtonDown = Color.FromArgb(204, 228, 247),
            Header = Color.FromArgb(243, 243, 243),
            Track = Color.FromArgb(230, 230, 230)
        };

        static readonly Color Accent = Color.FromArgb(0, 103, 192);
        static readonly Color AccentHover = Color.FromArgb(25, 125, 215);

        public static bool IsDark { get; private set; }

        static Palette Colors { get { return IsDark ? Dark : Light; } }

        /// <summary>Applies the scheme to the form and every control on it.</summary>
        public static void Apply(Form form, bool dark)
        {
            IsDark = dark;
            form.SuspendLayout();
            ApplyTo(form);
            form.ResumeLayout();
            SetDarkTitleBar(form, dark);
            form.Invalidate(true);

            // Windows only repaints the title bar when the frame changes, so nudge it when switching while open.
            if (form.Visible && form.WindowState == FormWindowState.Normal)
            {
                form.Width++;
                form.Width--;
            }
        }

        /// <summary>Gives a button the blue "main action" look.</summary>
        public static void StyleAccent(Button button)
        {
            if (!button.Enabled)
            {
                // Greyed-out text is unreadable on blue, so a disabled button looks like any other.
                StyleButton(button);
                return;
            }
            button.BackColor = Accent;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = Accent;
            button.FlatAppearance.MouseOverBackColor = AccentHover;
            button.FlatAppearance.MouseDownBackColor = Accent;
        }

        static void StyleButton(Button button)
        {
            button.FlatStyle = FlatStyle.Flat;
            button.BackColor = Colors.Button;
            button.ForeColor = Colors.Text;
            button.FlatAppearance.BorderColor = Colors.Border;
            button.FlatAppearance.MouseOverBackColor = Colors.ButtonHover;
            button.FlatAppearance.MouseDownBackColor = Colors.ButtonDown;
        }

        static void ApplyTo(Control control)
        {
            if (control is Form || control is Panel || control is Label)
            {
                control.BackColor = Colors.Back;
                control.ForeColor = Colors.Text;
            }

            var label = control as Label;
            if (label != null && "muted".Equals(label.Tag))
                label.ForeColor = Colors.MutedText;

            var textBox = control as TextBox;
            if (textBox != null)
            {
                textBox.BorderStyle = BorderStyle.FixedSingle;
                textBox.BackColor = Colors.Surface;
                textBox.ForeColor = Colors.Text;
                SetDarkScrollBars(textBox, IsDark);
            }

            var button = control as Button;
            if (button != null)
                StyleButton(button);

            var checkBox = control as CheckBox;
            if (checkBox != null)
            {
                checkBox.FlatStyle = FlatStyle.Flat;
                checkBox.BackColor = Colors.Back;
                checkBox.ForeColor = Colors.Text;
            }

            var progressBar = control as ThemedProgressBar;
            if (progressBar != null)
            {
                progressBar.TrackColor = Colors.Track;
                progressBar.BorderColor = Colors.Border;
                progressBar.FillColor = Accent;
                progressBar.Invalidate();
            }

            var listView = control as ListView;
            if (listView != null)
            {
                listView.BackColor = Colors.Surface;
                listView.ForeColor = Colors.Text;
                // Windows draws grid lines in a fixed light colour, which looks harsh on a dark background.
                listView.GridLines = !IsDark;
                PrepareListView(listView);
                SetDarkScrollBars(listView, IsDark);
                listView.Invalidate();
            }

            foreach (Control child in control.Controls)
                ApplyTo(child);
        }

        /// <summary>
        /// The column header ignores BackColor, so it's drawn here; the rows are still drawn by Windows.
        /// </summary>
        static void PrepareListView(ListView listView)
        {
            if (listView.OwnerDraw)
                return;

            listView.OwnerDraw = true;
            listView.DrawItem += (s, e) => e.DrawDefault = true;
            listView.DrawSubItem += (s, e) => e.DrawDefault = true;
            listView.DrawColumnHeader += (s, e) =>
            {
                using (var back = new SolidBrush(Colors.Header))
                using (var line = new Pen(Colors.Border))
                {
                    e.Graphics.FillRectangle(back, e.Bounds);
                    e.Graphics.DrawLine(line, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom);
                    e.Graphics.DrawLine(line, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                }
                var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine
                    | (e.Header.TextAlign == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left);
                var textBounds = Rectangle.Inflate(e.Bounds, -6, 0);
                TextRenderer.DrawText(e.Graphics, e.Header.Text, listView.Font, textBounds, Colors.Text, flags);
            };

            // Owner-drawn list views flicker and leave stale text behind while hovering unless double buffered.
            typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
                .SetValue(listView, true, null);
        }

        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        static extern int SetWindowTheme(IntPtr hwnd, string pszSubAppName, string pszSubIdList);

        [DllImport("dwmapi.dll")]
        static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

        static void SetDarkScrollBars(Control control, bool dark)
        {
            try
            {
                SetWindowTheme(control.Handle, dark ? "DarkMode_Explorer" : "Explorer", null);
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
                // Not on Windows (or a very old version), so scroll bars just stay light.
            }
        }

        static void SetDarkTitleBar(Form form, bool dark)
        {
            try
            {
                // DWMWA_USE_IMMERSIVE_DARK_MODE is 20 on Windows 10 20H1 and later, 19 on earlier Windows 10 builds.
                int value = dark ? 1 : 0;
                if (DwmSetWindowAttribute(form.Handle, 20, ref value, sizeof(int)) != 0)
                    DwmSetWindowAttribute(form.Handle, 19, ref value, sizeof(int));
            }
            catch (Exception ex) when (ex is DllNotFoundException || ex is EntryPointNotFoundException)
            {
            }
        }
    }

    /// <summary>
    /// The Windows progress bar is always light grey and green, so it is drawn here in the theme's colours.
    /// </summary>
    class ThemedProgressBar : ProgressBar
    {
        public Color TrackColor { get; set; } = Color.FromArgb(230, 230, 230);
        public Color BorderColor { get; set; } = Color.FromArgb(200, 200, 200);
        public Color FillColor { get; set; } = Color.FromArgb(0, 103, 192);

        public ThemedProgressBar()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var track = new SolidBrush(TrackColor))
            using (var border = new Pen(BorderColor))
            using (var fill = new SolidBrush(FillColor))
            {
                e.Graphics.FillRectangle(track, bounds);
                int range = Maximum - Minimum;
                if (range > 0 && Value > Minimum)
                {
                    int width = (int)((long)(bounds.Width - 1) * (Value - Minimum) / range);
                    e.Graphics.FillRectangle(fill, 1, 1, width, bounds.Height - 1);
                }
                e.Graphics.DrawRectangle(border, bounds);
            }
        }
    }
}
