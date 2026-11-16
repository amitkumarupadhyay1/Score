using System;
using System.Collections.Generic;

namespace Score
{
    class TopicPack
    {
        public string Key { get; set; }
        public string Name { get; set; }
        public string Icon { get; set; }
        public string Description { get; set; }
        public List<DeckSlide> Questions { get; set; }
    }

    static class QuizCuratedTopics
    {
        public static List<TopicPack> GetCuratedTopicPacks(int deckId = 0)
        {
            return new List<TopicPack>
            {
                new TopicPack
                {
                    Key = "science",
                    Name = "Science & Technology",
                    Icon = "🔬",
                    Description = "Physics, Astronomy, Biology, and Groundbreaking Tech",
                    Questions = new List<DeckSlide>
                    {
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionMultipleChoice,
                            RoundType = QuizScoringEngine.RoundStandard,
                            RoundName = "Science & Tech",
                            Title = "Cosmic Abundance",
                            ContentText = "What is the most abundant chemical element in the observable universe?",
                            OptionsJson = "Hydrogen\nHelium\nOxygen\nCarbon",
                            CorrectAnswer = "Hydrogen",
                            Points = 10,
                            PenaltyPoints = 0,
                            BonusPoints = 5,
                            TimeLimitSeconds = 30,
                            AllowPass = true,
                            HintsJson = "It is the simplest element, consisting of just one proton and one electron.\nIt powers nuclear fusion in main-sequence stars like our Sun.",
                            Explanation = "Hydrogen accounts for roughly 75% of all normal baryonic matter in the universe.",
                            IsActive = true
                        },
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionTrueFalse,
                            RoundType = QuizScoringEngine.RoundStandard,
                            RoundName = "Science & Tech",
                            Title = "Acoustic Velocity",
                            ContentText = "Sound waves travel faster through steel than they do through air.",
                            OptionsJson = "True\nFalse",
                            CorrectAnswer = "True",
                            Points = 10,
                            PenaltyPoints = 5,
                            BonusPoints = 5,
                            TimeLimitSeconds = 20,
                            AllowPass = true,
                            HintsJson = "Think about molecular density and elasticity in solids versus gases.",
                            Explanation = "Sound travels at roughly 5,100 m/s in steel compared to only ~343 m/s in air at room temperature.",
                            IsActive = true
                        },
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionMultipleChoice,
                            RoundType = QuizScoringEngine.RoundStandard,
                            RoundName = "Science & Tech",
                            Title = "Origin of Mass",
                            ContentText = "Which particle discovered at CERN in 2012 provides mass to other fundamental particles via its associated field?",
                            OptionsJson = "Higgs Boson\nPhoton\nGluon\nNeutrino",
                            CorrectAnswer = "Higgs Boson",
                            Points = 10,
                            PenaltyPoints = 0,
                            BonusPoints = 5,
                            TimeLimitSeconds = 30,
                            AllowPass = true,
                            HintsJson = "It was nicknamed the 'God Particle' in popular media.\nPredicted theoretically in 1964 by Peter Higgs and collaborators.",
                            Explanation = "The discovery of the Higgs boson was announced by the ATLAS and CMS collaborations at CERN on July 4, 2012.",
                            IsActive = true
                        },
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionShortAnswer,
                            RoundType = QuizScoringEngine.RoundBuzzer,
                            RoundName = "Science & Tech",
                            Title = "Cellular Energy",
                            ContentText = "What double-membrane organelle is widely known as the powerhouse of eukaryotic cells?",
                            CorrectAnswer = "Mitochondria",
                            AlternateAnswers = "Mitochondrion; Mitochondria",
                            Points = 15,
                            PenaltyPoints = 5,
                            BonusPoints = 7,
                            TimeLimitSeconds = 25,
                            AllowPass = false,
                            HintsJson = "It generates most of the chemical energy needed by the cell in the form of ATP.",
                            Explanation = "Mitochondria produce adenosine triphosphate (ATP) through cellular respiration.",
                            IsActive = true
                        },
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionMultipleChoice,
                            RoundType = QuizScoringEngine.RoundRapidFire,
                            RoundName = "Science & Tech",
                            Title = "Planetary Atmospheres",
                            ContentText = "Which planet in our solar system has the highest surface temperature?",
                            OptionsJson = "Venus\nMercury\nMars\nJupiter",
                            CorrectAnswer = "Venus",
                            Points = 10,
                            PenaltyPoints = 0,
                            BonusPoints = 5,
                            TimeLimitSeconds = 20,
                            AllowPass = true,
                            HintsJson = "Its dense carbon dioxide atmosphere causes an extreme runaway greenhouse effect.\nIt is the second planet from the Sun.",
                            Explanation = "Venus maintains an average surface temperature of about 465°C (869°F), hotter than Mercury despite being farther from the Sun.",
                            IsActive = true
                        }
                    }
                },
                new TopicPack
                {
                    Key = "history",
                    Name = "History & Civilization",
                    Icon = "🏛️",
                    Description = "Ancient Empires, Revolutions, and World Milestones",
                    Questions = new List<DeckSlide>
                    {
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionMultipleChoice,
                            RoundType = QuizScoringEngine.RoundStandard,
                            RoundName = "World History",
                            Title = "First Moon Landing",
                            ContentText = "In which year did the Apollo 11 lunar module land the first humans on the Moon?",
                            OptionsJson = "1969\n1965\n1971\n1973",
                            CorrectAnswer = "1969",
                            Points = 10,
                            PenaltyPoints = 0,
                            BonusPoints = 5,
                            TimeLimitSeconds = 30,
                            AllowPass = true,
                            HintsJson = "It took place during the presidency of Richard Nixon.\nNeil Armstrong and Buzz Aldrin stepped onto the Sea of Tranquility.",
                            Explanation = "Apollo 11 touched down on July 20, 1969, fulfilling President Kennedy's 1961 goal.",
                            IsActive = true
                        },
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionTrueFalse,
                            RoundType = QuizScoringEngine.RoundStandard,
                            RoundName = "World History",
                            Title = "The Rosetta Stone",
                            ContentText = "The Rosetta Stone proved key to deciphering ancient Egyptian hieroglyphics.",
                            OptionsJson = "True\nFalse",
                            CorrectAnswer = "True",
                            Points = 10,
                            PenaltyPoints = 5,
                            BonusPoints = 5,
                            TimeLimitSeconds = 20,
                            AllowPass = true,
                            HintsJson = "It featured the same royal decree written in three distinct scripts.",
                            Explanation = "Jean-François Champollion used the Greek and Demotic texts on the stone to crack hieroglyphs in 1822.",
                            IsActive = true
                        },
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionMultipleChoice,
                            RoundType = QuizScoringEngine.RoundStandard,
                            RoundName = "World History",
                            Title = "South African Democracy",
                            ContentText = "Who was inaugurated as the first democratically elected President of South Africa in 1994?",
                            OptionsJson = "Nelson Mandela\nDesmond Tutu\nThabo Mbeki\nF. W. de Klerk",
                            CorrectAnswer = "Nelson Mandela",
                            Points = 10,
                            PenaltyPoints = 0,
                            BonusPoints = 5,
                            TimeLimitSeconds = 30,
                            AllowPass = true,
                            HintsJson = "He spent 27 years in prison, including 18 on Robben Island, before his release in 1990.",
                            Explanation = "Nelson Mandela won the historic 1994 general election, marking the end of apartheid.",
                            IsActive = true
                        },
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionShortAnswer,
                            RoundType = QuizScoringEngine.RoundBuzzer,
                            RoundName = "World History",
                            Title = "The Great Charter",
                            ContentText = "What celebrated English legal charter was granted by King John at Runnymede in the year 1215?",
                            CorrectAnswer = "Magna Carta",
                            AlternateAnswers = "The Magna Carta; Great Charter; Magna Charta",
                            Points = 15,
                            PenaltyPoints = 5,
                            BonusPoints = 7,
                            TimeLimitSeconds = 25,
                            AllowPass = false,
                            HintsJson = "Latin for 'Great Charter', it established that everyone—even the monarch—is subject to the law.",
                            Explanation = "Magna Carta was agreed by King John on 15 June 1215 to make peace between an unpopular king and rebel barons.",
                            IsActive = true
                        },
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionMultipleChoice,
                            RoundType = QuizScoringEngine.RoundRapidFire,
                            RoundName = "World History",
                            Title = "The Fall of Constantinople",
                            ContentText = "Which Ottoman sultan led the siege and capture of Constantinople in 1453?",
                            OptionsJson = "Mehmed II\nSuleiman the Magnificent\nSelim I\nOsman I",
                            CorrectAnswer = "Mehmed II",
                            Points = 10,
                            PenaltyPoints = 0,
                            BonusPoints = 5,
                            TimeLimitSeconds = 20,
                            AllowPass = true,
                            HintsJson = "He was known by the epithet 'the Conqueror' (Fatih).",
                            Explanation = "Mehmed II conquered the Byzantine capital on 29 May 1453 at the age of 21.",
                            IsActive = true
                        }
                    }
                },
                new TopicPack
                {
                    Key = "geography",
                    Name = "World Geography",
                    Icon = "🌍",
                    Description = "Capitals, Natural Wonders, Rivers, and Topography",
                    Questions = new List<DeckSlide>
                    {
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionMultipleChoice,
                            RoundType = QuizScoringEngine.RoundStandard,
                            RoundName = "World Geography",
                            Title = "Southernmost Capital",
                            ContentText = "Which city is the southernmost national capital of a sovereign state in the world?",
                            OptionsJson = "Wellington\nCanberra\nBuenos Aires\nPretoria",
                            CorrectAnswer = "Wellington",
                            Points = 10,
                            PenaltyPoints = 0,
                            BonusPoints = 5,
                            TimeLimitSeconds = 30,
                            AllowPass = true,
                            HintsJson = "It is located at the southwestern tip of New Zealand's North Island.\nIt sits at approximately 41° South latitude.",
                            Explanation = "Wellington, New Zealand holds the record as the world's southernmost sovereign capital city.",
                            IsActive = true
                        },
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionTrueFalse,
                            RoundType = QuizScoringEngine.RoundStandard,
                            RoundName = "World Geography",
                            Title = "Deepest Freshwater Lake",
                            ContentText = "Lake Baikal in Siberia is both the world's deepest and oldest freshwater lake.",
                            OptionsJson = "True\nFalse",
                            CorrectAnswer = "True",
                            Points = 10,
                            PenaltyPoints = 5,
                            BonusPoints = 5,
                            TimeLimitSeconds = 20,
                            AllowPass = true,
                            HintsJson = "It holds roughly 20% of the world's unfrozen surface freshwater.",
                            Explanation = "Lake Baikal reaches a maximum depth of 1,642 meters (5,387 feet) and is over 25 million years old.",
                            IsActive = true
                        },
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionMultipleChoice,
                            RoundType = QuizScoringEngine.RoundStandard,
                            RoundName = "World Geography",
                            Title = "Longest Coastline",
                            ContentText = "Which country in the world possesses the longest total coastline?",
                            OptionsJson = "Canada\nNorway\nIndonesia\nRussia",
                            CorrectAnswer = "Canada",
                            Points = 10,
                            PenaltyPoints = 0,
                            BonusPoints = 5,
                            TimeLimitSeconds = 30,
                            AllowPass = true,
                            HintsJson = "Its borders touch three oceans: Atlantic, Pacific, and Arctic.",
                            Explanation = "Canada's coastline stretches over 243,042 kilometers (151,019 miles), the longest in the world.",
                            IsActive = true
                        },
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionShortAnswer,
                            RoundType = QuizScoringEngine.RoundBuzzer,
                            RoundName = "World Geography",
                            Title = "Highest Waterfall",
                            ContentText = "What is the name of the world's highest uninterrupted waterfall, situated in Venezuela?",
                            CorrectAnswer = "Angel Falls",
                            AlternateAnswers = "Salto Angel; Kerepakupai Meru",
                            Points = 15,
                            PenaltyPoints = 5,
                            BonusPoints = 7,
                            TimeLimitSeconds = 25,
                            AllowPass = false,
                            HintsJson = "It drops from the Auyán-tepui mountain in Canaima National Park.",
                            Explanation = "Angel Falls plunges 979 meters (3,212 feet) with an uninterrupted drop of 807 meters.",
                            IsActive = true
                        },
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionMultipleChoice,
                            RoundType = QuizScoringEngine.RoundRapidFire,
                            RoundName = "World Geography",
                            Title = "Desert Ecology",
                            ContentText = "Which desert is recognized as the driest non-polar desert on Earth?",
                            OptionsJson = "Atacama Desert\nSahara Desert\nKalahari Desert\nGobi Desert",
                            CorrectAnswer = "Atacama Desert",
                            Points = 10,
                            PenaltyPoints = 0,
                            BonusPoints = 5,
                            TimeLimitSeconds = 20,
                            AllowPass = true,
                            HintsJson = "It is located in South America, primarily in northern Chile along the Pacific coast.",
                            Explanation = "The Atacama Desert is so dry that some weather stations there have never recorded rainfall.",
                            IsActive = true
                        }
                    }
                },
                new TopicPack
                {
                    Key = "popculture",
                    Name = "Pop Culture & Cinema",
                    Icon = "🎬",
                    Description = "Hollywood, Legendary Music, Television, and Gaming",
                    Questions = new List<DeckSlide>
                    {
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionMultipleChoice,
                            RoundType = QuizScoringEngine.RoundStandard,
                            RoundName = "Pop Culture",
                            Title = "Inaugural Best Picture",
                            ContentText = "Which film won the Academy Award for Best Picture at the 1st Academy Awards in 1929?",
                            OptionsJson = "Wings\nSunrise\nMetropolis\nThe Jazz Singer",
                            CorrectAnswer = "Wings",
                            Points = 10,
                            PenaltyPoints = 0,
                            BonusPoints = 5,
                            TimeLimitSeconds = 30,
                            AllowPass = true,
                            HintsJson = "It was a silent World War I aviation romantic drama directed by William A. Wellman.",
                            Explanation = "'Wings' (1927) was the first movie ever to win the Academy Award for Best Picture (then called Outstanding Picture).",
                            IsActive = true
                        },
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionTrueFalse,
                            RoundType = QuizScoringEngine.RoundStandard,
                            RoundName = "Pop Culture",
                            Title = "Best-Selling Album",
                            ContentText = "Michael Jackson's 1982 album 'Thriller' remains the best-selling album of all time worldwide.",
                            OptionsJson = "True\nFalse",
                            CorrectAnswer = "True",
                            Points = 10,
                            PenaltyPoints = 5,
                            BonusPoints = 5,
                            TimeLimitSeconds = 20,
                            AllowPass = true,
                            HintsJson = "Produced by Quincy Jones, it yielded seven top-ten singles.",
                            Explanation = "'Thriller' has sold an estimated 70 million copies globally, solidifying its position as the top seller.",
                            IsActive = true
                        },
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionMultipleChoice,
                            RoundType = QuizScoringEngine.RoundStandard,
                            RoundName = "Pop Culture",
                            Title = "Television Icon",
                            ContentText = "In the acclaimed TV drama 'Breaking Bad', what criminal alias does chemistry teacher Walter White adopt?",
                            OptionsJson = "Heisenberg\nOppenheimer\nSchrodinger\nFermi",
                            CorrectAnswer = "Heisenberg",
                            Points = 10,
                            PenaltyPoints = 0,
                            BonusPoints = 5,
                            TimeLimitSeconds = 30,
                            AllowPass = true,
                            HintsJson = "Named in tribute to the German Nobel laureate who formulated the uncertainty principle.",
                            Explanation = "Walter White chose the pseudonym 'Heisenberg' as his underground meth kingpin identity.",
                            IsActive = true
                        },
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionShortAnswer,
                            RoundType = QuizScoringEngine.RoundBuzzer,
                            RoundName = "Pop Culture",
                            Title = "Video Game Origins",
                            ContentText = "What was Nintendo's iconic plumber Mario called when he debuted in the 1981 arcade game Donkey Kong?",
                            CorrectAnswer = "Jumpman",
                            AlternateAnswers = "Jumpman; Mr. Video",
                            Points = 15,
                            PenaltyPoints = 5,
                            BonusPoints = 7,
                            TimeLimitSeconds = 25,
                            AllowPass = false,
                            HintsJson = "Created by Shigeru Miyamoto, he wore red overalls and a blue shirt.",
                            Explanation = "Mario was originally named Jumpman before Nintendo of America renamed him after their landlord Mario Segale.",
                            IsActive = true
                        },
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionMultipleChoice,
                            RoundType = QuizScoringEngine.RoundRapidFire,
                            RoundName = "Pop Culture",
                            Title = "Animated Royalty",
                            ContentText = "What is the fictional Scandinavian kingdom where Disney's 'Frozen' is set?",
                            OptionsJson = "Arendelle\nCorona\nGenovia\nDunBroch",
                            CorrectAnswer = "Arendelle",
                            Points = 10,
                            PenaltyPoints = 0,
                            BonusPoints = 5,
                            TimeLimitSeconds = 20,
                            AllowPass = true,
                            HintsJson = "Ruled by royal sisters Anna and Elsa.",
                            Explanation = "Arendelle was inspired by Norwegian fjords, stave churches, and Bergen architecture.",
                            IsActive = true
                        }
                    }
                },
                new TopicPack
                {
                    Key = "sports",
                    Name = "Sports & Olympics",
                    Icon = "⚽",
                    Description = "World Cups, Track & Field Records, Tennis, and Legend",
                    Questions = new List<DeckSlide>
                    {
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionMultipleChoice,
                            RoundType = QuizScoringEngine.RoundStandard,
                            RoundName = "Sports",
                            Title = "World Sprint Record",
                            ContentText = "Who holds the men's 100-meter world sprint record of 9.58 seconds?",
                            OptionsJson = "Usain Bolt\nTyson Gay\nYohan Blake\nCarl Lewis",
                            CorrectAnswer = "Usain Bolt",
                            Points = 10,
                            PenaltyPoints = 0,
                            BonusPoints = 5,
                            TimeLimitSeconds = 30,
                            AllowPass = true,
                            HintsJson = "The Jamaican superstar set the record at the 2009 World Athletics Championships in Berlin.",
                            Explanation = "Usain Bolt ran 9.58s on August 16, 2009, with a top speed of 44.72 km/h (27.78 mph).",
                            IsActive = true
                        },
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionTrueFalse,
                            RoundType = QuizScoringEngine.RoundStandard,
                            RoundName = "Sports",
                            Title = "Basketball Regulations",
                            ContentText = "A regulation basketball game has 5 active players on the court per team at any one time.",
                            OptionsJson = "True\nFalse",
                            CorrectAnswer = "True",
                            Points = 10,
                            PenaltyPoints = 5,
                            BonusPoints = 5,
                            TimeLimitSeconds = 20,
                            AllowPass = true,
                            HintsJson = "Standard positions include point guard, shooting guard, small forward, power forward, and center.",
                            Explanation = "Under FIBA and NBA rules, each team plays with 5 players on the court (10 total players).",
                            IsActive = true
                        },
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionMultipleChoice,
                            RoundType = QuizScoringEngine.RoundStandard,
                            RoundName = "Sports",
                            Title = "FIFA World Cup Dynasty",
                            ContentText = "Which nation has won the FIFA Men's World Cup trophy the most times?",
                            OptionsJson = "Brazil\nGermany\nItaly\nArgentina",
                            CorrectAnswer = "Brazil",
                            Points = 10,
                            PenaltyPoints = 0,
                            BonusPoints = 5,
                            TimeLimitSeconds = 30,
                            AllowPass = true,
                            HintsJson = "Known as A Seleção, they captured titles in 1958, 1962, 1970, 1994, and 2002.",
                            Explanation = "Brazil holds 5 World Cup victories, followed by Germany and Italy with 4 each.",
                            IsActive = true
                        },
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionShortAnswer,
                            RoundType = QuizScoringEngine.RoundBuzzer,
                            RoundName = "Sports",
                            Title = "Grand Slam Grass",
                            ContentText = "What historic Grand Slam tennis championship is played on grass courts at the All England Club?",
                            CorrectAnswer = "Wimbledon",
                            AlternateAnswers = "The Championships, Wimbledon; Wimbledon Championship",
                            Points = 15,
                            PenaltyPoints = 5,
                            BonusPoints = 7,
                            TimeLimitSeconds = 25,
                            AllowPass = false,
                            HintsJson = "Founded in 1877 in southwest London, it is the oldest tennis tournament in the world.",
                            Explanation = "Wimbledon is the only major tennis championship still contested on traditional outdoor grass courts.",
                            IsActive = true
                        },
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionMultipleChoice,
                            RoundType = QuizScoringEngine.RoundRapidFire,
                            RoundName = "Sports",
                            Title = "Olympic Rings",
                            ContentText = "How many interlocking rings make up the official Olympic Games symbol?",
                            OptionsJson = "5\n4\n6\n7",
                            CorrectAnswer = "5",
                            Points = 10,
                            PenaltyPoints = 0,
                            BonusPoints = 5,
                            TimeLimitSeconds = 20,
                            AllowPass = true,
                            HintsJson = "Colored blue, yellow, black, green, and red, representing the five inhabited continents.",
                            Explanation = "Pierre de Coubertin designed the five interlocking rings in 1913 to symbolize universal athletic fraternity.",
                            IsActive = true
                        }
                    }
                },
                new TopicPack
                {
                    Key = "literature",
                    Name = "Literature & Arts",
                    Icon = "📚",
                    Description = "Classic Masterpieces, World Lore, Authors, and Fine Art",
                    Questions = new List<DeckSlide>
                    {
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionMultipleChoice,
                            RoundType = QuizScoringEngine.RoundStandard,
                            RoundName = "Literature & Art",
                            Title = "Gothic Breakthrough",
                            ContentText = "Who wrote the seminal 1818 Gothic novel 'Frankenstein; or, The Modern Prometheus'?",
                            OptionsJson = "Mary Shelley\nBram Stoker\nLord Byron\nPercy Bysshe Shelley",
                            CorrectAnswer = "Mary Shelley",
                            Points = 10,
                            PenaltyPoints = 0,
                            BonusPoints = 5,
                            TimeLimitSeconds = 30,
                            AllowPass = true,
                            HintsJson = "She began conceiving the tale during a rainy summer at Lake Geneva in 1816 when she was just 18.",
                            Explanation = "Mary Wollstonecraft Shelley published Frankenstein anonymously in London on 1 January 1818.",
                            IsActive = true
                        },
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionTrueFalse,
                            RoundType = QuizScoringEngine.RoundStandard,
                            RoundName = "Literature & Art",
                            Title = "Dystopian Lexicon",
                            ContentText = "George Orwell's dystopian masterpiece 'Nineteen Eighty-Four' introduced the concept and phrase 'Big Brother'.",
                            OptionsJson = "True\nFalse",
                            CorrectAnswer = "True",
                            Points = 10,
                            PenaltyPoints = 5,
                            BonusPoints = 5,
                            TimeLimitSeconds = 20,
                            AllowPass = true,
                            HintsJson = "The novel also popularized 'Thought Police', 'Doublethink', and 'Memory Hole'.",
                            Explanation = "Published in 1949, Orwell's vision of Oceania established 'Big Brother is watching you' as a lasting cultural motif.",
                            IsActive = true
                        },
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionMultipleChoice,
                            RoundType = QuizScoringEngine.RoundStandard,
                            RoundName = "Literature & Art",
                            Title = "Post-Impressionist Vision",
                            ContentText = "Which Dutch post-impressionist master painted 'The Starry Night' in June 1889?",
                            OptionsJson = "Vincent van Gogh\nRembrandt\nJohannes Vermeer\nPiet Mondrian",
                            CorrectAnswer = "Vincent van Gogh",
                            Points = 10,
                            PenaltyPoints = 0,
                            BonusPoints = 5,
                            TimeLimitSeconds = 30,
                            AllowPass = true,
                            HintsJson = "Painted while in the Saint-Paul-de-Mausole asylum in Saint-Rémy-de-Provence.",
                            Explanation = "Van Gogh depicted the view from his east-facing window just before sunrise, with an idealized village added.",
                            IsActive = true
                        },
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionShortAnswer,
                            RoundType = QuizScoringEngine.RoundBuzzer,
                            RoundName = "Literature & Art",
                            Title = "Homeric Epic",
                            ContentText = "What epic ancient Greek poem attributed to Homer describes the wrath of Achilles during the Trojan War?",
                            CorrectAnswer = "Iliad",
                            AlternateAnswers = "The Iliad; Homer's Iliad",
                            Points = 15,
                            PenaltyPoints = 5,
                            BonusPoints = 7,
                            TimeLimitSeconds = 25,
                            AllowPass = false,
                            HintsJson = "It focuses on the bitter quarrel between King Agamemnon and the warrior Achilles.\nIt precedes the Odyssey.",
                            Explanation = "The Iliad is traditionally accepted as the oldest surviving work of European literature.",
                            IsActive = true
                        },
                        new DeckSlide
                        {
                            QuizDeckId = deckId,
                            SlideType = "Question",
                            QuestionType = QuizScoringEngine.QuestionMultipleChoice,
                            RoundType = QuizScoringEngine.RoundRapidFire,
                            RoundName = "Literature & Art",
                            Title = "High Fantasy Epic",
                            ContentText = "Which Oxford philologist and scholar authored the epic high fantasy trilogy 'The Lord of the Rings'?",
                            OptionsJson = "J. R. R. Tolkien\nC. S. Lewis\nGeorge R. R. Martin\nPhilip Pullman",
                            CorrectAnswer = "J. R. R. Tolkien",
                            Points = 10,
                            PenaltyPoints = 0,
                            BonusPoints = 5,
                            TimeLimitSeconds = 20,
                            AllowPass = true,
                            HintsJson = "He created the languages Quenya and Sindarin as part of his Legendarium.",
                            Explanation = "J.R.R. Tolkien published The Fellowship of the Ring, The Two Towers, and The Return of the King in 1954-1955.",
                            IsActive = true
                        }
                    }
                }
            };
        }
    }
}
