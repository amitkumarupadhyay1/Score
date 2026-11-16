using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Microsoft.Win32;

namespace Score
{
    public partial class MainWindow : Window
    {
        private static string GetSessionSettingsDirectory(int sessionId)
        {
            return Path.Combine(AppPaths.SettingsDirectory, "Sessions", sessionId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private void StartFreshSession(bool keepSettings)
        {
            var now = DateTime.UtcNow;
            var newSession = new QuizSession
            {
                Title = keepSettings ? EventTitleEditor.Text : DefaultEventTitle,
                ConfiguredTeamCount = keepSettings ? configuredTeamCount : DefaultTeamCount,
                ConfiguredRounds = keepSettings ? configuredRounds : DefaultRoundCount,
                GlobalDurationSeconds = keepSettings ? SafeSeconds(GetGlobalDuration()) : 0,
                RoundDurationSeconds = keepSettings ? SafeSeconds(GetRoundDuration()) : DefaultRoundDurationSeconds,
                CurrentRound = 1,
                QuestionNumber = 1,
                QuestionDurationSeconds = keepSettings ? SelectedQuestionSeconds() : 30,
                LockAtQuestionEnd = keepSettings && QuestionLockCheckBox.IsChecked == true,
                Status = "NotStarted",
                LastUpdatedAtUtc = now,
                IsActive = true,
                UsesSessionSettings = true
            };
            using (var db = new ScoreDbContext())
            using (var transaction = db.Database.BeginTransaction())
            {
                foreach (var session in db.QuizSessions.Where(item => item.IsActive).ToList())
                {
                    session.IsActive = false;
                    if (!session.CompletedAtUtc.HasValue) session.CompletedAtUtc = now;
                }
                db.SaveChanges();
                var scores = db.ScoreCounts.ToList();
                foreach (var score in scores) score.ScoreValue = 0;
                foreach (var key in Enumerable.Range(0, newSession.ConfiguredTeamCount).Select(GetTeamKey))
                    if (!scores.Any(score => score.Team == key)) db.ScoreCounts.Add(new ScoreCount { Team = key, ScoreValue = 0 });
                db.QuizSessions.Add(newSession);
                db.SaveChanges();
                var directory = GetSessionSettingsDirectory(newSession.QuizSessionId);
                if (keepSettings) SaveTeamDetails(directory);
                else
                {
                    SafeFile.WriteAllLines(Path.Combine(directory, "team-names.txt"), new string[0]);
                    SafeFile.WriteAllLines(Path.Combine(directory, "team-players.txt"), new string[0]);
                }
                SafeFile.WriteAllLines(Path.Combine(directory, "round-context.txt"), keepSettings
                    ? new[] { RoundLabelEditor.Text.Trim(), CurrentRoundFormat }
                    : new[] { DefaultRoundName, "Standard" });
                db.QuizEventLogs.Add(new QuizEventLog
                {
                    QuizSessionId = newSession.QuizSessionId,
                    OccurredAtUtc = now,
                    EventType = "SessionCreated",
                    RoundNumber = 1,
                    Team = string.Empty,
                    Details = keepSettings ? "New game started with the existing roster and settings" : "New game started with default settings"
                });
                db.SaveChanges();
                transaction.Commit();
            }

            StopActiveTeamTimer();
            activeSession = newSession;
            RestoreQuestion();
            MarkSessionSaved();
            currentRound = 1;
            configuredRounds = newSession.ConfiguredRounds;
            isFinalRoundComplete = false;
            isFinalAnnouncement = false;
            isPreviewAnnouncement = false;
            roundFinalized = false;
            roundOvertimeAnnounced = false;
            setupLocked = false;
            WinnerOverlay.Visibility = Visibility.Collapsed;
            FinalGameActions.Visibility = Visibility.Collapsed;
            EventTitleEditor.Text = newSession.Title;
            LoadRoundContext();
            InitializeTeamState(newSession.ConfiguredTeamCount);
            BuildTeamCards();
            LoadTeamDetails();
            SynchronizeTeamCountSelector();
            SynchronizeRoundCountSelector();
            SynchronizeTimerSelector(GlobalMinutesSelector, newSession.GlobalDurationSeconds);
            SynchronizeTimerSelector(RoundMinutesSelector, newSession.RoundDurationSeconds);
            foreach (var card in Cards) { card.ScoreValue = 0; card.SetRoundScores(string.Empty); card.SetScoringEnabled(false); card.SetEditingEnabled(true); card.SetFinalWinner(false); }
            ResetTimerState();
            UpdateTotalLabels();
            UpdateLeaderIndicators();
            SetFeed("New game ready. Add or confirm player names, choose a round type, and start live scoring.");
            UpdateCompetitionStatus();
        }

        private bool SaveRoundAdvance(int nextRound, string status)
        {
            try
            {
                var elapsed = globalTimer.Elapsed;
                using (var db = new ScoreDbContext())
                using (var transaction = db.Database.BeginTransaction())
                {
                    foreach (var score in db.ScoreCounts) score.ScoreValue = 0;
                    var session = PrepareSessionUpdate(db, status, nextRound, elapsed, TimeSpan.Zero, null);
                    session.RoundDurationSeconds = SafeSeconds(GetRoundDuration());
                    ResetSavedQuestion(session);
                    db.QuizEventLogs.Add(CreateRoundEvent("RoundStarted", nextRound, elapsed, CurrentRoundContext + " started"));
                    db.SaveChanges();
                    transaction.Commit();
                    activeSession = session;
                }
                RoundSaveError.Visibility = Visibility.Collapsed;
                return true;
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Could not advance to round " + nextRound + ".", ex);
                RoundSaveError.Text = "Could not save the next round. The recorded result is still available. Choose NEXT ROUND to retry.";
                RoundSaveError.Visibility = Visibility.Visible;
                RoundSaveError.BringIntoView();
                return false;
            }
        }


        private void SetFeed(string message) { if (EventFeedLabel != null) EventFeedLabel.Text = message; }
        private void UpdateCompetitionStatus()
        {
            CompetitionStatusLabel.Text = isFinalAnnouncement ? "FINAL RESULT" : isFinalRoundComplete ? "FINAL ROUND COMPLETE" : roundFinalized ? "ROUND RECORDED" : globalTimer.State == QuizTimerState.Paused ? "PAUSED" : roundTimer.IsOvertime ? "ROUND OVERTIME" : globalTimer.State == QuizTimerState.Running || globalTimer.State == QuizTimerState.Overtime ? "LIVE" : "READY TO START";
            CompetitionStatusLabel.Foreground = globalTimer.State == QuizTimerState.Paused ? System.Windows.Media.Brushes.DarkGoldenrod : roundTimer.IsOvertime ? System.Windows.Media.Brushes.Firebrick : System.Windows.Media.Brushes.DarkGreen;
            RefreshProjector();
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

        private bool PersistSession(string status)
        {
            if (activeSession == null) return true;
            try
            {
                using (var db = new ScoreDbContext())
                {
                    var responseTimes = status == "NotStarted" ? null : GetTeamResponseTimes(Cards);
                    var session = PrepareSessionUpdate(db, status, currentRound, globalTimer.Elapsed, roundTimer.Elapsed, responseTimes);
                    db.SaveChanges();
                    activeSession = session;
                }
                MarkSessionSaved();
                return true;
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Could not persist session state " + status + ".", ex);
                SetFeed("Scores are still available, but the latest timer state could not be saved. Check the diagnostic log.");
                sessionSaveFailed = true;
                ShowSaveWarning();
                return false;
            }
        }

        private QuizSession PrepareSessionUpdate(ScoreDbContext db, string status, int roundNumber, TimeSpan eventElapsed, TimeSpan roundElapsed, TimeSpan[] responseTimes)
        {
            var session = db.QuizSessions.Single(item => item.QuizSessionId == activeSession.QuizSessionId && item.IsActive);
            var now = DateTime.UtcNow;
            session.Status = status;
            session.Title = EventTitleEditor.Text;
            session.ConfiguredRounds = configuredRounds;
            session.ConfiguredTeamCount = configuredTeamCount;
            session.CurrentRound = roundNumber;
            session.GlobalDurationSeconds = SafeSeconds(status == "NotStarted" ? GetGlobalDuration() : globalTimer.Duration);
            session.RoundDurationSeconds = SafeSeconds(status == "NotStarted" ? GetRoundDuration() : roundTimer.Duration);
            session.GlobalElapsedSeconds = SafeSeconds(eventElapsed);
            session.RoundElapsedSeconds = SafeSeconds(roundElapsed);
            session.TeamResponseTicks = responseTimes == null ? null : string.Join(",", responseTimes.Select(elapsed => elapsed.Ticks.ToString(CultureInfo.InvariantCulture)));
            SaveQuestion(session);
            if (status == "RoundComplete" || status == "FinalRoundComplete") session.QuestionState = QuizTimerState.Paused.ToString();
            session.StartedAtUtc = activeSession.StartedAtUtc;
            session.LastUpdatedAtUtc = now;
            if (status == "Paused") session.PausedAtUtc = now;
            if (status == "FinalRoundComplete" && !session.CompletedAtUtc.HasValue) session.CompletedAtUtc = now;
            return session;
        }

        private QuizEventLog CreateRoundEvent(string type, int roundNumber, TimeSpan elapsed, string details)
        {
            return new QuizEventLog
            {
                QuizSessionId = activeSession.QuizSessionId,
                OccurredAtUtc = DateTime.UtcNow,
                EventType = type,
                Team = string.Empty,
                RoundNumber = roundNumber,
                ElapsedSeconds = SafeSeconds(elapsed),
                Details = details
            };
        }

        private int SafeSeconds(TimeSpan value)
        {
            if (value <= TimeSpan.Zero) return 0;
            return value.TotalSeconds >= int.MaxValue ? int.MaxValue : (int)value.TotalSeconds;
        }

        private int SafeLogScore(long score)
        {
            return score >= int.MaxValue ? int.MaxValue : score <= int.MinValue ? int.MinValue : (int)score;
        }

        private void ExportLogButton_Click(object sender, RoutedEventArgs e)
        {
            var safeTitle = ExportSafety.SanitizeFileName(EventTitleEditor.Text, "quiz");
            var dialog = new SaveFileDialog { Title = "Export competition audit log", Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*", DefaultExt = ".csv", AddExtension = true, RestoreDirectory = true, FileName = safeTitle + "-audit-log.csv" };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                WriteAuditExport(dialog.FileName);
                SetFeed("Audit log exported successfully.");
                MessageBox.Show(this, "The competition audit log was exported successfully.", "Export complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Audit export failed for " + dialog.FileName + ".", ex);
                SetFeed("The audit log could not be exported to the selected location.");
                MessageBox.Show(this, "The audit log could not be saved. Check the diagnostic log and try again.", "Export failed", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        internal void WriteAuditExport(string path)
        {
            var csv = new StringBuilder();
            QuizSession session;
            using (var db = new ScoreDbContext())
            {
                session = db.QuizSessions.SingleOrDefault(item => item.QuizSessionId == activeSession.QuizSessionId);
                if (session == null) throw new InvalidOperationException("The current quiz session could not be found.");
                var rounds = db.QuizRoundScores.Where(round => round.QuizSessionId == activeSession.QuizSessionId && round.IsFinalized).ToList()
                    .GroupBy(round => new { round.RoundNumber, round.DatabaseTeamName }).Select(group => group.OrderByDescending(round => round.QuizRoundScoreId).First())
                    .OrderBy(round => round.RoundNumber).ThenBy(round => round.DatabaseTeamName).ToList();
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
                var liveResponse = GetTeamResponseTimes(Cards);
                for (var i = 0; i < Cards.Length; i++)
                {
                    var responseSeconds = totalTeamElapsed[i] + (roundFinalized ? 0 : SafeSeconds(liveResponse[i]));
                    csv.AppendLine(string.Join(",", Csv(Cards[i].TeamName), Csv(Cards[i].TeamMembers), totalScores[i], roundFinalized ? 0 : Cards[i].ScoreValue, responseSeconds, Csv(FormatElapsed(responseSeconds))));
                }
                csv.AppendLine("Score status," + Csv(roundFinalized ? "Finalized" : "Current round is provisional"));
                csv.AppendLine("Question,Duration seconds,Elapsed seconds,State");
                csv.AppendLine(string.Join(",", questionNumber, SelectedQuestionSeconds(), SafeSeconds(questionTimer.Elapsed), Csv(questionTimer.State.ToString())));
                csv.AppendLine();
                csv.AppendLine("ROUND-BY-ROUND RECORD");
                csv.AppendLine("Round,Team,Round score,Cumulative score after round,Round elapsed seconds,Round elapsed time,Team response seconds,Team response time,Round type,Participating");
                var cumulativeScores = new long[Cards.Length];
                foreach (var round in rounds)
                {
                    var index = GetTeamIndex(round.DatabaseTeamName);
                    if (index < 0) continue;
                    if (round.RoundNumber <= configuredRounds) cumulativeScores[index] += round.ScoreValue;
                    csv.AppendLine(string.Join(",", round.RoundNumber, Csv(Cards[index].TeamName), round.ScoreValue, cumulativeScores[index], round.ElapsedSeconds, Csv(FormatElapsed(round.ElapsedSeconds)), round.TeamElapsedSeconds, Csv(FormatElapsed(round.TeamElapsedSeconds)), Csv(round.RoundNumber > configuredRounds ? "Tie-break" : "Main competition"), round.IsTieBreakParticipant));
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
            File.WriteAllText(path, csv.ToString(), new UTF8Encoding(true));
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
            base.OnClosing(e);
            if (e.Cancel) return;
            StopActiveTeamTimer();
            questionTimer.Pause();
            try
            {
                SaveEventTitle();
                SaveTeamDetails();
                SaveRoundContext();
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Could not save all settings while closing.", ex);
            }
            if (!PersistSession(GetCurrentSessionStatus()) && MessageBox.Show(this, "The latest timer state could not be saved. Close anyway? Scores already saved remain recorded.", "Unsaved timer state", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) e.Cancel = true;
        }

    }
}
