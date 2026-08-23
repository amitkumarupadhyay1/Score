using System;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;

namespace Score
{
    internal static class DatabaseMaintenance
    {
        public static void Prepare()
        {
            var databaseAlreadyExisted = File.Exists(AppPaths.DatabasePath);
            try
            {
                using (var connection = new SQLiteConnection("Data Source=" + AppPaths.DatabasePath + ";Version=3;Foreign Keys=True;Default Timeout=5;Pooling=False;"))
                {
                    connection.Open();
                    Execute(connection, "PRAGMA foreign_keys=ON");
                    Execute(connection, "PRAGMA busy_timeout=5000");
                    Execute(connection, "PRAGMA journal_mode=WAL");
                    Execute(connection, "PRAGMA synchronous=FULL");
                    EnsureSchema(connection);
                    RepairMultipleActiveSessions(connection);
                    var integrity = Convert.ToString(Scalar(connection, "PRAGMA quick_check"), CultureInfo.InvariantCulture);
                    if (!string.Equals(integrity, "ok", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("SQLite integrity check failed: " + integrity);
                    Execute(connection, "PRAGMA wal_checkpoint(TRUNCATE)");
                }

                if (databaseAlreadyExisted) CreateDailyBackup();
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Database preparation failed for " + AppPaths.DatabasePath, ex);
                throw new InvalidOperationException("QuizScore Live could not safely open its data. Your database was not deleted. See the diagnostic log at " + AppDiagnostics.LogFilePath + ".", ex);
            }
        }

        private static void EnsureSchema(SQLiteConnection connection)
        {
            Execute(connection, "CREATE TABLE IF NOT EXISTS ScoreCounts (ScoreCountID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, Team TEXT, ScoreValue INTEGER NOT NULL)");
            Execute(connection, "CREATE TABLE IF NOT EXISTS QuizSessions (QuizSessionId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, Title TEXT, ConfiguredTeamCount INTEGER NOT NULL DEFAULT 4, ConfiguredRounds INTEGER NOT NULL, CurrentRound INTEGER NOT NULL, Status TEXT, GlobalDurationSeconds INTEGER NOT NULL DEFAULT 0, RoundDurationSeconds INTEGER NOT NULL DEFAULT 0, GlobalElapsedSeconds INTEGER NOT NULL DEFAULT 0, RoundElapsedSeconds INTEGER NOT NULL DEFAULT 0, StartedAtUtc DATETIME, PausedAtUtc DATETIME, CompletedAtUtc DATETIME, LastUpdatedAtUtc DATETIME, IsActive INTEGER NOT NULL)");
            Execute(connection, "CREATE TABLE IF NOT EXISTS QuizRoundScores (QuizRoundScoreId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, QuizSessionId INTEGER NOT NULL, DatabaseTeamName TEXT, RoundNumber INTEGER NOT NULL, ScoreValue INTEGER NOT NULL, TeamElapsedSeconds INTEGER NOT NULL, ElapsedSeconds INTEGER NOT NULL, IsFinalized INTEGER NOT NULL)");
            Execute(connection, "CREATE TABLE IF NOT EXISTS QuizEventLogs (QuizEventLogId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, QuizSessionId INTEGER NOT NULL, OccurredAtUtc TEXT NOT NULL, EventType TEXT, Team TEXT, RoundNumber INTEGER NOT NULL, ScoreChange INTEGER NOT NULL, ScoreAfter INTEGER NOT NULL, ElapsedSeconds INTEGER NOT NULL, Details TEXT)");

            AddColumnIfMissing(connection, "QuizSessions", "ConfiguredTeamCount", "INTEGER NOT NULL DEFAULT 4");
            AddColumnIfMissing(connection, "QuizSessions", "GlobalElapsedSeconds", "INTEGER NOT NULL DEFAULT 0");
            AddColumnIfMissing(connection, "QuizSessions", "RoundElapsedSeconds", "INTEGER NOT NULL DEFAULT 0");
            AddColumnIfMissing(connection, "QuizSessions", "LastUpdatedAtUtc", "DATETIME");

            Execute(connection, "CREATE INDEX IF NOT EXISTS IX_ScoreCounts_Team ON ScoreCounts (Team)");
            Execute(connection, "CREATE INDEX IF NOT EXISTS IX_QuizRoundScores_SessionRoundTeam ON QuizRoundScores (QuizSessionId, RoundNumber, DatabaseTeamName)");
            Execute(connection, "CREATE INDEX IF NOT EXISTS IX_QuizEventLogs_SessionSequence ON QuizEventLogs (QuizSessionId, QuizEventLogId)");
        }

        private static void AddColumnIfMissing(SQLiteConnection connection, string table, string column, string definition)
        {
            using (var command = new SQLiteCommand("PRAGMA table_info(" + table + ")", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                    if (string.Equals(Convert.ToString(reader["name"], CultureInfo.InvariantCulture), column, StringComparison.OrdinalIgnoreCase)) return;
            }
            Execute(connection, "ALTER TABLE " + table + " ADD COLUMN " + column + " " + definition);
        }

        private static void RepairMultipleActiveSessions(SQLiteConnection connection)
        {
            Execute(connection, "UPDATE QuizSessions SET IsActive = 0 WHERE IsActive = 1 AND QuizSessionId <> (SELECT MAX(QuizSessionId) FROM QuizSessions WHERE IsActive = 1)");
            Execute(connection, "CREATE UNIQUE INDEX IF NOT EXISTS UX_QuizSessions_OneActive ON QuizSessions (IsActive) WHERE IsActive = 1");
        }

        private static void CreateDailyBackup()
        {
            Directory.CreateDirectory(AppPaths.BackupsDirectory);
            var prefix = "ScoreDB-" + DateTime.UtcNow.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
            if (Directory.EnumerateFiles(AppPaths.BackupsDirectory, prefix + "*.sqlite").Any()) return;

            var backupPath = Path.Combine(AppPaths.BackupsDirectory, prefix + "-" + DateTime.UtcNow.ToString("HHmmss", CultureInfo.InvariantCulture) + ".sqlite");
            File.Copy(AppPaths.DatabasePath, backupPath, false);

            foreach (var oldBackup in new DirectoryInfo(AppPaths.BackupsDirectory).GetFiles("ScoreDB-*.sqlite").OrderByDescending(file => file.CreationTimeUtc).Skip(14))
                oldBackup.Delete();
        }

        private static void Execute(SQLiteConnection connection, string sql)
        {
            using (var command = new SQLiteCommand(sql, connection)) command.ExecuteNonQuery();
        }

        private static object Scalar(SQLiteConnection connection, string sql)
        {
            using (var command = new SQLiteCommand(sql, connection)) return command.ExecuteScalar();
        }
    }
}
