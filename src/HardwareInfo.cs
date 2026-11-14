using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using Microsoft.Win32;

namespace DeltaForceTuner
{
    internal class GameInfo
    {
        public bool Found;
        public string InstallPath = "";
        public string GameExe = "";
        public string AceDir = "";
        public string Source = "";
        public bool Running;
        public int RunningPid;

        public void RefreshRunning()
        {
            Running = false;
            RunningPid = 0;
            string[] names = new string[] { "DeltaForceClient-Win64-Shipping", "BHDClient-Win64-Shipping" };
            for (int n = 0; n < names.Length; n++)
            {
                Process[] ps = Process.GetProcessesByName(names[n]);
                if (ps.Length > 0)
                {
                    Running = true;
                    RunningPid = ps[0].Id;
                    for (int i = 0; i < ps.Length; i++) ps[i].Dispose();
                    return;
                }
            }
        }
    }

    internal class HardwareInfo
    {
        public string CpuName = "未知处理器";
        public int CoreCount;
        public int ThreadCount;
        public int PCount;
        public int ECount;
        public bool IsHybrid;
        public ulong TotalRamBytes;
        public ulong AvailRamBytes;
        public int RamLoadPercent;
        public string GpuName = "未检测到独立显卡";
        public string GpuDriver = "";
        public string GpuDriverDate = "";
        public string OsName = "Windows";
        public string OsVersion = "";
        public string OsBuild = "";
        public int OsBuildNumber;
        public int DisplayWidth;
        public int DisplayHeight;
        public int DisplayHz;
        public bool OnAc = true;
        public bool HasBattery;
        public int BatteryPercent = 255;
        public bool VbsEnabled;
        public bool MemoryIntegrity;
        public int HagsMode = -1;
        public string ActiveOverlayAc = "";
        public string ActiveOverlayDc = "";
        public string PowerPlan = "";
        public bool GameModeValue;
        public bool GameDvrValue;
        public bool MouseAccelValue;
        public int CpuUsage;
        public bool IsAdmin;
        public GameInfo Game = new GameInfo();

        public string RamText
        {
            get { return string.Format("{0:0.0} GB（可用 {1:0.0} GB）", (double)TotalRamBytes / 1073741824.0, (double)AvailRamBytes / 1073741824.0); }
        }

        public string RamShort
        {
            get { return string.Format("{0:0.0} GB", (double)TotalRamBytes / 1073741824.0); }
        }

        public string CpuBrief
        {
            get
            {
                if (IsHybrid) return string.Format("{0} 核 {1} 线程 · {2}P+{3}E", CoreCount, ThreadCount, PCount, ECount);
                return string.Format("{0} 核 {1} 线程", CoreCount, ThreadCount);
            }
        }

        public static HardwareInfo Collect(CpuTopology topo)
        {
            HardwareInfo h = new HardwareInfo();
            h.IsAdmin = Native.IsAdministrator();
            h.CoreCount = topo.Cores.Count;
            h.ThreadCount = topo.ThreadCount;
            h.PCount = topo.PCores.Count;
            h.ECount = topo.ECores.Count;
            h.IsHybrid = topo.IsHybrid;

            string cpu = RegistryUtil.ReadString(RegHive.LocalMachine, @"HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString", "");
            if (!string.IsNullOrEmpty(cpu)) h.CpuName = cpu.Trim();

            Native.MEMORYSTATUSEX mem = new Native.MEMORYSTATUSEX();
            mem.dwLength = (uint)Marshal.SizeOf(typeof(Native.MEMORYSTATUSEX));
            if (Native.GlobalMemoryStatusEx(ref mem))
            {
                h.TotalRamBytes = mem.ullTotalPhys;
                h.AvailRamBytes = mem.ullAvailPhys;
                h.RamLoadPercent = (int)mem.dwMemoryLoad;
            }

            ReadGpu(h);
            ReadOs(h);
            ReadDisplay(h);
            ReadPower(h);
            ReadSecurity(h);
            ReadSettings(h);
            h.Game = GameLocator.Find();
            h.Game.RefreshRunning();
            return h;
        }

        public void RefreshDynamic()
        {
            Native.MEMORYSTATUSEX mem = new Native.MEMORYSTATUSEX();
            mem.dwLength = (uint)Marshal.SizeOf(typeof(Native.MEMORYSTATUSEX));
            if (Native.GlobalMemoryStatusEx(ref mem))
            {
                h_UpdateRam(mem);
            }
            Native.SYSTEM_POWER_STATUS ps;
            if (Native.GetSystemPowerStatus(out ps))
            {
                OnAc = ps.ACLineStatus == 1;
                HasBattery = ps.BatteryFlag != 128 && ps.BatteryFlag != 255;
                BatteryPercent = ps.BatteryLifePercent;
            }
        }

