using System;
using System.IO;

namespace SettlementAutomationCore.Helpers
{
    public static class Logger
    {
        private static readonly string BaseLogDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            "Mount and Blade II Bannerlord",
            "Configs"
        );

        private static readonly object LogLock = new object();

        public static void WriteLog(string modName, string message)
        {
            try
            {
                lock (LogLock)
                {
                    if (!Directory.Exists(BaseLogDirectory))
                    {
                        Directory.CreateDirectory(BaseLogDirectory);
                    }

                    string logPath = Path.Combine(BaseLogDirectory, $"{modName}_Log.txt");
                    File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}\n");
                }
            }
            catch
            {
                // Fail silently to avoid interrupting gameplay
            }
        }
    }
}
