using System;
using System.Windows;
using System.Windows.Media;

namespace Score
{
    public partial class QuizMasterConsoleWindow
    {
        private void UpdateTimerDisplay()
        {
            if (_questionTimer.State == QuizTimerState.Stopped || _questionTimer.State == QuizTimerState.Completed)
            {
                // Show full duration in standby, 00:00 when completed
                if (_questionTimer.State == QuizTimerState.Stopped && _currentSlide != null && _currentSlide.TimeLimitSeconds > 0)
                    ConsoleTimerLabel.Text = TimeSpan.FromSeconds(_currentSlide.TimeLimitSeconds).ToString(@"mm\:ss");
                else
                    ConsoleTimerLabel.Text = "00:00";
            }
            else
            {
                // Show REMAINING countdown (not elapsed) — matches audience screen
                var remaining = _questionTimer.Remaining;
                ConsoleTimerLabel.Text = $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";
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
                else if (remaining == 0 && _lastWarningSecond != 0)
                {
                    _lastWarningSecond = 0;
                    QuizChimes.PlayBuzzer();
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

        private void TimerResetButton_Click(object sender, RoutedEventArgs e)
        {
            ResetQuestionTimer();
        }

        private void TimerAdd15Button_Click(object sender, RoutedEventArgs e)
        {
            AddTimerSeconds(15);
        }

        private void ToggleTimer(bool start)
        {
            if (_currentSlide == null) return;
            if (start)
            {
                // Handle restart if stopped, completed, or already overtime
                if (_questionTimer.State == QuizTimerState.Stopped ||
                    _questionTimer.State == QuizTimerState.Completed ||
                    _questionTimer.IsOvertime)
                {
                    var durSeconds = _currentSlide.TimeLimitSeconds > 0 ? _currentSlide.TimeLimitSeconds : 30;
                    _questionTimer.Start(TimeSpan.FromSeconds(durSeconds));
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
            PushSlideStateToAudience();
        }

        private void ResetQuestionTimer()
        {
            if (_currentSlide == null) return;
            _questionTimer.Reset();
            _timerRefresh.Stop();
            var dur = _currentSlide.TimeLimitSeconds > 0 ? _currentSlide.TimeLimitSeconds : 30;
            _questionTimer.Duration = TimeSpan.FromSeconds(dur);

            if (_currentSession != null)
            {
                _currentSession.TimerElapsedSeconds = 0;
                _currentSession.TimerStartedAtUtc = null;
                _currentSession.TimerState = "Stopped";
                _db.SaveChanges();
            }

            UpdateTimerDisplay();
            PushSlideStateToAudience();
            ConsoleStatusText.Text = $"Timer reset to {dur}s.";
        }

        private void AddTimerSeconds(int seconds)
        {
            if (_currentSlide == null) return;
            var currentRemaining = _questionTimer.Remaining;
            var newRemaining = currentRemaining + TimeSpan.FromSeconds(seconds);
            _questionTimer.Duration = _questionTimer.Elapsed + newRemaining;

            if (_questionTimer.State == QuizTimerState.Completed || _questionTimer.State == QuizTimerState.Overtime)
            {
                _questionTimer.Resume();
                _timerRefresh.Start();
                if (_currentSession != null)
                {
                    _currentSession.TimerStartedAtUtc = DateTime.UtcNow;
                    _currentSession.TimerState = "Running";
                }
            }

            if (_currentSession != null)
            {
                _db.SaveChanges();
            }

            UpdateTimerDisplay();
            PushSlideStateToAudience();
            ConsoleStatusText.Text = $"+{seconds}s added to timer.";
        }
    }
}
