using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Score
{
    static class QuizAIGeneratorService
    {
        public static List<TopicPack> GetCuratedTopicPacks(int deckId = 0)
        {
            return QuizCuratedTopics.GetCuratedTopicPacks(deckId);
        }

        public static List<DeckSlide> GenerateTopicQuestions(string topicKey, int deckId, int startingOrder)
        {
            var packs = GetCuratedTopicPacks(deckId);
            var pack = packs.FirstOrDefault(p => string.Equals(p.Key, topicKey, StringComparison.OrdinalIgnoreCase)) ?? packs[0];

            var result = new List<DeckSlide>();
            var order = startingOrder;
            foreach (var template in pack.Questions)
            {
                result.Add(new DeckSlide
                {
                    QuizDeckId = deckId,
                    SlideOrder = order++,
                    SlideType = template.SlideType ?? "Question",
                    QuestionType = template.QuestionType ?? QuizScoringEngine.QuestionMultipleChoice,
                    RoundType = template.RoundType ?? QuizScoringEngine.RoundStandard,
                    RoundName = template.RoundName ?? pack.Name,
                    Title = template.Title,
                    ContentText = template.ContentText,
                    OptionsJson = template.OptionsJson,
                    CorrectAnswer = template.CorrectAnswer,
                    AlternateAnswers = template.AlternateAnswers,
                    Points = template.Points,
                    PenaltyPoints = template.PenaltyPoints,
                    BonusPoints = template.BonusPoints,
                    TimeLimitSeconds = template.TimeLimitSeconds,
                    AllowPass = template.AllowPass,
                    HintsJson = template.HintsJson,
                    Explanation = template.Explanation,
                    IsActive = true
                });
            }
            return result;
        }

        public static List<DeckSlide> ParseAiQuestions(string rawText, int deckId, int startingOrder)
        {
            var results = new List<DeckSlide>();
            if (string.IsNullOrWhiteSpace(rawText)) return results;

            var blocks = SplitIntoQuestionBlocks(rawText);
            var order = startingOrder;

            foreach (var block in blocks)
            {
                var slide = ParseSingleQuestionBlock(block, deckId, order);
                if (slide != null)
                {
                    results.Add(slide);
                    order++;
                }
            }

            return results;
        }

        public static string StripMarkdownDecorations(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var s = text.Trim();
            s = Regex.Replace(s, @"\*\*([^\*]+)\*\*", "$1");
            s = Regex.Replace(s, @"__([^__]+)__", "$1");
            s = Regex.Replace(s, @"\*([^\*]+)\*", "$1");
            s = Regex.Replace(s, @"_([^_]+)_", "$1");
            s = s.Replace("**", "").Replace("__", "");
            return s.Trim();
        }

        public static string CleanForMcqMatch(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var s = StripMarkdownDecorations(text);
            s = s.Trim().TrimEnd('.', ';', ',', ':');
            return s.Trim();
        }

        private static List<string> SplitIntoQuestionBlocks(string text)
        {
            var blocks = new List<string>();
            var rawLines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            var currentBlock = new List<string>();

            for (var i = 0; i < rawLines.Length; i++)
            {
                var line = rawLines[i].Trim();
                var cleanLine = StripMarkdownDecorations(line);

                // Detect question start patterns: "1.", "Q1:", "Question 1:", "1)", "• Question", "**1. ", "### Question 1"
                var isQuestionHeader = Regex.IsMatch(cleanLine, @"^(?:Q(?:uestion)?\s*#?\s*\d+[\.:\)]?|\d+[\.:\)]|\•|\-)\s+\S+", RegexOptions.IgnoreCase) ||
                                       Regex.IsMatch(line, @"^(?:#+\s*|(?:\*\*|__)?)(?:Q(?:uestion)?\s*#?\s*\d+[\.:\)]?|\d+[\.:\)])\s*", RegexOptions.IgnoreCase);

                if (isQuestionHeader && currentBlock.Count > 0)
                {
                    var joined = string.Join("\n", currentBlock).Trim();
                    if (!string.IsNullOrWhiteSpace(joined))
                    {
                        blocks.Add(joined);
                        currentBlock.Clear();
                    }
                }

                currentBlock.Add(rawLines[i]);
            }

            if (currentBlock.Count > 0)
            {
                var joined = string.Join("\n", currentBlock).Trim();
                if (!string.IsNullOrWhiteSpace(joined)) blocks.Add(joined);
            }

            // Fallback: If no numbered question headers were found, split by double empty lines
            if (blocks.Count <= 1 && Regex.IsMatch(text, @"\n\s*\n"))
            {
                var paragraphBlocks = Regex.Split(text, @"(?:\r?\n\s*){2,}")
                    .Select(p => p.Trim())
                    .Where(p => p.Length > 0)
                    .ToList();
                if (paragraphBlocks.Count > 1) return paragraphBlocks;
            }

            return blocks;
        }

        private static DeckSlide ParseSingleQuestionBlock(string block, int deckId, int order)
        {
            var lines = block.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => l.Length > 0)
                .ToList();

            if (lines.Count == 0) return null;

            var promptLines = new List<string>();
            var options = new List<string>();
            string correctAnswer = null;
            string alternateAnswers = null;
            string explanation = null;
            var hints = new List<string>();
            var points = 10;
            var penalty = 0;
            var bonus = 5;
            var timeLimit = 30;
            string roundName = "General";

            var optionPattern = new Regex(@"^(?:[\*\-\•]\s*)?(?:(?:\*\*|__)?[\(\[]?([A-Za-z1-9])[\)\]]?[\.\)\:\-]?(?:\*\*|__)?)\s*(.+)$");
            var answerPattern = new Regex(@"^(?:[\*\-\•]\s*)?(?:\*\*|__)?(?:Correct\s+Answer|Answer\s+Key|Answer|Ans|Key)\s*[\:\-]?(?:\*\*|__)?\s*[\:\-]?\s*(.+)$", RegexOptions.IgnoreCase);
            var hintsPattern = new Regex(@"^(?:[\*\-\•]\s*)?(?:\*\*|__)?(?:Hints?|Clues?)\s*[\:\-]?(?:\*\*|__)?\s*[\:\-]?\s*(.+)$", RegexOptions.IgnoreCase);
            var explanationPattern = new Regex(@"^(?:[\*\-\•]\s*)?(?:\*\*|__)?(?:Explanation|Reason|Host\s+Notes?|Trivia|Note)\s*[\:\-]?(?:\*\*|__)?\s*[\:\-]?\s*(.+)$", RegexOptions.IgnoreCase);
            var aliasesPattern = new Regex(@"^(?:[\*\-\•]\s*)?(?:\*\*|__)?(?:Aliases?|Alternate\s+Answers?|Synonyms?)\s*[\:\-]?(?:\*\*|__)?\s*[\:\-]?\s*(.+)$", RegexOptions.IgnoreCase);
            var pointsPattern = new Regex(@"^(?:[\*\-\•]\s*)?(?:\*\*|__)?(?:Points?|Pts)\s*[\:\-]?(?:\*\*|__)?\s*[\:\-]?\s*(\d+)$", RegexOptions.IgnoreCase);
            var roundPattern = new Regex(@"^(?:[\*\-\•]\s*)?(?:\*\*|__)?(?:Round|Category|Topic)\s*[\:\-]?(?:\*\*|__)?\s*[\:\-]?\s*(.+)$", RegexOptions.IgnoreCase);

            foreach (var line in lines)
            {
                var ansMatch = answerPattern.Match(line);
                if (ansMatch.Success)
                {
                    correctAnswer = CleanForMcqMatch(ansMatch.Groups[1].Value);
                    continue;
                }

                var expMatch = explanationPattern.Match(line);
                if (expMatch.Success)
                {
                    explanation = StripMarkdownDecorations(expMatch.Groups[1].Value.Trim());
                    continue;
                }

                var hintMatch = hintsPattern.Match(line);
                if (hintMatch.Success)
                {
                    hints.Add(StripMarkdownDecorations(hintMatch.Groups[1].Value.Trim()));
                    continue;
                }

                var aliasMatch = aliasesPattern.Match(line);
                if (aliasMatch.Success)
                {
                    alternateAnswers = StripMarkdownDecorations(aliasMatch.Groups[1].Value.Trim());
                    continue;
                }

                var ptsMatch = pointsPattern.Match(line);
                if (ptsMatch.Success && int.TryParse(ptsMatch.Groups[1].Value, out var parsedPts))
                {
                    points = parsedPts;
                    continue;
                }

                var roundMatch = roundPattern.Match(line);
                if (roundMatch.Success)
                {
                    roundName = StripMarkdownDecorations(roundMatch.Groups[1].Value.Trim());
                    continue;
                }

                var optMatch = optionPattern.Match(line);
                if (optMatch.Success)
                {
                    var optText = CleanForMcqMatch(optMatch.Groups[2].Value);
                    options.Add(optText);
                    continue;
                }

                // If not metadata or option, it belongs to question prompt
                promptLines.Add(line);
            }

            if (promptLines.Count == 0 && options.Count == 0) return null;

            // Clean leading question numbering: "1. What is..." -> "What is..."
            if (promptLines.Count > 0)
            {
                var first = StripMarkdownDecorations(promptLines[0]);
                promptLines[0] = Regex.Replace(first, @"^(?:Q(?:uestion)?\s*#?\s*\d+[\.:\)]?|\d+[\.:\)]|\•|\-)\s*", "", RegexOptions.IgnoreCase).Trim();
            }

            var fullPrompt = StripMarkdownDecorations(string.Join(" ", promptLines).Trim());
            if (string.IsNullOrWhiteSpace(fullPrompt))
            {
                fullPrompt = "Question " + order;
            }

            // Derive concise Title
            string title;
            if (fullPrompt.Length <= 45)
            {
                title = fullPrompt;
            }
            else
            {
                var firstSentence = fullPrompt.Split(new[] { '?', '.', '!' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                title = firstSentence.Length <= 45 ? firstSentence : firstSentence.Substring(0, 42) + "...";
            }

            // Detect Question Type
            var questionType = QuizScoringEngine.QuestionMultipleChoice;
            var isTrueFalse = false;

            if (options.Count == 2 &&
                options.Any(o => string.Equals(o, "True", StringComparison.OrdinalIgnoreCase)) &&
                options.Any(o => string.Equals(o, "False", StringComparison.OrdinalIgnoreCase)))
            {
                isTrueFalse = true;
            }
            else if (!string.IsNullOrWhiteSpace(correctAnswer) && IsBooleanAnswer(correctAnswer, out var normBool))
            {
                isTrueFalse = true;
                correctAnswer = normBool;
            }
            else if (fullPrompt.IndexOf("True or False", StringComparison.OrdinalIgnoreCase) >= 0 ||
                     fullPrompt.IndexOf("True / False", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                isTrueFalse = true;
            }

            if (isTrueFalse)
            {
                questionType = QuizScoringEngine.QuestionTrueFalse;
                options.Clear();
                options.Add("True");
                options.Add("False");
                if (string.IsNullOrWhiteSpace(correctAnswer) || !IsBooleanAnswer(correctAnswer, out var norm))
                {
                    correctAnswer = "True";
                }
                else
                {
                    correctAnswer = norm;
                }
            }
            else if (options.Count >= 2)
            {
                questionType = QuizScoringEngine.QuestionMultipleChoice;

                // Resolve letter answers: e.g. "Answer: B" or "Answer: B) Paris"
                if (!string.IsNullOrWhiteSpace(correctAnswer))
                {
                    if (MatchesMcqOption(correctAnswer, options, out var matchedText))
                    {
                        correctAnswer = matchedText;
                    }
                    else
                    {
                        var letterIdx = ParseOptionLetterIndex(correctAnswer);
                        if (letterIdx >= 0 && letterIdx < options.Count)
                        {
                            correctAnswer = options[letterIdx];
                        }
                    }
                }
                else
                {
                    // Default to Option A if no answer specified
                    correctAnswer = options[0];
                }
            }
            else
            {
                questionType = QuizScoringEngine.QuestionShortAnswer;
                if (string.IsNullOrWhiteSpace(correctAnswer))
                {
                    correctAnswer = "Answer Key";
                }
            }

            return new DeckSlide
            {
                QuizDeckId = deckId,
                SlideOrder = order,
                SlideType = "Question",
                QuestionType = questionType,
                RoundType = QuizScoringEngine.RoundStandard,
                RoundName = roundName,
                Title = title,
                ContentText = fullPrompt,
                OptionsJson = options.Count > 0 ? string.Join("\n", options) : string.Empty,
                CorrectAnswer = correctAnswer,
                AlternateAnswers = alternateAnswers ?? string.Empty,
                Points = points,
                PenaltyPoints = penalty,
                BonusPoints = bonus,
                TimeLimitSeconds = timeLimit,
                AllowPass = true,
                HintsJson = hints.Count > 0 ? string.Join("\n", hints) : string.Empty,
                Explanation = explanation ?? string.Empty,
                IsActive = true
            };
        }

        public static bool MatchesMcqOption(string answer, List<string> choices, out string matchedOptionText)
        {
            matchedOptionText = null;
            if (string.IsNullOrWhiteSpace(answer) || choices == null || choices.Count == 0) return false;

            var cleanAns = CleanForMcqMatch(answer);
            if (string.IsNullOrWhiteSpace(cleanAns)) return false;

            // 1. Direct case-insensitive match (with markdown / trailing punctuation cleaned)
            foreach (var choice in choices)
            {
                var cleanChoice = CleanForMcqMatch(choice);
                if (string.Equals(cleanChoice, cleanAns, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(choice.Trim(), answer.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    matchedOptionText = choice.Trim();
                    return true;
                }
            }

            // 2. Strip letter prefix: e.g. "A. Paris" vs "Paris", "(B) Tokyo" vs "Tokyo", "E) None" vs "None"
            var strippedAns = CleanForMcqMatch(StripLetterPrefix(cleanAns));
            foreach (var choice in choices)
            {
                var cleanChoice = CleanForMcqMatch(choice);
                var strippedChoice = CleanForMcqMatch(StripLetterPrefix(cleanChoice));
                if (!string.IsNullOrWhiteSpace(strippedAns) &&
                    (string.Equals(strippedChoice, strippedAns, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(cleanChoice, strippedAns, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(strippedChoice, cleanAns, StringComparison.OrdinalIgnoreCase)))
                {
                    matchedOptionText = choice.Trim();
                    return true;
                }
            }

            // 3. Option letter index: e.g. answer is "A", "A.", "A)", "(A)", "[A]", "Option A", "1", "E"
            var letterIndex = ParseOptionLetterIndex(cleanAns);
            if (letterIndex >= 0 && letterIndex < choices.Count)
            {
                matchedOptionText = choices[letterIndex].Trim();
                return true;
            }

            return false;
        }

        public static string StripLetterPrefix(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return string.Empty;
            var trimmed = StripMarkdownDecorations(text).Trim();
            // Matches: A. , A) , A: , A- , (A) , [A] , Option A. , Option A: , Option A , Option (A) , 1. , 1) , (1)
            var cleaned = Regex.Replace(trimmed, @"^(?:Option\s+)?(?:[\(\[]?([A-Za-z1-9])[\)\]]?[\.\)\:\-]?\s*)", "", RegexOptions.IgnoreCase).Trim();
            return string.IsNullOrWhiteSpace(cleaned) ? trimmed : cleaned;
        }

        public static int ParseOptionLetterIndex(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return -1;
            var trimmed = StripMarkdownDecorations(text).Trim();
            // Match pure letter / digit keys: "A", "A.", "A)", "(A)", "[A]", "Option A", "Option A.", "Option (A)", "1", "1.", "(1)", "E", "(E)"
            var match = Regex.Match(trimmed, @"^[\(\[]?(?:Option\s+)?([A-Za-z1-9])[\)\]]?[\.\)\:\-]?$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var ch = match.Groups[1].Value.ToUpperInvariant()[0];
                if (ch >= 'A' && ch <= 'Z') return ch - 'A';
                if (ch >= '1' && ch <= '9') return ch - '1';
            }
            return -1;
        }

        public static bool IsBooleanAnswer(string text, out string normalized)
        {
            normalized = null;
            if (string.IsNullOrWhiteSpace(text)) return false;
            var t = CleanForMcqMatch(text).ToLowerInvariant();
            if (t == "true" || t == "t" || t == "yes" || t == "y" || t == "1" || t == "correct" || t == "right")
            {
                normalized = "True";
                return true;
            }
            if (t == "false" || t == "f" || t == "no" || t == "n" || t == "0" || t == "incorrect" || t == "wrong")
            {
                normalized = "False";
                return true;
            }
            return false;
        }
    }
}
