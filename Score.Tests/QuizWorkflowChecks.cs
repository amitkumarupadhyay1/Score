using System;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;

namespace Score.Tests
{
    internal static class QuizWorkflowChecks
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

        private static Button ScoreButton(TeamScoreCard card, string text)
        {
            return Find<StackPanel>(card, "ScoreActionsPanel").Children.OfType<Button>().Single(button => Equals(button.Content, text));
        }

        private static void ApplyReviewDialog(MainWindow window, string score, string reason)
        {
            Exception failure = null;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            timer.Tick += (sender, args) =>
            {
                timer.Stop();
                var dialog = Application.Current.Windows.OfType<Window>().FirstOrDefault(candidate => candidate.Owner == window);
                try
                {
                    Require(dialog != null, "Review Scores did not open a dialog.");
                    var controls = VisualDescendants(dialog).OfType<FrameworkElement>().ToArray();
                    ((TextBox)controls.Single(control => control.Name == "CorrectionScore")).Text = score;
                    ((TextBox)controls.Single(control => control.Name == "CorrectionReason")).Text = reason;
                    Click((Button)controls.Single(control => control.Name == "ApplyCorrection"));
                    Require(!dialog.IsVisible, "Saving a valid correction did not close the review dialog.");
                }
                catch (Exception ex) { failure = ex; if (dialog != null) dialog.Close(); }
            };
            timer.Start();
            Click(Find<Button>(window, "ReviewScoresButton"));
            timer.Stop();
            if (failure != null) throw failure;
        }

        private static void Capture(MainWindow window, string fileName)
        {
            var frame = new DispatcherFrame();
            var renderWait = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(600) };
            renderWait.Tick += (sender, args) => { renderWait.Stop(); frame.Continue = false; };
            renderWait.Start();
            Dispatcher.PushFrame(frame);
            window.Dispatcher.Invoke(DispatcherPriority.ApplicationIdle, new Action(() => { }));
            window.UpdateLayout();
            var card = Cards(window)[0];
            var score = Find<TextBlock>(card, "ScoreLabel");
            Equal("0", score.Text);
            Equal(80d, score.FontSize);
            var bounds = score.TransformToAncestor(card).TransformBounds(new Rect(score.RenderSize));
            Require(bounds.Top >= 0 && bounds.Bottom <= card.ActualHeight && bounds.Left >= 0 && bounds.Right <= card.ActualWidth, "Large score text does not fit its card.");
            var root = Find<Grid>(window, "RootLayout");
            var bitmap = new System.Windows.Media.Imaging.RenderTargetBitmap((int)root.ActualWidth, (int)root.ActualHeight, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
            using (var file = File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, fileName))) encoder.Save(file);
        }

        private static SQLiteConnection OpenDatabase()
        {
            var connection = new SQLiteConnection("Data Source=" + AppPaths.DatabasePath + ";Pooling=False;");
            connection.Open();
            return connection;
        }

        private static void Execute(SQLiteConnection connection, string sql)
        {
            using (var command = new SQLiteCommand(sql, connection)) command.ExecuteNonQuery();
        }

        private static long QueryNumber(SQLiteConnection connection, string sql)
        {
            using (var command = new SQLiteCommand(sql, connection)) return Convert.ToInt64(command.ExecuteScalar());
        }

        private static void SelectNewGameOption(MainWindow window, Button button, string option)
        {
            Exception failure = null;
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
            timer.Tick += (sender, args) =>
            {
                timer.Stop();
                var dialog = Application.Current.Windows.OfType<Window>().FirstOrDefault(candidate => candidate.Owner == window);
                var choice = dialog == null ? null : VisualDescendants(dialog).OfType<Button>().FirstOrDefault(candidate => candidate.Name == option);
                if (choice != null) Click(choice);
                else
                {
                    failure = new InvalidOperationException("Could not select " + option + " in the New Game dialog.");
                    if (dialog != null) dialog.Close();
                }
            };
            timer.Start();
            Click(button);
            timer.Stop();
            if (failure != null) throw failure;
        }

        private static TeamScoreCard[] Cards(MainWindow window)
        {
            return Find<Grid>(window, "TeamCardsGrid").Children.OfType<TeamScoreCard>().ToArray();
        }

        private static void AwardFive(TeamScoreCard card)
        {
            Click(Find<StackPanel>(card, "ScoreActionsPanel").Children.OfType<Button>().Single(button => Equals(button.Content, "+5")));
        }

        private static System.Collections.Generic.IEnumerable<DependencyObject> VisualDescendants(DependencyObject parent)
        {
            for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            {
                var child = VisualTreeHelper.GetChild(parent, index);
                yield return child;
                foreach (var descendant in VisualDescendants(child)) yield return descendant;
            }
        }

        private static string ResultCell(Panel results, int row, int column)
        {
            return ((Grid)((Border)results.Children[row]).Child).Children.OfType<TextBlock>().Single(cell => Grid.GetColumn(cell) == column).Text;
        }

        private static T Find<T>(FrameworkElement element, string name) where T : class
        {
            return (T)element.FindName(name);
        }

        private static void Click(Button button)
        {
            Require(button.IsEnabled, "Button is disabled: " + button.Content);
            button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        }

        private static void Equal(object expected, object actual)
        {
            Require(Equals(expected, actual), "Expected " + expected + " but found " + actual + ".");
        }

        private static void RequireFits(Window window, FrameworkElement control)
        {
            window.UpdateLayout();
            var bounds = control.TransformToAncestor(window).TransformBounds(new Rect(control.RenderSize));
            Require(control.IsVisible && bounds.Left >= 0 && bounds.Right <= window.ActualWidth && bounds.Top >= 0 && bounds.Bottom <= window.ActualHeight, control.Name + " is clipped at the minimum window size.");
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }
}
