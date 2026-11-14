using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;

namespace DeltaForceTuner
{
    internal static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(9, 13, 19);
        public static readonly Color Sidebar = Color.FromArgb(11, 17, 25);
        public static readonly Color Surface = Color.FromArgb(17, 24, 34);
        public static readonly Color Surface2 = Color.FromArgb(22, 31, 43);
        public static readonly Color Surface3 = Color.FromArgb(27, 38, 52);
        public static readonly Color Border = Color.FromArgb(36, 50, 67);
        public static readonly Color BorderSoft = Color.FromArgb(27, 38, 51);
        public static readonly Color Text = Color.FromArgb(233, 239, 247);
        public static readonly Color Muted = Color.FromArgb(143, 158, 179);
        public static readonly Color Faint = Color.FromArgb(98, 113, 134);
        public static readonly Color Accent = Color.FromArgb(255, 122, 26);
        public static readonly Color AccentDark = Color.FromArgb(204, 83, 8);
        public static readonly Color Green = Color.FromArgb(54, 214, 176);
        public static readonly Color Amber = Color.FromArgb(255, 179, 45);
        public static readonly Color Red = Color.FromArgb(255, 99, 99);
        public static readonly Color Blue = Color.FromArgb(61, 183, 255);
        public static readonly Color Pcore = Color.FromArgb(255, 145, 69);
        public static readonly Color Ecore = Color.FromArgb(61, 183, 255);
        public static readonly Color White = Color.FromArgb(255, 255, 255);

        private static readonly string UiFamily = PickFamily(new string[] { "Microsoft YaHei UI", "Microsoft YaHei", "Segoe UI" });
        private static readonly string NumFamily = PickFamily(new string[] { "Segoe UI Semibold", "Segoe UI", "Microsoft YaHei UI" });

        public static readonly Font H1 = new Font(UiFamily, 22f, FontStyle.Bold, GraphicsUnit.Point);
        public static readonly Font H2 = new Font(UiFamily, 14.5f, FontStyle.Bold, GraphicsUnit.Point);
        public static readonly Font H3 = new Font(UiFamily, 11.25f, FontStyle.Bold, GraphicsUnit.Point);
        public static readonly Font Body = new Font(UiFamily, 9.5f, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font BodyBold = new Font(UiFamily, 9.5f, FontStyle.Bold, GraphicsUnit.Point);
        public static readonly Font Small = new Font(UiFamily, 8.5f, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font SmallBold = new Font(UiFamily, 8.5f, FontStyle.Bold, GraphicsUnit.Point);
        public static readonly Font Tiny = new Font(UiFamily, 7.5f, FontStyle.Regular, GraphicsUnit.Point);
        public static readonly Font NumBig = new Font(NumFamily, 25f, FontStyle.Bold, GraphicsUnit.Point);
        public static readonly Font NumMid = new Font(NumFamily, 15f, FontStyle.Bold, GraphicsUnit.Point);
        public static readonly Font NumSmall = new Font(NumFamily, 10.5f, FontStyle.Bold, GraphicsUnit.Point);

        private static string PickFamily(string[] names)
        {
            try
            {
                InstalledFontCollection fonts = new InstalledFontCollection();
                for (int n = 0; n < names.Length; n++)
                {
                    for (int i = 0; i < fonts.Families.Length; i++)
                    {
                        if (string.Equals(fonts.Families[i].Name, names[n], StringComparison.OrdinalIgnoreCase))
                            return names[n];
                    }
                }
            }
            catch
            {
            }
            return "Segoe UI";
        }

        public static Color WithAlpha(Color c, int alpha)
        {
            return Color.FromArgb(Math.Max(0, Math.Min(255, alpha)), c.R, c.G, c.B);
        }

        public static GraphicsPath RoundPath(Rectangle r, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            if (radius <= 0)
            {
                path.AddRectangle(r);
                return path;
            }
            int d = radius * 2;
            if (d > r.Width) d = r.Width;
            if (d > r.Height) d = r.Height;
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        public static void FillRound(Graphics g, Rectangle r, int radius, Color color)
        {
            using (GraphicsPath p = RoundPath(r, radius))
            using (SolidBrush b = new SolidBrush(color))
            {
                g.FillPath(b, p);
            }
        }

        public static void DrawRound(Graphics g, Rectangle r, int radius, Color color, float width)
        {
            using (GraphicsPath p = RoundPath(r, radius))
            using (Pen pen = new Pen(color, width))
            {
                g.DrawPath(pen, p);
            }
        }
    }

    internal static class UiText
    {
        public static void Draw(Graphics g, string text, Font font, Color color, Rectangle bounds)
        {
            Draw(g, text, font, color, bounds, ContentAlignment.MiddleLeft, false);
        }

        public static void Draw(Graphics g, string text, Font font, Color color, Rectangle bounds, ContentAlignment align, bool wrap)
        {
            using (StringFormat sf = new StringFormat())
            using (SolidBrush brush = new SolidBrush(color))
            {
                sf.Trimming = StringTrimming.EllipsisCharacter;
                sf.FormatFlags = wrap ? 0 : StringFormatFlags.NoWrap;
                sf.LineAlignment = StringAlignment.Center;
                sf.Alignment = StringAlignment.Near;
                if (align == ContentAlignment.MiddleCenter || align == ContentAlignment.TopCenter || align == ContentAlignment.BottomCenter)
                    sf.Alignment = StringAlignment.Center;
                else if (align == ContentAlignment.MiddleRight || align == ContentAlignment.TopRight || align == ContentAlignment.BottomRight)
                    sf.Alignment = StringAlignment.Far;
                if (align == ContentAlignment.TopLeft || align == ContentAlignment.TopCenter || align == ContentAlignment.TopRight)
                    sf.LineAlignment = StringAlignment.Near;
                else if (align == ContentAlignment.BottomLeft || align == ContentAlignment.BottomCenter || align == ContentAlignment.BottomRight)
                    sf.LineAlignment = StringAlignment.Far;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.DrawString(text, font, brush, bounds, sf);
            }
        }

        public static Size Measure(string text, Font font, int maxWidth)
        {
            using (Graphics g = Graphics.FromHwnd(IntPtr.Zero))
            {
                return TextRenderer.MeasureText(g, text, font, new Size(maxWidth, 2000), TextFormatFlags.WordBreak | TextFormatFlags.NoPadding);
            }
        }
    }
}
