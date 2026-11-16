using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Windows;

namespace Score
{
    class DeckSlide
    {
        public int DeckSlideId { get; set; }
        public int QuizDeckId { get; set; }
        public string SlideType { get; set; }
        public string QuestionType { get; set; }
        public string RoundType { get; set; }
        public int SlideOrder { get; set; }
        public string Title { get; set; }
        public string ContentText { get; set; }
        public string MediaPath { get; set; }
        public string MediaType { get; set; }
        public string CorrectAnswer { get; set; }
        public string OptionsJson { get; set; }
        public int Points { get; set; }
        public int PenaltyPoints { get; set; }
        public int TimeLimitSeconds { get; set; }
        public bool IsActive { get; set; }

        // Extended Quiz Creation & Conducting Properties
        public string HintsJson { get; set; }
        public string AlternateAnswers { get; set; }
        public string Explanation { get; set; }
        public int BonusPoints { get; set; }
        public bool AllowPass { get; set; }
        public string RoundName { get; set; }
        public int MediaDisplayMode { get; set; }

        // Visual / UI Presentation Badges
        [NotMapped]
        public string SlideTypeBadgeText
        {
            get
            {
                var type = SlideType ?? "Question";
                if (string.Equals(type, "Question", StringComparison.OrdinalIgnoreCase))
                {
                    var qType = QuestionType ?? QuizScoringEngine.QuestionMultipleChoice;
                    switch (qType)
                    {
                        case QuizScoringEngine.QuestionMultipleChoice: return "MCQ";
                        case QuizScoringEngine.QuestionMultipleSelect: return "MULTI-SELECT";
                        case QuizScoringEngine.QuestionTrueFalse: return "TRUE/FALSE";
                        case QuizScoringEngine.QuestionShortAnswer: return "SHORT ANSWER";
                        case QuizScoringEngine.QuestionNumeric: return "NUMERIC";
                        case QuizScoringEngine.QuestionSequence: return "SEQUENCE";
                        case QuizScoringEngine.QuestionManual: return "MANUAL";
                        default: return qType.ToUpperInvariant();
                    }
                }
                return type.ToUpperInvariant();
            }
        }

        [NotMapped]
        public string SlideTypeBadgeBg
        {
            get
            {
                var type = (SlideType ?? "Question").ToLowerInvariant();
                switch (type)
                {
                    case "instruction": return "#F3E8FF";
                    case "media": return "#FEF3C7";
                    case "timer": return "#E0F2FE";
                    case "scoreboard": return "#F1F5F9";
                    default: return "#DCFCE7";
                }
            }
        }

        [NotMapped]
        public string SlideTypeBadgeFg
        {
            get
            {
                var type = (SlideType ?? "Question").ToLowerInvariant();
                switch (type)
                {
                    case "instruction": return "#7E22CE";
                    case "media": return "#B45309";
                    case "timer": return "#0369A1";
                    case "scoreboard": return "#334155";
                    default: return "#15803D";
                }
            }
        }

        [NotMapped]
        public string PointsBadgeText => string.Equals(SlideType ?? "Question", "Question", StringComparison.OrdinalIgnoreCase) ? $"{Points}p" : "—";

        [NotMapped]
        public Visibility PointsBadgeVisibility => string.Equals(SlideType ?? "Question", "Question", StringComparison.OrdinalIgnoreCase) ? Visibility.Visible : Visibility.Collapsed;
    }
}
