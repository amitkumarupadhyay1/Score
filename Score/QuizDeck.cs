using System;

namespace Score
{
    class QuizDeck
    {
        public int QuizDeckId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Theme { get; set; }
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
        public bool IsActive { get; set; }
    }
}