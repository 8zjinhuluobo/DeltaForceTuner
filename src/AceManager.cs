using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Text;

namespace DeltaForceTuner
{
    internal class AceProcessInfo
    {
        public int Pid;
        public long StartTicks;
        public string Name = "";
        public string Path = "";
        public ulong Affinity;
        public ulong OriginalMask;
        public bool Managed;
        public bool Protected;
        public string Status = "";
        public Color StatusColor = Theme.Muted;
        public string AffinityText = "—";
    }

    internal class AceApplyResult
    {
        public bool Ok;
        public int Applied;
        public int Already;
        public int Failed;
        public string Message = "";
    }

    internal class AceManager
    {
        private static readonly string[] KnownNames = new string[]
        {
            "SGuard64", "SGuardSvc64", "ACE-Service64", "ACE-Helper", "SGuardUpdate64", "ACE-Guard Client", "ACE-Tray"
        };

        public CpuTopology Topology;
        public StateStore Store;
        public List<AceProcessInfo> Processes = new List<AceProcessInfo>();
        public string LastMessage = "";
        public DateTime LastCheck = DateTime.MinValue;
        public string Mode = "Group";
        public int CustomCore = -1;
        public bool AutoApply = true;
        public ulong TargetMask;

        public AceManager(CpuTopology topo, StateStore store)
        {
            Topology = topo;
            Store = store;
            Mode = store.AceMode;
            if (Mode != "Single" && Mode != "Custom") Mode = "Group";
            CustomCore = store.AceCore;
            AutoApply = store.AutoApply;
            RebuildTarget();
        }

        public void RebuildTarget()
        {
            if (Mode == "Single")
            {
                TargetMask = Topology.SingleEcoreMask();
            }
            else if (Mode == "Custom")
            {
                CpuCore c = Topology.FindCore(CustomCore);
                if (c != null) TargetMask = c.Mask;
                else TargetMask = Topology.SingleEcoreMask();
            }
            else
            {
                if (Topology.EMask != 0) TargetMask = Topology.EMask;
                else TargetMask = Topology.SingleEcoreMask();
            }
        }

        public void SetMode(string mode, int core)
        {
            Mode = mode;
            if (core >= 0) CustomCore = core;
            Store.AceMode = Mode;
            Store.AceCore = CustomCore;
            Store.Save();
            RebuildTarget();
        }

        public void SetAutoApply(bool value)
        {
            AutoApply = value;
            Store.AutoApply = value;
            Store.Save();
        }

        public string TargetDescription
        {
            get
            {
                if (Mode == "Group")
                {
                    if (Topology.ECores.Count > 0) return "全部 E 核 · " + Topology.ECores.Count + " 核";
                    return Topology.MaskDescription(TargetMask);
                }
                if (Mode == "Custom")
                {
                    CpuCore c = Topology.FindCore(CustomCore);
                    if (c != null) return c.Label + " · CPU " + c.FirstCpu;
                }
                CpuCore e = Topology.CoreForCpu(CpuTopology.FirstBit(TargetMask));
                if (e != null) return e.Label + " · CPU " + e.FirstCpu;
                return Topology.MaskDescription(TargetMask);
            }
        }

