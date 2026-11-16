using System;
using System.IO;
using System.Linq;
using System.Data.SQLite;

namespace Score.Tests
{
    internal static class Program
    {
        private static int failures;

        [STAThread]
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
            Run("Quiz question types and alternate short answers are scored independently", TestQuizAnswerRules);
            Run("Quiz scoring applies buzzer and penalty rules", TestQuizScoringRules);
            Run("Quiz import and export handles sample templates and question parsing", TestQuizImportExport);
            Run("Smart AI topic packs, paste importer, and slide discrimination", TestQuizAiFeatures);
            Run("SQLite runtime creates, verifies, and backs up the database", TestDatabaseRuntime);
            Run("Live quiz, recovery, export, and both New Game options", QuizWorkflowChecks.Run);

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

        private static void TestQuizAnswerRules()
        {
            bool exact;
            True(QuizScoringEngine.IsCorrectAnswer("MultipleChoice", "Oslo", "oslo", out exact) && exact,
                "Multiple-choice matching should ignore case.");
            True(QuizScoringEngine.IsCorrectAnswer("ShortAnswer", "NYC\nNew York", "new york", out exact),
                "Short answers should accept alternate answers.");
            True(QuizScoringEngine.IsCorrectAnswer("ShortAnswer", "Mahatma Gandhi", "M. K. Gandhi", "Gandhi;M. K. Gandhi", out exact),
                "Short answers should accept alternate variants and normalize punctuation.");
            True(QuizScoringEngine.IsCorrectAnswer("MultipleSelect", "A|B", "B|A", out exact) && exact,
                "Multiple-select matching should accept correct options in any order.");
            True(QuizScoringEngine.IsCorrectAnswer("Sequence", "A, B, C, D", "A-B-C-D", out exact) && exact,
                "Sequence matching should normalize separators.");
            True(QuizScoringEngine.IsCorrectAnswer("Numeric", "42", "42.0", out exact),
                "Numeric answers should compare by value.");
            True(!QuizScoringEngine.IsCorrectAnswer("Manual", "anything", "anything", out exact),
                "Manual answers must not be auto-scored.");
            Equal(2, QuizScoringEngine.ParseOptions("A\n\nB\r\n").Count);
        }

        private static void TestQuizScoringRules()
        {
            Equal(10, QuizScoringEngine.CalculatePoints("Buzzer", 10, 0, 0, true, true, 1, 0));
            Equal(5, QuizScoringEngine.CalculatePoints("Buzzer", 10, 0, 0, true, false, 2, 0));
            Equal(-3, QuizScoringEngine.CalculatePoints("Standard", 10, 0, 0, false, false, 0, 3));
            Equal(5, QuizScoringEngine.CalculatePoints("PassBounce", 10, 0, 0, true, false, 0, 0, true, 5));
            Equal(0, QuizScoringEngine.CalculatePoints("PassBounce", 10, 0, 0, false, false, 0, 0, true, 5));
        }

        private static void TestQuizImportExport()
        {
            var template = QuizImportExportService.GenerateSampleCsvTemplate();
            True(!string.IsNullOrWhiteSpace(template), "Sample template must not be empty.");
            var slides = QuizImportExportService.ImportSlidesFromCsv(template, 99, 1);
            True(slides.Count >= 5, "Sample template must import multiple question slides.");
            True(slides.Any(s => s.QuestionType == "MultipleChoice"), "Template should have MCQ questions.");
            True(slides.Any(s => s.QuestionType == "ShortAnswer"), "Template should have ShortAnswer questions.");
            var exported = QuizImportExportService.ExportDeckToCsv(new QuizDeck { Title = "Export Test" }, slides);
            True(exported.Contains("First Moon Landing"), "Exported CSV must contain question titles.");
        }

