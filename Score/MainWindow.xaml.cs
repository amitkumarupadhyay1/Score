using System;
using System.Linq;
using System.Windows;
using System.IO;
using System.Collections.Generic;
using System.Windows.Threading;
using Microsoft.Win32;
using System.Text;

namespace Score
{
    public partial class MainWindow : Window
    {
        private int currentRound = 1;
        private int configuredRounds = 3;
        private readonly int[] totalScores = new int[4];
        private readonly List<int>[] roundScores = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
        private readonly int[] currentRoundTeamElapsed = new int[4];
        private readonly int[] totalTeamElapsed = new int[4];
        private readonly QuizTimerService globalTimer = new QuizTimerService();
        private readonly QuizTimerService roundTimer = new QuizTimerService();
        private readonly QuizTimerService teamTimer = new QuizTimerService();
        private readonly DispatcherTimer timerRefresh = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        private QuizSession activeSession;
        private TeamScoreCard activeTeamTimerCard;
        private bool isFinalAnnouncement;
        private bool isFinalRoundComplete;

        private string EventTitleFilePath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Score", "event-title.txt"); }
        }

        public MainWindow()
        {
            InitializeComponent();
            LoadEventTitle();
            LoadTeamNames();
            EnsureScoreRecords();
            LoadSession();
            LoadScores();
            LoadRoundHistory();
            UpdateTotalLabels();
            timerRefresh.Tick += TimerRefresh_Tick;
            UpdateTimerLabels();
        }

