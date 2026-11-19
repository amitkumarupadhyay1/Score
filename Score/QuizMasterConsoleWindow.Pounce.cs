using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Score
{
    public class PounceSubmissionItem
    {
        public PounceSubmission Submission { get; }
        public string CorrectButtonLabel { get; }
        public string IncorrectButtonLabel { get; }

        public PounceSubmissionItem(PounceSubmission submission, int bonus = 20, int penalty = 10)
        {
            Submission = submission ?? throw new ArgumentNullException(nameof(submission));
            CorrectButtonLabel = $"ACCEPT (+{bonus})";
            IncorrectButtonLabel = $"REJECT (-{penalty})";
        }

        public string TeamName => Submission.TeamName;
        public string Answer => Submission.Answer;
        public bool CanEvaluate => !Submission.Evaluated;

        public string StatusLabel
        {
            get
            {
                if (!Submission.Evaluated)
                {
                    return "Awaiting Adjudication";
                }
                return Submission.IsCorrect
                    ? $"Scored +{Submission.PointsAwarded} pts"
                    : $"Penalized {Submission.PointsAwarded} pts";
            }
        }

        public Brush StatusBrush
        {
            get
            {
                var conv = new BrushConverter();
                if (!Submission.Evaluated)
                {
                    return (Brush)conv.ConvertFromString("#FFD97706"); // Amber
                }
                return Submission.IsCorrect
                    ? (Brush)conv.ConvertFromString("#FF16A34A") // Green
                    : (Brush)conv.ConvertFromString("#FFDC2626"); // Red
            }
        }
    }

    public partial class QuizMasterConsoleWindow : Window
    {
        private bool _pounceEventsWired = false;

        public void WirePounceBuzzerEvents()
        {
            if (_pounceEventsWired || _buzzerServer == null) return;
            _buzzerServer.PounceReceived += sub =>
            {
                Dispatcher.Invoke(() => OnPounceSubmissionReceived(sub));
            };
            _buzzerServer.OnPouncesReset += () =>
            {
                Dispatcher.Invoke(() => RefreshPounceQueueUI());
            };
            _pounceEventsWired = true;
        }

        public void TogglePounceMode()
        {
            EnsureBuzzerServerStarted();
            WirePounceBuzzerEvents();

            if (_buzzerServer == null)
            {
                ConsoleStatusText.Text = "Buzzer server is not running.";
                return;
            }

            _buzzerServer.PounceEnabled = !_buzzerServer.PounceEnabled;
            UpdatePounceToggleButtonDisplay();
            RefreshPounceQueueUI();

            ConsoleStatusText.Text = _buzzerServer.PounceEnabled
                ? "Chit Pounce ACTIVATED: Contestants can submit secret answers via mobile."
                : "Chit Pounce DEACTIVATED: Mobile pounce submissions locked.";
        }

        public void UpdatePounceToggleButtonDisplay()
        {
            if (PounceToggleButton == null) return;
            bool isEnabled = _buzzerServer != null && _buzzerServer.PounceEnabled;
            var conv = new BrushConverter();

            PounceToggleButton.Content = isEnabled ? "POUNCE: LIVE (P)" : "POUNCE: OFF (P)";
            PounceToggleButton.Background = (Brush)conv.ConvertFromString(isEnabled ? "#FFB45309" : "#FF334155");
            PounceToggleButton.BorderBrush = (Brush)conv.ConvertFromString(isEnabled ? "#FF92400E" : "#FF475569");
            PounceToggleButton.ToolTip = isEnabled
                ? "Chit Pounce is LIVE on mobile screens. Contestants can submit secret answers (Shortcut: P)"
                : "Chit Pounce is OFF. Click or press P to allow secret answers on mobile screens.";
        }

        private void PounceToggleButton_Click(object sender, RoutedEventArgs e)
        {
            TogglePounceMode();
        }

        public void OnPounceSubmissionReceived(PounceSubmission sub)
        {
            if (sub == null) return;

            QuizChimes.PlayBounce();
            RefreshPounceQueueUI();

            ConsoleStatusText.Text = $"Pounce received from {sub.TeamName}: \"{sub.Answer}\"";
        }

        private void PounceCorrectButton_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as FrameworkElement)?.Tag as PounceSubmissionItem;
            if (item == null || item.Submission == null || item.Submission.Evaluated) return;

            var sub = item.Submission;
            int bonus = _currentSlide?.BonusPoints > 0 ? _currentSlide.BonusPoints : 20;

            var teamName = sub.TeamName;
            var currentScore = _teamScores.TryGetValue(teamName, out var s) ? s : 0;
            var newScore = currentScore + bonus;
            _teamScores[teamName] = newScore;

            sub.Evaluated = true;
            sub.IsCorrect = true;
            sub.PointsAwarded = bonus;

            QuizChimes.PlayCorrect();

            if (_currentSession != null)
            {
                var audit = new QuizAuditEntry
                {
                    QuizDeckSessionId = _currentSession.QuizDeckSessionId,
                    OccurredAtUtc = DateTime.UtcNow,
                    EventType = "PounceScored",
                    TeamName = teamName,
                    SlideNumber = _currentSlideIndex + 1,
                    OldPoints = currentScore,
                    NewPoints = newScore,
                    Reason = $"Chit pounce answer accepted ('{sub.Answer}') [+{bonus} pts]"
                };
                _db.QuizAuditEntries.Add(audit);

                var responseRecord = new QuizResponse
                {
                    QuizDeckSessionId = _currentSession.QuizDeckSessionId,
                    DeckSlideId = _currentSlide?.DeckSlideId ?? 0,
                    TeamName = teamName,
                    AnswerText = "[POUNCE] " + sub.Answer,
                    AnsweredAtUtc = DateTime.UtcNow,
                    ResponseTimeSeconds = (int)_questionTimer.Elapsed.TotalSeconds,
                    IsCorrect = true,
                    PointsAwarded = bonus,
                    BuzzOrder = 0,
                    SlideTitleSnapshot = _currentSlide?.Title,
                    SlideTypeSnapshot = _currentSlide?.SlideType,
                    SlideOrderSnapshot = _currentSlideIndex + 1,
                    IsPassAttempt = false
                };
                _db.QuizResponses.Add(responseRecord);
                _db.SaveChanges();
            }

            RefreshTeams();
            PushSlideStateToAudience();
            RefreshPounceQueueUI();

            ConsoleStatusText.Text = $"Pounce CORRECT: {teamName} awarded +{bonus} pts.";
        }

        private void PounceIncorrectButton_Click(object sender, RoutedEventArgs e)
        {
            var item = (sender as FrameworkElement)?.Tag as PounceSubmissionItem;
            if (item == null || item.Submission == null || item.Submission.Evaluated) return;

            var sub = item.Submission;
            int penaltyMagnitude = _currentSlide?.PenaltyPoints > 0 ? _currentSlide.PenaltyPoints : 10;
            int penalty = -Math.Abs(penaltyMagnitude);

            var teamName = sub.TeamName;
            var currentScore = _teamScores.TryGetValue(teamName, out var s) ? s : 0;
            var newScore = currentScore + penalty;
            _teamScores[teamName] = newScore;

            sub.Evaluated = true;
            sub.IsCorrect = false;
            sub.PointsAwarded = penalty;

            QuizChimes.PlayBuzzer();

            if (_currentSession != null)
            {
                var audit = new QuizAuditEntry
                {
                    QuizDeckSessionId = _currentSession.QuizDeckSessionId,
                    OccurredAtUtc = DateTime.UtcNow,
                    EventType = "PounceScored",
                    TeamName = teamName,
                    SlideNumber = _currentSlideIndex + 1,
                    OldPoints = currentScore,
                    NewPoints = newScore,
                    Reason = $"Chit pounce answer rejected ('{sub.Answer}') [{penalty} pts]"
                };
                _db.QuizAuditEntries.Add(audit);

                var responseRecord = new QuizResponse
                {
                    QuizDeckSessionId = _currentSession.QuizDeckSessionId,
                    DeckSlideId = _currentSlide?.DeckSlideId ?? 0,
                    TeamName = teamName,
                    AnswerText = "[POUNCE] " + sub.Answer,
                    AnsweredAtUtc = DateTime.UtcNow,
                    ResponseTimeSeconds = (int)_questionTimer.Elapsed.TotalSeconds,
                    IsCorrect = false,
                    PointsAwarded = penalty,
                    BuzzOrder = 0,
                    SlideTitleSnapshot = _currentSlide?.Title,
                    SlideTypeSnapshot = _currentSlide?.SlideType,
                    SlideOrderSnapshot = _currentSlideIndex + 1,
                    IsPassAttempt = false
                };
                _db.QuizResponses.Add(responseRecord);
                _db.SaveChanges();
            }

            RefreshTeams();
            PushSlideStateToAudience();
            RefreshPounceQueueUI();

            ConsoleStatusText.Text = $"✗ Pounce INCORRECT: {teamName} penalized {penalty} pts.";
        }

        private void ClearPouncesButton_Click(object sender, RoutedEventArgs e)
        {
            if (_buzzerServer != null)
            {
                _buzzerServer.ResetPounces();
            }
            RefreshPounceQueueUI();
            ConsoleStatusText.Text = "Pounce queue cleared for next question.";
        }

        public void RefreshPounceQueueUI()
        {
            if (PounceQueuePanel == null) return;

            var subs = _buzzerServer?.PounceSubmissions ?? new List<PounceSubmission>();
            bool isPounceActive = _buzzerServer != null && (_buzzerServer.PounceEnabled || subs.Count > 0);

            PounceQueuePanel.Visibility = isPounceActive ? Visibility.Visible : Visibility.Collapsed;

            if (PounceCountText != null)
            {
                PounceCountText.Text = subs.Count.ToString();
            }

            int bonus = _currentSlide?.BonusPoints > 0 ? _currentSlide.BonusPoints : 20;
            int penalty = _currentSlide?.PenaltyPoints > 0 ? _currentSlide.PenaltyPoints : 10;

            var items = subs.Select(s => new PounceSubmissionItem(s, bonus, penalty)).ToList();
            if (PounceItemsControl != null)
            {
                PounceItemsControl.ItemsSource = items;
            }
        }
    }
}
