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
        public string TeamResponseTicks { get; set; }
        public string TieBreakTeams { get; set; }
        public int QuestionNumber { get; set; }
        public int QuestionDurationSeconds { get; set; }
        public long QuestionElapsedTicks { get; set; }
        public string QuestionState { get; set; }
        public bool LockAtQuestionEnd { get; set; }
        public DateTime? StartedAtUtc { get; set; }
        public DateTime? PausedAtUtc { get; set; }
        public DateTime? CompletedAtUtc { get; set; }
        public DateTime? LastUpdatedAtUtc { get; set; }
        public bool IsActive { get; set; }
        public bool UsesSessionSettings { get; set; }
    }
}
