using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Score
{
    public partial class MainWindow : Window
    {
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
            EventTitleEditor.Text = string.IsNullOrWhiteSpace(EventTitleEditor.Text) ? DefaultEventTitle : EventTitleEditor.Text.Trim();
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
            SaveTeamDetails(SettingsDirectory);
        }

        private void SaveTeamDetails(string directory)
        {
            var cards = Cards;
            SafeFile.WriteAllLines(Path.Combine(directory, "team-names.txt"), cards.Select(card => card.TeamName ?? string.Empty).ToArray());
            SafeFile.WriteAllLines(Path.Combine(directory, "team-players.txt"), cards.Select(card => card.TeamMembers ?? string.Empty).ToArray());
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
                var activeSessions = db.QuizSessions.Where(session => session.IsActive).OrderByDescending(session => session.QuizSessionId).ToList();
                if (activeSessions.Count > 1)
                {
                    foreach (var stale in activeSessions.Skip(1))
                    {
                        stale.IsActive = false;
                        if (!stale.CompletedAtUtc.HasValue) stale.CompletedAtUtc = DateTime.UtcNow;
                    }
                    db.SaveChanges();
                }
                activeSession = activeSessions.FirstOrDefault();
                if (activeSession == null)
                {
                    activeSession = new QuizSession { Title = EventTitleEditor.Text, ConfiguredTeamCount = configuredTeamCount, ConfiguredRounds = configuredRounds, CurrentRound = currentRound, RoundDurationSeconds = DefaultRoundDurationSeconds, QuestionNumber = 1, QuestionDurationSeconds = 30, Status = "NotStarted", LastUpdatedAtUtc = DateTime.UtcNow, IsActive = true };
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
                var status = activeSession.Status ?? "NotStarted";
                var hasMeaningfulProgress = activeSession.GlobalElapsedSeconds > 0 || activeSession.RoundElapsedSeconds > 0 || !string.Equals(status, "NotStarted", StringComparison.OrdinalIgnoreCase);
                setupLocked = hasMeaningfulProgress && !string.Equals(status, "NotStarted", StringComparison.OrdinalIgnoreCase);
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
            RoundNumberLabel.Text = IsTieBreak ? "TIE-BREAK " + (currentRound - configuredRounds) : "ROUND " + currentRound + " OF " + configuredRounds;
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
        }

        private void LoadRoundHistory()
        {
            var cards = Cards;
            Array.Clear(totalScores, 0, totalScores.Length);
            Array.Clear(totalTeamElapsed, 0, totalTeamElapsed.Length);
            foreach (var scores in roundScores) scores.Clear();
            foreach (var elapsed in roundTeamElapsed) elapsed.Clear();
            using (var db = new ScoreDbContext())
            {
                var history = db.QuizRoundScores.Where(round => round.QuizSessionId == activeSession.QuizSessionId && round.IsFinalized)
                    .ToList()
                    .GroupBy(round => new { round.RoundNumber, round.DatabaseTeamName })
                    .Select(group => group.OrderByDescending(round => round.QuizRoundScoreId).First())
                    .OrderBy(round => round.RoundNumber)
                    .ToList();
                var currentResults = history.Where(round => round.RoundNumber == currentRound).ToList();
                roundFinalized = cards.All(card => currentResults.Any(round => round.DatabaseTeamName == card.DatabaseTeamName));
                foreach (var round in history)
                {
                    var index = GetTeamIndex(round.DatabaseTeamName);
                    if (index < 0) continue;
                    roundScores[index].Add(round.ScoreValue);
                    roundTeamElapsed[index].Add(round.TeamElapsedSeconds);
                    if (round.RoundNumber <= configuredRounds) totalScores[index] += round.ScoreValue;
                    totalTeamElapsed[index] += round.TeamElapsedSeconds;
                    if (round.RoundNumber == currentRound)
                    {
                        currentRoundTeamElapsed[index] = TimeSpan.FromSeconds(round.TeamElapsedSeconds);
                        cards[index].ScoreValue = round.ScoreValue;
                    }
                }
                if (!roundFinalized) RestoreTeamResponseTimes();
            }
            UpdateRoundScoreLabels();
            UpdateTeamTimerDisplay();
        }

        private void RestoreTeamResponseTimes()
        {
            var ticks = (activeSession.TeamResponseTicks ?? string.Empty).Split(',');
            for (var i = 0; i < Math.Min(ticks.Length, currentRoundTeamElapsed.Length); i++)
            {
                long value;
                if (long.TryParse(ticks[i], NumberStyles.None, CultureInfo.InvariantCulture, out value))
                    currentRoundTeamElapsed[i] = TimeSpan.FromTicks(value);
            }
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
            var hasMeaningfulProgress = activeSession != null && (activeSession.GlobalElapsedSeconds > 0 || activeSession.RoundElapsedSeconds > 0 || activeSession.StartedAtUtc.HasValue || currentRound > 1 || roundFinalized || Cards.Any(card => card.ScoreValue != 0) || currentRoundTeamElapsed.Any(elapsed => elapsed > TimeSpan.Zero));
            if (string.Equals(status, "Paused", StringComparison.OrdinalIgnoreCase) && !hasMeaningfulProgress)
            {
                status = "NotStarted";
                activeSession.Status = "NotStarted";
                setupLocked = false;
                PersistSession("NotStarted");
            }
            var hasStarted = roundFinalized || !string.Equals(status, "NotStarted", StringComparison.OrdinalIgnoreCase);
            if (!hasStarted)
            {
                setupLocked = false;
                SynchronizeTimerSelector(GlobalMinutesSelector, activeSession.GlobalDurationSeconds);
                SynchronizeTimerSelector(RoundMinutesSelector, activeSession.RoundDurationSeconds);
                foreach (var card in Cards) { card.SetScoringEnabled(false); card.SetEditingEnabled(true); }
                return;
            }

            setupLocked = true;
            SynchronizeTimerSelector(GlobalMinutesSelector, activeSession.GlobalDurationSeconds);
            SynchronizeTimerSelector(RoundMinutesSelector, activeSession.RoundDurationSeconds);

            var globalElapsed = Math.Max(0, activeSession.GlobalElapsedSeconds);
            var roundElapsed = Math.Max(0, activeSession.RoundElapsedSeconds);
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
                var completedStatus = GetCurrentSessionStatus();
                if (!string.Equals(status, completedStatus, StringComparison.OrdinalIgnoreCase)) PersistSession(completedStatus);
                SetFeed(currentRound >= configuredRounds ? "The completed game was recovered safely. Review the final result or export the audit log." : "The recorded round was recovered safely. Continue when you are ready.");
            }
            else
            {
                PersistSession("Paused");
                SetFeed("Recovered at the last saved time. Confirm the scores and clocks, then choose RESUME.");
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
                    var wasSynchronizing = isSynchronizingSelectors;
                    isSynchronizingSelectors = true;
                    selector.SelectedIndex = i;
                    isSynchronizingSelectors = wasSynchronizing;
                    return;
                }
            }
        }

    }
}
