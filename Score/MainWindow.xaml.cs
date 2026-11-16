using System;
using System.Collections.Generic;
using System.Globalization;
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
        private const string DefaultEventTitle = "QUIZ CHAMPIONSHIP";
        private const string DefaultRoundName = "General Knowledge";
        private const int DefaultTeamCount = 4;
        private const int DefaultRoundCount = 3;
        private const int DefaultRoundDurationSeconds = 120;
        private int currentRound = 1;
        private int configuredRounds = 3;
        private int configuredTeamCount = 4;
        private long[] totalScores = new long[4];
        private List<int>[] roundScores = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
        private List<int>[] roundTeamElapsed = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
        private TimeSpan[] currentRoundTeamElapsed = new TimeSpan[4];
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
        private bool chimesMuted;
        private ProjectorWindow projector;
        private ProjectorTheme projectorTheme;
        private bool synchronizingProjectorTheme;
        private DateTime lastSessionSnapshotUtc = DateTime.MinValue;

        private string SettingsDirectory
        {
            get { return activeSession != null && activeSession.UsesSessionSettings ? GetSessionSettingsDirectory(activeSession.QuizSessionId) : AppPaths.SettingsDirectory; }
        }
        private string ChimeSettingsPath { get { return Path.Combine(AppPaths.SettingsDirectory, "chimes-muted.txt"); } }
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
                LoadSession();
                ProjectorTheme storedTheme;
                if (!Enum.TryParse(ReadSettingLines(Path.Combine(AppPaths.SettingsDirectory, "projector-theme.txt"), new string[0]).FirstOrDefault(), out storedTheme) || !Enum.IsDefined(typeof(ProjectorTheme), storedTheme)) storedTheme = ProjectorTheme.Dark;
                SetProjectorTheme(storedTheme, false);
                RestoreQuestion();
                if (activeSession.UsesSessionSettings) EventTitleEditor.Text = activeSession.Title;
                LoadRoundContext();
                chimesMuted = ReadSettingLines(ChimeSettingsPath, new string[0]).FirstOrDefault() == "true";
                UpdateChimeButton();
                InitializeTeamState(configuredTeamCount);
                BuildTeamCards();
                EnsureScoreRecords();
                LoadTeamDetails();
                SynchronizeRoundCountSelector();
                SynchronizeTeamCountSelector();
                LoadScores();
                LoadRoundHistory();
                UpdateLeaderIndicators();
                RestoreSessionState();
                UpdateTotalLabels();
                timerRefresh.Tick += TimerRefresh_Tick;
                UpdateTimerLabels();
                UpdateTimerControls();
                UpdateCompetitionStatus();
                UpdateQuestionControls();
                saveRetryTimer.Tick += SaveRetryTimer_Tick;
                saveRetryTimer.Start();
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
            totalScores = new long[configuredTeamCount];
            currentRoundTeamElapsed = new TimeSpan[configuredTeamCount];
            totalTeamElapsed = new int[configuredTeamCount];
            roundScores = Enumerable.Range(0, configuredTeamCount).Select(index => new List<int>()).ToArray();
            roundTeamElapsed = Enumerable.Range(0, configuredTeamCount).Select(index => new List<int>()).ToArray();
        }

        private void BuildTeamCards()
        {
            TeamCardsGrid.Children.Clear();
            var colors = new[] { "#FF9B1C54", "#FF005A9C", "#FF176B45", "#FF765000", "#FF593C8F", "#FFA33E00", "#FF006B70", "#FF526900", "#FF9B315C", "#FF3C54A5", "#FF805100", "#FF5A4398" };
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
            RefreshProjector();
        }

        private string GetDefaultTeamName(int index) { return index < 4 ? new[] { "Team Aurora", "Team Blue", "Team Emerald", "Team Gold" }[index] : "Team " + (index + 1); }
        private string GetTeamKey(int index) { return index == 0 ? "JBA 1" : index == 1 ? "JBA 2" : index == 2 ? "YVM 1" : index == 3 ? "YVM 2" : "TEAM-" + (index + 1).ToString("00"); }

        private void UpdateTeamGridLayout()
        {
            if (TeamCardsGrid == null || TeamCardsViewport == null || configuredTeamCount <= 0) return;
            var availableWidth = TeamCardsViewport.ActualWidth > 0 ? TeamCardsViewport.ActualWidth : ActualWidth;
            var columns = availableWidth >= 2120 && configuredTeamCount >= 8 ? 4 : availableWidth >= 1580 && configuredTeamCount >= 6 ? 3 : availableWidth < 920 ? 1 : 2;
            columns = Math.Max(1, Math.Min(configuredTeamCount, columns));

            var baseHeight = configuredTeamCount <= 4 ? 340d : configuredTeamCount <= 8 ? 300d : 260d;
            var rows = Math.Max(1, (int)Math.Ceiling(configuredTeamCount / (double)columns));
            if (TeamCardsViewport.ActualHeight > 0)
            {
                var fitHeight = (TeamCardsViewport.ActualHeight - 8) / rows;
                baseHeight = Math.Min(baseHeight, Math.Max(260, fitHeight));
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

        private void TeamCardsViewport_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateTeamGridLayout();
        }

        private void RoundSetupButton_Click(object sender, RoutedEventArgs e)
        {
            var showSetup = RoundSetupPanel.Visibility != Visibility.Visible;
            RoundSetupPanel.Visibility = showSetup ? Visibility.Visible : Visibility.Collapsed;
            RoundSetupButton.Content = showSetup ? "HIDE SETUP" : "SHOW SETUP";
        }

        private void ChimeButton_Click(object sender, RoutedEventArgs e)
        {
            var mute = !chimesMuted;
            if (!TrySavePreference(() => SafeFile.WriteAllText(ChimeSettingsPath, mute ? "true" : "false"), "sound preference")) return;
            chimesMuted = mute;
            if (mute) QuizChimes.Stop();
            UpdateChimeButton();
        }

        private void UpdateChimeButton()
        {
            ChimeButton.Content = chimesMuted ? "SOUND OFF" : "SOUND ON";
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
            RefreshQuestion();
            if (card != null && CanScore(card)) UpdateScore(card, e.Points, false);
        }

        private void UpdateScore(TeamScoreCard card, int change, bool isUndo)
        {
            var verb = isUndo ? "ScoreUndo" : change > 0 ? "ScoreAwarded" : "ScorePenalty";
            var details = "Team key: " + card.DatabaseTeamName + "; " + CurrentRoundContext + "; players: " + (card.TeamMembers ?? string.Empty);
            int after;
            if (!CommitScoreChange(card, change, verb, details, out after)) return;
            card.UpdateScore(after, change);
            if (change > 0 && !isUndo && !chimesMuted) QuizChimes.PlayPointsAwarded();
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
                    var score = db.ScoreCounts.SingleOrDefault(record => record.Team == card.DatabaseTeamName);
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
                var lastCorrection = events.Where(log => log.RoundNumber == currentRound && log.EventType == "ScoreCorrected").Select(log => log.QuizEventLogId).DefaultIfEmpty(0).Max();
                candidate = events.LastOrDefault(log => log.QuizEventLogId > lastCorrection && log.RoundNumber == currentRound && (log.EventType == "ScoreAwarded" || log.EventType == "ScorePenalty") && !undone.Contains(log.QuizEventLogId));
            }
            if (candidate == null) { SetFeed("No recent score change can be undone. Use REVIEW SCORES to revise an adjudicated correction."); return; }
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
            var hasLeader = cards.Any(card => card.ScoreValue != 0) || totalScores.Any(score => score != 0);
            for (var i = 0; i < cards.Length; i++)
            {
                var rank = GetTournamentRank(i, true);
                cards[i].SetLeading(hasLeader && rank == 1);
                cards[i].SetRanking(rank);
                cards[i].SetDangerState(cards[i].ScoreValue < 0);
            }
            RefreshProjector();
        }

        private void FinishRoundButton_Click(object sender, RoutedEventArgs e)
        {
            if (roundFinalized) { SetFeed("Round " + currentRound + " is already recorded. Choose NEXT ROUND in the results panel."); return; }
            if (globalTimer.State == QuizTimerState.Stopped) { SetFeed("Choose START LIVE before finishing the round."); return; }
            var cards = Cards;
            var responseTimes = GetTeamResponseTimes(cards);
            var roundElapsed = roundTimer.Elapsed;
            var eventElapsed = globalTimer.Elapsed;
            var finalRound = currentRound >= configuredRounds;
            if (!SaveCompletedRoundScores(cards, responseTimes, roundElapsed, eventElapsed, finalRound)) return;
            questionTimer.Pause();
            StopActiveTeamTimer();
            currentRoundTeamElapsed = responseTimes;
            UpdateTeamTimerDisplay();
            roundTimer.Restore(roundTimer.Duration, roundElapsed, QuizTimerState.Completed);
            if (finalRound) globalTimer.Restore(globalTimer.Duration, eventElapsed, QuizTimerState.Completed);
            for (var i = 0; i < cards.Length; i++)
            {
                var responseSeconds = SafeSeconds(responseTimes[i]);
                if (!IsTieBreak) totalScores[i] += cards[i].ScoreValue;
                roundScores[i].Add(cards[i].ScoreValue);
                roundTeamElapsed[i].Add(responseSeconds);
                totalTeamElapsed[i] += responseSeconds;
            }
            roundFinalized = true;
            UpdateTotalLabels();
            UpdateRoundScoreLabels();
            UpdateLeaderIndicators();
            SetFeed(CurrentRoundContext + " recorded. Review the result before moving on.");
            ShowRoundAnnouncement(cards);
            if (!chimesMuted) QuizChimes.PlayRoundFinished();
            UpdateTimerLabels();
            UpdateQuestionControls();
        }

        private bool SaveCompletedRoundScores(TeamScoreCard[] cards, TimeSpan[] responseTimes, TimeSpan roundElapsed, TimeSpan eventElapsed, bool finalRound)
        {
            try
            {
                using (var db = new ScoreDbContext())
                using (var transaction = db.Database.BeginTransaction())
                {
                    for (var i = 0; i < cards.Length; i++)
                    {
                        var teamName = cards[i].DatabaseTeamName;
                        var result = db.QuizRoundScores.Where(round => round.QuizSessionId == activeSession.QuizSessionId && round.RoundNumber == currentRound && round.DatabaseTeamName == teamName)
                            .OrderByDescending(round => round.QuizRoundScoreId).FirstOrDefault();
                        if (result == null)
                        {
                            result = new QuizRoundScore { QuizSessionId = activeSession.QuizSessionId, DatabaseTeamName = teamName, RoundNumber = currentRound };
                            db.QuizRoundScores.Add(result);
                        }
                        result.ScoreValue = cards[i].ScoreValue;
                        result.TeamElapsedSeconds = SafeSeconds(responseTimes[i]);
                        result.ElapsedSeconds = SafeSeconds(roundElapsed);
                        result.IsFinalized = true;
                        result.IsTieBreakParticipant = IsTeamEligible(cards[i]);
                    }
                    var session = PrepareSessionUpdate(db, finalRound ? "FinalRoundComplete" : "RoundComplete", currentRound, eventElapsed, roundElapsed, responseTimes);
                    db.QuizEventLogs.Add(CreateRoundEvent("RoundFinished", currentRound, eventElapsed, CurrentRoundContext + " completed"));
                    if (finalRound) db.QuizEventLogs.Add(CreateRoundEvent("FinalRoundComplete", currentRound, eventElapsed, "All rounds completed"));
                    db.SaveChanges();
                    transaction.Commit();
                    activeSession = session;
                }
                return true;
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Could not finalize round " + currentRound + ".", ex);
                SetFeed("The round was not finalized because its result could not be saved safely.");
                MessageBox.Show(this, "The round result could not be saved. Please try Finish Round again.", "Round not saved", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }
        }

        private void UpdateTotalLabels() { for (var i = 0; i < Cards.Length; i++) Cards[i].SetTotalScore(totalScores[i]); }
        private void UpdateRoundScoreLabels() { for (var i = 0; i < Cards.Length; i++) Cards[i].SetRoundScores(string.Join("  ·  ", roundScores[i].Select((score, round) => RoundCaption(round + 1) + " " + score))); }

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
            RefreshProjector();
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
            if (e.Key == Key.Space && Keyboard.FocusedElement is Button) return;
            if (e.Key == Key.Escape && AboutOverlay.Visibility == Visibility.Visible) { AboutOverlay.Visibility = Visibility.Collapsed; e.Handled = true; }
            else if (e.Key == Key.Escape && WinnerOverlay.Visibility == Visibility.Visible && (isPreviewAnnouncement || isFinalAnnouncement)) { WinnerActionButton_Click(sender, e); e.Handled = true; }
            else if (e.Key == Key.Space && (globalTimer.State == QuizTimerState.Running || globalTimer.State == QuizTimerState.Overtime || globalTimer.State == QuizTimerState.Paused)) { if (globalTimer.State == QuizTimerState.Running || globalTimer.State == QuizTimerState.Overtime) PauseTimerButton_Click(this, new RoutedEventArgs()); else ResumeTimerButton_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (e.Key == Key.F11) { WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized; e.Handled = true; }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.Z) { UndoLastScoreButton_Click(this, new RoutedEventArgs()); e.Handled = true; }
            else if (Keyboard.Modifiers == ModifierKeys.Control && e.Key == Key.E) { ExportLogButton_Click(this, new RoutedEventArgs()); e.Handled = true; }
        }

        private void QuizDeckButton_Click(object sender, RoutedEventArgs e)
        {
            var quizDeckWindow = new QuizDeckBuilderWindow();
            quizDeckWindow.Owner = this;
            quizDeckWindow.ShowDialog();
        }

        private void HostQuizButton_Click(object sender, RoutedEventArgs e)
        {
            var quizMasterWindow = new QuizMasterConsoleWindow
            {
                Owner = this
            };
            quizMasterWindow.ShowDialog();
        }

        protected override void OnClosed(EventArgs e)
        {
            timerRefresh.Stop();
            saveRetryTimer.Stop();
            if (projector != null) projector.Close();
            base.OnClosed(e);
        }
    }
}
