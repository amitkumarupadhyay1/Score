using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Score
{
    public partial class QuizMasterConsoleWindow : Window
    {
        private void SyncToDeskButton_Click(object sender, RoutedEventArgs e)
        {
            if (_teamScores.Count == 0)
            {
                MessageBox.Show(this, "No scores to synchronize yet.", "Sync to Main Desk", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                // Synchronize scores into ScoreCounts for the main scoreboard
                using (var transaction = _db.Database.BeginTransaction())
                {
                    var existingCounts = _db.ScoreCounts.ToList();
                    foreach (var kvp in _teamScores)
                    {
                        var entry = existingCounts.FirstOrDefault(sc => string.Equals(sc.Team, kvp.Key, StringComparison.OrdinalIgnoreCase));
                        if (entry != null)
                        {
                            entry.ScoreValue = kvp.Value;
                        }
                    }
                    _db.SaveChanges();
                    transaction.Commit();
                }

                ConsoleStatusText.Text = "Synchronized session totals to main Live Scoring Desk.";
                MessageBox.Show(this, "Session team scores have been pushed to the main Live Score Desk.", "Sync Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Score Desk sync failed.", ex);
                MessageBox.Show(this, "Could not sync scores: " + ex.Message, "Sync Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            // If typing inside an editor, don't trigger hotkeys unless Esc
            if (e.OriginalSource is TextBox)
            {
                if (e.Key == Key.Enter && e.OriginalSource == ConsoleAnswerEditor)
                {
                    SubmitAnswerButton_Click(sender, e);
                    e.Handled = true;
                }
                return;
            }

            switch (e.Key)
            {
                case Key.Space:
                    if (_questionTimer.State == QuizTimerState.Running)
                        TimerPauseButton_Click(sender, e);
                    else
                        TimerStartButton_Click(sender, e);
                    e.Handled = true;
                    break;

                case Key.Right:
                case Key.N:
                    NextSlideButton_Click(sender, e);
                    e.Handled = true;
                    break;

                case Key.Left:
                case Key.P:
                    PrevSlideButton_Click(sender, e);
                    e.Handled = true;
                    break;

                case Key.R:
                    RevealAnswerButton_Click(sender, e);
                    e.Handled = true;
                    break;

                case Key.C:
                    MarkCorrectButton_Click(sender, e);
                    e.Handled = true;
                    break;

                case Key.X:
                case Key.Delete:
                    MarkIncorrectButton_Click(sender, e);
                    e.Handled = true;
                    break;

                case Key.B:
                    PassButton_Click(sender, e);
                    e.Handled = true;
                    break;

                case Key.D1:
                case Key.NumPad1:
                    QuickBuzzTeam(0);
                    e.Handled = true;
                    break;
                case Key.D2:
                case Key.NumPad2:
                    QuickBuzzTeam(1);
                    e.Handled = true;
                    break;
                case Key.D3:
                case Key.NumPad3:
                    QuickBuzzTeam(2);
                    e.Handled = true;
                    break;
                case Key.D4:
                case Key.NumPad4:
                    QuickBuzzTeam(3);
                    e.Handled = true;
                    break;
                case Key.D5:
                case Key.NumPad5:
                    QuickBuzzTeam(4);
                    e.Handled = true;
                    break;
                case Key.D6:
                case Key.NumPad6:
                    QuickBuzzTeam(5);
                    e.Handled = true;
                    break;
                case Key.D7:
                case Key.NumPad7:
                    QuickBuzzTeam(6);
                    e.Handled = true;
                    break;
                case Key.D8:
                case Key.NumPad8:
                    QuickBuzzTeam(7);
                    e.Handled = true;
                    break;
                case Key.D9:
                case Key.NumPad9:
                    QuickBuzzTeam(8);
                    e.Handled = true;
                    break;
            }
        }

        private void QuickBuzzTeam(int teamIndex)
        {
            if (teamIndex >= 0 && teamIndex < _teams.Count)
            {
                var team = _teams[teamIndex];
                RegisterBuzz(team);
            }
        }

        private void OpenProjectorButton_Click(object sender, RoutedEventArgs e)
        {
            var projector = new QuizAudienceWindow();
            projector.ShowOnAudienceScreen(this);
            ConsoleStatusText.Text = "Audience view opened on stage monitor.";
        }

        private void ExportResultsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSession == null)
            {
                ConsoleStatusText.Text = "No active quiz session to export.";
                return;
            }

            var safeTitle = string.Join("_", (_currentDeck?.Title ?? "Quiz").Split(System.IO.Path.GetInvalidFileNameChars()));
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"QuizResults_{safeTitle}_{DateTime.UtcNow:yyyyMMdd_HHmm}.csv",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog() != true) return;

            try
            {
                var responses = _db.QuizResponses
                    .Where(r => r.QuizDeckSessionId == _currentSession.QuizDeckSessionId)
                    .OrderBy(r => r.DeckSlideId)
                    .ThenBy(r => r.BuzzOrder)
                    .ToList();

                var slides = _db.DeckSlides
                    .Where(s => s.QuizDeckId == _currentDeck.QuizDeckId)
                    .ToDictionary(s => s.DeckSlideId);

                var lines = new List<string>
                {
                    "SlideNumber,SlideType,RoundName,SlideTitle,TeamName,AnswerText,IsCorrect,PointsAwarded,IsPassAttempt,ResponseTimeSeconds,BuzzOrder,AnsweredAtUtc"
                };

                foreach (var r in responses)
                {
                    slides.TryGetValue(r.DeckSlideId, out var slide);
                    var slideType = r.SlideTypeSnapshot ?? slide?.SlideType ?? string.Empty;
                    var roundName = slide?.RoundName ?? "General";
                    var slideTitle = r.SlideTitleSnapshot ?? slide?.Title ?? string.Empty;
                    var slideNumber = r.SlideOrderSnapshot > 0
                        ? r.SlideOrderSnapshot
                        : slide == null ? r.DeckSlideId : slide.SlideOrder;

                    lines.Add(string.Join(",",
                        slideNumber,
                        ExportSafety.ToCsvCell(slideType),
                        ExportSafety.ToCsvCell(roundName),
                        ExportSafety.ToCsvCell(slideTitle),
                        ExportSafety.ToCsvCell(r.TeamName ?? ""),
                        ExportSafety.ToCsvCell(r.AnswerText ?? ""),
                        r.IsCorrect,
                        r.PointsAwarded,
                        r.IsPassAttempt,
                        r.ResponseTimeSeconds,
                        r.BuzzOrder,
                        ExportSafety.ToCsvCell(r.AnsweredAtUtc?.ToString("u") ?? "")
                    ));
                }

                System.IO.File.WriteAllText(dlg.FileName, string.Join("\n", lines));
                ConsoleStatusText.Text = $"Results exported to {System.IO.Path.GetFileName(dlg.FileName)}.";
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Quiz results export failed.", ex);
                ConsoleStatusText.Text = "Export failed.";
            }
        }

        private void AuditLogButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSession == null)
            {
                ConsoleStatusText.Text = "No active quiz session to audit.";
                return;
            }

            var auditEntries = _db.QuizAuditEntries
                .Where(a => a.QuizDeckSessionId == _currentSession.QuizDeckSessionId)
                .OrderByDescending(a => a.OccurredAtUtc)
                .ToList();

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"AUDIT LOG - Session {_currentSession.QuizDeckSessionId}");
            sb.AppendLine($"Deck: {_currentDeck?.Title}");
            sb.AppendLine($"Started: {_currentSession.StartedAtUtc}");
            sb.AppendLine(new string('-', 60));
            foreach (var a in auditEntries)
            {
                sb.AppendLine($"{a.OccurredAtUtc:yyyy-MM-dd HH:mm:ss} | {a.EventType} | {a.TeamName} | Slide {a.SlideNumber} | {a.OldPoints} -> {a.NewPoints} | {a.Reason}");
            }

            var dialog = new Window
            {
                Title = "Quiz Conducting Audit Trail",
                Width = 850,
                Height = 520,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = this,
                Content = new ScrollViewer
                {
                    Content = new TextBlock
                    {
                        Text = sb.ToString(),
                        FontFamily = new FontFamily("Consolas"),
                        FontSize = 12,
                        Margin = new Thickness(14),
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            };
            dialog.ShowDialog();
        }

        private void UpdateTimerDisplay()
        {
            if (_questionTimer.State == QuizTimerState.Stopped || _questionTimer.State == QuizTimerState.Completed)
            {
                ConsoleTimerLabel.Text = "00:00";
            }
            else
            {
                var ts = _questionTimer.Elapsed;
                ConsoleTimerLabel.Text = $"{(int)ts.TotalMinutes:D2}:{ts.Seconds:D2}";
            }
        }

        private void TimerRefresh_Tick(object sender, EventArgs e)
        {
            _questionTimer.RefreshState();
            UpdateTimerDisplay();
            if (_questionTimer.IsOvertime)
                ConsoleTimerLabel.Foreground = Brushes.Crimson;
            else
                ConsoleTimerLabel.Foreground = (Brush)new BrushConverter().ConvertFromString("#FF38BDF8");

            if (_questionTimer.State == QuizTimerState.Running && _questionTimer.Duration > TimeSpan.Zero)
            {
                var remaining = (int)Math.Ceiling(_questionTimer.Remaining.TotalSeconds);
                if (remaining <= 5 && remaining > 0 && remaining != _lastWarningSecond)
                {
                    _lastWarningSecond = remaining;
                    QuizChimes.PlayTickWarning();
                }
            }
        }

        private void TimerStartButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleTimer(true);
        }

        private void TimerPauseButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleTimer(false);
        }

        private void ToggleTimer(bool start)
        {
            if (_currentSlide == null) return;
            if (start)
            {
                if (_questionTimer.State == QuizTimerState.Stopped && _currentSlide.TimeLimitSeconds > 0)
                {
                    _questionTimer.Start(TimeSpan.FromSeconds(_currentSlide.TimeLimitSeconds));
                    if (_currentSession != null)
                    {
                        _currentSession.TimerElapsedSeconds = 0;
                        _currentSession.TimerStartedAtUtc = DateTime.UtcNow;
                        _currentSession.TimerState = "Running";
                    }
                    _timerRefresh.Start();
                }
                else if (_questionTimer.State == QuizTimerState.Paused)
                {
                    if (_currentSession != null)
                    {
                        _currentSession.TimerElapsedSeconds = (int)_questionTimer.Elapsed.TotalSeconds;
                        _currentSession.TimerStartedAtUtc = DateTime.UtcNow;
                        _currentSession.TimerState = "Running";
                    }
                    _questionTimer.Resume();
                    _timerRefresh.Start();
                }
            }
            else
            {
                _questionTimer.Pause();
                _timerRefresh.Stop();
                if (_currentSession != null)
                {
                    _currentSession.TimerElapsedSeconds = (int)_questionTimer.Elapsed.TotalSeconds;
                    _currentSession.TimerStartedAtUtc = null;
                    _currentSession.TimerState = "Paused";
                }
                UpdateTimerDisplay();
            }
            if (_currentSession != null)
            {
                _db.SaveChanges();
            }
        }
    }
}
