using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Score
{
    internal static class QuizImportExportService
    {
        public static string GenerateSampleCsvTemplate()
        {
            var sb = new StringBuilder();
            sb.AppendLine("RoundName,SlideType,QuestionType,RoundType,Title,ContentText,Options,CorrectAnswer,AlternateAnswers,Points,PenaltyPoints,BonusPoints,TimeLimitSeconds,Hints,Explanation,MediaPath");
            
            // Sample 1: Multiple Choice
            sb.AppendLine("General Knowledge,Question,MultipleChoice,Standard,First Moon Landing,\"Who was the first person to walk on the moon?\",\"Neil Armstrong|Buzz Aldrin|Yuri Gagarin|Michael Collins\",Neil Armstrong,\"Armstrong|Neil A.\",10,0,5,30,\"Apollo 11 mission\",\"Neil Armstrong set foot on the moon on July 20, 1969.\",");
            
            // Sample 2: True/False
            sb.AppendLine("Science Round,Question,TrueFalse,Standard,Speed of Light,\"Light travels faster in water than in air.\",\"True|False\",False,\"False|F\",10,5,0,20,,Water has higher refractive index slowing light down.,");
            
            // Sample 3: Direct Short Answer
            sb.AppendLine("Indian History,Question,ShortAnswer,PassBounce,Father of Nation,\"Who is known as the Father of the Nation in India?\",,Mahatma Gandhi,\"Gandhi|Mohandas Gandhi|M. K. Gandhi|Bapu\",10,0,5,30,\"Appears on Indian currency banknotes\",\"Mohandas Karamchand Gandhi\",");
            
            // Sample 4: Buzzer Round
            sb.AppendLine("Pop Culture,Question,MultipleChoice,Buzzer,Oscars 2024,\"Which film won Best Picture at the 96th Academy Awards?\",\"Oppenheimer|Barbie|Killers of the Flower Moon|Poor Things\",Oppenheimer,Oppenheimer,20,10,0,15,\"Directed by Christopher Nolan\",\"Directed by Christopher Nolan and starring Cillian Murphy.\",");
            
            // Sample 5: Multi-Select
            sb.AppendLine("Geography,Question,MultipleSelect,Standard,Landlocked Countries,\"Which of the following countries are landlocked?\",\"Nepal|Sri Lanka|Switzerland|Japan\",Nepal|Switzerland,Nepal;Switzerland,15,5,5,45,\"Two countries from the list\",\"Nepal is in South Asia and Switzerland is in Europe.\",");
            
            // Sample 6: Sequence / Chronology
            sb.AppendLine("Tech History,Question,Sequence,Standard,Company Founding Order,\"Arrange these tech companies from oldest to newest: Apple, Google, Microsoft, Facebook\",,\"Microsoft, Apple, Google, Facebook\",\"Microsoft-Apple-Google-Facebook|Microsoft,Apple,Google,Facebook\",20,0,5,60,\"Founded in 1975, 1976, 1998, 2004\",\"MS (1975) -> Apple (1976) -> Google (1998) -> FB (2004)\",");

            return sb.ToString();
        }

        public static string ExportDeckToCsv(QuizDeck deck, IList<DeckSlide> slides)
        {
            var sb = new StringBuilder();
            sb.AppendLine("RoundName,SlideType,QuestionType,RoundType,Title,ContentText,Options,CorrectAnswer,AlternateAnswers,Points,PenaltyPoints,BonusPoints,TimeLimitSeconds,Hints,Explanation,MediaPath");

            foreach (var s in slides.OrderBy(s => s.SlideOrder))
            {
                var options = string.Join("|", QuizScoringEngine.ParseOptions(s.OptionsJson));
                sb.AppendLine(string.Join(",",
                    ExportSafety.ToCsvCell(s.RoundName ?? string.Empty),
                    ExportSafety.ToCsvCell(s.SlideType ?? "Question"),
                    ExportSafety.ToCsvCell(s.QuestionType ?? "MultipleChoice"),
                    ExportSafety.ToCsvCell(s.RoundType ?? "Standard"),
                    ExportSafety.ToCsvCell(s.Title ?? string.Empty),
                    ExportSafety.ToCsvCell(s.ContentText ?? string.Empty),
                    ExportSafety.ToCsvCell(options),
                    ExportSafety.ToCsvCell(s.CorrectAnswer ?? string.Empty),
                    ExportSafety.ToCsvCell(s.AlternateAnswers ?? string.Empty),
                    s.Points,
                    s.PenaltyPoints,
                    s.BonusPoints,
                    s.TimeLimitSeconds,
                    ExportSafety.ToCsvCell(s.HintsJson ?? string.Empty),
                    ExportSafety.ToCsvCell(s.Explanation ?? string.Empty),
                    ExportSafety.ToCsvCell(s.MediaPath ?? string.Empty)
                ));
            }

            return sb.ToString();
        }

        public static List<DeckSlide> ImportSlidesFromCsv(string csvContent, int quizDeckId, int startOrder = 1)
        {
            var slides = new List<DeckSlide>();
            if (string.IsNullOrWhiteSpace(csvContent)) return slides;

            var rows = ParseCsvRows(csvContent);
            if (rows.Count <= 1) return slides; // Only header or empty

            var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
            var colIndex = new Dictionary<string, int>();
            for (var i = 0; i < header.Count; i++)
            {
                colIndex[header[i]] = i;
            }

            int GetInt(List<string> row, string name, int def)
            {
                if (colIndex.TryGetValue(name, out var idx) && idx < row.Count)
                {
                    if (int.TryParse(row[idx].Trim(), out var val)) return val;
                }
                return def;
            }

            string GetString(List<string> row, string name, string def = "")
            {
                if (colIndex.TryGetValue(name, out var idx) && idx < row.Count)
                {
                    return row[idx].Trim();
                }
                return def;
            }

            var currentOrder = startOrder;
            for (var r = 1; r < rows.Count; r++)
            {
                var row = rows[r];
                if (row.Count == 0 || row.All(string.IsNullOrWhiteSpace)) continue;

                var title = GetString(row, "title", "Question " + currentOrder);
                var content = GetString(row, "contenttext", GetString(row, "question", string.Empty));
                var rawOptions = GetString(row, "options", string.Empty);
                var optionsFormatted = rawOptions.Contains("|")
                    ? string.Join(Environment.NewLine, rawOptions.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries).Select(o => o.Trim()))
                    : rawOptions;

                var slide = new DeckSlide
                {
                    QuizDeckId = quizDeckId,
                    SlideOrder = currentOrder++,
                    SlideType = GetString(row, "slidetype", "Question"),
                    QuestionType = GetString(row, "questiontype", "MultipleChoice"),
                    RoundType = GetString(row, "roundtype", "Standard"),
                    RoundName = GetString(row, "roundname", "General"),
                    Title = title,
                    ContentText = content,
                    OptionsJson = optionsFormatted,
                    CorrectAnswer = GetString(row, "correctanswer", string.Empty),
                    AlternateAnswers = GetString(row, "alternateanswers", string.Empty),
                    Points = GetInt(row, "points", 10),
                    PenaltyPoints = GetInt(row, "penaltypoints", 0),
                    BonusPoints = GetInt(row, "bonuspoints", 5),
                    TimeLimitSeconds = GetInt(row, "timelimitseconds", 30),
                    HintsJson = GetString(row, "hints", string.Empty),
                    Explanation = GetString(row, "explanation", string.Empty),
                    MediaPath = GetString(row, "mediapath", string.Empty),
                    AllowPass = true,
                    IsActive = true
                };

                slides.Add(slide);
            }

            return slides;
        }

        private static List<List<string>> ParseCsvRows(string text)
        {
            var rows = new List<List<string>>();
            var currentRow = new List<string>();
            var currentField = new StringBuilder();
            var inQuotes = false;

            for (var i = 0; i < text.Length; i++)
            {
                var c = text[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '"')
                        {
                            currentField.Append('"');
                            i++;
                        }
                        else
                        {
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        currentRow.Add(currentField.ToString());
                        currentField.Clear();
                    }
                    else if (c == '\r')
                    {
                        if (i + 1 < text.Length && text[i + 1] == '\n') i++;
                        currentRow.Add(currentField.ToString());
                        currentField.Clear();
                        rows.Add(currentRow);
                        currentRow = new List<string>();
                    }
                    else if (c == '\n')
                    {
                        currentRow.Add(currentField.ToString());
                        currentField.Clear();
                        rows.Add(currentRow);
                        currentRow = new List<string>();
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
            }

            if (currentField.Length > 0 || currentRow.Count > 0)
            {
                currentRow.Add(currentField.ToString());
                rows.Add(currentRow);
            }

            return rows;
        }
    }
}
