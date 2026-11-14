using System;
using System.Drawing;
using System.Windows.Forms;

namespace DeltaForceTuner
{
    internal class TweakCard : Card
    {
        public Tweak Tweak;
        private MainForm _owner;
        private ToggleSwitch _toggle;
        private Pill _badge;
        private Label _title;
        private Label _desc;
        private Label _status;
        private Label _risk;
        private RoundButton _restore;
        private ToolTip _tip;
        private bool _suppress;

        public TweakCard(Tweak tweak, MainForm owner)
        {
            Tweak = tweak;
            _owner = owner;
            Height = 122;
            FillColor = Theme.Surface;
            StrokeColor = Theme.BorderSoft;
            Margin = new Padding(0, 0, 0, 12);

            _title = MakeLabel(tweak.Title, Theme.H3, Theme.Text);
            _title.Location = new Point(22, 14);
            _title.Height = 26;
            Controls.Add(_title);

            _badge = new Pill();
            _badge.SetText(tweak.Badge);
            _badge.FillColor = tweak.Recommended ? Theme.WithAlpha(Theme.Accent, 28) : Theme.Surface3;
            _badge.TextColor = tweak.Recommended ? Theme.Accent : Theme.Muted;
            _badge.BackColor = FillColor;
            _badge.Location = new Point(200, 15);
            Controls.Add(_badge);

            _desc = MakeLabel(tweak.Description, Theme.Small, Theme.Muted);
            _desc.Location = new Point(22, 42);
            _desc.Height = 36;
            Controls.Add(_desc);
            _tip = new ToolTip();
            _tip.SetToolTip(_desc, tweak.Description + "\n" + tweak.Risk);

            _status = MakeLabel("状态读取中", Theme.Tiny, Theme.Muted);
            _status.Location = new Point(22, 84);
            _status.Height = 18;
            Controls.Add(_status);

            _risk = MakeLabel(tweak.Risk, Theme.Tiny, Theme.Faint);
            _risk.Location = new Point(22, 101);
            _risk.Height = 16;
            Controls.Add(_risk);

            _toggle = new ToggleSwitch();
            _toggle.Location = new Point(Width - 74, 22);
            _toggle.BackColor = FillColor;
            _toggle.CheckedChanged += delegate
            {
                if (_suppress) return;
                _owner.OnTweakToggle(this, _toggle.Checked);
            };
            Controls.Add(_toggle);

            _restore = new RoundButton();
            _restore.Ghost = true;
            _restore.Text = "恢复原始";
            _restore.Font = Theme.Tiny;
            _restore.Size = new Size(88, 26);
            _restore.Location = new Point(Width - 112, 84);
            _restore.BackColor = FillColor;
            _restore.Visible = false;
            _restore.Click += delegate { _owner.OnTweakRestore(this); };
            Controls.Add(_restore);

            Resize += delegate { LayoutCard(); };
            LayoutCard();
        }

        private static Label MakeLabel(string text, Font font, Color color)
        {
            Label l = new Label();
            l.Text = text;
            l.Font = font;
            l.ForeColor = color;
            l.AutoSize = false;
            l.UseMnemonic = false;
            l.BackColor = Theme.Surface;
            l.TextAlign = ContentAlignment.TopLeft;
            return l;
        }

        private void LayoutCard()
        {
            if (_title == null) return;
            _desc.Width = Math.Max(120, Width - 170);
            _title.Width = Math.Max(100, Width - 230);
            Size ts = TextRenderer.MeasureText(_title.Text, _title.Font);
            _badge.Location = new Point(Math.Min(Width - 150, 22 + ts.Width + 12), 15);
            _toggle.Location = new Point(Width - 74, 22);
            _restore.Location = new Point(Width - 112, 84);
            _status.Width = Math.Max(110, Width - 160);
            _risk.Width = Math.Max(140, Width - 150);
        }

        public void RefreshState(HardwareInfo hw)
        {
            TweakState st = Tweak.GetState();
            bool available = Tweak.IsAvailable(hw);
            bool batteryBlocked = Tweak.Id == "power_perf" && !hw.OnAc;
            bool enabled = st == TweakState.Enabled;
            _suppress = true;
            _toggle.SetCheckedSilently(enabled);
            _suppress = false;
            _toggle.Enabled = available && !batteryBlocked;
            _restore.Visible = Tweak.HasBackup;
            _badge.SetText(available ? Tweak.Badge : "不可用");
            _badge.FillColor = !available ? Theme.Surface3 : (Tweak.Recommended ? Theme.WithAlpha(Theme.Accent, 28) : Theme.Surface3);
            _badge.TextColor = !available ? Theme.Faint : (Tweak.Recommended ? Theme.Accent : Theme.Muted);
            if (!available)
            {
                _status.Text = Tweak.UnavailableReason(hw);
                _status.ForeColor = Theme.Faint;
            }
            else if (batteryBlocked)
            {
                _status.Text = "未插电 · 插电后可启用";
                _status.ForeColor = Theme.Amber;
            }
            else if (st == TweakState.Enabled)
            {
                _status.Text = Tweak.RequiresRestart ? "已启用 · 重启后生效" : "已启用";
                _status.ForeColor = Theme.Green;
            }
            else if (st == TweakState.Disabled)
            {
                _status.Text = "未启用";
                _status.ForeColor = Theme.Muted;
            }
            else if (st == TweakState.Custom)
            {
                _status.Text = "自定义值 · 关闭或恢复可还原";
                _status.ForeColor = Theme.Amber;
            }
            else
            {
                _status.Text = "未设置（系统默认状态）";
                _status.ForeColor = Theme.Amber;
            }
            if (_tip != null) _tip.SetToolTip(_desc, Tweak.Description + "\n" + Tweak.Risk);
        }
    }
}
