using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Score
{
    public partial class QuizMasterConsoleWindow : Window
    {
        private bool EnsureSessionActive()
        {
            if (_currentSession != null && (_currentSession.Status == "Running" || _currentSession.Status == "Paused")) return true;
            if (IsHeadlessTestEnvironment()) return false;

            var res = MessageBox.Show(this,
                "No quiz session is currently running.\n\nWould you like to start the quiz now with registered teams?",
                "Start Live Quiz",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);
            if (res == MessageBoxResult.Yes)
            {
                StartQuizButton_Click(this, new RoutedEventArgs());
                return _currentSession != null;
            }
            return false;
        }

        private void PassButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureSessionActive()) return;
            if (_teams.Count == 0 || _currentSlide == null || !_currentSlide.AllowPass) return;

            QuizChimes.PlayBounce();

            var activeTeam = _currentSession?.ActiveTeamOnFloor ?? (ConsoleTeamList.SelectedValue as string);
            var currentIdx = _teams.IndexOf(activeTeam ?? string.Empty);
            var nextIdx = (currentIdx + 1) % _teams.Count;
            var nextTeam = _teams[nextIdx];

            _passCount++;
            _currentSession.PassCount = _passCount;
            _currentSession.ActiveTeamOnFloor = nextTeam;
            ConsoleTeamList.SelectedValue = nextTeam;

            _db.QuizAuditEntries.Add(new QuizAuditEntry
            {
                QuizDeckSessionId = _currentSession.QuizDeckSessionId,
                OccurredAtUtc = DateTime.UtcNow,
                EventType = "PassedToTeam",
                TeamName = nextTeam,
                SlideNumber = _currentSlideIndex + 1,
                Reason = $"Answer bounced to {nextTeam} (Pass #{_passCount})"
            });
            _db.SaveChanges();

            UpdateFloorBanner();
            RefreshTeams();
            PushSlideStateToAudience();
            ConsoleStatusText.Text = $"Question passed to {nextTeam} (Bounce #{_passCount}).";
        }

        private void ConsoleTeamList_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (_refreshingTeamList || ConsoleTeamList.SelectedItem == null) return;
            var selectedTeam = ConsoleTeamList.SelectedValue as string;
            if (string.IsNullOrWhiteSpace(selectedTeam))
            {
                dynamic item = ConsoleTeamList.SelectedItem;
                try { selectedTeam = item?.Team as string; } catch { }
            }
            if (!string.IsNullOrWhiteSpace(selectedTeam))
            {
                if (_currentBuzzingTeam != null)
                {
                    _currentBuzzingTeam = selectedTeam;
                }
                if (_currentSession != null)
                {
                    _currentSession.ActiveTeamOnFloor = selectedTeam;
                    _db.SaveChanges();
                }
                UpdateFloorBanner();
                PushSlideStateToAudience();
            }
        }

        private void AcceptBuzzButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureSessionActive()) return;
            var targetTeam = ConsoleTeamList.SelectedValue as string;
            if (string.IsNullOrWhiteSpace(targetTeam) && _teams.Count > 0)
            {
                targetTeam = _teams[0];
            }
            if (!string.IsNullOrWhiteSpace(targetTeam))
            {
                RegisterBuzz(targetTeam);
            }
        }

        private void RegisterBuzz(string teamName)
        {
            if (_currentSession == null || _currentSlide == null || string.IsNullOrWhiteSpace(teamName)) return;

            var isQuestion = string.Equals(_currentSlide.SlideType ?? "Question", "Question", StringComparison.OrdinalIgnoreCase);
            if (!isQuestion)
            {
                ConsoleStatusText.Text = "Buzzers disabled: Current slide is not a live question.";
                return;
            }

            if (_hasCorrectBuzzForSlide)
            {
                ConsoleStatusText.Text = "Buzzers disabled: Question has already been answered correctly.";
                return;
            }

            if (_buzzedTeams.Contains(teamName))
            {
                ConsoleStatusText.Text = teamName + " has already buzzed on this question.";
                return;
            }

            // Auto-pause timer when accepting buzz so countdown warning ticks stop while the host speaks
            ToggleTimer(false);

            QuizChimes.PlayBuzzer();

            _buzzOrderCounter++;
            _buzzedTeams.Add(teamName);
            _buzzQueue.Add(teamName);

            if (_currentBuzzingTeam == null)
            {
                _currentBuzzingTeam = teamName;
                _currentSession.ActiveTeamOnFloor = teamName;
            }

            _db.QuizAuditEntries.Add(new QuizAuditEntry
            {
                QuizDeckSessionId = _currentSession.QuizDeckSessionId,
                OccurredAtUtc = DateTime.UtcNow,
                EventType = "BuzzAccepted",
                TeamName = teamName,
                SlideNumber = _currentSlideIndex + 1,
                Reason = "Buzz queue position " + _buzzOrderCounter
            });
            _db.SaveChanges();

            RefreshTeams();
            UpdateFloorBanner();
            PushSlideStateToAudience();
            _audienceWindow?.PushBuzzAlert(teamName, _buzzOrderCounter);
            ConsoleStatusText.Text = $"Buzz #{_buzzOrderCounter} registered: {teamName}";
        }

        private void MarkCorrectButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureSessionActive()) return;
            QuizChimes.PlayCorrect();
            RecordAnswer(true);
        }

        private void MarkIncorrectButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureSessionActive()) return;
            QuizChimes.PlayIncorrect();
            RecordAnswer(false);
        }

        private void RecordAnswer(bool isCorrect)
        {
            if (_currentSession == null || _currentSlide == null) return;
            var teamName = _currentBuzzingTeam ?? _currentSession.ActiveTeamOnFloor ?? (ConsoleTeamList.SelectedValue as string);
            if (string.IsNullOrWhiteSpace(teamName))
            {
                ConsoleStatusText.Text = "Select a team before scoring an answer.";
                return;
            }

            // Auto-pause timer when scoring Correct/Wrong so countdown warning ticks stop while the host speaks
            ToggleTimer(false);

            var answer = ConsoleAnswerEditor.Text ?? string.Empty;
            var responseTime = (int)_questionTimer.Elapsed.TotalSeconds;
            var currentScore = _teamScores.TryGetValue(teamName, out var score) ? score : 0;
            var isPass = _passCount > 0;

            var points = QuizScoringEngine.CalculatePoints(
                _currentSlide.RoundType ?? QuizScoringEngine.RoundStandard,
                _currentSlide.Points,
                responseTime,
                _currentSlide.TimeLimitSeconds,
                isCorrect,
                !_hasCorrectBuzzForSlide,
                _buzzOrderCounter,
                _currentSlide.PenaltyPoints,
                isPass,
                _currentSlide.BonusPoints);

            var updatedScore = currentScore + points;

            _db.QuizResponses.Add(new QuizResponse
            {
                QuizDeckSessionId = _currentSession.QuizDeckSessionId,
                DeckSlideId = _currentSlide.DeckSlideId,
                TeamName = teamName,
                AnswerText = answer,
                AnsweredAtUtc = DateTime.UtcNow,
                ResponseTimeSeconds = responseTime,
                IsCorrect = isCorrect,
                PointsAwarded = points,
                BuzzOrder = _buzzOrderCounter,
                SlideTitleSnapshot = _currentSlide.Title,
                SlideTypeSnapshot = _currentSlide.SlideType,
                SlideOrderSnapshot = _currentSlideIndex + 1,
                IsPassAttempt = isPass
            });

            _db.QuizAuditEntries.Add(new QuizAuditEntry
            {
                QuizDeckSessionId = _currentSession.QuizDeckSessionId,
                OccurredAtUtc = DateTime.UtcNow,
                EventType = "ResponseScored",
                TeamName = teamName,
                SlideNumber = _currentSlideIndex + 1,
                OldPoints = currentScore,
                NewPoints = updatedScore,
                Reason = isCorrect ? (isPass ? "Correct on pass" : "Marked correct") : "Marked incorrect"
            });
            _db.SaveChanges();

            _teamScores[teamName] = updatedScore;

            if (isCorrect)
            {
                _hasCorrectBuzzForSlide = true;
                _currentBuzzingTeam = null;
                _buzzQueue.Clear();
                if (_buzzerServer != null)
                {
                    _buzzerServer.IsBuzzerArmed = false;
                }
            }
            else
            {
                // If wrong and there is a buzz queue, pop team and advance to next in queue
                _buzzQueue.Remove(teamName);
                _currentBuzzingTeam = _buzzQueue.FirstOrDefault();
                if (_currentBuzzingTeam != null)
                {
                    _currentSession.ActiveTeamOnFloor = _currentBuzzingTeam;
                }
                else
                {
                    _currentSession.ActiveTeamOnFloor = null;
                }
            }

            RefreshTeams();
            UpdateFloorBanner();
            PushSlideStateToAudience();
            ConsoleStatusText.Text = isCorrect ? $"Correct: {teamName} +{points} pts" : $"Incorrect: {teamName} {points} pts";
            ConsoleAnswerEditor.Clear();
        }

        private void RevealAnswerButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSession == null || _currentSlide == null) return;
            _currentSession.AnswerRevealed = !_currentSession.AnswerRevealed;
            _db.SaveChanges();
            UpdateRevealButtonState();
            ConsoleStatusText.Text = _currentSession.AnswerRevealed ? "Answer revealed on audience screen." : "Answer hidden.";
            PushSlideStateToAudience();
        }

        private void QuickOptionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureSessionActive()) return;
            if (sender is System.Windows.Controls.Button btn && btn.Content != null)
            {
                ConsoleAnswerEditor.Text = btn.Content.ToString();
                SubmitAnswerButton_Click(sender, e);
            }
        }

        private void SubmitAnswerButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureSessionActive()) return;
            if (_currentSession == null || _currentSlide == null) return;
            var isCorrect = QuizScoringEngine.IsCorrectAnswer(
                _currentSlide.QuestionType ?? QuizScoringEngine.QuestionMultipleChoice,
                _currentSlide.CorrectAnswer ?? string.Empty,
                ConsoleAnswerEditor.Text,
                _currentSlide.AlternateAnswers,
                out _);
            RecordAnswer(isCorrect);
        }

        private void OverrideScoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (!EnsureSessionActive()) return;
            OverrideReasonPanel.Visibility = OverrideReasonPanel.Visibility == Visibility.Visible
                ? Visibility.Collapsed : Visibility.Visible;
        }

        private void CancelOverrideButton_Click(object sender, RoutedEventArgs e)
        {
            OverrideReasonPanel.Visibility = Visibility.Collapsed;
            OverrideReasonEditor.Clear();
            OverrideTotalEditor.Clear();
        }

        private void ApplyOverrideButton_Click(object sender, RoutedEventArgs e)
        {
            var teamName = (ConsoleTeamList.SelectedValue as string) ?? _currentBuzzingTeam;
            if (string.IsNullOrWhiteSpace(teamName) ||
                !int.TryParse(OverrideTotalEditor.Text, out var newTotal) ||
                string.IsNullOrWhiteSpace(OverrideReasonEditor.Text))
            {
                ConsoleStatusText.Text = "Select a team, enter a valid total, and provide a reason.";
                return;
            }
            var oldTotal = _teamScores.TryGetValue(teamName, out var score) ? score : 0;
            var pointDelta = newTotal - oldTotal;

            _db.QuizAuditEntries.Add(new QuizAuditEntry
            {
                QuizDeckSessionId = _currentSession.QuizDeckSessionId,
                OccurredAtUtc = DateTime.UtcNow,
                EventType = "ScoreOverride",
                TeamName = teamName,
                SlideNumber = _currentSlideIndex + 1,
                OldPoints = oldTotal,
                NewPoints = newTotal,
                Reason = OverrideReasonEditor.Text.Trim()
            });

            _db.QuizResponses.Add(new QuizResponse
            {
                QuizDeckSessionId = _currentSession.QuizDeckSessionId,
                DeckSlideId = _currentSlide.DeckSlideId,
                TeamName = teamName,
                AnswerText = "[ADJUDICATION OVERRIDE]",
                AnsweredAtUtc = DateTime.UtcNow,
                PointsAwarded = pointDelta,
                SlideTitleSnapshot = _currentSlide.Title,
                SlideTypeSnapshot = _currentSlide.SlideType,
                SlideOrderSnapshot = _currentSlideIndex + 1
            });
            _db.SaveChanges();

            _teamScores[teamName] = newTotal;
            RefreshTeams();
            ConsoleStatusText.Text = $"Score updated: {teamName} {oldTotal} -> {newTotal}.";
            OverrideReasonPanel.Visibility = Visibility.Collapsed;
            OverrideReasonEditor.Clear();
            OverrideTotalEditor.Clear();
        }

    }
}
