using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

namespace DeltaForceTuner
{
    internal class CpuCore
    {
        public int Index;
        public int EfficiencyClass;
        public int Group;
        public ulong Mask;
        public int FirstCpu;
        public int LogicalCount;
        public bool IsEfficiency;
        public string Label = "";
        public string Kind = "";
    }

    internal class CpuTopology
    {
        public List<CpuCore> Cores = new List<CpuCore>();
        public List<CpuCore> PCores = new List<CpuCore>();
        public List<CpuCore> ECores = new List<CpuCore>();
        public ulong AllMask;
        public ulong PMask;
        public ulong EMask;
        public bool IsHybrid;
        public bool MultiGroup;
        public int GroupCount;
        public string Warning = "";

        public static CpuTopology Detect()
        {
            CpuTopology t = new CpuTopology();
            t.GroupCount = Native.GetActiveProcessorGroupCount();
            if (t.GroupCount < 1) t.GroupCount = 1;
            t.MultiGroup = t.GroupCount > 1;

            uint len = 0;
            Native.GetLogicalProcessorInformationEx(0, IntPtr.Zero, ref len);
            if (len == 0) return t;
            IntPtr buffer = Marshal.AllocHGlobal((int)len);
            try
            {
                if (!Native.GetLogicalProcessorInformationEx(0, buffer, ref len)) return t;
                int offset = 0;
                List<CpuCore> all = new List<CpuCore>();
                while (offset < len)
                {
                    int rel = Marshal.ReadInt32(buffer, offset);
                    int size = Marshal.ReadInt32(buffer, offset + 4);
                    if (size <= 0) break;
                    if (rel == 0)
                    {
                        int eff = Marshal.ReadByte(buffer, offset + 9);
                        int groups = Marshal.ReadInt16(buffer, offset + 30);
                        for (int g = 0; g < groups; g++)
                        {
                            int baseOff = offset + 32 + g * 16;
                            ulong mask = (ulong)Marshal.ReadInt64(buffer, baseOff);
                            int group = Marshal.ReadInt16(buffer, baseOff + 8);
                            if (mask == 0) continue;
                            CpuCore c = new CpuCore();
                            c.EfficiencyClass = eff;
                            c.Group = group;
                            c.Mask = mask;
                            c.FirstCpu = FirstBit(mask);
                            c.LogicalCount = PopCount(mask);
                            all.Add(c);
                        }
                    }
                    offset += size;
                }

                all.Sort(delegate(CpuCore a, CpuCore b)
                {
                    if (a.Group != b.Group) return a.Group.CompareTo(b.Group);
                    return a.FirstCpu.CompareTo(b.FirstCpu);
                });

                // Windows process affinity masks are group-relative. Consumer CPUs
                // have one group; on larger systems we keep group 0 only and warn.
                int useGroup = 0;
                for (int i = 0; i < all.Count; i++)
                {
                    if (all[i].Group == useGroup) t.Cores.Add(all[i]);
                }
                if (t.Cores.Count == 0 && all.Count > 0)
                {
                    useGroup = all[0].Group;
                    for (int i = 0; i < all.Count; i++)
                        if (all[i].Group == useGroup) t.Cores.Add(all[i]);
                }

                int maxEff = -1;
                for (int i = 0; i < t.Cores.Count; i++)
                    if (t.Cores[i].EfficiencyClass > maxEff) maxEff = t.Cores[i].EfficiencyClass;
                t.IsHybrid = false;
                for (int i = 0; i < t.Cores.Count; i++)
                {
                    CpuCore c = t.Cores[i];
                    c.IsEfficiency = c.EfficiencyClass < maxEff;
                    if (c.IsEfficiency) t.IsHybrid = true;
                }

                int p = 0, e = 0;
                for (int i = 0; i < t.Cores.Count; i++)
                {
                    CpuCore c = t.Cores[i];
                    c.Index = i;
                    if (c.IsEfficiency)
                    {
                        e++;
                        c.Label = "E" + e;
                        c.Kind = "能效核";
                        t.ECores.Add(c);
                        t.EMask |= c.Mask;
                    }
                    else
                    {
                        p++;
                        c.Label = "P" + p;
                        c.Kind = "性能核";
                        t.PCores.Add(c);
                        t.PMask |= c.Mask;
                    }
                    t.AllMask |= c.Mask;
                }
                if (!t.IsHybrid)
                {
                    for (int i = 0; i < t.Cores.Count; i++)
                    {
                        t.Cores[i].IsEfficiency = false;
                        t.Cores[i].Label = "C" + (i + 1);
                        t.Cores[i].Kind = "物理核心";
                    }
                }
                if (t.MultiGroup)
                    t.Warning = "检测到 " + t.GroupCount + " 个处理器组；本工具按第 1 组设置亲和性。";
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
            return t;
        }

        public CpuCore FindCore(int index)
        {
            for (int i = 0; i < Cores.Count; i++)
                if (Cores[i].Index == index) return Cores[i];
            return null;
        }

        public CpuCore CoreForCpu(int cpu)
        {
            for (int i = 0; i < Cores.Count; i++)
            {
                if ((Cores[i].Mask & (1UL << cpu)) != 0) return Cores[i];
            }
            return null;
        }

        public int ThreadCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Cores.Count; i++) n += Cores[i].LogicalCount;
                return n;
            }
        }

        public ulong SingleEcoreMask()
        {
            if (ECores.Count > 0) return ECores[ECores.Count - 1].Mask;
            if (Cores.Count > 0) return Cores[Cores.Count - 1].Mask;
            return 0;
        }

        public int SingleEcoreIndex()
        {
            if (ECores.Count > 0) return ECores[ECores.Count - 1].Index;
            if (Cores.Count > 0) return Cores[Cores.Count - 1].Index;
            return -1;
        }

        public string MaskDescription(ulong mask)
        {
            if (mask == 0) return "未设置";
            if (mask == AllMask) return "全部 " + ThreadCount + " 线程";
            if (mask == EMask && EMask != 0) return "全部 E 核";
            if (mask == PMask && PMask != 0) return "全部 P 核";
            List<int> cpus = new List<int>();
            for (int i = 0; i < 64; i++)
                if ((mask & (1UL << i)) != 0) cpus.Add(i);
            if (cpus.Count == 1) return "CPU " + cpus[0];
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < cpus.Count && i < 8; i++)
            {
                if (i > 0) sb.Append(",");
                sb.Append(cpus[i]);
            }
            if (cpus.Count > 8) sb.Append("…");
            return sb.ToString();
        }

        public static int PopCount(ulong v)
        {
            int n = 0;
            while (v != 0) { v &= (v - 1); n++; }
            return n;
        }

        public static int FirstBit(ulong v)
        {
            for (int i = 0; i < 64; i++)
                if ((v & (1UL << i)) != 0) return i;
            return -1;
        }
    }

    internal class CpuLoadSampler
    {
        private ulong _lastIdle;
        private ulong _lastTotal;
        private bool _hasLast;

        public int Sample()
        {
            Native.FILETIME idle, kernel, user;
            if (!Native.GetSystemTimes(out idle, out kernel, out user)) return 0;
            ulong i = Native.FileTimeToUInt64(idle);
            ulong k = Native.FileTimeToUInt64(kernel);
            ulong u = Native.FileTimeToUInt64(user);
            ulong total = k + u;
            if (!_hasLast || total <= _lastTotal)
            {
                _lastIdle = i;
                _lastTotal = total;
                _hasLast = true;
                return 0;
            }
            ulong di = i - _lastIdle;
            ulong dt = total - _lastTotal;
            _lastIdle = i;
            _lastTotal = total;
            if (dt == 0) return 0;
            double busy = 1.0 - ((double)di / (double)dt);
            if (busy < 0) busy = 0;
            if (busy > 1) busy = 1;
            return (int)Math.Round(busy * 100);
        }
    }
}
