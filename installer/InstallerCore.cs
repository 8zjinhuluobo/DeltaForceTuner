using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;

namespace DeltaForceTuner.Setup
{
    internal static class InstallInfo
    {
        public const string ProductName = "三角洲行动 · 本机调优助手";
        public const string Version = "1.0.1";
        public const string Publisher = "Local";
        public const string AppExeName = "DeltaForceTuner.exe";
        public const string UninstallerName = "Uninstall.exe";
        public const string UninstallKey = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\DeltaForceTuner";

        public static string DefaultDir
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    @"Programs\DeltaForceTuner");
            }
        }

        public static string ConfigDir
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "DeltaForceTuner");
            }
        }

        public static string StartMenuDir
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    @"Microsoft\Windows\Start Menu\Programs\DeltaForceTuner");
            }
        }

        public static string StartMenuLink
        {
            get { return Path.Combine(StartMenuDir, "三角洲行动调优助手.lnk"); }
        }

        public static string DesktopLink
        {
            get
            {
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    "三角洲行动调优助手.lnk");
            }
        }

        public static string AppExe(string dir)
        {
            return Path.Combine(dir, AppExeName);
        }

        public static string Uninstaller(string dir)
        {
            return Path.Combine(dir, UninstallerName);
        }

        public static string NormalizeDir(string value)
        {
            if (string.IsNullOrEmpty(value)) throw new ArgumentException("安装目录不能为空。");
            string dir = Path.GetFullPath(value.Trim().Trim('"'));
            string root = Path.GetPathRoot(dir);
            if (dir.Length < 3) throw new ArgumentException("请不要安装到磁盘根目录。");
            if (string.Equals(dir.TrimEnd('\\'), root.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("请不要安装到磁盘根目录。");
            string win = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (dir.StartsWith(win, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("请不要安装到 Windows 系统目录。");
            if (File.Exists(dir)) throw new ArgumentException("目标路径是一个文件，请选择文件夹。");
            return dir.TrimEnd('\\');
        }

        public static string FindInstalledDir()
        {
            try
            {
                using (RegistryKey k = Registry.CurrentUser.OpenSubKey(UninstallKey, false))
                {
                    if (k != null)
                    {
                        string d = Convert.ToString(k.GetValue("InstallLocation", ""));
                        if (!string.IsNullOrEmpty(d) && Directory.Exists(d)) return d;
                    }
                }
            }
            catch
            {
            }
            return null;
        }
    }

    internal class SetupOptions
    {
        public string Dir = InstallInfo.DefaultDir;
        public bool DesktopShortcut = true;
        public bool CreateShortcuts = true;
        public bool WriteRegistry = true;
        public bool Launch = true;
        public bool ForceClose = false;
    }

    internal class InstallResult
    {
        public bool Ok;
        public string Message = "";
        public List<string> Warnings = new List<string>();
        public string Dir = "";
    }

    internal class UninstallOptions
    {
        public string Dir = "";
        public bool DeleteSettings;
        public bool WriteRegistry = true;
        public bool ForceClose = false;
        public bool SelfDelete = true;
    }

    internal class UninstallResult
    {
        public bool Ok;
        public string Message = "";
        public List<string> Warnings = new List<string>();
    }

    internal static class SetupLog
    {
        public static string Path
        {
            get { return System.IO.Path.Combine(System.IO.Path.GetTempPath(), "DeltaForceTunerSetup.log"); }
        }

        public static void Write(string message)
        {
            try
            {
                File.AppendAllText(Path, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "  " + message + Environment.NewLine, new UTF8Encoding(false));
            }
            catch
            {
            }
        }
    }

    internal static class InstallEngine
    {
        public static bool IsAppRunning()
        {
            try
            {
                return Process.GetProcessesByName("DeltaForceTuner").Length > 0;
            }
            catch
            {
                return false;
            }
        }

        public static bool StopApp(bool force)
        {
            try
            {
                Process[] ps = Process.GetProcessesByName("DeltaForceTuner");
                if (ps.Length == 0) return true;
                for (int i = 0; i < ps.Length; i++)
                {
                    try { ps[i].CloseMainWindow(); } catch { }
                }
                DateTime end = DateTime.Now.AddSeconds(3);
                while (DateTime.Now < end)
                {
                    bool alive = false;
                    for (int i = 0; i < ps.Length; i++)
                    {
                        try { if (!ps[i].HasExited) alive = true; } catch { }
                    }
                    if (!alive) break;
                    System.Threading.Thread.Sleep(150);
                }
                for (int i = 0; i < ps.Length; i++)
                {
                    try
                    {
                        if (!ps[i].HasExited)
                        {
                            if (!force) return false;
                            ps[i].Kill();
                        }
                    }
                    catch { }
                    try { ps[i].Dispose(); } catch { }
                }
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static InstallResult Install(SetupOptions o, Action<int, string> progress)
        {
            InstallResult r = new InstallResult();
            try
            {
                r.Dir = InstallInfo.NormalizeDir(o.Dir);
                if (IsAppRunning())
                {
                    if (!o.ForceClose || !StopApp(true))
                    {
                        r.Message = "程序正在运行，请先退出后再安装。";
                        return r;
                    }
                }
                if (progress != null) progress(8, "正在创建安装目录…");
                Directory.CreateDirectory(r.Dir);

                if (progress != null) progress(20, "正在释放主程序…");
                Extract("payload/app.exe", InstallInfo.AppExe(r.Dir));
                if (progress != null) progress(38, "正在释放卸载程序…");
                Extract("payload/uninstall.exe", InstallInfo.Uninstaller(r.Dir));
                if (progress != null) progress(48, "正在释放说明文件…");
                ExtractOptional("payload/readme.txt", Path.Combine(r.Dir, "使用说明.txt"));
                ExtractOptional("payload/README.md", Path.Combine(r.Dir, "README.md"));
                ExtractOptional("payload/preview.png", Path.Combine(r.Dir, "preview.png"));
                ExtractOptional("payload/sha256.txt", Path.Combine(r.Dir, "SHA256.txt"));

                if (o.CreateShortcuts)
                {
                    if (progress != null) progress(62, "正在创建开始菜单快捷方式…");
                    try
                    {
                        Directory.CreateDirectory(InstallInfo.StartMenuDir);
                        ShortcutUtil.Create(InstallInfo.StartMenuLink, InstallInfo.AppExe(r.Dir), "", r.Dir,
                            InstallInfo.AppExe(r.Dir), InstallInfo.ProductName);
                    }
                    catch (Exception ex)
                    {
                        r.Warnings.Add("开始菜单快捷方式创建失败：" + ex.Message);
                    }
                    if (o.DesktopShortcut)
                    {
                        try
                        {
                            ShortcutUtil.Create(InstallInfo.DesktopLink, InstallInfo.AppExe(r.Dir), "", r.Dir,
                                InstallInfo.AppExe(r.Dir), InstallInfo.ProductName);
                        }
                        catch (Exception ex)
                        {
                            r.Warnings.Add("桌面快捷方式创建失败：" + ex.Message);
                        }
                    }
                }

                if (o.WriteRegistry)
                {
                    if (progress != null) progress(78, "正在写入卸载信息…");
                    try
                    {
                        WriteUninstallEntry(r.Dir);
                    }
                    catch (Exception ex)
                    {
                        r.Warnings.Add("卸载信息写入失败（不影响使用，可手动运行安装目录里的 Uninstall.exe）：" + ex.Message);
                    }
                }

                if (progress != null) progress(92, "正在完成…");
                if (o.Launch)
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(InstallInfo.AppExe(r.Dir)) { WorkingDirectory = r.Dir, UseShellExecute = true });
                    }
                    catch (Exception ex)
                    {
                        r.Warnings.Add("启动程序失败：" + ex.Message);
                    }
                }
                if (progress != null) progress(100, "安装完成");
                r.Ok = true;
                r.Message = "安装完成";
                SetupLog.Write("install ok " + r.Dir + (r.Warnings.Count > 0 ? " warnings=" + string.Join(" | ", r.Warnings.ToArray()) : ""));
                return r;
            }
            catch (Exception ex)
            {
                r.Message = ex.Message;
                SetupLog.Write("install failed: " + ex);
                return r;
            }
        }

        private static void Extract(string name, string dest)
        {
            using (Stream s = Assembly.GetExecutingAssembly().GetManifestResourceStream(name))
            {
                if (s == null) throw new InvalidOperationException("安装包缺少资源：" + name);
                string dir = Path.GetDirectoryName(dest);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                using (FileStream fs = new FileStream(dest, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    s.CopyTo(fs);
                }
            }
        }

        private static void ExtractOptional(string name, string dest)
        {
            try
            {
                Extract(name, dest);
            }
            catch
            {
            }
        }

        private static void WriteUninstallEntry(string dir)
        {
            long size = 0;
            try
            {
                string[] files = Directory.GetFiles(dir);
                for (int i = 0; i < files.Length; i++)
                {
                    try { size += new FileInfo(files[i]).Length; } catch { }
                }
            }
            catch
            {
            }
            using (RegistryKey k = Registry.CurrentUser.CreateSubKey(InstallInfo.UninstallKey))
            {
                if (k == null) throw new InvalidOperationException("无法创建卸载信息注册表项。");
                k.SetValue("DisplayName", InstallInfo.ProductName, RegistryValueKind.String);
                k.SetValue("DisplayVersion", InstallInfo.Version, RegistryValueKind.String);
                k.SetValue("Publisher", InstallInfo.Publisher, RegistryValueKind.String);
                k.SetValue("InstallLocation", dir, RegistryValueKind.String);
                k.SetValue("DisplayIcon", InstallInfo.AppExe(dir) + ",0", RegistryValueKind.String);
                k.SetValue("UninstallString", "\"" + InstallInfo.Uninstaller(dir) + "\" --uninstall", RegistryValueKind.String);
                k.SetValue("QuietUninstallString", "\"" + InstallInfo.Uninstaller(dir) + "\" --uninstall --quiet", RegistryValueKind.String);
                k.SetValue("EstimatedSize", (int)Math.Max(1, size / 1024), RegistryValueKind.DWord);
                k.SetValue("InstallDate", DateTime.Now.ToString("yyyyMMdd"), RegistryValueKind.String);
                k.SetValue("NoModify", 1, RegistryValueKind.DWord);
                k.SetValue("NoRepair", 1, RegistryValueKind.DWord);
            }
        }

        public static bool Launch(string dir)
        {
            try
            {
                Process.Start(new ProcessStartInfo(InstallInfo.AppExe(dir)) { WorkingDirectory = dir, UseShellExecute = true });
                return true;
            }
            catch
            {
                return false;
            }
        }
    }

    internal static class UninstallEngine
    {
        public static UninstallResult Uninstall(UninstallOptions o)
        {
            UninstallResult r = new UninstallResult();
            try
            {
                string dir = o.Dir;
                if (string.IsNullOrEmpty(dir)) dir = InstallInfo.FindInstalledDir();
                if (string.IsNullOrEmpty(dir)) dir = Path.GetDirectoryName(ApplicationExecutablePath());
                if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir))
                {
                    r.Message = "没有找到安装目录。";
                    return r;
                }
                dir = InstallInfo.NormalizeDir(dir);
                if (InstallEngine.IsAppRunning())
                {
                    if (!o.ForceClose || !InstallEngine.StopApp(true))
                    {
                        r.Message = "程序正在运行，请先退出后再卸载。";
                        return r;
                    }
                }

                DeleteShortcuts();
                if (o.WriteRegistry)
                {
                    try
                    {
                        Registry.CurrentUser.DeleteSubKeyTree(InstallInfo.UninstallKey, false);
                    }
                    catch (Exception ex)
                    {
                        r.Warnings.Add("删除卸载信息失败：" + ex.Message);
                    }
                }
                if (o.DeleteSettings)
                {
                    try
                    {
                        if (Directory.Exists(InstallInfo.ConfigDir)) Directory.Delete(InstallInfo.ConfigDir, true);
                    }
                    catch (Exception ex)
                    {
                        r.Warnings.Add("删除设置备份失败：" + ex.Message);
                    }
                }

                string uninstaller = InstallInfo.Uninstaller(dir);
                string[] files = Directory.GetFiles(dir);
                for (int i = 0; i < files.Length; i++)
                {
                    if (string.Equals(files[i], uninstaller, StringComparison.OrdinalIgnoreCase)) continue;
                    try { File.Delete(files[i]); } catch { }
                }
                if (o.SelfDelete)
                    ScheduleSelfDelete(dir, uninstaller);
                r.Ok = true;
                r.Message = "卸载完成。";
                SetupLog.Write("uninstall ok " + dir + (r.Warnings.Count > 0 ? " warnings=" + string.Join(" | ", r.Warnings.ToArray()) : ""));
                return r;
            }
            catch (Exception ex)
            {
                r.Message = ex.Message;
                SetupLog.Write("uninstall failed: " + ex);
                return r;
            }
        }

        private static void DeleteShortcuts()
        {
            try { if (File.Exists(InstallInfo.StartMenuLink)) File.Delete(InstallInfo.StartMenuLink); } catch { }
            try { if (File.Exists(InstallInfo.DesktopLink)) File.Delete(InstallInfo.DesktopLink); } catch { }
            try
            {
                if (Directory.Exists(InstallInfo.StartMenuDir) && Directory.GetFileSystemEntries(InstallInfo.StartMenuDir).Length == 0)
                    Directory.Delete(InstallInfo.StartMenuDir);
            }
            catch { }
        }

        private static void ScheduleSelfDelete(string dir, string uninstaller)
        {
            try
            {
                string args = "/c ping 127.0.0.1 -n 3 >nul & del /f /q \"" + uninstaller + "\" & rmdir /s /q \"" + dir + "\"";
                ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", args);
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.WorkingDirectory = Path.GetTempPath();
                Process.Start(psi);
            }
            catch (Exception ex)
            {
                SetupLog.Write("self-delete schedule failed: " + ex.Message);
            }
        }

        private static string ApplicationExecutablePath()
        {
            try { return System.Windows.Forms.Application.ExecutablePath; }
            catch { return ""; }
        }
    }

    internal static class ShortcutUtil
    {
        public static void Create(string linkPath, string target, string args, string workingDir, string iconPath, string description)
        {
            Type shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) throw new InvalidOperationException("WScript.Shell 不可用。");
            object shell = null;
            object shortcut = null;
            try
            {
                shell = Activator.CreateInstance(shellType);
                shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { linkPath });
                Type st = shortcut.GetType();
                st.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { target });
                st.InvokeMember("Arguments", BindingFlags.SetProperty, null, shortcut, new object[] { args == null ? "" : args });
                st.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDir });
                st.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { iconPath + ",0" });
                st.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { description });
                st.InvokeMember("WindowStyle", BindingFlags.SetProperty, null, shortcut, new object[] { 1 });
                st.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
            }
            finally
            {
                if (shortcut != null) Marshal.ReleaseComObject(shortcut);
                if (shell != null) Marshal.ReleaseComObject(shell);
            }
        }
    }
}
