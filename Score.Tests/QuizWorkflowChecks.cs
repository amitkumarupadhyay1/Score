using System;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Score.Tests
{
    internal static partial class QuizWorkflowChecks
    {
        public static void Run()
        {
            var previousHome = Environment.GetEnvironmentVariable("QUIZSCORE_HOME");
            var previousDataDirectory = AppDomain.CurrentDomain.GetData("DataDirectory");
            var testHome = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "QuizScoreLive-Workflow-" + Guid.NewGuid().ToString("N"));
            Environment.SetEnvironmentVariable("QUIZSCORE_HOME", testHome);
            App app = null;
            MainWindow window = null;
            try
            {
                AppPaths.Initialize();
                AppDomain.CurrentDomain.SetData("DataDirectory", AppPaths.DataDirectory);
                NativeSqliteLoader.EnsureLoaded();
                DatabaseMaintenance.Prepare();
                app = new App();
                app.InitializeComponent();
                app.ShutdownMode = ShutdownMode.OnExplicitShutdown;
                window = new MainWindow();
                Find<ComboBox>(window, "GlobalMinutesSelector").SelectedIndex = 5;
                Find<ComboBox>(window, "RoundMinutesSelector").SelectedIndex = 4;
                window.Close();
                window = new MainWindow();
                Equal("01:30:00", Find<TextBlock>(window, "GlobalTimerLabel").Text);
                Equal("05:00", Find<TextBlock>(window, "RoundTimerLabel").Text);
                Console.WriteLine("PASS  First-session setup survives reopening before Start Live");
                var cards = Cards(window);
                cards[0].TeamName = "Aurora Science";
                cards[0].TeamMembers = "Ada, Grace";
                Find<TextBox>(window, "EventTitleEditor").Text = "Science Cup";
                Find<TextBox>(window, "RoundLabelEditor").Text = "Physics";
                Find<ComboBox>(window, "RoundCountSelector").SelectedIndex = 1;
                Find<ComboBox>(window, "GlobalMinutesSelector").SelectedIndex = 1;
                Find<ComboBox>(window, "RoundMinutesSelector").SelectedIndex = 1;
                Click(Find<Button>(window, "ChimeButton"));
                Equal("SOUND OFF", Find<Button>(window, "ChimeButton").Content);

                Click(Find<Button>(window, "StartTimerButton"));
                var responseButton = Find<Button>(cards[0], "TeamTimerButton");
                Click(responseButton);
                Thread.Sleep(1100);
                Click(Find<Button>(window, "PauseTimerButton"));
                Equal(true, Find<Button>(window, "ResumeTimerButton").IsEnabled);
                Equal(false, responseButton.IsEnabled);
                Equal(false, Find<Button>(cards[0], "ResetTeamTimerButton").IsEnabled);
                Require(Find<TextBlock>(cards[0], "TeamTimerLabel").Text != "00:00", "Pause did not keep the team's response time.");
                AwardFive(cards[0]);
                Click(Find<Button>(window, "ResumeTimerButton"));
                Equal(true, responseButton.IsEnabled);
                AwardFive(cards[0]);
                Equal(10, cards[0].ScoreValue);
                Click(Find<Button>(window, "FinishRoundButton"));
                using (var connection = OpenDatabase())
                {
                    Execute(connection, "CREATE TRIGGER RejectNextRound BEFORE INSERT ON QuizEventLogs WHEN NEW.EventType = 'RoundStarted' BEGIN SELECT RAISE(ABORT, 'Test round transition failure'); END");
                    Click(Find<Button>(window, "WinnerActionButton"));
                    Equal(Visibility.Visible, Find<TextBlock>(window, "RoundSaveError").Visibility);
                    Equal("ROUND 1 OF 2", Find<TextBlock>(window, "RoundNumberLabel").Text);
                    Equal(10, cards[0].ScoreValue);
                    Equal(1L, QueryNumber(connection, "SELECT CurrentRound FROM QuizSessions WHERE IsActive = 1"));
                    Equal(10L, QueryNumber(connection, "SELECT MAX(ScoreValue) FROM ScoreCounts"));
                    Equal(0L, QueryNumber(connection, "SELECT COUNT(*) FROM QuizEventLogs WHERE EventType = 'RoundStarted'"));
                    Execute(connection, "DROP TRIGGER RejectNextRound");
                    window.Close();
                    // An older interrupted transition can leave this working table empty while the recorded results survive.
                    Execute(connection, "UPDATE ScoreCounts SET ScoreValue = 0");
                }
                window = new MainWindow();
                cards = Cards(window);
                Equal("10 POINTS", Find<TextBlock>(window, "WinnerScore").Text);
                Equal(10, cards[0].ScoreValue);
                Click(Find<Button>(window, "WinnerActionButton"));
                Equal("ROUND 2 OF 2", Find<TextBlock>(window, "RoundNumberLabel").Text);
                Equal(0, cards[0].ScoreValue);
                Equal("00:00", Find<TextBlock>(cards[0], "TeamTimerLabel").Text);
                Click(Find<Button>(window, "ResumeTimerButton"));
                AwardFive(cards[1]);
                AwardFive(cards[2]);
                responseButton = Find<Button>(cards[1], "TeamTimerButton");
                Click(responseButton);
                Thread.Sleep(1100);
                Click(responseButton);
                var responseTime = Find<TextBlock>(cards[1], "TeamTimerLabel").Text;
                Require(responseTime != "00:00", "The response timer did not record a time.");
                using (var connection = OpenDatabase())
                    Require(QueryNumber(connection, "SELECT LENGTH(TeamResponseTicks) FROM QuizSessions WHERE IsActive = 1") > 0, "Response time was not saved on stop.");
                window.Close();
                using (var connection = OpenDatabase())
                    Execute(connection, "UPDATE QuizSessions SET Status = 'Running', GlobalElapsedSeconds = 20, RoundElapsedSeconds = 20, LastUpdatedAtUtc = '2000-01-01 00:00:00Z' WHERE IsActive = 1");

                window = new MainWindow();
                Equal("SOUND OFF", Find<Button>(window, "ChimeButton").Content);
                Equal("PAUSED", Find<TextBlock>(window, "CompetitionStatusLabel").Text);
                cards = Cards(window);
                Equal("Aurora Science", cards[0].TeamName);
                Equal(5, cards[1].ScoreValue);
                Equal("00:40", Find<TextBlock>(window, "RoundTimerLabel").Text);
                Equal(responseTime, Find<TextBlock>(cards[1], "TeamTimerLabel").Text);
                Equal(false, Find<Button>(cards[1], "TeamTimerButton").IsEnabled);
                Click(Find<Button>(window, "ResumeTimerButton"));
                Click(Find<Button>(window, "FinishRoundButton"));
                var results = Find<StackPanel>(window, "RoundResultsRows");
                Equal("1", ResultCell(results, 0, 0));
                Equal(cards[1].TeamName, ResultCell(results, 0, 1));
                Equal("1", ResultCell(results, 1, 0));
                Equal("3", ResultCell(results, 2, 0));
                Equal("Aurora Science", ResultCell(results, 2, 1));
                Equal("00:" + responseTime, ResultCell(results, 0, 4));
                Click(Find<Button>(window, "WinnerActionButton"));
                var final = Find<StackPanel>(window, "FinalStandingsRows");
                Equal("Aurora Science", ResultCell(final, 0, 1));
                Equal("2", ResultCell(final, 1, 0));
                Equal("2", ResultCell(final, 2, 0));
                Equal(Visibility.Visible, Find<StackPanel>(window, "FinalGameActions").Visibility);

                var export = Path.Combine(testHome, "results.csv");
                window.WriteAuditExport(export);
                var csv = File.ReadAllText(export);
                Require(csv.Contains("Science Cup") && csv.Contains("FinalRoundComplete") && csv.Contains("2,\"" + cards[1].TeamName + "\",5"), "CSV is missing final results.");

                window.ShowActivated = false;
                window.ShowInTaskbar = false;
                window.WindowState = WindowState.Normal;
                window.Width = window.MinWidth;
                window.Height = window.MinHeight;
                window.Show();
                RequireFits(window, Find<Button>(window, "FinalExportButton"));
                RequireFits(window, Find<Button>(window, "FinalNewGameButton"));
                SelectNewGameOption(window, Find<Button>(window, "FinalNewGameButton"), "KeepGameSettings");
                RequireFits(window, Find<Button>(window, "ChimeButton"));
                RequireFits(window, Find<Button>(window, "ProjectorButton"));
                Equal("Science Cup", Find<TextBox>(window, "EventTitleEditor").Text);
                Equal("ROUND 1 OF 2", Find<TextBlock>(window, "RoundNumberLabel").Text);
                Equal("Ada, Grace", Cards(window)[0].TeamMembers);
                Equal("Physics", Find<TextBox>(window, "RoundLabelEditor").Text);
                Equal("01:00", Find<TextBlock>(window, "RoundTimerLabel").Text);
                Require(Cards(window).All(card => card.ScoreValue == 0), "Old scores survived New Game.");
                window.Close();

                window = new MainWindow();
                Equal("Science Cup", Find<TextBox>(window, "EventTitleEditor").Text);
                Equal("Ada, Grace", Cards(window)[0].TeamMembers);
                Equal("01:00", Find<TextBlock>(window, "RoundTimerLabel").Text);
                window.ShowActivated = false;
                window.ShowInTaskbar = false;
                window.WindowState = WindowState.Normal;
                window.Show();
                SelectNewGameOption(window, Find<Button>(window, "NewGameButton"), "StartBlankGame");
                Equal("QUIZ CHAMPIONSHIP", Find<TextBox>(window, "EventTitleEditor").Text);
                Equal("ROUND 1 OF 3", Find<TextBlock>(window, "RoundNumberLabel").Text);
                Equal(string.Empty, Cards(window)[0].TeamMembers);
                Equal("General Knowledge", Find<TextBox>(window, "RoundLabelEditor").Text);
                Equal("02:00", Find<TextBlock>(window, "RoundTimerLabel").Text);
                window.Close();

                window = new MainWindow();
                Equal("QUIZ CHAMPIONSHIP", Find<TextBox>(window, "EventTitleEditor").Text);
                Equal(string.Empty, Cards(window)[0].TeamMembers);
                Equal("SOUND OFF", Find<Button>(window, "ChimeButton").Content);
                Equal(true, Find<Button>(window, "StartTimerButton").IsEnabled);
                using (var connection = OpenDatabase())
                {
                    Equal(8L, QueryNumber(connection, "SELECT COUNT(*) FROM QuizRoundScores WHERE QuizSessionId IN (SELECT QuizSessionId FROM QuizSessions WHERE Title = 'Science Cup' AND IsActive = 0)"));
                    Equal(1L, QueryNumber(connection, "SELECT COUNT(*) FROM QuizSessions WHERE IsActive = 1"));
                    Equal(1L, QueryNumber(connection, "SELECT COUNT(*) FROM QuizEventLogs WHERE EventType = 'RoundStarted'"));
                }
                window.ShowActivated = false;
                window.ShowInTaskbar = false;
                window.WindowState = WindowState.Normal;
                window.Show();
                Click(Find<Button>(window, "ProjectorButton"));
                var projector = app.Windows.OfType<ProjectorWindow>().Single();
                Click(Find<Button>(window, "StartTimerButton"));
                AwardFive(Cards(window)[0]);
                window.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
                Require(VisualDescendants(projector).OfType<TextBlock>().Any(text => text.Text == "5"), "The projector score did not update.");
                Require(VisualDescendants(projector).OfType<TextBlock>().Any(text => text.Text.StartsWith("ROUND  0", StringComparison.Ordinal)), "The projector clock is missing.");
                Find<ComboBox>(window, "ProjectorThemeSelector").SelectedIndex = 1;
                Equal(ProjectorTheme.HighContrast, projector.Theme);
                projector.SetTheme(ProjectorTheme.Light);
                Equal(2, Find<ComboBox>(window, "ProjectorThemeSelector").SelectedIndex);
                Click(Find<Button>(window, "ProjectorStandingsButton"));
                Equal(ProjectorView.Standings, projector.View);
                AwardFive(Cards(window)[0]);
                Equal(ProjectorView.Standings, projector.View);
                Equal(10L, window.GetProjectorTeams()[0].Total);
                Click(Find<Button>(window, "ProjectorButton"));
                Equal(ProjectorView.Live, projector.View);
                window.Close();
                window = null;
                Require(!projector.IsVisible, "The projector stayed open after the control window closed.");
                window = new MainWindow();
                window.ShowActivated = false;
                window.ShowInTaskbar = false;
                window.WindowState = WindowState.Normal;
                window.Show();
                Equal(2, Find<ComboBox>(window, "ProjectorThemeSelector").SelectedIndex);
                RunUpgradeScenarios(ref window, testHome);
                VerifyQuizCreationAndBuzzerSystem();
                Console.WriteLine("PASS  Quiz authoring studio, buzzer server, audio, console, and audience windows initialize cleanly");
                ProjectorChecks.Run();
            }
            finally
            {
                if (window != null) window.Close();
                if (app != null) app.Shutdown();
                AppDomain.CurrentDomain.SetData("DataDirectory", previousDataDirectory);
                Environment.SetEnvironmentVariable("QUIZSCORE_HOME", previousHome);
                GC.Collect();
                GC.WaitForPendingFinalizers();
                SQLiteConnection.ClearAllPools();
                try { Directory.Delete(testHome, true); }
                catch (IOException ex) { Console.WriteLine("Temporary workflow data cleanup: " + ex.Message); }
            }
        }

        private static void RunUpgradeScenarios(ref MainWindow window, string testHome)
        {
            SelectNewGameOption(window, Find<Button>(window, "NewGameButton"), "StartBlankGame");
            Find<ComboBox>(window, "RoundCountSelector").SelectedIndex = 0;
            Click(Find<Button>(window, "StartTimerButton"));
            var cards = Cards(window);
            Click(Find<Button>(cards[0], "TeamTimerButton"));
            Thread.Sleep(1100);
            Click(Find<Button>(cards[0], "TeamTimerButton"));
            var export = Path.Combine(testHome, "live-upgrade.csv");
            window.WriteAuditExport(export);
            var teamRow = File.ReadLines(export).First(line => line.StartsWith("\"" + cards[0].TeamName + "\","));
            Require(!teamRow.EndsWith(",0,\"00:00:00\""), "Live export omitted response time.");

            using (var connection = OpenDatabase())
            {
                Execute(connection, "CREATE TRIGGER RejectPause BEFORE UPDATE ON QuizSessions WHEN NEW.Status='Paused' BEGIN SELECT RAISE(ABORT, 'Test pause failure'); END");
                try
                {
                    Click(Find<Button>(window, "PauseTimerButton"));
                    Equal(Visibility.Visible, Find<Border>(window, "SaveWarningPanel").Visibility);
                    Equal(1L, QueryNumber(connection, "SELECT COUNT(*) FROM QuizSessions WHERE IsActive=1 AND Status='Running'"));
                }
                finally { Execute(connection, "DROP TRIGGER RejectPause"); }
                Click(Find<Button>(window, "RetrySaveButton"));
                Equal(Visibility.Collapsed, Find<Border>(window, "SaveWarningPanel").Visibility);
                Equal(1L, QueryNumber(connection, "SELECT COUNT(*) FROM QuizSessions WHERE IsActive=1 AND Status='Paused'"));
            }
            Console.WriteLine("PASS  Live response export and visible save failure with retry");

            Click(Find<Button>(window, "ResumeTimerButton"));
            Find<Expander>(window, "QuestionPanel").IsExpanded = true;
            Find<ComboBox>(window, "QuestionSecondsSelector").Text = "1";
            Find<CheckBox>(window, "QuestionLockCheckBox").IsChecked = true;
            Click(Find<Button>(window, "QuestionStartButton"));
            Thread.Sleep(1100);
            window.RefreshQuestion();
            Require(Find<TextBlock>(window, "QuestionSummary").Text.Contains("TIME UP"), "Question did not expire.");
            Equal(false, ScoreButton(cards[0], "+5").IsEnabled);
            Click(Find<Button>(window, "QuestionAddButton"));
            Equal(true, ScoreButton(cards[0], "+5").IsEnabled);
            Equal("11", Find<ComboBox>(window, "QuestionSecondsSelector").Text);
            Click(Find<Button>(window, "QuestionStartButton"));
            Click(Find<Button>(window, "PauseTimerButton"));
            var held = Find<TextBlock>(window, "QuestionSummary").Text;
            Thread.Sleep(200);
            window.RefreshQuestion();
            Equal(held, Find<TextBlock>(window, "QuestionSummary").Text);
            window.Close();
            window = new MainWindow();
            cards = Cards(window);
            Require(Find<TextBlock>(window, "QuestionSummary").Text.Contains("HELD"), "Question resumed automatically on recovery.");
            Equal("11", Find<ComboBox>(window, "QuestionSecondsSelector").Text);
            Equal(true, Find<CheckBox>(window, "QuestionLockCheckBox").IsChecked);
            Click(Find<Button>(window, "ResumeTimerButton"));
            Click(Find<Button>(window, "QuestionStartButton"));
            Click(Find<Button>(window, "QuestionHoldButton"));
            Require(Find<TextBlock>(window, "QuestionSummary").Text.Contains("HELD"), "Hold did not pause the question.");
            Click(Find<Button>(window, "QuestionRestartButton"));
            Require(Find<TextBlock>(window, "QuestionSummary").Text.Contains("READY"), "Question restart did not reset it.");
            Click(Find<Button>(window, "QuestionNextButton"));
            Require(Find<TextBlock>(window, "QuestionSummary").Text.StartsWith("QUESTION 2"), "Question number did not advance.");
            Console.WriteLine("PASS  Question expiry, score lock, add time, hold, restart and recovery");

            AwardFive(cards[0]);
            AwardFive(cards[1]);
            string error;
            Require(!window.TryCorrectScore(1, cards[0].DatabaseTeamName, 7, " ", out error), "Correction accepted an empty reason.");
            window.ShowActivated = false;
            window.ShowInTaskbar = false;
            window.WindowState = WindowState.Normal;
            window.Show();
            ApplyReviewDialog(window, "7", "Judge accepted an appeal");
            Equal(7, cards[0].ScoreValue);
            using (var connection = OpenDatabase())
            {
                Execute(connection, "CREATE TRIGGER RejectCorrection BEFORE INSERT ON QuizEventLogs WHEN NEW.EventType='ScoreCorrected' BEGIN SELECT RAISE(ABORT, 'Test correction failure'); END");
                try
                {
                    Require(!window.TryCorrectScore(1, cards[0].DatabaseTeamName, 9, "Correction should roll back", out error), "Correction unexpectedly committed.");
                    Equal(7, cards[0].ScoreValue);
                    Equal(7L, QueryNumber(connection, "SELECT ScoreValue FROM ScoreCounts WHERE Team='JBA 1'"));
                }
                finally { Execute(connection, "DROP TRIGGER RejectCorrection"); }
            }
            Require(window.TryCorrectScore(1, cards[0].DatabaseTeamName, 5, "Judge reviewed the evidence again", out error), error);
            Click(Find<Button>(window, "FinishRoundButton"));
            Equal(Visibility.Visible, Find<Button>(window, "TieBreakButton").Visibility);
            using (var connection = OpenDatabase())
            {
                Execute(connection, "CREATE TRIGGER RejectTieBreak BEFORE INSERT ON QuizEventLogs WHEN NEW.EventType='TieBreakStarted' BEGIN SELECT RAISE(ABORT, 'Test tie-break failure'); END");
                try
                {
                    Require(!window.TryStartTieBreak(out error), "Tie-break failure did not roll back.");
                    Equal(1L, QueryNumber(connection, "SELECT CurrentRound FROM QuizSessions WHERE IsActive=1"));
                    Equal(5, cards[0].ScoreValue);
                }
                finally { Execute(connection, "DROP TRIGGER RejectTieBreak"); }
            }
            Require(window.TryStartTieBreak(out error), error);
            Equal("TIE-BREAK 1", Find<TextBlock>(window, "RoundNumberLabel").Text);
            Equal(false, ScoreButton(cards[2], "+5").IsEnabled);
            window.Close();
            window = new MainWindow();
            cards = Cards(window);
            Equal("TIE-BREAK 1", Find<TextBlock>(window, "RoundNumberLabel").Text);
            Equal(false, ScoreButton(cards[2], "+5").IsEnabled);
            Click(Find<Button>(window, "ResumeTimerButton"));
            Click(Find<Button>(window, "FinishRoundButton"));
            Require(window.TryStartTieBreak(out error), error);
            Equal("TIE-BREAK 2", Find<TextBlock>(window, "RoundNumberLabel").Text);
            Click(Find<Button>(window, "ResumeTimerButton"));
            Click(Find<Button>(cards[0], "PenaltyButton"));
            Click(Find<Button>(window, "FinishRoundButton"));
            Click(Find<Button>(window, "WinnerActionButton"));
            Equal(cards[1].TeamName, Find<TextBlock>(window, "WinnerTitle").Text);
            Equal(1, window.GetTournamentRank(1, false));
            Equal(2, window.GetTournamentRank(0, false));
            Equal(5L, window.GetProjectorTeams()[0].Total);
            Equal(2, window.GetProjectorTeams()[0].Rank);
            Equal(false, window.GetProjectorTeams()[2].Eligible);
            Require(Find<TextBlock>(cards[0], "TotalScoreLabel").Text.Contains("5"), "Tie-break changed main competition total.");
            window.WriteAuditExport(export);
            Require(File.ReadAllText(export).Contains("Tie-break"), "Export does not identify tie-break scores.");
            Require(window.TryCorrectScore(1, cards[0].DatabaseTeamName, 8, "Late appeal changes the main result", out error), error);
            Equal(cards[0].TeamName, Find<TextBlock>(window, "WinnerTitle").Text);
            Equal("ROUND 1 OF 1", Find<TextBlock>(window, "RoundNumberLabel").Text);
            using (var connection = OpenDatabase())
            {
                Equal(0L, QueryNumber(connection, "SELECT COUNT(*) FROM QuizRoundScores WHERE IsFinalized=1 AND RoundNumber>1 AND QuizSessionId=(SELECT QuizSessionId FROM QuizSessions WHERE IsActive=1)"));
                Equal(1L, QueryNumber(connection, "SELECT COUNT(*) FROM QuizEventLogs WHERE EventType='TieBreaksVoided'"));
            }
            window.Close();
            window = new MainWindow();
            Click(Find<Button>(window, "WinnerActionButton"));
            Equal(Cards(window)[0].TeamName, Find<TextBlock>(window, "WinnerTitle").Text);
            Console.WriteLine("PASS  Audited corrections, transactional failures, tie-break eligibility and late appeals");

            window.ShowActivated = false;
            window.ShowInTaskbar = false;
            window.WindowState = WindowState.Normal;
            window.Width = 1366;
            window.Height = 768;
            window.Show();
            SelectNewGameOption(window, Find<Button>(window, "FinalNewGameButton"), "StartBlankGame");
            Equal(false, Find<CheckBox>(window, "QuestionLockCheckBox").IsChecked);
            Require(Find<TextBlock>(window, "QuestionSummary").Text.StartsWith("QUESTION 1"), "New game retained question progress.");
            Find<Expander>(window, "QuestionPanel").IsExpanded = true;
            window.UpdateLayout();
            RequireFits(window, Find<Button>(window, "ReviewScoresButton"));
            RequireFits(window, Find<Button>(window, "QuestionNextButton"));
            Capture(window, "upgrade-controls.png");
            window.Width = window.MinWidth;
            window.Height = window.MinHeight;
            window.UpdateLayout();
            RequireFits(window, Find<Button>(window, "ReviewScoresButton"));
            RequireFits(window, Find<Button>(window, "QuestionNextButton"));
            Capture(window, "upgrade-minimum.png");
            Console.WriteLine("PASS  New Game clears upgrade state and controls fit at minimum width");
        }

        private static void VerifyQuizCreationAndBuzzerSystem()
        {
            // 1. Verify QuizDeckBuilderWindow sizing & responsive top bar
            var builder = new QuizDeckBuilderWindow();
            Equal(920d, builder.MinWidth);
            Equal(520d, builder.MinHeight);
            Equal(1280d, builder.Width);
            Equal(740d, builder.Height);
            builder.ShowActivated = false;
            builder.ShowInTaskbar = false;
            builder.Show();
            builder.Width = builder.MinWidth;
            builder.Height = builder.MinHeight;
            builder.UpdateLayout();
            RequireFits(builder, Find<Button>(builder, "SaveDeckButton"));
            RequireFits(builder, Find<Button>(builder, "NewDeckButton"));
            RequireFits(builder, Find<Button>(builder, "QuickAiQuizButton"));
            RequireFits(builder, Find<Button>(builder, "SmartPasteButton"));
            RequireFits(builder, Find<Button>(builder, "ImportCsvButton"));
            var validationBanner = Find<Border>(builder, "ValidationAlertBanner");
            var instructionBanner = Find<Border>(builder, "InstructionNoticeBanner");
            var dismissButton = Find<Button>(builder, "DismissAlertButton");
            var deckLockedBanner = Find<Border>(builder, "DeckLockedBanner");
            var unlockDeckButton = Find<Button>(builder, "UnlockDeckButton");
            Require(validationBanner != null, "ValidationAlertBanner must exist.");
            Require(instructionBanner != null, "InstructionNoticeBanner must exist.");
            Require(dismissButton != null, "DismissAlertButton must exist.");
            Require(deckLockedBanner != null, "DeckLockedBanner must exist.");
            Require(unlockDeckButton != null, "UnlockDeckButton must exist.");

            // Deep verification: Validation alert behavior on missing prompt
            Equal(Visibility.Collapsed, validationBanner.Visibility);
            var saveSlideBtn = Find<Button>(builder, "SaveSlideButton");
            var promptEditor = Find<TextBox>(builder, "SlideContentEditor");
            var previousPrompt = promptEditor.Text;
            promptEditor.Text = string.Empty;
            Click(saveSlideBtn);
            Equal(Visibility.Visible, validationBanner.Visibility);

            // Test dismiss button
            Click(dismissButton);
            Equal(Visibility.Collapsed, validationBanner.Visibility);

            // Restore prompt
            promptEditor.Text = previousPrompt;

            // Deep verification: Slide Type Discrimination & Dynamic Controls
            var slideTypeSelector = Find<ComboBox>(builder, "SlideTypeSelector");
            var card2 = Find<Border>(builder, "Card2AnswerKeys");
            var card3 = Find<Border>(builder, "Card3ScoringRules");

            // Toggle to Instruction
            slideTypeSelector.SelectedItem = "Instruction";
            Equal(Visibility.Collapsed, card2.Visibility);
            Equal(Visibility.Collapsed, card3.Visibility);
            Equal(Visibility.Visible, instructionBanner.Visibility);
            Equal("✓ Save Instruction", saveSlideBtn.Content);

            // Toggle to Timer
            slideTypeSelector.SelectedItem = "Timer";
            Equal(Visibility.Collapsed, card2.Visibility);
            Equal(Visibility.Visible, card3.Visibility);
            Equal(Visibility.Collapsed, instructionBanner.Visibility);
            Equal("✓ Save Timer", saveSlideBtn.Content);

            // Toggle to Media
            slideTypeSelector.SelectedItem = "Media";
            Equal(Visibility.Collapsed, card2.Visibility);
            Equal(Visibility.Collapsed, card3.Visibility);
            Equal("✓ Save Media Slide", saveSlideBtn.Content);

            // Toggle to Scoreboard
            slideTypeSelector.SelectedItem = "Scoreboard";
            Equal(Visibility.Collapsed, card2.Visibility);
            Equal(Visibility.Collapsed, card3.Visibility);
            Equal("✓ Save Scoreboard", saveSlideBtn.Content);

            // Toggle back to Question
            slideTypeSelector.SelectedItem = "Question";
            Equal(Visibility.Visible, card2.Visibility);
            Equal(Visibility.Visible, card3.Visibility);
            Equal(Visibility.Collapsed, instructionBanner.Visibility);
            Equal("✓ Save Question", saveSlideBtn.Content);

            // Verify Option Card click-to-select
            var optBCard = Find<Border>(builder, "OptionBCard");
            var optBCheck = Find<RadioButton>(builder, "OptionBCheck");
            optBCard.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(
                System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
            {
                RoutedEvent = UIElement.PreviewMouseLeftButtonDownEvent,
                Source = optBCard
            });
            Equal(true, optBCheck.IsChecked);

            builder.Close();

            // Verify Quick Topic and Smart Paste Dialogs
            var quickTopicDlg = new QuizQuickTopicDialog();
            quickTopicDlg.Close();

            var smartPasteDlg = new QuizSmartPasteDialog(1, 1);
            smartPasteDlg.Close();

            // 2. Verify QuizChimes soundpack
            QuizChimes.IsMuted = false;
            QuizChimes.PlayCorrect();
            QuizChimes.PlayIncorrect();
            QuizChimes.PlayBuzzer();
            QuizChimes.PlayTickWarning();
            QuizChimes.PlayBounce();
            QuizChimes.Stop();
            QuizChimes.IsMuted = true;
            QuizChimes.PlayBuzzer(); // Muted, no exception
            QuizChimes.IsMuted = false;

            // 3. Verify QuizBuzzerServer & QR Code Generation
            // QR Code Matrix ISO 18004 validation
            var testMatrix = QuizBuzzerServer.GenerateQrMatrix("http://192.168.1.50:8088/");
            Equal(29, testMatrix.GetLength(0));
            // Format Info Copy 1: (8, 0) is f14 of 0x5412 (1)
            Equal(true, testMatrix[8, 0]);
            // Format Info Copy 2: (28, 8) is f0 of 0x5412 (0)
            Equal(false, testMatrix[28, 8]);
            // Dark module at (size - 8, 8) = (21, 8) must be true
            Equal(true, testMatrix[21, 8]);
            // Timing patterns
            Equal(true, testMatrix[6, 8]);
            Equal(true, testMatrix[8, 6]);

            using (var server = new QuizBuzzerServer(8099))
            {
                server.GetTeamsCallback = () => new[] { "Team Falcon", "Team Eagle" };
                string receivedTeam = null;
                server.OnBuzzReceived += team => receivedTeam = team;
                server.Start();
                Equal(true, server.IsRunning);
                Require(server.LocalUrl.StartsWith("http://"), "LocalUrl must be an HTTP URL.");
                Require(server.LocalUrl.Contains(":8099"), "LocalUrl must contain port 8099.");

                // QR Code generation
                var qrBitmap = QuizBuzzerServer.GenerateQrBitmap(server.LocalUrl, 6, 4);
                Require(qrBitmap != null && qrBitmap.PixelWidth > 100 && qrBitmap.PixelHeight > 100, "QR Code bitmap was not generated.");

                // HTTP GET /
                using (var client = new System.Net.WebClient())
                {
                    client.Encoding = System.Text.Encoding.UTF8;
                    var html = client.DownloadString("http://localhost:8099/");
                    Require(html.Contains("QUIZSCORE LIVE"), "Mobile buzzer HTML must contain QUIZSCORE LIVE.");
                    Require(html.Contains("BUZZ IN!"), "Mobile buzzer HTML must contain the BUZZ IN button.");
                    Require(html.Contains("wakeLock"), "Mobile buzzer HTML must include Screen Wake Lock API.");
                    Require(html.Contains("quiz_team"), "Mobile buzzer HTML must persist team in localStorage.");
                    Require(html.Contains("80, 40, 80"), "Mobile buzzer HTML must include [80, 40, 80] vibration pattern.");

                    // HTTP GET /api/teams and /api/teams/
                    var teamsJson = client.DownloadString("http://localhost:8099/api/teams");
                    Require(teamsJson.Contains("Team Falcon"), "Teams API must return configured teams.");
                    var teamsJsonSlash = client.DownloadString("http://localhost:8099/api/teams/");
                    Require(teamsJsonSlash.Contains("Team Falcon"), "Teams API with trailing slash must return configured teams.");

                    // HTTP POST /api/buzz with JSON
                    client.Headers[System.Net.HttpRequestHeader.ContentType] = "application/json";
                    var buzzResponse = client.UploadString("http://localhost:8099/api/buzz", "{\"team\":\"Team Falcon\"}");
                    Require(buzzResponse.Contains("\"status\":\"ok\""), "Buzz API response must indicate success.");
                    Require(buzzResponse.Contains("\"timestamp\":"), "Buzz response must include high-precision timestamp.");
                    Require(server.LastBuzzStopwatchTicks > 0, "LastBuzzStopwatchTicks must be recorded.");
                    Require(server.LastBuzzUtcTicks > 0, "LastBuzzUtcTicks must be recorded.");
                    Equal("Team Falcon", receivedTeam);

                    // Test Arming / Lockout flag: HTTP 423 Locked when disarmed
                    server.IsBuzzerArmed = false;
                    try
                    {
                        client.Headers[System.Net.HttpRequestHeader.ContentType] = "application/json";
                        client.UploadString("http://localhost:8099/api/buzz", "{\"team\":\"Team Falcon\"}");
                        Require(false, "Buzz must fail when disarmed.");
                    }
                    catch (System.Net.WebException ex)
                    {
                        var httpRes = ex.Response as System.Net.HttpWebResponse;
                        Require(httpRes != null && (int)httpRes.StatusCode == 423, "Disarmed buzzer must return HTTP 423 Locked.");
                    }
                    server.IsBuzzerArmed = true;

                    // HTTP POST /api/buzz/ with trailing slash and form data
                    client.Headers[System.Net.HttpRequestHeader.ContentType] = "application/x-www-form-urlencoded";
                    var formBuzzResponse = client.UploadString("http://localhost:8099/api/buzz/", "team=Team+Eagle");
                    Require(formBuzzResponse.Contains("\"status\":\"ok\""), "Form buzz response must indicate success.");
                    Equal("Team Eagle", receivedTeam);

                    // HTTP GET /api/buzz?team=...
                    var getBuzzResponse = client.DownloadString("http://localhost:8099/api/buzz?team=Team+Hawk");
                    Require(getBuzzResponse.Contains("\"status\":\"ok\""), "GET buzz query response must indicate success.");
                    Equal("Team Hawk", receivedTeam);
                }

                server.Stop();
                Equal(false, server.IsRunning);
            }

            // Port conflict fallback test
            var dummyListener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Any, 8094);
            try
            {
                dummyListener.Start();
                using (var fallbackServer = new QuizBuzzerServer(8094))
                {
                    fallbackServer.Start();
                    Equal(true, fallbackServer.IsRunning);
                    Equal(8095, fallbackServer.Port);
                    fallbackServer.Stop();
                }
            }
            finally
            {
                dummyListener.Stop();
            }

            // 4. Verify QuizMasterConsoleWindow toolbar buttons
            var consoleWindow = new QuizMasterConsoleWindow();
            var soundButton = Find<Button>(consoleWindow, "SoundToggleButton");
            var mobileButton = Find<Button>(consoleWindow, "MobileBuzzerButton");
            Require(soundButton != null, "SoundToggleButton must exist.");
            Require(mobileButton != null, "MobileBuzzerButton must exist.");

            // Test sound toggle
            QuizChimes.IsMuted = false;
            Click(soundButton);
            Equal(true, QuizChimes.IsMuted);
            Equal("🔇 MUTE", soundButton.Content);
            Click(soundButton);
            Equal(false, QuizChimes.IsMuted);
            Equal("🔊 SOUND ON", soundButton.Content);

            VerifyHostConducting(consoleWindow);

            // Test Host Focus Mode & Panel Collapse
            consoleWindow.ToggleLeftPanel();
            consoleWindow.ToggleRightPanel();
            consoleWindow.ToggleFocusMode();
            consoleWindow.ApplyTheme("Emerald Arena");
            Equal("Emerald Arena", QuizThemePalette.GetPalette("Emerald").Name);
            Equal("Royal Velvet", QuizThemePalette.GetPalette("Neon").Name);
            Equal("Daylight Contrast", QuizThemePalette.GetPalette("Light").Name);

            consoleWindow.Close();

            var targetedConsole = new QuizMasterConsoleWindow(1);
            targetedConsole.Close();

            var libraryWindow = new QuizLibraryWindow();
            var searchBox = Find<TextBox>(libraryWindow, "SearchBox");
            var sortSelector = Find<ComboBox>(libraryWindow, "SortSelector");
            var createButton = Find<Button>(libraryWindow, "CreateNewButton");
            Require(searchBox != null, "SearchBox must exist in QuizLibraryWindow.");
            Require(sortSelector != null, "SortSelector must exist in QuizLibraryWindow.");
            Require(createButton != null, "CreateNewButton must exist in QuizLibraryWindow.");
            libraryWindow.Close();

            var historyDialog = new QuizSessionHistoryDialog();
            historyDialog.Close();

            Equal(10, QuizSampleDeckSeeder.CreateSampleSlides(1).Count);

            var audienceWindow = new QuizAudienceWindow();
            VerifyAudienceWindow(audienceWindow);
            audienceWindow.ApplyTheme("Emerald Arena");
            audienceWindow.AnimateSlideTransition();
            audienceWindow.StartTimerPulse();
            audienceWindow.StopTimerPulse();
            audienceWindow.Close();
        }

    }
}
