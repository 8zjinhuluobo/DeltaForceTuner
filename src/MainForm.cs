using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace DeltaForceTuner
{
    internal class MainForm : Form
    {
        private readonly CpuTopology _topo;
        private readonly CpuLoadSampler _loadSampler = new CpuLoadSampler();
        private readonly StateStore _store;
        private readonly TweakManager _tweaks;
        private readonly AceManager _ace;
        private HardwareInfo _hw;
        private readonly string _initialPage;
        private readonly string _initialAction;

        private Panel _sidebar;
        private Panel _header;
        private Panel _contentHost;
        private Label _pageTitle;
        private Label _pageSub;
        private Pill _adminPill;
        private RoundButton _elevateBtn;
        private RoundButton _refreshBtn;
        private FlowLayoutPanel _overviewPage;
        private FlowLayoutPanel _settingsPage;
        private FlowLayoutPanel _acePage;
        private FlowLayoutPanel _helpPage;
        private readonly Dictionary<string, NavButton> _navs = new Dictionary<string, NavButton>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, TweakCard> _cards = new Dictionary<string, TweakCard>(StringComparer.OrdinalIgnoreCase);

        private Label _heroTitle;
        private Label _heroSub;
        private Label _heroStats;
        private ScoreRing _score;
        private RoundButton _heroApply;
        private HeroCard _hero;

        private Label _ovCpu;
        private Label _ovGpu;
        private Label _ovRam;
        private Label _ovOs;
        private Label _ovDisplay;
        private Label _ovPower;
        private Label _ovGame;
        private Label _ovAce;
        private Label _ovGameCard;
        private Label _ovAceCard;
        private RoundButton _openAceBtn;
        private Label[] _infoKeys;
        private Label[] _infoValues;
        private Card _infoCard;
        private Pill _ovGamePill;
        private Pill _ovAcePill;
        private Card _gameCard;

        private Card _recCard;
        private readonly List<Label> _recNames = new List<Label>();
        private readonly List<Label> _recDescs = new List<Label>();
        private readonly List<Pill> _recPills = new List<Pill>();
        private readonly List<Tweak> _recTweaks = new List<Tweak>();
        private RoundButton _recApply;

        private RoundButton _applyRecBtn;
        private RoundButton _restoreAllBtn;
        private Card _settingsIntro;

        private Card _aceStatusCard;
        private Label _aceTarget;
        private Label _aceStatus;
        private Label _aceLast;
        private Label _aceProcessCount;
        private Label _aceAutoLabel;
        private ToggleSwitch _aceAutoToggle;
        private ToggleSwitch _restoreOnExitToggle;
        private RoundButton _aceApplyBtn;
        private RoundButton _aceRestoreBtn;
        private RoundButton _modeGroup;
        private RoundButton _modeSingle;
        private RoundButton _modeCustom;
        private CpuMapControl _cpuMap;
        private ProcessListControl _processList;
        private Card _cpuMapCard;
        private Card _modeCard;
        private Card _processCard;

        private Card _toastPanel;
        private Label _toastLabel;
        private Timer _toastTimer;
        private Timer _tick;
        private NotifyIcon _tray;
        private bool _reallyExit;
        private bool _relaunching;
        private bool _balloonShown;
        private int _tickCount;
        private bool _refreshing;

        public MainForm(string page, string action)
        {
            _initialPage = string.IsNullOrEmpty(page) ? "overview" : page;
            _initialAction = action == null ? "" : action;
            _topo = CpuTopology.Detect();
            _store = StateStore.Load();
            _hw = HardwareInfo.Collect(_topo);
            _tweaks = new TweakManager(_store);
            _ace = new AceManager(_topo, _store);
            BuildUi();
            BuildTray();
            _tick = new Timer();
            _tick.Interval = 2000;
            _tick.Tick += delegate { OnTick(); };
            _tick.Start();
            _toastTimer = new Timer();
            _toastTimer.Interval = 3600;
            _toastTimer.Tick += delegate { HideToast(); };
            Shown += delegate
            {
                FitToScreen();
                LayoutAll();
                RefreshAll();
                if (!string.IsNullOrEmpty(_initialAction))
                {
                    BeginInvoke(new MethodInvoker(delegate { RunInitialAction(); }));
                }
            };
            FormClosing += OnFormClosing;
            FormClosed += OnFormClosed;
            Resize += delegate { if (_header != null && IsHandleCreated) LayoutAll(); };
        }

        private void BuildUi()
        {
            Text = "三角洲行动 · 本机调优助手";
            AutoScaleMode = AutoScaleMode.Dpi;
            AutoScaleDimensions = new SizeF(96f, 96f);
            ClientSize = new Size(1200, 820);
            MinimumSize = new Size(1060, 680);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Theme.Bg;
            ForeColor = Theme.Text;
            Font = Theme.Body;
            DoubleBuffered = true;
            KeyPreview = true;
            try
            {
                Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
            }
            catch
            {
            }

            _sidebar = new Panel();
            _sidebar.Dock = DockStyle.Left;
            _sidebar.Width = 226;
            _sidebar.BackColor = Theme.Sidebar;
            _sidebar.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(Theme.BorderSoft, 1f))
                    e.Graphics.DrawLine(p, _sidebar.Width - 1, 0, _sidebar.Width - 1, _sidebar.Height);
            };
            Controls.Add(_sidebar);
            BuildSidebar();

            Panel main = new Panel();
            main.Dock = DockStyle.Fill;
            main.BackColor = Theme.Bg;
            main.Padding = new Padding(28, 0, 28, 0);
            Controls.Add(main);
            main.BringToFront();

            _contentHost = new Panel();
            _contentHost.Dock = DockStyle.Fill;
            _contentHost.BackColor = Theme.Bg;
            main.Controls.Add(_contentHost);

            _header = new Panel();
            _header.Dock = DockStyle.Top;
            _header.Height = 100;
            _header.BackColor = Theme.Bg;
            _header.Paint += delegate(object s, PaintEventArgs e)
            {
                using (Pen p = new Pen(Theme.BorderSoft, 1f))
                    e.Graphics.DrawLine(p, 0, _header.Height - 1, _header.Width, _header.Height - 1);
            };
            main.Controls.Add(_header);
            BuildHeader();

            BuildPages();
            BuildToast();
            UpdateAdminUi();
        }

        private void BuildSidebar()
        {
            Panel logo = new Panel();
            logo.Dock = DockStyle.Top;
            logo.Height = 112;
            logo.BackColor = Theme.Sidebar;
            logo.Paint += delegate(object s, PaintEventArgs e)
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                Rectangle r = new Rectangle(22, 28, 38, 38);
                Theme.FillRound(e.Graphics, r, 11, Theme.Accent);
                PointF[] tri = new PointF[] { new PointF(41, 38), new PointF(52, 58), new PointF(30, 58) };
                using (SolidBrush b = new SolidBrush(Color.White))
                    e.Graphics.FillPolygon(b, tri);
            };
            Label brand = MakeLabel("DELTA FORCE", Theme.NumSmall, Theme.Text, Theme.Sidebar);
            brand.Location = new Point(70, 28);
            brand.Size = new Size(140, 22);
            Label brand2 = MakeLabel("本机调优助手", Theme.Tiny, Theme.Faint, Theme.Sidebar);
            brand2.Location = new Point(70, 51);
            brand2.Size = new Size(140, 18);
            logo.Controls.Add(brand);
            logo.Controls.Add(brand2);
            _sidebar.Controls.Add(logo);

            Label section = MakeLabel("优化工作台", Theme.Tiny, Theme.Faint, Theme.Sidebar);
            section.Location = new Point(22, 120);
            section.Size = new Size(180, 18);
            _sidebar.Controls.Add(section);

            string[,] nav = new string[,]
            {
                { "overview", "概览", "\uE80F" },
                { "settings", "性能开关", "\uE9D9" },
                { "ace", "ACE（可选）", "\uE950" },
                { "help", "使用说明", "\uE897" }
            };
            int y = 144;
            for (int i = 0; i < nav.GetLength(0); i++)
            {
                NavButton b = new NavButton();
                b.Text = nav[i, 1];
                b.Glyph = nav[i, 2];
                b.Location = new Point(10, y);
                b.Size = new Size(206, 44);
                b.BackColor = Theme.Sidebar;
                string key = nav[i, 0];
                b.Click += delegate { ShowPage(key); };
                _sidebar.Controls.Add(b);
                _navs[key] = b;
                y += 50;
            }

            Panel bottom = new Panel();
            bottom.Dock = DockStyle.Bottom;
            bottom.Height = 170;
            bottom.BackColor = Theme.Sidebar;
            _sidebar.Controls.Add(bottom);

            _adminPill = new Pill();
            _adminPill.SetText("普通权限");
            _adminPill.Location = new Point(18, 12);
            _adminPill.BackColor = Theme.Sidebar;
            _adminPill.Cursor = Cursors.Hand;
            _adminPill.Click += delegate { ShowElevate(); };
            bottom.Controls.Add(_adminPill);

            Label local = MakeLabel("纯本地运行 · 不联网", Theme.Tiny, Theme.Faint, Theme.Sidebar);
            local.Location = new Point(18, 42);
            local.Size = new Size(190, 18);
            bottom.Controls.Add(local);

            Label ver = MakeLabel("v1.0.1 · 所有更改可恢复", Theme.Tiny, Theme.Faint, Theme.Sidebar);
            ver.Location = new Point(18, 62);
            ver.Size = new Size(200, 18);
            bottom.Controls.Add(ver);

            RoundButton restore = MakeButton("恢复全部", 194, 32, true);
            restore.Location = new Point(16, 86);
            restore.BackColor = Theme.Sidebar;
            restore.Font = Theme.Small;
            restore.Click += delegate { RestoreAllClicked(); };
            bottom.Controls.Add(restore);

            RoundButton exit = MakeButton("退出工具", 194, 32, true);
            exit.Location = new Point(16, 124);
            exit.BackColor = Theme.Sidebar;
            exit.Font = Theme.Small;
            exit.Click += delegate
            {
                _reallyExit = true;
                Close();
            };
            bottom.Controls.Add(exit);
        }

        private void BuildHeader()
        {
            _pageTitle = MakeLabel("概览", Theme.H1, Theme.Text, Theme.Bg);
            _pageTitle.Location = new Point(0, 10);
            _pageTitle.Size = new Size(620, 48);
            _header.Controls.Add(_pageTitle);

            _pageSub = MakeLabel("检查本机状态，启用低风险推荐设置；所有更改都可恢复。", Theme.Small, Theme.Muted, Theme.Bg);
            _pageSub.Location = new Point(2, 60);
            _pageSub.Size = new Size(720, 22);
            _header.Controls.Add(_pageSub);

            _elevateBtn = MakeButton("以管理员身份重启", 142, 34, false);
            _elevateBtn.Font = Theme.Small;
            _elevateBtn.Click += delegate { ShowElevate(); };
            _header.Controls.Add(_elevateBtn);

            _refreshBtn = MakeButton("重新检测", 96, 34, true);
            _refreshBtn.BackColor = Theme.Bg;
            _refreshBtn.Font = Theme.Small;
            _refreshBtn.Click += delegate { RefreshAll(); ShowToast("已重新检测本机状态", false); };
            _header.Controls.Add(_refreshBtn);
        }

        private void BuildPages()
        {
            _overviewPage = NewPage();
            _settingsPage = NewPage();
            _acePage = NewPage();
            _helpPage = NewPage();
            BuildOverview();
            BuildSettings();
            BuildAce();
            BuildHelp();
            ShowPage(_initialPage);
        }

        private FlowLayoutPanel NewPage()
        {
            FlowLayoutPanel p = new FlowLayoutPanel();
            p.Dock = DockStyle.Fill;
            p.FlowDirection = FlowDirection.TopDown;
            p.WrapContents = false;
            p.AutoScroll = true;
            p.BackColor = Theme.Bg;
            p.Padding = new Padding(0, 2, 4, 16);
            p.Visible = false;
            p.Resize += delegate { LayoutPage(p); };
            p.HandleCreated += delegate { Native.SetWindowTheme(p.Handle, "DarkMode_Explorer", null); };
            _contentHost.Controls.Add(p);
            return p;
        }

        private void BuildOverview()
        {
            _hero = new HeroCard();
            _hero.Height = 190;
            AddCard(_overviewPage, _hero, 190);
            _hero.Paint += delegate { };

            Pill badge = new Pill();
            badge.SetText("本机配置已识别");
            badge.FillColor = Theme.WithAlpha(Theme.Accent, 28);
            badge.TextColor = Theme.Accent;
            badge.BackColor = Color.FromArgb(37, 28, 22);
            badge.Location = new Point(24, 20);
            _hero.Controls.Add(badge);

            _heroTitle = MakeLabel("本机游戏性能调优", new Font("Microsoft YaHei UI", 19f, FontStyle.Bold, GraphicsUnit.Point), Theme.Text, Color.Transparent);
            _heroTitle.Location = new Point(24, 47);
            _heroTitle.Size = new Size(700, 36);
            _hero.Controls.Add(_heroTitle);

            _heroSub = MakeLabel("", Theme.Small, Theme.Muted, Color.Transparent);
            _heroSub.Location = new Point(24, 85);
            _heroSub.Size = new Size(700, 22);
            _hero.Controls.Add(_heroSub);

            _heroStats = MakeLabel("", Theme.Tiny, Theme.Faint, Color.Transparent);
            _heroStats.Location = new Point(24, 112);
            _heroStats.Size = new Size(700, 18);
            _hero.Controls.Add(_heroStats);

            _score = new ScoreRing();
            _score.Size = new Size(118, 118);
            _score.Location = new Point(_hero.Width - 150, 34);
            _score.BackColor = Color.FromArgb(62, 41, 27);
            _hero.Controls.Add(_score);

            _heroApply = MakeButton("一键应用推荐", 138, 36, false);
            _heroApply.Location = new Point(24, 142);
            _heroApply.Font = Theme.SmallBold;
            _heroApply.Click += delegate { ApplyRecommendedClicked(); };
            _hero.Controls.Add(_heroApply);

            _hero.Resize += delegate { LayoutHero(); };

            _infoCard = NewCard(264);
            AddCard(_overviewPage, _infoCard, 264);
            AddCardTitle(_infoCard, "本机概况", "CPU、显卡、内存、系统与电源状态");
            _infoKeys = new Label[8];
            _infoValues = new Label[8];
            string[] keys = new string[] { "处理器", "显卡", "内存", "系统", "显示", "电源", "游戏", "ACE" };
            for (int i = 0; i < 8; i++)
            {
                _infoKeys[i] = MakeLabel(keys[i], Theme.Tiny, Theme.Faint, _infoCard.FillColor);
                _infoValues[i] = MakeLabel("—", Theme.Body, Theme.Text, _infoCard.FillColor);
                _infoValues[i].AutoEllipsis = true;
                _infoCard.Controls.Add(_infoKeys[i]);
                _infoCard.Controls.Add(_infoValues[i]);
            }
            _ovCpu = _infoValues[0]; _ovGpu = _infoValues[1]; _ovRam = _infoValues[2]; _ovOs = _infoValues[3];
            _ovDisplay = _infoValues[4]; _ovPower = _infoValues[5]; _ovGame = _infoValues[6]; _ovAce = _infoValues[7];
            _infoCard.Resize += delegate { LayoutInfo(); };

            _gameCard = NewCard(168);
            AddCard(_overviewPage, _gameCard, 168);
            AddCardTitle(_gameCard, "游戏与 ACE", "只检测，不修改游戏目录");
            _ovGameCard = MakeLabel("未检测到游戏", Theme.Body, Theme.Text, _gameCard.FillColor);
            _ovGameCard.Location = new Point(22, 64);
            _ovGameCard.Size = new Size(600, 22);
            _ovGameCard.AutoEllipsis = true;
            _gameCard.Controls.Add(_ovGameCard);
            _ovAceCard = MakeLabel("ACE：未运行", Theme.Small, Theme.Muted, _gameCard.FillColor);
            _ovAceCard.Location = new Point(22, 92);
            _ovAceCard.Size = new Size(600, 22);
            _ovAceCard.AutoEllipsis = true;
            _gameCard.Controls.Add(_ovAceCard);
            _ovGamePill = new Pill();
            _ovGamePill.SetText("检测中");
            _ovGamePill.BackColor = _gameCard.FillColor;
            _ovGamePill.Location = new Point(_gameCard.Width - 230, 64);
            _gameCard.Controls.Add(_ovGamePill);
            _ovAcePill = new Pill();
            _ovAcePill.SetText("ACE 未运行");
            _ovAcePill.BackColor = _gameCard.FillColor;
            _ovAcePill.Location = new Point(_gameCard.Width - 230, 92);
            _gameCard.Controls.Add(_ovAcePill);
            _openAceBtn = MakeButton("打开 ACE 隔离", 132, 30, true);
            _openAceBtn.Font = Theme.Tiny;
            _openAceBtn.BackColor = _gameCard.FillColor;
            _openAceBtn.Location = new Point(_gameCard.Width - 158, 124);
            _openAceBtn.Click += delegate { ShowPage("ace"); };
            _gameCard.Controls.Add(_openAceBtn);
            _gameCard.Resize += delegate { LayoutGameCard(_openAceBtn); };

            _recCard = NewCard(286);
            AddCard(_overviewPage, _recCard, 286);
            AddCardTitle(_recCard, "推荐启用", "按你的硬件和使用场景筛选；一项也可以单独开关");
            _recApply = MakeButton("应用剩余推荐", 132, 32, false);
            _recApply.Font = Theme.Tiny;
            _recApply.Location = new Point(_recCard.Width - 154, 16);
            _recApply.Click += delegate { ApplyRecommendedClicked(); };
            _recCard.Controls.Add(_recApply);

            Label hName = MakeLabel("项目", Theme.Tiny, Theme.Faint, _recCard.FillColor);
            hName.Location = new Point(22, 60); hName.Size = new Size(160, 16);
            _recCard.Controls.Add(hName);
            Label hDesc = MakeLabel("为什么推荐", Theme.Tiny, Theme.Faint, _recCard.FillColor);
            hDesc.Location = new Point(206, 60); hDesc.Size = new Size(400, 16);
            _recCard.Controls.Add(hDesc);
            Label hState = MakeLabel("状态", Theme.Tiny, Theme.Faint, _recCard.FillColor);
            hState.Location = new Point(_recCard.Width - 120, 60); hState.Size = new Size(84, 16);
            _recCard.Controls.Add(hState);

            List<Tweak> recommended = _tweaks.RecommendedTweaks();
            for (int i = 0; i < recommended.Count; i++)
            {
                Tweak t = recommended[i];
                _recTweaks.Add(t);
                Label n = MakeLabel(t.Title, Theme.SmallBold, Theme.Text, _recCard.FillColor);
                n.Location = new Point(22, 84 + i * 36);
                n.Size = new Size(172, 20);
                _recCard.Controls.Add(n);
                _recNames.Add(n);
                Label d = MakeLabel(ShortDescription(t), Theme.Tiny, Theme.Muted, _recCard.FillColor);
                d.Location = new Point(206, 85 + i * 36);
                d.Size = new Size(440, 18);
                d.AutoEllipsis = true;
                _recCard.Controls.Add(d);
                _recDescs.Add(d);
                Pill p = new Pill();
                p.SetText("检查中");
                p.BackColor = _recCard.FillColor;
                p.Location = new Point(_recCard.Width - 120, 84 + i * 36);
                _recCard.Controls.Add(p);
                _recPills.Add(p);
            }
            _recCard.Resize += delegate { LayoutRecommendations(); };

            Card safety = NewCard(146);
            AddCard(_overviewPage, safety, 146);
            AddCardTitle(safety, "安全边界", "这个工具刻意不做什么");
            AddCheckLine(safety, "不修改游戏目录、ACE 文件或驱动；不注入、不结束 ACE 进程。", 66);
            AddCheckLine(safety, "不关闭 Defender、内存完整性、VBS 等安全功能。", 92);
            AddCheckLine(safety, "每个注册表/电源项在修改前都保存原始值，可一键恢复。", 118);

            Card advice = NewCard(210);
            AddCard(_overviewPage, advice, 210);
            AddCardTitle(advice, "游戏内建议", "这些是游戏设置，工具不会替你改");
            AddBullet(advice, "显示模式优先“全屏独占”；帧率上限设为显示器刷新率，关闭垂直同步。", 66);
            AddBullet(advice, "Iris Xe 建议 1920×1080 或 1600×900，开启 FSR“性能/平衡”，纹理中低。", 94);
            AddBullet(advice, "阴影、体积云、环境光遮蔽、动态模糊、景深调低或关闭。", 122);
            AddBullet(advice, "关闭后台浏览器/下载/录屏；16GB 内存玩前先释放内存。", 150);
            AddBullet(advice, "把游戏放在 SSD；更新 Intel 显卡驱动，插电再玩。", 178);
        }

        private void BuildSettings()
        {
            _settingsIntro = NewCard(104);
            AddCard(_settingsPage, _settingsIntro, 104);
            AddCardTitle(_settingsIntro, "调整说明", "修改 Windows 用户/系统设置，不触碰游戏文件；每项都可单独恢复。");
            _applyRecBtn = MakeButton("一键应用推荐", 132, 34, false);
            _applyRecBtn.Font = Theme.Small;
            _applyRecBtn.Location = new Point(_settingsIntro.Width - 292, 18);
            _applyRecBtn.Click += delegate { ApplyRecommendedClicked(); };
            _settingsIntro.Controls.Add(_applyRecBtn);
            _restoreAllBtn = MakeButton("恢复全部", 120, 34, true);
            _restoreAllBtn.Font = Theme.Small;
            _restoreAllBtn.Location = new Point(_settingsIntro.Width - 148, 18);
            _restoreAllBtn.Click += delegate { RestoreAllClicked(); };
            _settingsIntro.Controls.Add(_restoreAllBtn);

            for (int i = 0; i < _tweaks.All.Count; i++)
            {
                Tweak t = _tweaks.All[i];
                TweakCard card = new TweakCard(t, this);
                AddCard(_settingsPage, card, 122);
                _cards[t.Id] = card;
            }

            Card note = NewCard(96);
            AddCard(_settingsPage, note, 96);
            AddCardTitle(note, "权限说明", "");
            Label l = MakeLabel("写入 HKLM 项与切换电源模式需要管理员权限。非管理员点击时，工具会先询问是否以管理员身份重启；UAC 被拒绝时不会写入任何内容。", Theme.Small, Theme.Muted, note.FillColor);
            l.Location = new Point(22, 42);
            l.Size = new Size(note.Width - 44, 40);
            note.Controls.Add(l);
            note.Resize += delegate { l.Width = note.Width - 44; };
        }

        private void BuildAce()
        {
            _aceStatusCard = NewCard(206);
            AddCard(_acePage, _aceStatusCard, 206);
            AddCardTitle(_aceStatusCard, "当前状态", "默认关闭；普通用户不推荐修改 ACE，直接用前面的 Windows 设置即可");

            _aceTarget = MakeLabel("当前目标：—", Theme.NumMid, Theme.Text, _aceStatusCard.FillColor);
            _aceTarget.Location = new Point(22, 66);
            _aceTarget.Size = new Size(620, 30);
            _aceStatusCard.Controls.Add(_aceTarget);

            _aceStatus = MakeLabel("等待检测", Theme.Body, Theme.Muted, _aceStatusCard.FillColor);
            _aceStatus.Location = new Point(22, 100);
            _aceStatus.Size = new Size(620, 22);
            _aceStatus.AutoEllipsis = true;
            _aceStatusCard.Controls.Add(_aceStatus);

            _aceLast = MakeLabel("", Theme.Tiny, Theme.Faint, _aceStatusCard.FillColor);
            _aceLast.Location = new Point(22, 128);
            _aceLast.Size = new Size(620, 18);
            _aceStatusCard.Controls.Add(_aceLast);

            _aceAutoLabel = MakeLabel("自动保持（默认关）", Theme.Tiny, Theme.Muted, _aceStatusCard.FillColor);
            _aceAutoLabel.Location = new Point(_aceStatusCard.Width - 206, 22);
            _aceAutoLabel.Size = new Size(128, 20);
            _aceAutoLabel.TextAlign = ContentAlignment.MiddleRight;
            _aceStatusCard.Controls.Add(_aceAutoLabel);
            _aceAutoToggle = new ToggleSwitch();
            _aceAutoToggle.Location = new Point(_aceStatusCard.Width - 70, 20);
            _aceAutoToggle.BackColor = _aceStatusCard.FillColor;
            _aceAutoToggle.CheckedChanged += delegate
            {
                if (_refreshing) return;
                _ace.SetAutoApply(_aceAutoToggle.Checked);
                if (_aceAutoToggle.Checked) ApplyAce(false);
                ShowToast(_aceAutoToggle.Checked ? "已开启自动保持" : "已关闭自动保持", false);
            };
            _aceStatusCard.Controls.Add(_aceAutoToggle);

            _aceApplyBtn = MakeButton("立即应用", 128, 34, false);
            _aceApplyBtn.Location = new Point(_aceStatusCard.Width - 292, 154);
            _aceApplyBtn.Click += delegate { ApplyAce(false); };
            _aceStatusCard.Controls.Add(_aceApplyBtn);
            _aceRestoreBtn = MakeButton("恢复原始", 120, 34, true);
            _aceRestoreBtn.Location = new Point(_aceStatusCard.Width - 154, 154);
            _aceRestoreBtn.Click += delegate { RestoreAce(false); };
            _aceStatusCard.Controls.Add(_aceRestoreBtn);
            _aceStatusCard.Resize += delegate { LayoutAceStatus(); };

            _cpuMapCard = NewCard(268);
            AddCard(_acePage, _cpuMapCard, 268);
            AddCardTitle(_cpuMapCard, "CPU 拓扑", "点击 E 核可切换为“自定义单核”；橙色圆点表示 ACE 当前所在核");
            _cpuMap = new CpuMapControl();
            _cpuMap.Topology = _topo;
            _cpuMap.Location = new Point(20, 64);
            _cpuMap.Size = new Size(_cpuMapCard.Width - 40, 180);
            _cpuMap.BackColor = _cpuMapCard.FillColor;
            _cpuMap.CoreClicked += delegate(object s, EventArgs e)
            {
                if (_cpuMap.SelectedCoreIndex < 0) return;
                _ace.SetMode("Custom", _cpuMap.SelectedCoreIndex);
                RefreshAce();
                ShowToast("已选择 " + _ace.TargetDescription + "，点“立即应用”生效", false);
            };
            _cpuMapCard.Controls.Add(_cpuMap);
            _cpuMapCard.Resize += delegate
            {
                _cpuMap.Width = _cpuMapCard.Width - 40;
                _cpuMap.Height = 180;
            };

            _modeCard = NewCard(206);
            AddCard(_acePage, _modeCard, 206);
            AddCardTitle(_modeCard, "目标模式", "默认使用全部 E 核，避免多个 ACE 进程挤在一个核上");
            _modeGroup = MakeButton("E 核组（推荐）", 142, 36, false);
            _modeGroup.Location = new Point(22, 68);
            _modeGroup.Font = Theme.Small;
            _modeGroup.Click += delegate { SetAceMode("Group", -1); };
            _modeCard.Controls.Add(_modeGroup);
            _modeSingle = MakeButton("单颗 E 核", 112, 36, true);
            _modeSingle.Location = new Point(174, 68);
            _modeSingle.Font = Theme.Small;
            _modeSingle.Click += delegate { SetAceMode("Single", -1); };
            _modeCard.Controls.Add(_modeSingle);
            _modeCustom = MakeButton("自定义", 96, 36, true);
            _modeCustom.Location = new Point(296, 68);
            _modeCustom.Font = Theme.Small;
            _modeCustom.Click += delegate { SetAceMode("Custom", _ace.CustomCore); };
            _modeCard.Controls.Add(_modeCustom);

            Label restoreExitLabel = MakeLabel("退出时恢复 ACE 原始亲和性", Theme.Small, Theme.Muted, _modeCard.FillColor);
            restoreExitLabel.Location = new Point(22, 128);
            restoreExitLabel.Size = new Size(300, 22);
            _modeCard.Controls.Add(restoreExitLabel);
            _restoreOnExitToggle = new ToggleSwitch();
            _restoreOnExitToggle.Location = new Point(330, 126);
            _restoreOnExitToggle.BackColor = _modeCard.FillColor;
            _restoreOnExitToggle.CheckedChanged += delegate
            {
                if (_refreshing) return;
                _store.RestoreOnExit = _restoreOnExitToggle.Checked;
                _store.Save();
                ShowToast(_restoreOnExitToggle.Checked ? "退出时将恢复 ACE 亲和性" : "退出时保留 ACE 亲和性", false);
            };
            _modeCard.Controls.Add(_restoreOnExitToggle);
            Label hint = MakeLabel("单颗模式默认选 E8（CPU 19）。你也可以在拓扑图中点任意 E 核。", Theme.Tiny, Theme.Faint, _modeCard.FillColor);
            hint.Location = new Point(22, 164);
            hint.Size = new Size(_modeCard.Width - 44, 20);
            _modeCard.Controls.Add(hint);
            _modeCard.Resize += delegate { hint.Width = _modeCard.Width - 44; };

            _processCard = NewCard(274);
            AddCard(_acePage, _processCard, 274);
            AddCardTitle(_processCard, "ACE 进程", "只会处理 AntiCheatExpert 目录下的用户态进程，不会碰游戏主程序");
            _aceProcessCount = MakeLabel("0 个进程", Theme.Tiny, Theme.Faint, _processCard.FillColor);
            _aceProcessCount.Location = new Point(_processCard.Width - 100, 22);
            _aceProcessCount.Size = new Size(84, 18);
            _aceProcessCount.TextAlign = ContentAlignment.MiddleRight;
            _processCard.Controls.Add(_aceProcessCount);
            _processList = new ProcessListControl();
            _processList.Location = new Point(20, 64);
            _processList.Size = new Size(_processCard.Width - 40, 190);
            _processList.BackColor = _processCard.FillColor;
            _processCard.Controls.Add(_processList);
            _processCard.Resize += delegate
            {
                _processList.Width = _processCard.Width - 40;
                _aceProcessCount.Location = new Point(_processCard.Width - 100, 22);
            };

            Card safety = NewCard(196);
            AddCard(_acePage, safety, 196);
            AddCardTitle(safety, "只做调度，不做对抗", "如果 ACE 保护它的进程，Windows 会拒绝修改，工具不会绕过保护");
            AddCheckLine(safety, "只调用 SetProcessAffinityMask，不注入 DLL、不挂钩子、不暂停/结束进程。", 66);
            AddCheckLine(safety, "不修改 ACE 目录、驱动、服务或游戏文件；只改运行中的进程亲和性。", 94);
            AddCheckLine(safety, "关闭此页面开关或退出工具（默认）即可恢复原始亲和性。", 122);
            Label warn = MakeLabel("提示：部分反作弊版本可能把“外部修改进程设置”视为异常。使用前请自行确认游戏规则；如遇异常，立即恢复并重启游戏。", Theme.Tiny, Theme.Amber, safety.FillColor);
            warn.Location = new Point(22, 152);
            warn.Size = new Size(safety.Width - 44, 28);
            safety.Controls.Add(warn);
            safety.Resize += delegate { warn.Width = safety.Width - 44; };
        }

        private void BuildHelp()
        {
            AddHelpCard("使用步骤", 168,
                "1. 插电，启动《三角洲行动》，等 ACE 进程出现。\n" +
                "2. 前三项 Windows 设置已经足够；ACE 页默认关闭，普通用户可完全不使用。\n" +
                "3. 如果确实要限制 ACE：打开“ACE（可选）”，选择 E 核组或单颗 E 核，手动点“立即应用”。\n" +
                "4. 可以关闭窗口最小化到托盘，游戏结束后从托盘退出。");
            AddHelpCard("推荐逻辑", 170,
                "推荐项只包含低风险、可逆、对大多数笔记本有用的设置：游戏模式、关闭后台录制、多媒体调度、关闭鼠标加速、插电时最佳性能。\n" +
                "硬件加速 GPU 调度（HAGS）被标为“可选”，因为核显/老驱动上可能反而卡顿，而且需要重启。\n" +
                "本机是 6P+8E 的 i9-12900H：把 ACE 放到 E 核，避免它和游戏争抢 P 核。");
            AddHelpCard("安全与恢复", 184,
                "所有注册表修改都会先保存原始值到：\n%LocalAppData%\\DeltaForceTuner\\state.xml\n" +
                "每个开关都有“恢复原始”；侧栏“恢复全部”会恢复所有本工具改过的设置和 ACE 亲和性。\n" +
                "工具不修改游戏目录、ACE 文件、驱动、服务，也不关闭 Defender/VBS/内存完整性。\n" +
                "如果 UAC 被拒绝、ACE 拒绝访问或电源计划锁定，工具会显示原因，不会强行写入。");
            AddHelpCard("ACE 说明", 160,
                "ACE 是游戏自带的反作弊。它有时会把 CPU 吃到 10%–25%，还会周期性恢复自己的亲和性。\n" +
                "本工具只调整运行中的 ACE 用户态进程（SGuard64 / SGuardSvc64 / ACE-Service64 等）的 CPU 亲和性，不结束、不暂停、不注入。\n" +
                "如果状态显示“ACE 保护拒绝访问”，说明该版本不允许外部修改，这是反作弊的合法行为，工具不会尝试绕过。");
            AddHelpCard("常见问题", 190,
                "Q: 找不到 ACE 进程？ 先启动游戏，进入大厅后再看；ACE 服务可能在游戏退出后仍在运行。\n" +
                "Q: 改了没感觉？ 本工具解决的是 CPU 争抢，不改变显卡性能；Iris Xe 建议同时降低游戏画质/分辨率。\n" +
                "Q: 需要重启吗？ 只有 HAGS 需要；其他开关即时生效。\n" +
                "Q: 会在后台联网/上传吗？ 不会。没有网络代码、没有安装服务、没有开机启动。");
            AddHelpCard("兼容性与文件", 150,
                "Windows 10/11 x64；使用系统自带的 .NET Framework 4.8，无需安装运行时。\n" +
                "程序是单个本地 exe；配置和备份只写到当前用户的 LocalAppData。\n" +
                "建议把 exe 放在普通文件夹运行，不要放到游戏目录，避免被反作弊扫描误判。\n" +
                "版本 1.0.1 · 构建时间 " + DateTime.Now.ToString("yyyy-MM-dd"));
        }

        private void AddHelpCard(string title, int height, string body)
        {
            Card c = NewCard(height);
            AddCard(_helpPage, c, height);
            AddCardTitle(c, title, "");
            Label l = MakeLabel(body, Theme.Small, Theme.Muted, c.FillColor);
            l.Location = new Point(22, 44);
            l.Size = new Size(c.Width - 44, height - 56);
            c.Controls.Add(l);
            c.Resize += delegate { l.Width = c.Width - 44; };
        }

        private Card NewCard(int height)
        {
            Card c = new Card();
            c.Height = height;
            c.FillColor = Theme.Surface;
            c.StrokeColor = Theme.BorderSoft;
            c.Margin = new Padding(0, 0, 0, 12);
            return c;
        }

        private void AddCard(FlowLayoutPanel page, Control card, int height)
        {
            card.Width = PageWidth();
            card.Height = height;
            card.Tag = height;
            page.Controls.Add(card);
        }

        private int PageWidth()
        {
            int w = _contentHost == null ? 900 : _contentHost.ClientSize.Width - 26;
            if (w < 700) w = 700;
            return w;
        }

        private Label MakeLabel(string text, Font font, Color color, Color back)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = font;
            l.ForeColor = color;
            l.BackColor = back;
            l.AutoSize = false;
            l.UseMnemonic = false;
            l.TextAlign = ContentAlignment.MiddleLeft;
            return l;
        }

        private RoundButton MakeButton(string text, int width, int height, bool ghost)
        {
            RoundButton b = new RoundButton();
            b.Text = text;
            b.Size = new Size(width, height);
            b.Ghost = ghost;
            b.Font = Theme.SmallBold;
            if (ghost)
            {
                b.FillColor = Theme.Surface2;
                b.BorderColor = Theme.Border;
                b.TextColor = Theme.Muted;
            }
            return b;
        }

        private void AddCardTitle(Card card, string title, string subtitle)
        {
            Label t = MakeLabel(title, Theme.H3, Theme.Text, card.FillColor);
            t.Location = new Point(22, 16);
            t.Size = new Size(card.Width - 44, 24);
            card.Controls.Add(t);
            card.Resize += delegate { t.Width = Math.Max(120, card.Width - 44); };
            if (!string.IsNullOrEmpty(subtitle))
            {
                Label s = MakeLabel(subtitle, Theme.Tiny, Theme.Faint, card.FillColor);
                s.Location = new Point(22, 40);
                s.Size = new Size(card.Width - 44, 18);
                card.Controls.Add(s);
                card.Resize += delegate { s.Width = Math.Max(120, card.Width - 44); };
            }
        }

        private void AddCheckLine(Card card, string text, int y)
        {
            Label check = MakeLabel("✓", Theme.BodyBold, Theme.Green, card.FillColor);
            check.Location = new Point(22, y);
            check.Size = new Size(18, 20);
            card.Controls.Add(check);
            Label l = MakeLabel(text, Theme.Small, Theme.Muted, card.FillColor);
            l.Location = new Point(42, y);
            l.Size = new Size(card.Width - 64, 20);
            card.Controls.Add(l);
            card.Resize += delegate { l.Width = Math.Max(120, card.Width - 64); };
        }

        private void AddBullet(Card card, string text, int y)
        {
            Label dot = MakeLabel("•", Theme.BodyBold, Theme.Accent, card.FillColor);
            dot.Location = new Point(22, y);
            dot.Size = new Size(16, 20);
            card.Controls.Add(dot);
            Label l = MakeLabel(text, Theme.Small, Theme.Muted, card.FillColor);
            l.Location = new Point(40, y);
            l.Size = new Size(card.Width - 64, 22);
            l.AutoEllipsis = true;
            card.Controls.Add(l);
            card.Resize += delegate { l.Width = Math.Max(120, card.Width - 64); };
        }

        private string ShortDescription(Tweak t)
        {
            if (t.Id == "game_mode") return "降低后台程序对游戏线程的抢占";
            if (t.Id == "game_dvr") return "减少后台编码与磁盘写入";
            if (t.Id == "multimedia") return "网络不节流，游戏线程优先";
            if (t.Id == "mouse_accel") return "瞄准更线性，适合 FPS";
            if (t.Id == "power_perf") return "插电时减少降频与保守调度";
            return t.Description;
        }

        private void BuildToast()
        {
            _toastPanel = new Card();
            _toastPanel.FillColor = Theme.Surface2;
            _toastPanel.StrokeColor = Theme.Border;
            _toastPanel.Size = new Size(390, 56);
            _toastPanel.Visible = false;
            _toastLabel = MakeLabel("", Theme.Small, Theme.Text, Theme.Surface2);
            _toastLabel.Location = new Point(16, 6);
            _toastLabel.Size = new Size(358, 44);
            _toastPanel.Controls.Add(_toastLabel);
            Controls.Add(_toastPanel);
            _toastPanel.BringToFront();
        }

        private void BuildTray()
        {
            try
            {
                _tray = new NotifyIcon();
                _tray.Icon = Icon != null ? Icon : SystemIcons.Application;
                _tray.Text = "三角洲行动 · 本机调优助手";
                _tray.Visible = true;
                ContextMenuStrip menu = new ContextMenuStrip();
                menu.Items.Add("显示主界面", null, delegate { ShowFromTray(); });
                menu.Items.Add("立即应用 ACE 小核", null, delegate { ApplyAce(false); });
                menu.Items.Add("恢复 ACE 原始亲和性", null, delegate { RestoreAce(false); });
                menu.Items.Add(new ToolStripSeparator());
                menu.Items.Add("退出工具", null, delegate { _reallyExit = true; Close(); });
                _tray.ContextMenuStrip = menu;
                _tray.DoubleClick += delegate { ShowFromTray(); };
            }
            catch
            {
                _tray = null;
            }
        }

        private void FitToScreen()
        {
            try
            {
                Screen sc = Screen.FromControl(this);
                Rectangle wa = sc.WorkingArea;
                if (Width > wa.Width - 20) Width = wa.Width - 20;
                if (Height > wa.Height - 20) Height = wa.Height - 20;
                if (Left < wa.Left) Left = wa.Left;
                if (Top < wa.Top) Top = wa.Top;
            }
            catch
            {
            }
        }

        private void LayoutAll()
        {
            if (_pageTitle == null) return;
            LayoutHeader();
            LayoutPages();
            LayoutHero();
            LayoutInfo();
            LayoutRecommendations();
            LayoutAceStatus();
            LayoutToast();
            if (_gameCard != null && _openAceBtn != null) LayoutGameCard(_openAceBtn);
        }

        private void LayoutHeader()
        {
            if (_header == null) return;
            int right = _header.Width - 2;
            _refreshBtn.Location = new Point(right - _refreshBtn.Width, 32);
            if (_elevateBtn.Visible)
                _elevateBtn.Location = new Point(right - _refreshBtn.Width - _elevateBtn.Width - 10, 32);
            _pageSub.Width = Math.Max(300, _header.Width - 380);
        }

        private void LayoutPages()
        {
            LayoutPage(_overviewPage);
            LayoutPage(_settingsPage);
            LayoutPage(_acePage);
            LayoutPage(_helpPage);
            if (_settingsIntro != null)
            {
                _applyRecBtn.Location = new Point(_settingsIntro.Width - 292, 18);
                _restoreAllBtn.Location = new Point(_settingsIntro.Width - 148, 18);
            }
        }

        private void LayoutPage(FlowLayoutPanel page)
        {
            if (page == null) return;
            int w = page.ClientSize.Width - 28;
            if (w < 700) w = 700;
            for (int i = 0; i < page.Controls.Count; i++)
            {
                Control c = page.Controls[i];
                c.Width = w;
                object tag = c.Tag;
                if (tag is int) c.Height = (int)tag;
            }
        }

        private void LayoutHero()
        {
            if (_hero == null) return;
            _score.Location = new Point(_hero.Width - 152, 34);
            int cw = Math.Max(300, _hero.Width - 190);
            _heroTitle.Width = cw;
            _heroSub.Width = cw;
            _heroStats.Width = cw;
        }

        private void LayoutInfo()
        {
            if (_infoCard == null || _infoKeys == null) return;
            int colW = Math.Max(280, (_infoCard.Width - 66) / 2);
            for (int i = 0; i < 8; i++)
            {
                int col = i % 2;
                int row = i / 2;
                int x = 22 + col * (colW + 22);
                int y = 74 + row * 44;
                _infoKeys[i].Location = new Point(x, y);
                _infoKeys[i].Size = new Size(colW - 10, 16);
                _infoValues[i].Location = new Point(x, y + 17);
                _infoValues[i].Size = new Size(colW - 10, 21);
            }
        }

        private void LayoutGameCard(RoundButton open)
        {
            if (_gameCard == null) return;
            _ovGameCard.Width = Math.Max(260, _gameCard.Width - 270);
            _ovAceCard.Width = Math.Max(260, _gameCard.Width - 270);
            _ovGamePill.Location = new Point(_gameCard.Width - 230, 64);
            _ovAcePill.Location = new Point(_gameCard.Width - 230, 92);
            open.Location = new Point(_gameCard.Width - 158, 124);
        }

        private void LayoutRecommendations()
        {
            if (_recCard == null) return;
            _recApply.Location = new Point(_recCard.Width - 154, 16);
            for (int i = 0; i < _recDescs.Count; i++)
            {
                _recDescs[i].Width = Math.Max(200, _recCard.Width - 370);
                _recPills[i].Location = new Point(_recCard.Width - 120, 84 + i * 36);
            }
        }

        private void LayoutAceStatus()
        {
            if (_aceStatusCard == null) return;
            _aceAutoToggle.Location = new Point(_aceStatusCard.Width - 70, 20);
            _aceAutoLabel.Location = new Point(_aceStatusCard.Width - 206, 22);
            _aceApplyBtn.Location = new Point(_aceStatusCard.Width - 292, 154);
            _aceRestoreBtn.Location = new Point(_aceStatusCard.Width - 154, 154);
            _aceTarget.Width = Math.Max(300, _aceStatusCard.Width - 220);
            _aceStatus.Width = Math.Max(300, _aceStatusCard.Width - 220);
            _aceLast.Width = Math.Max(300, _aceStatusCard.Width - 220);
        }

        private void LayoutToast()
        {
            if (_toastPanel == null) return;
            _toastPanel.Location = new Point(ClientSize.Width - _toastPanel.Width - 26, ClientSize.Height - _toastPanel.Height - 26);
        }

        private void ShowPage(string key)
        {
            if (string.IsNullOrEmpty(key)) key = "overview";
            if (!_navs.ContainsKey(key)) key = "overview";
            _overviewPage.Visible = key == "overview";
            _settingsPage.Visible = key == "settings";
            _acePage.Visible = key == "ace";
            _helpPage.Visible = key == "help";
            foreach (KeyValuePair<string, NavButton> kv in _navs)
            {
                kv.Value.Active = string.Equals(kv.Key, key, StringComparison.OrdinalIgnoreCase);
                kv.Value.Invalidate();
            }
            if (key == "overview")
            {
                _pageTitle.Text = "概览";
                _pageSub.Text = "检查本机状态，启用低风险推荐设置；所有更改都可恢复。";
            }
            else if (key == "settings")
            {
                _pageTitle.Text = "性能开关";
                _pageSub.Text = "每一项都可以单独恢复；不修改游戏文件，不关闭安全功能。";
            }
            else if (key == "ace")
            {
                _pageTitle.Text = "ACE（可选）";
                _pageSub.Text = "默认关闭；仅在你明确需要时手动调整 ACE 进程亲和性。";
            }
            else
            {
                _pageTitle.Text = "使用说明";
                _pageSub.Text = "安全边界、恢复方式、常见问题。";
            }
            _store.LastPage = key;
            _store.Save();
            LayoutAll();
            RefreshPage(key);
        }

        private void RefreshPage(string key)
        {
            if (key == "overview") RefreshOverview();
            else if (key == "settings") RefreshTweaks();
            else if (key == "ace") RefreshAce();
        }

        private void RefreshAll()
        {
            if (_refreshing) return;
            _refreshing = true;
            try
            {
                _hw = HardwareInfo.Collect(_topo);
                _ace.Topology = _topo;
                _ace.RebuildTarget();
                RefreshOverview();
                RefreshTweaks();
                RefreshAce();
                UpdateAdminUi();
            }
            finally
            {
                _refreshing = false;
            }
        }

        private void RefreshOverview()
        {
            if (_hw == null || _heroTitle == null) return;
            _heroTitle.Text = "为三角洲行动让出性能";
            _heroSub.Text = _hw.CpuBrief + " · " + _hw.GpuName + " · 内存 " + _hw.RamShort;
            _heroStats.Text = _hw.OsName + " · " + _hw.DisplayWidth + "×" + _hw.DisplayHeight + " @ " + _hw.DisplayHz + "Hz · " +
                (_hw.Game.Found ? "三角洲行动已安装" : "未检测到游戏");
            int done = _tweaks.RecommendedDone(_hw);
            int available = _tweaks.RecommendedAvailable(_hw);
            _score.Value = available == 0 ? 0 : (int)Math.Round(100.0 * done / available);
            _score.Caption = "推荐完成度";
            _ovCpu.Text = _hw.CpuName + " · " + _hw.CpuBrief;
            _ovGpu.Text = _hw.GpuName + (_hw.GpuDriver.Length > 0 ? " · 驱动 " + _hw.GpuDriver : "");
            _ovRam.Text = _hw.RamText + " · 占用 " + _hw.RamLoadPercent + "%";
            _ovOs.Text = _hw.OsName + " " + _hw.OsVersion + " · Build " + _hw.OsBuild;
            _ovDisplay.Text = _hw.DisplayWidth + "×" + _hw.DisplayHeight + " @ " + _hw.DisplayHz + "Hz";
            _ovPower.Text = (_hw.OnAc ? "已插电" : "电池") + " · 计划 " + _hw.PowerPlan + " · " + _hw.OverlayName(true);
            _ovGame.Text = _hw.Game.Found ? "已安装 · 三角洲行动" : "未检测到《三角洲行动》";
            _ovAce.Text = _ace.Processes.Count == 0 ? "未运行" : _ace.Processes.Count + " 个进程";
            if (_ovGameCard != null) _ovGameCard.Text = _hw.Game.Found ? _hw.Game.InstallPath : "未检测到《三角洲行动》";
            if (_ovAceCard != null) _ovAceCard.Text = "ACE：" + (_ace.Processes.Count == 0 ? "未运行" : _ace.Processes.Count + " 个进程");
            SetPill(_ovGamePill, _hw.Game.Found ? "已安装" : "未检测到", _hw.Game.Found ? Theme.Green : Theme.Amber);
            SetPill(_ovAcePill, _ace.Processes.Count == 0 ? "ACE 未运行" : "ACE 运行中", _ace.Processes.Count == 0 ? Theme.Muted : Theme.Green);
            if (_recApply != null)
                _recApply.Text = done >= available ? "推荐已启用" : "应用剩余 " + (available - done) + " 项";
            if (done >= available && available > 0)
            {
                _recApply.Text = "推荐已启用";
            }
            for (int i = 0; i < _recPills.Count; i++)
            {
                Tweak t = _recTweaks[i];
                TweakState st = t.GetState();
                if (!t.IsAvailable(_hw)) SetPill(_recPills[i], "不可用", Theme.Faint);
                else if (st == TweakState.Enabled) SetPill(_recPills[i], "已启用", Theme.Green);
                else if (t.Id == "power_perf" && !_hw.OnAc) SetPill(_recPills[i], "插电后启用", Theme.Amber);
                else SetPill(_recPills[i], "建议启用", Theme.Accent);
            }
        }

        private void RefreshTweaks()
        {
            foreach (KeyValuePair<string, TweakCard> kv in _cards)
                kv.Value.RefreshState(_hw);
            if (_applyRecBtn != null)
            {
                int done = _tweaks.RecommendedDone(_hw);
                int available = _tweaks.RecommendedAvailable(_hw);
                _applyRecBtn.Text = done >= available ? "推荐已启用" : "一键应用推荐";
            }
        }

        private void RefreshAce()
        {
            if (_cpuMap == null) return;
            _ace.Topology = _topo;
            _ace.RebuildTarget();
            if (_ace.LastCheck == DateTime.MinValue || (DateTime.Now - _ace.LastCheck).TotalSeconds > 1.5)
                _ace.Refresh();
            _cpuMap.Topology = _topo;
            _cpuMap.SelectedMask = _ace.TargetMask;
            _cpuMap.GroupMode = _ace.Mode == "Group";
            _cpuMap.AceMask = 0;
            for (int i = 0; i < _ace.Processes.Count; i++)
            {
                AceProcessInfo p = _ace.Processes[i];
                if (p.Affinity == _ace.TargetMask) _cpuMap.AceMask |= p.Affinity;
            }
            _cpuMap.Invalidate();
            _aceTarget.Text = (_ace.HasManaged ? "当前目标：" : "可选目标（未应用）：") + _ace.TargetDescription;
            if (_ace.Processes.Count == 0)
            {
                _aceStatus.Text = "等待游戏启动 · 未检测到 ACE 进程";
                _aceStatus.ForeColor = Theme.Muted;
            }
            else if (_ace.HasManaged)
            {
                _aceStatus.Text = "已限制 " + CountManaged() + " 个 ACE 进程到小核；游戏仍可用全部核心";
                _aceStatus.ForeColor = Theme.Green;
            }
            else
            {
                _aceStatus.Text = "检测到 " + _ace.Processes.Count + " 个 ACE 进程，尚未限制";
                _aceStatus.ForeColor = Theme.Amber;
            }
            _aceLast.Text = _ace.LastCheck == DateTime.MinValue ? "" : "最近检查 " + _ace.LastCheck.ToString("HH:mm:ss") +
                (string.IsNullOrEmpty(_ace.LastMessage) ? "" : " · " + _ace.LastMessage);
            _aceProcessCount.Text = _ace.Processes.Count + " 个进程";
            _processList.SetItems(_ace.Processes);
            _refreshing = true;
            _aceAutoToggle.SetCheckedSilently(_ace.AutoApply);
            _restoreOnExitToggle.SetCheckedSilently(_store.RestoreOnExit);
            _refreshing = false;
            StyleModeButton(_modeGroup, _ace.Mode == "Group");
            StyleModeButton(_modeSingle, _ace.Mode == "Single");
            StyleModeButton(_modeCustom, _ace.Mode == "Custom");
        }

        private int CountManaged()
        {
            int n = 0;
            for (int i = 0; i < _ace.Processes.Count; i++)
                if (_ace.Processes[i].Managed) n++;
            return n;
        }

        private void StyleModeButton(RoundButton b, bool active)
        {
            b.FillColor = active ? Theme.Accent : Theme.Surface2;
            b.TextColor = active ? Color.White : Theme.Muted;
            b.BorderColor = active ? Theme.Accent : Theme.Border;
            b.Invalidate();
        }

        private void SetPill(Pill p, string text, Color color)
        {
            p.SetText(text);
            p.TextColor = color;
            p.FillColor = Theme.WithAlpha(color, 24);
            p.BorderColor = Theme.WithAlpha(color, 55);
        }

        private void UpdateAdminUi()
        {
            bool admin = Elevation.IsAdmin;
            if (admin)
            {
                _adminPill.SetText("管理员");
                _adminPill.FillColor = Theme.WithAlpha(Theme.Green, 24);
                _adminPill.TextColor = Theme.Green;
            }
            else
            {
                _adminPill.SetText("普通权限");
                _adminPill.FillColor = Theme.WithAlpha(Theme.Amber, 24);
                _adminPill.TextColor = Theme.Amber;
            }
            _elevateBtn.Visible = !admin;
            LayoutHeader();
        }

        private void ShowElevate()
        {
            if (Elevation.IsAdmin) return;
            DialogResult r = MessageBox.Show("修改系统设置或调整 ACE 亲和性需要管理员权限。\n\n现在以管理员身份重新启动吗？",
                "需要管理员权限", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return;
            _relaunching = true;
            if (Elevation.Relaunch("--page=" + CurrentPage()))
            {
                _reallyExit = true;
                Close();
            }
            else
            {
                _relaunching = false;
                ShowToast("已取消管理员授权，未做任何修改。", true);
            }
        }

        private string CurrentPage()
        {
            if (_acePage != null && _acePage.Visible) return "ace";
            if (_settingsPage != null && _settingsPage.Visible) return "settings";
            if (_helpPage != null && _helpPage.Visible) return "help";
            return "overview";
        }

        private bool EnsureAdmin(string args)
        {
            if (Elevation.IsAdmin) return true;
            DialogResult r = MessageBox.Show("此操作需要管理员权限。\n\n现在以管理员身份重新启动并继续吗？",
                "需要管理员权限", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (r != DialogResult.Yes) return false;
            _relaunching = true;
            if (Elevation.Relaunch(args))
            {
                _reallyExit = true;
                Close();
                return false;
            }
            _relaunching = false;
            ShowToast("已取消管理员授权，未做任何修改。", true);
            return false;
        }

        internal void OnTweakToggle(TweakCard card, bool enabled)
        {
            if (_refreshing) return;
            Tweak t = card.Tweak;
            if (t.Id == "power_perf" && enabled && !_hw.OnAc)
            {
                ShowToast("电源最佳性能只在插电时启用；请插电后再试。", true);
                card.RefreshState(_hw);
                return;
            }
            string args = (enabled ? "--apply=" : "--disable=") + t.Id;
            if (t.RequiresAdmin && !EnsureAdmin(args))
            {
                card.RefreshState(_hw);
                return;
            }
            TweakResult r = enabled ? t.Enable() : t.Disable();
            if (r.Ok)
            {
                ShowToast((enabled ? "已启用：" : "已关闭：") + t.Title + (t.RequiresRestart && enabled ? "（重启后生效）" : ""), false);
            }
            else
            {
                ShowToast(r.Message, true);
                Log.Error("设置失败 " + t.Id + " :: " + r.Message);
            }
            RefreshTweaks();
            RefreshOverview();
        }

        internal void OnTweakRestore(TweakCard card)
        {
            Tweak t = card.Tweak;
            if (t.RequiresAdmin && !EnsureAdmin("--restore=" + t.Id))
            {
                card.RefreshState(_hw);
                return;
            }
            TweakResult r = t.RestoreOriginal();
            ShowToast(r.Ok ? "已恢复：" + t.Title : r.Message, !r.Ok);
            RefreshTweaks();
            RefreshOverview();
        }

        private void ApplyTweakById(string id, bool enable, bool fromElevation)
        {
            Tweak t = _tweaks.Get(id);
            if (t == null)
            {
                ShowToast("未知设置：" + id, true);
                return;
            }
            if (!fromElevation && t.RequiresAdmin && !EnsureAdmin((enable ? "--apply=" : "--disable=") + id)) return;
            if (t.Id == "power_perf" && enable && !_hw.OnAc)
            {
                ShowToast("当前未插电，已跳过电源最佳性能。", true);
                return;
            }
            TweakResult r = enable ? t.Enable() : t.Disable();
            ShowToast(r.Ok ? (enable ? "已启用：" : "已关闭：") + t.Title : r.Message, !r.Ok);
            RefreshTweaks();
            RefreshOverview();
            ShowPage("settings");
        }

        private void ApplyRecommendedClicked()
        {
            bool needAdmin = false;
            for (int i = 0; i < _tweaks.All.Count; i++)
            {
                Tweak t = _tweaks.All[i];
                if (!t.Recommended || !t.IsAvailable(_hw)) continue;
                if (t.GetState() == TweakState.Enabled) continue;
                if (t.RequiresAdmin) needAdmin = true;
            }
            if (needAdmin && !EnsureAdmin("--apply=recommended")) return;
            string result = _tweaks.ApplyRecommended(_hw);
            ShowToast(result.IndexOf("部分") >= 0 ? result : "推荐项已处理完成", result.IndexOf("部分") >= 0);
            RefreshTweaks();
            RefreshOverview();
            Log.Info("应用推荐：" + result.Replace("\n", " | "));
        }

        private void RestoreAllClicked()
        {
            DialogResult confirm = MessageBox.Show("恢复本工具修改过的所有设置，并恢复 ACE 的原始亲和性？\n\n不会影响游戏文件。",
                "恢复全部", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (confirm != DialogResult.Yes) return;
            bool needAdmin = false;
            for (int i = 0; i < _tweaks.All.Count; i++)
            {
                if (!_tweaks.All[i].HasBackup) continue;
                if (_tweaks.All[i].RequiresAdmin) needAdmin = true;
            }
            if (!Elevation.IsAdmin && (_ace.HasManaged || _store.AceOriginals.Count > 0)) needAdmin = true;
            if (needAdmin && !EnsureAdmin("--restore-all")) return;
            string res = _tweaks.RestoreAll();
            AceApplyResult ar = _ace.RestoreAll();
            ShowToast("恢复完成：" + res.Replace("\n", " ") + " " + ar.Message, false);
            Log.Info("恢复全部：" + res.Replace("\n", " | "));
            RefreshTweaks();
            RefreshOverview();
            RefreshAce();
        }

        private void SetAceMode(string mode, int core)
        {
            if (mode == "Custom" && core < 0)
            {
                core = _ace.CustomCore >= 0 ? _ace.CustomCore : _topo.SingleEcoreIndex();
            }
            _ace.SetMode(mode, core);
            RefreshAce();
            ShowToast("目标已切换：" + _ace.TargetDescription, false);
        }

        private bool ConfirmAceOnce()
        {
            if (_store.Meta.ContainsKey("ace_confirmed")) return true;
            DialogResult r = MessageBox.Show(
                "本工具只调用 Windows 的进程亲和性 API，把 ACE 用户态进程限制到小核。\n\n" +
                "它不注入、不修改 ACE 文件、不结束进程；但部分反作弊版本可能不允许外部调整其进程。\n" +
                "如果游戏提示异常，请立即点“恢复原始”并重启游戏。\n\n是否继续？",
                "ACE 小核隔离", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
            if (r != DialogResult.Yes) return false;
            _store.Meta["ace_confirmed"] = "1";
            _store.Save();
            return true;
        }

        private void ApplyAce(bool fromElevation)
        {
            if (!fromElevation && !Elevation.IsAdmin && !EnsureAdmin("--ace-apply")) return;
            if (!ConfirmAceOnce()) return;
            AceApplyResult r = _ace.Apply();
            ShowToast(r.Message, !r.Ok);
            Log.Info("ACE 应用：" + r.Message);
            RefreshAce();
        }

        private void RestoreAce(bool fromElevation)
        {
            if (!fromElevation && !Elevation.IsAdmin && !EnsureAdmin("--ace-restore")) return;
            AceApplyResult r = _ace.RestoreAll();
            ShowToast(r.Message, !r.Ok);
            Log.Info("ACE 恢复：" + r.Message);
            RefreshAce();
        }

        private void RunInitialAction()
        {
            string a = _initialAction;
            if (a == "ace-apply") { ApplyAce(true); ShowPage("ace"); return; }
            if (a == "ace-restore") { RestoreAce(true); ShowPage("ace"); return; }
            if (a == "apply:recommended") { string res = _tweaks.ApplyRecommended(_hw); ShowToast(res, res.IndexOf("部分") >= 0); RefreshTweaks(); RefreshOverview(); ShowPage("settings"); return; }
            if (a == "restore-all") { string res = _tweaks.RestoreAll(); _ace.RestoreAll(); ShowToast(res, false); RefreshTweaks(); RefreshOverview(); RefreshAce(); return; }
            if (a.StartsWith("apply:")) { ApplyTweakById(a.Substring(6), true, true); return; }
            if (a.StartsWith("disable:")) { ApplyTweakById(a.Substring(8), false, true); return; }
            if (a.StartsWith("restore:")) { Tweak t = _tweaks.Get(a.Substring(8)); if (t != null) { TweakResult r = t.RestoreOriginal(); ShowToast(r.Message, !r.Ok); RefreshTweaks(); RefreshOverview(); } return; }
        }

        private void OnTick()
        {
            _tickCount++;
            try
            {
                _hw.RefreshDynamic();
                _hw.CpuUsage = _loadSampler.Sample();
                _hw.Game.RefreshRunning();
                _ace.ApplyIfAuto();
                if (_acePage.Visible) RefreshAce();
                else if (_tickCount % 3 == 0) RefreshAce();
                if (_tickCount % 4 == 0) RefreshTweaks();
                if (_tickCount % 2 == 0) RefreshOverview();
                if (_tickCount % 5 == 0) UpdateAdminUi();
            }
            catch (Exception ex)
            {
                Log.Error("定时刷新失败", ex);
            }
        }

        private void ShowToast(string message, bool error)
        {
            if (_toastPanel == null) return;
            if (string.IsNullOrEmpty(message)) message = error ? "操作失败" : "完成";
            _toastLabel.Text = message;
            _toastLabel.ForeColor = error ? Theme.Red : Theme.Text;
            _toastPanel.StrokeColor = error ? Theme.WithAlpha(Theme.Red, 110) : Theme.Border;
            _toastPanel.Visible = true;
            _toastPanel.BringToFront();
            LayoutToast();
            _toastTimer.Stop();
            _toastTimer.Start();
        }

        private void HideToast()
        {
            if (_toastPanel != null) _toastPanel.Visible = false;
        }

        private void ShowFromTray()
        {
            Show();
            WindowState = FormWindowState.Normal;
            Activate();
            BringToFront();
        }

        private void OnFormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_reallyExit && e.CloseReason == CloseReason.UserClosing && _store.MinimizeToTray && _tray != null)
            {
                e.Cancel = true;
                Hide();
                if (!_balloonShown && _tray != null)
                {
                    _balloonShown = true;
                    try
                    {
                        _tray.ShowBalloonTip(2500, "仍在后台运行", "工具会继续在托盘运行；ACE 相关功能默认关闭。双击托盘图标可重新打开。", ToolTipIcon.Info);
                    }
                    catch
                    {
                    }
                }
            }
        }

        private void OnFormClosed(object sender, FormClosedEventArgs e)
        {
            try
            {
                _tick.Stop();
                _toastTimer.Stop();
                if (_store.RestoreOnExit && !_relaunching)
                {
                    AceApplyResult r = _ace.RestoreAll();
                    Log.Info("退出恢复 ACE：" + r.Message);
                }
            }
            catch (Exception ex)
            {
                Log.Error("退出恢复失败", ex);
            }
            if (_tray != null)
            {
                _tray.Visible = false;
                _tray.Dispose();
            }
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            try
            {
                int dark = 1;
                Native.DwmSetWindowAttribute(Handle, 20, ref dark, 4);
                int corner = 2;
                Native.DwmSetWindowAttribute(Handle, 33, ref corner, 4);
            }
            catch
            {
            }
        }
    }
}