        private static void TestQuizAiFeatures()
        {
            // 1. Curated Topic Packs
            var packs = QuizAIGeneratorService.GetCuratedTopicPacks(42);
            Equal(6, packs.Count);
            True(packs.Any(p => p.Key == "science"), "Science topic pack missing.");
            True(packs.Any(p => p.Key == "history"), "History topic pack missing.");
            True(packs.Any(p => p.Key == "geography"), "Geography topic pack missing.");
            True(packs.Any(p => p.Key == "popculture"), "Pop Culture topic pack missing.");
            True(packs.Any(p => p.Key == "sports"), "Sports topic pack missing.");
            True(packs.Any(p => p.Key == "literature"), "Literature topic pack missing.");

            foreach (var pack in packs)
            {
                Equal(5, pack.Questions.Count);
                True(!string.IsNullOrWhiteSpace(pack.Name), "Pack name is empty.");
                True(!string.IsNullOrWhiteSpace(pack.Icon), "Pack icon is empty.");
                foreach (var q in pack.Questions)
                {
                    True(!string.IsNullOrWhiteSpace(q.Title), "Slide title is empty.");
                    True(!string.IsNullOrWhiteSpace(q.ContentText), "Question content is empty.");
                    True(q.Points > 0, "Points must be positive.");
                    True(!string.IsNullOrWhiteSpace(q.CorrectAnswer), "Correct answer must not be empty.");
                    True(q.TimeLimitSeconds > 0, "Time limit must be positive.");
                }
            }

            // 2. Generation of slides
            var generated = QuizAIGeneratorService.GenerateTopicQuestions("science", 10, 5);
            Equal(5, generated.Count);
            Equal(5, generated[0].SlideOrder);
            Equal(9, generated[4].SlideOrder);
            Equal(10, generated[0].QuizDeckId);

            // 3. Option Matching & Prefix Stripping
            var options = new System.Collections.Generic.List<string> { "Paris", "London", "Tokyo", "Berlin" };
            string matched;
            True(QuizAIGeneratorService.MatchesMcqOption("Paris", options, out matched) && matched == "Paris", "Exact match failed.");
            True(QuizAIGeneratorService.MatchesMcqOption("  paris  ", options, out matched) && matched == "Paris", "Trimmed case match failed.");
            True(QuizAIGeneratorService.MatchesMcqOption("A. Paris", options, out matched) && matched == "Paris", "Prefix match failed.");
            True(QuizAIGeneratorService.MatchesMcqOption("A) Paris", options, out matched) && matched == "Paris", "Prefix parenthesis match failed.");
            True(QuizAIGeneratorService.MatchesMcqOption("(A) Paris", options, out matched) && matched == "Paris", "Enclosed parenthesis match failed.");
            True(QuizAIGeneratorService.MatchesMcqOption("[A] Paris", options, out matched) && matched == "Paris", "Bracket prefix match failed.");
            True(QuizAIGeneratorService.MatchesMcqOption("Option A: Paris", options, out matched) && matched == "Paris", "Option word prefix match failed.");
            True(QuizAIGeneratorService.MatchesMcqOption("Paris.", options, out matched) && matched == "Paris", "Trailing period match failed.");
            True(QuizAIGeneratorService.MatchesMcqOption("A", options, out matched) && matched == "Paris", "Option letter A match failed.");
            True(QuizAIGeneratorService.MatchesMcqOption("(B)", options, out matched) && matched == "London", "Option letter (B) match failed.");
            True(QuizAIGeneratorService.MatchesMcqOption("[B]", options, out matched) && matched == "London", "Option letter [B] match failed.");
            True(QuizAIGeneratorService.MatchesMcqOption("Option C", options, out matched) && matched == "Tokyo", "Option C match failed.");
            True(QuizAIGeneratorService.MatchesMcqOption("**D**", options, out matched) && matched == "Berlin", "Markdown bold letter match failed.");
            True(!QuizAIGeneratorService.MatchesMcqOption("Madrid", options, out _), "Non-existent option should fail.");

            var prefixedOptions = new System.Collections.Generic.List<string> { "A. Paris", "B. London" };
            True(QuizAIGeneratorService.MatchesMcqOption("Paris", prefixedOptions, out matched) && matched == "A. Paris", "Prefixed option match failed.");
            True(QuizAIGeneratorService.MatchesMcqOption("(A)", prefixedOptions, out matched) && matched == "A. Paris", "Letter (A) to prefixed option match failed.");

            // 5-choice options (Option E)
            var fiveChoices = new System.Collections.Generic.List<string> { "Paris", "London", "Rome", "Berlin", "None of the above" };
            True(QuizAIGeneratorService.MatchesMcqOption("E", fiveChoices, out matched) && matched == "None of the above", "Option E match failed.");
            True(QuizAIGeneratorService.MatchesMcqOption("(E)", fiveChoices, out matched) && matched == "None of the above", "Option (E) match failed.");

            // 4. Boolean normalization
            string norm;
            True(QuizAIGeneratorService.IsBooleanAnswer("true", out norm) && norm == "True", "Boolean true normalization failed.");
            True(QuizAIGeneratorService.IsBooleanAnswer("False", out norm) && norm == "False", "Boolean false normalization failed.");
            True(QuizAIGeneratorService.IsBooleanAnswer("T", out norm) && norm == "True", "Boolean T normalization failed.");
            True(QuizAIGeneratorService.IsBooleanAnswer("**True**", out norm) && norm == "True", "Markdown bold boolean true failed.");
            True(QuizAIGeneratorService.IsBooleanAnswer("Yes", out norm) && norm == "True", "Boolean Yes normalization failed.");
            True(QuizAIGeneratorService.IsBooleanAnswer("No", out norm) && norm == "False", "Boolean No normalization failed.");

            // 5. Smart AI Paste Parsing (Standard and ChatGPT Markdown Bold)
            var pasteSample =
                "**1. What is the capital of Australia?**\n" +
                "**A)** Sydney\n" +
                "**B)** Canberra\n" +
                "**C)** Melbourne\n" +
                "**D)** Brisbane\n" +
                "**Answer:** **B) Canberra**\n" +
                "**Explanation:** Canberra is the federal capital territory.\n" +
                "**Hint:** It was founded in 1913 as a compromise.\n\n" +
                "2. Light travels faster through water than a vacuum.\n" +
                "True or False?\n" +
                "Answer: False\n" +
                "Hint: Water has a higher refractive index.\n\n" +
                "3. Who wrote the Odyssey?\n" +
                "Answer: Homer\n" +
                "Aliases: Ancient Homer\n" +
                "Points: 15\n\n" +
                "4. Which of the following is NOT a noble gas?\n" +
                "(A) Helium\n" +
                "(B) Neon\n" +
                "(C) Argon\n" +
                "(D) Krypton\n" +
                "(E) Chlorine\n" +
                "Answer: (E) Chlorine\n" +
                "Explanation: Chlorine is a halogen.";

            var parsed = QuizAIGeneratorService.ParseAiQuestions(pasteSample, 55, 1);
            Equal(4, parsed.Count);
            Equal("MultipleChoice", parsed[0].QuestionType);
            Equal("Canberra", parsed[0].CorrectAnswer);
            True(parsed[0].OptionsJson.Contains("Sydney") && parsed[0].OptionsJson.Contains("Canberra"), "Options missing.");
            True(!string.IsNullOrWhiteSpace(parsed[0].HintsJson), "Hint missing.");
            True(!string.IsNullOrWhiteSpace(parsed[0].Explanation), "Explanation missing.");

            Equal("TrueFalse", parsed[1].QuestionType);
            Equal("False", parsed[1].CorrectAnswer);

            Equal("ShortAnswer", parsed[2].QuestionType);
            Equal("Homer", parsed[2].CorrectAnswer);
            Equal("Ancient Homer", parsed[2].AlternateAnswers);
            Equal(15, parsed[2].Points);

            Equal("MultipleChoice", parsed[3].QuestionType);
            Equal("Chlorine", parsed[3].CorrectAnswer);
            True(parsed[3].OptionsJson.Contains("Chlorine"), "Option E missing in 5-choice question.");

            // 6. Slide Type Discrimination Badges
            var qSlide = new DeckSlide { SlideType = "Question", QuestionType = "MultipleChoice", Points = 10 };
            Equal("MCQ", qSlide.SlideTypeBadgeText);
            Equal("#DCFCE7", qSlide.SlideTypeBadgeBg);
            Equal("#15803D", qSlide.SlideTypeBadgeFg);
            Equal("10p", qSlide.PointsBadgeText);
            Equal(System.Windows.Visibility.Visible, qSlide.PointsBadgeVisibility);

            var tfSlide = new DeckSlide { SlideType = "Question", QuestionType = "TrueFalse", Points = 5 };
            Equal("TRUE/FALSE", tfSlide.SlideTypeBadgeText);
            Equal("5p", tfSlide.PointsBadgeText);

            var saSlide = new DeckSlide { SlideType = "Question", QuestionType = "ShortAnswer", Points = 15 };
            Equal("SHORT ANSWER", saSlide.SlideTypeBadgeText);

            var instrSlide = new DeckSlide { SlideType = "Instruction", Title = "Rules", Points = 0 };
            Equal("INSTRUCTION", instrSlide.SlideTypeBadgeText);
            Equal("#F3E8FF", instrSlide.SlideTypeBadgeBg);
            Equal("#7E22CE", instrSlide.SlideTypeBadgeFg);
            Equal("—", instrSlide.PointsBadgeText);
            Equal(System.Windows.Visibility.Collapsed, instrSlide.PointsBadgeVisibility);

            var mediaSlide = new DeckSlide { SlideType = "Media", Title = "Audio Clue", Points = 0 };
            Equal("MEDIA", mediaSlide.SlideTypeBadgeText);
            Equal("#FEF3C7", mediaSlide.SlideTypeBadgeBg);
            Equal("#B45309", mediaSlide.SlideTypeBadgeFg);
            Equal(System.Windows.Visibility.Collapsed, mediaSlide.PointsBadgeVisibility);

            var timerSlide = new DeckSlide { SlideType = "Timer", Title = "Intermission", TimeLimitSeconds = 45 };
            Equal("TIMER", timerSlide.SlideTypeBadgeText);
            Equal("#E0F2FE", timerSlide.SlideTypeBadgeBg);
            Equal("#0369A1", timerSlide.SlideTypeBadgeFg);
            Equal(System.Windows.Visibility.Collapsed, timerSlide.PointsBadgeVisibility);

            var scoreSlide = new DeckSlide { SlideType = "Scoreboard", Title = "Standings" };
            Equal("SCOREBOARD", scoreSlide.SlideTypeBadgeText);
            Equal("#F1F5F9", scoreSlide.SlideTypeBadgeBg);
            Equal("#334155", scoreSlide.SlideTypeBadgeFg);
            Equal(System.Windows.Visibility.Collapsed, scoreSlide.PointsBadgeVisibility);

            // 7. IsValidQuestion verification
            True(QuizDeckBuilderWindow.IsValidQuestion("MultipleChoice", "Paris", "Paris\nLondon\nTokyo"), "MCQ validation failed.");
            True(QuizDeckBuilderWindow.IsValidQuestion("MultipleChoice", "A", "Paris\nLondon\nTokyo"), "MCQ letter validation failed.");
            True(QuizDeckBuilderWindow.IsValidQuestion("MultipleChoice", "(B)", "Paris\nLondon\nTokyo"), "MCQ parenthesized letter failed.");
            True(QuizDeckBuilderWindow.IsValidQuestion("MultipleChoice", "Paris.", "Paris\nLondon\nTokyo"), "MCQ trailing period failed.");
            True(!QuizDeckBuilderWindow.IsValidQuestion("MultipleChoice", "Berlin", "Paris\nLondon\nTokyo"), "MCQ unmatched answer passed.");
            True(QuizDeckBuilderWindow.IsValidQuestion("TrueFalse", "True", ""), "TrueFalse validation failed.");
            True(QuizDeckBuilderWindow.IsValidQuestion("TrueFalse", "false", ""), "TrueFalse lowercase failed.");
            True(!QuizDeckBuilderWindow.IsValidQuestion("TrueFalse", "Maybe", ""), "TrueFalse invalid answer passed.");
            True(QuizDeckBuilderWindow.IsValidQuestion("Numeric", "42", ""), "Numeric integer validation failed.");
            True(QuizDeckBuilderWindow.IsValidQuestion("Numeric", "3.14", ""), "Numeric decimal validation failed.");
            True(!QuizDeckBuilderWindow.IsValidQuestion("Numeric", "abc", ""), "Numeric invalid text passed.");
            True(QuizDeckBuilderWindow.IsValidQuestion("ShortAnswer", "Leonardo da Vinci", ""), "Short answer validation failed.");
            True(!QuizDeckBuilderWindow.IsValidQuestion("ShortAnswer", "", ""), "Short answer empty answer passed.");
            True(QuizDeckBuilderWindow.IsValidQuestion("Manual", "", ""), "Manual validation failed.");
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
                using (var connection = new SQLiteConnection("Data Source=" + AppPaths.DatabasePath + ";Pooling=False;"))
                {
                    connection.Open();
                    using (var command = new SQLiteCommand(
                        "CREATE TABLE DeckSlides (DeckSlideId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, QuizDeckId INTEGER NOT NULL, SlideType TEXT, SlideOrder INTEGER NOT NULL, Title TEXT, ContentText TEXT, MediaPath TEXT, MediaType TEXT, CorrectAnswer TEXT, OptionsJson TEXT, Points INTEGER NOT NULL DEFAULT 0, TimeLimitSeconds INTEGER NOT NULL DEFAULT 0, IsActive INTEGER NOT NULL);" +
                        "CREATE TABLE QuizDeckSessions (QuizDeckSessionId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, QuizDeckId INTEGER NOT NULL, StartedAtUtc DATETIME, CompletedAtUtc DATETIME, Status TEXT, CurrentSlideIndex INTEGER NOT NULL DEFAULT 0, QuizMasterNotes TEXT);" +
                        "CREATE TABLE QuizResponses (QuizResponseId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, QuizDeckSessionId INTEGER NOT NULL, DeckSlideId INTEGER NOT NULL, TeamName TEXT, AnswerText TEXT, AnsweredAtUtc DATETIME, ResponseTimeSeconds INTEGER NOT NULL DEFAULT 0, IsCorrect INTEGER NOT NULL DEFAULT 0, PointsAwarded INTEGER NOT NULL DEFAULT 0, BuzzOrder INTEGER NOT NULL DEFAULT 0);",
                        connection))
                        command.ExecuteNonQuery();
                }
                DatabaseMaintenance.Prepare();
                True(File.Exists(AppPaths.DatabasePath), "The SQLite database was not created.");
                True(new FileInfo(AppPaths.DatabasePath).Length > 0, "The SQLite database is empty.");

                using (var connection = new SQLiteConnection("Data Source=" + AppPaths.DatabasePath + ";Pooling=False;"))
                {
                    connection.Open();
                    True(HasColumn(connection, "DeckSlides", "QuestionType"), "Question types are missing from the deck schema.");
                    True(HasColumn(connection, "QuizDeckSessions", "TimerStartedAtUtc"), "Persisted timer state is missing.");
                    True(HasColumn(connection, "QuizResponses", "SlideTitleSnapshot"), "Response history snapshots are missing.");
                    True(HasColumn(connection, "QuizResponses", "SlideOrderSnapshot"), "Response slide-order snapshots are missing.");
                    using (var command = new SQLiteCommand("SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='QuizDeckSessionTeams'", connection))
                        True(Convert.ToInt64(command.ExecuteScalar()) == 1, "The persisted quiz team table is missing.");
                    using (var command = new SQLiteCommand("DROP INDEX UX_ScoreCounts_Team; INSERT INTO ScoreCounts (Team,ScoreValue) VALUES ('legacy-team',0),('legacy-team',20);", connection))
                        command.ExecuteNonQuery();
                }

                DatabaseMaintenance.Prepare();
                True(Directory.GetFiles(AppPaths.BackupsDirectory, "ScoreDB-*.sqlite").Length == 1, "The rolling database backup was not created.");
                using (var connection = new SQLiteConnection("Data Source=" + AppPaths.DatabasePath + ";Pooling=False;"))
                {
                    connection.Open();
                    using (var command = new SQLiteCommand("SELECT ScoreValue FROM ScoreCounts WHERE Team='legacy-team'", connection))
                        Equal(20L, Convert.ToInt64(command.ExecuteScalar()));
                    using (var command = new SQLiteCommand("SELECT COUNT(*) FROM ScoreCounts WHERE Team='legacy-team'", connection))
                        Equal(1L, Convert.ToInt64(command.ExecuteScalar()));
                    var rejected = false;
                    try
                    {
                        using (var command = new SQLiteCommand("INSERT INTO ScoreCounts (Team,ScoreValue) VALUES ('legacy-team',1)", connection))
                            command.ExecuteNonQuery();
                    }
                    catch (SQLiteException)
                    {
                        rejected = true;
                    }
                    True(rejected, "Duplicate team scores were allowed after migration.");
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("QUIZSCORE_HOME", originalHome);
                if (Directory.Exists(testHome)) Directory.Delete(testHome, true);
            }
        }

        private static bool HasColumn(SQLiteConnection connection, string tableName, string columnName)
        {
            using (var command = new SQLiteCommand("PRAGMA table_info(" + tableName + ")", connection))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                    if (string.Equals(Convert.ToString(reader["name"]), columnName, StringComparison.OrdinalIgnoreCase))
                        return true;
            }
            return false;
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
                Console.Error.WriteLine("FAIL  " + name + ": " + ex);
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
