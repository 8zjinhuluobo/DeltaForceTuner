using System;
using Microsoft.Win32;

namespace DeltaForceTuner
{
    internal enum RegHive
    {
        CurrentUser,
        LocalMachine
    }

    internal class RegValueSpec
    {
        public RegHive Hive;
        public string Key;
        public string Name;
        public RegistryValueKind Kind = RegistryValueKind.DWord;
        public object EnabledValue;
        public object DisabledValue;
        public string DisplayName;

        public RegValueSpec(RegHive hive, string key, string name, RegistryValueKind kind, object enabledValue, object disabledValue)
        {
            Hive = hive;
            Key = key;
            Name = name;
            Kind = kind;
            EnabledValue = enabledValue;
            DisabledValue = disabledValue;
            DisplayName = name;
        }

        public string Id
        {
            get { return (Hive == RegHive.LocalMachine ? "HKLM\\" : "HKCU\\") + Key + "\\" + Name; }
        }
    }

    internal static class RegistryUtil
    {
        public static RegistryKey OpenBase(RegHive hive, bool writable)
        {
            RegistryHive h = hive == RegHive.LocalMachine ? RegistryHive.LocalMachine : RegistryHive.CurrentUser;
            RegistryView view = hive == RegHive.LocalMachine ? RegistryView.Registry64 : RegistryView.Default;
            return RegistryKey.OpenBaseKey(h, view);
        }

        public static object Read(RegHive hive, string key, string name)
        {
            try
            {
                using (RegistryKey b = OpenBase(hive, false))
                using (RegistryKey k = b.OpenSubKey(key, false))
                {
                    if (k == null) return null;
                    return k.GetValue(name, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                }
            }
            catch
            {
                return null;
            }
        }

        public static bool Exists(RegHive hive, string key, string name)
        {
            try
            {
                using (RegistryKey b = OpenBase(hive, false))
                using (RegistryKey k = b.OpenSubKey(key, false))
                {
                    if (k == null) return false;
                    string[] names = k.GetValueNames();
                    for (int i = 0; i < names.Length; i++)
                        if (string.Equals(names[i], name, StringComparison.OrdinalIgnoreCase)) return true;
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        public static string ReadString(RegHive hive, string key, string name, string fallback)
        {
            object o = Read(hive, key, name);
            if (o == null) return fallback;
            return o.ToString();
        }

        public static int ReadDword(RegHive hive, string key, string name, int fallback)
        {
            object o = Read(hive, key, name);
            if (o == null) return fallback;
            try { return Convert.ToInt32(o); }
            catch { return fallback; }
        }

        public static bool ReadBoolDword(RegHive hive, string key, string name)
        {
            return ReadDword(hive, key, name, 0) != 0;
        }

        public static bool Write(RegHive hive, string key, string name, RegistryValueKind kind, object value)
        {
            using (RegistryKey b = OpenBase(hive, true))
            using (RegistryKey k = b.CreateSubKey(key))
            {
                if (k == null) return false;
                k.SetValue(name, value, kind);
            }
            return true;
        }

        public static bool DeleteValue(RegHive hive, string key, string name)
        {
            try
            {
                using (RegistryKey b = OpenBase(hive, true))
                using (RegistryKey k = b.OpenSubKey(key, true))
                {
                    if (k == null) return true;
                    k.DeleteValue(name, false);
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static RegistryValueKind ReadKind(RegHive hive, string key, string name, RegistryValueKind fallback)
        {
            try
            {
                using (RegistryKey b = OpenBase(hive, false))
                using (RegistryKey k = b.OpenSubKey(key, false))
                {
                    if (k == null) return fallback;
                    return k.GetValueKind(name);
                }
            }
            catch
            {
                return fallback;
            }
        }

        public static string ValueToString(object value)
        {
            if (value == null) return "";
            if (value is int) return ((int)value).ToString();
            if (value is long) return ((long)value).ToString();
            if (value is string) return (string)value;
            if (value is string[]) return string.Join("\n", (string[])value);
            return value.ToString();
        }

        public static bool ValuesEqual(object a, object b, RegistryValueKind kind)
        {
            if (a == null && b == null) return true;
            if (a == null || b == null) return false;
            try
            {
                if (kind == RegistryValueKind.DWord || kind == RegistryValueKind.QWord)
                    return Convert.ToInt64(a) == Convert.ToInt64(b);
                return string.Equals(a.ToString(), b.ToString(), StringComparison.Ordinal);
            }
            catch
            {
                return false;
            }
        }
    }
}
