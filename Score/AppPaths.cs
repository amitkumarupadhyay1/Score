using System;
using System.IO;

namespace Score
{
    internal static class AppPaths
    {
        private const string ProductFolderName = "QuizScore Live";

        public static string RootDirectory
        {
            get
            {
                var overrideDirectory = Environment.GetEnvironmentVariable("QUIZSCORE_HOME");
                if (!string.IsNullOrWhiteSpace(overrideDirectory) && Path.IsPathRooted(overrideDirectory)) return Path.GetFullPath(overrideDirectory);
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), ProductFolderName);
            }
        }

        public static string DataDirectory { get { return Path.Combine(RootDirectory, "Data"); } }
        public static string SettingsDirectory { get { return Path.Combine(RootDirectory, "Settings"); } }
        public static string LogsDirectory { get { return Path.Combine(RootDirectory, "Logs"); } }
        public static string BackupsDirectory { get { return Path.Combine(RootDirectory, "Backups"); } }
        public static string DatabasePath { get { return Path.Combine(DataDirectory, "ScoreDB.sqlite"); } }

        public static void Initialize()
        {
            Directory.CreateDirectory(RootDirectory);
            Directory.CreateDirectory(DataDirectory);
            Directory.CreateDirectory(SettingsDirectory);
            Directory.CreateDirectory(LogsDirectory);
            Directory.CreateDirectory(BackupsDirectory);
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("QUIZSCORE_HOME"))) MigrateLegacyData();
        }

        private static void MigrateLegacyData()
        {
            var legacyDatabase = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Data", "ScoreDB.sqlite");
            if (!File.Exists(DatabasePath) && File.Exists(legacyDatabase))
            {
                File.Copy(legacyDatabase, DatabasePath, false);
                AppDiagnostics.Write("Migrated the legacy database to the per-user data directory.");
            }

            var legacySettings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Score");
            if (!Directory.Exists(legacySettings) || string.Equals(legacySettings, SettingsDirectory, StringComparison.OrdinalIgnoreCase)) return;

            foreach (var fileName in new[] { "event-title.txt", "team-names.txt", "team-players.txt", "round-context.txt" })
            {
                var source = Path.Combine(legacySettings, fileName);
                var destination = Path.Combine(SettingsDirectory, fileName);
                if (!File.Exists(destination) && File.Exists(source)) File.Copy(source, destination, false);
            }
        }
    }
}
