using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace DeltaForceTuner
{
    internal class Card : Panel
    {
        public int Radius = 14;
        public Color FillColor = Theme.Surface;
        public Color StrokeColor = Theme.BorderSoft;
        public Color AccentLine = Color.Empty;

        public Card()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRound(e.Graphics, r, Radius, FillColor);
            if (StrokeColor.A > 0) Theme.DrawRound(e.Graphics, r, Radius, StrokeColor, 1f);
            if (AccentLine != Color.Empty)
            {
                using (GraphicsPath p = Theme.RoundPath(new Rectangle(0, 0, Width - 1, Height - 1), Radius))
                {
                    e.Graphics.SetClip(p);
                    using (SolidBrush b = new SolidBrush(AccentLine))
                    {
                        e.Graphics.FillRectangle(b, 0, 0, 3, Height);
                    }
                    e.Graphics.ResetClip();
                }
            }
            base.OnPaint(e);
        }
    }

    internal class HeroCard : Card
    {
        public HeroCard()
        {
            FillColor = Theme.Surface;
            StrokeColor = Theme.Border;
            Radius = 16;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            using (GraphicsPath p = Theme.RoundPath(r, Radius))
            {
                using (LinearGradientBrush b = new LinearGradientBrush(r, Color.FromArgb(24, 35, 49), Color.FromArgb(35, 25, 19), 12f))
                {
                    e.Graphics.FillPath(b, p);
                }
                e.Graphics.SetClip(p);
                using (SolidBrush glow = new SolidBrush(Color.FromArgb(34, Theme.Accent)))
                {
                    e.Graphics.FillEllipse(glow, Width - 250, -110, 360, 280);
                }
                using (SolidBrush glow2 = new SolidBrush(Color.FromArgb(14, Theme.Blue)))
                {
                    e.Graphics.FillEllipse(glow2, Width - 90, -40, 220, 220);
                }
                e.Graphics.ResetClip();
            }
            Theme.DrawRound(e.Graphics, r, Radius, Theme.Border, 1f);
        }
    }

    internal class RoundButton : Control
    {
        public Color FillColor = Theme.Accent;
        public Color HoverColor = Color.FromArgb(255, 140, 55);
        public Color DownColor = Theme.AccentDark;
        public Color TextColor = Color.White;
        public Color BorderColor = Color.Empty;
        public int Radius = 9;
        public bool Ghost;
        private bool _hover;
        private bool _down;

        public RoundButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Size = new Size(120, 38);
            Font = Theme.BodyBold;
            Cursor = Cursors.Hand;
            TabStop = true;
            BackColor = Theme.Surface;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _down = true; Focus(); Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }
        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                OnClick(EventArgs.Empty);
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill;
            if (!Enabled) fill = Theme.Surface2;
            else if (Ghost) fill = _down ? Theme.Surface3 : (_hover ? Theme.Surface2 : Color.Transparent);
            else fill = _down ? DownColor : (_hover ? HoverColor : FillColor);
            if (fill.A > 0) Theme.FillRound(e.Graphics, r, Radius, fill);
            Color border = BorderColor;
            if (Ghost && border == Color.Empty) border = Theme.Border;
            if (border.A > 0) Theme.DrawRound(e.Graphics, r, Radius, border, 1f);
            Color tc = Enabled ? TextColor : Theme.Faint;
            if (Ghost && Enabled) tc = _hover ? Theme.Text : Theme.Muted;
            UiText.Draw(e.Graphics, Text, Font, tc, r, ContentAlignment.MiddleCenter, false);
            base.OnPaint(e);
        }
    }

    internal class ToggleSwitch : Control
    {
        private bool _checked;
        private float _progress;
        private readonly Timer _anim;
        public event EventHandler CheckedChanged;

        public ToggleSwitch()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Size = new Size(48, 26);
            Cursor = Cursors.Hand;
            TabStop = true;
            _anim = new Timer();
            _anim.Interval = 15;
            _anim.Tick += delegate
            {
                float target = _checked ? 1f : 0f;
                float d = target - _progress;
                if (Math.Abs(d) < 0.04f)
                {
                    _progress = target;
                    _anim.Stop();
                }
                else
                {
                    _progress += d * 0.35f;
                }
                Invalidate();
            };
        }

        public bool Checked
        {
            get { return _checked; }
            set
            {
                if (_checked == value) return;
                _checked = value;
                _anim.Start();
                if (CheckedChanged != null) CheckedChanged(this, EventArgs.Empty);
            }
        }

        public void SetCheckedSilently(bool value)
        {
            if (_checked == value) return;
            _checked = value;
            _progress = value ? 1f : 0f;
            Invalidate();
        }

        protected override void OnClick(EventArgs e)
        {
            if (Enabled)
            {
                Checked = !Checked;
                Focus();
            }
            base.OnClick(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Space)
            {
                if (Enabled) Checked = !Checked;
                e.Handled = true;
            }
            base.OnKeyDown(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int w = Width;
            int h = Height;
            int trackH = Math.Min(26, h);
            int trackW = Math.Min(48, w);
            Rectangle track = new Rectangle((w - trackW) / 2, (h - trackH) / 2, trackW, trackH);
            Color on = Enabled ? Theme.Accent : Color.FromArgb(90, 68, 48);
            Color off = Enabled ? Theme.Surface3 : Theme.Surface2;
            Color trackColor = Blend(off, on, _progress);
            Theme.FillRound(e.Graphics, track, trackH / 2, trackColor);
            Theme.DrawRound(e.Graphics, track, trackH / 2, Enabled ? Theme.WithAlpha(Theme.White, 18) : Theme.BorderSoft, 1f);
            int thumb = trackH - 6;
            int tx = track.X + 3 + (int)Math.Round(_progress * (track.Width - thumb - 6));
            Rectangle tr = new Rectangle(tx, track.Y + 3, thumb, thumb);
            using (SolidBrush b = new SolidBrush(Enabled ? Color.White : Theme.Faint))
            {
                e.Graphics.FillEllipse(b, tr);
            }
            if (Focused && Enabled)
            {
                using (Pen p = new Pen(Theme.WithAlpha(Theme.Accent, 120), 1f))
                {
                    p.DashStyle = DashStyle.Dot;
                    e.Graphics.DrawRectangle(p, 0, 0, Width - 1, Height - 1);
                }
            }
            base.OnPaint(e);
        }

        private static Color Blend(Color a, Color b, float t)
        {
            t = Math.Max(0f, Math.Min(1f, t));
            return Color.FromArgb(
                (int)(a.A + (b.A - a.A) * t),
                (int)(a.R + (b.R - a.R) * t),
                (int)(a.G + (b.G - a.G) * t),
                (int)(a.B + (b.B - a.B) * t));
        }
    }

    internal class Pill : Control
    {
        public Color FillColor = Theme.Surface3;
        public Color TextColor = Theme.Muted;
        public Color BorderColor = Color.Empty;
        public int Radius = 8;

        public Pill()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Font = Theme.Tiny;
            Height = 22;
            Width = 58;
            BackColor = Theme.Surface;
        }

        protected override void OnTextChanged(EventArgs e)
        {
            base.OnTextChanged(e);
            UpdateSize();
        }

        public void SetText(string text)
        {
            Text = text;
            UpdateSize();
        }

        private void UpdateSize()
        {
            Size s = TextRenderer.MeasureText(Text, Font);
            Width = s.Width + 18;
            Height = 22;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRound(e.Graphics, r, Radius, FillColor);
            if (BorderColor.A > 0) Theme.DrawRound(e.Graphics, r, Radius, BorderColor, 1f);
            UiText.Draw(e.Graphics, Text, Font, TextColor, new Rectangle(9, 0, Width - 15, Height), ContentAlignment.MiddleLeft, false);
            base.OnPaint(e);
        }
    }

    internal class NavButton : Control
    {
        public string Glyph = "";
        public bool Active;
        private bool _hover;

        public NavButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Height = 44;
            Font = Theme.Body;
            Cursor = Cursors.Hand;
            TabStop = true;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnClick(EventArgs e) { Focus(); base.OnClick(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width, Height);
            if (Active)
            {
                Theme.FillRound(e.Graphics, new Rectangle(8, 3, Width - 16, Height - 6), 9, Theme.Surface2);
                using (SolidBrush b = new SolidBrush(Theme.Accent))
                {
                    e.Graphics.FillRectangle(b, 0, 11, 3, Height - 22);
                }
            }
            else if (_hover)
            {
                Theme.FillRound(e.Graphics, new Rectangle(8, 3, Width - 16, Height - 6), 9, Theme.Surface);
            }
            Color fg = Active ? Theme.Text : (_hover ? Theme.Text : Theme.Muted);
            Color icon = Active ? Theme.Accent : (_hover ? Theme.Text : Theme.Faint);
            if (Glyph.Length > 0)
            {
                using (Font iconFont = new Font("Segoe MDL2 Assets", 12f, FontStyle.Regular, GraphicsUnit.Point))
                {
                    UiText.Draw(e.Graphics, Glyph, iconFont, icon, new Rectangle(22, 0, 24, Height), ContentAlignment.MiddleCenter, false);
                }
            }
            UiText.Draw(e.Graphics, Text, Font, fg, new Rectangle(54, 0, Width - 64, Height), ContentAlignment.MiddleLeft, false);
            base.OnPaint(e);
        }
    }

    internal class ScoreRing : Control
    {
        private int _value;
        public string Caption = "推荐完成度";

        public ScoreRing()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Size = new Size(112, 112);
            BackColor = Color.FromArgb(62, 41, 27);
        }

        public int Value
        {
            get { return _value; }
            set { _value = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            if (Width <= 4 || Height <= 4) return;
            using (GraphicsPath p = new GraphicsPath())
            {
                p.AddEllipse(0, 0, Width - 1, Height - 1);
                Region old = Region;
                Region = new Region(p);
                if (old != null) old.Dispose();
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush back = new SolidBrush(Color.FromArgb(31, 26, 22)))
            {
                e.Graphics.FillEllipse(back, 1, 1, Width - 3, Height - 3);
            }
            Rectangle r = new Rectangle(8, 8, Width - 17, Height - 17);
            using (Pen bg = new Pen(Theme.Surface3, 8f))
            {
                e.Graphics.DrawArc(bg, r, -90, 360);
            }
            Color c = _value >= 80 ? Theme.Green : (_value >= 40 ? Theme.Amber : Theme.Accent);
            using (Pen p = new Pen(c, 8f))
            {
                p.StartCap = LineCap.Round;
                p.EndCap = LineCap.Round;
                e.Graphics.DrawArc(p, r, -90, Math.Max(4, (int)(360f * _value / 100f)));
            }
            UiText.Draw(e.Graphics, _value.ToString(), Theme.NumBig, Theme.Text, new Rectangle(0, 24, Width, 38), ContentAlignment.MiddleCenter, false);
            UiText.Draw(e.Graphics, "分", Theme.Small, Theme.Muted, new Rectangle(0, 60, Width, 20), ContentAlignment.MiddleCenter, false);
            UiText.Draw(e.Graphics, Caption, Theme.Tiny, Theme.Faint, new Rectangle(0, Height - 24, Width, 18), ContentAlignment.MiddleCenter, false);
            base.OnPaint(e);
        }
    }

    internal class CpuMapControl : Control
    {
        public CpuTopology Topology;
        public ulong SelectedMask;
        public ulong AceMask;
        public bool GroupMode;
        public int SelectedCoreIndex = -1;
        public event EventHandler CoreClicked;
        private List<Rectangle> _hitRects = new List<Rectangle>();
        private List<int> _hitCores = new List<int>();
        private int _hover = -1;

        public CpuMapControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = 176;
            BackColor = Theme.Surface;
            Cursor = Cursors.Hand;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int found = -1;
            for (int i = 0; i < _hitRects.Count; i++)
            {
                if (_hitRects[i].Contains(e.Location)) { found = _hitCores[i]; break; }
            }
            if (found != _hover)
            {
                _hover = found;
                Invalidate();
            }
            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hover = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseClick(MouseEventArgs e)
        {
            for (int i = 0; i < _hitRects.Count; i++)
            {
                if (_hitRects[i].Contains(e.Location))
                {
                    int index = _hitCores[i];
                    if (Topology != null)
                    {
                        CpuCore core = Topology.FindCore(index);
                        if (core != null && Topology.IsHybrid && !core.IsEfficiency) return;
                    }
                    SelectedCoreIndex = index;
                    Invalidate();
                    if (CoreClicked != null) CoreClicked(index, EventArgs.Empty);
                    break;
                }
            }
            base.OnMouseClick(e);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            _hitRects.Clear();
            _hitCores.Clear();
            if (Topology == null || Topology.Cores.Count == 0) return;

            int labelW = 92;
            int gap = 6;
            int areaW = Width - labelW - 4;
            int pCount = 0;
            int eCount = 0;
            for (int i = 0; i < Topology.Cores.Count; i++)
            {
                if (Topology.Cores[i].IsEfficiency) eCount++; else pCount++;
            }

            int rowY = 12;
            if (pCount > 0)
            {
                DrawRow(e.Graphics, "P-Core", "性能核 · " + pCount + " 核", Topology.PCores, labelW, areaW, gap, rowY, Theme.Pcore);
                rowY += 70;
            }
            if (eCount > 0)
            {
                DrawRow(e.Graphics, "E-Core", "能效核 · " + eCount + " 核", Topology.ECores, labelW, areaW, gap, rowY, Theme.Ecore);
                rowY += 70;
            }

            string note = GroupMode ? "ACE 目标：全部 E 核" : "ACE 目标：单颗 E 核";
            if (eCount == 0) note = "未检测到大小核混合架构，使用末尾逻辑核心";
            UiText.Draw(e.Graphics, note, Theme.Tiny, Theme.Faint, new Rectangle(0, Math.Min(Height - 20, rowY + 2), Width, 18), ContentAlignment.MiddleLeft, false);
            base.OnPaint(e);
        }

        private void DrawRow(Graphics g, string title, string sub, List<CpuCore> cores, int labelW, int areaW, int gap, int y, Color color)
        {
            UiText.Draw(g, title, Theme.SmallBold, color, new Rectangle(0, y + 2, labelW - 8, 18), ContentAlignment.MiddleLeft, false);
            UiText.Draw(g, sub, Theme.Tiny, Theme.Faint, new Rectangle(0, y + 20, labelW - 8, 16), ContentAlignment.MiddleLeft, false);
            if (cores == null || cores.Count == 0) return;
            int n = cores.Count;
            int w = (areaW - gap * (n - 1)) / n;
            if (w > 74) w = 74;
            if (w < 34) w = 34;
            int totalW = w * n + gap * (n - 1);
            int startX = labelW + Math.Max(0, (areaW - totalW) / 2);
            for (int i = 0; i < n; i++)
            {
                CpuCore core = cores[i];
                Rectangle r = new Rectangle(startX + i * (w + gap), y, w, 52);
                bool selected = (SelectedMask & core.Mask) != 0;
                bool ace = (AceMask & core.Mask) != 0;
                Color fill = selected ? Theme.WithAlpha(color, 54) : Theme.Surface2;
                Color stroke = selected ? color : Theme.BorderSoft;
                if (_hover == core.Index) fill = selected ? Theme.WithAlpha(color, 75) : Theme.Surface3;
                Theme.FillRound(g, r, 8, fill);
                Theme.DrawRound(g, r, 8, stroke, selected ? 1.4f : 1f);
                UiText.Draw(g, core.Label, Theme.SmallBold, selected ? Theme.Text : Theme.Muted, new Rectangle(r.X, r.Y + 7, r.Width, 18), ContentAlignment.MiddleCenter, false);
                UiText.Draw(g, core.LogicalCount + " 线程", Theme.Tiny, Theme.Faint, new Rectangle(r.X, r.Y + 26, r.Width, 15), ContentAlignment.MiddleCenter, false);
                if (ace)
                {
                    using (SolidBrush b = new SolidBrush(Theme.Accent))
                    {
                        g.FillEllipse(b, r.Right - 10, r.Y + 6, 5, 5);
                    }
                }
                _hitRects.Add(r);
                _hitCores.Add(core.Index);
            }
        }
    }

    internal class ProcessListControl : Control
    {
        private List<AceProcessInfo> _items = new List<AceProcessInfo>();

        public ProcessListControl()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = 150;
            BackColor = Theme.Surface;
        }

        public void SetItems(List<AceProcessInfo> items)
        {
            _items = items == null ? new List<AceProcessInfo>() : items;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            int rowH = 30;
            int headerH = 26;
            int w = Width - 2;
            int c1 = 34;
            int c2 = 14;
            int c3 = 25;
            UiText.Draw(e.Graphics, "进程", Theme.Tiny, Theme.Faint, new Rectangle(10, 0, w * c1 / 100, headerH), ContentAlignment.MiddleLeft, false);
            UiText.Draw(e.Graphics, "PID", Theme.Tiny, Theme.Faint, new Rectangle(w * c1 / 100 + 8, 0, w * c2 / 100, headerH), ContentAlignment.MiddleLeft, false);
            UiText.Draw(e.Graphics, "当前亲和性", Theme.Tiny, Theme.Faint, new Rectangle(w * (c1 + c2) / 100 + 8, 0, w * c3 / 100, headerH), ContentAlignment.MiddleLeft, false);
            UiText.Draw(e.Graphics, "状态", Theme.Tiny, Theme.Faint, new Rectangle(w * (c1 + c2 + c3) / 100 + 8, 0, w - w * (c1 + c2 + c3) / 100 - 12, headerH), ContentAlignment.MiddleLeft, false);
            using (Pen p = new Pen(Theme.BorderSoft, 1f))
            {
                e.Graphics.DrawLine(p, 8, headerH - 1, Width - 8, headerH - 1);
            }

            if (_items.Count == 0)
            {
                UiText.Draw(e.Graphics, "尚未检测到 ACE 进程。启动游戏后，ACE 客户端会自动出现。", Theme.Small, Theme.Faint, new Rectangle(10, headerH, Width - 20, 44), ContentAlignment.MiddleLeft, false);
                return;
            }

            for (int i = 0; i < _items.Count && i < 4; i++)
            {
                AceProcessInfo it = _items[i];
                int y = headerH + i * rowH;
                if (i % 2 == 0) Theme.FillRound(e.Graphics, new Rectangle(6, y + 1, Width - 12, rowH - 2), 6, Theme.WithAlpha(Theme.Surface2, 70));
                Color status = it.StatusColor;
                UiText.Draw(e.Graphics, it.Name, Theme.Small, Theme.Text, new Rectangle(10, y, w * c1 / 100, rowH), ContentAlignment.MiddleLeft, false);
                UiText.Draw(e.Graphics, it.Pid.ToString(), Theme.Small, Theme.Muted, new Rectangle(w * c1 / 100 + 8, y, w * c2 / 100, rowH), ContentAlignment.MiddleLeft, false);
                UiText.Draw(e.Graphics, it.AffinityText, Theme.Small, Theme.Muted, new Rectangle(w * (c1 + c2) / 100 + 8, y, w * c3 / 100, rowH), ContentAlignment.MiddleLeft, false);
                UiText.Draw(e.Graphics, it.Status, Theme.SmallBold, status, new Rectangle(w * (c1 + c2 + c3) / 100 + 8, y, w - w * (c1 + c2 + c3) / 100 - 12, rowH), ContentAlignment.MiddleLeft, false);
            }
            if (_items.Count > 4)
            {
                UiText.Draw(e.Graphics, "另有 " + (_items.Count - 4) + " 个 ACE 进程…", Theme.Tiny, Theme.Faint, new Rectangle(10, headerH + 4 * rowH, Width - 20, 20), ContentAlignment.MiddleLeft, false);
            }
            base.OnPaint(e);
        }
    }
}
