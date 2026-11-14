using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace DeltaForceTuner.Setup
{
    internal static class InstallerProgram
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
#if UNINSTALL
                string render = GetArg(args, "--render");
                if (render != null)
                {
                    return RenderUninstall(render);
                }
                if (Has(args, "--quiet"))
                {
                    return SilentUninstall(args);
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new UninstallForm());
                return 0;
#else
                string renderSetup = GetArg(args, "--render");
                if (renderSetup != null)
                {
                    return RenderSetup(renderSetup);
                }
                if (Has(args, "--install"))
                {
                    return SilentInstall(args);
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new SetupForm());
                return 0;
#endif
            }
            catch (Exception ex)
            {
                SetupLog.Write("fatal: " + ex);
                try
                {
                    MessageBox.Show("安装程序遇到错误：\n" + ex.Message, "三角洲行动 · 安装程序",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch { }
                return 1;
            }
        }

#if !UNINSTALL
        private static int SilentInstall(string[] args)
        {
            try
            {
                SetupOptions o = new SetupOptions();
                string dir = GetArg(args, "--dir");
                if (!string.IsNullOrEmpty(dir)) o.Dir = dir;
                o.CreateShortcuts = !Has(args, "--no-shortcuts");
                o.DesktopShortcut = !Has(args, "--no-desktop");
                o.WriteRegistry = !Has(args, "--no-registry");
                o.Launch = !Has(args, "--no-launch");
                o.ForceClose = Has(args, "--force-close");
                InstallResult r = InstallEngine.Install(o, null);
                Console.WriteLine((r.Ok ? "OK " : "FAIL ") + r.Message);
                Console.WriteLine("DIR " + r.Dir);
                for (int i = 0; i < r.Warnings.Count; i++) Console.WriteLine("WARN " + r.Warnings[i]);
                return r.Ok ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL " + ex.Message);
                SetupLog.Write("silent install failed: " + ex);
                return 1;
            }
        }

        private static int RenderSetup(string path)
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (SetupForm form = new SetupForm())
                {
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new Point(0, 0);
                    form.ShowInTaskbar = false;
                    form.Show();
                    Application.DoEvents();
                    Thread.Sleep(300);
                    Application.DoEvents();
                    using (Bitmap bmp = new Bitmap(form.Width, form.Height))
                    {
                        form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
                        bmp.Save(path, ImageFormat.Png);
                    }
                    form.Close();
                }
                return 0;
            }
            catch (Exception ex)
            {
                SetupLog.Write("render setup failed: " + ex);
                return 1;
            }
        }
#endif

#if UNINSTALL
        private static int SilentUninstall(string[] args)
        {
            try
            {
                UninstallOptions o = new UninstallOptions();
                o.Dir = GetArg(args, "--dir");
                o.DeleteSettings = Has(args, "--delete-settings");
                o.WriteRegistry = !Has(args, "--no-registry");
                o.ForceClose = Has(args, "--force-close");
                o.SelfDelete = !Has(args, "--no-self-delete");
                UninstallResult r = UninstallEngine.Uninstall(o);
                Console.WriteLine((r.Ok ? "OK " : "FAIL ") + r.Message);
                for (int i = 0; i < r.Warnings.Count; i++) Console.WriteLine("WARN " + r.Warnings[i]);
                return r.Ok ? 0 : 1;
            }
            catch (Exception ex)
            {
                Console.WriteLine("FAIL " + ex.Message);
                SetupLog.Write("silent uninstall failed: " + ex);
                return 1;
            }
        }

        private static int RenderUninstall(string path)
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (UninstallForm form = new UninstallForm())
                {
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new Point(0, 0);
                    form.ShowInTaskbar = false;
                    form.Show();
                    Application.DoEvents();
                    Thread.Sleep(300);
                    Application.DoEvents();
                    using (Bitmap bmp = new Bitmap(form.Width, form.Height))
                    {
                        form.DrawToBitmap(bmp, new Rectangle(0, 0, form.Width, form.Height));
                        bmp.Save(path, ImageFormat.Png);
                    }
                    form.Close();
                }
                return 0;
            }
            catch (Exception ex)
            {
                SetupLog.Write("render uninstall failed: " + ex);
                return 1;
            }
        }
#endif

        private static bool Has(string[] args, string name)
        {
            for (int i = 0; i < args.Length; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        private static string GetArg(string[] args, string name)
        {
            for (int i = 0; i < args.Length; i++)
            {
                string a = args[i];
                if (string.Equals(a, name, StringComparison.OrdinalIgnoreCase)) return "";
                if (a.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase))
                    return a.Substring(name.Length + 1);
            }
            return null;
        }
    }
}
