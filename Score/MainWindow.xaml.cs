using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Score
{
    public partial class MainWindow : Window
    {
        private int currentRound = 1;
        private int configuredRounds = 3;
        private readonly int[] totalScores = new int[4];
        private readonly List<int>[] roundScores = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
        private readonly List<int>[] roundTeamElapsed = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
        private readonly int[] currentRoundTeamElapsed = new int[4];
        private readonly int[] totalTeamElapsed = new int[4];
        private readonly QuizTimerService globalTimer = new QuizTimerService();
        private readonly QuizTimerService roundTimer = new QuizTimerService();
        private readonly QuizTimerService teamTimer = new QuizTimerService();
        private readonly DispatcherTimer timerRefresh = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        private QuizSession activeSession;
        private TeamScoreCard activeTeamTimerCard;
        private bool isFinalAnnouncement;
        private bool isPreviewAnnouncement;
        private bool isFinalRoundComplete;
        private bool roundFinalized;
        private bool roundOvertimeAnnounced;

        private string SettingsDirectory { get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Score"); } }
        private string EventTitleFilePath { get { return Path.Combine(SettingsDirectory, "event-title.txt"); } }
        private string TeamNamesFilePath { get { return Path.Combine(SettingsDirectory, "team-names.txt"); } }
        private string TeamPlayersFilePath { get { return Path.Combine(SettingsDirectory, "team-players.txt"); } }
        private string RoundContextFilePath { get { return Path.Combine(SettingsDirectory, "round-context.txt"); } }

        public MainWindow()
        {
            try
            {
                WriteStartupLog("MainWindow constructor started.");
                InitializeComponent();
                WriteStartupLog("MainWindow initialized successfully.");
                LoadEventTitle();
                LoadTeamDetails();
                LoadRoundContext();
                EnsureScoreRecords();
                LoadSession();
                SynchronizeRoundCountSelector();
                LoadScores();
                LoadRoundHistory();
                foreach (var card in Cards) card.SetScoringEnabled(false);
                UpdateTotalLabels();
                timerRefresh.Tick += TimerRefresh_Tick;
                UpdateTimerLabels();
                UpdateCompetitionStatus();
                WriteStartupLog("MainWindow finished loading.");
            }
            catch (Exception ex)
            {
                WriteStartupLog("MainWindow constructor failed: " + ex);
                throw;
            }
        }

        private static void WriteStartupLog(string message)
        {
            try
            {
                File.AppendAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup.log"), DateTime.UtcNow.ToString("o") + " " + message + Environment.NewLine);
            }
            catch
            {
            }
        }

        private TeamScoreCard[] Cards { get { return new[] { Team1Card, Team2Card, Team3Card, Team4Card }; } }

        private void EnsureSettingsDirectory()
        {
            if (!Directory.Exists(SettingsDirectory)) Directory.CreateDirectory(SettingsDirectory);
        }

        private void LoadEventTitle()
        {
            if (!File.Exists(EventTitleFilePath)) return;
            var value = File.ReadAllText(EventTitleFilePath).Trim();
            if (!string.IsNullOrWhiteSpace(value)) EventTitleEditor.Text = value;
        }

        private void SaveEventTitle()
        {
            EventTitleEditor.Text = string.IsNullOrWhiteSpace(EventTitleEditor.Text) ? "QUIZ CHAMPIONSHIP" : EventTitleEditor.Text.Trim();
            EnsureSettingsDirectory();
            File.WriteAllText(EventTitleFilePath, EventTitleEditor.Text);
        }

        private void LoadTeamDetails()
        {
            var defaultNames = new[] { "Team Aurora", "Team Blue", "Team Emerald", "Team Gold" };
            var names = File.Exists(TeamNamesFilePath) ? File.ReadAllLines(TeamNamesFilePath) : defaultNames;
            var players = File.Exists(TeamPlayersFilePath) ? File.ReadAllLines(TeamPlayersFilePath) : new string[0];
            var cards = Cards;
            for (var i = 0; i < cards.Length; i++)
            {
                cards[i].TeamName = i < names.Length && !string.IsNullOrWhiteSpace(names[i]) ? names[i] : defaultNames[i];
                cards[i].TeamMembers = i < players.Length ? players[i] : string.Empty;
            }
        }

        private void SaveTeamDetails()
        {
            EnsureSettingsDirectory();
            File.WriteAllLines(TeamNamesFilePath, Cards.Select(card => card.TeamName ?? string.Empty).ToArray());
            File.WriteAllLines(TeamPlayersFilePath, Cards.Select(card => card.TeamMembers ?? string.Empty).ToArray());
        }

        private void LoadRoundContext()
        {
            if (!File.Exists(RoundContextFilePath)) return;
            var values = File.ReadAllLines(RoundContextFilePath);
            if (values.Length > 0 && !string.IsNullOrWhiteSpace(values[0])) RoundLabelEditor.Text = values[0];
            if (values.Length > 1)
            {
                var index = -1;
                for (var i = 0; i < RoundFormatSelector.Items.Count; i++)
                {
                    var item = RoundFormatSelector.Items[i] as ComboBoxItem;
                    if (item != null && string.Equals(item.Content.ToString(), values[1], StringComparison.OrdinalIgnoreCase)) index = i;
                }
                if (index >= 0) RoundFormatSelector.SelectedIndex = index;
            }
        }

        private void SaveRoundContext()
        {
            EnsureSettingsDirectory();
            File.WriteAllLines(RoundContextFilePath, new[] { RoundLabelEditor.Text.Trim(), CurrentRoundFormat });
        }

        private string CurrentRoundFormat
        {
            get
            {
                var selected = RoundFormatSelector.SelectedItem as ComboBoxItem;
                return selected == null ? "Standard" : selected.Content.ToString();
            }
        }

        private string CurrentRoundContext { get { return CurrentRoundFormat + " — " + (string.IsNullOrWhiteSpace(RoundLabelEditor.Text) ? "Untitled round" : RoundLabelEditor.Text.Trim()); } }

        private void EnsureScoreRecords()
        {
            var teamKeys = new[] { "JBA 1", "JBA 2", "YVM 1", "YVM 2" };
            using (var db = new ScoreDbContext())
            {
                db.Database.ExecuteSqlCommand("CREATE TABLE IF NOT EXISTS ScoreCounts (ScoreCountID INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, Team TEXT, ScoreValue INTEGER NOT NULL)");
                db.Database.ExecuteSqlCommand("CREATE TABLE IF NOT EXISTS QuizSessions (QuizSessionId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, Title TEXT, ConfiguredRounds INTEGER NOT NULL, CurrentRound INTEGER NOT NULL, Status TEXT, GlobalDurationSeconds INTEGER NOT NULL, RoundDurationSeconds INTEGER NOT NULL, StartedAtUtc DATETIME, PausedAtUtc DATETIME, CompletedAtUtc DATETIME, IsActive INTEGER NOT NULL)");
                db.Database.ExecuteSqlCommand("CREATE TABLE IF NOT EXISTS QuizRoundScores (QuizRoundScoreId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, QuizSessionId INTEGER NOT NULL, DatabaseTeamName TEXT, RoundNumber INTEGER NOT NULL, ScoreValue INTEGER NOT NULL, TeamElapsedSeconds INTEGER NOT NULL, ElapsedSeconds INTEGER NOT NULL, IsFinalized INTEGER NOT NULL)");
                db.Database.ExecuteSqlCommand("CREATE TABLE IF NOT EXISTS QuizEventLogs (QuizEventLogId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, QuizSessionId INTEGER NOT NULL, OccurredAtUtc TEXT NOT NULL, EventType TEXT, Team TEXT, RoundNumber INTEGER NOT NULL, ScoreChange INTEGER NOT NULL, ScoreAfter INTEGER NOT NULL, ElapsedSeconds INTEGER NOT NULL, Details TEXT)");
                foreach (var key in teamKeys)
                    if (!db.ScoreCounts.Any(record => record.Team == key)) db.ScoreCounts.Add(new ScoreCount { Team = key, ScoreValue = 0 });
                db.SaveChanges();
            }
        }

        private void LoadSession()
        {
            using (var db = new ScoreDbContext())
            {
                activeSession = db.QuizSessions.SingleOrDefault(session => session.IsActive);
                if (activeSession == null)
                {
                    activeSession = new QuizSession { Title = EventTitleEditor.Text, ConfiguredRounds = configuredRounds, CurrentRound = currentRound, Status = "NotStarted", IsActive = true };
                    db.QuizSessions.Add(activeSession);
                    db.SaveChanges();
                    LogEvent("SessionCreated", string.Empty, 0, 0, "New quiz session created");
                }
                else
                {
                    configuredRounds = Math.Max(1, activeSession.ConfiguredRounds);
                    currentRound = Math.Max(1, activeSession.CurrentRound);
                }
            }
        }

        private void SynchronizeRoundCountSelector()
        {
            for (var i = 0; i < RoundCountSelector.Items.Count; i++)
            {
                var item = RoundCountSelector.Items[i] as ComboBoxItem;
                if (item != null && item.Content.ToString() == configuredRounds.ToString()) RoundCountSelector.SelectedIndex = i;
            }
            RoundNumberLabel.Text = "ROUND " + currentRound + " OF " + configuredRounds;
        }

        private void LoadScores()
        {
            using (var db = new ScoreDbContext())
                foreach (var card in Cards) card.ScoreValue = GetScoreFromDb(db, card);
            UpdateLeaderIndicators();
        }

        private void LoadRoundHistory()
        {
            using (var db = new ScoreDbContext())
            {
                var history = db.QuizRoundScores.Where(round => round.QuizSessionId == activeSession.QuizSessionId).OrderBy(round => round.RoundNumber).ToList();
                foreach (var round in history)
                {
                    var index = GetTeamIndex(round.DatabaseTeamName);
                    if (index < 0) continue;
                    roundScores[index].Add(round.ScoreValue);
                    roundTeamElapsed[index].Add(round.TeamElapsedSeconds);
                    totalScores[index] += round.ScoreValue;
                    totalTeamElapsed[index] += round.TeamElapsedSeconds;
                }
                roundFinalized = history.Count(round => round.RoundNumber == currentRound) >= Cards.Length;
            }
            UpdateRoundScoreLabels();
        }

        private int GetTeamIndex(string key)
        {
            var cards = Cards;
            for (var i = 0; i < cards.Length; i++) if (cards[i].DatabaseTeamName == key) return i;
            return -1;
        }

        private TeamScoreCard GetCard(string key) { return Cards.FirstOrDefault(card => card.DatabaseTeamName == key); }
        private int GetScoreFromDb(ScoreDbContext db, TeamScoreCard card) { var score = db.ScoreCounts.FirstOrDefault(record => record.Team == card.DatabaseTeamName); return score == null ? 0 : score.ScoreValue; }

        private void Card_ScoreRequested(object sender, TeamScoreRequestedEventArgs e)
        {
            if (roundFinalized) { SetFeed("This round has already been finalized. Select NEXT ROUND before scoring again."); return; }
            var card = sender as TeamScoreCard;
            if (card != null) UpdateScore(card, e.Points, false);
        }

        private void UpdateScore(TeamScoreCard card, int change, bool isUndo)
        {
            var after = 0;
            using (var db = new ScoreDbContext())
            {
                var score = db.ScoreCounts.FirstOrDefault(record => record.Team == card.DatabaseTeamName);
                if (score == null) return;
                score.ScoreValue += change;
                after = score.ScoreValue;
                db.SaveChanges();
            }
            card.UpdateScore(after, change);
            PersistSession("Running");
            UpdateLeaderIndicators();
            var verb = isUndo ? "ScoreUndo" : change > 0 ? "ScoreAwarded" : "ScorePenalty";
            LogEvent(verb, card.TeamName, change, after, "Team key: " + card.DatabaseTeamName + "; " + CurrentRoundContext + "; players: " + (card.TeamMembers ?? string.Empty));
            SetFeed(card.TeamName + (change > 0 ? " awarded +" : " penalized ") + change + " point" + (Math.Abs(change) == 1 ? string.Empty : "s") + ". Score: " + after + ".");
        }

        private void UndoLastScoreButton_Click(object sender, RoutedEventArgs e)
        {
            QuizEventLog candidate = null;
            var undone = new HashSet<int>();
            using (var db = new ScoreDbContext())
            {
                var events = db.QuizEventLogs.Where(log => log.QuizSessionId == activeSession.QuizSessionId).OrderBy(log => log.QuizEventLogId).ToList();
                foreach (var log in events.Where(log => log.EventType == "ScoreUndo"))
                {
                    var marker = "Reversed event ";
                    var start = (log.Details ?? string.Empty).IndexOf(marker, StringComparison.Ordinal);
                    int id;
                    if (start >= 0 && int.TryParse(log.Details.Substring(start + marker.Length).Split(';')[0], out id)) undone.Add(id);
                }
                candidate = events.LastOrDefault(log => (log.EventType == "ScoreAwarded" || log.EventType == "ScorePenalty") && !undone.Contains(log.QuizEventLogId));
            }
            if (candidate == null) { SetFeed("There is no score change left to undo in this session."); return; }
            var keyMarker = "Team key: ";
            var keyStart = (candidate.Details ?? string.Empty).IndexOf(keyMarker, StringComparison.Ordinal);
            var key = keyStart < 0 ? string.Empty : candidate.Details.Substring(keyStart + keyMarker.Length).Split(';')[0].Trim();
            var card = GetCard(key);
            if (card == null) { SetFeed("The last score entry cannot be matched to a team."); return; }
            UpdateScore(card, -candidate.ScoreChange, true);
            LogEvent("ScoreUndo", card.TeamName, 0, card.ScoreValue, "Reversed event " + candidate.QuizEventLogId + "; original change " + candidate.ScoreChange);
            SetFeed("Undid the last score change for " + card.TeamName + ".");
        }

        private void UpdateLeaderIndicators()
        {
            var cards = Cards;
            var standings = cards.Select((card, index) => totalScores[index] + card.ScoreValue).ToArray();
            var highest = standings.Max();
            var hasLeader = highest > 0;
            for (var i = 0; i < cards.Length; i++)
            {
                var rank = 1 + standings.Count(value => value > standings[i]);
                cards[i].SetLeading(hasLeader && standings[i] == highest);
                cards[i].SetRanking(rank);
                cards[i].SetDangerState(cards[i].ScoreValue < 0);
            }
        }

        private void FinishRoundButton_Click(object sender, RoutedEventArgs e)
        {
            if (roundFinalized) { SetFeed("Round " + currentRound + " is already recorded. Choose NEXT ROUND in the results panel."); return; }
            StopActiveTeamTimer();
            roundTimer.Stop();
            var cards = Cards;
            SaveCompletedRoundScores(cards);
            for (var i = 0; i < cards.Length; i++)
            {
                totalScores[i] += cards[i].ScoreValue;
                roundScores[i].Add(cards[i].ScoreValue);
                roundTeamElapsed[i].Add(currentRoundTeamElapsed[i]);
                totalTeamElapsed[i] += currentRoundTeamElapsed[i];
            }
            roundFinalized = true;
            UpdateTotalLabels();
            UpdateRoundScoreLabels();
            LogEvent("RoundFinished", string.Empty, 0, 0, CurrentRoundContext + " completed");
            SetFeed(CurrentRoundContext + " recorded. Review the result before moving on.");
            ShowRoundAnnouncement(cards);
            UpdateTimerLabels();
        }

        private void SaveCompletedRoundScores(TeamScoreCard[] cards)
        {
            using (var db = new ScoreDbContext())
            {
                for (var i = 0; i < cards.Length; i++)
                {
                    var teamName = cards[i].DatabaseTeamName;
                    if (db.QuizRoundScores.Any(round => round.QuizSessionId == activeSession.QuizSessionId && round.RoundNumber == currentRound && round.DatabaseTeamName == teamName)) continue;
                    db.QuizRoundScores.Add(new QuizRoundScore { QuizSessionId = activeSession.QuizSessionId, DatabaseTeamName = cards[i].DatabaseTeamName, RoundNumber = currentRound, ScoreValue = cards[i].ScoreValue, TeamElapsedSeconds = currentRoundTeamElapsed[i], ElapsedSeconds = (int)roundTimer.Elapsed.TotalSeconds, IsFinalized = true });
                }
                db.SaveChanges();
            }
            PersistSession(currentRound >= configuredRounds ? "FinalRoundComplete" : "RoundComplete");
        }

        private void ShowRoundAnnouncement(TeamScoreCard[] cards)
        {
            isFinalRoundComplete = currentRound >= configuredRounds;
            isFinalAnnouncement = false;
            isPreviewAnnouncement = false;
            var highest = cards.Max(card => card.ScoreValue);
            var winners = cards.Where(card => card.ScoreValue == highest).ToArray();
            WinnerContext.Text = isFinalRoundComplete ? "FINAL ROUND COMPLETE" : "ROUND " + currentRound + " RESULT";
            WinnerTitle.Text = highest == 0 ? "NO SCORE" : winners.Length == 1 ? winners[0].TeamName : "TIE";
            WinnerScore.Text = highest == 0 ? "0 POINTS" : winners.Length == 1 ? highest + " POINTS" : string.Join("  /  ", winners.Select(card => card.TeamName)) + "\n" + highest + " POINTS";
            PopulateRoundResults(cards, false);
            RoundResultsPanel.Visibility = Visibility.Visible;
            FinalStandings.Visibility = Visibility.Collapsed;
            WinnerActionButton.Content = isFinalRoundComplete ? "ANNOUNCE FINAL RESULT" : "NEXT ROUND";
            if (isFinalRoundComplete)
            {
                globalTimer.Stop();
                PersistSession("FinalRoundComplete");
                LogEvent("FinalRoundComplete", string.Empty, 0, 0, "All rounds completed");
            }
            WinnerOverlay.Visibility = Visibility.Visible;
            foreach (var card in cards) card.SetScoringEnabled(false);
            UpdateCompetitionStatus();
        }

        private void PopulateRoundResults(TeamScoreCard[] cards, bool includeCurrentRound)
        {
            RoundResultsRows.Children.Clear();
            var standings = Enumerable.Range(0, cards.Length)
                .OrderByDescending(index => totalScores[index])
                .ThenByDescending(index => cards[index].ScoreValue)
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
                var surface = new Border { Padding = new Thickness(12, 9, 12, 9), Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(255, 20, 36, 58)), BorderBrush = cards[index].ScoreColor, BorderThickness = new Thickness(position == 0 ? 2 : 1), CornerRadius = new CornerRadius(8), Child = row };
                AddResultCell(row, position == 0 ? "1" : (position + 1).ToString(), 0, System.Windows.Media.Brushes.White, true, TextAlignment.Left);
                AddResultCell(row, cards[index].TeamName, 1, System.Windows.Media.Brushes.White, true, TextAlignment.Left);
                AddResultCell(row, (cards[index].ScoreValue >= 0 ? "+" : string.Empty) + cards[index].ScoreValue + " pts", 2, cards[index].ScoreColor, true, TextAlignment.Right);
                var displayedTotal = totalScores[index] + (includeCurrentRound ? cards[index].ScoreValue : 0);
                AddResultCell(row, displayedTotal + " pts", 3, System.Windows.Media.Brushes.LightGreen, true, TextAlignment.Right);
                AddResultCell(row, FormatElapsed(currentRoundTeamElapsed[index]), 4, System.Windows.Media.Brushes.LightGreen, false, TextAlignment.Right);
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
                SetFeed("Final results remain recorded. You can review the scoreboard or export the audit log.");
                return;
            }
            if (isPreviewAnnouncement) { WinnerOverlay.Visibility = Visibility.Collapsed; isPreviewAnnouncement = false; return; }
            if (isFinalRoundComplete) { ShowFinalAnnouncement(); return; }
            currentRound++;
            roundFinalized = false;
            roundOvertimeAnnounced = false;
            ResetActiveRoundScores();
            ResetTeamTimer();
            Array.Clear(currentRoundTeamElapsed, 0, currentRoundTeamElapsed.Length);
            if (globalTimer.State == QuizTimerState.Running || globalTimer.State == QuizTimerState.Overtime) { roundTimer.Start(GetRoundDuration()); timerRefresh.Start(); }
            else roundTimer.Reset();
            WinnerOverlay.Visibility = Visibility.Collapsed;
            foreach (var card in Cards) card.SetScoringEnabled(true);
            RoundNumberLabel.Text = "ROUND " + currentRound + " OF " + configuredRounds;
            PersistSession("Running");
            LogEvent("RoundStarted", string.Empty, 0, 0, CurrentRoundContext + " started");
            SetFeed(CurrentRoundContext + " is ready. Award points using +1, +2, +5, or a penalty.");
            UpdateTimerLabels();
            UpdateTimerControls();
            UpdateCompetitionStatus();
        }

        private void AnnounceWinnerButton_Click(object sender, RoutedEventArgs e)
        {
            var cards = Cards;
            var highest = cards.Max(card => card.ScoreValue);
            var winners = cards.Where(card => card.ScoreValue == highest).ToArray();
            WinnerContext.Text = "LIVE ROUND RESULT";
            WinnerTitle.Text = highest == 0 ? "NO SCORE" : winners.Length == 1 ? winners[0].TeamName : "TIE";
            WinnerScore.Text = highest + " ROUND POINTS";
            FinalStandings.Visibility = Visibility.Collapsed;
            PopulateRoundResults(cards, true);
            RoundResultsPanel.Visibility = Visibility.Visible;
            WinnerActionButton.Content = "CLOSE";
            isFinalRoundComplete = false;
            isFinalAnnouncement = false;
            isPreviewAnnouncement = true;
            WinnerOverlay.Visibility = Visibility.Visible;
        }

        private void ShowStandingsButton_Click(object sender, RoutedEventArgs e)
        {
            WinnerContext.Text = "LIVE STANDINGS";
            WinnerTitle.Text = "SCOREBOARD";
            WinnerScore.Text = currentRound > 1 ? "THROUGH ROUND " + (currentRound - 1) : "BEFORE ROUND 1";
            PopulateFinalStandings(Cards);
            FinalStandings.Visibility = Visibility.Visible;
            RoundResultsPanel.Visibility = Visibility.Collapsed;
            isFinalAnnouncement = false;
            isFinalRoundComplete = false;
            isPreviewAnnouncement = true;
            WinnerActionButton.Content = "CLOSE SCOREBOARD";
            WinnerOverlay.Visibility = Visibility.Visible;
        }

        private void ShowFinalAnnouncement()
        {
            var cards = Cards;
            var highest = totalScores.Max();
            var winners = cards.Where((card, index) => totalScores[index] == highest).ToArray();
            isFinalAnnouncement = true;
            isPreviewAnnouncement = false;
            WinnerContext.Text = "FINAL RESULT";
            WinnerTitle.Text = winners.Length == 1 ? winners[0].TeamName : "TIE";
            WinnerScore.Text = highest + " POINTS  🏆";
            PopulateFinalStandings(cards);
            FinalStandings.Visibility = Visibility.Visible;
            RoundResultsPanel.Visibility = Visibility.Collapsed;
            WinnerActionButton.Content = "CLOSE RESULTS";
            LogEvent("FinalWinnerAnnounced", string.Join(" / ", winners.Select(card => card.TeamName)), 0, highest, "Final result announced");
            for (var i = 0; i < cards.Length; i++) cards[i].SetFinalWinner(totalScores[i] == highest);
            AnimateFinalResults();
        }

        private void AnimateFinalResults()
        {
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
            element.RenderTransform = new TransformGroup
            {
                Children = new TransformCollection
                {
                    new ScaleTransform(scale, scale),
                    new TranslateTransform(0, offset)
                }
            };
            var opacity = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(420)) { BeginTime = TimeSpan.FromMilliseconds(delayMilliseconds) };
            var movement = new DoubleAnimation(offset, 0, TimeSpan.FromMilliseconds(560)) { BeginTime = TimeSpan.FromMilliseconds(delayMilliseconds), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            var growX = new DoubleAnimation(scale, 1, TimeSpan.FromMilliseconds(560)) { BeginTime = TimeSpan.FromMilliseconds(delayMilliseconds), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            var growY = new DoubleAnimation(scale, 1, TimeSpan.FromMilliseconds(560)) { BeginTime = TimeSpan.FromMilliseconds(delayMilliseconds), EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
            element.BeginAnimation(UIElement.OpacityProperty, opacity);
            element.RenderTransform.BeginAnimation(TranslateTransform.YProperty, movement);
            element.RenderTransform.BeginAnimation(ScaleTransform.ScaleXProperty, growX);
            element.RenderTransform.BeginAnimation(ScaleTransform.ScaleYProperty, growY);
        }

        private void PopulateFinalStandings(TeamScoreCard[] cards)
        {
            FinalStandingsRows.Children.Clear();
            var ranking = Enumerable.Range(0, cards.Length).OrderByDescending(index => totalScores[index]).ThenBy(index => index).ToArray();
            for (var position = 0; position < ranking.Length; position++)
            {
                var index = ranking[position];
                var row = new Grid { Margin = new Thickness(0, 2, 0, 2) };
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(52) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(150) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(140) });
                var background = position == 0 ? System.Windows.Media.Color.FromArgb(255, 55, 49, 28) : System.Windows.Media.Color.FromArgb(255, 20, 36, 58);
                var surface = new Border { Padding = new Thickness(12, 10, 12, 10), Background = new System.Windows.Media.SolidColorBrush(background), BorderBrush = cards[index].ScoreColor, BorderThickness = new Thickness(position == 0 ? 2 : 1), CornerRadius = new CornerRadius(8), Child = row };
                var rankText = position == 0 ? "1  *" : (position + 1).ToString();
                AddFinalCell(row, rankText, 0, position == 0 ? System.Windows.Media.Brushes.Gold : System.Windows.Media.Brushes.White, true, TextAlignment.Left);
                AddFinalCell(row, cards[index].TeamName, 1, System.Windows.Media.Brushes.White, true, TextAlignment.Left);
                AddFinalCell(row, totalScores[index] + " pts", 2, cards[index].ScoreColor, true, TextAlignment.Right);
                AddFinalCell(row, string.Join("   ", roundScores[index].Select((score, round) => "R" + (round + 1) + " " + (score >= 0 ? "+" : string.Empty) + score + " / " + FormatShortElapsed(round < roundTeamElapsed[index].Count ? roundTeamElapsed[index][round] : 0))), 3, System.Windows.Media.Brushes.LightSteelBlue, false, TextAlignment.Right);
                AddFinalCell(row, FormatElapsed(totalTeamElapsed[index]), 4, System.Windows.Media.Brushes.LightGreen, false, TextAlignment.Right);
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
            var confirmation = MessageBox.Show("Start a new game? The current session, scores, and audit log will be preserved and can still be exported.", "Start new game", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirmation == MessageBoxResult.Yes) StartFreshSession();
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

        private void StartFreshSession()
        {
            StopActiveTeamTimer();
            using (var db = new ScoreDbContext())
            {
                var previous = db.QuizSessions.SingleOrDefault(session => session.QuizSessionId == activeSession.QuizSessionId);
                if (previous != null) { previous.IsActive = false; previous.CompletedAtUtc = DateTime.UtcNow; }
                foreach (var score in db.ScoreCounts) score.ScoreValue = 0;
                var newSession = new QuizSession { Title = EventTitleEditor.Text, ConfiguredRounds = configuredRounds, CurrentRound = 1, Status = "NotStarted", IsActive = true };
                db.QuizSessions.Add(newSession);
                db.SaveChanges();
                activeSession = newSession;
            }
            Array.Clear(totalScores, 0, totalScores.Length);
            Array.Clear(currentRoundTeamElapsed, 0, currentRoundTeamElapsed.Length);
            Array.Clear(totalTeamElapsed, 0, totalTeamElapsed.Length);
            foreach (var list in roundScores) list.Clear();
            foreach (var list in roundTeamElapsed) list.Clear();
            currentRound = 1;
            isFinalRoundComplete = false;
            isFinalAnnouncement = false;
            isPreviewAnnouncement = false;
            roundFinalized = false;
            roundOvertimeAnnounced = false;
            WinnerOverlay.Visibility = Visibility.Collapsed;
            foreach (var card in Cards) { card.ScoreValue = 0; card.SetScoringEnabled(false); card.SetFinalWinner(false); }
            ResetTimerState();
            UpdateTimerControls();
            UpdateTotalLabels();
            UpdateRoundScoreLabels();
            UpdateLeaderIndicators();
            RoundNumberLabel.Text = "ROUND 1 OF " + configuredRounds;
            LogEvent("SessionCreated", string.Empty, 0, 0, "New game started; previous session archived");
            SetFeed("New game ready. Add or confirm player names, choose a round type, and start live scoring.");
            UpdateCompetitionStatus();
        }

        private void ResetActiveRoundScores()
        {
            using (var db = new ScoreDbContext())
            {
                foreach (var score in db.ScoreCounts) score.ScoreValue = 0;
                db.SaveChanges();
            }
            foreach (var card in Cards) card.ScoreValue = 0;
            UpdateLeaderIndicators();
        }

        private void UpdateTotalLabels() { for (var i = 0; i < Cards.Length; i++) Cards[i].SetTotalScore(totalScores[i]); }
        private void UpdateRoundScoreLabels() { for (var i = 0; i < Cards.Length; i++) Cards[i].SetRoundScores(string.Join("  ·  ", roundScores[i].Select((score, round) => "R" + (round + 1) + " " + score))); }

        private void StartTimerButton_Click(object sender, RoutedEventArgs e)
        {
            globalTimer.Start(GetGlobalDuration());
            roundTimer.Start(GetRoundDuration());
            roundOvertimeAnnounced = false;
            activeSession.GlobalDurationSeconds = (int)GetGlobalDuration().TotalSeconds;
            activeSession.RoundDurationSeconds = (int)GetRoundDuration().TotalSeconds;
            activeSession.StartedAtUtc = DateTime.UtcNow;
            GlobalMinutesSelector.IsEnabled = false;
            RoundMinutesSelector.IsEnabled = false;
            foreach (var card in Cards) card.SetScoringEnabled(true);
            timerRefresh.Start();
            PersistSession("Running");
            LogEvent("TimerStarted", string.Empty, 0, 0, CurrentRoundContext + "; event and round timers started");
            SetFeed("Live scoring started for " + CurrentRoundContext + ".");
            UpdateTimerLabels();
            UpdateTimerControls();
            UpdateCompetitionStatus();
        }

        private void RestartTimerButton_Click(object sender, RoutedEventArgs e)
        {
            StartTimerButton_Click(sender, e);
            SetFeed("Timers restarted using the selected event and round limits.");
        }

        private void PauseTimerButton_Click(object sender, RoutedEventArgs e)
        {
            globalTimer.Pause(); roundTimer.Pause(); StopActiveTeamTimer(); PersistSession("Paused");
            LogEvent("TimerPaused", string.Empty, 0, 0, "Timers paused"); SetFeed("Timers paused. Scores remain available for correction."); UpdateTimerLabels(); UpdateTimerControls(); UpdateCompetitionStatus();
        }

        private void ResumeTimerButton_Click(object sender, RoutedEventArgs e)
        {
            globalTimer.Resume(); roundTimer.Resume(); timerRefresh.Start(); PersistSession("Running");
            LogEvent("TimerResumed", string.Empty, 0, 0, "Timers resumed"); SetFeed("Timers resumed."); UpdateTimerLabels(); UpdateTimerControls(); UpdateCompetitionStatus();
        }

        private TimeSpan GetRoundDuration() { return TimeSpan.FromMinutes(GetSelectedMinutes(RoundMinutesSelector)); }
        private TimeSpan GetGlobalDuration() { return TimeSpan.FromMinutes(GetSelectedMinutes(GlobalMinutesSelector)); }
        private int GetSelectedMinutes(ComboBox selector)
        {
            var item = selector.SelectedItem as ComboBoxItem;
            if (item == null) return 0;
            var firstWord = item.Content.ToString().Split(' ')[0];
            int value;
            return int.TryParse(firstWord, out value) ? value : 0;
        }

        private void TimerRefresh_Tick(object sender, EventArgs e)
        {
            globalTimer.RefreshState(); roundTimer.RefreshState(); UpdateTimerLabels(); UpdateTeamTimerDisplay();
            if (roundTimer.IsOvertime && !roundOvertimeAnnounced) { roundOvertimeAnnounced = true; LogEvent("RoundOvertime", string.Empty, 0, 0, CurrentRoundContext + " timer expired"); SetFeed("Round time has expired — overtime is now being shown in red."); UpdateCompetitionStatus(); }
        }

        private void UpdateTimerLabels()
        {
            GlobalTimerLabel.Text = globalTimer.Duration == TimeSpan.Zero ? "NO LIMIT" : FormatTimer(globalTimer);
            RoundTimerLabel.Text = roundTimer.Duration == TimeSpan.Zero ? "NO LIMIT" : FormatTimer(roundTimer);
            GlobalTimerLabel.Foreground = globalTimer.IsOvertime ? System.Windows.Media.Brushes.IndianRed : System.Windows.Media.Brushes.LightGreen;
            RoundTimerLabel.Foreground = roundTimer.IsOvertime ? System.Windows.Media.Brushes.IndianRed : System.Windows.Media.Brushes.LightGreen;
        }

        private void UpdateTimerControls()
        {
            var isRunning = globalTimer.State == QuizTimerState.Running || globalTimer.State == QuizTimerState.Overtime;
            var isPaused = globalTimer.State == QuizTimerState.Paused;
            var isStopped = globalTimer.State == QuizTimerState.Stopped;
            StartTimerButton.IsEnabled = isStopped;
            StartTimerButton.Content = isRunning || isPaused ? "LIVE NOW" : "START LIVE";
            PauseTimerButton.IsEnabled = isRunning;
            ResumeTimerButton.IsEnabled = isPaused;
            RestartTimerButton.IsEnabled = !isStopped;
            foreach (var card in Cards) card.SetLiveScoring(isRunning);
        }

        private string FormatTimer(QuizTimerService timer) { return timer.IsOvertime ? "OT " + timer.Elapsed.ToString(@"mm\:ss") : timer.Remaining.ToString(@"mm\:ss"); }
        private string FormatElapsed(int seconds) { return TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss"); }
        private string FormatShortElapsed(int seconds) { return TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss"); }

        private void ResetTimerState()
        {
            globalTimer.Reset(); roundTimer.Reset(); ResetTeamTimer(); timerRefresh.Stop();
            GlobalMinutesSelector.IsEnabled = true; RoundMinutesSelector.IsEnabled = true; UpdateTimerLabels(); UpdateTimerControls();
        }

        private void Card_TeamTimerStarted(object sender, RoutedEventArgs e)
        {
            var card = sender as TeamScoreCard;
            if (card == null) return;
            StopActiveTeamTimer(); teamTimer.Start(TimeSpan.Zero); activeTeamTimerCard = card; card.SetTeamTimerRunning(true); timerRefresh.Start();
            LogEvent("TeamTimerStarted", card.TeamName, 0, card.ScoreValue, "Team key: " + card.DatabaseTeamName + "; response timer started"); SetFeed("Response timer started for " + card.TeamName + ".");
        }

        private void Card_TeamTimerStopped(object sender, RoutedEventArgs e)
        {
            if (sender == activeTeamTimerCard) { var card = activeTeamTimerCard; StopActiveTeamTimer(); LogEvent("TeamTimerStopped", card.TeamName, 0, card.ScoreValue, "Response timer stopped"); }
        }

        private void StopActiveTeamTimer()
        {
            if (activeTeamTimerCard == null) return;
            teamTimer.Stop(); var index = GetTeamIndex(activeTeamTimerCard.DatabaseTeamName);
            if (index >= 0) currentRoundTeamElapsed[index] += (int)teamTimer.Elapsed.TotalSeconds;
            activeTeamTimerCard.SetTeamTimerRunning(false); activeTeamTimerCard = null; UpdateTeamTimerDisplay();
        }

        private void ResetTeamTimer()
        {
            StopActiveTeamTimer(); teamTimer.Reset();
            foreach (var card in Cards) { card.SetTeamTimerRunning(false); card.SetTeamTimerDisplay("00:00", false); }
        }

        private void UpdateTeamTimerDisplay()
        {
            for (var i = 0; i < Cards.Length; i++)
            {
                var elapsed = currentRoundTeamElapsed[i];
                if (Cards[i] == activeTeamTimerCard) elapsed += (int)teamTimer.Elapsed.TotalSeconds;
                Cards[i].SetTeamTimerDisplay(TimeSpan.FromSeconds(elapsed).ToString(@"mm\:ss"), false);
            }
        }

        private void RoundCountSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = RoundCountSelector.SelectedItem as ComboBoxItem; int value;
            if (item != null && int.TryParse(item.Content.ToString(), out value)) { configuredRounds = value; SynchronizeRoundCountSelector(); PersistSession(activeSession == null ? "NotStarted" : activeSession.Status); UpdateCompetitionStatus(); }
        }

        private void TimerDurationSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GlobalTimerLabel == null || RoundTimerLabel == null) return;
            UpdateTimerLabels();
        }

        private void EventTitleEditor_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { EventTitleEditor.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)); e.Handled = true; } }
        private void EventTitleEditor_LostFocus(object sender, RoutedEventArgs e) { SaveEventTitle(); PersistSession(activeSession == null ? "NotStarted" : activeSession.Status); }
        private void RoundFormatSelector_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (RoundLabelEditor == null) return; SaveRoundContext(); SetFeed("Round format set to " + CurrentRoundFormat + "."); }
        private void RoundLabelEditor_LostFocus(object sender, RoutedEventArgs e) { RoundLabelEditor.Text = string.IsNullOrWhiteSpace(RoundLabelEditor.Text) ? "Untitled round" : RoundLabelEditor.Text.Trim(); SaveRoundContext(); }
        private void Card_TeamDetailsChanged(object sender, RoutedEventArgs e) { SaveTeamDetails(); var card = sender as TeamScoreCard; if (card != null) { LogEvent("TeamDetailsUpdated", card.TeamName, 0, card.ScoreValue, "Team key: " + card.DatabaseTeamName + "; players: " + (card.TeamMembers ?? string.Empty)); SetFeed(card.TeamName + " roster saved."); } }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.FocusedElement is TextBox || Keyboard.FocusedElement is ComboBox) return;
            if (e.Key == Key.Space) { if (globalTimer.State == QuizTimerState.Running) PauseTimerButton_Click(this, new RoutedEventArgs()); else ResumeTimerButton_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (e.Key == Key.F11) { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; e.Handled = true; }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z) { UndoLastScoreButton_Click(this, new RoutedEventArgs()); e.Handled = true; }
        }

        private void SetFeed(string message) { if (EventFeedLabel != null) EventFeedLabel.Text = message; }
        private void UpdateCompetitionStatus()
        {
            CompetitionStatusLabel.Text = isFinalAnnouncement ? "FINAL RESULT" : isFinalRoundComplete ? "FINAL ROUND COMPLETE" : roundFinalized ? "ROUND RECORDED" : roundTimer.IsOvertime ? "ROUND OVERTIME" : globalTimer.State == QuizTimerState.Paused ? "PAUSED" : globalTimer.State == QuizTimerState.Running ? "LIVE" : "READY TO START";
            CompetitionStatusLabel.Foreground = roundTimer.IsOvertime ? System.Windows.Media.Brushes.IndianRed : globalTimer.State == QuizTimerState.Paused ? System.Windows.Media.Brushes.Gold : System.Windows.Media.Brushes.LightGreen;
        }

        private void LogEvent(string type, string team, int change, int after, string details)
        {
            if (activeSession == null) return;
            try
            {
                using (var db = new ScoreDbContext())
                {
                    db.QuizEventLogs.Add(new QuizEventLog { QuizSessionId = activeSession.QuizSessionId, OccurredAtUtc = DateTime.UtcNow, EventType = type, Team = team, RoundNumber = currentRound, ScoreChange = change, ScoreAfter = after, ElapsedSeconds = (int)globalTimer.Elapsed.TotalSeconds, Details = details });
                    db.SaveChanges();
                }
            }
            catch { }
        }

        private void PersistSession(string status)
        {
            if (activeSession == null) return;
            using (var db = new ScoreDbContext())
            {
                var session = db.QuizSessions.SingleOrDefault(item => item.QuizSessionId == activeSession.QuizSessionId);
                if (session == null) return;
                session.Status = status; session.Title = EventTitleEditor.Text; session.ConfiguredRounds = configuredRounds; session.CurrentRound = currentRound;
                session.GlobalDurationSeconds = activeSession.GlobalDurationSeconds;
                session.RoundDurationSeconds = activeSession.RoundDurationSeconds;
                session.StartedAtUtc = activeSession.StartedAtUtc;
                if (status == "Paused") session.PausedAtUtc = DateTime.UtcNow;
                if (status == "FinalRoundComplete") session.CompletedAtUtc = DateTime.UtcNow;
                db.SaveChanges();
                activeSession.Status = session.Status; activeSession.Title = session.Title; activeSession.ConfiguredRounds = session.ConfiguredRounds; activeSession.CurrentRound = session.CurrentRound;
            }
        }

        private void ExportLogButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog { Title = "Export competition audit log", Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*", FileName = (string.IsNullOrWhiteSpace(EventTitleEditor.Text) ? "quiz" : EventTitleEditor.Text.Trim()) + "-audit-log.csv" };
            if (dialog.ShowDialog() != true) return;
            var csv = new StringBuilder();
            QuizSession session;
            using (var db = new ScoreDbContext())
            {
                session = db.QuizSessions.SingleOrDefault(item => item.QuizSessionId == activeSession.QuizSessionId);
                if (session == null) return;
                var rounds = db.QuizRoundScores.Where(round => round.QuizSessionId == activeSession.QuizSessionId).OrderBy(round => round.RoundNumber).ThenBy(round => round.DatabaseTeamName).ToList();
                var logs = db.QuizEventLogs.Where(log => log.QuizSessionId == activeSession.QuizSessionId).OrderBy(log => log.OccurredAtUtc).ThenBy(log => log.QuizEventLogId).ToList();
                csv.AppendLine("QUIZ AUDIT EXPORT");
                csv.AppendLine("Purpose," + Csv("Complete, round-wise record of quiz configuration, scores, response times, and actions. Team names are the names shown on screen."));
                csv.AppendLine("Exported UTC," + Csv(DateTime.UtcNow.ToString("u")));
                csv.AppendLine();
                csv.AppendLine("SESSION DETAILS");
                csv.AppendLine("Session ID,Title,Status,Configured rounds,Whole event limit seconds,Each round limit seconds,Started UTC,Paused UTC,Completed UTC,Active");
                csv.AppendLine(string.Join(",", session.QuizSessionId, Csv(session.Title), Csv(session.Status), session.ConfiguredRounds, session.GlobalDurationSeconds, session.RoundDurationSeconds, Csv(session.StartedAtUtc.HasValue ? session.StartedAtUtc.Value.ToString("u") : string.Empty), Csv(session.PausedAtUtc.HasValue ? session.PausedAtUtc.Value.ToString("u") : string.Empty), Csv(session.CompletedAtUtc.HasValue ? session.CompletedAtUtc.Value.ToString("u") : string.Empty), session.IsActive));
                csv.AppendLine();
                csv.AppendLine("TEAMS ON SCREEN");
                csv.AppendLine("Team,Players,Final score,Overall response seconds,Overall response time");
                for (var i = 0; i < Cards.Length; i++) csv.AppendLine(string.Join(",", Csv(Cards[i].TeamName), Csv(Cards[i].TeamMembers), totalScores[i], totalTeamElapsed[i], Csv(FormatElapsed(totalTeamElapsed[i]))));
                csv.AppendLine();
                csv.AppendLine("ROUND-BY-ROUND RECORD");
                csv.AppendLine("Round,Team,Round score,Cumulative score after round,Round elapsed seconds,Round elapsed time,Team response seconds,Team response time");
                var cumulativeScores = new int[Cards.Length];
                foreach (var round in rounds)
                {
                    var index = GetTeamIndex(round.DatabaseTeamName);
                    if (index < 0) continue;
                    cumulativeScores[index] += round.ScoreValue;
                    csv.AppendLine(string.Join(",", round.RoundNumber, Csv(Cards[index].TeamName), round.ScoreValue, cumulativeScores[index], round.ElapsedSeconds, Csv(FormatElapsed(round.ElapsedSeconds)), round.TeamElapsedSeconds, Csv(FormatElapsed(round.TeamElapsedSeconds))));
                }
                csv.AppendLine();
                csv.AppendLine("AUDIT TIMELINE");
                csv.AppendLine("Sequence,Timestamp UTC,Action,Team,Round,Score change,Score after action,Quiz elapsed seconds,Details");
                for (var i = 0; i < logs.Count; i++)
                {
                    var log = logs[i];
                    csv.AppendLine(string.Join(",", i + 1, Csv(log.OccurredAtUtc.ToString("u")), Csv(log.EventType), Csv(GetDisplayTeamName(log.Team)), log.RoundNumber, log.ScoreChange, log.ScoreAfter, log.ElapsedSeconds, Csv(GetAuditDetails(log.Details))));
                }
            }
            File.WriteAllText(dialog.FileName, csv.ToString(), Encoding.UTF8);
            SetFeed("Audit log exported successfully.");
            MessageBox.Show("The competition audit log was exported successfully.", "Export complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private string GetDisplayTeamName(string team)
        {
            if (string.IsNullOrWhiteSpace(team)) return string.Empty;
            var names = team.Split(new[] { " / " }, StringSplitOptions.None).Select(GetSingleDisplayTeamName);
            return string.Join(" / ", names);
        }

        private string GetSingleDisplayTeamName(string team)
        {
            var card = GetCard(team.Trim());
            return card == null ? team.Trim() : card.TeamName;
        }

        private string GetAuditDetails(string details)
        {
            if (string.IsNullOrWhiteSpace(details)) return string.Empty;
            var marker = "Team key: ";
            var start = details.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (start < 0) return details;
            var end = details.IndexOf(';', start);
            var key = end < 0 ? details.Substring(start + marker.Length) : details.Substring(start + marker.Length, end - start - marker.Length);
            var replacement = "Team: " + GetSingleDisplayTeamName(key);
            return end < 0 ? details.Substring(0, start) + replacement : details.Substring(0, start) + replacement + details.Substring(end + 1);
        }

        private string Csv(string value) { return "\"" + (value ?? string.Empty).Replace("\"", "\"\"") + "\""; }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            SaveEventTitle(); SaveTeamDetails(); SaveRoundContext(); PersistSession(globalTimer.State.ToString()); base.OnClosing(e);
        }
    }
}
