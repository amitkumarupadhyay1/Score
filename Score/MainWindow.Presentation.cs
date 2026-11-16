using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace Score
{
    public partial class MainWindow : Window
    {
        private void ProjectorButton_Click(object sender, RoutedEventArgs e)
        {
            ShowProjector(ProjectorView.Live);
        }

        private void ProjectorStandingsButton_Click(object sender, RoutedEventArgs e)
        {
            ShowProjector(ProjectorView.Standings);
        }

        private void ShowProjector(ProjectorView view)
        {
            if (projector != null)
            {
                if (projector.WindowState == WindowState.Minimized) projector.WindowState = WindowState.Normal;
                projector.SetView(view);
                RefreshProjector();
                return;
            }
            projector = new ProjectorWindow(EventTitleEditor, RoundNumberLabel, RoundTimerLabel, QuestionSummary, CompetitionStatusLabel);
            projector.SetTheme(projectorTheme);
            projector.ThemeChanged += theme => SetProjectorTheme(theme, true);
            projector.SetView(view);
            projector.Closed += (closedSender, closedArgs) => projector = null;
            RefreshProjector();
            projector.ShowOnAudienceScreen(this);
        }

        private void RefreshProjector()
        {
            if (projector != null) projector.SetTeams(GetProjectorTeams(), activeSession.QuizSessionId + "/" + currentRound,
                IsTieBreak ? "MAIN TOTALS · TIE-BREAK ORDER" : roundFinalized ? "RECORDED TOTALS" : "LIVE TOTALS");
        }

        internal ProjectorTeam[] GetProjectorTeams()
        {
            return Cards.Select((card, index) => new ProjectorTeam { Key = card.DatabaseTeamName, Name = card.TeamName, RoundScore = card.ScoreValue,
                Total = totalScores[index] + (!roundFinalized && !IsTieBreak ? card.ScoreValue : 0), Rank = GetTournamentRank(index, true), Eligible = IsTeamEligible(card) }).ToArray();
        }

        private void ProjectorThemeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (activeSession == null || synchronizingProjectorTheme || ProjectorThemeSelector.SelectedIndex < 0) return;
            SetProjectorTheme((ProjectorTheme)ProjectorThemeSelector.SelectedIndex, true);
        }

        private void SetProjectorTheme(ProjectorTheme theme, bool save)
        {
            if (synchronizingProjectorTheme) return;
            synchronizingProjectorTheme = true;
            try
            {
                projectorTheme = theme;
                ProjectorThemeSelector.SelectedIndex = (int)theme;
                if (projector != null) projector.SetTheme(theme);
                if (save) TrySavePreference(() => SafeFile.WriteAllText(Path.Combine(AppPaths.SettingsDirectory, "projector-theme.txt"), theme.ToString()), "projector theme");
            }
            finally { synchronizingProjectorTheme = false; }
        }


        private void ShowRoundAnnouncement(TeamScoreCard[] cards, bool isRecovery = false)
        {
            isFinalRoundComplete = currentRound >= configuredRounds;
            isFinalAnnouncement = false;
            isPreviewAnnouncement = false;
            RoundSaveError.Visibility = Visibility.Collapsed;
            var eligible = cards.Where(IsTeamEligible).ToArray();
            var highest = eligible.Max(card => card.ScoreValue);
            var winners = eligible.Where(card => card.ScoreValue == highest).ToArray();
            WinnerContext.Text = isFinalRoundComplete ? "FINAL ROUND COMPLETE" : "ROUND " + currentRound + " RESULT";
            var allZero = cards.All(card => card.ScoreValue == 0);
            WinnerTitle.Text = allZero ? "NO SCORE" : winners.Length == 1 ? winners[0].TeamName : "TIE";
            WinnerScore.Text = allZero || winners.Length == 1 ? highest + " POINTS" : string.Join("  /  ", winners.Select(card => card.TeamName)) + "\n" + highest + " POINTS";
            PopulateRoundResults(cards, false);
            RoundResultsPanel.Visibility = Visibility.Visible;
            FinalStandings.Visibility = Visibility.Collapsed;
            FinalGameActions.Visibility = isFinalRoundComplete ? Visibility.Visible : Visibility.Collapsed;
            UpdateTieBreakButton();
            WinnerActionButton.Content = isFinalRoundComplete ? "ANNOUNCE FINAL RESULT" : "NEXT ROUND";
            if (isFinalRoundComplete)
            {
                globalTimer.Stop();
                timerRefresh.Stop();
                UpdateTimerControls();
            }
            WinnerOverlay.Visibility = Visibility.Visible;
            foreach (var card in cards) card.SetScoringEnabled(false);
            if (SystemParameters.ClientAreaAnimation && !isRecovery)
                WinnerOverlay.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(260)));
            UpdateCompetitionStatus();
        }

        private void PopulateRoundResults(TeamScoreCard[] cards, bool includeCurrentRound)
        {
            RoundResultsRows.Children.Clear();
            var standings = Enumerable.Range(0, cards.Length)
                .Where(index => IsTeamEligible(cards[index]))
                .OrderByDescending(index => cards[index].ScoreValue)
                .ToArray();
            for (var position = 0; position < standings.Length; position++)
            {
                var index = standings[position];
                var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(120) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                var rank = 1 + cards.Count(card => IsTeamEligible(card) && card.ScoreValue > cards[index].ScoreValue);
                var surface = new Border { Padding = new Thickness(12, 9, 12, 9), Background = Brushes.White, BorderBrush = cards[index].ScoreColor, BorderThickness = new Thickness(rank == 1 ? 2 : 1), CornerRadius = new CornerRadius(8), Child = row };
                AddResultCell(row, rank.ToString(), 0, Brushes.DarkSlateGray, true, TextAlignment.Left);
                AddResultCell(row, cards[index].TeamName, 1, System.Windows.Media.Brushes.DarkSlateGray, true, TextAlignment.Left);
                AddResultCell(row, (cards[index].ScoreValue >= 0 ? "+" : string.Empty) + cards[index].ScoreValue + " pts", 2, cards[index].ScoreColor, true, TextAlignment.Right);
                var displayedTotal = totalScores[index] + (includeCurrentRound && !IsTieBreak ? cards[index].ScoreValue : 0);
                AddResultCell(row, displayedTotal + " pts", 3, System.Windows.Media.Brushes.DarkGreen, true, TextAlignment.Right);
                AddResultCell(row, FormatElapsed(SafeSeconds(currentRoundTeamElapsed[index])), 4, System.Windows.Media.Brushes.DarkGreen, false, TextAlignment.Right);
                RoundResultsRows.Children.Add(surface);
            }
        }

        private void AddResultCell(Grid row, string text, int column, System.Windows.Media.Brush foreground, bool bold, TextAlignment alignment)
        {
            var cell = new TextBlock { Text = text, Foreground = foreground, FontSize = 14, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center, TextAlignment = alignment };
            Grid.SetColumn(cell, column);
            row.Children.Add(cell);
        }

        private void WinnerActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (isFinalAnnouncement)
            {
                WinnerOverlay.Visibility = Visibility.Collapsed;
                isFinalAnnouncement = false;
                UpdateCompetitionStatus();
                SetFeed("Final results remain recorded. You can review the scoreboard or export the audit log.");
                return;
            }
            if (isPreviewAnnouncement) { WinnerOverlay.Visibility = Visibility.Collapsed; isPreviewAnnouncement = false; return; }
            if (isFinalRoundComplete) { ShowFinalAnnouncement(); return; }
            if (!roundFinalized) return;
            var nextRound = currentRound + 1;
            var status = IsEventClockRunning() ? "Running" : "Paused";
            if (!SaveRoundAdvance(nextRound, status)) return;
            currentRound = nextRound;
            ResetQuestion();
            roundFinalized = false;
            roundOvertimeAnnounced = false;
            foreach (var card in Cards) card.ScoreValue = 0;
            ResetTeamTimer();
            Array.Clear(currentRoundTeamElapsed, 0, currentRoundTeamElapsed.Length);
            if (status == "Running") { roundTimer.Start(GetRoundDuration()); timerRefresh.Start(); }
            else roundTimer.Restore(GetRoundDuration(), TimeSpan.Zero, QuizTimerState.Paused);
            WinnerOverlay.Visibility = Visibility.Collapsed;
            foreach (var card in Cards) { card.SetScoringEnabled(true); card.SetEditingEnabled(true); }
            RoundNumberLabel.Text = "ROUND " + currentRound + " OF " + configuredRounds;
            SetFeed(CurrentRoundContext + " is ready. Award points using +1, +2, +5, or a penalty.");
            UpdateLeaderIndicators();
            UpdateTimerLabels();
            UpdateTimerControls();
            UpdateCompetitionStatus();
        }

        private void AnnounceWinnerButton_Click(object sender, RoutedEventArgs e)
        {
            RoundSaveError.Visibility = Visibility.Collapsed;
            if (roundFinalized)
            {
                ShowRoundAnnouncement(Cards, true);
                return;
            }
            var cards = Cards;
            var eligible = cards.Where(IsTeamEligible).ToArray();
            var highest = eligible.Max(card => card.ScoreValue);
            var winners = eligible.Where(card => card.ScoreValue == highest).ToArray();
            WinnerContext.Text = "LIVE ROUND RESULT";
            WinnerTitle.Text = cards.All(card => card.ScoreValue == 0) ? "NO SCORE" : winners.Length == 1 ? winners[0].TeamName : "TIE";
            WinnerScore.Text = highest + " ROUND POINTS";
            FinalStandings.Visibility = Visibility.Collapsed;
            PopulateRoundResults(cards, true);
            RoundResultsPanel.Visibility = Visibility.Visible;
            WinnerActionButton.Content = "CLOSE";
            FinalGameActions.Visibility = Visibility.Collapsed;
            isFinalAnnouncement = false;
            isPreviewAnnouncement = true;
            WinnerOverlay.Visibility = Visibility.Visible;
        }

        private void ShowStandingsButton_Click(object sender, RoutedEventArgs e)
        {
            RoundSaveError.Visibility = Visibility.Collapsed;
            var completedRounds = roundFinalized ? currentRound : currentRound - 1;
            WinnerContext.Text = "LIVE STANDINGS";
            WinnerTitle.Text = "SCOREBOARD";
            WinnerScore.Text = completedRounds > 0 ? "THROUGH ROUND " + completedRounds : "BEFORE ROUND 1";
            PopulateFinalStandings(Cards);
            FinalStandings.Visibility = Visibility.Visible;
            RoundResultsPanel.Visibility = Visibility.Collapsed;
            isFinalAnnouncement = false;
            FinalGameActions.Visibility = isFinalRoundComplete ? Visibility.Visible : Visibility.Collapsed;
            UpdateTieBreakButton();
            isPreviewAnnouncement = true;
            WinnerActionButton.Content = "CLOSE SCOREBOARD";
            WinnerOverlay.Visibility = Visibility.Visible;
        }

        private void ShowFinalAnnouncement()
        {
            var cards = Cards;
            var winners = cards.Where((card, index) => GetTournamentRank(index, false) == 1).ToArray();
            var highest = totalScores[GetTeamIndex(winners[0].DatabaseTeamName)];
            isFinalAnnouncement = true;
            isPreviewAnnouncement = false;
            FinalGameActions.Visibility = Visibility.Visible;
            UpdateTieBreakButton();
            WinnerContext.Text = "FINAL RESULT";
            WinnerTitle.Text = winners.Length == 1 ? winners[0].TeamName : "TIE";
            WinnerScore.Text = highest + " POINTS" + (IsTieBreak ? " · TIE-BREAK " + (currentRound - configuredRounds) : "  🏆");
            PopulateFinalStandings(cards);
            FinalStandings.Visibility = Visibility.Visible;
            RoundResultsPanel.Visibility = Visibility.Collapsed;
            WinnerActionButton.Content = "CLOSE RESULTS";
            UpdateCompetitionStatus();
            LogEvent("FinalWinnerAnnounced", string.Join(" / ", winners.Select(card => card.TeamName)), 0, SafeLogScore(highest), "Final result announced");
            for (var i = 0; i < cards.Length; i++) cards[i].SetFinalWinner(GetTournamentRank(i, false) == 1);
            AnimateFinalResults();
        }

        private void AnimateFinalResults()
        {
            if (!SystemParameters.ClientAreaAnimation) return;
            WinnerOverlay.Opacity = 0;
            var overlayFade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(420));
            WinnerOverlay.BeginAnimation(UIElement.OpacityProperty, overlayFade);
            AnimateReveal(WinnerContext, 0, 0, 1.0);
            AnimateReveal(WinnerTitle, 90, 22, 1.08);
            AnimateReveal(WinnerScore, 180, 18, 1.06);
            for (var i = 0; i < FinalStandingsRows.Children.Count; i++)
                AnimateReveal(FinalStandingsRows.Children[i] as UIElement, 300 + i * 110, 24, 1.0);
            AnimateReveal(WinnerActionButton, 760, 12, 1.0);
        }

        private void AnimateReveal(UIElement element, int delayMilliseconds, double offset, double scale)
        {
            if (element == null) return;
            element.Opacity = 0;
            var scaleTransform = new ScaleTransform(scale, scale);
            var translateTransform = new TranslateTransform(0, offset);
            element.RenderTransform = new TransformGroup
            {
                Children = new TransformCollection
                {
                    scaleTransform,
                    translateTransform
                }
            };
            var opacity = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(420)) { BeginTime = TimeSpan.FromMilliseconds(delayMilliseconds) };
            var movement = new DoubleAnimation(offset, 0, TimeSpan.FromMilliseconds(560)) { BeginTime = TimeSpan.FromMilliseconds(delayMilliseconds), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            var growX = new DoubleAnimation(scale, 1, TimeSpan.FromMilliseconds(560)) { BeginTime = TimeSpan.FromMilliseconds(delayMilliseconds), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            var growY = new DoubleAnimation(scale, 1, TimeSpan.FromMilliseconds(560)) { BeginTime = TimeSpan.FromMilliseconds(delayMilliseconds), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            element.BeginAnimation(UIElement.OpacityProperty, opacity);
            translateTransform.BeginAnimation(TranslateTransform.YProperty, movement);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, growX);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, growY);
        }

        private void PopulateFinalStandings(TeamScoreCard[] cards)
        {
            FinalStandingsRows.Children.Clear();
            var ranking = Enumerable.Range(0, cards.Length).OrderBy(index => GetTournamentRank(index, false)).ThenBy(index => index).ToArray();
            for (var position = 0; position < ranking.Length; position++)
            {
                var index = ranking[position];
                var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
                var rank = GetTournamentRank(index, false);
                var background = rank == 1 ? new SolidColorBrush(Color.FromRgb(255, 248, 225)) : Brushes.White;
                var surface = new Border { Padding = new Thickness(12, 10, 12, 10), Background = background, BorderBrush = cards[index].ScoreColor, BorderThickness = new Thickness(rank == 1 ? 2 : 1), CornerRadius = new CornerRadius(8), Child = row };
                AddFinalCell(row, rank.ToString(), 0, rank == 1 ? Brushes.DarkGoldenrod : Brushes.DarkSlateGray, true, TextAlignment.Left);
                AddFinalCell(row, cards[index].TeamName, 1, System.Windows.Media.Brushes.DarkSlateGray, true, TextAlignment.Left);
                AddFinalCell(row, totalScores[index] + " pts", 2, cards[index].ScoreColor, true, TextAlignment.Right);
                AddFinalCell(row, string.Join("   ", roundScores[index].Select((score, round) => RoundCaption(round + 1) + " " + (score >= 0 ? "+" : string.Empty) + score + " / " + FormatShortElapsed(round < roundTeamElapsed[index].Count ? roundTeamElapsed[index][round] : 0))), 3, System.Windows.Media.Brushes.SlateGray, false, TextAlignment.Right);
                AddFinalCell(row, FormatElapsed(totalTeamElapsed[index]), 4, System.Windows.Media.Brushes.DarkGreen, false, TextAlignment.Right);
                FinalStandingsRows.Children.Add(surface);
            }
        }

        private void AddFinalCell(Grid row, string text, int column, System.Windows.Media.Brush foreground, bool bold, TextAlignment alignment)
        {
            var cell = new TextBlock { Text = text, Foreground = foreground, FontSize = 13, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal, VerticalAlignment = VerticalAlignment.Center, TextAlignment = alignment, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetColumn(cell, column);
            row.Children.Add(cell);
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var options = new StackPanel { Margin = new Thickness(20) };
            options.Children.Add(new TextBlock
            {
                Text = "Both options clear scores and elapsed timers. The current game is archived locally. Export its results first if you need a CSV copy.",
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 420,
                Margin = new Thickness(0, 0, 0, 16)
            });
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            options.Children.Add(actions);
            var dialog = new Window
            {
                Owner = this,
                Title = "Start a new game",
                Content = options,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                SizeToContent = SizeToContent.WidthAndHeight,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false
            };
            var style = (Style)FindResource("ToolbarButtonStyle");
            var keep = new Button { Name = "KeepGameSettings", Content = "Keep roster and settings", Style = style };
            var blank = new Button { Name = "StartBlankGame", Content = "Start blank", Style = style };
            var cancel = new Button { Content = "Cancel", Style = style, IsCancel = true, IsDefault = true };
            actions.Children.Add(keep);
            actions.Children.Add(blank);
            actions.Children.Add(cancel);
            var keepSettings = false;
            keep.Click += (button, args) => { keepSettings = true; dialog.DialogResult = true; };
            blank.Click += (button, args) => dialog.DialogResult = true;
            if (dialog.ShowDialog() != true) return;
            try
            {
                StartFreshSession(keepSettings);
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Could not start a new game.", ex);
                SetFeed("Could not start a new game. Check the diagnostic log and try again.");
                MessageBox.Show(this, "The new game could not be started. Check the diagnostic log and try again.", "New game", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void AboutButton_Click(object sender, RoutedEventArgs e)
        {
            var version = System.Reflection.Assembly.GetEntryAssembly().GetName().Version;
            AboutVersionLabel.Text = "Version " + (version == null ? "1.0.0" : version.ToString(3));
            AboutOverlay.Visibility = Visibility.Visible;
        }

        private void CloseAboutButton_Click(object sender, RoutedEventArgs e)
        {
            AboutOverlay.Visibility = Visibility.Collapsed;
        }

    }
}
