using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Score
{
    public partial class QuizMasterConsoleWindow : Window
    {
        private bool _isRapidFireActive = false;
        private DispatcherTimer _rapidFireTimer;
        private int _rapidFireSecondsRemaining = 60;
        private string _rapidFireTeam = string.Empty;
        private int _rapidFireAttempted = 0;
        private int _rapidFireCorrect = 0;
        private int _rapidFirePointsEarned = 0;

        public bool IsRapidFireActive => _isRapidFireActive;

        private void RapidFireBarrageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isRapidFireActive)
            {
                StopRapidFireBarrage();
            }
            else
            {
                StartRapidFireBarrage();
            }
        }

        private void StopRapidFireButton_Click(object sender, RoutedEventArgs e)
        {
            StopRapidFireBarrage();
        }

        public void StartRapidFireBarrage()
        {
            if (!EnsureSessionActive()) return;

            var selectedTeam = ConsoleTeamList.SelectedValue as string;
            if (string.IsNullOrWhiteSpace(selectedTeam))
            {
                selectedTeam = _currentBuzzingTeam ?? _currentSession?.ActiveTeamOnFloor ?? _teams.FirstOrDefault();
            }

            if (string.IsNullOrWhiteSpace(selectedTeam))
            {
                MessageBox.Show(this, "Please select an active team from the Teams panel before starting Rapid Fire.", "Select Team", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            _isRapidFireActive = true;
            _rapidFireTeam = selectedTeam;
            _rapidFireSecondsRemaining = 60;
            _rapidFireAttempted = 0;
            _rapidFireCorrect = 0;
            _rapidFirePointsEarned = 0;

            // Pause standard slide timer so it doesn't conflict
            _questionTimer.Stop();
            _timerRefresh.Stop();

            if (RapidFireTeamLabel != null)
                RapidFireTeamLabel.Text = $"ACTIVE TEAM: {_rapidFireTeam.ToUpperInvariant()}";

            if (RapidFireClockLabel != null)
            {
                RapidFireClockLabel.Text = "60";
                RapidFireClockLabel.Foreground = Brushes.White;
            }

            if (RapidFireStatsLabel != null)
                RapidFireStatsLabel.Text = "Attempted: 0 | Correct: 0 | Points: +0 | [ENTER] = Correct (+Advance) | [SPACE] = Pass (0 pts +Advance)";

            if (RapidFireBanner != null)
                RapidFireBanner.Visibility = Visibility.Visible;

            if (RapidFireBarrageButton != null)
                RapidFireBarrageButton.Content = "⏹ STOP BARRAGE (Esc)";

            if (_rapidFireTimer == null)
            {
                _rapidFireTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _rapidFireTimer.Tick += RapidFireTimer_Tick;
            }
            _rapidFireTimer.Start();

            QuizChimes.PlayBuzzer();
            ConsoleStatusText.Text = $"⚡ Rapid Fire Barrage started for {_rapidFireTeam} (60s countdown active).";
        }

        private void RapidFireTimer_Tick(object sender, EventArgs e)
        {
            if (!_isRapidFireActive) return;

            _rapidFireSecondsRemaining--;
            if (_rapidFireSecondsRemaining > 0)
            {
                if (RapidFireClockLabel != null)
                {
                    RapidFireClockLabel.Text = _rapidFireSecondsRemaining.ToString();
                    if (_rapidFireSecondsRemaining <= 5)
                    {
                        QuizChimes.PlayTickWarning();
                        RapidFireClockLabel.Foreground = (Brush)(new BrushConverter().ConvertFromString("#FFEF4444"));
                    }
                }
            }
            else
            {
                if (RapidFireClockLabel != null)
                {
                    RapidFireClockLabel.Text = "00";
                    RapidFireClockLabel.Foreground = (Brush)(new BrushConverter().ConvertFromString("#FFEF4444"));
                }
                FinishRapidFireBarrage();
            }
        }

        public void StopRapidFireBarrage()
        {
            if (!_isRapidFireActive) return;
            FinishRapidFireBarrage();
        }

        private void FinishRapidFireBarrage()
        {
            _rapidFireTimer?.Stop();
            _isRapidFireActive = false;

            if (RapidFireBanner != null)
                RapidFireBanner.Visibility = Visibility.Collapsed;

            if (RapidFireBarrageButton != null)
                RapidFireBarrageButton.Content = "⚡ RAPID FIRE (60s)";

            QuizChimes.PlayRoundFinished();

            var summary = $"⚡ 60s RAPID FIRE BARRAGE COMPLETE\n\n" +
                          $"Team: {_rapidFireTeam}\n" +
                          $"Total Questions Attempted: {_rapidFireAttempted}\n" +
                          $"Correct Answers: {_rapidFireCorrect}\n" +
                          $"Total Points Earned: +{_rapidFirePointsEarned} pts\n\n" +
                          $"Current Standing: {(_teamScores.TryGetValue(_rapidFireTeam, out var total) ? total : 0)} pts";

            MessageBox.Show(this, summary, "Rapid Fire Barrage Summary", MessageBoxButton.OK, MessageBoxImage.Information);
            ConsoleStatusText.Text = $"Barrage finished: {_rapidFireTeam} scored +{_rapidFirePointsEarned} pts ({_rapidFireCorrect}/{_rapidFireAttempted} correct).";
            PushSlideStateToAudience();
        }

        public bool HandleRapidFireKey(Key key)
        {
            if (!_isRapidFireActive) return false;

            if (key == Key.Enter)
            {
                RapidFireRecordAnswer(true);
                return true;
            }

            if (key == Key.Space)
            {
                RapidFireRecordAnswer(false);
                return true;
            }

            if (key == Key.Escape)
            {
                StopRapidFireBarrage();
                return true;
            }

            return false;
        }

        private void RapidFireRecordAnswer(bool isCorrect)
        {
            if (!_isRapidFireActive || _currentSlide == null) return;

            _rapidFireAttempted++;
            int points = 0;

            if (isCorrect)
            {
                _rapidFireCorrect++;
                points = _currentSlide.Points > 0 ? _currentSlide.Points : 10;
                _rapidFirePointsEarned += points;

                var currentScore = _teamScores.TryGetValue(_rapidFireTeam, out var s) ? s : 0;
                var updatedScore = currentScore + points;
                _teamScores[_rapidFireTeam] = updatedScore;

                if (_currentSession != null)
                {
                    var responseRecord = new QuizResponse
                    {
                        QuizDeckSessionId = _currentSession.QuizDeckSessionId,
                        DeckSlideId = _currentSlide.DeckSlideId,
                        TeamName = _rapidFireTeam,
                        AnswerText = "[Rapid Fire Correct]",
                        AnsweredAtUtc = DateTime.UtcNow,
                        ResponseTimeSeconds = Math.Max(0, 60 - _rapidFireSecondsRemaining),
                        IsCorrect = true,
                        PointsAwarded = points,
                        BuzzOrder = 1,
                        SlideTitleSnapshot = _currentSlide.Title,
                        SlideTypeSnapshot = _currentSlide.SlideType,
                        SlideOrderSnapshot = _currentSlideIndex + 1,
                        IsPassAttempt = false
                    };
                    _db.QuizResponses.Add(responseRecord);

                    _db.QuizAuditEntries.Add(new QuizAuditEntry
                    {
                        QuizDeckSessionId = _currentSession.QuizDeckSessionId,
                        OccurredAtUtc = DateTime.UtcNow,
                        EventType = "RapidFireCorrect",
                        TeamName = _rapidFireTeam,
                        SlideNumber = _currentSlideIndex + 1,
                        OldPoints = currentScore,
                        NewPoints = updatedScore,
                        Reason = $"Rapid fire barrage +{points} pts"
                    });
                    _db.SaveChanges();
                }

                QuizChimes.PlayCorrect();
            }
            else
            {
                // Pass: 0 points
                if (_currentSession != null)
                {
                    _db.QuizAuditEntries.Add(new QuizAuditEntry
                    {
                        QuizDeckSessionId = _currentSession.QuizDeckSessionId,
                        OccurredAtUtc = DateTime.UtcNow,
                        EventType = "RapidFirePass",
                        TeamName = _rapidFireTeam,
                        SlideNumber = _currentSlideIndex + 1,
                        OldPoints = _teamScores.TryGetValue(_rapidFireTeam, out var sc) ? sc : 0,
                        NewPoints = _teamScores.TryGetValue(_rapidFireTeam, out var sc2) ? sc2 : 0,
                        Reason = "Rapid fire pass (0 pts)"
                    });
                    _db.SaveChanges();
                }

                QuizChimes.PlayBounce();
            }

            if (RapidFireStatsLabel != null)
                RapidFireStatsLabel.Text = $"Attempted: {_rapidFireAttempted} | Correct: {_rapidFireCorrect} | Points: +{_rapidFirePointsEarned} | [ENTER] = Correct (+Advance) | [SPACE] = Pass (0 pts +Advance)";

            RefreshTeams();

            // Auto-advance slide immediately
            AdvanceSlide(1);
        }
    }
}
