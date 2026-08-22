using System;

namespace Score
{
    class QuizEventLog
    {
        public int QuizEventLogId { get; set; }
        public int QuizSessionId { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public string EventType { get; set; }
        public string Team { get; set; }
        public int RoundNumber { get; set; }
        public int ScoreChange { get; set; }
        public int ScoreAfter { get; set; }
        public int ElapsedSeconds { get; set; }
        public string Details { get; set; }
    }
}
