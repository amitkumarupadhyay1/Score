using System;
using System.Collections.Generic;
using System.Linq;

namespace Score
{
    internal static class QuizSampleDeckSeeder
    {
        public const string SampleDeckTitle = "General Knowledge Championship 2026";

        public static void EnsureSampleDeck(ScoreDbContext db)
        {
            if (db == null) return;
            try
            {
                if (db.QuizDecks.Any(d => d.IsActive))
                    return;

                var sampleDeck = new QuizDeck
                {
                    Title = SampleDeckTitle,
                    Description = "Complete sample quiz showcasing MCQ, True/False, Short Answer, Buzzer, Clues, and Tie-Breakers. Fully editable and deletable.",
                    Theme = "Stage",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow,
                    IsActive = true
                };

                db.QuizDecks.Add(sampleDeck);
                db.SaveChanges();

                var slides = CreateSampleSlides(sampleDeck.QuizDeckId);
                db.DeckSlides.AddRange(slides);
                db.SaveChanges();
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Could not seed showcase sample quiz.", ex);
            }
        }

        public static List<DeckSlide> CreateSampleSlides(int deckId)
        {
            return new List<DeckSlide>
            {
                new DeckSlide
                {
                    QuizDeckId = deckId,
                    SlideOrder = 1,
                    SlideType = "Question",
                    QuestionType = QuizScoringEngine.QuestionMultipleChoice,
                    RoundType = QuizScoringEngine.RoundStandard,
                    RoundName = "World Geography",
                    Title = "Federal Capitals",
                    ContentText = "Which planned city serves as the federal capital of Australia, located within the ACT?",
                    OptionsJson = "Sydney\nCanberra\nMelbourne\nBrisbane",
                    CorrectAnswer = "Canberra",
                    AlternateAnswers = "City of Canberra",
                    Points = 10,
                    PenaltyPoints = 0,
                    BonusPoints = 5,
                    TimeLimitSeconds = 30,
                    AllowPass = true,
                    HintsJson = "It was selected in 1908 as a compromise between Sydney and Melbourne.\nIt houses the Parliament House on Capital Hill.",
                    Explanation = "Canberra was founded in 1913 as Australia's purpose-built capital territory.",
                    IsActive = true
                },
                new DeckSlide
                {
                    QuizDeckId = deckId,
                    SlideOrder = 2,
                    SlideType = "Instruction",
                    Title = "Championship Rules",
                    ContentText = "Welcome Contestants and Audience!\n\n• Each round tests different skills: Direct MCQs, Rapid Buzzers, Progressive Clues, and Sudden Death.\n• Listen carefully to the Quiz Master.\n• Good luck to all teams!",
                    Points = 0,
                    PenaltyPoints = 0,
                    BonusPoints = 0,
                    TimeLimitSeconds = 0,
                    AllowPass = false,
                    IsActive = true
                },
                new DeckSlide
                {
                    QuizDeckId = deckId,
                    SlideOrder = 3,
                    SlideType = "Question",
                    QuestionType = QuizScoringEngine.QuestionTrueFalse,
                    RoundType = QuizScoringEngine.RoundStandard,
                    RoundName = "Physics & Optics",
                    Title = "Speed of Light",
                    ContentText = "Light travels faster through water than it does through a total vacuum.",
                    OptionsJson = "True\nFalse",
                    CorrectAnswer = "False",
                    Points = 10,
                    PenaltyPoints = 5,
                    BonusPoints = 0,
                    TimeLimitSeconds = 20,
                    AllowPass = true,
                    HintsJson = "Consider optical refractive index and vacuum permittivity.",
                    Explanation = "Light reaches maximum speed (approx 300,000 km/s) in a vacuum; it slows down in denser optical media like water (approx 225,000 km/s).",
                    IsActive = true
                },
                new DeckSlide
                {
                    QuizDeckId = deckId,
                    SlideOrder = 4,
                    SlideType = "Question",
                    QuestionType = QuizScoringEngine.QuestionShortAnswer,
                    RoundType = QuizScoringEngine.RoundStandard,
                    RoundName = "Science & Biology",
                    Title = "Cellular Biology",
                    ContentText = "What double-membrane organelle is known as the powerhouse of eukaryotic cells, generating most ATP?",
                    OptionsJson = string.Empty,
                    CorrectAnswer = "Mitochondria",
                    AlternateAnswers = "Mitochondrion\nMitochondria",
                    Points = 15,
                    PenaltyPoints = 0,
                    BonusPoints = 5,
                    TimeLimitSeconds = 30,
                    AllowPass = true,
                    HintsJson = "They contain their own circular DNA independent of the nucleus.\nThey carry out oxidative phosphorylation.",
                    Explanation = "Mitochondria generate adenosine triphosphate (ATP) through cellular respiration.",
                    IsActive = true
                },
                new DeckSlide
                {
                    QuizDeckId = deckId,
                    SlideOrder = 5,
                    SlideType = "Question",
                    QuestionType = QuizScoringEngine.QuestionMultipleChoice,
                    RoundType = QuizScoringEngine.RoundBuzzer,
                    RoundName = "Rapid Buzzers",
                    Title = "Cosmic Abundance",
                    ContentText = "What is the most abundant chemical element in the observable universe by mass?",
                    OptionsJson = "Hydrogen\nHelium\nOxygen\nCarbon",
                    CorrectAnswer = "Hydrogen",
                    Points = 20,
                    PenaltyPoints = 10,
                    BonusPoints = 0,
                    TimeLimitSeconds = 15,
                    AllowPass = false,
                    HintsJson = "It has atomic number 1.\nIt accounts for approximately 75% of baryonic mass.",
                    Explanation = "Hydrogen constitutes roughly 75% of normal baryonic matter in the cosmos.",
                    IsActive = true
                },
                new DeckSlide
                {
                    QuizDeckId = deckId,
                    SlideOrder = 6,
                    SlideType = "Question",
                    QuestionType = QuizScoringEngine.QuestionShortAnswer,
                    RoundType = QuizScoringEngine.RoundStandard,
                    RoundName = "Inventions & Tech",
                    Title = "Mystery Innovation",
                    ContentText = "Identify this historical milestone from the progressive clues below.",
                    OptionsJson = string.Empty,
                    CorrectAnswer = "World Wide Web",
                    AlternateAnswers = "WWW\nThe Web\nWorld Wide Web",
                    Points = 25,
                    PenaltyPoints = 0,
                    BonusPoints = 0,
                    TimeLimitSeconds = 45,
                    AllowPass = true,
                    HintsJson = "Conceived in 1989 at CERN in Geneva, Switzerland.\nInvented by British computer scientist Tim Berners-Lee.\nIt introduced HTTP, HTML, and URLs to the public.",
                    Explanation = "Tim Berners-Lee created the World Wide Web at CERN to allow automated information sharing among scientists worldwide.",
                    IsActive = true
                },
                new DeckSlide
                {
                    QuizDeckId = deckId,
                    SlideOrder = 7,
                    SlideType = "Question",
                    QuestionType = QuizScoringEngine.QuestionMultipleChoice,
                    RoundType = QuizScoringEngine.RoundStandard,
                    RoundName = "World History",
                    Title = "Ancient Wonder",
                    ContentText = "Which of the Seven Wonders of the Ancient World is the only one largely standing today?",
                    OptionsJson = "Colossus of Rhodes\nHanging Gardens of Babylon\nGreat Pyramid of Giza\nLighthouse of Alexandria",
                    CorrectAnswer = "Great Pyramid of Giza",
                    AlternateAnswers = "Pyramid of Giza\nGreat Pyramid",
                    Points = 15,
                    PenaltyPoints = 0,
                    BonusPoints = 5,
                    TimeLimitSeconds = 25,
                    AllowPass = true,
                    HintsJson = "Located on the west bank of the Nile in modern Egypt.\nBuilt as a tomb for Fourth Dynasty pharaoh Khufu.",
                    Explanation = "The Great Pyramid of Giza was built circa 2560 BC and remains the only substantially intact ancient wonder.",
                    IsActive = true
                },
                new DeckSlide
                {
                    QuizDeckId = deckId,
                    SlideOrder = 8,
                    SlideType = "Question",
                    QuestionType = QuizScoringEngine.QuestionMultipleChoice,
                    RoundType = QuizScoringEngine.RoundBuzzer,
                    RoundName = "Rapid Buzzers",
                    Title = "Lunar Exploration",
                    ContentText = "In what year did the Apollo 11 mission successfully land the first humans on the Moon?",
                    OptionsJson = "1967\n1968\n1969\n1971",
                    CorrectAnswer = "1969",
                    Points = 20,
                    PenaltyPoints = 10,
                    BonusPoints = 0,
                    TimeLimitSeconds = 15,
                    AllowPass = false,
                    HintsJson = "Neil Armstrong and Buzz Aldrin stepped onto Mare Tranquillitatis on July 20.\nWoodstock took place later that same summer.",
                    Explanation = "Apollo 11 landed on the lunar surface on July 20, 1969.",
                    IsActive = true
                },
                new DeckSlide
                {
                    QuizDeckId = deckId,
                    SlideOrder = 9,
                    SlideType = "Question",
                    QuestionType = QuizScoringEngine.QuestionShortAnswer,
                    RoundType = QuizScoringEngine.RoundStandard,
                    RoundName = "Literature & Arts",
                    Title = "The Bard of Avon",
                    ContentText = "Which English playwright authored 'Hamlet', 'Macbeth', 'Othello', and 'King Lear'?",
                    OptionsJson = string.Empty,
                    CorrectAnswer = "William Shakespeare",
                    AlternateAnswers = "Shakespeare\nWilliam Shakespeare",
                    Points = 15,
                    PenaltyPoints = 0,
                    BonusPoints = 5,
                    TimeLimitSeconds = 25,
                    AllowPass = true,
                    HintsJson = "Associated with the Globe Theatre in London.\nBorn in Stratford-upon-Avon in 1564.",
                    Explanation = "William Shakespeare is widely regarded as the greatest writer in the English language.",
                    IsActive = true
                },
                new DeckSlide
                {
                    QuizDeckId = deckId,
                    SlideOrder = 10,
                    SlideType = "Question",
                    QuestionType = QuizScoringEngine.QuestionShortAnswer,
                    RoundType = "TieBreak",
                    RoundName = "Sudden Death",
                    Title = "Tie-Breaker Challenge",
                    ContentText = "What Latin phrase, meaning 'in good faith', is commonly used to describe genuine intentions or authentic credentials?",
                    OptionsJson = string.Empty,
                    CorrectAnswer = "Bona fide",
                    AlternateAnswers = "Bona Fide\nBonafide",
                    Points = 10,
                    PenaltyPoints = 5,
                    BonusPoints = 0,
                    TimeLimitSeconds = 20,
                    AllowPass = false,
                    HintsJson = "Its opposite is 'mala fide'.",
                    Explanation = "Bona fide translates literally as 'with good faith' in Latin.",
                    IsActive = true
                }
            };
        }
    }
}
