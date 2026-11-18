using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using Microsoft.Win32;

namespace Score
{
    public partial class QuizSessionHistoryDialog : Window
    {
        public class SessionHistoryItem
        {
            public int SessionId { get; set; }
            public string DeckTitle { get; set; }
            public string Status { get; set; }
            public DateTime StartedAt { get; set; }
            public string FormattedDate => StartedAt.ToLocalTime().ToString("dd MMM yyyy, HH:mm");
            public string TeamsSummary { get; set; }
            public int ResponseCount { get; set; }
        }

        public QuizSessionHistoryDialog()
        {
            InitializeComponent();
            Loaded += (s, e) => LoadHistory();
        }

        private void LoadHistory()
        {
            try
            {
                using (var db = new ScoreDbContext())
                {
                    var sessions = db.QuizDeckSessions
                        .OrderByDescending(s => s.QuizDeckSessionId)
                        .ToList();

                    var deckMap = db.QuizDecks.ToDictionary(d => d.QuizDeckId, d => d.Title);
                    var sessionIds = sessions.Select(s => s.QuizDeckSessionId).ToList();

                    var teamsData = db.QuizDeckSessionTeams
                        .Where(t => sessionIds.Contains(t.QuizDeckSessionId))
                        .GroupBy(t => t.QuizDeckSessionId)
                        .ToDictionary(g => g.Key, g => string.Join(", ", g.Select(x => x.TeamName)));

                    var responseCounts = db.QuizResponses
                        .Where(r => sessionIds.Contains(r.QuizDeckSessionId))
                        .GroupBy(r => r.QuizDeckSessionId)
                        .ToDictionary(g => g.Key, g => g.Count());

                    var items = sessions.Select(s => new SessionHistoryItem
                    {
                        SessionId = s.QuizDeckSessionId,
                        DeckTitle = deckMap.TryGetValue(s.QuizDeckId, out var t) ? t : $"Deck #{s.QuizDeckId}",
                        Status = s.Status ?? "Unknown",
                        StartedAt = s.StartedAtUtc ?? DateTime.UtcNow,
                        TeamsSummary = teamsData.TryGetValue(s.QuizDeckSessionId, out var tm) ? tm : "None",
                        ResponseCount = responseCounts.TryGetValue(s.QuizDeckSessionId, out var rc) ? rc : 0
                    }).ToList();

                    SessionsGrid.ItemsSource = items;
                    HistoryCountText.Text = $"Showing {items.Count} quiz session(s).";
                }
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Failed to load quiz session history.", ex);
                HistoryCountText.Text = "Error loading history: " + ex.Message;
            }
        }

        private void PurgeTestRunsButton_Click(object sender, RoutedEventArgs e)
        {
            var res = MessageBox.Show(this,
                "Are you sure you want to purge all completed and abandoned test runs?\n\nThis will remove past scores, answer logs, and audit entries from testing.\nActive sessions will not be affected.\n\nContinue?",
                "Purge Test Data",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (res != MessageBoxResult.Yes) return;

            try
            {
                using (var db = new ScoreDbContext())
                {
                    using (var transaction = db.Database.BeginTransaction())
                    {
                        var completedSessions = db.QuizDeckSessions
                            .Where(s => s.Status == "Completed" || s.Status == "Abandoned")
                            .ToList();

                        var completedIds = completedSessions.Select(s => s.QuizDeckSessionId).ToList();

                        if (completedIds.Count > 0)
                        {
                            var audits = db.QuizAuditEntries.Where(a => completedIds.Contains(a.QuizDeckSessionId)).ToList();
                            db.QuizAuditEntries.RemoveRange(audits);

                            var responses = db.QuizResponses.Where(r => completedIds.Contains(r.QuizDeckSessionId)).ToList();
                            db.QuizResponses.RemoveRange(responses);

                            var teams = db.QuizDeckSessionTeams.Where(t => completedIds.Contains(t.QuizDeckSessionId)).ToList();
                            db.QuizDeckSessionTeams.RemoveRange(teams);

                            db.QuizDeckSessions.RemoveRange(completedSessions);
                        }

                        db.SaveChanges();
                        transaction.Commit();
                    }
                }

                MessageBox.Show(this, "Test session runs and response logs successfully purged.", "Purge Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadHistory();
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Could not purge test sessions.", ex);
                MessageBox.Show(this, "Purge failed: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ExportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            if (!(SessionsGrid.SelectedItem is SessionHistoryItem selected))
            {
                MessageBox.Show(this, "Please select a quiz session from the table first.", "Export CSV", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dlg = new SaveFileDialog
            {
                FileName = $"QuizExport_Session_{selected.SessionId}_{selected.StartedAt:yyyyMMdd_HHmm}.csv",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };

            if (dlg.ShowDialog() != true) return;

            try
            {
                using (var db = new ScoreDbContext())
                {
                    var responses = db.QuizResponses
                        .Where(r => r.QuizDeckSessionId == selected.SessionId)
                        .OrderBy(r => r.SlideOrderSnapshot)
                        .ToList();

                    var sb = new StringBuilder();
                    sb.AppendLine("SessionId,SlideOrder,TeamName,AnswerText,IsCorrect,PointsAwarded,ResponseTimeSeconds,TimestampUtc");

                    foreach (var r in responses)
                    {
                        sb.AppendLine($"{r.QuizDeckSessionId},{r.SlideOrderSnapshot},\"{r.TeamName}\",\"{r.AnswerText}\",{r.IsCorrect},{r.PointsAwarded},{r.ResponseTimeSeconds},{r.AnsweredAtUtc:o}");
                    }

                    File.WriteAllText(dlg.FileName, sb.ToString(), Encoding.UTF8);
                    MessageBox.Show(this, $"Exported {responses.Count} response records to CSV successfully.", "Export Complete", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Failed to export session CSV.", ex);
                MessageBox.Show(this, "Export failed: " + ex.Message, "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
