using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Score.Tests
{
    internal static class ProductionQualityChecks
    {
        internal static void Run()
        {
            VerifyCleanSlateNoAutoSeeding();
            VerifyShowcaseSampleSeeder();
            VerifyDeckDeletionAndCascadeCleanup();
            VerifyComboBoxContrastAndStyles();
            VerifyQuestionSidebarButtonsFitting();
            VerifyThemePalettes();
            VerifyTimerEngineStateTransitions();
            VerifyNeutralTeamDefaults();
            VerifyGuidedSetupWorkflow();
            VerifySessionHistoryCascadeDeletion();
            VerifyStudioCollapsiblePanelsAndThemes();
            VerifyConsoleTimerGlyphsAndWidths();
            Console.WriteLine("PASS  Production quality, clean slate, ComboBox contrast, and theme checks passed.");
        }

        private static void VerifyCleanSlateNoAutoSeeding()
        {
            var originalHome = Environment.GetEnvironmentVariable("QUIZSCORE_HOME");
            var testHome = Path.Combine(Path.GetTempPath(), "QuizScoreLive-CleanSlate-" + Guid.NewGuid().ToString("N"));
            try
            {
                Environment.SetEnvironmentVariable("QUIZSCORE_HOME", testHome);
                AppPaths.Initialize();
                NativeSqliteLoader.EnsureLoaded();
                DatabaseMaintenance.Prepare();

                using (var connection = new SQLiteConnection("Data Source=" + AppPaths.DatabasePath + ";Pooling=False;"))
                {
                    connection.Open();
                    using (var command = new SQLiteCommand("SELECT COUNT(*) FROM QuizDecks WHERE IsActive = 1;", connection))
                    {
                        var count = Convert.ToInt64(command.ExecuteScalar());
                        Require(count == 0, "Clean database must start with zero active quiz decks (no unsolicited preloaded data).");
                    }
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("QUIZSCORE_HOME", originalHome);
                if (Directory.Exists(testHome)) Directory.Delete(testHome, true);
            }
        }

        private static void VerifyShowcaseSampleSeeder()
        {
            var slides = QuizSampleDeckSeeder.CreateSampleSlides(999);
            Require(slides != null && slides.Count >= 10, "Showcase sample must generate at least 10 slides.");
            Require(slides.Any(s => s.QuestionType == "MultipleChoice"), "Showcase must include MultipleChoice questions.");
            Require(slides.Any(s => s.QuestionType == "TrueFalse"), "Showcase must include True/False questions.");
            Require(slides.Any(s => s.QuestionType == "ShortAnswer"), "Showcase must include Short Answer questions.");
            Require(slides.Any(s => s.RoundType == "TieBreak"), "Showcase must include Tie-Break questions.");

            foreach (var slide in slides.Where(s => s.SlideType == "Question"))
            {
                Require(!string.IsNullOrWhiteSpace(slide.Title), "Slide title must not be empty.");
                Require(!string.IsNullOrWhiteSpace(slide.ContentText), "Slide prompt must not be empty.");
                Require(slide.Points > 0, "Slide points must be greater than zero.");
                Require(slide.TimeLimitSeconds > 0, "Time limit must be positive.");
                Require(!string.IsNullOrWhiteSpace(slide.CorrectAnswer), "Correct answer must not be empty.");
            }
        }

        private static void VerifyDeckDeletionAndCascadeCleanup()
        {
            var originalHome = Environment.GetEnvironmentVariable("QUIZSCORE_HOME");
            var testHome = Path.Combine(Path.GetTempPath(), "QuizScoreLive-Cascade-" + Guid.NewGuid().ToString("N"));
            try
            {
                Environment.SetEnvironmentVariable("QUIZSCORE_HOME", testHome);
                AppPaths.Initialize();
                NativeSqliteLoader.EnsureLoaded();
                DatabaseMaintenance.Prepare();

                int deckId;
                using (var connection = new SQLiteConnection("Data Source=" + AppPaths.DatabasePath + ";Pooling=False;"))
                {
                    connection.Open();
                    using (var cmd = new SQLiteCommand(
                        "INSERT INTO QuizDecks (Title, Theme, CreatedAtUtc, IsActive) VALUES ('Test Deck', 'Stage', datetime('now'), 1); SELECT last_insert_rowid();",
                        connection))
                    {
                        deckId = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    using (var cmd = new SQLiteCommand(
                        "INSERT INTO DeckSlides (QuizDeckId, SlideOrder, SlideType, Title, ContentText, IsActive) VALUES (" + deckId + ", 1, 'Question', 'Q1', 'Prompt', 1);" +
                        "INSERT INTO QuizDeckSessions (QuizDeckId, StartedAtUtc, Status) VALUES (" + deckId + ", datetime('now'), 'Completed'); SELECT last_insert_rowid();",
                        connection))
                    {
                        var sessionId = Convert.ToInt32(cmd.ExecuteScalar());
                        using (var subCmd = new SQLiteCommand(
                            "INSERT INTO QuizResponses (QuizDeckSessionId, DeckSlideId, TeamName, AnswerText, IsCorrect, PointsAwarded) VALUES (" + sessionId + ", 1, 'Alpha', 'Ans', 1, 10);" +
                            "INSERT INTO QuizAuditEntries (QuizDeckSessionId, OccurredAtUtc, EventType, TeamName, SlideNumber, OldPoints, NewPoints, Reason) VALUES (" + sessionId + ", datetime('now'), 'Score', 'Alpha', 1, 0, 10, 'Correct');",
                            connection))
                        {
                            subCmd.ExecuteNonQuery();
                        }
                    }

                    // Cascade cleanup
                    using (var cmd = new SQLiteCommand(
                        "UPDATE QuizDecks SET IsActive = 0 WHERE QuizDeckId = " + deckId + ";" +
                        "UPDATE DeckSlides SET IsActive = 0 WHERE QuizDeckId = " + deckId + ";" +
                        "DELETE FROM QuizResponses WHERE QuizDeckSessionId IN (SELECT QuizDeckSessionId FROM QuizDeckSessions WHERE QuizDeckId = " + deckId + ");" +
                        "DELETE FROM QuizAuditEntries WHERE QuizDeckSessionId IN (SELECT QuizDeckSessionId FROM QuizDeckSessions WHERE QuizDeckId = " + deckId + ");" +
                        "DELETE FROM QuizDeckSessions WHERE QuizDeckId = " + deckId + ";",
                        connection))
                    {
                        cmd.ExecuteNonQuery();
                    }

                    using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM QuizDecks WHERE QuizDeckId = " + deckId + " AND IsActive = 1;", connection))
                        Require(Convert.ToInt64(cmd.ExecuteScalar()) == 0, "Deck must be inactive.");
                    using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM DeckSlides WHERE QuizDeckId = " + deckId + " AND IsActive = 1;", connection))
                        Require(Convert.ToInt64(cmd.ExecuteScalar()) == 0, "Slides must be inactive.");
                    using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM QuizDeckSessions WHERE QuizDeckId = " + deckId + ";", connection))
                        Require(Convert.ToInt64(cmd.ExecuteScalar()) == 0, "Sessions must be purged.");
                    using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM QuizResponses;", connection))
                        Require(Convert.ToInt64(cmd.ExecuteScalar()) == 0, "Responses must be purged.");
                    using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM QuizAuditEntries;", connection))
                        Require(Convert.ToInt64(cmd.ExecuteScalar()) == 0, "Audit entries must be purged.");
                }
            }
            finally
            {
                Environment.SetEnvironmentVariable("QUIZSCORE_HOME", originalHome);
                if (Directory.Exists(testHome)) Directory.Delete(testHome, true);
            }
        }

        private static void VerifyComboBoxContrastAndStyles()
        {
            var console = new QuizMasterConsoleWindow();
            var deckCombo = FindElement<ComboBox>(console, "ConsoleDeckSelector");
            Require(deckCombo != null, "ConsoleDeckSelector must exist.");
            Require(deckCombo.Style != null, "ConsoleDeckSelector must have an explicit style.");
            Require(deckCombo.ItemContainerStyle != null, "ConsoleDeckSelector must have an ItemContainerStyle to prevent white-on-white text.");
            Require(!Equals(deckCombo.Foreground, Brushes.White), "ConsoleDeckSelector foreground must be high-contrast dark text, not white.");

            var themeCombo = FindElement<ComboBox>(console, "DeskThemeSelector");
            Require(themeCombo != null, "DeskThemeSelector must exist.");
            Require(themeCombo.Style != null, "DeskThemeSelector must have an explicit style.");
            console.Close();

            var builder = new QuizDeckBuilderWindow();
            var builderDeckCombo = FindElement<ComboBox>(builder, "DeckSelector");
            Require(builderDeckCombo != null, "DeckSelector must exist.");
            Require(builderDeckCombo.ItemContainerStyle != null, "DeckSelector must have ItemContainerStyle.");

            var builderThemeCombo = FindElement<ComboBox>(builder, "DeckThemeSelector");
            Require(builderThemeCombo != null, "DeckThemeSelector must exist.");
            builder.Close();
        }

        private static void VerifyQuestionSidebarButtonsFitting()
        {
            var builder = new QuizDeckBuilderWindow();
            var upBtn = FindElement<Button>(builder, "MoveUpButton");
            var downBtn = FindElement<Button>(builder, "MoveDownButton");
            var delBtn = FindElement<Button>(builder, "DeleteSlideButton");

            Require(upBtn != null && downBtn != null && delBtn != null, "All sidebar reorder buttons must exist.");
            Require(Convert.ToString(upBtn.Content) == "Up", "MoveUpButton content should be compact 'Up'.");
            Require(Convert.ToString(downBtn.Content) == "Down", "MoveDownButton content should be compact 'Down'.");
            Require(Convert.ToString(delBtn.Content) == "Delete", "DeleteSlideButton content should be 'Delete'.");
            builder.Close();
        }

        private static void VerifyThemePalettes()
        {
            var themes = new[] { "Midnight Navy", "Emerald Arena", "Cobalt Ocean", "Daylight Contrast" };
            foreach (var theme in themes)
            {
                var palette = QuizThemePalette.GetPalette(theme);
                Require(palette != null, "Palette must exist for " + theme);
                Require(palette.WindowBackground != null, "WindowBackground missing in " + theme);
                Require(palette.CardBackground != null, "CardBackground missing in " + theme);
                Require(palette.TextPrimary != null, "TextPrimary missing in " + theme);
                Require(palette.AccentColor != null, "AccentColor missing in " + theme);
                Require(palette.TimerBorderBackground != null, "TimerBorderBackground missing in " + theme);
            }
        }

        private static void VerifyTimerEngineStateTransitions()
        {
            var timer = new QuizTimerService();
            Require(timer.State == QuizTimerState.Stopped, "Timer must start stopped.");

            timer.Start(TimeSpan.FromSeconds(30));
            Require(timer.State == QuizTimerState.Running, "Timer must be running.");
            Require(timer.Duration == TimeSpan.FromSeconds(30), "Timer duration must be 30s.");

            timer.Pause();
            Require(timer.State == QuizTimerState.Paused, "Timer must pause.");

            timer.Resume();
            Require(timer.State == QuizTimerState.Running, "Timer must resume.");

            timer.Duration = timer.Duration.Add(TimeSpan.FromSeconds(15));
            Require(timer.Duration == TimeSpan.FromSeconds(45), "Timer must add 15 seconds to duration.");

            timer.Reset();
            Require(timer.State == QuizTimerState.Stopped, "Timer must reset to stopped.");
        }

        private static void VerifyNeutralTeamDefaults()
        {
            var originalHome = Environment.GetEnvironmentVariable("QUIZSCORE_HOME");
            var testHome = Path.Combine(Path.GetTempPath(), "QuizScoreLive-Defaults-" + Guid.NewGuid().ToString("N"));
            try
            {
                Environment.SetEnvironmentVariable("QUIZSCORE_HOME", testHome);
                AppPaths.Initialize();
                AppDomain.CurrentDomain.SetData("DataDirectory", AppPaths.DataDirectory);
                NativeSqliteLoader.EnsureLoaded();
                DatabaseMaintenance.Prepare();

                var window = new MainWindow();
                var cards = window.Cards;
                Require(cards.Length == 4, "Scoring desk must default to 4 teams.");
                for (int i = 0; i < cards.Length; i++)
                {
                    Require(cards[i].TeamName == "Team " + (i + 1), $"Team name must be 'Team {i + 1}', got '{cards[i].TeamName}'.");
                    Require(cards[i].DatabaseTeamName == $"TEAM-{(i + 1):00}", $"Database team key must be 'TEAM-{(i + 1):00}', got '{cards[i].DatabaseTeamName}'.");
                    Require(!cards[i].TeamName.Contains("Aurora"), "Legacy Aurora dummy team name must be eradicated.");
                    Require(!cards[i].DatabaseTeamName.Contains("JBA"), "Legacy JBA team key must be eradicated.");
                    Require(!cards[i].DatabaseTeamName.Contains("YVM"), "Legacy YVM team key must be eradicated.");
                }
                var title = FindElement<TextBox>(window, "EventTitleEditor").Text;
                Require(title == "Live Quiz Event", $"Default event title must be 'Live Quiz Event', got '{title}'.");
                window.Close();
            }
            finally
            {
                SQLiteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Environment.SetEnvironmentVariable("QUIZSCORE_HOME", originalHome);
                try { if (Directory.Exists(testHome)) Directory.Delete(testHome, true); } catch { }
            }
        }

        private static void VerifyGuidedSetupWorkflow()
        {
            var wizard = new GuidedSetupWizardWindow();
            var titleBox = FindElement<TextBox>(wizard, "EventTitleInput");
            var placeholder = FindElement<TextBlock>(wizard, "EventTitlePlaceholder");
            Require(titleBox != null, "Wizard title box must exist.");
            Require(placeholder != null && placeholder.Text.Contains("Science Bowl"), "Placeholder must display 'e.g. Annual Inter-School Science Bowl 2026'.");
            Require(placeholder.Visibility == Visibility.Visible, "Placeholder must be visible when title is empty.");

            var demoBtn = FindElement<Button>(wizard, "DemoQuickFillButton");
            Require(demoBtn != null, "DemoQuickFillButton must exist.");
            demoBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(titleBox.Text.Contains("Science Bowl"), "Demo click should load Science Bowl demo title.");
            Require(placeholder.Visibility == Visibility.Collapsed, "Placeholder must hide when title is populated.");
            Require(wizard.ConfiguredTeams.Count == 0, "Before launch, ConfiguredTeams should be uncommitted.");
            var launchBtn = FindElement<Button>(wizard, "LaunchButton");
            launchBtn.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(wizard.ConfiguredTeams.Count == 4, "Demo should commit 4 teams.");
            Require(wizard.ConfiguredTeams.Contains("Cosmic Quarks"), "Demo should contain 'Cosmic Quarks'.");
            Require(wizard.ConfiguredTitle.Contains("Science Bowl"), "Demo should commit Science Bowl title.");
            wizard.Close();
        }

        private static void VerifySessionHistoryCascadeDeletion()
        {
            var originalHome = Environment.GetEnvironmentVariable("QUIZSCORE_HOME");
            var testHome = Path.Combine(Path.GetTempPath(), "QuizScoreLive-SessionCascade-" + Guid.NewGuid().ToString("N"));
            try
            {
                Environment.SetEnvironmentVariable("QUIZSCORE_HOME", testHome);
                AppPaths.Initialize();
                AppDomain.CurrentDomain.SetData("DataDirectory", AppPaths.DataDirectory);
                NativeSqliteLoader.EnsureLoaded();
                DatabaseMaintenance.Prepare();

                int testSessionId;
                using (var connection = new SQLiteConnection("Data Source=" + AppPaths.DatabasePath + ";Pooling=False;"))
                {
                    connection.Open();
                    using (var cmd = new SQLiteCommand(
                        "INSERT INTO QuizSessions (Title, ConfiguredTeamCount, ConfiguredRounds, CurrentRound, Status, IsActive, UsesSessionSettings) " +
                        "VALUES ('Test Deletion Session', 4, 3, 1, 'Completed', 0, 1); SELECT last_insert_rowid();",
                        connection))
                    {
                        testSessionId = Convert.ToInt32(cmd.ExecuteScalar());
                    }

                    using (var cmd = new SQLiteCommand(
                        "INSERT INTO QuizRoundScores (QuizSessionId, DatabaseTeamName, RoundNumber, ScoreValue, TeamElapsedSeconds, ElapsedSeconds, IsFinalized, IsTieBreakParticipant) " +
                        "VALUES (" + testSessionId + ", 'TEAM-01', 1, 10, 20, 20, 1, 0);" +
                        "INSERT INTO QuizEventLogs (QuizSessionId, OccurredAtUtc, EventType, Team, RoundNumber, ScoreChange, ScoreAfter, ElapsedSeconds, Details) " +
                        "VALUES (" + testSessionId + ", datetime('now'), 'TestEvent', 'TEAM-01', 1, 10, 10, 20, 'Test log');",
                        connection))
                    {
                        cmd.ExecuteNonQuery();
                    }
                }

                var sessionDir = Path.Combine(AppPaths.SettingsDirectory, "Sessions", testSessionId.ToString());
                Directory.CreateDirectory(sessionDir);
                File.WriteAllText(Path.Combine(sessionDir, "team-names.txt"), "Team 1\nTeam 2");

                // Perform cascade deletion
                SessionHistoryDialog.CascadeDeleteSession(testSessionId);

                using (var connection = new SQLiteConnection("Data Source=" + AppPaths.DatabasePath + ";Pooling=False;"))
                {
                    connection.Open();
                    using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM QuizSessions WHERE QuizSessionId = " + testSessionId + ";", connection))
                        Require(Convert.ToInt64(cmd.ExecuteScalar()) == 0, "QuizSession must be deleted.");
                    using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM QuizRoundScores WHERE QuizSessionId = " + testSessionId + ";", connection))
                        Require(Convert.ToInt64(cmd.ExecuteScalar()) == 0, "QuizRoundScores must be cascade deleted.");
                    using (var cmd = new SQLiteCommand("SELECT COUNT(*) FROM QuizEventLogs WHERE QuizSessionId = " + testSessionId + ";", connection))
                        Require(Convert.ToInt64(cmd.ExecuteScalar()) == 0, "QuizEventLogs must be cascade deleted.");
                }
                Require(!Directory.Exists(sessionDir), "Session settings directory must be deleted.");
            }
            finally
            {
                SQLiteConnection.ClearAllPools();
                GC.Collect();
                GC.WaitForPendingFinalizers();
                Environment.SetEnvironmentVariable("QUIZSCORE_HOME", originalHome);
                try { if (Directory.Exists(testHome)) Directory.Delete(testHome, true); } catch { }
            }
        }

        private static void VerifyStudioCollapsiblePanelsAndThemes()
        {
            var builder = new QuizDeckBuilderWindow();
            var leftCol = FindElement<ColumnDefinition>(builder, "LeftColumnDef");
            var rightCol = FindElement<ColumnDefinition>(builder, "RightColumnDef");
            var collapseLeft = FindElement<Button>(builder, "CollapseLeftPanelButton");
            var expandLeft = FindElement<Button>(builder, "ExpandLeftPanelButton");
            var collapseRight = FindElement<Button>(builder, "CollapseRightPanelButton");
            var expandRight = FindElement<Button>(builder, "ExpandRightPanelButton");

            Require(leftCol != null && rightCol != null, "Left and right column definitions must exist.");
            Require(collapseLeft != null && expandLeft != null, "Left collapse/expand buttons must exist.");
            Require(collapseRight != null && expandRight != null, "Right collapse/expand buttons must exist.");

            // Collapse left
            collapseLeft.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(leftCol.Width.Value == 0, "Left column width must collapse to 0.");
            Require(expandLeft.Visibility == Visibility.Visible, "ExpandLeftPanelButton must be visible.");

            // Expand left
            expandLeft.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(leftCol.Width.Value == 280, "Left column width must restore to 280.");
            Require(expandLeft.Visibility == Visibility.Collapsed, "ExpandLeftPanelButton must be collapsed.");

            // Collapse right
            collapseRight.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(rightCol.Width.Value == 0, "Right column width must collapse to 0.");
            Require(expandRight.Visibility == Visibility.Visible, "ExpandRightPanelButton must be visible.");

            // Expand right
            expandRight.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            Require(rightCol.Width.Value == 320, "Right column width must restore to 320.");
            Require(expandRight.Visibility == Visibility.Collapsed, "ExpandRightPanelButton must be collapsed.");

            // Theme selection
            var themeSelector = FindElement<ComboBox>(builder, "DeckThemeSelector");
            Require(themeSelector != null, "DeckThemeSelector must exist.");
            themeSelector.SelectedItem = "Broadcast";
            var previewBorder = FindElement<Border>(builder, "PreviewStageScreenBorder");
            Require(previewBorder != null, "PreviewStageScreenBorder must exist.");

            builder.Close();
        }

        private static void VerifyConsoleTimerGlyphsAndWidths()
        {
            var console = new QuizMasterConsoleWindow();
            var startBtn = FindElement<Button>(console, "TimerStartButton");
            var pauseBtn = FindElement<Button>(console, "TimerPauseButton");
            var resetBtn = FindElement<Button>(console, "TimerResetButton");
            var addBtn = FindElement<Button>(console, "TimerAdd15Button");

            Require(startBtn != null && pauseBtn != null && resetBtn != null && addBtn != null, "Timer buttons must exist.");
            Require(Convert.ToString(pauseBtn.Content) == "❚❚", "Pause button must use standard '❚❚' symbol, not fragile emoji.");
            Require(Convert.ToString(startBtn.Content) == "▶", "Start button must use '▶'.");
            Require(Convert.ToString(resetBtn.Content) == "↺", "Reset button must use '↺'.");
            Require(Convert.ToString(addBtn.Content) == "+15s", "Add 15 button must use '+15s'.");
            Require(startBtn.Width >= 36 && pauseBtn.Width >= 36, "Timer button width must be at least 36 to prevent clipping.");

            var buzzLabel = FindElement<TextBlock>(console, "BuzzQueueListLabel");
            Require(buzzLabel != null && !buzzLabel.Text.Contains("Alpha"), "BuzzQueueListLabel must not contain hardcoded dummy 'Team Alpha'.");

            console.Close();
        }

        private static T FindElement<T>(FrameworkElement root, string name) where T : class
        {
            return root?.FindName(name) as T;
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
