using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace DeltaForceTuner
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            try
            {
                string selfTest = GetArg(args, "--selftest");
                if (selfTest != null)
                {
                    if (selfTest.Length == 0)
                        selfTest = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "selftest.txt");
                    return SelfTest.Run(selfTest);
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
                Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
                {
                    Log.Error("界面线程异常", e.Exception);
                    MessageBox.Show("程序遇到一个错误，但已阻止它影响系统设置。\n\n" + e.Exception.Message,
                        "三角洲行动 · 本机调优助手", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                };
                AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
                {
                    Log.Error("未处理异常", e.ExceptionObject as Exception);
                };

                string render = GetArg(args, "--render");
                string page = GetArg(args, "--page");
                if (string.IsNullOrEmpty(page)) page = "overview";
                string action = "";
                string apply = GetArg(args, "--apply");
                string disable = GetArg(args, "--disable");
                if (!string.IsNullOrEmpty(apply)) action = "apply:" + apply;
                else if (!string.IsNullOrEmpty(disable)) action = "disable:" + disable;
                if (GetArg(args, "--ace-apply") != null) action = "ace-apply";
                if (GetArg(args, "--ace-restore") != null) action = "ace-restore";

                if (render != null)
                {
                    int h = 820;
                    string hs = GetArg(args, "--height");
                    if (!string.IsNullOrEmpty(hs)) int.TryParse(hs, out h);
                    if (h < 600) h = 600;
                    return Render(page, render, h);
                }

                Log.Info("启动，管理员=" + Elevation.IsAdmin);
                using (MainForm form = new MainForm(page, action))
                {
                    Application.Run(form);
                }
                return 0;
            }
            catch (Exception ex)
            {
                Log.Error("启动失败", ex);
                try { Console.WriteLine(ex.ToString()); } catch { }
                try
                {
                    MessageBox.Show("启动失败：\n" + ex.Message, "三角洲行动 · 本机调优助手", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                catch
                {
                }
                return 1;
            }
        }

        private static int Render(string page, string path, int height)
        {
            try
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                using (MainForm form = new MainForm(page, ""))
                {
                    form.ClientSize = new Size(1200, height);
                    form.StartPosition = FormStartPosition.Manual;
                    form.Location = new Point(0, 0);
                    form.ShowInTaskbar = false;
                    form.Show();
                    Application.DoEvents();
                    form.ClientSize = new Size(1200, height);
                    Application.DoEvents();
                    Thread.Sleep(350);
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
                Log.Error("渲染失败", ex);
                try { Console.WriteLine("RENDER ERROR: " + ex.ToString()); } catch { }
                return 1;
            }
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