        private string TeamNamesFilePath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Score", "team-names.txt"); }
        }

        private void LoadEventTitle()
        {
            if (File.Exists(EventTitleFilePath))
            {
                var savedTitle = File.ReadAllText(EventTitleFilePath).Trim();
                if (!string.IsNullOrWhiteSpace(savedTitle))
                    EventTitleEditor.Text = savedTitle;
            }
        }

        private void SaveEventTitle()
        {
            var title = string.IsNullOrWhiteSpace(EventTitleEditor.Text) ? "QUIZ SCOREBOARD" : EventTitleEditor.Text.Trim();
            EventTitleEditor.Text = title;
            var directory = Path.GetDirectoryName(EventTitleFilePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(EventTitleFilePath, title);
        }

        private void LoadTeamNames()
        {
            var defaults = new[] { "APS", "Sunbeam", "JB Academy", "AIS" };
            var names = File.Exists(TeamNamesFilePath) ? File.ReadAllLines(TeamNamesFilePath) : defaults;
            var cards = new[] { Team1Card, Team2Card, Team3Card, Team4Card };

            for (var index = 0; index < cards.Length; index++)
                cards[index].TeamName = index < names.Length && !string.IsNullOrWhiteSpace(names[index]) ? names[index] : defaults[index];
        }

        private void SaveTeamNames()
        {
            var directory = Path.GetDirectoryName(TeamNamesFilePath);
            if (!Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllLines(TeamNamesFilePath, new[] { Team1Card.TeamName, Team2Card.TeamName, Team3Card.TeamName, Team4Card.TeamName });
        }

        private void EventTitleEditor_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                EventTitleEditor.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next));
                e.Handled = true;
            }
        }

        private void EventTitleEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            SaveEventTitle();
        }

        private void LoadScores()
        {
            using (var score = new ScoreDbContext())
            {
                Team1Card.ScoreValue = GetScoreFromDb(score, Team1Card);
                Team2Card.ScoreValue = GetScoreFromDb(score, Team2Card);
                Team3Card.ScoreValue = GetScoreFromDb(score, Team3Card);
                Team4Card.ScoreValue = GetScoreFromDb(score, Team4Card);
            }

            UpdateLeaderIndicators();
        }

        private void EnsureScoreRecords()
        {
            var teamNames = new[] { "JBA 1", "JBA 2", "YVM 1", "YVM 2" };
            using (var score = new ScoreDbContext())
            {
                score.Database.Initialize(true);
                score.Database.ExecuteSqlCommand("CREATE TABLE IF NOT EXISTS QuizEventLogs (QuizEventLogId INTEGER NOT NULL PRIMARY KEY AUTOINCREMENT, QuizSessionId INTEGER NOT NULL, OccurredAtUtc TEXT NOT NULL, EventType TEXT, Team TEXT, RoundNumber INTEGER NOT NULL, ScoreChange INTEGER NOT NULL, ScoreAfter INTEGER NOT NULL, ElapsedSeconds INTEGER NOT NULL, Details TEXT)");
                foreach (var teamName in teamNames)
                    if (!score.ScoreCounts.Any(record => record.Team == teamName))
                        score.ScoreCounts.Add(new ScoreCount { Team = teamName, ScoreValue = 0 });
                score.SaveChanges();
            }
        }

        private void LoadSession()
        {
            using (var score = new ScoreDbContext())
            {
                activeSession = score.QuizSessions.SingleOrDefault(session => session.IsActive);
                if (activeSession == null)
                {
                    activeSession = new QuizSession
                    {
                        Title = EventTitleEditor.Text,
                        ConfiguredRounds = configuredRounds,
                        CurrentRound = currentRound,
                        Status = "NotStarted",
                        GlobalDurationSeconds = 0,
                        RoundDurationSeconds = 0,
                        IsActive = true
                    };
                    score.QuizSessions.Add(activeSession);
                    score.SaveChanges();
                }
                else
                {
                    configuredRounds = activeSession.ConfiguredRounds;
                    currentRound = activeSession.CurrentRound;
                    RoundNumberLabel.Text = "ROUND " + currentRound;
                }
            }
        }

        private void LoadRoundHistory()
        {
            using (var score = new ScoreDbContext())
            {
                var history = score.QuizRoundScores
                    .Where(round => round.QuizSessionId == activeSession.QuizSessionId)
                    .OrderBy(round => round.RoundNumber)
                    .ToList();

                foreach (var round in history)
                {
                    var teamIndex = GetTeamIndex(round.DatabaseTeamName);
                    if (teamIndex >= 0)
                    {
                        roundScores[teamIndex].Add(round.ScoreValue);
                        totalScores[teamIndex] += round.ScoreValue;
                        totalTeamElapsed[teamIndex] += round.TeamElapsedSeconds;
                    }
                }
            }
            UpdateRoundScoreLabels();
        }

        private int GetTeamIndex(string databaseTeamName)
        {
            var cards = new[] { Team1Card, Team2Card, Team3Card, Team4Card };
            for (var index = 0; index < cards.Length; index++)
                if (cards[index].DatabaseTeamName == databaseTeamName)
                    return index;
            return -1;
        }

        private int GetScoreFromDb(ScoreDbContext db, TeamScoreCard card)
        {
            var scoreRecord = db.ScoreCounts.FirstOrDefault(m => m.Team == card.DatabaseTeamName);
            return scoreRecord != null ? scoreRecord.ScoreValue : 0;
        }

        private void Card_ScoreDecreased(object sender, RoutedEventArgs e)
        {
            var card = sender as TeamScoreCard;
            if (card != null)
            {
                UpdateScore(card, -1);
            }
        }

        private void Card_ScoreIncreased(object sender, RoutedEventArgs e)
        {
            var card = sender as TeamScoreCard;
            if (card != null)
            {
                UpdateScore(card, 1);
            }
        }

        private void UpdateScore(TeamScoreCard card, int change)
        {
            var updatedScore = 0;
            var scoreWasUpdated = false;
            using (var score = new ScoreDbContext())
            {
                var dbTeamName = card.DatabaseTeamName;
                var scoreRecord = score.ScoreCounts.FirstOrDefault(m => m.Team == dbTeamName);
                if (scoreRecord != null)
                {
                    scoreRecord.ScoreValue += change;
                    score.SaveChanges();

                    card.UpdateScore(scoreRecord.ScoreValue, change);
                    PersistSession("Running");
                    UpdateLeaderIndicators();
                    updatedScore = scoreRecord.ScoreValue;
                    scoreWasUpdated = true;
                }
            }

            if (scoreWasUpdated)
                LogEvent(change > 0 ? "ScoreIncreased" : "ScoreDecreased", card.TeamName, change, updatedScore, "Score changed");
        }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            SaveEventTitle();
            SaveTeamNames();
            PersistSession(globalTimer.State.ToString());
            base.OnClosing(e);
        }

        private void ResetButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Reset all team scores to zero?",
                "Reset scoreboard",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            ResetScores();
        }

        private void RoundCountSelector_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (RoundCountSelector.SelectedItem is System.Windows.Controls.ComboBoxItem item && int.TryParse(item.Content.ToString(), out var rounds))
                configuredRounds = rounds;
        }

        private void FinishRoundButton_Click(object sender, RoutedEventArgs e)
        {
            StopActiveTeamTimer();
            roundTimer.Stop();
            var cards = new[] { Team1Card, Team2Card, Team3Card, Team4Card };
            SaveCompletedRoundScores(cards);
            for (var index = 0; index < cards.Length; index++)
                totalScores[index] += cards[index].ScoreValue;

            for (var index = 0; index < cards.Length; index++)
                roundScores[index].Add(cards[index].ScoreValue);

            for (var index = 0; index < cards.Length; index++)
                totalTeamElapsed[index] += currentRoundTeamElapsed[index];

            UpdateTotalLabels();
            UpdateRoundScoreLabels();
            ShowRoundAnnouncement(cards);
            UpdateTimerLabels();
            LogEvent("RoundFinished", string.Empty, 0, 0, "Round " + currentRound + " completed");
        }

        private void ShowRoundAnnouncement(TeamScoreCard[] cards)
        {
            isFinalRoundComplete = currentRound >= configuredRounds;
            isFinalAnnouncement = false;
            var highestScore = cards.Max(card => card.ScoreValue);
            var winners = cards.Where(card => card.ScoreValue == highestScore).ToArray();
            WinnerContext.Text = isFinalRoundComplete ? "FINAL ROUND COMPLETE" : "ROUND " + currentRound + " WINNER";
            WinnerTitle.Text = highestScore == 0 ? "NO WINNER" : winners.Length == 1 ? winners[0].TeamName : "TIE";
            WinnerScore.Text = highestScore == 0
                ? "SCORES ARE ZERO"
                : winners.Length == 1
                ? highestScore + " POINTS"
                : string.Join("  /  ", winners.Select(card => card.TeamName)) + "\n" + highestScore + " POINTS";
            FinalStandings.Visibility = Visibility.Collapsed;
            WinnerActionButton.Content = isFinalRoundComplete ? "ANNOUNCE FINAL WINNER" : "NEXT ROUND";
            if (isFinalRoundComplete)
            {
                globalTimer.Stop();
                PersistSession("FinalRoundComplete");
                LogEvent("FinalRoundComplete", string.Empty, 0, 0, "Final round completed");
            }
            WinnerOverlay.Visibility = Visibility.Visible;
            foreach (var card in cards)
                card.SetScoringEnabled(false);
        }

        private void WinnerActionButton_Click(object sender, RoutedEventArgs e)
        {
            if (isFinalAnnouncement)
            {
                NewGameButton_Click(sender, e);
                return;
            }

            if (isFinalRoundComplete)
            {
                ShowFinalAnnouncement();
                return;
            }

            currentRound++;
            ResetActiveRoundScores();
            ResetTeamTimer();
            Array.Clear(currentRoundTeamElapsed, 0, currentRoundTeamElapsed.Length);
            roundTimer.Start(GetRoundDuration());
            activeSession.CurrentRound = currentRound;
            PersistSession("Running");
            WinnerOverlay.Visibility = Visibility.Collapsed;
            foreach (var card in new[] { Team1Card, Team2Card, Team3Card, Team4Card })
                card.SetScoringEnabled(true);
            RoundNumberLabel.Text = "ROUND " + currentRound;
            UpdateTimerLabels();
        }

        private void UpdateLeaderIndicators()
        {
            var highestScore = Math.Max(Math.Max(Team1Card.ScoreValue, Team2Card.ScoreValue), Math.Max(Team3Card.ScoreValue, Team4Card.ScoreValue));
            var hasLeader = highestScore > 0;
            Team1Card.SetLeading(hasLeader && Team1Card.ScoreValue == highestScore);
            Team2Card.SetLeading(hasLeader && Team2Card.ScoreValue == highestScore);
            Team3Card.SetLeading(hasLeader && Team3Card.ScoreValue == highestScore);
            Team4Card.SetLeading(hasLeader && Team4Card.ScoreValue == highestScore);

            var cards = new[] { Team1Card, Team2Card, Team3Card, Team4Card };
            foreach (var card in cards)
            {
                var rank = 1;
                foreach (var otherCard in cards)
                    if (otherCard.ScoreValue > card.ScoreValue)
                        rank++;
                card.SetRanking(rank);
                card.SetDangerState(card.ScoreValue < 0);
            }
        }

        private void ShowFinalAnnouncement()
        {
            var cards = new[] { Team1Card, Team2Card, Team3Card, Team4Card };
            var highestTotal = totalScores.Max();
            var winners = cards.Where((card, index) => totalScores[index] == highestTotal).ToArray();
            isFinalAnnouncement = true;
            WinnerContext.Text = "FINAL RESULT";
            WinnerTitle.Text = winners.Length == 1 ? winners[0].TeamName : "TIE";
            WinnerScore.Text = highestTotal + " POINTS  🏆";
            var ranking = Enumerable.Range(0, cards.Length).OrderByDescending(index => totalScores[index]).ToArray();
            var fastestTeamTime = totalTeamElapsed.Where(seconds => seconds > 0).DefaultIfEmpty().Min();
            FinalStandings.Text = string.Join("\n", ranking.Select((index, rank) =>
                (rank + 1) + ". " + cards[index].TeamName + "    " + string.Join("  |  ", roundScores[index].Select((score, round) => "R" + (round + 1) + ": " + score)) + "    TOTAL " + totalScores[index] + "    TIME " + FormatElapsed(totalTeamElapsed[index]) + (totalTeamElapsed[index] > 0 && totalTeamElapsed[index] == fastestTeamTime ? "  FASTEST" : "")));
            FinalStandings.Visibility = Visibility.Visible;
            WinnerActionButton.Content = "NEW GAME";
            LogEvent("FinalWinnerAnnounced", string.Join(" / ", winners.Select(card => card.TeamName)), 0, highestTotal, "Final winner announced");
            for (var index = 0; index < cards.Length; index++)
            {
                cards[index].SetFinalWinner(totalScores[index] == highestTotal);
                cards[index].SetScoringEnabled(false);
            }
        }

        private void AnnounceWinnerButton_Click(object sender, RoutedEventArgs e)
        {
            var cards = new[] { Team1Card, Team2Card, Team3Card, Team4Card };
            var highestScore = cards.Max(card => card.ScoreValue);
            var winners = cards.Where(card => card.ScoreValue == highestScore).ToArray();

            WinnerTitle.Text = highestScore == 0 ? "NO WINNER" : winners.Length == 1 ? winners[0].TeamName : "TIE";
            WinnerScore.Text = highestScore == 0
                ? "SCORES ARE ZERO"
                : winners.Length == 1
                ? highestScore + " POINTS"
                : string.Join("  /  ", winners.Select(card => card.TeamName)) + "\n" + highestScore + " POINTS";
            WinnerOverlay.Visibility = Visibility.Visible;
            foreach (var card in cards)
                card.SetScoringEnabled(false);
        }

        private void NewGameButton_Click(object sender, RoutedEventArgs e)
        {
            WinnerOverlay.Visibility = Visibility.Collapsed;
            foreach (var card in new[] { Team1Card, Team2Card, Team3Card, Team4Card })
                card.SetScoringEnabled(true);
            ResetScores();
            LogEvent("GameReset", string.Empty, 0, 0, "New game started");
        }

        private void ResetScores()
        {
            Array.Clear(totalScores, 0, totalScores.Length);
            Array.Clear(currentRoundTeamElapsed, 0, currentRoundTeamElapsed.Length);
            Array.Clear(totalTeamElapsed, 0, totalTeamElapsed.Length);
            foreach (var scores in roundScores)
                scores.Clear();
            ClearPersistedRoundHistory();
            currentRound = 1;
            isFinalRoundComplete = false;
            isFinalAnnouncement = false;
            RoundNumberLabel.Text = "ROUND 1";
            ResetActiveRoundScores();
            UpdateTotalLabels();
            UpdateRoundScoreLabels();
            ResetTimerState();
            PersistSession("NotStarted");
        }

        private void ClearPersistedRoundHistory()
        {
            using (var score = new ScoreDbContext())
            {
                var history = score.QuizRoundScores.Where(round => round.QuizSessionId == activeSession.QuizSessionId).ToList();
                score.QuizRoundScores.RemoveRange(history);
                score.SaveChanges();
            }
        }

        private void ResetActiveRoundScores()
        {
            using (var score = new ScoreDbContext())
            {
                foreach (var scoreRecord in score.ScoreCounts)
                    scoreRecord.ScoreValue = 0;
                score.SaveChanges();
            }

            Team1Card.ScoreValue = 0;
            Team2Card.ScoreValue = 0;
            Team3Card.ScoreValue = 0;
            Team4Card.ScoreValue = 0;
            UpdateLeaderIndicators();
        }

        private void UpdateTotalLabels()
        {
            Team1Card.SetTotalScore(totalScores[0]);
            Team2Card.SetTotalScore(totalScores[1]);
            Team3Card.SetTotalScore(totalScores[2]);
            Team4Card.SetTotalScore(totalScores[3]);
        }

        private void UpdateRoundScoreLabels()
        {
            var cards = new[] { Team1Card, Team2Card, Team3Card, Team4Card };
            for (var index = 0; index < cards.Length; index++)
            {
                var history = roundScores[index].Select((score, round) => "R" + (round + 1) + " " + score);
                cards[index].SetRoundScores(string.Join("  |  ", history));
            }
        }

        private void LogEvent(string eventType, string team, int scoreChange, int scoreAfter, string details)
        {
            if (activeSession == null)
                return;

            try
            {
                using (var score = new ScoreDbContext())
                {
                    score.QuizEventLogs.Add(new QuizEventLog
                    {
                        QuizSessionId = activeSession.QuizSessionId,
                        OccurredAtUtc = DateTime.UtcNow,
                        EventType = eventType,
                        Team = team,
                        RoundNumber = currentRound,
                        ScoreChange = scoreChange,
                        ScoreAfter = scoreAfter,
                        ElapsedSeconds = (int)globalTimer.Elapsed.TotalSeconds,
                        Details = details
                    });
                    score.SaveChanges();
                }
            }
            catch (Exception)
            {
                // Logging must never interrupt scoring or timer controls.
            }
        }

        private void ExportLogButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Title = "Export quiz log",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = (EventTitleEditor.Text.Trim().Length == 0 ? "quiz" : EventTitleEditor.Text.Trim()) + "-log.csv"
            };

            if (dialog.ShowDialog() != true)
                return;

            var csv = new StringBuilder();
            csv.AppendLine("QUIZ SUMMARY");
            csv.AppendLine("Title," + Csv(EventTitleEditor.Text));
            csv.AppendLine("Exported UTC," + Csv(DateTime.UtcNow.ToString("u")));
            csv.AppendLine("Configured rounds," + configuredRounds);
            csv.AppendLine();
            csv.AppendLine("ROUND RESULTS");
            csv.AppendLine("Round,Team,Score,Round elapsed seconds,Team elapsed seconds");

            using (var score = new ScoreDbContext())
            {
                var rounds = score.QuizRoundScores
                    .Where(round => round.QuizSessionId == activeSession.QuizSessionId)
                    .OrderBy(round => round.RoundNumber)
                    .ThenBy(round => round.DatabaseTeamName)
                    .ToList();

                foreach (var round in rounds)
                {
                    var card = new[] { Team1Card, Team2Card, Team3Card, Team4Card }.FirstOrDefault(item => item.DatabaseTeamName == round.DatabaseTeamName);
                    csv.AppendLine(round.RoundNumber + "," + Csv(card == null ? round.DatabaseTeamName : card.TeamName) + "," + round.ScoreValue + "," + round.ElapsedSeconds + "," + round.TeamElapsedSeconds);
                }

                csv.AppendLine();
                csv.AppendLine("EVENT TIMELINE");
                csv.AppendLine("Timestamp UTC,Event,Team,Round,Score change,Score after,Elapsed seconds,Details");
                var events = score.QuizEventLogs
                    .Where(item => item.QuizSessionId == activeSession.QuizSessionId)
                    .OrderBy(item => item.OccurredAtUtc)
                    .ToList();

                foreach (var item in events)
                    csv.AppendLine(string.Join(",", Csv(item.OccurredAtUtc.ToString("u")), Csv(item.EventType), Csv(item.Team), item.RoundNumber, item.ScoreChange, item.ScoreAfter, item.ElapsedSeconds, Csv(item.Details)));
            }

            File.WriteAllText(dialog.FileName, csv.ToString(), Encoding.UTF8);
            MessageBox.Show("Quiz log exported successfully.", "Export complete", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private string Csv(string value)
        {
            if (value == null)
                return string.Empty;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }

        private TimeSpan GetRoundDuration()
        {
            return TimeSpan.FromMinutes(GetSelectedMinutes(RoundMinutesSelector));
        }

        private TimeSpan GetGlobalDuration()
        {
            return TimeSpan.FromMinutes(GetSelectedMinutes(GlobalMinutesSelector));
        }

        private int GetSelectedMinutes(System.Windows.Controls.ComboBox selector)
        {
            var item = selector.SelectedItem as System.Windows.Controls.ComboBoxItem;
            int minutes;
            return item != null && int.TryParse(item.Content.ToString(), out minutes) ? minutes : 0;
        }

        private void StartTimerButton_Click(object sender, RoutedEventArgs e)
        {
            globalTimer.Start(GetGlobalDuration());
            roundTimer.Start(GetRoundDuration());
            activeSession.GlobalDurationSeconds = (int)GetGlobalDuration().TotalSeconds;
            activeSession.RoundDurationSeconds = (int)GetRoundDuration().TotalSeconds;
            activeSession.StartedAtUtc = DateTime.UtcNow;
            PersistSession("Running");
            GlobalMinutesSelector.IsEnabled = false;
            RoundMinutesSelector.IsEnabled = false;
            timerRefresh.Start();
            UpdateTimerLabels();
            LogEvent("TimerStarted", string.Empty, 0, 0, "Global and round timers started");
        }

        private void PauseTimerButton_Click(object sender, RoutedEventArgs e)
        {
            globalTimer.Pause();
            roundTimer.Pause();
            PersistSession("Paused");
            UpdateTimerLabels();
            LogEvent("TimerPaused", string.Empty, 0, 0, "Global and round timers paused");
        }

        private void ResumeTimerButton_Click(object sender, RoutedEventArgs e)
        {
            globalTimer.Resume();
            roundTimer.Resume();
            PersistSession("Running");
            timerRefresh.Start();
            UpdateTimerLabels();
            LogEvent("TimerResumed", string.Empty, 0, 0, "Global and round timers resumed");
        }

        private void TimerRefresh_Tick(object sender, EventArgs e)
        {
            globalTimer.RefreshState();
            roundTimer.RefreshState();
            UpdateTimerLabels();
            UpdateTeamTimerDisplay();
        }

        private void UpdateTimerLabels()
        {
            GlobalTimerLabel.Text = globalTimer.Duration == TimeSpan.Zero ? "NO LIMIT" : FormatTimer(globalTimer);
            RoundTimerLabel.Text = roundTimer.Duration == TimeSpan.Zero ? "NO LIMIT" : FormatTimer(roundTimer);
            GlobalTimerLabel.Foreground = globalTimer.IsOvertime ? System.Windows.Media.Brushes.IndianRed : System.Windows.Media.Brushes.LightGreen;
            RoundTimerLabel.Foreground = roundTimer.IsOvertime ? System.Windows.Media.Brushes.IndianRed : System.Windows.Media.Brushes.LightGreen;
        }

        private string FormatTimer(QuizTimerService timer)
        {
            return timer.IsOvertime ? "OT " + timer.Elapsed.ToString(@"mm\:ss") : timer.Remaining.ToString(@"mm\:ss");
        }

        private void ResetTimerState()
        {
            globalTimer.Reset();
            roundTimer.Reset();
            ResetTeamTimer();
            timerRefresh.Stop();
            GlobalMinutesSelector.IsEnabled = true;
            RoundMinutesSelector.IsEnabled = true;
            UpdateTimerLabels();
        }

        private void Card_TeamTimerStarted(object sender, RoutedEventArgs e)
        {
            var card = sender as TeamScoreCard;
            if (card == null)
                return;

            StopActiveTeamTimer();
            teamTimer.Start(TimeSpan.Zero);
            activeTeamTimerCard = card;
            card.SetTeamTimerRunning(true);
            timerRefresh.Start();
            UpdateTeamTimerDisplay();
            LogEvent("TeamTimerStarted", card.TeamName, 0, card.ScoreValue, "Team timer started");
        }

        private void Card_TeamTimerStopped(object sender, RoutedEventArgs e)
        {
            var card = sender as TeamScoreCard;
            if (card == activeTeamTimerCard)
            {
                StopActiveTeamTimer();
                LogEvent("TeamTimerStopped", card.TeamName, 0, card.ScoreValue, "Team timer stopped");
            }
        }

        private void StopActiveTeamTimer()
        {
            if (activeTeamTimerCard == null)
                return;

            teamTimer.Stop();
            var teamIndex = GetTeamIndex(activeTeamTimerCard.DatabaseTeamName);
            if (teamIndex >= 0)
                currentRoundTeamElapsed[teamIndex] += (int)teamTimer.Elapsed.TotalSeconds;
            activeTeamTimerCard.SetTeamTimerRunning(false);
            UpdateTeamTimerDisplay();
            activeTeamTimerCard = null;
        }

        private void ResetTeamTimer()
        {
            StopActiveTeamTimer();
            teamTimer.Reset();
            foreach (var card in new[] { Team1Card, Team2Card, Team3Card, Team4Card })
            {
                card.SetTeamTimerRunning(false);
                card.SetTeamTimerDisplay("TEAM --:--", false);
            }
        }

        private string FormatElapsed(int totalSeconds)
        {
            return TimeSpan.FromSeconds(totalSeconds).ToString(@"hh\:mm\:ss");
        }

        private void UpdateTeamTimerDisplay()
        {
            if (activeTeamTimerCard != null)
                activeTeamTimerCard.SetTeamTimerDisplay("TEAM " + teamTimer.Elapsed.ToString(@"mm\:ss"), teamTimer.IsOvertime);
        }

        private void SaveCompletedRoundScores(TeamScoreCard[] cards)
        {
            using (var score = new ScoreDbContext())
            {
                for (var index = 0; index < cards.Length; index++)
                {
                    if (score.QuizRoundScores.Any(round => round.QuizSessionId == activeSession.QuizSessionId && round.RoundNumber == currentRound && round.DatabaseTeamName == cards[index].DatabaseTeamName))
                        continue;

                    score.QuizRoundScores.Add(new QuizRoundScore
                    {
                        QuizSessionId = activeSession.QuizSessionId,
                        DatabaseTeamName = cards[index].DatabaseTeamName,
                        RoundNumber = currentRound,
                        ScoreValue = cards[index].ScoreValue,
                        TeamElapsedSeconds = currentRoundTeamElapsed[index],
                        ElapsedSeconds = (int)roundTimer.Elapsed.TotalSeconds,
                        IsFinalized = true
                    });
                }
                score.SaveChanges();
            }
            PersistSession(currentRound >= configuredRounds ? "FinalRoundComplete" : "RoundComplete");
        }

        private void PersistSession(string status)
        {
            if (activeSession == null)
                return;

            using (var score = new ScoreDbContext())
            {
                var session = score.QuizSessions.Single(item => item.QuizSessionId == activeSession.QuizSessionId);
                session.Status = status;
                session.Title = EventTitleEditor.Text;
                session.ConfiguredRounds = configuredRounds;
                session.CurrentRound = currentRound;
                session.PausedAtUtc = status == "Paused" ? DateTime.UtcNow : session.PausedAtUtc;
                session.CompletedAtUtc = status == "FinalRoundComplete" ? DateTime.UtcNow : session.CompletedAtUtc;
                score.SaveChanges();

                activeSession.Status = session.Status;
                activeSession.Title = session.Title;
                activeSession.ConfiguredRounds = session.ConfiguredRounds;
                activeSession.CurrentRound = session.CurrentRound;
                activeSession.PausedAtUtc = session.PausedAtUtc;
                activeSession.CompletedAtUtc = session.CompletedAtUtc;
            }
        }
    }
}