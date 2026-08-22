using System;

namespace Score
{
    class QuizSession
    {
        public int QuizSessionId { get; set; }
        public string Title { get; set; }
        public int ConfiguredRounds { get; set; }
        public int CurrentRound { get; set; }
        public string Status { get; set; }
        public int GlobalDurationSeconds { get; set; }
        public int RoundDurationSeconds { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? PausedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public bool IsActive { get; set; }
    }
}
