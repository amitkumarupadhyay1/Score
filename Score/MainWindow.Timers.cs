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
            globalTimer.Pause(); roundTimer.Pause(); questionTimer.Pause(); StopActiveTeamTimer(); timerRefresh.Stop(); PersistSession(GetCurrentSessionStatus());
            LogEvent("TimerPaused", string.Empty, 0, 0, "Timers paused"); SetFeed("Timers paused. Scores remain available for correction."); UpdateTimerLabels(); UpdateTimerControls(); UpdateCompetitionStatus();
        }

        private void ResumeTimerButton_Click(object sender, RoutedEventArgs e)
        {
            globalTimer.Resume(); roundTimer.Resume(); timerRefresh.Start(); PersistSession(GetCurrentSessionStatus());
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
            RefreshQuestion();
            globalTimer.RefreshState(); roundTimer.RefreshState(); UpdateTimerLabels(); UpdateTeamTimerDisplay();
            if (!roundFinalized && roundTimer.IsOvertime && !roundOvertimeAnnounced) { roundOvertimeAnnounced = true; LogEvent("RoundOvertime", string.Empty, 0, 0, CurrentRoundContext + " timer expired"); SetFeed("Round time has expired — overtime is now being shown in red."); UpdateCompetitionStatus(); }
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
            GlobalTimerLabel.Foreground = globalTimer.IsOvertime ? System.Windows.Media.Brushes.Firebrick : System.Windows.Media.Brushes.DarkGreen;
            RoundTimerLabel.Foreground = roundTimer.IsOvertime ? System.Windows.Media.Brushes.Firebrick : System.Windows.Media.Brushes.DarkGreen;
        }

        private string FormatSelectedDuration(ComboBox selector)
        {
            var minutes = GetSelectedMinutes(selector);
            return minutes <= 0 ? "NO LIMIT" : FormatClock(TimeSpan.FromMinutes(minutes));
        }

        private void UpdateTimerControls()
        {
            var isRunning = IsEventClockRunning();
            var isPaused = globalTimer.State == QuizTimerState.Paused;
            var isStopped = globalTimer.State == QuizTimerState.Stopped && !setupLocked;
            StartTimerButton.IsEnabled = isStopped;
            StartTimerButton.Content = isRunning || isPaused ? "LIVE NOW" : "START LIVE";
            PauseTimerButton.IsEnabled = isRunning;
            ResumeTimerButton.IsEnabled = isPaused;
            RestartTimerButton.IsEnabled = isRunning || isPaused;
            TeamCountSelector.IsEnabled = !setupLocked;
            RoundCountSelector.IsEnabled = !setupLocked;
            foreach (var card in Cards)
            {
                card.SetScoringEnabled(CanScore(card));
                card.SetLiveScoring(isRunning && !roundFinalized && IsTeamEligible(card));
                card.SetResponseTimerEnabled(isRunning && !roundFinalized && IsTeamEligible(card));
            }
            UpdateQuestionControls();
        }

        private bool IsEventClockRunning()
        {
            return globalTimer.State == QuizTimerState.Running || globalTimer.State == QuizTimerState.Overtime;
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
            if (card == null || roundFinalized || !IsEventClockRunning() || !IsTeamEligible(card)) return;
            StopActiveTeamTimer(); teamTimer.Start(TimeSpan.Zero); activeTeamTimerCard = card; card.SetTeamTimerRunning(true); timerRefresh.Start();
            LogEvent("TeamTimerStarted", card.TeamName, 0, card.ScoreValue, "Team key: " + card.DatabaseTeamName + "; response timer started"); SetFeed("Response timer started for " + card.TeamName + ".");
            PersistSession(GetCurrentSessionStatus());
        }

        private void Card_TeamTimerStopped(object sender, RoutedEventArgs e)
        {
            if (activeTeamTimerCard == null || sender != activeTeamTimerCard) return;
            var card = activeTeamTimerCard;
            StopActiveTeamTimer();
            LogEvent("TeamTimerStopped", card.TeamName, 0, card.ScoreValue, "Response timer stopped");
            PersistSession(GetCurrentSessionStatus());
        }

        private void Card_TeamTimerResetRequested(object sender, RoutedEventArgs e)
        {
            var card = sender as TeamScoreCard;
            if (card == null || roundFinalized || !IsEventClockRunning() || !IsTeamEligible(card)) return;
            var index = GetTeamIndex(card.DatabaseTeamName);
            if (index < 0) return;
            if (card == activeTeamTimerCard)
            {
                teamTimer.Reset();
                activeTeamTimerCard = null;
            }
            currentRoundTeamElapsed[index] = TimeSpan.Zero;
            card.SetTeamTimerRunning(false);
            card.SetTeamTimerDisplay("00:00", false);
            LogEvent("TeamTimerReset", card.TeamName, 0, card.ScoreValue, "Team response timer reset for the current round");
            SetFeed("Response timer reset for " + card.TeamName + ".");
            PersistSession(GetCurrentSessionStatus());
        }

        private void StopActiveTeamTimer()
        {
            if (activeTeamTimerCard == null) return;
            teamTimer.Stop(); var index = GetTeamIndex(activeTeamTimerCard.DatabaseTeamName);
            if (index >= 0) currentRoundTeamElapsed[index] += teamTimer.Elapsed;
            activeTeamTimerCard.SetTeamTimerRunning(false); activeTeamTimerCard = null; UpdateTeamTimerDisplay();
        }

        private void ResetTeamTimer()
        {
            StopActiveTeamTimer(); teamTimer.Reset();
            foreach (var card in Cards) { card.SetTeamTimerRunning(false); card.SetTeamTimerDisplay("00:00", false); }
        }

        private void UpdateTeamTimerDisplay()
        {
            var cards = Cards;
            for (var i = 0; i < cards.Length; i++)
            {
                var elapsed = GetTeamResponseTime(cards[i], i);
                cards[i].SetTeamTimerDisplay(FormatClock(elapsed), false);
            }
        }

        private TimeSpan GetTeamResponseTime(TeamScoreCard card, int index)
        {
            var elapsed = currentRoundTeamElapsed[index];
            return card == activeTeamTimerCard ? elapsed + teamTimer.Elapsed : elapsed;
        }

        private TimeSpan[] GetTeamResponseTimes(TeamScoreCard[] cards)
        {
            return cards.Select(GetTeamResponseTime).ToArray();
        }


        private void TimerDurationSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GlobalTimerLabel == null || RoundTimerLabel == null || GlobalMinutesSelector == null || RoundMinutesSelector == null) return;
            UpdateTimerLabels();
            if (activeSession != null && !setupLocked && !isSynchronizingSelectors)
                PersistSession("NotStarted");
        }

    }
}
