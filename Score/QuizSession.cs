using System;

namespace Score
{
    class QuizSession
    {
        public int QuizSessionId { get; set; }
        public string Title { get; set; }
        public int ConfiguredTeamCount { get; set; }
        public int ConfiguredRounds { get; set; }
        public int CurrentRound { get; set; }
        public string Status { get; set; }
        public int GlobalDurationSeconds { get; set; }
        public int RoundDurationSeconds { get; set; }
        public int GlobalElapsedSeconds { get; set; }
        public int RoundElapsedSeconds { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? PausedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public DateTime? LastUpdatedAtUtc { get; set; }
        public bool IsActive { get; set; }
    }
}
