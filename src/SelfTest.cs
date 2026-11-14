using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Win32;

namespace DeltaForceTuner
{
    internal static class SelfTest
    {
        public static int Run(string outputPath)
        {
            StringBuilder sb = new StringBuilder();
            int warnings = 0;
            try
            {
                CpuTopology topo = CpuTopology.Detect();
                HardwareInfo hw = HardwareInfo.Collect(topo);
                StateStore store = StateStore.Load();
                TweakManager tweaks = new TweakManager(store);
                AceManager ace = new AceManager(topo, store);
                ace.Refresh();

                sb.AppendLine("三角洲行动 · 本机调优助手 — 只读自检");
                sb.AppendLine("生成时间: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
                sb.AppendLine("程序版本: 1.0.1");
                sb.AppendLine("管理员权限: " + (Elevation.IsAdmin ? "是" : "否"));
                sb.AppendLine("配置文件: " + store.FilePath);
                sb.AppendLine();
                sb.AppendLine("[系统]");
                sb.AppendLine("OS: " + hw.OsName + " " + hw.OsVersion + " Build " + hw.OsBuild);
                sb.AppendLine("CPU: " + hw.CpuName);
                sb.AppendLine("拓扑: " + hw.CpuBrief + (topo.IsHybrid ? " (P=" + topo.PCores.Count + ", E=" + topo.ECores.Count + ")" : ""));
                for (int i = 0; i < topo.Cores.Count; i++)
                {
                    CpuCore c = topo.Cores[i];
                    sb.AppendLine(string.Format("  {0,-4} {1,-4} mask=0x{2:X} firstCPU={3} logical={4} eff={5}",
                        c.Label, c.Kind, c.Mask, c.FirstCpu, c.LogicalCount, c.EfficiencyClass));
                }
                sb.AppendLine("内存: " + hw.RamText + " (占用 " + hw.RamLoadPercent + "%)");
                sb.AppendLine("GPU: " + hw.GpuName + " / 驱动 " + hw.GpuDriver);
                sb.AppendLine("显示: " + hw.DisplayWidth + "x" + hw.DisplayHeight + " @ " + hw.DisplayHz + "Hz");
                sb.AppendLine("电源: " + (hw.OnAc ? "已插电" : "电池") + " / 计划 " + hw.PowerPlan + " / AC覆盖 " + hw.OverlayName(true));
                sb.AppendLine("VBS: " + (hw.VbsEnabled ? "开启" : "关闭") + " / 内存完整性: " + (hw.MemoryIntegrity ? "开启" : "关闭"));
                sb.AppendLine("HAGS: " + (hw.HagsMode == 2 ? "开启" : (hw.HagsMode == 1 ? "关闭" : "未设置")));
                sb.AppendLine();
                sb.AppendLine("[游戏与 ACE]");
                sb.AppendLine("游戏目录: " + (hw.Game.Found ? hw.Game.InstallPath : "未检测到"));
                sb.AppendLine("游戏主程序: " + (hw.Game.Found ? hw.Game.GameExe : "—"));
                sb.AppendLine("ACE 目录: " + (string.IsNullOrEmpty(hw.Game.AceDir) ? "未检测到" : hw.Game.AceDir));
                sb.AppendLine("游戏运行: " + (hw.Game.Running ? "是 (PID " + hw.Game.RunningPid + ")" : "否"));
                sb.AppendLine("ACE 用户态进程: " + ace.Processes.Count);
                for (int i = 0; i < ace.Processes.Count; i++)
                {
                    AceProcessInfo p = ace.Processes[i];
                    sb.AppendLine(string.Format("  {0} PID={1} 亲和性={2} 状态={3}", p.Name, p.Pid, p.AffinityText, p.Status));
                }
                if (!hw.Game.Found) warnings++;
                if (ace.Processes.Count == 0) sb.AppendLine("  (未运行 ACE；启动游戏后再测试)");
                sb.AppendLine();
                sb.AppendLine("[设置状态]");
                for (int i = 0; i < tweaks.All.Count; i++)
                {
                    Tweak t = tweaks.All[i];
                    TweakState state = t.GetState();
                    sb.AppendLine(string.Format("  {0,-14} {1,-8} {2}", t.Id, StateText(state), t.Title));
                }
                sb.AppendLine();
                sb.AppendLine("[亲和性 API 冒烟测试]");
                string affinityResult;
                bool affinityOk = TestAffinity(topo, out affinityResult);
                if (!affinityOk) warnings++;
                sb.AppendLine(affinityResult);
                sb.AppendLine();
                sb.AppendLine("[只读保证]");
                sb.AppendLine("本次自检只读取系统/游戏信息；没有写入注册表、电源计划或游戏文件。");
                sb.AppendLine("亲和性冒烟测试只会短暂限制本进程自身，并立即恢复。");
                sb.AppendLine();
                sb.AppendLine("RESULT=" + (warnings == 0 ? "PASS" : "PASS_WITH_WARNINGS") + " warnings=" + warnings);

                string dir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                File.WriteAllText(outputPath, sb.ToString(), new UTF8Encoding(true));
                try { Console.WriteLine(sb.ToString()); } catch { }
                return 0;
            }
            catch (Exception ex)
            {
                sb.AppendLine("RESULT=FAIL");
                sb.AppendLine(ex.ToString());
                try
                {
                    string dir = Path.GetDirectoryName(Path.GetFullPath(outputPath));
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    File.WriteAllText(outputPath, sb.ToString(), new UTF8Encoding(true));
                    Console.WriteLine(sb.ToString());
                }
                catch
                {
                }
                return 1;
            }
        }

        private static string StateText(TweakState s)
        {
            if (s == TweakState.Enabled) return "已启用";
            if (s == TweakState.Disabled) return "未启用";
            if (s == TweakState.Custom) return "自定义";
            return "未设置";
        }

        private static bool TestAffinity(CpuTopology topo, out string result)
        {
            ulong target = topo.SingleEcoreMask();
            if (target == 0)
            {
                result = "跳过：没有可用核心。";
                return false;
            }
            try
            {
                IntPtr h = Native.GetCurrentProcess();
                UIntPtr pm, sm;
                if (!Native.GetProcessAffinityMask(h, out pm, out sm))
                {
                    result = "失败：无法读取当前进程亲和性。";
                    return false;
                }
                ulong original = pm.ToUInt64();
                if (!Native.SetProcessAffinityMask(h, new UIntPtr(target)))
                {
                    result = "失败：SetProcessAffinityMask 返回 " + System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                    return false;
                }
                UIntPtr check, checkSys;
                Native.GetProcessAffinityMask(h, out check, out checkSys);
                ulong got = check.ToUInt64();
                Native.SetProcessAffinityMask(h, new UIntPtr(original));
                if (got != target)
                {
                    result = "失败：写入后读回不一致。";
                    return false;
                }
                result = "通过：当前进程可绑定到 " + topo.MaskDescription(target) + "，并已恢复。";
                return true;
            }
            catch (Exception ex)
            {
                result = "失败：" + ex.Message;
                return false;
            }
        }
    }
}
