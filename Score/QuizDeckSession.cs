using System;

namespace Score
{
    class QuizDeckSession
    {
        public int QuizDeckSessionId { get; set; }
        public int QuizDeckId { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public string Status { get; set; } // Setup, Running, Paused, Completed
        public int CurrentSlideIndex { get; set; }
        public string QuizMasterNotes { get; set; }
        public DateTime? TimerStartedAtUtc { get; set; }
        public int TimerElapsedSeconds { get; set; }
        public string TimerState { get; set; }
        public bool AnswerRevealed { get; set; }

        // Extended Quiz Conducting State Properties
        public string ActiveTeamOnFloor { get; set; }
        public int PassCount { get; set; }
        public string BuzzQueueJson { get; set; }
        public int ClueRevealStep { get; set; }
    }
}
