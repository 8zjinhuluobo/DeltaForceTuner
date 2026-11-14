using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows.Forms;

namespace DeltaForceTuner
{
    internal static class Elevation
    {
        public static bool IsAdmin
        {
            get { return Native.IsAdministrator(); }
        }

        public static bool Relaunch(string arguments)
        {
            try
            {
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = Application.ExecutablePath;
                psi.Arguments = arguments;
                psi.UseShellExecute = true;
                psi.Verb = "runas";
                psi.WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory;
                Process.Start(psi);
                return true;
            }
            catch (Win32Exception)
            {
                return false;
            }
            catch (Exception ex)
            {
                Log.Error("提升权限失败", ex);
                return false;
            }
        }
    }
}