        public bool IsAceName(string name)
        {
            for (int i = 0; i < KnownNames.Length; i++)
                if (string.Equals(name, KnownNames[i], StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        public void Refresh()
        {
            Processes.Clear();
            Process[] all = Process.GetProcesses();
            for (int i = 0; i < all.Length; i++)
            {
                Process p = all[i];
                try
                {
                    string name = p.ProcessName;
                    bool known = IsAceName(name);
                    bool looksLikeAce = name.StartsWith("ACE-", StringComparison.OrdinalIgnoreCase) ||
                                        name.StartsWith("SGuard", StringComparison.OrdinalIgnoreCase);
                    if (!known && !looksLikeAce) continue;
                    if (name.IndexOf("Setup", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Uninstall", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.IndexOf("Update", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        name.Equals("closevbs", StringComparison.OrdinalIgnoreCase)) continue;
                    string path = Native.GetProcessPath(p.Id);
                    bool acePath = !string.IsNullOrEmpty(path) && path.IndexOf(@"\AntiCheatExpert\", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (known && !acePath && path != null)
                    {
                        // A same-named process outside ACE is not our target.
                        if (path.IndexOf("AntiCheat", StringComparison.OrdinalIgnoreCase) < 0 &&
                            path.IndexOf("SGuard", StringComparison.OrdinalIgnoreCase) < 0 &&
                            path.IndexOf("ACE-", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    }
                    AceProcessInfo info = new AceProcessInfo();
                    info.Pid = p.Id;
                    info.Name = name;
                    info.Path = path == null ? "" : path;
                    try { info.StartTicks = p.StartTime.Ticks; } catch { info.StartTicks = 0; }
                    ReadAffinity(info);
                    Processes.Add(info);
                }
                catch
                {
                }
                finally
                {
                    p.Dispose();
                }
            }
            Processes.Sort(delegate(AceProcessInfo a, AceProcessInfo b)
            {
                int sa = a.Name.StartsWith("SGuard", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
                int sb = b.Name.StartsWith("SGuard", StringComparison.OrdinalIgnoreCase) ? 0 : 1;
                if (sa != sb) return sa.CompareTo(sb);
                return string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase);
            });
            LastCheck = DateTime.Now;
        }

        private void ReadAffinity(AceProcessInfo info)
        {
            IntPtr h = Native.OpenProcess(Native.PROCESS_QUERY_INFORMATION | Native.PROCESS_SET_INFORMATION, false, info.Pid);
            if (h == IntPtr.Zero)
            {
                info.Protected = true;
                info.Status = "受保护 · 无法读取";
                info.StatusColor = Theme.Amber;
                info.AffinityText = "—";
                return;
            }
            try
            {
                UIntPtr pm, sm;
                if (!Native.GetProcessAffinityMask(h, out pm, out sm))
                {
                    info.Protected = true;
                    info.Status = "受保护 · 无法读取";
                    info.StatusColor = Theme.Amber;
                    info.AffinityText = "—";
                    return;
                }
                info.Affinity = pm.ToUInt64();
                info.AffinityText = Topology.MaskDescription(info.Affinity);
                info.OriginalMask = FindOriginal(info.Pid, info.StartTicks);
                if (info.Affinity == TargetMask && TargetMask != 0)
                {
                    info.Managed = info.OriginalMask != 0;
                    info.Status = "已在目标小核";
                    info.StatusColor = Theme.Green;
                }
                else if (info.OriginalMask != 0)
                {
                    info.Managed = true;
                    info.Status = "亲和性被重置，等待重设";
                    info.StatusColor = Theme.Amber;
                }
                else
                {
                    info.Status = "尚未限制";
                    info.StatusColor = Theme.Muted;
                }
            }
            finally
            {
                Native.CloseHandle(h);
            }
        }

        public AceApplyResult Apply()
        {
            RebuildTarget();
            AceApplyResult result = new AceApplyResult();
            if (!Native.IsAdministrator())
            {
                result.Message = "需要管理员权限。";
                return result;
            }
            if (TargetMask == 0)
            {
                result.Message = "无法确定目标核心。";
                return result;
            }
            Refresh();
            int applied = 0;
            int already = 0;
            int failed = 0;
            for (int i = 0; i < Processes.Count; i++)
            {
                AceProcessInfo info = Processes[i];
                if (info.Protected)
                {
                    failed++;
                    continue;
                }
                if (info.Affinity == TargetMask)
                {
                    already++;
                    info.Status = "已在目标小核";
                    info.StatusColor = Theme.Green;
                    continue;
                }
                string err;
                if (ApplyOne(info, out err))
                {
                    applied++;
                    info.Status = "已限制到小核";
                    info.StatusColor = Theme.Green;
                }
                else
                {
                    failed++;
                    info.Status = err;
                    info.StatusColor = Theme.Red;
                }
            }
            result.Applied = applied;
            result.Already = already;
            result.Failed = failed;
            result.Ok = failed == 0;
            if (Processes.Count == 0)
            {
                result.Ok = true;
                result.Message = "尚未检测到 ACE 进程。请先启动游戏，工具会自动等待并应用。";
            }
            else if (failed > 0)
            {
                result.Message = string.Format("已限制 {0} 个，{1} 个已在目标核，{2} 个失败。", applied, already, failed);
            }
            else
            {
                result.Message = string.Format("ACE 已限制到小核：{0} 个进程。", applied + already);
            }
            LastMessage = result.Message;
            SaveOriginals();
            return result;
        }

        private bool ApplyOne(AceProcessInfo info, out string error)
        {
            error = "";
            IntPtr h = Native.OpenProcess(Native.PROCESS_QUERY_INFORMATION | Native.PROCESS_SET_INFORMATION, false, info.Pid);
            if (h == IntPtr.Zero)
            {
                int err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                error = err == 5 ? "ACE 保护拒绝访问" : "无法打开进程 (" + err + ")";
                info.Protected = true;
                return false;
            }
            try
            {
                UIntPtr pm, sm;
                if (!Native.GetProcessAffinityMask(h, out pm, out sm))
                {
                    error = "无法读取亲和性";
                    info.Protected = true;
                    return false;
                }
                ulong current = pm.ToUInt64();
                ulong system = sm.ToUInt64();
                if ((TargetMask & system) != TargetMask)
                {
                    error = "目标核心不在系统掩码内";
                    return false;
                }
                if (current != TargetMask)
                {
                    ulong original = FindOriginal(info.Pid, info.StartTicks);
                    if (original == 0)
                    {
                        original = current;
                        AddOriginal(info, original);
                    }
                    info.OriginalMask = original;
                    if (!Native.SetProcessAffinityMask(h, new UIntPtr(TargetMask)))
                    {
                        int err = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
                        error = err == 5 ? "ACE 保护拒绝修改" : "设置失败 (" + err + ")";
                        return false;
                    }
                    UIntPtr check, checkSys;
                    if (Native.GetProcessAffinityMask(h, out check, out checkSys))
                        info.Affinity = check.ToUInt64();
                    else
                        info.Affinity = TargetMask;
                    info.AffinityText = Topology.MaskDescription(info.Affinity);
                    if (info.Affinity != TargetMask)
                    {
                        error = "设置未生效";
                        return false;
                    }
                }
                info.Managed = true;
                return true;
            }
            finally
            {
                Native.CloseHandle(h);
            }
        }

        private ulong FindOriginal(int pid, long start)
        {
            for (int i = 0; i < Store.AceOriginals.Count; i++)
            {
                AceOriginalRecord r = Store.AceOriginals[i];
                if (r.Pid == pid && (start == 0 || r.StartTicks == start)) return r.Mask;
            }
            return 0;
        }

        private void AddOriginal(AceProcessInfo info, ulong mask)
        {
            if (mask == 0) return;
            AceOriginalRecord r = new AceOriginalRecord();
            r.Pid = info.Pid;
            r.StartTicks = info.StartTicks;
            r.Mask = mask;
            Store.AceOriginals.Add(r);
        }

        private void SaveOriginals()
        {
            Store.Save();
        }

        public AceApplyResult RestoreAll()
        {
            AceApplyResult result = new AceApplyResult();
            Refresh();
            int restored = 0;
            int failed = 0;
            List<int> restoredPids = new List<int>();
            List<int> currentPids = new List<int>();
            for (int i = 0; i < Processes.Count; i++) currentPids.Add(Processes[i].Pid);
            for (int i = 0; i < Processes.Count; i++)
            {
                AceProcessInfo info = Processes[i];
                if (info.Protected) continue;
                ulong original = FindOriginal(info.Pid, info.StartTicks);
                if (original == 0) continue;
                IntPtr h = Native.OpenProcess(Native.PROCESS_QUERY_INFORMATION | Native.PROCESS_SET_INFORMATION, false, info.Pid);
                if (h == IntPtr.Zero)
                {
                    failed++;
                    continue;
                }
                try
                {
                    if (Native.SetProcessAffinityMask(h, new UIntPtr(original)))
                    {
                        restored++;
                        restoredPids.Add(info.Pid);
                        info.Managed = false;
                    }
                    else
                    {
                        failed++;
                    }
                }
                finally
                {
                    Native.CloseHandle(h);
                }
            }
            for (int i = Store.AceOriginals.Count - 1; i >= 0; i--)
            {
                AceOriginalRecord r = Store.AceOriginals[i];
                if (restoredPids.Contains(r.Pid) || !currentPids.Contains(r.Pid))
                    Store.AceOriginals.RemoveAt(i);
            }
            Store.Save();
            result.Applied = restored;
            result.Failed = failed;
            result.Ok = failed == 0;
            result.Message = string.Format("已恢复 {0} 个 ACE 进程的原始亲和性。", restored);
            if (failed > 0) result.Message += string.Format(" {0} 个进程恢复失败（可能已退出或被保护）。", failed);
            LastMessage = result.Message;
            return result;
        }

        public void ApplyIfAuto()
        {
            if (!AutoApply) return;
            if (TargetMask == 0) return;
            if (!Native.IsAdministrator())
            {
                LastMessage = "需要管理员权限才能保持 ACE 小核隔离。";
                return;
            }
            // Only do work when ACE is actually present.
            Refresh();
            bool has = false;
            for (int i = 0; i < Processes.Count; i++)
            {
                if (!Processes[i].Protected && Processes[i].Affinity != TargetMask)
                {
                    has = true;
                    break;
                }
            }
            if (!has) return;
            AceApplyResult r = Apply();
            LastMessage = r.Message;
        }

        public bool HasManaged
        {
            get
            {
                for (int i = 0; i < Processes.Count; i++)
                    if (Processes[i].Managed) return true;
                return false;
            }
        }

        public string CleanPath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "路径不可见（受保护）";
            try
            {
                if (path.Length > 80) return "…" + path.Substring(path.Length - 77);
            }
            catch
            {
            }
            return path;
        }
    }
}
