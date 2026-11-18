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

        internal static void VerifyAudienceWindow(QuizAudienceWindow window)
        {
            Require(window.WindowStyle == WindowStyle.None, "WindowStyle must be None for borderless stage presentation.");
            Require(window.ResizeMode == ResizeMode.NoResize, "ResizeMode must be NoResize for stage presentation.");

            var blackout = Find<Grid>(window, "BlackoutOverlay");
            Require(blackout != null, "BlackoutOverlay Grid must exist.");
            Equal(Visibility.Collapsed, blackout.Visibility);

            window.ToggleBlackout();
            Equal(Visibility.Visible, blackout.Visibility);
            window.ToggleBlackout(false);
            Equal(Visibility.Collapsed, blackout.Visibility);
            window.ToggleBlackout(true);
            Equal(Visibility.Visible, blackout.Visibility);
            window.ToggleBlackout(false);

            var buzzBorder = Find<Border>(window, "AudienceBuzzBorder");
            var buzzIndicator = Find<TextBlock>(window, "AudienceBuzzIndicator");
            Require(buzzBorder != null, "AudienceBuzzBorder must exist.");
            Equal(Visibility.Collapsed, buzzBorder.Visibility);

            window.PushBuzzAlert("Team Gamma", 2);
            Equal(Visibility.Visible, buzzBorder.Visibility);
            Require(buzzIndicator.Text.Contains("TEAM GAMMA"), "Buzz indicator text must contain team name.");
            Require(buzzIndicator.Text.Contains("#2"), "Buzz indicator must show buzz order.");

            window.DismissBuzzAlert();
            Equal(Visibility.Collapsed, buzzBorder.Visibility);

            var slide = new DeckSlide
            {
                SlideType = "Question",
                QuestionType = QuizScoringEngine.QuestionMultipleChoice,
                Title = "Auditorium Stage Test",
                ContentText = "What is the capital of France?",
                OptionsJson = "London\nParis\nBerlin\nRome",
                CorrectAnswer = "Paris",
                Explanation = "Secret host notes about Paris: do NOT show to audience!",
                Points = 20,
                TimeLimitSeconds = 45,
                HintsJson = "Hint 1: European city\nHint 2: Eiffel tower"
            };

            var session = new QuizDeckSession
            {
                QuizDeckSessionId = 999,
                QuizDeckId = 1,
                CurrentSlideIndex = 0,
                Status = "Running"
            };

            // Before reveal
            window.PushSlideState(slide, session, false, "Team Alpha", 1);
            var answerContainer = Find<Border>(window, "AudienceAnswerContainer");
            Equal(Visibility.Collapsed, answerContainer.Visibility);
            var cluesPanel = Find<Border>(window, "AudienceCluesPanel");
            Equal(Visibility.Visible, cluesPanel.Visibility);

            var options = Find<ItemsControl>(window, "AudienceOptions");
            var itemsBefore = options.ItemsSource as System.Collections.Generic.List<AudienceOptionItem>;
            Require(itemsBefore != null && itemsBefore.Count == 4, "Must parse 4 options.");
            Require(itemsBefore[1].CheckmarkVisibility == Visibility.Collapsed, "Option checkmark must not be visible before reveal.");

            // After reveal
            window.PushSlideState(slide, session, true, "Team Alpha", 1);
            Equal(Visibility.Visible, answerContainer.Visibility);
            var answerText = Find<TextBlock>(window, "AudienceAnswerText");
            Equal("Paris", answerText.Text);

            var itemsAfter = options.ItemsSource as System.Collections.Generic.List<AudienceOptionItem>;
            Require(itemsAfter != null && itemsAfter.Count == 4, "Must have 4 options.");
            Equal(Visibility.Visible, itemsAfter[1].CheckmarkVisibility);
            Equal("✔ CORRECT", itemsAfter[1].Checkmark);
            Equal(FontWeights.Bold, itemsAfter[1].FontWeight);
            Equal(Visibility.Collapsed, itemsAfter[0].CheckmarkVisibility);

            var teams = new System.Collections.Generic.List<QuizDeckSessionTeam>
            {
                new QuizDeckSessionTeam { TeamName = "Team Alpha" },
                new QuizDeckSessionTeam { TeamName = "Team Beta" }
            };
            var scores = new System.Collections.Generic.Dictionary<string, int>
            {
                { "Team Alpha", 50 },
                { "Team Beta", 30 }
            };
            window.PushScoreboard(teams, scores);
            var scoreboard = Find<ItemsControl>(window, "AudienceScoreboard");
            Require(scoreboard.ItemsSource != null, "Scoreboard ItemsSource must be populated.");
        }

        internal static void VerifyHostConducting(QuizMasterConsoleWindow window)
        {
            var openProjector = Find<Button>(window, "OpenProjectorButton");
            Require(openProjector != null, "OpenProjectorButton must exist.");

            var nextBtn = Find<Button>(window, "NextSlideButton");
            var prevBtn = Find<Button>(window, "PrevSlideButton");
            var revealBtn = Find<Button>(window, "RevealAnswerButton");
            var correctBtn = Find<Button>(window, "MarkCorrectButton");
            var incorrectBtn = Find<Button>(window, "MarkIncorrectButton");
            var acceptBuzzBtn = Find<Button>(window, "AcceptBuzzButton");
            Require(nextBtn != null, "NextSlideButton must exist.");
            Require(prevBtn != null, "PrevSlideButton must exist.");
            Require(revealBtn != null, "RevealAnswerButton must exist.");
            Require(correctBtn != null, "MarkCorrectButton must exist.");
            Require(incorrectBtn != null, "MarkIncorrectButton must exist.");
            Require(acceptBuzzBtn != null, "AcceptBuzzButton must exist.");

            // Test projector button creates audience window singleton
            Click(openProjector);
            var aud1 = Application.Current.Windows.OfType<QuizAudienceWindow>().FirstOrDefault();
            Require(aud1 != null, "Audience window must be opened.");
            Click(openProjector); // Should bring to front, not create duplicate
            var audCount = Application.Current.Windows.OfType<QuizAudienceWindow>().Count();
            Equal(1, audCount);
            aud1.Close();
        }
    }
}
