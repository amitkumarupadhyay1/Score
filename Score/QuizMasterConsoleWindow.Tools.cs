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
            if (_isRapidFireActive && HandleRapidFireKey(e.Key))
            {
                e.Handled = true;
                return;
            }

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

            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (e.Key == Key.Z)
                {
                    UndoScoreButton_Click(sender, e);
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.OemOpenBrackets)
                {
                    ToggleLeftPanel();
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.OemCloseBrackets)
                {
                    ToggleRightPanel();
                    e.Handled = true;
                    return;
                }
                if (e.Key == Key.F)
                {
                    ToggleFocusMode();
                    e.Handled = true;
                    return;
                }
            }

            switch (e.Key)
            {
                case Key.Back:
                    DismissBuzzButton_Click(sender, e);
                    e.Handled = true;
                    break;

                case Key.A:
                    ToggleBuzzerArming();
                    e.Handled = true;
                    break;

                case Key.M:
                    MediaPlayPauseButton_Click(sender, e);
                    e.Handled = true;
                    break;

                case Key.Space:
                    if (_questionTimer.State == QuizTimerState.Running)
                        TimerPauseButton_Click(sender, e);
                    else
                        TimerStartButton_Click(sender, e);
                    e.Handled = true;
                    break;

                case Key.PageDown:
                case Key.Right:
                case Key.N:
                    AdvanceSlide(1);
                    e.Handled = true;
                    break;

                case Key.PageUp:
                case Key.Left:
                    AdvanceSlide(-1);
                    e.Handled = true;
                    break;

                case Key.P:
                    PounceToggleButton_Click(sender, e);
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

                case Key.S:
                    PassButton_Click(sender, e);
                    e.Handled = true;
                    break;

                case Key.B:
                    ToggleBlackoutDesk();
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
            if (_audienceWindow != null && _audienceWindow.IsLoaded)
            {
                if (_audienceWindow.WindowState == WindowState.Minimized)
                {
                    _audienceWindow.WindowState = WindowState.Normal;
                }
                _audienceWindow.Activate();
                _audienceWindow.Focus();
                ConsoleStatusText.Text = "Audience view brought to front.";
                PushSlideStateToAudience();
                return;
            }

            _audienceWindow = new QuizAudienceWindow();
            _audienceWindow.Closed += (s, args) => { _audienceWindow = null; };
            _audienceWindow.ShowOnAudienceScreen(this);
            ConsoleStatusText.Text = "Audience view opened on stage monitor.";
            PushSlideStateToAudience();
        }

        internal void PushSlideStateToAudience()
        {
            if (_audienceWindow != null && _audienceWindow.IsLoaded)
            {
                _audienceWindow.PushSlideState(_currentSlide, _currentSession, _currentSession?.AnswerRevealed ?? false, _currentSession?.ActiveTeamOnFloor, _clueStep);
                _audienceWindow.PushScoreboard(_teams, _teamScores);
            }
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
            dialog.PreviewKeyDown += (s, ev) =>
            {
                if (ev.Key == Key.Escape)
                {
                    dialog.Close();
                    ev.Handled = true;
                }
            };
            dialog.ShowDialog();
        }



        private void UpdateClueDisplay(string[] hints)
        {
            if (_clueStep == 0)
            {
                ClueStatusLabel.Text = $"0 of {hints.Length} clues revealed";
                RevealClueButton.Content = "💡 REVEAL CLUE 1";
            }
            else if (_clueStep <= hints.Length)
            {
                ClueStatusLabel.Text = $"Clue {_clueStep}/{hints.Length}: \"{hints[_clueStep - 1]}\"";
                RevealClueButton.Content = _clueStep < hints.Length ? $"💡 REVEAL CLUE {_clueStep + 1}" : "ALL CLUES REVEALED";
            }
        }

        private void RevealClueButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSlide == null) return;
            var hints = (_currentSlide.HintsJson ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (_clueStep < hints.Length)
            {
                _clueStep++;
                if (_currentSession != null)
                {
                    _currentSession.ClueRevealStep = _clueStep;
                    _db.SaveChanges();
                }
                UpdateClueDisplay(hints);
                ApplyClueDecay();
                ConsoleStatusText.Text = $"Clue {_clueStep} revealed to the audience.";
                PushSlideStateToAudience();
            }
        }

        private void UpdateFloorBanner()
        {
            if (_currentSlide == null)
            {
                TurnBanner.Visibility = Visibility.Collapsed;
                return;
            }

            TurnBanner.Visibility = Visibility.Visible;
            var conv = new BrushConverter();

            if (_currentSession == null)
            {
                TurnBanner.Background = (Brush)conv.ConvertFromString("#FFF0FDF4");
                TurnBanner.BorderBrush = (Brush)conv.ConvertFromString("#FFBBF7D0");
                TurnStatusLabel.Text = "✦ READY TO CONDUCT · CLICK '▶ START QUIZ'";
                TurnStatusLabel.Foreground = (Brush)conv.ConvertFromString("#FF166534");
                TurnDetailsLabel.Text = "Browse and preview questions above. Click 'START QUIZ' in the top bar to register teams and enable live buzzers.";
                TurnDetailsLabel.Foreground = (Brush)conv.ConvertFromString("#FF15803D");
                PassButton.Visibility = Visibility.Collapsed;
                return;
            }

            PassButton.Visibility = _currentSlide.AllowPass ? Visibility.Visible : Visibility.Collapsed;
            DismissBuzzButton.Visibility = !string.IsNullOrWhiteSpace(_currentBuzzingTeam) ? Visibility.Visible : Visibility.Collapsed;

            if (_hasCorrectBuzzForSlide)
            {
                TurnBanner.Background = (Brush)conv.ConvertFromString("#FFDCFCE7");
                TurnBanner.BorderBrush = (Brush)conv.ConvertFromString("#FF86EFAC");
                TurnStatusLabel.Text = "✓ QUESTION COMPLETED & SCORED · PRESS NEXT (N) ▶";
                TurnStatusLabel.Foreground = (Brush)conv.ConvertFromString("#FF166534");
                TurnDetailsLabel.Text = "Correct answer recorded. Advance to the next question or reveal answer on stage.";
                TurnDetailsLabel.Foreground = (Brush)conv.ConvertFromString("#FF15803D");
                return;
            }

            if (!string.IsNullOrWhiteSpace(_currentBuzzingTeam))
            {
                TurnBanner.Background = (Brush)conv.ConvertFromString("#FFFEE2E2");
                TurnBanner.BorderBrush = (Brush)conv.ConvertFromString("#FFFCA5A5");
                TurnStatusLabel.Text = $"🚨 {_currentBuzzingTeam.ToUpperInvariant()} HAS BUZZED · ANSWERING NOW";
                TurnStatusLabel.Foreground = (Brush)conv.ConvertFromString("#FFB91C1C");
                TurnDetailsLabel.Text = "Evaluate team response or click Mark Correct (C) / Mark Wrong (X).";
                TurnDetailsLabel.Foreground = (Brush)conv.ConvertFromString("#FF991B1B");
                return;
            }

            var activeTeam = _currentSession?.ActiveTeamOnFloor ?? (ConsoleTeamList.SelectedValue as string);
            if (!string.IsNullOrWhiteSpace(activeTeam))
            {
                TurnBanner.Background = (Brush)conv.ConvertFromString("#FFFEF9C3");
                TurnBanner.BorderBrush = (Brush)conv.ConvertFromString("#FFFDE047");
                TurnStatusLabel.Foreground = (Brush)conv.ConvertFromString("#FF854D0E");
                TurnDetailsLabel.Foreground = (Brush)conv.ConvertFromString("#FFA16207");

                if (_passCount == 0)
                {
                    TurnStatusLabel.Text = $"ACTIVE FLOOR: {activeTeam.ToUpperInvariant()}";
                    TurnDetailsLabel.Text = $"Direct question · Full score: +{_currentSlide.Points} pts";
                }
                else
                {
                    var bonus = _currentSlide.BonusPoints > 0 ? _currentSlide.BonusPoints : (_currentSlide.Points / 2);
                    TurnStatusLabel.Text = $"BOUNCE #{_passCount}: {activeTeam.ToUpperInvariant()}";
                    TurnDetailsLabel.Text = $"Passed question · Bonus score: +{bonus} pts";
                }
            }
            else
            {
                TurnBanner.Background = (Brush)conv.ConvertFromString("#FFF1F5F9");
                TurnBanner.BorderBrush = (Brush)conv.ConvertFromString("#FFE2E8F0");
                TurnStatusLabel.Text = "✦ OPEN FLOOR · WAITING FOR TEAM BUZZ";
                TurnStatusLabel.Foreground = (Brush)conv.ConvertFromString("#FF475569");
                TurnDetailsLabel.Text = "Press keys 1-9 or let teams buzz in from their mobile devices.";
                TurnDetailsLabel.Foreground = (Brush)conv.ConvertFromString("#FF64748B");
            }
        }

        private void UpdateRevealButtonState()
        {
            var isRevealed = _currentSession != null && _currentSession.AnswerRevealed;
            var conv = new BrushConverter();
            if (isRevealed)
            {
                RevealAnswerButton.Content = "👁 HIDE ANSWER (R)";
                RevealAnswerButton.Background = (Brush)conv.ConvertFromString("#FFE11D48");
                RevealAnswerButton.BorderBrush = (Brush)conv.ConvertFromString("#FFBE123C");
            }
            else
            {
                RevealAnswerButton.Content = "REVEAL ON STAGE (R)";
                RevealAnswerButton.Background = (Brush)conv.ConvertFromString("#FF2563EB");
                RevealAnswerButton.BorderBrush = (Brush)conv.ConvertFromString("#FF1D4ED8");
            }
        }

        private bool _isBlackoutActive = false;

        private void BlackoutToggleButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleBlackoutDesk();
        }

        private void ToggleBlackoutDesk()
        {
            _isBlackoutActive = !_isBlackoutActive;
            if (_audienceWindow != null && _audienceWindow.IsLoaded)
            {
                _audienceWindow.ToggleBlackout(_isBlackoutActive);
            }

            if (StageBlackoutPrompterBanner != null)
                StageBlackoutPrompterBanner.Visibility = _isBlackoutActive ? Visibility.Visible : Visibility.Collapsed;

            var conv = new BrushConverter();
            if (BlackoutToggleButton != null)
            {
                BlackoutToggleButton.Background = (Brush)conv.ConvertFromString(_isBlackoutActive ? "#FFDC2626" : "#FF334155");
                BlackoutToggleButton.Content = _isBlackoutActive ? "⬛ BLACKOUT ON (B)" : "⬛ BLACKOUT (B)";
            }

            if (_isBlackoutActive)
            {
                ToggleTimer(false);
                if (_buzzerServer != null)
                {
                    _buzzerServer.IsBuzzerArmed = false;
                    UpdateBuzzerArmButtonDisplay();
                }
                ConsoleStatusText.Text = "Emergency Stage Blackout engaged. Projector is black.";
            }
            else
            {
                if (_buzzerServer != null && !_hasCorrectBuzzForSlide && _currentSlide?.SlideType == "Question")
                {
                    _buzzerServer.IsBuzzerArmed = true;
                    UpdateBuzzerArmButtonDisplay();
                }
                ConsoleStatusText.Text = "Stage Blackout lifted.";
            }
        }

        private void MediaPlayPauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_audienceWindow != null && _audienceWindow.IsLoaded)
            {
                if (MediaPlayPauseButton.Content.ToString().Contains("PLAY"))
                {
                    _audienceWindow.ResumeMediaPlayback();
                    MediaPlayPauseButton.Content = "⏸ PAUSE (M)";
                }
                else
                {
                    _audienceWindow.PauseMediaPlayback();
                    MediaPlayPauseButton.Content = "▶ PLAY (M)";
                }
            }
        }

        private void MediaReplayButton_Click(object sender, RoutedEventArgs e)
        {
            if (_audienceWindow != null && _audienceWindow.IsLoaded)
            {
                _audienceWindow.ReplayMediaPlayback();
                MediaPlayPauseButton.Content = "⏸ PAUSE (M)";
            }
        }

        private void MediaVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_audienceWindow != null && _audienceWindow.IsLoaded)
            {
                _audienceWindow.SetMediaVolume(e.NewValue);
            }
        }
    }
}
