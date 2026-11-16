using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Score
{
    public static class QuizScoringEngine
    {
        // Round Type Constants
        public const string RoundMixBag = "MixBag";
        public const string RoundBuzzer = "Buzzer";
        public const string RoundRapidFire = "RapidFire";
        public const string RoundVideo = "Video";
        public const string RoundImage = "Image";
        public const string RoundTimed = "Timed";
        public const string RoundStandard = "Standard";
        public const string RoundPassBounce = "PassBounce";

        // Question Type Constants
        public const string QuestionMultipleChoice = "MultipleChoice";
        public const string QuestionMultipleSelect = "MultipleSelect";
        public const string QuestionTrueFalse = "TrueFalse";
        public const string QuestionShortAnswer = "ShortAnswer";
        public const string QuestionNumeric = "Numeric";
        public const string QuestionNumericClosest = "NumericClosest";
        public const string QuestionSequence = "Sequence";
        public const string QuestionManual = "Manual";

        public static int CalculatePoints(string roundType, int questionPoints, int responseTimeSeconds,
            int timeLimitSeconds, bool isCorrect, bool isFirstCorrectBuzz, int buzzOrder, int penalty)
        {
            return CalculatePoints(roundType, questionPoints, responseTimeSeconds, timeLimitSeconds,
                isCorrect, isFirstCorrectBuzz, buzzOrder, penalty, false, 0);
        }

        public static int CalculatePoints(string roundType, int questionPoints, int responseTimeSeconds,
            int timeLimitSeconds, bool isCorrect, bool isFirstCorrectBuzz, int buzzOrder, int penalty,
            bool isPassAttempt, int bonusPoints)
        {
            if (!isCorrect)
            {
                // In a pass attempt, traditional quizzes usually do NOT penalize passed questions unless pounce
                if (isPassAttempt && roundType == RoundPassBounce)
                    return 0;

                return -Math.Abs(penalty);
            }

            // If answered on a pass/bounce, award designated bonus points or half points
            if (isPassAttempt)
            {
                return bonusPoints > 0 ? bonusPoints : Math.Max(1, questionPoints / 2);
            }

            var basePoints = questionPoints;

            switch (roundType)
            {
                case RoundBuzzer:
                    // First correct buzzer gets full points; subsequent queue buzzers get half
                    return isFirstCorrectBuzz ? basePoints : Math.Max(1, basePoints / 2);

                case RoundRapidFire:
                    // Fixed points per correct answer in rapid fire
                    return basePoints;

                case RoundTimed:
                    // Speed-based scoring: faster answers score more points
                    if (timeLimitSeconds <= 0) return basePoints;
                    var timeRatio = (double)responseTimeSeconds / timeLimitSeconds;
                    var speedFactor = Math.Max(0.5, 1.0 - timeRatio); // 0.5 to 1.0
                    return (int)Math.Round(basePoints * speedFactor);

                case RoundPassBounce:
                case RoundVideo:
                case RoundImage:
                case RoundMixBag:
                default:
                    return basePoints;
            }
        }

        public static bool IsCorrectAnswer(string questionType, string correctAnswer, string teamAnswer,
            out bool exactMatch)
        {
            return IsCorrectAnswer(questionType, correctAnswer, teamAnswer, null, out exactMatch);
        }

        public static bool IsCorrectAnswer(string questionType, string correctAnswer, string teamAnswer,
            string alternateAnswers, out bool exactMatch)
        {
            exactMatch = false;
            if (string.IsNullOrWhiteSpace(teamAnswer)) return false;

            var correct = correctAnswer ?? string.Empty;
            var answer = teamAnswer.Trim();

            switch (questionType)
            {
                case QuestionTrueFalse:
                    exactMatch = string.Equals(answer, correct.Trim(), StringComparison.OrdinalIgnoreCase);
                    return exactMatch;

                case QuestionShortAnswer:
                    var allAccepted = new List<string>();
                    allAccepted.AddRange(SplitAcceptedVariants(correct));
                    if (!string.IsNullOrWhiteSpace(alternateAnswers))
                        allAccepted.AddRange(SplitAcceptedVariants(alternateAnswers));

                    var normalizedTeamAnswer = NormalizeForComparison(answer);
                    foreach (var variant in allAccepted)
                    {
                        if (string.Equals(normalizedTeamAnswer, NormalizeForComparison(variant), StringComparison.OrdinalIgnoreCase))
                        {
                            exactMatch = true;
                            return true;
                        }
                    }
                    return false;

                case QuestionNumeric:
                    if (decimal.TryParse(answer, out var numAnswer) &&
                        decimal.TryParse(correct.Trim(), out var numCorrect))
                    {
                        exactMatch = numAnswer == numCorrect;
                        return exactMatch;
                    }
                    return false;

                case QuestionMultipleSelect:
                    var correctSet = new HashSet<string>(SplitAcceptedVariants(correct), StringComparer.OrdinalIgnoreCase);
                    var answerSet = new HashSet<string>(SplitAcceptedVariants(answer), StringComparer.OrdinalIgnoreCase);
                    exactMatch = correctSet.SetEquals(answerSet);
                    return exactMatch;

                case QuestionSequence:
                    var correctSeq = NormalizeSequence(correct);
                    var answerSeq = NormalizeSequence(answer);
                    exactMatch = string.Equals(correctSeq, answerSeq, StringComparison.OrdinalIgnoreCase);
                    return exactMatch;

                case QuestionMultipleChoice:
                    exactMatch = string.Equals(answer, correct.Trim(), StringComparison.OrdinalIgnoreCase);
                    return exactMatch;

                case QuestionManual:
                    return false;

                default:
                    exactMatch = string.Equals(answer, correct.Trim(), StringComparison.OrdinalIgnoreCase);
                    return exactMatch;
            }
        }

        public static List<string> ParseOptions(string options)
        {
            if (string.IsNullOrWhiteSpace(options)) return new List<string>();
            return options.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(o => o.Trim())
                .Where(o => o.Length > 0)
                .ToList();
        }

        public static IEnumerable<string> SplitAcceptedVariants(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return Enumerable.Empty<string>();
            return input.Split(new[] { '\r', '\n', '|', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => v.Trim())
                .Where(v => v.Length > 0);
        }

        private static string NormalizeForComparison(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            // Remove common trailing/leading punctuation and excess spacing
            var cleaned = Regex.Replace(input.Trim(), @"[.,\-_'\s]+", " ");
            return cleaned.Trim();
        }

        private static string NormalizeSequence(string input)
        {
            if (string.IsNullOrWhiteSpace(input)) return string.Empty;
            // Remove commas, dashes, colons, spaces: "A, B, C, D" -> "ABCD"
            return Regex.Replace(input.ToUpperInvariant(), @"[\s,>\-_;:]+", "");
        }
    }
}
