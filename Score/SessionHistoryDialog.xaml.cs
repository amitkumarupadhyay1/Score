using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;

namespace Score
{
    public partial class SessionHistoryDialog : Window
    {
        public class SessionHistoryRow
        {
            public int QuizSessionId { get; set; }
            public string Title { get; set; }
            public DateTime? StartedAtUtc { get; set; }
            public string FormattedDate => (StartedAtUtc ?? DateTime.UtcNow).ToLocalTime().ToString("dd MMM yyyy, HH:mm");
            public int ConfiguredTeamCount { get; set; }
            public int ConfiguredRounds { get; set; }
            public string Status { get; set; }
            public bool IsActive { get; set; }
            public string WinnerInfo { get; set; }
        }

        public class TeamScoreSummary
        {
            public string TeamName { get; set; }
            public int TotalScore { get; set; }
            public string ScoreFormatted => $"{TotalScore} pts";
            public string TimeSummary { get; set; }
        }

        public SessionHistoryDialog()
        {
            InitializeComponent();
            Loaded += (s, e) => LoadSessions();
        }

        private void LoadSessions()
        {
            try
            {
                using (var db = new ScoreDbContext())
                {
                    var sessions = db.QuizSessions
                        .OrderByDescending(s => s.QuizSessionId)
                        .ToList();

                    var sessionIds = sessions.Select(s => s.QuizSessionId).ToList();
                    var roundScores = db.QuizRoundScores
                        .Where(r => sessionIds.Contains(r.QuizSessionId) && r.IsFinalized)
                        .ToList();

                    var rows = new List<SessionHistoryRow>();
                    foreach (var s in sessions)
                    {
                        var sScores = roundScores.Where(r => r.QuizSessionId == s.QuizSessionId).ToList();
                        var winner = "No scores";
                        if (sScores.Count > 0)
                        {
                            var topTeam = sScores
                                .GroupBy(r => r.DatabaseTeamName)
                                .Select(g => new { TeamKey = g.Key, Total = g.Sum(x => x.ScoreValue) })
                                .OrderByDescending(x => x.Total)
                                .FirstOrDefault();

                            if (topTeam != null)
                            {
                                var friendlyName = ResolveTeamName(s.QuizSessionId, topTeam.TeamKey);
                                winner = $"{friendlyName} ({topTeam.Total} pts)";
                            }
                        }

                        rows.Add(new SessionHistoryRow
                        {
                            QuizSessionId = s.QuizSessionId,
                            Title = string.IsNullOrWhiteSpace(s.Title) ? "Live Quiz Event" : s.Title,
                            StartedAtUtc = s.StartedAtUtc ?? s.LastUpdatedAtUtc,
                            ConfiguredTeamCount = s.ConfiguredTeamCount,
                            ConfiguredRounds = s.ConfiguredRounds,
                            Status = s.IsActive ? "● Active" : (s.Status ?? "Completed"),
                            IsActive = s.IsActive,
                            WinnerInfo = winner
                        });
                    }

                    SessionsDataGrid.ItemsSource = rows;
                    StatusInfoLabel.Text = $"Showing {rows.Count} session(s).";

                    if (rows.Count > 0)
                    {
                        SessionsDataGrid.SelectedIndex = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Failed to load session history.", ex);
                StatusInfoLabel.Text = "Error loading sessions: " + ex.Message;
            }
        }

        private void SessionsDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = SessionsDataGrid.SelectedItem as SessionHistoryRow;
            if (selected == null)
            {
                DetailTitleLabel.Text = "Select a session";
                DetailMetaLabel.Text = "-";
                TeamScoresListBox.ItemsSource = null;
                return;
            }

            DetailTitleLabel.Text = selected.Title;
            DetailMetaLabel.Text = $"Session #{selected.QuizSessionId} · {selected.FormattedDate} · {selected.ConfiguredRounds} Rounds";

            try
            {
                using (var db = new ScoreDbContext())
                {
                    var roundScores = db.QuizRoundScores
                        .Where(r => r.QuizSessionId == selected.QuizSessionId && r.IsFinalized)
                        .ToList();

                    if (roundScores.Count > 0)
                    {
                        var teamTotals = roundScores
                            .GroupBy(r => r.DatabaseTeamName)
                            .Select(g =>
                            {
                                var teamName = ResolveTeamName(selected.QuizSessionId, g.Key);
                                var totalSec = g.Sum(x => x.TeamElapsedSeconds);
                                return new TeamScoreSummary
                                {
                                    TeamName = teamName,
                                    TotalScore = g.Sum(x => x.ScoreValue),
                                    TimeSummary = $"Total time: {totalSec / 60:D2}:{totalSec % 60:D2} across {g.Count()} round(s)"
                                };
                            })
                            .OrderByDescending(t => t.TotalScore)
                            .ToList();

                        TeamScoresListBox.ItemsSource = teamTotals;
                    }
                    else
                    {
                        var fallbackList = Enumerable.Range(0, Math.Max(2, selected.ConfiguredTeamCount))
                            .Select(i => new TeamScoreSummary
                            {
                                TeamName = ResolveTeamName(selected.QuizSessionId, $"TEAM-{(i + 1):00}"),
                                TotalScore = 0,
                                TimeSummary = "No finalized round scores recorded"
                            }).ToList();
                        TeamScoresListBox.ItemsSource = fallbackList;
                    }
                }
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Could not load score breakdown.", ex);
            }
        }

        private static string ResolveTeamName(int sessionId, string databaseKey)
        {
            try
            {
                var dir = Path.Combine(AppPaths.SettingsDirectory, "Sessions", sessionId.ToString());
                var path = Path.Combine(dir, "team-names.txt");
                if (File.Exists(path))
                {
                    var lines = File.ReadAllLines(path);
                    if (databaseKey != null && databaseKey.StartsWith("TEAM-", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(databaseKey.Substring(5), out var num) && num >= 1 && num <= lines.Length)
                        {
                            var friendly = lines[num - 1].Trim();
                            if (!string.IsNullOrWhiteSpace(friendly)) return friendly;
                        }
                    }
                }
            }
            catch { }

            return databaseKey ?? "Team";
        }

        private void DeleteSessionButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = SessionsDataGrid.SelectedItem as SessionHistoryRow;
            if (selected == null)
            {
                MessageBox.Show(this, "Please select a session to delete.", "Delete Session", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var confirm = MessageBox.Show(this,
                $"Are you sure you want to delete session #{selected.QuizSessionId} ('{selected.Title}')?\n\nThis will permanently remove all associated round scores and event logs.",
                "Confirm Session Deletion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                CascadeDeleteSession(selected.QuizSessionId);
                LoadSessions();
                MessageBox.Show(this, "Session deleted successfully.", "Delete Session", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Failed to delete session.", ex);
                MessageBox.Show(this, "Could not delete session: " + ex.Message, "Delete Session", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteAllInactiveButton_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(this,
                "Are you sure you want to delete ALL inactive past sessions?\n\nThe currently active session will be preserved. All older round scores, event logs, and settings folders will be permanently deleted.",
                "Confirm Bulk Deletion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                int countDeleted = 0;
                using (var db = new ScoreDbContext())
                {
                    var inactiveIds = db.QuizSessions
                        .Where(s => !s.IsActive)
                        .Select(s => s.QuizSessionId)
                        .ToList();

                    foreach (var id in inactiveIds)
                    {
                        CascadeDeleteSession(id);
                        countDeleted++;
                    }
                }

                LoadSessions();
                MessageBox.Show(this, $"Purged {countDeleted} inactive session(s).", "Bulk Purge Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Failed bulk inactive sessions purge.", ex);
                MessageBox.Show(this, "Bulk purge encountered an error: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        internal static void CascadeDeleteSession(int sessionId)
        {
            using (var db = new ScoreDbContext())
            using (var transaction = db.Database.BeginTransaction())
            {
                var roundScores = db.QuizRoundScores.Where(r => r.QuizSessionId == sessionId).ToList();
                db.QuizRoundScores.RemoveRange(roundScores);

                var eventLogs = db.QuizEventLogs.Where(l => l.QuizSessionId == sessionId).ToList();
                db.QuizEventLogs.RemoveRange(eventLogs);

                var session = db.QuizSessions.SingleOrDefault(s => s.QuizSessionId == sessionId);
                if (session != null)
                {
                    db.QuizSessions.Remove(session);
                }

                db.SaveChanges();
                transaction.Commit();
            }

            try
            {
                var dir = Path.Combine(AppPaths.SettingsDirectory, "Sessions", sessionId.ToString());
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                }
            }
            catch { }
        }

        private void ExportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = SessionsDataGrid.SelectedItem as SessionHistoryRow;
            if (selected == null)
            {
                MessageBox.Show(this, "Please select a session to export.", "Export CSV", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                FileName = $"QuizSession_{selected.QuizSessionId}_{DateTime.Now:yyyyMMdd_HHmm}.csv"
            };

            if (dialog.ShowDialog(this) != true) return;

            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("SessionID,Title,Date,Status,TeamKey,TeamName,Round,RoundScore,ElapsedSeconds");

                using (var db = new ScoreDbContext())
                {
                    var roundScores = db.QuizRoundScores
                        .Where(r => r.QuizSessionId == selected.QuizSessionId)
                        .OrderBy(r => r.RoundNumber)
                        .ToList();

                    foreach (var r in roundScores)
                    {
                        var teamFriendly = ResolveTeamName(selected.QuizSessionId, r.DatabaseTeamName);
                        sb.AppendLine($"{selected.QuizSessionId},\"{EscapeCsv(selected.Title)}\",\"{selected.FormattedDate}\",\"{selected.Status}\",\"{r.DatabaseTeamName}\",\"{EscapeCsv(teamFriendly)}\",{r.RoundNumber},{r.ScoreValue},{r.TeamElapsedSeconds}");
                    }
                }

                File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
                MessageBox.Show(this, "Session results exported successfully.", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Could not export session to CSV.", ex);
                MessageBox.Show(this, "Could not export CSV: " + ex.Message, "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static string EscapeCsv(string text)
        {
            return (text ?? string.Empty).Replace("\"", "\"\"");
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
