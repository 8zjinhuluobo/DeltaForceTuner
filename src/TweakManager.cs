using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using Microsoft.Win32;

namespace DeltaForceTuner
{
    internal enum TweakState
    {
        Unknown,
        Enabled,
        Disabled,
        Custom
    }

    internal class TweakResult
    {
        public bool Ok;
        public string Message;

        public static TweakResult Success(string message)
        {
            TweakResult r = new TweakResult();
            r.Ok = true;
            r.Message = message;
            return r;
        }

        public static TweakResult Failure(string message)
        {
            TweakResult r = new TweakResult();
            r.Ok = false;
            r.Message = message;
            return r;
        }
    }

    internal abstract class Tweak
    {
        public string Id = "";
        public string Title = "";
        public string Description = "";
        public string Risk = "";
        public bool Recommended;
        public bool RequiresRestart;
        public bool RequiresAdmin;
        public string Badge = "可选";
        public StateStore Store;

        public abstract TweakState GetState();
        public abstract TweakResult Enable();
        public abstract TweakResult Disable();
        public abstract TweakResult RestoreOriginal();
        public abstract bool HasBackup { get; }

        public virtual bool IsAvailable(HardwareInfo hw)
        {
            return true;
        }

        public virtual string UnavailableReason(HardwareInfo hw)
        {
            return "";
        }
    }

    internal class RegBackup
    {
        public RegHive Hive;
        public string Key = "";
        public string Name = "";
        public RegistryValueKind Kind = RegistryValueKind.DWord;
        public bool Existed;
        public string Data = "";

        public static RegBackup Capture(RegValueSpec spec)
        {
            RegBackup b = new RegBackup();
            b.Hive = spec.Hive;
            b.Key = spec.Key;
            b.Name = spec.Name;
            b.Kind = spec.Kind;
            object value = RegistryUtil.Read(spec.Hive, spec.Key, spec.Name);
            b.Existed = value != null;
            if (b.Existed)
            {
                b.Kind = RegistryUtil.ReadKind(spec.Hive, spec.Key, spec.Name, spec.Kind);
                b.Data = Serialize(value, b.Kind);
            }
            return b;
        }

        public bool Restore()
        {
            if (Existed)
                return RegistryUtil.Write(Hive, Key, Name, Kind, Deserialize(Data, Kind));
            return RegistryUtil.DeleteValue(Hive, Key, Name);
        }

        private static string Serialize(object value, RegistryValueKind kind)
        {
            if (value == null) return "";
            if (kind == RegistryValueKind.DWord)
                return Convert.ToString(unchecked((uint)Convert.ToInt32(value)), CultureInfo.InvariantCulture);
            if (kind == RegistryValueKind.QWord)
                return Convert.ToString(unchecked((ulong)Convert.ToInt64(value)), CultureInfo.InvariantCulture);
            if (value is byte[])
                return Convert.ToBase64String((byte[])value);
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(value.ToString()));
        }

