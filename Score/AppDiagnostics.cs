using System;
using System.IO;
using System.Text;

namespace Score
{
    internal static class AppDiagnostics
    {
        private const long MaximumLogBytes = 2L * 1024L * 1024L;
        private static readonly object SyncRoot = new object();

        public static string LogFilePath { get { return Path.Combine(AppPaths.LogsDirectory, "QuizScoreLive.log"); } }

        public static void Write(string message)
        {
            try
            {
                lock (SyncRoot)
                {
                    Directory.CreateDirectory(AppPaths.LogsDirectory);
                    RotateIfNeeded();
                    var line = DateTime.UtcNow.ToString("o") + " [" + Environment.CurrentManagedThreadId + "] " + message + Environment.NewLine;
                    File.AppendAllText(LogFilePath, line, new UTF8Encoding(false));
                }
            }
            catch
            {
                // Diagnostics must never cause a second application failure.
            }
        }

        public static void WriteException(string context, Exception exception)
        {
            Write(context + Environment.NewLine + exception);
        }

        private static void RotateIfNeeded()
        {
            if (!File.Exists(LogFilePath) || new FileInfo(LogFilePath).Length < MaximumLogBytes) return;
            var archivePath = Path.Combine(AppPaths.LogsDirectory, "QuizScoreLive.previous.log");
            if (File.Exists(archivePath)) File.Delete(archivePath);
            File.Move(LogFilePath, archivePath);
        }
    }
}
