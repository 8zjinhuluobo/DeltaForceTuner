using System;
using System.IO;

namespace DeltaForceTuner
{
    internal static class AppPaths
    {
        private static string _dataDir;

        public static string DataDir
        {
            get
            {
                if (!string.IsNullOrEmpty(_dataDir)) return _dataDir;
                string[] candidates = new string[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DeltaForceTuner"),
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data"),
                    Path.Combine(Path.GetTempPath(), "DeltaForceTuner")
                };
                for (int i = 0; i < candidates.Length; i++)
                {
                    if (TryWrite(candidates[i]))
                    {
                        _dataDir = candidates[i];
                        return _dataDir;
                    }
                }
                _dataDir = candidates[candidates.Length - 1];
                return _dataDir;
            }
        }

        private static bool TryWrite(string dir)
        {
            try
            {
                if (string.IsNullOrEmpty(dir)) return false;
                Directory.CreateDirectory(dir);
                string probe = Path.Combine(dir, ".dft-" + Guid.NewGuid().ToString("N") + ".tmp");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
