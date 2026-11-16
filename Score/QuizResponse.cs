using System;

namespace Score
{
    class QuizResponse
    {
        public int QuizResponseId { get; set; }
        public int QuizDeckSessionId { get; set; }
        public int DeckSlideId { get; set; }
        public string TeamName { get; set; }
        public string AnswerText { get; set; }
        public DateTime? AnsweredAtUtc { get; set; }
        public int ResponseTimeSeconds { get; set; }
        public bool IsCorrect { get; set; }
        public int PointsAwarded { get; set; }
        public int BuzzOrder { get; set; }
        public string SlideTitleSnapshot { get; set; }
        public string SlideTypeSnapshot { get; set; }
        public int SlideOrderSnapshot { get; set; }

        // Extended Quiz Conducting Tracking
        public bool IsPassAttempt { get; set; }
        public string AdjudicationNotes { get; set; }
    }
}
