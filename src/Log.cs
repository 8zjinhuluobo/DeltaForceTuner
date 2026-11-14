using System;
using System.IO;
using System.Text;

namespace DeltaForceTuner
{
    internal static class Log
    {
        private static readonly object Sync = new object();

        public static string FilePath
        {
            get
            {
                return Path.Combine(AppPaths.DataDir, "app.log");
            }
        }

        public static void Info(string message)
        {
            Write("INFO", message);
        }

        public static void Error(string message)
        {
            Write("ERROR", message);
        }

        public static void Error(string message, Exception ex)
        {
            Write("ERROR", message + " :: " + (ex == null ? "" : ex.ToString()));
        }

        private static void Write(string level, string message)
        {
            try
            {
                lock (Sync)
                {
                    string path = FilePath;
                    string dir = Path.GetDirectoryName(path);
                    if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
                    try
                    {
                        FileInfo fi = new FileInfo(path);
                        if (fi.Exists && fi.Length > 512 * 1024)
                        {
                            string old = path + ".1";
                            if (File.Exists(old)) File.Delete(old);
                            File.Move(path, old);
                        }
                    }
                    catch
                    {
                    }
                    string line = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " [" + level + "] " + message + Environment.NewLine;
                    File.AppendAllText(path, line, new UTF8Encoding(false));
                }
            }
            catch
            {
            }
        }
    }
}
