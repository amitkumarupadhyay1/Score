using System;
using System.IO;

namespace Score.Tests
{
    internal static class Program
    {
        private static int failures;

        private static int Main()
        {
            Run("CSV quotes ordinary text", () => Equal("\"Team Alpha\"", ExportSafety.ToCsvCell("Team Alpha")));
            Run("CSV escapes quotes", () => Equal("\"A \"\"Team\"\"\"", ExportSafety.ToCsvCell("A \"Team\"")));
            Run("CSV neutralizes formulas", () => Equal("\"'=2+2\"", ExportSafety.ToCsvCell("=2+2")));
            Run("CSV neutralizes whitespace formulas", () => Equal("\"'  @SUM(A1:A2)\"", ExportSafety.ToCsvCell("  @SUM(A1:A2)")));
            Run("File names remove unsafe characters", () => True(!ExportSafety.SanitizeFileName("Quiz: Final/2026", "quiz").Contains(":"), "Colon remained in a Windows file name."));
            Run("File names avoid reserved devices", () => Equal("CON-quiz", ExportSafety.SanitizeFileName("CON", "quiz")));
            Run("File names are bounded", () => True(ExportSafety.SanitizeFileName(new string('A', 200), "quiz").Length <= 80, "File name exceeded 80 characters."));

            Run("Timer begins stopped", () => Equal(QuizTimerState.Stopped, new QuizTimerService().State));
            Run("Timer pause is a real paused state even in overtime", TestOvertimePause);
            Run("Timer restore preserves elapsed time", TestRestore);
            Run("SQLite runtime creates, verifies, and backs up the database", TestDatabaseRuntime);

            Console.WriteLine(failures == 0 ? "All production checks passed." : failures + " production check(s) failed.");
            return failures == 0 ? 0 : 1;
        }

        private static void TestOvertimePause()
        {
            var timer = new QuizTimerService();
            timer.Restore(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), QuizTimerState.Overtime);
            timer.Pause();
            Equal(QuizTimerState.Paused, timer.State);
            True(timer.IsOvertime, "The overtime condition was lost while paused.");
            timer.Resume();
            Equal(QuizTimerState.Overtime, timer.State);
        }

        private static void TestRestore()
        {
            var timer = new QuizTimerService();
            timer.Restore(TimeSpan.FromSeconds(60), TimeSpan.FromSeconds(15), QuizTimerState.Paused);
            Equal(QuizTimerState.Paused, timer.State);
            True(timer.Elapsed >= TimeSpan.FromSeconds(15), "Restored elapsed time was lost.");
            True(timer.Remaining <= TimeSpan.FromSeconds(45), "Restored remaining time is incorrect.");
        }

        private static void TestDatabaseRuntime()
        {
            var originalHome = Environment.GetEnvironmentVariable("QUIZSCORE_HOME");
            var testHome = Path.Combine(Path.GetTempPath(), "QuizScoreLive-Check-" + Guid.NewGuid().ToString("N"));
            try
            {
                Environment.SetEnvironmentVariable("QUIZSCORE_HOME", testHome);
                AppPaths.Initialize();
                NativeSqliteLoader.EnsureLoaded();
                DatabaseMaintenance.Prepare();
                True(File.Exists(AppPaths.DatabasePath), "The SQLite database was not created.");
                True(new FileInfo(AppPaths.DatabasePath).Length > 0, "The SQLite database is empty.");

                DatabaseMaintenance.Prepare();
                True(Directory.GetFiles(AppPaths.BackupsDirectory, "ScoreDB-*.sqlite").Length == 1, "The rolling database backup was not created.");
            }
            finally
            {
                Environment.SetEnvironmentVariable("QUIZSCORE_HOME", originalHome);
                if (Directory.Exists(testHome)) Directory.Delete(testHome, true);
            }
        }

        private static void Run(string name, Action check)
        {
            try
            {
                check();
                Console.WriteLine("PASS  " + name);
            }
            catch (Exception ex)
            {
                failures++;
                Console.Error.WriteLine("FAIL  " + name + ": " + ex.Message);
            }
        }

        private static void Equal<T>(T expected, T actual)
        {
            if (!object.Equals(expected, actual)) throw new InvalidOperationException("Expected " + expected + " but found " + actual + ".");
        }

        private static void True(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
