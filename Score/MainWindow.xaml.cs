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
        private int configuredTeamCount = 4;
        private int[] totalScores = new int[4];
        private List<int>[] roundScores = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
        private List<int>[] roundTeamElapsed = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
        private int[] currentRoundTeamElapsed = new int[4];
        private int[] totalTeamElapsed = new int[4];
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
        private bool setupLocked;
        private bool isSynchronizingSelectors;
        private DateTime lastSessionSnapshotUtc = DateTime.MinValue;

        private string SettingsDirectory { get { return AppPaths.SettingsDirectory; } }
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
                LoadRoundContext();
                LoadSession();
                InitializeTeamState(configuredTeamCount);
                BuildTeamCards();
                EnsureScoreRecords();
                LoadTeamDetails();
                SynchronizeRoundCountSelector();
                SynchronizeTeamCountSelector();
                LoadScores();
                LoadRoundHistory();
                RestoreSessionState();
                UpdateTotalLabels();
                timerRefresh.Tick += TimerRefresh_Tick;
                UpdateTimerLabels();
                UpdateTimerControls();
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
            AppDiagnostics.Write(message);
        }

        private TeamScoreCard[] Cards { get { return TeamCardsGrid.Children.OfType<TeamScoreCard>().ToArray(); } }

        private void InitializeTeamState(int teamCount)
        {
            configuredTeamCount = Math.Max(2, Math.Min(12, teamCount));
            totalScores = new int[configuredTeamCount];
            currentRoundTeamElapsed = new int[configuredTeamCount];
            totalTeamElapsed = new int[configuredTeamCount];
            roundScores = Enumerable.Range(0, configuredTeamCount).Select(index => new List<int>()).ToArray();
            roundTeamElapsed = Enumerable.Range(0, configuredTeamCount).Select(index => new List<int>()).ToArray();
        }

        private void BuildTeamCards()
        {
            TeamCardsGrid.Children.Clear();
            var colors = new[] { "#FFEF6A9B", "#FF54B5FF", "#FF69D4A1", "#FFF4C95D", "#FFB28CFF", "#FFFF8A65", "#FF63D7D2", "#FFC5D86D", "#FFFF91B8", "#FF9DB7FF", "#FFFFC266", "#FFB7A3FF" };
            var cardHeight = configuredTeamCount <= 4 ? 340 : configuredTeamCount <= 8 ? 300 : 260;
            for (var i = 0; i < configuredTeamCount; i++)
            {
                var card = new TeamScoreCard { Height = cardHeight, DatabaseTeamName = GetTeamKey(i), TeamName = GetDefaultTeamName(i), ScoreColor = (Brush)new BrushConverter().ConvertFromString(colors[i]) };
                card.ScoreRequested += Card_ScoreRequested;
                card.TeamTimerStarted += Card_TeamTimerStarted;
                card.TeamTimerStopped += Card_TeamTimerStopped;
                card.TeamTimerResetRequested += Card_TeamTimerResetRequested;
                card.TeamDetailsChanged += Card_TeamDetailsChanged;
                TeamCardsGrid.Children.Add(card);
            }
            TeamCountLabel.Text = configuredTeamCount.ToString();
            UpdateTeamGridLayout();
        }

        private string GetDefaultTeamName(int index) { return index < 4 ? new[] { "Team Aurora", "Team Blue", "Team Emerald", "Team Gold" }[index] : "Team " + (index + 1); }
        private string GetTeamKey(int index) { return index == 0 ? "JBA 1" : index == 1 ? "JBA 2" : index == 2 ? "YVM 1" : index == 3 ? "YVM 2" : "TEAM-" + (index + 1).ToString("00"); }

        private void EnsureSettingsDirectory()
        {
            if (!Directory.Exists(SettingsDirectory)) Directory.CreateDirectory(SettingsDirectory);
        }

        private void LoadEventTitle()
        {
            try
            {
                if (!File.Exists(EventTitleFilePath)) return;
                var value = File.ReadAllText(EventTitleFilePath).Trim();
                if (!string.IsNullOrWhiteSpace(value)) EventTitleEditor.Text = value;
            }
            catch (Exception ex) { AppDiagnostics.WriteException("Could not read the saved event title.", ex); }
        }

        private void SaveEventTitle()
        {
            EventTitleEditor.Text = string.IsNullOrWhiteSpace(EventTitleEditor.Text) ? "QUIZ CHAMPIONSHIP" : EventTitleEditor.Text.Trim();
            EnsureSettingsDirectory();
            SafeFile.WriteAllText(EventTitleFilePath, EventTitleEditor.Text);
        }

        private void LoadTeamDetails()
        {
            var defaultNames = Enumerable.Range(0, configuredTeamCount).Select(GetDefaultTeamName).ToArray();
            var names = ReadSettingLines(TeamNamesFilePath, defaultNames);
            var players = ReadSettingLines(TeamPlayersFilePath, new string[0]);
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
            SafeFile.WriteAllLines(TeamNamesFilePath, Cards.Select(card => card.TeamName ?? string.Empty).ToArray());
            SafeFile.WriteAllLines(TeamPlayersFilePath, Cards.Select(card => card.TeamMembers ?? string.Empty).ToArray());
        }

        private void LoadRoundContext()
        {
            var values = ReadSettingLines(RoundContextFilePath, new string[0]);
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
            SafeFile.WriteAllLines(RoundContextFilePath, new[] { RoundLabelEditor.Text.Trim(), CurrentRoundFormat });
        }

        private string[] ReadSettingLines(string path, string[] fallback)
        {
            try { return File.Exists(path) ? File.ReadAllLines(path) : fallback; }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Could not read setting file " + path + ".", ex);
                return fallback;
            }
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
            var teamKeys = Enumerable.Range(0, configuredTeamCount).Select(GetTeamKey).ToArray();
            using (var db = new ScoreDbContext())
            {
                var existingKeys = new HashSet<string>(db.ScoreCounts.Select(record => record.Team).ToList(), StringComparer.Ordinal);
                foreach (var key in teamKeys)
                    if (!existingKeys.Contains(key)) db.ScoreCounts.Add(new ScoreCount { Team = key, ScoreValue = 0 });
                db.SaveChanges();
            }
        }

        private void LoadSession()
        {
            using (var db = new ScoreDbContext())
            {
                activeSession = db.QuizSessions.Where(session => session.IsActive).OrderByDescending(session => session.QuizSessionId).FirstOrDefault();
                if (activeSession == null)
                {
                    activeSession = new QuizSession { Title = EventTitleEditor.Text, ConfiguredTeamCount = configuredTeamCount, ConfiguredRounds = configuredRounds, CurrentRound = currentRound, Status = "NotStarted", LastUpdatedAtUtc = DateTime.UtcNow, IsActive = true };
                    db.QuizSessions.Add(activeSession);
                    db.SaveChanges();
                    db.QuizEventLogs.Add(new QuizEventLog { QuizSessionId = activeSession.QuizSessionId, OccurredAtUtc = DateTime.UtcNow, EventType = "SessionCreated", Team = string.Empty, RoundNumber = currentRound, ScoreChange = 0, ScoreAfter = 0, ElapsedSeconds = 0, Details = "New quiz session created" });
                    db.SaveChanges();
                }
                else
                {
                    configuredRounds = Math.Max(1, activeSession.ConfiguredRounds);
                    configuredTeamCount = activeSession.ConfiguredTeamCount < 2 ? 4 : Math.Min(12, activeSession.ConfiguredTeamCount);
                    currentRound = Math.Max(1, activeSession.CurrentRound);
                }
                setupLocked = !string.Equals(activeSession.Status, "NotStarted", StringComparison.OrdinalIgnoreCase);
            }
        }

        private void SynchronizeRoundCountSelector()
        {
            isSynchronizingSelectors = true;
            for (var i = 0; i < RoundCountSelector.Items.Count; i++)
            {
                var item = RoundCountSelector.Items[i] as ComboBoxItem;
                if (item != null && item.Content.ToString() == configuredRounds.ToString()) RoundCountSelector.SelectedIndex = i;
            }
            isSynchronizingSelectors = false;
            RoundNumberLabel.Text = "ROUND " + currentRound + " OF " + configuredRounds;
        }

        private void SynchronizeTeamCountSelector()
        {
            isSynchronizingSelectors = true;
            for (var i = 0; i < TeamCountSelector.Items.Count; i++)
            {
                var item = TeamCountSelector.Items[i] as ComboBoxItem;
                if (item != null && item.Content.ToString() == configuredTeamCount.ToString()) TeamCountSelector.SelectedIndex = i;
            }
            isSynchronizingSelectors = false;
        }

        private void LoadScores()
        {
            using (var db = new ScoreDbContext())
            {
                var storedScores = db.ScoreCounts.ToList()
                    .Where(record => !string.IsNullOrWhiteSpace(record.Team))
                    .GroupBy(record => record.Team)
                    .ToDictionary(group => group.Key, group => group.OrderByDescending(record => record.ScoreCountID).First().ScoreValue, StringComparer.Ordinal);
                foreach (var card in Cards)
                {
                    int score;
                    card.ScoreValue = storedScores.TryGetValue(card.DatabaseTeamName, out score) ? score : 0;
                }
            }
            UpdateLeaderIndicators();
        }

        private void LoadRoundHistory()
        {
            using (var db = new ScoreDbContext())
            {
                var history = db.QuizRoundScores.Where(round => round.QuizSessionId == activeSession.QuizSessionId)
                    .ToList()
                    .GroupBy(round => new { round.RoundNumber, round.DatabaseTeamName })
                    .Select(group => group.OrderByDescending(round => round.QuizRoundScoreId).First())
                    .OrderBy(round => round.RoundNumber)
                    .ToList();
                foreach (var round in history)
                {
                    var index = GetTeamIndex(round.DatabaseTeamName);
                    if (index < 0) continue;
                    roundScores[index].Add(round.ScoreValue);
                    roundTeamElapsed[index].Add(round.TeamElapsedSeconds);
                    totalScores[index] += round.ScoreValue;
                    totalTeamElapsed[index] += round.TeamElapsedSeconds;
                    if (round.RoundNumber == currentRound) currentRoundTeamElapsed[index] = round.TeamElapsedSeconds;
                }
                roundFinalized = history.Count(round => round.RoundNumber == currentRound) >= Cards.Length;
            }
            UpdateRoundScoreLabels();
        }

        private void RestoreSessionState()
        {
            var status = activeSession == null ? "NotStarted" : activeSession.Status ?? "NotStarted";
            if (string.Equals(status, "Stopped", StringComparison.OrdinalIgnoreCase) && !roundFinalized && Cards.All(card => card.ScoreValue == 0))
            {
                activeSession.Status = "NotStarted";
                setupLocked = false;
                PersistSession("NotStarted");
                status = "NotStarted";
            }
            var hasStarted = !string.Equals(status, "NotStarted", StringComparison.OrdinalIgnoreCase);
            if (!hasStarted)
            {
                setupLocked = false;
                foreach (var card in Cards) { card.SetScoringEnabled(false); card.SetEditingEnabled(true); }
                return;
            }

            setupLocked = true;
            SynchronizeTimerSelector(GlobalMinutesSelector, activeSession.GlobalDurationSeconds);
            SynchronizeTimerSelector(RoundMinutesSelector, activeSession.RoundDurationSeconds);

            var globalElapsed = Math.Max(0, activeSession.GlobalElapsedSeconds);
            var roundElapsed = Math.Max(0, activeSession.RoundElapsedSeconds);
            if (string.Equals(status, "Running", StringComparison.OrdinalIgnoreCase) && activeSession.LastUpdatedAtUtc.HasValue)
            {
                var unsavedSeconds = Math.Max(0, Math.Min(30, (DateTime.UtcNow - activeSession.LastUpdatedAtUtc.Value).TotalSeconds));
                globalElapsed += (int)unsavedSeconds;
                roundElapsed += (int)unsavedSeconds;
            }

            var isCompletedRound = roundFinalized || string.Equals(status, "RoundComplete", StringComparison.OrdinalIgnoreCase) || string.Equals(status, "FinalRoundComplete", StringComparison.OrdinalIgnoreCase);
            globalTimer.Restore(TimeSpan.FromSeconds(Math.Max(0, activeSession.GlobalDurationSeconds)), TimeSpan.FromSeconds(globalElapsed), isCompletedRound && currentRound >= configuredRounds ? QuizTimerState.Completed : QuizTimerState.Paused);
            roundTimer.Restore(TimeSpan.FromSeconds(Math.Max(0, activeSession.RoundDurationSeconds)), TimeSpan.FromSeconds(roundElapsed), isCompletedRound ? QuizTimerState.Completed : QuizTimerState.Paused);

            foreach (var card in Cards)
            {
                card.SetScoringEnabled(!isCompletedRound);
                card.SetEditingEnabled(!isCompletedRound);
            }
            GlobalMinutesSelector.IsEnabled = false;
            RoundMinutesSelector.IsEnabled = false;

            if (isCompletedRound)
            {
                ShowRoundAnnouncement(Cards, true);
                SetFeed(currentRound >= configuredRounds ? "The completed game was recovered safely. Review the final result or export the audit log." : "The recorded round was recovered safely. Continue when you are ready.");
            }
            else
            {
                PersistSession("Paused");
                SetFeed("The previous live session was recovered in a paused state. Confirm the scores, then choose RESUME.");
            }
        }

        private void SynchronizeTimerSelector(ComboBox selector, int durationSeconds)
        {
            var minutes = durationSeconds <= 0 ? 0 : (int)TimeSpan.FromSeconds(durationSeconds).TotalMinutes;
            for (var i = 0; i < selector.Items.Count; i++)
            {
                var item = selector.Items[i] as ComboBoxItem;
                if (item != null && GetMinutesFromText(item.Content.ToString()) == minutes)
                {
                    selector.SelectedIndex = i;
                    return;
                }
            }
        }

        private void UpdateTeamGridLayout()
        {
            if (TeamCardsGrid == null || TeamCardsViewport == null || configuredTeamCount <= 0) return;
            var availableWidth = TeamCardsViewport.ActualWidth > 0 ? TeamCardsViewport.ActualWidth : ActualWidth;
            var columns = availableWidth >= 2120 && configuredTeamCount >= 8 ? 4 : availableWidth >= 1580 && configuredTeamCount >= 6 ? 3 : availableWidth < 900 ? 1 : 2;
            columns = Math.Max(1, Math.Min(configuredTeamCount, columns));

            var baseHeight = configuredTeamCount <= 4 ? 340d : configuredTeamCount <= 8 ? 300d : 260d;
            var rows = Math.Max(1, (int)Math.Ceiling(configuredTeamCount / (double)columns));
            if (TeamCardsViewport.ActualHeight > 0)
            {
                var fitHeight = (TeamCardsViewport.ActualHeight - 8) / rows;
                baseHeight = Math.Min(baseHeight, Math.Max(230, fitHeight));
            }

            TeamCardsGrid.ColumnDefinitions.Clear();
            TeamCardsGrid.RowDefinitions.Clear();
            for (var column = 0; column < columns; column++)
                TeamCardsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            for (var row = 0; row < rows; row++)
                TeamCardsGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(baseHeight) });

            var cards = Cards;
            for (var index = 0; index < cards.Length; index++)
            {
                var card = cards[index];
                card.Height = double.NaN;
                card.MaxHeight = baseHeight;
                card.VerticalAlignment = VerticalAlignment.Stretch;
                Grid.SetColumn(card, index % columns);
                Grid.SetRow(card, index / columns);
            }
        }

        private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateTeamGridLayout();
        }

        private int GetTeamIndex(string key)
        {
            var cards = Cards;
            for (var i = 0; i < cards.Length; i++) if (cards[i].DatabaseTeamName == key) return i;
            return -1;
        }

        private TeamScoreCard GetCard(string key) { return Cards.FirstOrDefault(card => card.DatabaseTeamName == key); }
        private void Card_ScoreRequested(object sender, TeamScoreRequestedEventArgs e)
        {
            if (roundFinalized) { SetFeed("This round has already been finalized. Select NEXT ROUND before scoring again."); return; }
            var card = sender as TeamScoreCard;
            if (card != null) UpdateScore(card, e.Points, false);
        }

        private void UpdateScore(TeamScoreCard card, int change, bool isUndo)
        {
            var verb = isUndo ? "ScoreUndo" : change > 0 ? "ScoreAwarded" : "ScorePenalty";
            var details = "Team key: " + card.DatabaseTeamName + "; " + CurrentRoundContext + "; players: " + (card.TeamMembers ?? string.Empty);
            int after;
            if (!CommitScoreChange(card, change, verb, details, out after)) return;
            card.UpdateScore(after, change);
            PersistSession(GetCurrentSessionStatus());
            UpdateLeaderIndicators();
            SetFeed(card.TeamName + (change > 0 ? " awarded +" : " penalized ") + change + " point" + (Math.Abs(change) == 1 ? string.Empty : "s") + ". Score: " + after + ".");
        }

        private bool CommitScoreChange(TeamScoreCard card, int change, string eventType, string details, out int after)
        {
            after = card.ScoreValue;
            try
            {
                using (var db = new ScoreDbContext())
                using (var transaction = db.Database.BeginTransaction())
                {
                    var score = db.ScoreCounts.FirstOrDefault(record => record.Team == card.DatabaseTeamName);
                    if (score == null) throw new InvalidOperationException("The score record for " + card.DatabaseTeamName + " is missing.");
                    var candidate = (long)score.ScoreValue + change;
                    if (candidate > int.MaxValue || candidate < int.MinValue) throw new OverflowException("The score is outside the supported range.");
                    score.ScoreValue = (int)candidate;
                    after = score.ScoreValue;
                    db.QuizEventLogs.Add(new QuizEventLog
                    {
                        QuizSessionId = activeSession.QuizSessionId,
                        OccurredAtUtc = DateTime.UtcNow,
                        EventType = eventType,
                        Team = card.TeamName,
                        RoundNumber = currentRound,
                        ScoreChange = change,
                        ScoreAfter = after,
                        ElapsedSeconds = (int)globalTimer.Elapsed.TotalSeconds,
                        Details = details
                    });
                    db.SaveChanges();
                    transaction.Commit();
                }
                return true;
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Could not save a score change for " + card.DatabaseTeamName + ".", ex);
                SetFeed("The score was not changed because it could not be saved safely. Please retry.");
                MessageBox.Show("The score change could not be saved, so the on-screen score was left unchanged.\n\n" + ex.Message, "Score not saved", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        private void UndoLastScoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (roundFinalized) { SetFeed("A finalized round cannot be changed. The recorded result remains protected."); return; }
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
                candidate = events.LastOrDefault(log => log.RoundNumber == currentRound && (log.EventType == "ScoreAwarded" || log.EventType == "ScorePenalty") && !undone.Contains(log.QuizEventLogId));
            }
            if (candidate == null) { SetFeed("There is no score change left to undo in this session."); return; }
            var keyMarker = "Team key: ";
            var keyStart = (candidate.Details ?? string.Empty).IndexOf(keyMarker, StringComparison.Ordinal);
            var key = keyStart < 0 ? string.Empty : candidate.Details.Substring(keyStart + keyMarker.Length).Split(';')[0].Trim();
            var card = GetCard(key);
            if (card == null) { SetFeed("The last score entry cannot be matched to a team."); return; }
            int after;
            if (!CommitScoreChange(card, -candidate.ScoreChange, "ScoreUndo", "Reversed event " + candidate.QuizEventLogId + "; original change " + candidate.ScoreChange + "; Team key: " + card.DatabaseTeamName, out after)) return;
            card.UpdateScore(after, -candidate.ScoreChange);
            PersistSession(GetCurrentSessionStatus());
            UpdateLeaderIndicators();
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
            if (globalTimer.State == QuizTimerState.Stopped) { SetFeed("Choose START LIVE before finishing the round."); return; }
            StopActiveTeamTimer();
            roundTimer.Stop();
            var cards = Cards;
            if (!SaveCompletedRoundScores(cards)) return;
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

        private bool SaveCompletedRoundScores(TeamScoreCard[] cards)
        {
            try
            {
                using (var db = new ScoreDbContext())
                using (var transaction = db.Database.BeginTransaction())
                {
                    for (var i = 0; i < cards.Length; i++)
                    {
                        var teamName = cards[i].DatabaseTeamName;
                        if (db.QuizRoundScores.Any(round => round.QuizSessionId == activeSession.QuizSessionId && round.RoundNumber == currentRound && round.DatabaseTeamName == teamName)) continue;
                        db.QuizRoundScores.Add(new QuizRoundScore { QuizSessionId = activeSession.QuizSessionId, DatabaseTeamName = cards[i].DatabaseTeamName, RoundNumber = currentRound, ScoreValue = cards[i].ScoreValue, TeamElapsedSeconds = currentRoundTeamElapsed[i], ElapsedSeconds = (int)roundTimer.Elapsed.TotalSeconds, IsFinalized = true });
                    }
                    db.SaveChanges();
                    transaction.Commit();
                }
                PersistSession(currentRound >= configuredRounds ? "FinalRoundComplete" : "RoundComplete");
                return true;
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Could not finalize round " + currentRound + ".", ex);
                SetFeed("The round was not finalized because its result could not be saved safely.");
                MessageBox.Show("The round result could not be saved. No in-memory totals were changed; please retry.\n\n" + ex.Message, "Round not saved", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        private void ShowRoundAnnouncement(TeamScoreCard[] cards, bool isRecovery = false)
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
                if (!isRecovery) LogEvent("FinalRoundComplete", string.Empty, 0, 0, "All rounds completed");
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
            else if (globalTimer.State == QuizTimerState.Paused) roundTimer.Restore(GetRoundDuration(), TimeSpan.Zero, QuizTimerState.Paused);
            else roundTimer.Reset();
            WinnerOverlay.Visibility = Visibility.Collapsed;
            foreach (var card in Cards) card.SetScoringEnabled(true);
            RoundNumberLabel.Text = "ROUND " + currentRound + " OF " + configuredRounds;
            PersistSession(GetCurrentSessionStatus());
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
            var confirmation = MessageBox.Show("Start a new game? The current session remains in the local database, but only the new active game is available from this screen. Select No and use EXPORT LOG first if you need a CSV copy.", "Start new game", MessageBoxButton.YesNo, MessageBoxImage.Question);
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
                var newSession = new QuizSession { Title = EventTitleEditor.Text, ConfiguredTeamCount = configuredTeamCount, ConfiguredRounds = configuredRounds, CurrentRound = 1, Status = "NotStarted", LastUpdatedAtUtc = DateTime.UtcNow, IsActive = true };
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
            setupLocked = false;
            WinnerOverlay.Visibility = Visibility.Collapsed;
            foreach (var card in Cards) { card.ScoreValue = 0; card.SetScoringEnabled(false); card.SetEditingEnabled(true); card.SetFinalWinner(false); }
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
            StopActiveTeamTimer();
            globalTimer.Start(GetGlobalDuration());
            roundTimer.Start(GetRoundDuration());
            roundOvertimeAnnounced = false;
            setupLocked = true;
            activeSession.GlobalDurationSeconds = (int)GetGlobalDuration().TotalSeconds;
            activeSession.RoundDurationSeconds = (int)GetRoundDuration().TotalSeconds;
            activeSession.GlobalElapsedSeconds = 0;
            activeSession.RoundElapsedSeconds = 0;
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
            var confirmation = MessageBox.Show("Restart both event and round timers from zero? Scores already awarded will not change.", "Restart timers", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirmation != MessageBoxResult.Yes) return;
            StopActiveTeamTimer();
            globalTimer.Start(GetGlobalDuration());
            roundTimer.Start(GetRoundDuration());
            roundOvertimeAnnounced = false;
            activeSession.GlobalDurationSeconds = (int)GetGlobalDuration().TotalSeconds;
            activeSession.RoundDurationSeconds = (int)GetRoundDuration().TotalSeconds;
            activeSession.StartedAtUtc = DateTime.UtcNow;
            timerRefresh.Start();
            PersistSession("Running");
            LogEvent("TimersRestarted", string.Empty, 0, 0, "Event and round timers restarted from zero");
            UpdateTimerLabels();
            UpdateTimerControls();
            UpdateCompetitionStatus();
            SetFeed("Timers restarted using the selected event and round limits.");
        }

        private void PauseTimerButton_Click(object sender, RoutedEventArgs e)
        {
            globalTimer.Pause(); roundTimer.Pause(); StopActiveTeamTimer(); timerRefresh.Stop(); PersistSession("Paused");
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
            return GetMinutesFromText(item.Content.ToString());
        }

        private int GetMinutesFromText(string text)
        {
            var firstWord = (text ?? string.Empty).Split(' ')[0];
            int value;
            return int.TryParse(firstWord, out value) ? value : 0;
        }

        private void TimerRefresh_Tick(object sender, EventArgs e)
        {
            globalTimer.RefreshState(); roundTimer.RefreshState(); UpdateTimerLabels(); UpdateTeamTimerDisplay();
            if (roundTimer.IsOvertime && !roundOvertimeAnnounced) { roundOvertimeAnnounced = true; LogEvent("RoundOvertime", string.Empty, 0, 0, CurrentRoundContext + " timer expired"); SetFeed("Round time has expired — overtime is now being shown in red."); UpdateCompetitionStatus(); }
            if ((DateTime.UtcNow - lastSessionSnapshotUtc).TotalSeconds >= 10)
            {
                lastSessionSnapshotUtc = DateTime.UtcNow;
                PersistSession(GetCurrentSessionStatus());
            }
        }

        private void UpdateTimerLabels()
        {
            var showConfiguredValues = globalTimer.State == QuizTimerState.Stopped && !setupLocked;
            GlobalTimerLabel.Text = showConfiguredValues ? FormatSelectedDuration(GlobalMinutesSelector) : globalTimer.Duration == TimeSpan.Zero ? "NO LIMIT" : FormatTimer(globalTimer);
            RoundTimerLabel.Text = showConfiguredValues ? FormatSelectedDuration(RoundMinutesSelector) : roundTimer.Duration == TimeSpan.Zero ? "NO LIMIT" : FormatTimer(roundTimer);
            GlobalTimerLabel.Foreground = globalTimer.IsOvertime ? System.Windows.Media.Brushes.IndianRed : System.Windows.Media.Brushes.LightGreen;
            RoundTimerLabel.Foreground = roundTimer.IsOvertime ? System.Windows.Media.Brushes.IndianRed : System.Windows.Media.Brushes.LightGreen;
        }

        private string FormatSelectedDuration(ComboBox selector)
        {
            var minutes = GetSelectedMinutes(selector);
            return minutes <= 0 ? "NO LIMIT" : FormatClock(TimeSpan.FromMinutes(minutes));
        }

        private void UpdateTimerControls()
        {
            var isRunning = globalTimer.State == QuizTimerState.Running || globalTimer.State == QuizTimerState.Overtime;
            var isPaused = globalTimer.State == QuizTimerState.Paused;
            var isStopped = globalTimer.State == QuizTimerState.Stopped && !setupLocked;
            StartTimerButton.IsEnabled = isStopped;
            StartTimerButton.Content = isRunning || isPaused ? "LIVE NOW" : "START LIVE";
            PauseTimerButton.IsEnabled = isRunning;
            ResumeTimerButton.IsEnabled = isPaused;
            RestartTimerButton.IsEnabled = isRunning || isPaused;
            TeamCountSelector.IsEnabled = !setupLocked;
            RoundCountSelector.IsEnabled = !setupLocked;
            foreach (var card in Cards) card.SetLiveScoring(isRunning);
        }

        private string FormatTimer(QuizTimerService timer) { return timer.IsOvertime ? "OT " + FormatClock(timer.Elapsed - timer.Duration) : FormatClock(timer.Remaining); }
        private string FormatClock(TimeSpan value)
        {
            if (value < TimeSpan.Zero) value = TimeSpan.Zero;
            return value.TotalHours >= 1
                ? ((int)value.TotalHours).ToString("00") + ":" + value.Minutes.ToString("00") + ":" + value.Seconds.ToString("00")
                : ((int)value.TotalMinutes).ToString("00") + ":" + value.Seconds.ToString("00");
        }
        private string FormatElapsed(int seconds) { return TimeSpan.FromSeconds(seconds).ToString(@"hh\:mm\:ss"); }
        private string FormatShortElapsed(int seconds) { return TimeSpan.FromSeconds(seconds).ToString(@"mm\:ss"); }

        private void ResetTimerState()
        {
            globalTimer.Reset(); roundTimer.Reset(); ResetTeamTimer(); timerRefresh.Stop();
            GlobalMinutesSelector.IsEnabled = !setupLocked; RoundMinutesSelector.IsEnabled = !setupLocked; UpdateTimerLabels(); UpdateTimerControls();
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

        private void Card_TeamTimerResetRequested(object sender, RoutedEventArgs e)
        {
            var card = sender as TeamScoreCard;
            if (card == null) return;
            var index = GetTeamIndex(card.DatabaseTeamName);
            if (index < 0) return;
            if (card == activeTeamTimerCard)
            {
                teamTimer.Reset();
                activeTeamTimerCard = null;
            }
            currentRoundTeamElapsed[index] = 0;
            card.SetTeamTimerRunning(false);
            card.SetTeamTimerDisplay("00:00", false);
            LogEvent("TeamTimerReset", card.TeamName, 0, card.ScoreValue, "Team response timer reset for the current round");
            SetFeed("Response timer reset for " + card.TeamName + ".");
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
            if (isSynchronizingSelectors || item == null || !int.TryParse(item.Content.ToString(), out value)) return;
            if (setupLocked) { SynchronizeRoundCountSelector(); SetFeed("The round count is locked after live scoring starts. Start a NEW GAME to change it safely."); return; }
            configuredRounds = value; SynchronizeRoundCountSelector(); PersistSession(activeSession == null ? "NotStarted" : activeSession.Status); UpdateCompetitionStatus();
        }

        private void TeamCountSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var item = TeamCountSelector.SelectedItem as ComboBoxItem;
            int value;
            if (isSynchronizingSelectors || item == null || !int.TryParse(item.Content.ToString(), out value) || activeSession == null || value == configuredTeamCount) return;
            if (setupLocked || globalTimer.State != QuizTimerState.Stopped)
            {
                SynchronizeTeamCountSelector();
                SetFeed("The team count is locked after live scoring starts. Start a NEW GAME before changing it.");
                return;
            }
            configuredTeamCount = Math.Max(2, Math.Min(12, value));
            InitializeTeamState(configuredTeamCount);
            BuildTeamCards();
            LoadTeamDetails();
            foreach (var card in Cards) { card.SetScoringEnabled(false); card.SetEditingEnabled(true); }
            EnsureScoreRecords();
            activeSession.ConfiguredTeamCount = configuredTeamCount;
            PersistSession("NotStarted");
            UpdateTotalLabels();
            UpdateRoundScoreLabels();
            UpdateCompetitionStatus();
        }

        private void TimerDurationSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GlobalTimerLabel == null || RoundTimerLabel == null) return;
            UpdateTimerLabels();
        }

        private void EventTitleEditor_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) { EventTitleEditor.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next)); e.Handled = true; } }
        private void EventTitleEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            if (!TrySavePreference(SaveEventTitle, "event title")) return;
            PersistSession(activeSession == null ? "NotStarted" : GetCurrentSessionStatus());
        }

        private void RoundFormatSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RoundLabelEditor == null || !TrySavePreference(SaveRoundContext, "round setup")) return;
            SetFeed("Round format set to " + CurrentRoundFormat + ".");
        }

        private void RoundLabelEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            RoundLabelEditor.Text = string.IsNullOrWhiteSpace(RoundLabelEditor.Text) ? "Untitled round" : RoundLabelEditor.Text.Trim();
            TrySavePreference(SaveRoundContext, "round setup");
        }

        private void Card_TeamDetailsChanged(object sender, RoutedEventArgs e)
        {
            if (!TrySavePreference(SaveTeamDetails, "team names and players")) return;
            var card = sender as TeamScoreCard;
            if (card != null)
            {
                LogEvent("TeamDetailsUpdated", card.TeamName, 0, card.ScoreValue, "Team key: " + card.DatabaseTeamName + "; players saved: " + (card.TeamMembers ?? string.Empty));
                SetFeed(card.TeamName + " roster saved.");
            }
        }

        private bool TrySavePreference(Action save, string description)
        {
            try
            {
                save();
                return true;
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Could not save " + description + ".", ex);
                SetFeed("Could not save " + description + ". Check that your Windows account can write to the app data folder.");
                return false;
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (Keyboard.FocusedElement is TextBox || Keyboard.FocusedElement is ComboBox) return;
            if (e.Key == Key.Escape && AboutOverlay.Visibility == Visibility.Visible) { AboutOverlay.Visibility = Visibility.Collapsed; e.Handled = true; }
            else if (e.Key == Key.Escape && WinnerOverlay.Visibility == Visibility.Visible && (isPreviewAnnouncement || isFinalAnnouncement)) { WinnerOverlay.Visibility = Visibility.Collapsed; isPreviewAnnouncement = false; isFinalAnnouncement = false; e.Handled = true; }
            else if (e.Key == Key.Space && (globalTimer.State == QuizTimerState.Running || globalTimer.State == QuizTimerState.Overtime || globalTimer.State == QuizTimerState.Paused)) { if (globalTimer.State == QuizTimerState.Running || globalTimer.State == QuizTimerState.Overtime) PauseTimerButton_Click(this, new RoutedEventArgs()); else ResumeTimerButton_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (e.Key == Key.F11) { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; e.Handled = true; }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z) { UndoLastScoreButton_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.E) { ExportLogButton_Click(this, new RoutedEventArgs()); e.Handled = true; }
        }

        private void SetFeed(string message) { if (EventFeedLabel != null) EventFeedLabel.Text = message; }
        private void UpdateCompetitionStatus()
        {
            CompetitionStatusLabel.Text = isFinalAnnouncement ? "FINAL RESULT" : isFinalRoundComplete ? "FINAL ROUND COMPLETE" : roundFinalized ? "ROUND RECORDED" : globalTimer.State == QuizTimerState.Paused ? "PAUSED" : roundTimer.IsOvertime ? "ROUND OVERTIME" : globalTimer.State == QuizTimerState.Running || globalTimer.State == QuizTimerState.Overtime ? "LIVE" : "READY TO START";
            CompetitionStatusLabel.Foreground = globalTimer.State == QuizTimerState.Paused ? System.Windows.Media.Brushes.Gold : roundTimer.IsOvertime ? System.Windows.Media.Brushes.IndianRed : System.Windows.Media.Brushes.LightGreen;
        }

        private string GetCurrentSessionStatus()
        {
            if (isFinalRoundComplete || (roundFinalized && currentRound >= configuredRounds)) return "FinalRoundComplete";
            if (roundFinalized) return "RoundComplete";
            if (globalTimer.State == QuizTimerState.Paused) return "Paused";
            if (globalTimer.State == QuizTimerState.Running || globalTimer.State == QuizTimerState.Overtime) return "Running";
            return setupLocked ? "Paused" : "NotStarted";
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
            catch (Exception ex) { AppDiagnostics.WriteException("Could not append audit event " + type + ".", ex); }
        }

        private void PersistSession(string status)
        {
            if (activeSession == null) return;
            try
            {
                using (var db = new ScoreDbContext())
                {
                    var session = db.QuizSessions.SingleOrDefault(item => item.QuizSessionId == activeSession.QuizSessionId);
                    if (session == null) return;
                    session.Status = status;
                    session.Title = EventTitleEditor.Text;
                    session.ConfiguredRounds = configuredRounds;
                    session.CurrentRound = currentRound;
                    session.ConfiguredTeamCount = configuredTeamCount;
                    session.GlobalDurationSeconds = SafeSeconds(globalTimer.Duration);
                    session.RoundDurationSeconds = SafeSeconds(roundTimer.Duration);
                    session.GlobalElapsedSeconds = SafeSeconds(globalTimer.Elapsed);
                    session.RoundElapsedSeconds = SafeSeconds(roundTimer.Elapsed);
                    session.StartedAtUtc = activeSession.StartedAtUtc;
                    session.LastUpdatedAtUtc = DateTime.UtcNow;
                    if (status == "Paused") session.PausedAtUtc = DateTime.UtcNow;
                    if (status == "FinalRoundComplete") session.CompletedAtUtc = DateTime.UtcNow;
                    db.SaveChanges();

                    activeSession.Status = session.Status;
                    activeSession.Title = session.Title;
                    activeSession.ConfiguredRounds = session.ConfiguredRounds;
                    activeSession.ConfiguredTeamCount = session.ConfiguredTeamCount;
                    activeSession.CurrentRound = session.CurrentRound;
                    activeSession.GlobalDurationSeconds = session.GlobalDurationSeconds;
                    activeSession.RoundDurationSeconds = session.RoundDurationSeconds;
                    activeSession.GlobalElapsedSeconds = session.GlobalElapsedSeconds;
                    activeSession.RoundElapsedSeconds = session.RoundElapsedSeconds;
                    activeSession.StartedAtUtc = session.StartedAtUtc;
                    activeSession.PausedAtUtc = session.PausedAtUtc;
                    activeSession.CompletedAtUtc = session.CompletedAtUtc;
                    activeSession.LastUpdatedAtUtc = session.LastUpdatedAtUtc;
                }
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Could not persist session state " + status + ".", ex);
                SetFeed("Scores are still available, but the latest timer state could not be saved. Check the diagnostic log.");
            }
        }

        private int SafeSeconds(TimeSpan value)
        {
            if (value <= TimeSpan.Zero) return 0;
            return value.TotalSeconds >= int.MaxValue ? int.MaxValue : (int)value.TotalSeconds;
        }

        private void ExportLogButton_Click(object sender, RoutedEventArgs e)
        {
            var safeTitle = ExportSafety.SanitizeFileName(EventTitleEditor.Text, "quiz");
            var dialog = new SaveFileDialog { Title = "Export competition audit log", Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*", DefaultExt = ".csv", AddExtension = true, RestoreDirectory = true, FileName = safeTitle + "-audit-log.csv" };
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
                csv.AppendLine("Session ID,Title,Status,Configured teams,Configured rounds,Whole event limit seconds,Each round limit seconds,Started UTC,Paused UTC,Completed UTC,Active");
                csv.AppendLine(string.Join(",", session.QuizSessionId, Csv(session.Title), Csv(session.Status), session.ConfiguredTeamCount, session.ConfiguredRounds, session.GlobalDurationSeconds, session.RoundDurationSeconds, Csv(session.StartedAtUtc.HasValue ? session.StartedAtUtc.Value.ToString("u") : string.Empty), Csv(session.PausedAtUtc.HasValue ? session.PausedAtUtc.Value.ToString("u") : string.Empty), Csv(session.CompletedAtUtc.HasValue ? session.CompletedAtUtc.Value.ToString("u") : string.Empty), session.IsActive));
                csv.AppendLine();
                csv.AppendLine("TEAMS ON SCREEN");
                csv.AppendLine("Team,Players,Recorded total,Current round score,Overall response seconds,Overall response time");
                for (var i = 0; i < Cards.Length; i++) csv.AppendLine(string.Join(",", Csv(Cards[i].TeamName), Csv(Cards[i].TeamMembers), totalScores[i], roundFinalized ? 0 : Cards[i].ScoreValue, totalTeamElapsed[i], Csv(FormatElapsed(totalTeamElapsed[i]))));
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
            try
            {
                File.WriteAllText(dialog.FileName, csv.ToString(), new UTF8Encoding(true));
                SetFeed("Audit log exported successfully.");
                MessageBox.Show("The competition audit log was exported successfully.", "Export complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Audit export failed for " + dialog.FileName + ".", ex);
                SetFeed("The audit log could not be exported to the selected location.");
                MessageBox.Show("The audit log could not be written. Choose another folder and try again.\n\n" + ex.Message, "Export failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
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
            return end < 0 ? details.Substring(0, start) + replacement : details.Substring(0, start) + replacement + "; " + details.Substring(end + 1).TrimStart();
        }

        private string Csv(string value) { return ExportSafety.ToCsvCell(value); }

        protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                StopActiveTeamTimer();
                SaveEventTitle();
                SaveTeamDetails();
                SaveRoundContext();
                PersistSession(GetCurrentSessionStatus());
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Could not save all settings while closing.", ex);
            }
            base.OnClosing(e);
        }
    }
}
