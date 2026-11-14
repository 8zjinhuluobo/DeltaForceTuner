using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using DeltaForceTuner;

namespace DeltaForceTuner.Setup
{
    internal class SetupCard : Panel
    {
        public Color FillColor = Theme.Surface;
        public Color StrokeColor = Theme.BorderSoft;
        public int Radius = 12;

        public SetupCard()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            BackColor = Theme.Bg;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Theme.FillRound(e.Graphics, r, Radius, FillColor);
            Theme.DrawRound(e.Graphics, r, Radius, StrokeColor, 1f);
        }
    }

    internal class SetupButton : Control
    {
        public Color FillColor = Theme.Accent;
        public Color HoverColor = Color.FromArgb(255, 140, 55);
        public Color TextColor = Color.White;
        public Color BorderColor = Color.Empty;
        public bool Ghost;
        public int Radius = 9;
        private bool _hover;
        private bool _down;

        public SetupButton()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            Font = Theme.SmallBold;
            Cursor = Cursors.Hand;
            TabStop = true;
            BackColor = Theme.Bg;
        }

        protected override void OnMouseEnter(EventArgs e) { _hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hover = false; _down = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { if (e.Button == MouseButtons.Left) { _down = true; Focus(); Invalidate(); } base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { _down = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle r = new Rectangle(0, 0, Width - 1, Height - 1);
            Color fill;
            if (!Enabled) fill = Theme.Surface2;
            else if (Ghost) fill = _down ? Theme.Surface3 : (_hover ? Theme.Surface2 : Color.Transparent);
            else fill = _down ? Theme.AccentDark : (_hover ? HoverColor : FillColor);
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

    internal class SetupProgress : Control
    {
        private int _value;

        public SetupProgress()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw, true);
            Height = 10;
            BackColor = Theme.Bg;
        }

        public int Value
        {
            get { return _value; }
            set { _value = Math.Max(0, Math.Min(100, value)); Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bg = new Rectangle(0, 1, Width - 1, Height - 3);
            Theme.FillRound(e.Graphics, bg, bg.Height / 2, Theme.Surface3);
            int w = (int)Math.Round((double)(Width - 1) * _value / 100.0);
            if (w > 2)
            {
                Rectangle fg = new Rectangle(0, 1, w, Height - 3);
                Theme.FillRound(e.Graphics, fg, fg.Height / 2, Theme.Accent);
            }
        }
    }

    internal class SetupForm : Form
    {
        private TextBox _dir;
        private SetupButton _browse;
        private CheckBox _desktop;
        private CheckBox _launch;
        private SetupButton _install;
        private SetupButton _cancel;
        private SetupProgress _progress;
        private Label _status;
        private bool _busy;

        public SetupForm()
        {
            Build();
        }

        private void Build()
        {
            Text = "安装 · 三角洲行动 本机调优助手";
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);
            ClientSize = new Size(720, 526);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 108;
            header.BackColor = Theme.Sidebar;
            header.Paint += delegate(object s, PaintEventArgs e) { DrawHeader(e.Graphics); };
            Controls.Add(header);

            Label lblDir = MakeLabel("安装位置", Theme.SmallBold, Theme.Text);
            lblDir.Location = new Point(26, 124);
            lblDir.Size = new Size(200, 22);
            Controls.Add(lblDir);

            _dir = new TextBox();
            _dir.Text = InstallInfo.DefaultDir;
            _dir.Location = new Point(26, 146);
            _dir.Size = new Size(520, 26);
            _dir.BackColor = Theme.Surface2;
            _dir.ForeColor = Theme.Text;
            _dir.BorderStyle = BorderStyle.FixedSingle;
            _dir.Font = Theme.Small;
            Controls.Add(_dir);

            _browse = new SetupButton();
            _browse.Ghost = true;
            _browse.Text = "浏览…";
            _browse.Location = new Point(556, 144);
            _browse.Size = new Size(138, 30);
            _browse.BackColor = Theme.Bg;
            _browse.Click += delegate { Browse(); };
            Controls.Add(_browse);

            _desktop = new CheckBox();
            _desktop.Text = "创建桌面快捷方式";
            _desktop.Checked = true;
            _desktop.AutoSize = true;
            _desktop.Location = new Point(26, 190);
            _desktop.ForeColor = Theme.Text;
            _desktop.BackColor = Theme.Bg;
            _desktop.FlatStyle = FlatStyle.Flat;
            _desktop.Font = Theme.Small;
            Controls.Add(_desktop);

            _launch = new CheckBox();
            _launch.Text = "安装完成后启动程序";
            _launch.Checked = true;
            _launch.AutoSize = true;
            _launch.Location = new Point(26, 216);
            _launch.ForeColor = Theme.Text;
            _launch.BackColor = Theme.Bg;
            _launch.FlatStyle = FlatStyle.Flat;
            _launch.Font = Theme.Small;
            Controls.Add(_launch);

            SetupCard info = new SetupCard();
            info.Location = new Point(26, 254);
            info.Size = new Size(668, 128);
            Controls.Add(info);

            Label infoTitle = MakeLabel("安装说明", Theme.SmallBold, Theme.Text);
            infoTitle.Location = new Point(18, 12);
            infoTitle.Size = new Size(200, 22);
            infoTitle.BackColor = info.FillColor;
            info.Controls.Add(infoTitle);

            AddInfoLine(info, "• 安装到当前用户目录，不需要管理员权限；程序本身需要时再单独申请。", 40);
            AddInfoLine(info, "• 只写入程序文件、开始菜单/桌面快捷方式和当前用户的卸载信息。", 66);
            AddInfoLine(info, "• 不修改游戏文件、ACE 文件、驱动或服务；设置备份保存在 %LocalAppData%\\DeltaForceTuner。", 92);

            _status = MakeLabel("准备就绪", Theme.Small, Theme.Muted);
            _status.Location = new Point(26, 396);
            _status.Size = new Size(668, 22);
            Controls.Add(_status);

            _progress = new SetupProgress();
            _progress.Location = new Point(26, 420);
            _progress.Size = new Size(668, 10);
            Controls.Add(_progress);

            _install = new SetupButton();
            _install.Text = "安装";
            _install.Location = new Point(520, 454);
            _install.Size = new Size(174, 42);
            _install.Font = Theme.BodyBold;
            _install.Click += delegate { InstallClicked(); };
            Controls.Add(_install);

            _cancel = new SetupButton();
            _cancel.Ghost = true;
            _cancel.Text = "取消";
            _cancel.Location = new Point(406, 454);
            _cancel.Size = new Size(104, 42);
            _cancel.BackColor = Theme.Bg;
            _cancel.Click += delegate { Close(); };
            Controls.Add(_cancel);

        }

        private void DrawHeader(Graphics g)
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            Theme.FillRound(g, new Rectangle(26, 28, 48, 48), 13, Theme.Accent);
            PointF[] tri = new PointF[] { new PointF(50, 38), new PointF(62, 66), new PointF(38, 66) };
            using (SolidBrush b = new SolidBrush(Color.White)) g.FillPolygon(b, tri);
            using (SolidBrush b = new SolidBrush(Theme.Text))
            using (Font f = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold, GraphicsUnit.Point))
                g.DrawString("三角洲行动 · 本机调优助手", f, b, new PointF(90, 30));
            using (SolidBrush b = new SolidBrush(Theme.Muted))
            using (Font f = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point))
                g.DrawString("安装程序 v1.0.1 · 本地运行 · 可逆调整 · 不修改游戏文件", f, b, new PointF(92, 62));
            using (Pen p = new Pen(Theme.BorderSoft, 1f))
                g.DrawLine(p, 26, 100, 694, 100);
        }

        private static void AddInfoLine(Control parent, string text, int y)
        {
            Label l = MakeLabel(text, Theme.Tiny, Theme.Muted);
            l.Location = new Point(18, y);
            l.Size = new Size(630, 20);
            l.BackColor = Theme.Surface;
            parent.Controls.Add(l);
        }

        private static Label MakeLabel(string text, Font font, Color color)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = font;
            l.ForeColor = color;
            l.AutoSize = false;
            l.UseMnemonic = false;
            l.BackColor = Theme.Bg;
            l.TextAlign = ContentAlignment.MiddleLeft;
            return l;
        }

        private void Browse()
        {
            using (FolderBrowserDialog d = new FolderBrowserDialog())
            {
                d.Description = "选择安装文件夹";
                d.ShowNewFolderButton = true;
                try { d.SelectedPath = _dir.Text; } catch { }
                if (d.ShowDialog(this) == DialogResult.OK) _dir.Text = d.SelectedPath;
            }
        }

        private void InstallClicked()
        {
            if (_busy) return;
            string dir;
            try
            {
                dir = InstallInfo.NormalizeDir(_dir.Text);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "安装目录", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            bool forceClose = false;
            if (InstallEngine.IsAppRunning())
            {
                DialogResult r = MessageBox.Show(this, "检测到程序正在运行。是否关闭后继续安装？",
                    "程序正在运行", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (r != DialogResult.Yes) return;
                forceClose = true;
            }

            SetBusy(true);
            _progress.Value = 3;
            _status.Text = "正在准备…";
            Application.DoEvents();

            SetupOptions o = new SetupOptions();
            o.Dir = dir;
            o.DesktopShortcut = _desktop.Checked;
            o.CreateShortcuts = true;
            o.WriteRegistry = true;
            o.Launch = false;
            o.ForceClose = forceClose;
            InstallResult result = InstallEngine.Install(o, delegate(int value, string message)
            {
                _progress.Value = value;
                _status.Text = message;
                Application.DoEvents();
            });

            if (!result.Ok)
            {
                SetBusy(false);
                MessageBox.Show(this, "安装失败：\n\n" + result.Message, "安装失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (_launch.Checked) InstallEngine.Launch(result.Dir);
            string extra = "";
            if (result.Warnings.Count > 0)
            {
                extra = "\n\n注意：\n";
                for (int i = 0; i < result.Warnings.Count; i++) extra += "• " + result.Warnings[i] + "\n";
            }
            MessageBox.Show(this, "安装完成。\n\n安装目录：\n" + result.Dir + extra,
                "安装完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            DialogResult = DialogResult.OK;
            Close();
        }

        private void SetBusy(bool busy)
        {
            _busy = busy;
            _dir.Enabled = !busy;
            _browse.Enabled = !busy;
            _desktop.Enabled = !busy;
            _launch.Enabled = !busy;
            _install.Enabled = !busy;
            _cancel.Enabled = !busy;
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int dark = 1;
                DwmSetWindowAttribute(Handle, 20, ref dark, 4);
                int corner = 2;
                DwmSetWindowAttribute(Handle, 33, ref corner, 4);
            }
            catch { }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    }

    internal class UninstallForm : Form
    {
        private CheckBox _deleteSettings;
        private SetupButton _uninstall;
        private SetupButton _cancel;
        private string _dir;
        private bool _busy;

        public UninstallForm()
        {
            Build();
        }

        private void Build()
        {
            _dir = InstallInfo.FindInstalledDir();
            if (string.IsNullOrEmpty(_dir)) _dir = Path.GetDirectoryName(Application.ExecutablePath);

            Text = "卸载 · 三角洲行动 本机调优助手";
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);
            ClientSize = new Size(600, 360);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            try { Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath); } catch { }

            Panel header = new Panel();
            header.Dock = DockStyle.Top;
            header.Height = 100;
            header.BackColor = Theme.Sidebar;
            header.Paint += delegate(object s, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Theme.FillRound(e.Graphics, new Rectangle(24, 26, 42, 42), 12, Theme.Red);
                using (SolidBrush b = new SolidBrush(Color.White))
                using (Font f = new Font("Segoe UI", 16f, FontStyle.Bold, GraphicsUnit.Point))
                    e.Graphics.DrawString("×", f, b, new PointF(38, 33));
                using (SolidBrush b = new SolidBrush(Theme.Text))
                using (Font f = new Font("Microsoft YaHei UI", 14f, FontStyle.Bold, GraphicsUnit.Point))
                    e.Graphics.DrawString("卸载 三角洲行动 · 本机调优助手", f, b, new PointF(82, 30));
                using (SolidBrush b = new SolidBrush(Theme.Muted))
                using (Font f = new Font("Microsoft YaHei UI", 8.5f, FontStyle.Regular, GraphicsUnit.Point))
                    e.Graphics.DrawString("不会删除游戏文件、ACE 文件或 Windows 安全设置", f, b, new PointF(84, 60));
                using (Pen p = new Pen(Theme.BorderSoft, 1f))
                    e.Graphics.DrawLine(p, 24, 92, 576, 92);
            };
            Controls.Add(header);

            Label d = MakeLabel("安装目录：", Theme.SmallBold, Theme.Text);
            d.Location = new Point(26, 120);
            d.Size = new Size(80, 22);
            Controls.Add(d);
            Label path = MakeLabel(_dir, Theme.Small, Theme.Muted);
            path.Location = new Point(108, 120);
            path.Size = new Size(460, 22);
            path.AutoEllipsis = true;
            Controls.Add(path);

            Label note = MakeLabel("卸载会删除程序文件、开始菜单/桌面快捷方式和当前用户的卸载信息。", Theme.Small, Theme.Muted);
            note.Location = new Point(26, 156);
            note.Size = new Size(548, 22);
            Controls.Add(note);

            _deleteSettings = new CheckBox();
            _deleteSettings.Text = "同时删除设置备份（%LocalAppData%\\DeltaForceTuner）";
            _deleteSettings.Checked = false;
            _deleteSettings.AutoSize = true;
            _deleteSettings.Location = new Point(26, 196);
            _deleteSettings.ForeColor = Theme.Text;
            _deleteSettings.BackColor = Theme.Bg;
            _deleteSettings.FlatStyle = FlatStyle.Flat;
            _deleteSettings.Font = Theme.Small;
            Controls.Add(_deleteSettings);

            Label warn = MakeLabel("默认保留设置备份，方便以后重新安装时恢复；游戏文件不会被删除。", Theme.Tiny, Theme.Faint);
            warn.Location = new Point(44, 224);
            warn.Size = new Size(530, 20);
            Controls.Add(warn);

            _uninstall = new SetupButton();
            _uninstall.Text = "卸载";
            _uninstall.FillColor = Theme.Red;
            _uninstall.HoverColor = Color.FromArgb(255, 125, 125);
            _uninstall.Location = new Point(402, 286);
            _uninstall.Size = new Size(172, 42);
            _uninstall.Font = Theme.BodyBold;
            _uninstall.Click += delegate { UninstallClicked(); };
            Controls.Add(_uninstall);

            _cancel = new SetupButton();
            _cancel.Ghost = true;
            _cancel.Text = "取消";
            _cancel.Location = new Point(288, 286);
            _cancel.Size = new Size(104, 42);
            _cancel.BackColor = Theme.Bg;
            _cancel.Click += delegate { Close(); };
            Controls.Add(_cancel);

            Label foot = MakeLabel("卸载程序位于安装目录，卸载完成后会自动删除自身。", Theme.Tiny, Theme.Faint);
            foot.Location = new Point(26, 326);
            foot.Size = new Size(548, 20);
            Controls.Add(foot);
        }

        private static Label MakeLabel(string text, Font font, Color color)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = font;
            l.ForeColor = color;
            l.AutoSize = false;
            l.UseMnemonic = false;
            l.BackColor = Theme.Bg;
            l.TextAlign = ContentAlignment.MiddleLeft;
            return l;
        }

        private void UninstallClicked()
        {
            if (_busy) return;
            if (MessageBox.Show(this, "确定要卸载吗？\n\n将删除程序文件和快捷方式；设置备份 " + (_deleteSettings.Checked ? "也会删除" : "会保留") + "。",
                "确认卸载", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            bool forceClose = false;
            if (InstallEngine.IsAppRunning())
            {
                if (MessageBox.Show(this, "程序正在运行。是否关闭后继续卸载？", "程序正在运行",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                forceClose = true;
            }
            _busy = true;
            _uninstall.Enabled = false;
            _cancel.Enabled = false;
            UninstallOptions o = new UninstallOptions();
            o.Dir = _dir;
            o.DeleteSettings = _deleteSettings.Checked;
            o.WriteRegistry = true;
            o.ForceClose = forceClose;
            o.SelfDelete = true;
            UninstallResult r = UninstallEngine.Uninstall(o);
            MessageBox.Show(this, r.Ok ? "卸载完成。" : "卸载失败：\n" + r.Message, r.Ok ? "卸载完成" : "卸载失败",
                MessageBoxButtons.OK, r.Ok ? MessageBoxIcon.Information : MessageBoxIcon.Error);
            Close();
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int dark = 1;
                DwmSetWindowAttribute(Handle, 20, ref dark, 4);
                int corner = 2;
                DwmSetWindowAttribute(Handle, 33, ref corner, 4);
            }
            catch { }
        }

        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    }
}
