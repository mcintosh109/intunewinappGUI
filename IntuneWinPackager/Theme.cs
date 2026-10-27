using System;
using System.Drawing;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace IntuneWinPackager
{
    /// <summary>
    /// Dark and light colour schemes. WinForms on .NET Framework has no built-in dark mode, so the colours are
    /// applied to each control by hand, and the title bar, scroll bars and list header are themed through Windows.
    /// </summary>
    static class Theme
    {
        public static bool IsDark { get; private set; }

        static readonly Color DarkBack = Color.FromArgb(32, 32, 32);
        static readonly Color DarkSurface = Color.FromArgb(43, 43, 43);
        static readonly Color DarkText = Color.FromArgb(230, 230, 230);
        static readonly Color DarkMutedText = Color.FromArgb(160, 160, 160);
        static readonly Color DarkBorder = Color.FromArgb(70, 70, 70);
        static readonly Color DarkButton = Color.FromArgb(55, 55, 55);
        static readonly Color DarkButtonHover = Color.FromArgb(70, 70, 70);
        static readonly Color DarkHeader = Color.FromArgb(50, 50, 50);
        static readonly Color Accent = Color.FromArgb(0, 103, 192);
        static readonly Color AccentHover = Color.FromArgb(25, 125, 215);

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
            if (!IsDark)
                return;
            if (!button.Enabled)
            {
                // Greyed-out text is unreadable on blue, so a disabled button looks like any other.
                button.BackColor = DarkButton;
                button.FlatAppearance.BorderColor = DarkBorder;
                return;
            }
            button.BackColor = Accent;
            button.ForeColor = Color.White;
            button.FlatAppearance.BorderColor = Accent;
            button.FlatAppearance.MouseOverBackColor = AccentHover;
            button.FlatAppearance.MouseDownBackColor = Accent;
        }

        static void ApplyTo(Control control)
        {
            if (control is Form || control is Panel || control is Label)
            {
                control.BackColor = IsDark ? DarkBack : SystemColors.Control;
                control.ForeColor = IsDark ? DarkText : SystemColors.ControlText;
            }

            var label = control as Label;
            if (label != null && "muted".Equals(label.Tag))
                label.ForeColor = IsDark ? DarkMutedText : SystemColors.GrayText;

            var textBox = control as TextBox;
            if (textBox != null)
            {
                textBox.BackColor = IsDark ? DarkSurface : SystemColors.Window;
                textBox.ForeColor = IsDark ? DarkText : SystemColors.WindowText;
                textBox.BorderStyle = IsDark ? BorderStyle.FixedSingle : BorderStyle.Fixed3D;
                SetDarkScrollBars(textBox, IsDark);
            }

            var button = control as Button;
            if (button != null)
            {
                if (IsDark)
                {
                    button.FlatStyle = FlatStyle.Flat;
                    button.BackColor = DarkButton;
                    button.ForeColor = DarkText;
                    button.FlatAppearance.BorderColor = DarkBorder;
                    button.FlatAppearance.MouseOverBackColor = DarkButtonHover;
                    button.FlatAppearance.MouseDownBackColor = DarkBorder;
                }
                else
                {
                    button.FlatStyle = FlatStyle.Standard;
                    button.BackColor = SystemColors.Control;
                    button.ForeColor = SystemColors.ControlText;
                    button.UseVisualStyleBackColor = true;
                }
            }

            var checkBox = control as CheckBox;
            if (checkBox != null)
            {
                checkBox.FlatStyle = IsDark ? FlatStyle.Flat : FlatStyle.Standard;
                checkBox.BackColor = IsDark ? DarkBack : SystemColors.Control;
                checkBox.ForeColor = IsDark ? DarkText : SystemColors.ControlText;
            }

            var progressBar = control as ThemedProgressBar;
            if (progressBar != null)
                progressBar.Dark = IsDark;

            var listView = control as ListView;
            if (listView != null)
            {
                listView.BackColor = IsDark ? DarkSurface : SystemColors.Window;
                listView.ForeColor = IsDark ? DarkText : SystemColors.WindowText;
                // Windows draws grid lines in a fixed light colour, which looks harsh on a dark background.
                listView.GridLines = !IsDark;
                PrepareListView(listView);
                SetDarkScrollBars(listView, IsDark);
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
                if (!IsDark)
                {
                    e.DrawDefault = true;
                    return;
                }
                using (var back = new SolidBrush(DarkHeader))
                using (var line = new Pen(DarkBorder))
                {
                    e.Graphics.FillRectangle(back, e.Bounds);
                    e.Graphics.DrawLine(line, e.Bounds.Right - 1, e.Bounds.Top, e.Bounds.Right - 1, e.Bounds.Bottom);
                    e.Graphics.DrawLine(line, e.Bounds.Left, e.Bounds.Bottom - 1, e.Bounds.Right, e.Bounds.Bottom - 1);
                }
                var flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.SingleLine
                    | (e.Header.TextAlign == HorizontalAlignment.Right ? TextFormatFlags.Right : TextFormatFlags.Left);
                var textBounds = Rectangle.Inflate(e.Bounds, -6, 0);
                TextRenderer.DrawText(e.Graphics, e.Header.Text, listView.Font, textBounds, DarkText, flags);
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
    /// The Windows progress bar is always light grey and green, so in dark mode it is drawn here instead.
    /// In light mode Windows draws it as normal.
    /// </summary>
    class ThemedProgressBar : ProgressBar
    {
        static readonly Color Track = Color.FromArgb(43, 43, 43);
        static readonly Color Border = Color.FromArgb(70, 70, 70);
        static readonly Color Fill = Color.FromArgb(0, 103, 192);

        bool dark;

        public bool Dark
        {
            get { return dark; }
            set
            {
                if (dark == value)
                    return;
                dark = value;
                SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, value);
                if (IsHandleCreated)
                    RecreateHandle();
                Invalidate();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            if (!dark)
            {
                base.OnPaint(e);
                return;
            }
            var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            using (var track = new SolidBrush(Track))
            using (var border = new Pen(Border))
            using (var fill = new SolidBrush(Fill))
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
