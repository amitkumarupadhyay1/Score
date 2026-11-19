using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Score
{
    public partial class QuizMasterConsoleWindow : Window
    {
        private readonly Dictionary<string, double> _buzzQueueDeltas = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        private long _firstBuzzSwTicks = 0;

        public void DismissCurrentBuzz(bool allowRebuzz = false)
        {
            if (_currentSession == null || (_currentBuzzingTeam == null && _buzzQueue.Count == 0))
            {
                ConsoleStatusText.Text = "No active buzz to dismiss.";
                return;
            }

            var dismissedTeam = _currentBuzzingTeam ?? _buzzQueue.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(dismissedTeam)) return;

            _buzzQueue.Remove(dismissedTeam);
            _buzzQueueDeltas.Remove(dismissedTeam);

            if (allowRebuzz)
            {
                _buzzedTeams.Remove(dismissedTeam);
            }

            _db.QuizAuditEntries.Add(new QuizAuditEntry
            {
                QuizDeckSessionId = _currentSession.QuizDeckSessionId,
                OccurredAtUtc = DateTime.UtcNow,
                EventType = "BuzzDismissed",
                TeamName = dismissedTeam,
                SlideNumber = _currentSlideIndex + 1,
                Reason = allowRebuzz
                    ? "Host dismissed false buzz (Floor open for rebuzz)"
                    : "Host dismissed false buzz (Pass to next in queue)"
            });
            _db.SaveChanges();

            _currentBuzzingTeam = _buzzQueue.FirstOrDefault();
            _currentSession.ActiveTeamOnFloor = _currentBuzzingTeam;
            _db.SaveChanges();

            if (_currentBuzzingTeam == null)
            {
                _firstBuzzSwTicks = 0;
                if (_buzzerServer != null && !_hasCorrectBuzzForSlide && _currentSlide?.SlideType == "Question")
                {
                    _buzzerServer.IsBuzzerArmed = true;
                }
            }

            RefreshTeams();
            UpdateFloorBanner();
            PushSlideStateToAudience();
            ConsoleStatusText.Text = $"Buzz dismissed for {dismissedTeam}. " +
                (_currentBuzzingTeam != null ? $"Active floor: {_currentBuzzingTeam}" : "Floor is open.");
        }

        public void ClearBuzzQueue(bool allowRebuzzAll = false)
        {
            if (_currentSession == null || _buzzQueue.Count == 0) return;

            if (allowRebuzzAll)
            {
                foreach (var t in _buzzQueue)
                {
                    _buzzedTeams.Remove(t);
                }
            }

            _buzzQueue.Clear();
            _buzzQueueDeltas.Clear();
            _firstBuzzSwTicks = 0;
            _currentBuzzingTeam = null;

            if (_currentSession != null)
            {
                _currentSession.ActiveTeamOnFloor = null;
                _db.SaveChanges();
            }

            if (_buzzerServer != null && !_hasCorrectBuzzForSlide && _currentSlide?.SlideType == "Question")
            {
                _buzzerServer.IsBuzzerArmed = true;
            }

            _db.QuizAuditEntries.Add(new QuizAuditEntry
            {
                QuizDeckSessionId = _currentSession?.QuizDeckSessionId ?? 0,
                OccurredAtUtc = DateTime.UtcNow,
                EventType = "BuzzQueueCleared",
                TeamName = string.Empty,
                SlideNumber = _currentSlideIndex + 1,
                Reason = allowRebuzzAll ? "Host cleared buzz queue (All teams may re-buzz)" : "Host cleared buzz queue"
            });
            _db.SaveChanges();

            RefreshTeams();
            UpdateFloorBanner();
            PushSlideStateToAudience();
            ConsoleStatusText.Text = "Buzz queue cleared.";
        }

        public void ToggleBuzzerArming()
        {
            if (_buzzerServer == null) return;

            _buzzerServer.IsBuzzerArmed = !_buzzerServer.IsBuzzerArmed;
            UpdateBuzzerArmButtonDisplay();

            ConsoleStatusText.Text = _buzzerServer.IsBuzzerArmed
                ? "Buzzer Armed: Contestants can buzz in now."
                : "Buzzer Locked: Buzzer inputs locked by host.";
        }

        public void UpdateBuzzerArmButtonDisplay()
        {
            if (ArmBuzzerButton == null) return;
            var isArmed = _buzzerServer != null && _buzzerServer.IsBuzzerArmed;
            var conv = new BrushConverter();
            ArmBuzzerButton.Content = isArmed ? "🔔 BUZZERS ARMED (A)" : "🔒 BUZZERS LOCKED (A)";
            ArmBuzzerButton.Background = (Brush)conv.ConvertFromString(isArmed ? "#FF059669" : "#FF64748B");
            ArmBuzzerButton.BorderBrush = (Brush)conv.ConvertFromString(isArmed ? "#FF047857" : "#FF475569");
            ArmBuzzerButton.ToolTip = isArmed
                ? "Buzzers are live and accepting inputs (Press A to lock)"
                : "Buzzers are locked out (Press A to arm when reading is complete)";
        }

        private void RecordDetailedBuzz(string teamName, long swTicks, long utcTicks)
        {
            if (string.IsNullOrWhiteSpace(teamName)) return;

            if (_firstBuzzSwTicks == 0 || _buzzQueue.Count == 0)
            {
                _firstBuzzSwTicks = swTicks;
                _buzzQueueDeltas[teamName] = 0.0;
            }
            else
            {
                double deltaMs = Math.Max(0.0, (swTicks - _firstBuzzSwTicks) * 1000.0 / Stopwatch.Frequency);
                _buzzQueueDeltas[teamName] = deltaMs;
            }

            RegisterBuzz(teamName);
        }

        private void DismissBuzzButton_Click(object sender, RoutedEventArgs e)
        {
            DismissCurrentBuzz(allowRebuzz: false);
        }

        private void RebuzzDismissButton_Click(object sender, RoutedEventArgs e)
        {
            DismissCurrentBuzz(allowRebuzz: true);
        }

        private void ArmBuzzerButton_Click(object sender, RoutedEventArgs e)
        {
            ToggleBuzzerArming();
        }

        private void ClearQueueButton_Click(object sender, RoutedEventArgs e)
        {
            ClearBuzzQueue(allowRebuzzAll: true);
        }

        private void EnsureBuzzerServerStarted()
        {
            try
            {
                if (_buzzerServer == null)
                {
                    _buzzerServer = new QuizBuzzerServer();
                    _buzzerServer.GetTeamsCallback = () => _teams;
                    _buzzerServer.OnBuzzReceived += (team) =>
                    {
                        Dispatcher.Invoke(() => RegisterBuzz(team));
                    };
                    _buzzerServer.OnBuzzReceivedDetailed += (team, swTicks, utcTicks) =>
                    {
                        Dispatcher.Invoke(() => RecordDetailedBuzz(team, swTicks, utcTicks));
                    };
                    _buzzerServer.Start();
                    WirePounceBuzzerEvents();
                    UpdateBuzzerArmButtonDisplay();
                    UpdatePounceToggleButtonDisplay();
                }
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Could not auto-start QuizBuzzerServer.", ex);
            }
        }

        private void SoundToggleButton_Click(object sender, RoutedEventArgs e)
        {
            QuizChimes.IsMuted = !QuizChimes.IsMuted;
            UpdateSoundButtonState();
        }

        private void UpdateSoundButtonState()
        {
            var conv = new BrushConverter();
            SoundToggleButton.Content = QuizChimes.IsMuted ? "🔇 MUTE" : "🔊 SOUND ON";
            SoundToggleButton.Background = (Brush)conv.ConvertFromString(QuizChimes.IsMuted ? "#FF64748B" : "#FF059669");
            SoundToggleButton.BorderBrush = (Brush)conv.ConvertFromString(QuizChimes.IsMuted ? "#FF475569" : "#FF047857");
        }

        private void MobileBuzzerButton_Click(object sender, RoutedEventArgs e)
        {
            EnsureBuzzerServerStarted();
            var dialog = new QuizBuzzerDialog(_buzzerServer) { Owner = this };
            dialog.ShowDialog();
        }
    }
}
