using System;

namespace Score
{
    class QuizAuditEntry
    {
        public int QuizAuditEntryId { get; set; }
        public int QuizDeckSessionId { get; set; }
        public DateTime OccurredAtUtc { get; set; }
        public string EventType { get; set; }
        public string TeamName { get; set; }
        public int SlideNumber { get; set; }
        public int OldPoints { get; set; }
        public int NewPoints { get; set; }
        public string Reason { get; set; }
    }
}