        private static object Deserialize(string data, RegistryValueKind kind)
        {
            if (kind == RegistryValueKind.DWord)
                return unchecked((int)uint.Parse(data, CultureInfo.InvariantCulture));
            if (kind == RegistryValueKind.QWord)
                return unchecked((long)ulong.Parse(data, CultureInfo.InvariantCulture));
            byte[] bytes = Convert.FromBase64String(data);
            if (kind == RegistryValueKind.Binary) return bytes;
            return Encoding.UTF8.GetString(bytes);
        }
    }

    internal class RegistryTweak : Tweak
    {
        public List<RegValueSpec> Values = new List<RegValueSpec>();

        public void Add(RegHive hive, string key, string name, RegistryValueKind kind, object on, object off)
        {
            Values.Add(new RegValueSpec(hive, key, name, kind, on, off));
        }

        public override TweakState GetState()
        {
            if (Values.Count == 0) return TweakState.Unknown;
            bool allOn = true;
            bool allOff = true;
            bool anyMissing = false;
            for (int i = 0; i < Values.Count; i++)
            {
                RegValueSpec s = Values[i];
                object v = RegistryUtil.Read(s.Hive, s.Key, s.Name);
                if (v == null)
                {
                    allOn = false;
                    allOff = false;
                    anyMissing = true;
                    continue;
                }
                if (!RegistryUtil.ValuesEqual(v, s.EnabledValue, s.Kind)) allOn = false;
                if (!RegistryUtil.ValuesEqual(v, s.DisabledValue, s.Kind)) allOff = false;
            }
            if (allOn) return TweakState.Enabled;
            if (allOff) return TweakState.Disabled;
            if (anyMissing) return TweakState.Unknown;
            return TweakState.Custom;
        }

        public override TweakResult Enable()
        {
            return Apply(true);
        }

        public override TweakResult Disable()
        {
            return Apply(false);
        }

        private TweakResult Apply(bool enabled)
        {
            for (int i = 0; i < Values.Count; i++)
            {
                RegValueSpec s = Values[i];
                EnsureBackup(s);
                object value = enabled ? s.EnabledValue : s.DisabledValue;
                try
                {
                    if (!RegistryUtil.Write(s.Hive, s.Key, s.Name, s.Kind, value))
                        return TweakResult.Failure("写入失败：" + s.Id);
                }
                catch (UnauthorizedAccessException)
                {
                    return TweakResult.Failure("需要管理员权限才能修改系统设置。");
                }
                catch (IOException)
                {
                    return TweakResult.Failure("无法写入备份文件，已停止修改（没有写入注册表）。");
                }
                catch (Exception ex)
                {
                    return TweakResult.Failure("写入失败：" + ex.Message);
                }
            }
            Store.Save();
            OnApplied(enabled);
            TweakState state = GetState();
            if (enabled && state != TweakState.Enabled)
                return TweakResult.Failure("设置未完全生效，可能有其他程序或策略正在覆盖它。");
            if (!enabled && state != TweakState.Disabled)
                return TweakResult.Failure("设置未完全恢复，可能有其他程序或策略正在覆盖它。");
            return TweakResult.Success(enabled ? "已启用" : "已关闭");
        }

        protected virtual void OnApplied(bool enabled)
        {
        }

        protected virtual void OnRestored()
        {
        }

        public override TweakResult RestoreOriginal()
        {
            List<RegBackup> list;
            if (!Store.Backups.TryGetValue(Id, out list) || list.Count == 0)
                return TweakResult.Success("没有需要恢复的备份。");
            for (int i = 0; i < list.Count; i++)
            {
                try
                {
                    if (!list[i].Restore())
                        return TweakResult.Failure("恢复失败：" + list[i].Key + "\\" + list[i].Name);
                }
                catch (UnauthorizedAccessException)
                {
                    return TweakResult.Failure("需要管理员权限才能恢复系统设置。");
                }
                catch (Exception ex)
                {
                    return TweakResult.Failure("恢复失败：" + ex.Message);
                }
            }
            Store.Backups.Remove(Id);
            Store.Save();
            OnRestored();
            return TweakResult.Success("已恢复到此前的原始值。");
        }

        public override bool HasBackup
        {
            get
            {
                List<RegBackup> list;
                return Store.Backups.TryGetValue(Id, out list) && list.Count > 0;
            }
        }

        protected void EnsureBackup(RegValueSpec spec)
        {
            List<RegBackup> list;
            if (!Store.Backups.TryGetValue(Id, out list))
            {
                list = new List<RegBackup>();
                Store.Backups[Id] = list;
            }
            for (int i = 0; i < list.Count; i++)
                if (list[i].Key == spec.Key && list[i].Name == spec.Name && list[i].Hive == spec.Hive) return;
            list.Add(RegBackup.Capture(spec));
            if (!Store.Save())
            {
                list.RemoveAt(list.Count - 1);
                throw new IOException("无法保存原始值");
            }
        }
    }

    internal class MouseTweak : RegistryTweak
    {
        protected override void OnApplied(bool enabled)
        {
            int speed, t1, t2;
            if (enabled)
            {
                speed = 0; t1 = 0; t2 = 0;
            }
            else
            {
                speed = ParseInt(RegistryUtil.ReadString(RegHive.CurrentUser, @"Control Panel\Mouse", "MouseSpeed", "1"), 1);
                t1 = ParseInt(RegistryUtil.ReadString(RegHive.CurrentUser, @"Control Panel\Mouse", "MouseThreshold1", "6"), 6);
                t2 = ParseInt(RegistryUtil.ReadString(RegHive.CurrentUser, @"Control Panel\Mouse", "MouseThreshold2", "10"), 10);
            }
            Native.SetMouseParameters(speed, t1, t2);
        }

        protected override void OnRestored()
        {
            int speed = ParseInt(RegistryUtil.ReadString(RegHive.CurrentUser, @"Control Panel\Mouse", "MouseSpeed", "1"), 1);
            int t1 = ParseInt(RegistryUtil.ReadString(RegHive.CurrentUser, @"Control Panel\Mouse", "MouseThreshold1", "6"), 6);
            int t2 = ParseInt(RegistryUtil.ReadString(RegHive.CurrentUser, @"Control Panel\Mouse", "MouseThreshold2", "10"), 10);
            Native.SetMouseParameters(speed, t1, t2);
        }

        private static int ParseInt(string s, int fallback)
        {
            int n;
            if (int.TryParse(s, out n)) return n;
            return fallback;
        }
    }

    internal class PowerTweak : Tweak
    {
        public override bool IsAvailable(HardwareInfo hw)
        {
            return hw.OsBuildNumber >= 22000;
        }

        public override string UnavailableReason(HardwareInfo hw)
        {
            return "此 Windows 版本不支持“电源模式”覆盖开关。";
        }

        public override TweakState GetState()
        {
            string ac = RegistryUtil.ReadString(RegHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes", "ActiveOverlayAcPowerScheme", "");
            if (string.IsNullOrEmpty(ac)) return TweakState.Unknown;
            string name = PowerNames.Overlay(ac);
            if (name == "最佳性能") return TweakState.Enabled;
            if (name == "平衡") return TweakState.Disabled;
            return TweakState.Custom;
        }

        public override TweakResult Enable()
        {
            return SetOverlay(PowerNames.BestPerformanceOverlay, true);
        }

        public override TweakResult Disable()
        {
            return SetOverlay(PowerNames.BalancedOverlay, false);
        }

        private static bool IsOnAc()
        {
            try
            {
                Native.SYSTEM_POWER_STATUS ps;
                return Native.GetSystemPowerStatus(out ps) && ps.ACLineStatus == 1;
            }
            catch
            {
                return false;
            }
        }

        private TweakResult SetOverlay(string guid, bool enabled)
        {
            if (!IsOnAc()) return TweakResult.Failure("请插电后再切换电源模式。");
            if (!Store.Meta.ContainsKey("power_ac"))
            {
                string ac = RegistryUtil.ReadString(RegHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes", "ActiveOverlayAcPowerScheme", "");
                string dc = RegistryUtil.ReadString(RegHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes", "ActiveOverlayDcPowerScheme", "");
                Store.Meta["power_ac"] = ac;
                Store.Meta["power_dc"] = dc;
                if (!Store.Save()) return TweakResult.Failure("无法写入电源模式备份，已停止修改。");
            }
            string output;
            int code = RunPowerCfg("/overlaysetactive " + guid, out output);
            if (code != 0)
            {
                if (output.IndexOf("权限", StringComparison.OrdinalIgnoreCase) >= 0 || output.IndexOf("access", StringComparison.OrdinalIgnoreCase) >= 0)
                    return TweakResult.Failure("需要管理员权限才能切换电源模式。");
                return TweakResult.Failure("powercfg 执行失败：" + Clean(output));
            }
            System.Threading.Thread.Sleep(180);
            string now = RegistryUtil.ReadString(RegHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes", "ActiveOverlayAcPowerScheme", "");
            if (enabled && now != PowerNames.BestPerformanceOverlay)
                return TweakResult.Failure("命令已执行，但电源模式没有变化（可能被电源计划锁定）。");
            if (!enabled && now != PowerNames.BalancedOverlay)
                return TweakResult.Failure("命令已执行，但电源模式没有变化（可能被电源计划锁定）。");
            return TweakResult.Success(enabled ? "已切换到最佳性能（插电）" : "已恢复平衡模式");
        }

        public override TweakResult RestoreOriginal()
        {
            string original;
            if (!Store.Meta.TryGetValue("power_ac", out original) || string.IsNullOrEmpty(original))
                return TweakResult.Success("没有需要恢复的备份。");
            if (!IsOnAc()) return TweakResult.Failure("请插电后再恢复电源模式。");
            string output;
            int code = RunPowerCfg("/overlaysetactive " + original, out output);
            if (code != 0)
                return TweakResult.Failure("恢复电源模式失败：" + Clean(output));
            System.Threading.Thread.Sleep(180);
            Store.Meta.Remove("power_ac");
            Store.Meta.Remove("power_dc");
            Store.Save();
            return TweakResult.Success("已恢复此前的电源模式。");
        }

        public override bool HasBackup
        {
            get { return Store.Meta.ContainsKey("power_ac"); }
        }

        private static string Clean(string s)
        {
            if (string.IsNullOrEmpty(s)) return "未知错误";
            s = s.Replace("\r", " ").Replace("\n", " ").Trim();
            if (s.Length > 160) s = s.Substring(0, 160) + "…";
            return s;
        }

        public static int RunPowerCfg(string args, out string output)
        {
            output = "";
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "powercfg.exe";
                psi.Arguments = args;
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;
                using (Process p = Process.Start(psi))
                {
                    if (p == null) return -1;
                    string stdout = p.StandardOutput.ReadToEnd();
                    string stderr = p.StandardError.ReadToEnd();
                    if (!p.WaitForExit(8000))
                    {
                        try { p.Kill(); } catch { }
                        output = "命令超时";
                        return -1;
                    }
                    output = (stdout + " " + stderr).Trim();
                    return p.ExitCode;
                }
            }
            catch (Exception ex)
            {
                output = ex.Message;
                return -1;
            }
        }
    }

    internal class TweakManager
    {
        public List<Tweak> All = new List<Tweak>();
        private Dictionary<string, Tweak> _byId = new Dictionary<string, Tweak>(StringComparer.OrdinalIgnoreCase);
        public StateStore Store;

        public TweakManager(StateStore store)
        {
            Store = store;
            Build();
        }

        private void Build()
        {
            RegistryTweak gameMode = new RegistryTweak();
            gameMode.Id = "game_mode";
            gameMode.Title = "Windows 游戏模式";
            gameMode.Description = "让 Windows 优先把 CPU 和 GPU 资源留给正在运行的游戏，减少后台程序抢占。";
            gameMode.Risk = "低风险 · 可随时关闭";
            gameMode.Recommended = true;
            gameMode.Badge = "推荐";
            gameMode.Add(RegHive.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", RegistryValueKind.DWord, 1, 0);
            gameMode.Add(RegHive.CurrentUser, @"Software\Microsoft\GameBar", "AllowAutoGameMode", RegistryValueKind.DWord, 1, 0);
            Add(gameMode);

            RegistryTweak gameDvr = new RegistryTweak();
            gameDvr.Id = "game_dvr";
            gameDvr.Title = "关闭后台录制";
            gameDvr.Description = "关闭 Game DVR 的后台持续录制，减少编码和磁盘写入；Game Bar 的录制功能也可能一并关闭，可随时恢复。";
            gameDvr.Risk = "低风险 · 仅影响后台录制";
            gameDvr.Recommended = true;
            gameDvr.Badge = "推荐";
            gameDvr.Add(RegHive.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", RegistryValueKind.DWord, 0, 1);
            gameDvr.Add(RegHive.CurrentUser, @"Software\Microsoft\Windows\CurrentVersion\GameDVR", "AppCaptureEnabled", RegistryValueKind.DWord, 0, 1);
            Add(gameDvr);

            RegistryTweak multimedia = new RegistryTweak();
            multimedia.Id = "multimedia";
            multimedia.Title = "多媒体与网络调度";
            multimedia.Description = "关闭网络节流，并让 Windows 的多媒体调度更偏向游戏线程；适合竞技射击。";
            multimedia.Risk = "低风险 · 可恢复默认";
            multimedia.Recommended = true;
            multimedia.RequiresAdmin = true;
            multimedia.Badge = "推荐";
            string mm = @"SOFTWARE\Microsoft\Windows NT\CurrentVersion\Multimedia\SystemProfile";
            multimedia.Add(RegHive.LocalMachine, mm, "NetworkThrottlingIndex", RegistryValueKind.DWord, unchecked((int)0xFFFFFFFF), 10);
            multimedia.Add(RegHive.LocalMachine, mm, "SystemResponsiveness", RegistryValueKind.DWord, 10, 20);
            string games = mm + @"\Tasks\Games";
            multimedia.Add(RegHive.LocalMachine, games, "Priority", RegistryValueKind.DWord, 6, 2);
            multimedia.Add(RegHive.LocalMachine, games, "Scheduling Category", RegistryValueKind.String, "High", "Medium");
            multimedia.Add(RegHive.LocalMachine, games, "SFIO Priority", RegistryValueKind.String, "High", "Normal");
            Add(multimedia);

            MouseTweak mouse = new MouseTweak();
            mouse.Id = "mouse_accel";
            mouse.Title = "关闭鼠标加速";
            mouse.Description = "移除 Windows 指针加速，让 FPS 瞄准更线性、更可预测；会改变鼠标手感。";
            mouse.Risk = "会改变手感 · 可随时恢复";
            mouse.Recommended = true;
            mouse.Badge = "推荐";
            mouse.Add(RegHive.CurrentUser, @"Control Panel\Mouse", "MouseSpeed", RegistryValueKind.String, "0", "1");
            mouse.Add(RegHive.CurrentUser, @"Control Panel\Mouse", "MouseThreshold1", RegistryValueKind.String, "0", "6");
            mouse.Add(RegHive.CurrentUser, @"Control Panel\Mouse", "MouseThreshold2", RegistryValueKind.String, "0", "10");
            Add(mouse);

            PowerTweak power = new PowerTweak();
            power.Id = "power_perf";
            power.Title = "电源模式：最佳性能";
            power.Description = "插电时把 Windows 电源模式切到“最佳性能”，减少降频和调度保守；电池模式不启用。";
            power.Risk = "建议插电使用 · 会增加功耗与发热";
            power.Recommended = true;
            power.RequiresAdmin = true;
            power.Badge = "推荐";
            Add(power);

            RegistryTweak hags = new RegistryTweak();
            hags.Id = "hags";
            hags.Title = "硬件加速 GPU 调度";
            hags.Description = "让 GPU 自己管理显存调度，部分新驱动可降低延迟；部分核显/老驱动可能反而卡顿。";
            hags.Risk = "需要重启 · 可选，不一定适合每台电脑";
            hags.Recommended = false;
            hags.RequiresAdmin = true;
            hags.RequiresRestart = true;
            hags.Badge = "可选";
            hags.Add(RegHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", RegistryValueKind.DWord, 2, 1);
            Add(hags);
        }

        private void Add(Tweak t)
        {
            t.Store = Store;
            All.Add(t);
            _byId[t.Id] = t;
        }

        public Tweak Get(string id)
        {
            Tweak t;
            _byId.TryGetValue(id, out t);
            return t;
        }

        public List<Tweak> RecommendedTweaks()
        {
            List<Tweak> list = new List<Tweak>();
            for (int i = 0; i < All.Count; i++)
                if (All[i].Recommended) list.Add(All[i]);
            return list;
        }

        public int RecommendedDone(HardwareInfo hw)
        {
            List<Tweak> list = RecommendedTweaks();
            int n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (!list[i].IsAvailable(hw)) continue;
                if (list[i].GetState() == TweakState.Enabled) n++;
            }
            return n;
        }

        public int RecommendedAvailable(HardwareInfo hw)
        {
            List<Tweak> list = RecommendedTweaks();
            int n = 0;
            for (int i = 0; i < list.Count; i++)
                if (list[i].IsAvailable(hw)) n++;
            return n;
        }

        public string ApplyRecommended(HardwareInfo hw)
        {
            StringBuilder sb = new StringBuilder();
            int ok = 0;
            int fail = 0;
            for (int i = 0; i < All.Count; i++)
            {
                Tweak t = All[i];
                if (!t.Recommended || !t.IsAvailable(hw)) continue;
                if (t.Id == "power_perf" && !hw.OnAc)
                {
                    sb.AppendLine("• 电源模式：当前未插电，已跳过");
                    continue;
                }
                if (t.GetState() == TweakState.Enabled)
                {
                    sb.AppendLine("• " + t.Title + "：已启用");
                    ok++;
                    continue;
                }
                TweakResult r = t.Enable();
                if (r.Ok)
                {
                    ok++;
                    sb.AppendLine("• " + t.Title + "：已启用");
                    if (t.RequiresRestart) sb.AppendLine("  重启后生效");
                }
                else
                {
                    fail++;
                    sb.AppendLine("• " + t.Title + "：" + r.Message);
                }
            }
            if (fail == 0) return "推荐项已应用完成。\n\n" + sb.ToString();
            return "部分设置未能应用。\n\n" + sb.ToString();
        }

        public string RestoreAll()
        {
            StringBuilder sb = new StringBuilder();
            int n = 0;
            for (int i = 0; i < All.Count; i++)
            {
                if (!All[i].HasBackup) continue;
                TweakResult r = All[i].RestoreOriginal();
                sb.AppendLine("• " + All[i].Title + "：" + r.Message);
                if (r.Ok) n++;
            }
            if (n == 0) return "没有需要恢复的设置。";
            return "恢复完成。\n\n" + sb.ToString();
        }

        public bool HasAnyBackup()
        {
            for (int i = 0; i < All.Count; i++)
                if (All[i].HasBackup) return true;
            return false;
        }
    }

    internal class AceOriginalRecord
    {
        public int Pid;
        public long StartTicks;
        public ulong Mask;
    }

    internal class StateStore
    {
        public string FilePath = "";
        public Dictionary<string, List<RegBackup>> Backups = new Dictionary<string, List<RegBackup>>(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> Meta = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        public List<AceOriginalRecord> AceOriginals = new List<AceOriginalRecord>();
        public string AceMode = "Group";
        public int AceCore = -1;
        public bool AutoApply = false;
        public bool RestoreOnExit = true;
        public bool MinimizeToTray = true;
        public string LastPage = "overview";

        public static StateStore Load()
        {
            StateStore s = new StateStore();
            s.FilePath = Path.Combine(AppPaths.DataDir, "state.xml");
            if (!File.Exists(s.FilePath)) return s;
            try
            {
                XmlDocument doc = new XmlDocument();
                doc.Load(s.FilePath);
                XmlNode root = doc.SelectSingleNode("/TunerState");
                if (root == null) return s;
                XmlNode backups = root.SelectSingleNode("Backups");
                if (backups != null)
                {
                    foreach (XmlNode tn in backups.ChildNodes)
                    {
                        if (tn.Name != "Tweak") continue;
                        string id = Attr(tn, "id");
                        List<RegBackup> list = new List<RegBackup>();
                        foreach (XmlNode vn in tn.ChildNodes)
                        {
                            if (vn.Name != "Value") continue;
                            RegBackup b = new RegBackup();
                            b.Hive = Attr(vn, "hive") == "HKLM" ? RegHive.LocalMachine : RegHive.CurrentUser;
                            b.Key = Attr(vn, "key");
                            b.Name = Attr(vn, "name");
                            try { b.Kind = (RegistryValueKind)Enum.Parse(typeof(RegistryValueKind), Attr(vn, "kind"), true); }
                            catch { b.Kind = RegistryValueKind.DWord; }
                            b.Existed = Attr(vn, "existed") == "true";
                            b.Data = Attr(vn, "data");
                            list.Add(b);
                        }
                        if (list.Count > 0) s.Backups[id] = list;
                    }
                }
                XmlNode meta = root.SelectSingleNode("Meta");
                if (meta != null)
                {
                    foreach (XmlNode mn in meta.ChildNodes)
                    {
                        if (mn.Name != "Item") continue;
                        s.Meta[Attr(mn, "key")] = Attr(mn, "value");
                    }
                }
                XmlNode ace = root.SelectSingleNode("Ace");
                if (ace != null)
                {
                    s.AceMode = Attr(ace, "mode");
                    if (string.IsNullOrEmpty(s.AceMode)) s.AceMode = "Group";
                    int core;
                    if (int.TryParse(Attr(ace, "core"), out core)) s.AceCore = core;
                    s.AutoApply = Attr(ace, "auto") == "true";
                    s.RestoreOnExit = Attr(ace, "restore") != "false";
                    s.MinimizeToTray = Attr(ace, "tray") != "false";
                    s.LastPage = Attr(ace, "page");
                    if (string.IsNullOrEmpty(s.LastPage)) s.LastPage = "overview";
                }
                XmlNode originals = root.SelectSingleNode("AceOriginals");
                if (originals != null)
                {
                    foreach (XmlNode on in originals.ChildNodes)
                    {
                        if (on.Name != "Process") continue;
                        AceOriginalRecord r = new AceOriginalRecord();
                        int pid;
                        long st;
                        ulong mask;
                        if (!int.TryParse(Attr(on, "pid"), out pid)) continue;
                        if (!long.TryParse(Attr(on, "start"), out st)) continue;
                        if (!ulong.TryParse(Attr(on, "mask"), out mask)) continue;
                        r.Pid = pid; r.StartTicks = st; r.Mask = mask;
                        s.AceOriginals.Add(r);
                    }
                }
            }
            catch
            {
                try { File.Move(s.FilePath, s.FilePath + ".bad"); } catch { }
            }
            return s;
        }

        private static string Attr(XmlNode n, string name)
        {
            if (n.Attributes == null) return "";
            XmlAttribute a = n.Attributes[name];
            return a == null ? "" : a.Value;
        }

        public bool Save()
        {
            try
            {
                string dir = Path.GetDirectoryName(FilePath);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                string tmp = FilePath + ".tmp";
                XmlWriterSettings settings = new XmlWriterSettings();
                settings.Indent = true;
                settings.Encoding = new UTF8Encoding(false);
                using (XmlWriter w = XmlWriter.Create(tmp, settings))
                {
                    w.WriteStartDocument();
                    w.WriteStartElement("TunerState");
                    w.WriteAttributeString("version", "1");
                    w.WriteStartElement("Backups");
                    foreach (KeyValuePair<string, List<RegBackup>> kv in Backups)
                    {
                        w.WriteStartElement("Tweak");
                        w.WriteAttributeString("id", kv.Key);
                        for (int i = 0; i < kv.Value.Count; i++)
                        {
                            RegBackup b = kv.Value[i];
                            w.WriteStartElement("Value");
                            w.WriteAttributeString("hive", b.Hive == RegHive.LocalMachine ? "HKLM" : "HKCU");
                            w.WriteAttributeString("key", b.Key);
                            w.WriteAttributeString("name", b.Name);
                            w.WriteAttributeString("kind", b.Kind.ToString());
                            w.WriteAttributeString("existed", b.Existed ? "true" : "false");
                            w.WriteAttributeString("data", b.Data);
                            w.WriteEndElement();
                        }
                        w.WriteEndElement();
                    }
                    w.WriteEndElement();
                    w.WriteStartElement("Meta");
                    foreach (KeyValuePair<string, string> kv in Meta)
                    {
                        w.WriteStartElement("Item");
                        w.WriteAttributeString("key", kv.Key);
                        w.WriteAttributeString("value", kv.Value);
                        w.WriteEndElement();
                    }
                    w.WriteEndElement();
                    w.WriteStartElement("Ace");
                    w.WriteAttributeString("mode", AceMode);
                    w.WriteAttributeString("core", AceCore.ToString(CultureInfo.InvariantCulture));
                    w.WriteAttributeString("auto", AutoApply ? "true" : "false");
                    w.WriteAttributeString("restore", RestoreOnExit ? "true" : "false");
                    w.WriteAttributeString("tray", MinimizeToTray ? "true" : "false");
                    w.WriteAttributeString("page", LastPage);
                    w.WriteEndElement();
                    w.WriteStartElement("AceOriginals");
                    for (int i = 0; i < AceOriginals.Count; i++)
                    {
                        AceOriginalRecord r = AceOriginals[i];
                        w.WriteStartElement("Process");
                        w.WriteAttributeString("pid", r.Pid.ToString(CultureInfo.InvariantCulture));
                        w.WriteAttributeString("start", r.StartTicks.ToString(CultureInfo.InvariantCulture));
                        w.WriteAttributeString("mask", r.Mask.ToString(CultureInfo.InvariantCulture));
                        w.WriteEndElement();
                    }
                    w.WriteEndElement();
                    w.WriteEndElement();
                    w.WriteEndDocument();
                }
                File.Copy(tmp, FilePath, true);
                File.Delete(tmp);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
