using System;
using System.IO;
using System.Text;

namespace Score
{
    internal static class SafeFile
    {
        public static void WriteAllText(string path, string content)
        {
            WriteAtomic(path, temporaryPath => File.WriteAllText(temporaryPath, content ?? string.Empty, new UTF8Encoding(false)));
        }

        public static void WriteAllLines(string path, string[] lines)
        {
            WriteAtomic(path, temporaryPath => File.WriteAllLines(temporaryPath, lines ?? new string[0], new UTF8Encoding(false)));
        }

        private static void WriteAtomic(string path, Action<string> writer)
        {
            var directory = Path.GetDirectoryName(path);
            if (string.IsNullOrWhiteSpace(directory)) throw new ArgumentException("A destination directory is required.", "path");
            Directory.CreateDirectory(directory);

            var temporaryPath = Path.Combine(directory, Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
            var backupPath = path + ".bak";
            try
            {
                writer(temporaryPath);
                if (File.Exists(path)) File.Replace(temporaryPath, path, backupPath, true);
                else File.Move(temporaryPath, path);
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }
    }
}