        private void h_UpdateRam(Native.MEMORYSTATUSEX mem)
        {
            AvailRamBytes = mem.ullAvailPhys;
            TotalRamBytes = mem.ullTotalPhys;
            RamLoadPercent = (int)mem.dwMemoryLoad;
        }

        private static void ReadGpu(HardwareInfo h)
        {
            try
            {
                using (RegistryKey b = RegistryUtil.OpenBase(RegHive.LocalMachine, false))
                using (RegistryKey k = b.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Class\{4d36e968-e325-11ce-bfc1-08002be10318}"))
                {
                    if (k == null) return;
                    string[] subs = k.GetSubKeyNames();
                    int bestScore = -100;
                    for (int i = 0; i < subs.Length; i++)
                    {
                        if (subs[i].Length != 4) continue;
                        using (RegistryKey sk = k.OpenSubKey(subs[i]))
                        {
                            if (sk == null) continue;
                            string desc = Convert.ToString(sk.GetValue("DriverDesc", ""));
                            if (string.IsNullOrEmpty(desc)) continue;
                            if (desc.IndexOf("Virtual", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                            if (desc.IndexOf("Basic", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                            if (desc.IndexOf("Remote", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                            int score = 0;
                            if (desc.IndexOf("NVIDIA", StringComparison.OrdinalIgnoreCase) >= 0) score += 20;
                            if (desc.IndexOf("AMD", StringComparison.OrdinalIgnoreCase) >= 0 || desc.IndexOf("Radeon", StringComparison.OrdinalIgnoreCase) >= 0) score += 18;
                            if (desc.IndexOf("Intel", StringComparison.OrdinalIgnoreCase) >= 0) score += 10;
                            if (score > bestScore)
                            {
                                bestScore = score;
                                h.GpuName = desc;
                                h.GpuDriver = Convert.ToString(sk.GetValue("DriverVersion", ""));
                                h.GpuDriverDate = Convert.ToString(sk.GetValue("DriverDate", ""));
                            }
                        }
                    }
                }
            }
            catch
            {
            }
        }

        private static void ReadOs(HardwareInfo h)
        {
            try
            {
                using (RegistryKey b = RegistryUtil.OpenBase(RegHive.LocalMachine, false))
                using (RegistryKey k = b.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion"))
                {
                    if (k != null)
                    {
                        string product = Convert.ToString(k.GetValue("ProductName", "Windows"));
                        string display = Convert.ToString(k.GetValue("DisplayVersion", ""));
                        string build = Convert.ToString(k.GetValue("CurrentBuild", ""));
                        int ubr = 0;
                        try { ubr = Convert.ToInt32(k.GetValue("UBR", 0)); } catch { }
                        int bn = 0;
                        int.TryParse(build, out bn);
                        h.OsBuildNumber = bn;
                        if (bn >= 22000 && product.StartsWith("Windows 10", StringComparison.OrdinalIgnoreCase))
                            product = product.Replace("Windows 10", "Windows 11");
                        h.OsName = product;
                        h.OsVersion = display;
                        h.OsBuild = string.Format("{0}.{1}", build, ubr);
                    }
                }
            }
            catch
            {
            }
            if (string.IsNullOrEmpty(h.OsBuild)) h.OsBuild = Environment.OSVersion.Version.ToString();
        }

        private static void ReadDisplay(HardwareInfo h)
        {
            try
            {
                Native.DEVMODE dm = new Native.DEVMODE();
                dm.dmSize = (short)Marshal.SizeOf(typeof(Native.DEVMODE));
                if (Native.EnumDisplaySettings(null, -1, ref dm))
                {
                    h.DisplayWidth = dm.dmPelsWidth;
                    h.DisplayHeight = dm.dmPelsHeight;
                    h.DisplayHz = dm.dmDisplayFrequency;
                }
                else if (Screen.PrimaryScreen != null)
                {
                    h.DisplayWidth = Screen.PrimaryScreen.Bounds.Width;
                    h.DisplayHeight = Screen.PrimaryScreen.Bounds.Height;
                }
            }
            catch
            {
            }
        }

        private static void ReadPower(HardwareInfo h)
        {
            try
            {
                Native.SYSTEM_POWER_STATUS ps;
                if (Native.GetSystemPowerStatus(out ps))
                {
                    h.OnAc = ps.ACLineStatus == 1;
                    h.HasBattery = ps.BatteryFlag != 128 && ps.BatteryFlag != 255;
                    h.BatteryPercent = ps.BatteryLifePercent;
                }
            }
            catch
            {
            }
            h.ActiveOverlayAc = RegistryUtil.ReadString(RegHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes", "ActiveOverlayAcPowerScheme", "");
            h.ActiveOverlayDc = RegistryUtil.ReadString(RegHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes", "ActiveOverlayDcPowerScheme", "");
            string plan = RegistryUtil.ReadString(RegHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\Power\User\PowerSchemes", "ActivePowerScheme", "");
            h.PowerPlan = PowerNames.Plan(plan);
        }

        private static void ReadSecurity(HardwareInfo h)
        {
            h.VbsEnabled = RegistryUtil.ReadDword(RegHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\DeviceGuard", "EnableVirtualizationBasedSecurity", 0) == 1;
            h.MemoryIntegrity = RegistryUtil.ReadDword(RegHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\HypervisorEnforcedCodeIntegrity", "Enabled", 0) == 1;
        }

        private static void ReadSettings(HardwareInfo h)
        {
            h.HagsMode = RegistryUtil.ReadDword(RegHive.LocalMachine, @"SYSTEM\CurrentControlSet\Control\GraphicsDrivers", "HwSchMode", -1);
            h.GameModeValue = RegistryUtil.ReadDword(RegHive.CurrentUser, @"Software\Microsoft\GameBar", "AutoGameModeEnabled", 0) == 1;
            h.GameDvrValue = RegistryUtil.ReadDword(RegHive.CurrentUser, @"System\GameConfigStore", "GameDVR_Enabled", 0) == 1;
            h.MouseAccelValue = RegistryUtil.ReadString(RegHive.CurrentUser, @"Control Panel\Mouse", "MouseSpeed", "1") != "0";
        }

        public string OverlayName(bool ac)
        {
            string g = ac ? ActiveOverlayAc : ActiveOverlayDc;
            return PowerNames.Overlay(g);
        }
    }

    internal static class PowerNames
    {
        public const string BalancedOverlay = "00000000-0000-0000-0000-000000000000";
        public const string BestPerformanceOverlay = "ded574b5-45a0-4f42-8737-46345c09c238";
        public const string BestEfficiencyOverlay = "961cc777-2547-4f9d-8174-7d86181b8a7a";

        public static string Overlay(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return "未知";
            string g = guid.Trim().ToLowerInvariant();
            if (g == BalancedOverlay) return "平衡";
            if (g == BestPerformanceOverlay) return "最佳性能";
            if (g == BestEfficiencyOverlay) return "最佳能效";
            return "自定义";
        }

        public static string Plan(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return "未知";
            string g = guid.Trim().ToLowerInvariant();
            if (g == "381b4222-f694-41f0-9685-ff5bb260df2e") return "平衡";
            if (g == "8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c") return "高性能";
            if (g == "a1841308-3541-4fab-bc81-f71556f20b4a") return "节能";
            if (g == "e9a42b02-d5df-448d-aa00-03f14749eb61") return "卓越性能";
            return "自定义";
        }
    }

    internal static class GameLocator
    {
        public static GameInfo Find()
        {
            GameInfo g = new GameInfo();
            List<string> candidates = new List<string>();
            AddUninstallCandidates(candidates);
            AddSteamLibraries(candidates);
            AddKnownPaths(candidates);
            for (int i = 0; i < candidates.Count; i++)
            {
                string c = candidates[i];
                if (string.IsNullOrEmpty(c)) continue;
                if (TryInstall(c, g)) return g;
            }
            return g;
        }

        private static void AddUninstallCandidates(List<string> list)
        {
            RegistryHive[] hives = new RegistryHive[] { RegistryHive.LocalMachine, RegistryHive.CurrentUser };
            RegistryView[] views = new RegistryView[] { RegistryView.Registry64, RegistryView.Registry32 };
            for (int hi = 0; hi < hives.Length; hi++)
            {
                for (int vi = 0; vi < views.Length; vi++)
                {
                    if (hives[hi] == RegistryHive.CurrentUser && views[vi] == RegistryView.Registry32) continue;
                    try
                    {
                        using (RegistryKey b = RegistryKey.OpenBaseKey(hives[hi], views[vi]))
                        using (RegistryKey k = b.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall"))
                        {
                            if (k == null) continue;
                            string[] names = k.GetSubKeyNames();
                            for (int i = 0; i < names.Length; i++)
                            {
                                using (RegistryKey sk = k.OpenSubKey(names[i]))
                                {
                                    if (sk == null) continue;
                                    string dn = Convert.ToString(sk.GetValue("DisplayName", ""));
                                    if (string.IsNullOrEmpty(dn)) continue;
                                    bool match = dn.IndexOf("三角洲行动", StringComparison.OrdinalIgnoreCase) >= 0 ||
                                                 dn.IndexOf("Delta Force", StringComparison.OrdinalIgnoreCase) >= 0;
                                    if (!match) continue;
                                    if (dn.IndexOf("助手", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                                    if (dn.IndexOf("精彩时刻", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                                    string loc = Convert.ToString(sk.GetValue("InstallLocation", ""));
                                    string icon = Convert.ToString(sk.GetValue("DisplayIcon", ""));
                                    if (!string.IsNullOrEmpty(loc)) list.Add(loc.Trim('"'));
                                    if (!string.IsNullOrEmpty(icon))
                                    {
                                        string p = icon.Trim('"');
                                        int comma = p.LastIndexOf(',');
                                        if (comma > 0) p = p.Substring(0, comma);
                                        if (p.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) p = Path.GetDirectoryName(p);
                                        if (!string.IsNullOrEmpty(p)) list.Add(p);
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                }
            }
        }

        private static void AddSteamLibraries(List<string> list)
        {
            string[] steamRoots = new string[]
            {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"),
                @"D:\Steam", @"E:\Steam", @"D:\SteamLibrary", @"E:\SteamLibrary"
            };
            for (int i = 0; i < steamRoots.Length; i++)
            {
                try
                {
                    string vdf = Path.Combine(steamRoots[i], @"steamapps\libraryfolders.vdf");
                    if (!File.Exists(vdf)) continue;
                    string[] lines = File.ReadAllLines(vdf);
                    for (int n = 0; n < lines.Length; n++)
                    {
                        string line = lines[n].Trim();
                        int a = line.IndexOf('"');
                        if (a < 0) continue;
                        int b = line.IndexOf('"', a + 1);
                        if (b < 0) continue;
                        string key = line.Substring(a + 1, b - a - 1);
                        if (!string.Equals(key, "path", StringComparison.OrdinalIgnoreCase)) continue;
                        int c = line.IndexOf('"', b + 1);
                        int d = c < 0 ? -1 : line.IndexOf('"', c + 1);
                        if (c < 0 || d < 0) continue;
                        string path = line.Substring(c + 1, d - c - 1).Replace(@"\\", @"\");
                        list.Add(Path.Combine(path, @"steamapps\common\Delta Force"));
                    }
                }
                catch
                {
                }
            }
        }

        private static void AddKnownPaths(List<string> list)
        {
            string[] paths = new string[]
            {
                @"C:\Program Files (x86)\Steam\steamapps\common\Delta Force",
                @"C:\Program Files\Steam\steamapps\common\Delta Force",
                @"D:\Steam\steamapps\common\Delta Force",
                @"D:\SteamLibrary\steamapps\common\Delta Force",
                @"E:\Steam\steamapps\common\Delta Force",
                @"E:\SteamLibrary\steamapps\common\Delta Force",
                @"D:\Games\Delta Force",
                @"E:\Games\Delta Force",
                @"D:\APPs_D\Steam\steamapps\common\Delta Force"
            };
            for (int i = 0; i < paths.Length; i++) list.Add(paths[i]);
        }

        private static bool TryInstall(string candidate, GameInfo g)
        {
            try
            {
                if (string.IsNullOrEmpty(candidate)) return false;
                string dir = candidate.Trim().Trim('"');
                if (File.Exists(dir)) dir = Path.GetDirectoryName(dir);
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) return false;

                string[] rels = new string[]
                {
                    @"Game\DeltaForce\Binaries\Win64\DeltaForceClient-Win64-Shipping.exe",
                    @"Game\BlackHawkDown\BHD\Binaries\Win64\BHDClient-Win64-Shipping.exe"
                };
                for (int level = 0; level < 4 && !string.IsNullOrEmpty(dir); level++)
                {
                    for (int i = 0; i < rels.Length; i++)
                    {
                        string exe = Path.Combine(dir, rels[i]);
                        if (File.Exists(exe))
                        {
                            g.Found = true;
                            g.InstallPath = dir;
                            g.GameExe = exe;
                            g.Source = i == 0 ? "正式客户端" : "BHD 客户端";
                            string ace = Path.Combine(Path.GetDirectoryName(exe), "AntiCheatExpert");
                            if (Directory.Exists(ace)) g.AceDir = ace;
                            return true;
                        }
                    }
                    DirectoryInfo parent = Directory.GetParent(dir);
                    if (parent == null) break;
                    dir = parent.FullName;
                }
            }
            catch
            {
            }
            return false;
        }
    }
}
