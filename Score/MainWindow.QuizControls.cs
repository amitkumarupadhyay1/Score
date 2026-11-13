using System;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace Score
{
    public partial class MainWindow
    {
        private readonly QuizTimerService questionTimer = new QuizTimerService();
        private readonly DispatcherTimer saveRetryTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
        private int questionNumber = 1;
        private bool questionWarningPlayed;
        private bool sessionSaveFailed;
        private DateTime? lastSuccessfulSaveUtc;

        private void HeaderLayout_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (HeaderControls == null || HeaderLayout.ActualWidth <= 0) return;
            var compact = HeaderLayout.ActualWidth < 1200;
            Grid.SetRow(HeaderControls, compact ? 1 : 0);
            Grid.SetColumn(HeaderControls, compact ? 0 : 1);
            Grid.SetColumnSpan(HeaderControls, compact ? 2 : 1);
            Grid.SetColumnSpan(HeaderTitle, compact ? 2 : 1);
            HeaderControls.Margin = compact ? new Thickness(0, 6, 0, 0) : new Thickness(12, 0, 0, 0);
        }

        private bool IsTieBreak { get { return currentRound > configuredRounds; } }
        private string RoundCaption(int round) { return round <= configuredRounds ? "R" + round : "TB" + (round - configuredRounds); }
        private bool IsTeamEligible(TeamScoreCard card)
        {
            return !IsTieBreak || (activeSession.TieBreakTeams ?? string.Empty).Split('|').Contains(card.DatabaseTeamName);
        }

        private bool CanScore(TeamScoreCard card)
        {
            return setupLocked && !roundFinalized && IsTeamEligible(card) &&
                !(QuestionLockCheckBox.IsChecked == true && questionTimer.IsOvertime);
        }

        private int CompareTeams(int left, int right, bool includeCurrent)
        {
            var liveMain = includeCurrent && !roundFinalized && !IsTieBreak;
            var comparison = (totalScores[left] + (liveMain ? Cards[left].ScoreValue : 0))
                .CompareTo(totalScores[right] + (liveMain ? Cards[right].ScoreValue : 0));
            if (comparison != 0) return comparison;
            // Earlier tie-breaks take precedence so eliminated teams cannot re-enter later.
            for (var round = configuredRounds; round < Math.Min(roundScores[left].Count, roundScores[right].Count); round++)
            {
                comparison = roundScores[left][round].CompareTo(roundScores[right][round]);
                if (comparison != 0) return comparison;
            }
            return includeCurrent && IsTieBreak && !roundFinalized
                ? Cards[left].ScoreValue.CompareTo(Cards[right].ScoreValue) : 0;
        }

        internal int GetTournamentRank(int index, bool includeCurrent)
        {
            return 1 + Enumerable.Range(0, Cards.Length).Count(other => CompareTeams(other, index, includeCurrent) > 0);
        }

        private void MarkSessionSaved()
        {
            sessionSaveFailed = false;
            lastSuccessfulSaveUtc = DateTime.UtcNow;
            SaveWarningPanel.Visibility = Visibility.Collapsed;
        }

        private void ShowSaveWarning()
        {
            SaveWarningPanel.Visibility = Visibility.Visible;
            SaveWarningLabel.Text = "TIMER STATE NOT SAVED · retrying every 5 seconds. Last saved: " +
                (lastSuccessfulSaveUtc.HasValue ? lastSuccessfulSaveUtc.Value.ToLocalTime().ToString("HH:mm:ss") : "not yet in this window");
        }

        private void SaveRetryTimer_Tick(object sender, EventArgs e)
        {
            if (sessionSaveFailed) PersistSession(GetCurrentSessionStatus());
        }

        private void RetrySave_Click(object sender, RoutedEventArgs e)
        {
            if (PersistSession(GetCurrentSessionStatus())) SetFeed("The current timers and session state are saved.");
        }

        private int SelectedQuestionSeconds()
        {
            int seconds;
            return int.TryParse(QuestionSecondsSelector.Text, out seconds) && seconds >= 1 && seconds <= 3600 ? seconds : 30;
        }

        private void RestoreQuestion()
        {
            questionNumber = Math.Max(1, activeSession.QuestionNumber);
            var seconds = Math.Max(1, Math.Min(3600, activeSession.QuestionDurationSeconds == 0 ? 30 : activeSession.QuestionDurationSeconds));
            QuestionSecondsSelector.Text = seconds.ToString(CultureInfo.InvariantCulture);
            QuestionLockCheckBox.IsChecked = activeSession.LockAtQuestionEnd;
            QuizTimerState state;
            if (!Enum.TryParse(activeSession.QuestionState, out state)) state = QuizTimerState.Stopped;
            if (state == QuizTimerState.Running || state == QuizTimerState.Overtime) state = QuizTimerState.Paused;
            questionTimer.Restore(TimeSpan.FromSeconds(seconds), TimeSpan.FromTicks(Math.Max(0, activeSession.QuestionElapsedTicks)), state);
            questionWarningPlayed = questionTimer.Remaining.TotalSeconds <= 5;
        }

        private void SaveQuestion(QuizSession session)
        {
            session.QuestionNumber = questionNumber;
            session.QuestionDurationSeconds = questionTimer.State == QuizTimerState.Stopped ? SelectedQuestionSeconds() : SafeSeconds(questionTimer.Duration);
            session.QuestionElapsedTicks = questionTimer.Elapsed.Ticks;
            session.QuestionState = questionTimer.State.ToString();
            session.LockAtQuestionEnd = QuestionLockCheckBox.IsChecked == true;
        }

        private static void ResetSavedQuestion(QuizSession session)
        {
            session.QuestionNumber = 1;
            session.QuestionElapsedTicks = 0;
            session.QuestionState = QuizTimerState.Stopped.ToString();
        }

        private void ResetQuestion()
        {
            questionNumber = 1;
            questionTimer.Reset();
            questionWarningPlayed = false;
            UpdateQuestionControls();
        }

        private bool QuestionCanRun { get { return setupLocked && !roundFinalized && IsEventClockRunning(); } }

        private void QuestionStart_Click(object sender, RoutedEventArgs e)
        {
            if (!QuestionCanRun) return;
            int seconds;
            if (!int.TryParse(QuestionSecondsSelector.Text, out seconds) || seconds < 1 || seconds > 3600)
            {
                SetFeed("Enter a question duration between 1 and 3600 seconds.");
                return;
            }
            if (questionTimer.State == QuizTimerState.Stopped)
            {
                questionTimer.Start(TimeSpan.FromSeconds(seconds));
                questionWarningPlayed = false;
            }
            else if (questionTimer.State == QuizTimerState.Paused && !questionTimer.IsOvertime) questionTimer.Resume();
            else return;
            RecordQuestionAction("QuestionStarted");
        }

        private void QuestionHold_Click(object sender, RoutedEventArgs e)
        {
            questionTimer.Pause();
            RecordQuestionAction("QuestionHeld");
        }

        private void QuestionAdd_Click(object sender, RoutedEventArgs e)
        {
            if (!setupLocked || roundFinalized || questionTimer.State == QuizTimerState.Stopped) return;
            var duration = questionTimer.Duration + TimeSpan.FromSeconds(10);
            if (duration.TotalSeconds > 3600) { SetFeed("The question limit cannot exceed one hour."); return; }
            if (questionTimer.State == QuizTimerState.Completed)
                questionTimer.Restore(duration, questionTimer.Elapsed, QuizTimerState.Paused);
            else questionTimer.Duration = duration;
            QuestionSecondsSelector.Text = ((int)duration.TotalSeconds).ToString(CultureInfo.InvariantCulture);
            questionWarningPlayed = false;
            RecordQuestionAction("QuestionTimeAdded");
        }

        private void QuestionRestart_Click(object sender, RoutedEventArgs e)
        {
            if (!setupLocked || roundFinalized) return;
            questionTimer.Reset();
            questionWarningPlayed = false;
            RecordQuestionAction("QuestionRestarted");
            SetFeed("Question " + questionNumber + " reset. Choose START QUESTION when ready. Event and round clocks are unchanged.");
        }

        private void QuestionNext_Click(object sender, RoutedEventArgs e)
        {
            if (!setupLocked || roundFinalized) return;
            questionNumber++;
            questionTimer.Reset();
            questionWarningPlayed = false;
            RecordQuestionAction("QuestionAdvanced");
        }

        private void QuestionLock_Click(object sender, RoutedEventArgs e)
        {
            RecordQuestionAction("QuestionLockChanged");
        }

        private void QuestionDuration_LostFocus(object sender, RoutedEventArgs e)
        {
            if (activeSession == null || questionTimer.State != QuizTimerState.Stopped) return;
            PersistSession(GetCurrentSessionStatus());
            UpdateQuestionControls();
        }

        private void RecordQuestionAction(string action)
        {
            PersistSession(GetCurrentSessionStatus());
            LogEvent(action, string.Empty, 0, 0, "Question " + questionNumber + "; elapsed " + SafeSeconds(questionTimer.Elapsed) + "s; limit " + SelectedQuestionSeconds() + "s");
            UpdateQuestionControls();
            UpdateTimerControls();
        }

        internal void RefreshQuestion()
        {
            if (questionTimer.State == QuizTimerState.Running)
            {
                if (questionTimer.IsOvertime)
                {
                    questionTimer.Restore(questionTimer.Duration, questionTimer.Duration, QuizTimerState.Completed);
                    if (!chimesMuted) QuizChimes.PlayRoundFinished();
                    RecordQuestionAction("QuestionExpired");
                    SetFeed("Question " + questionNumber + " time is up." + (QuestionLockCheckBox.IsChecked == true ? " Scoring is locked; use REVIEW SCORES for an adjudicated correction." : string.Empty));
                }
                else if (!questionWarningPlayed && questionTimer.Remaining.TotalSeconds <= 5)
                {
                    questionWarningPlayed = true;
                    if (!chimesMuted) System.Media.SystemSounds.Exclamation.Play();
                }
            }
            UpdateQuestionControls();
        }

        private void UpdateQuestionControls()
        {
            if (QuestionSummary == null) return;
            var ready = questionTimer.State == QuizTimerState.Stopped;
            var remaining = ready ? TimeSpan.FromSeconds(SelectedQuestionSeconds()) : questionTimer.Remaining;
            var status = ready ? "READY" : questionTimer.IsOvertime ? "TIME UP" : questionTimer.State == QuizTimerState.Paused ? "HELD" : "RUNNING";
            QuestionSummary.Text = "QUESTION " + questionNumber + " · " + status + " · " + FormatClock(TimeSpan.FromSeconds(Math.Ceiling(remaining.TotalSeconds)));
            QuestionSummary.Foreground = questionTimer.IsOvertime ? Brushes.Firebrick : Brushes.DarkSlateBlue;
            QuestionStartButton.Content = questionTimer.State == QuizTimerState.Paused ? "RESUME QUESTION" : "START QUESTION";
            QuestionStartButton.IsEnabled = QuestionCanRun && (ready || questionTimer.State == QuizTimerState.Paused && !questionTimer.IsOvertime);
            QuestionHoldButton.IsEnabled = questionTimer.State == QuizTimerState.Running;
            QuestionAddButton.IsEnabled = setupLocked && !roundFinalized && !ready;
            QuestionRestartButton.IsEnabled = QuestionNextButton.IsEnabled = setupLocked && !roundFinalized;
            QuestionSecondsSelector.IsEnabled = ready;
        }

        private void UpdateTieBreakButton()
        {
            TieBreakButton.Visibility = roundFinalized && currentRound >= configuredRounds &&
                Enumerable.Range(0, Cards.Length).Count(index => GetTournamentRank(index, false) == 1) > 1 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void TieBreak_Click(object sender, RoutedEventArgs e)
        {
            var leaders = Cards.Where((card, index) => GetTournamentRank(index, false) == 1).ToArray();
            if (MessageBox.Show(this, "Start an extra round for " + string.Join(", ", leaders.Select(card => card.TeamName)) +
                "? Main competition totals will stay unchanged. The extra round starts paused.", "Tie-break", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes) return;
            string error;
            if (!TryStartTieBreak(out error))
            {
                RoundSaveError.Text = error;
                RoundSaveError.Visibility = Visibility.Visible;
            }
        }

        internal bool TryStartTieBreak(out string error)
        {
            error = null;
            var leaders = Cards.Where((card, index) => GetTournamentRank(index, false) == 1).ToArray();
            if (!roundFinalized || currentRound < configuredRounds || leaders.Length < 2)
            {
                error = "Finish the competition first. A tie-break is available when first place is shared.";
                return false;
            }
            try
            {
                using (var db = new ScoreDbContext())
                using (var transaction = db.Database.BeginTransaction())
                {
                    var session = PrepareSessionUpdate(db, "Paused", currentRound + 1, globalTimer.Elapsed, TimeSpan.Zero, null);
                    session.TieBreakTeams = string.Join("|", leaders.Select(card => card.DatabaseTeamName));
                    session.CompletedAtUtc = null;
                    ResetSavedQuestion(session);
                    foreach (var score in db.ScoreCounts) score.ScoreValue = 0;
                    db.QuizEventLogs.Add(CreateRoundEvent("TieBreakStarted", currentRound + 1, globalTimer.Elapsed, "Eligible teams: " + string.Join(" / ", leaders.Select(card => card.TeamName)) + "; main totals unchanged"));
                    db.SaveChanges();
                    transaction.Commit();
                    activeSession = session;
                }
                currentRound++;
                roundFinalized = isFinalRoundComplete = isFinalAnnouncement = isPreviewAnnouncement = false;
                roundOvertimeAnnounced = false;
                ResetTeamTimer();
                Array.Clear(currentRoundTeamElapsed, 0, currentRoundTeamElapsed.Length);
                foreach (var card in Cards) { card.ScoreValue = 0; card.SetFinalWinner(false); }
                ResetQuestion();
                globalTimer.Restore(globalTimer.Duration, globalTimer.Elapsed, QuizTimerState.Paused);
                roundTimer.Restore(GetRoundDuration(), TimeSpan.Zero, QuizTimerState.Paused);
                WinnerOverlay.Visibility = Visibility.Collapsed;
                RoundNumberLabel.Text = "TIE-BREAK " + (currentRound - configuredRounds);
                MarkSessionSaved();
                UpdateLeaderIndicators();
                UpdateTimerControls();
                UpdateTimerLabels();
                UpdateCompetitionStatus();
                SetFeed("Tie-break ready for " + string.Join(", ", leaders.Select(card => card.TeamName)) + ". Choose RESUME.");
                return true;
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Could not start tie-break.", ex);
                error = "The tie-break could not be saved. The final results remain unchanged. Retry when storage is available.";
                return false;
            }
        }

        private void ReviewScores_Click(object sender, RoutedEventArgs e)
        {
            var panel = new StackPanel { Margin = new Thickness(20) };
            panel.Children.Add(new TextBlock { Text = "Review a score", FontSize = 22, FontWeight = FontWeights.Bold });
            panel.Children.Add(new TextBlock { Text = "Choose the team and round, enter the corrected score, and record the judge's reason. Earlier entries remain in the audit log. Correcting an earlier result after tie-breaks voids later tie-breaks so they can be replayed.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 12) });
            var teams = new ComboBox { Name = "CorrectionTeam", ItemsSource = Cards, DisplayMemberPath = "TeamName", SelectedIndex = 0, Height = 32 };
            var rounds = new ComboBox { Name = "CorrectionRound", ItemsSource = Enumerable.Range(1, currentRound), SelectedItem = currentRound, Height = 32 };
            var value = new TextBox { Name = "CorrectionScore", Height = 32, MaxLength = 11 };
            var reason = new TextBox { Name = "CorrectionReason", Height = 70, MaxLength = 500, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true };
            foreach (var entry in new[] { Tuple.Create("Team", (Control)teams), Tuple.Create("Round number", (Control)rounds), Tuple.Create("Corrected score", (Control)value), Tuple.Create("Reason (required)", (Control)reason) })
            {
                panel.Children.Add(new TextBlock { Text = entry.Item1, Margin = new Thickness(0, 8, 0, 3) });
                panel.Children.Add(entry.Item2);
            }
            Action showScore = () =>
            {
                var card = teams.SelectedItem as TeamScoreCard;
                if (card == null || rounds.SelectedItem == null) return;
                var round = (int)rounds.SelectedItem;
                var index = GetTeamIndex(card.DatabaseTeamName);
                value.Text = (round == currentRound ? card.ScoreValue : roundScores[index][round - 1]).ToString(CultureInfo.InvariantCulture);
            };
            teams.SelectionChanged += (s, args) => showScore();
            rounds.SelectionChanged += (s, args) => showScore();
            showScore();
            var validation = new TextBlock { Name = "CorrectionError", Foreground = Brushes.Firebrick, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 8, 0, 8) };
            panel.Children.Add(validation);
            var actions = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            var apply = new Button { Name = "ApplyCorrection", Content = "SAVE CORRECTION", Style = (Style)FindResource("ToolbarButtonStyle") };
            actions.Children.Add(apply);
            actions.Children.Add(new Button { Content = "CANCEL", IsCancel = true, Style = (Style)FindResource("ToolbarButtonStyle") });
            panel.Children.Add(actions);
            var dialog = new Window { Title = "Review scores", Owner = this, Width = 500, MaxHeight = Math.Max(400, ActualHeight - 30), SizeToContent = SizeToContent.Height, Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, WindowStartupLocation = WindowStartupLocation.CenterOwner, ShowInTaskbar = false };
            apply.Click += (s, args) =>
            {
                int score;
                if (!int.TryParse(value.Text, out score)) { validation.Text = "Enter a whole-number score."; return; }
                string error;
                if (TryCorrectScore((int)rounds.SelectedItem, ((TeamScoreCard)teams.SelectedItem).DatabaseTeamName, score, reason.Text, out error)) dialog.DialogResult = true;
                else validation.Text = error;
            };
            dialog.ShowDialog();
        }

        internal bool TryCorrectScore(int round, string key, int corrected, string reason, out string error)
        {
            error = null;
            var card = GetCard(key);
            if (!setupLocked || card == null || round < 1 || round > currentRound || string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 500)
            {
                error = "Choose a played round and team, and enter a reason of 1 to 500 characters.";
                return false;
            }
            if (IsTieBreak && !roundFinalized && round < currentRound)
            {
                error = "Finish the active tie-break before correcting earlier rounds. Later tie-breaks will then be voided for replay.";
                return false;
            }
            if (round == currentRound && IsTieBreak && !IsTeamEligible(card))
            {
                error = "This team is not participating in the current tie-break.";
                return false;
            }
            try
            {
                var targetRound = currentRound;
                var responseTimes = GetTeamResponseTimes(Cards);
                using (var db = new ScoreDbContext())
                using (var transaction = db.Database.BeginTransaction())
                {
                    var result = db.QuizRoundScores.Where(item => item.QuizSessionId == activeSession.QuizSessionId && item.RoundNumber == round && item.DatabaseTeamName == key && item.IsFinalized).OrderByDescending(item => item.QuizRoundScoreId).FirstOrDefault();
                    if (result == null && (round != currentRound || roundFinalized)) throw new InvalidOperationException("The recorded round is missing.");
                    if (round > configuredRounds && result != null && !result.IsTieBreakParticipant)
                    {
                        error = "This team did not participate in that tie-break.";
                        return false;
                    }
                    var working = db.ScoreCounts.Single(item => item.Team == key);
                    var before = result == null ? working.ScoreValue : result.ScoreValue;
                    if (before == corrected) { error = "The score has not changed."; return false; }
                    if (result != null) result.ScoreValue = corrected;
                    if (round == currentRound) working.ScoreValue = corrected;
                    db.QuizEventLogs.Add(new QuizEventLog { QuizSessionId = activeSession.QuizSessionId, OccurredAtUtc = DateTime.UtcNow, EventType = "ScoreCorrected", Team = card.TeamName, RoundNumber = round, ScoreChange = SafeLogScore((long)corrected - before), ScoreAfter = corrected, ElapsedSeconds = SafeSeconds(globalTimer.Elapsed), Details = "Team key: " + key + "; previous score " + before + "; corrected score " + corrected + "; reason: " + reason.Trim() });
                    if (IsTieBreak && round < currentRound)
                    {
                        targetRound = Math.Max(configuredRounds, round);
                        var invalid = db.QuizRoundScores.Where(item => item.QuizSessionId == activeSession.QuizSessionId && item.RoundNumber > targetRound && item.IsFinalized).ToList();
                        foreach (var item in invalid) item.IsFinalized = false;
                        db.QuizEventLogs.Add(CreateRoundEvent("TieBreaksVoided", targetRound, globalTimer.Elapsed, "Earlier result corrected; replay required. Previous results: " + string.Join("; ", invalid.Select(item => "round " + item.RoundNumber + " " + item.DatabaseTeamName + "=" + item.ScoreValue))));
                    }
                    var session = PrepareSessionUpdate(db, GetCurrentSessionStatus(), targetRound, globalTimer.Elapsed, roundTimer.Elapsed, responseTimes);
                    if (targetRound != currentRound)
                    {
                        session.Status = "FinalRoundComplete";
                        var targetResults = db.QuizRoundScores.Where(item => item.QuizSessionId == activeSession.QuizSessionId && item.RoundNumber == targetRound && item.IsFinalized).ToList();
                        session.TieBreakTeams = targetRound > configuredRounds ? string.Join("|", targetResults.Where(item => item.IsTieBreakParticipant).Select(item => item.DatabaseTeamName)) : null;
                        session.RoundElapsedSeconds = targetResults.Select(item => item.ElapsedSeconds).DefaultIfEmpty(0).Max();
                        session.TeamResponseTicks = string.Join(",", Cards.Select(team => TimeSpan.FromSeconds(targetResults.Where(item => item.DatabaseTeamName == team.DatabaseTeamName).Select(item => item.TeamElapsedSeconds).FirstOrDefault()).Ticks.ToString(CultureInfo.InvariantCulture)));
                        ResetSavedQuestion(session);
                    }
                    db.SaveChanges();
                    transaction.Commit();
                    activeSession = session;
                }
                if (round == currentRound) card.ScoreValue = corrected;
                if (targetRound != currentRound)
                {
                    currentRound = targetRound;
                    ResetQuestion();
                    roundTimer.Restore(roundTimer.Duration, TimeSpan.FromSeconds(activeSession.RoundElapsedSeconds), QuizTimerState.Completed);
                    RoundNumberLabel.Text = IsTieBreak ? "TIE-BREAK " + (currentRound - configuredRounds) : "ROUND " + currentRound + " OF " + configuredRounds;
                }
                StopActiveTeamTimer();
                LoadRoundHistory();
                if (!roundFinalized) currentRoundTeamElapsed = responseTimes;
                UpdateTeamTimerDisplay();
                UpdateTotalLabels();
                UpdateLeaderIndicators();
                UpdateTimerControls();
                if (roundFinalized && currentRound >= configuredRounds) { ShowFinalAnnouncement(); WinnerOverlay.Visibility = Visibility.Visible; }
                else if (WinnerOverlay.Visibility == Visibility.Visible) ShowRoundAnnouncement(Cards, true);
                MarkSessionSaved();
                SetFeed(card.TeamName + " score corrected for " + RoundCaption(round) + ". Reason saved in the audit log.");
                return true;
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Could not save score correction.", ex);
                error = "The correction could not be saved. The previous result remains recorded. Please retry.";
                return false;
            }
        }
    }
}
