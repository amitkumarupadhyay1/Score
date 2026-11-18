using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Score
{
    public partial class QuizMasterConsoleWindow : Window
    {
        private class ScoringHistorySnapshot
        {
            public int QuizResponseId { get; set; }
            public string TeamName { get; set; }
            public int PointsDelta { get; set; }
            public int SlideIndex { get; set; }
            public bool WasCorrect { get; set; }
            public List<string> BuzzQueueState { get; set; } = new List<string>();
            public HashSet<string> BuzzedTeamsState { get; set; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            public string ActiveTeamOnFloor { get; set; }
            public bool HasCorrectBuzzForSlide { get; set; }
            public bool BuzzerServerArmed { get; set; }
            public int PassCount { get; set; }
        }

        private readonly Stack<ScoringHistorySnapshot> _undoStack = new Stack<ScoringHistorySnapshot>();

        private void SnapshotCurrentScoringState(int responseId, string teamName, int pointsDelta, bool wasCorrect)
        {
            var snap = new ScoringHistorySnapshot
            {
                QuizResponseId = responseId,
                TeamName = teamName,
                PointsDelta = pointsDelta,
                SlideIndex = _currentSlideIndex,
                WasCorrect = wasCorrect,
                BuzzQueueState = new List<string>(_buzzQueue),
                BuzzedTeamsState = new HashSet<string>(_buzzedTeams, StringComparer.OrdinalIgnoreCase),
                ActiveTeamOnFloor = _currentBuzzingTeam ?? _currentSession?.ActiveTeamOnFloor,
                HasCorrectBuzzForSlide = _hasCorrectBuzzForSlide,
                BuzzerServerArmed = _buzzerServer?.IsBuzzerArmed ?? false,
                PassCount = _passCount
            };
            _undoStack.Push(snap);
            UpdateUndoButtonState();
        }

        public void UndoLastScore()
        {
            if (_undoStack.Count == 0)
            {
                ConsoleStatusText.Text = "No score actions available to undo.";
                return;
            }

            var snap = _undoStack.Pop();
            UpdateUndoButtonState();

            // 1. Revert team score in-memory
            if (_teamScores.ContainsKey(snap.TeamName))
            {
                _teamScores[snap.TeamName] -= snap.PointsDelta;
            }

            // 2. Remove the saved response from SQLite
            try
            {
                var response = _db.QuizResponses.Find(snap.QuizResponseId);
                if (response != null)
                {
                    _db.QuizResponses.Remove(response);
                }

                _db.QuizAuditEntries.Add(new QuizAuditEntry
                {
                    QuizDeckSessionId = _currentSession?.QuizDeckSessionId ?? 0,
                    OccurredAtUtc = DateTime.UtcNow,
                    EventType = "ScoreUndone",
                    TeamName = snap.TeamName,
                    SlideNumber = snap.SlideIndex + 1,
                    Reason = $"Host reverted last score ({snap.PointsDelta:+0;-0;0} pts)"
                });
                _db.SaveChanges();
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Could not remove response on score undo.", ex);
            }

            // 3. Fully restore slide interactive state if we are still on the same slide
            if (_currentSlideIndex == snap.SlideIndex)
            {
                _hasCorrectBuzzForSlide = snap.HasCorrectBuzzForSlide;
                _buzzQueue.Clear();
                _buzzQueue.AddRange(snap.BuzzQueueState);
                _buzzedTeams.Clear();
                foreach (var t in snap.BuzzedTeamsState) _buzzedTeams.Add(t);
                _currentBuzzingTeam = snap.ActiveTeamOnFloor;
                _passCount = snap.PassCount;

                if (_currentSession != null)
                {
                    _currentSession.ActiveTeamOnFloor = _currentBuzzingTeam;
                    _currentSession.PassCount = _passCount;
                    _db.SaveChanges();
                }

                if (_buzzerServer != null)
                {
                    _buzzerServer.IsBuzzerArmed = snap.BuzzerServerArmed;
                    UpdateBuzzerArmButtonDisplay();
                }
            }

            RefreshTeams();
            UpdateFloorBanner();
            PushSlideStateToAudience();
            ConsoleStatusText.Text = $"UNDO: Reverted {snap.TeamName} score by {-snap.PointsDelta:+0;-0;0} pts. State restored.";
        }

        private void UpdateUndoButtonState()
        {
            if (UndoScoreButton != null)
            {
                UndoScoreButton.IsEnabled = _undoStack.Count > 0;
                UndoScoreButton.ToolTip = _undoStack.Count > 0
                    ? $"Undo score for {_undoStack.Peek().TeamName} (Ctrl+Z)"
                    : "No actions to undo (Ctrl+Z)";
            }
        }

        private void UndoScoreButton_Click(object sender, RoutedEventArgs e)
        {
            UndoLastScore();
        }
    }
}
