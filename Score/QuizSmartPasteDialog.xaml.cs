using System;
using System.Collections.Generic;
using System.Windows;

namespace Score
{
    public partial class QuizSmartPasteDialog : Window
    {
        private readonly int _deckId;
        private readonly int _startOrder;

        internal List<DeckSlide> ParsedSlides { get; private set; } = new List<DeckSlide>();
        public bool ReplaceExisting { get; private set; }

        public QuizSmartPasteDialog(int deckId, int startOrder)
        {
            InitializeComponent();
            _deckId = deckId;
            _startOrder = startOrder;
            Loaded += QuizSmartPasteDialog_Loaded;
        }

        private void QuizSmartPasteDialog_Loaded(object sender, RoutedEventArgs e)
        {
            // Seed a helpful sample format if text is empty
            if (string.IsNullOrWhiteSpace(PasteInputBox.Text))
            {
                PasteInputBox.Text =
                    "1. Which planet has the most confirmed moons in our solar system?\n" +
                    "A) Jupiter\n" +
                    "B) Saturn\n" +
                    "C) Uranus\n" +
                    "D) Neptune\n" +
                    "Answer: B\n" +
                    "Explanation: Saturn has 146 confirmed moons as of 2023.\n" +
                    "Hint: It is famous for its prominent ring system.\n\n" +
                    "2. The Great Wall of China is easily visible from the Moon with the naked eye.\n" +
                    "True or False?\n" +
                    "Answer: False\n" +
                    "Explanation: Apollo astronauts confirmed it cannot be discerned without binoculars or optical aid.\n\n" +
                    "3. Who painted the celebrated masterpiece 'The Starry Night'?\n" +
                    "Answer: Vincent van Gogh\n" +
                    "Aliases: Van Gogh; Vincent\n" +
                    "Points: 15\n" +
                    "Hint: Painted in June 1889 at Saint-Rémy-de-Provence.";
            }
        }

        private void ParseButton_Click(object sender, RoutedEventArgs e)
        {
            var text = PasteInputBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show(this, "Please paste questions into the text box above.", "Input Required", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            ParsedSlides = QuizAIGeneratorService.ParseAiQuestions(text, _deckId, _startOrder);

            ParsedQuestionsListBox.ItemsSource = null;
            ParsedQuestionsListBox.ItemsSource = ParsedSlides;

            if (ParsedSlides.Count > 0)
            {
                ParseStatusLabel.Text = $"✓ Successfully parsed {ParsedSlides.Count} question{(ParsedSlides.Count == 1 ? "" : "s")}. Ready to import.";
                ImportButton.IsEnabled = true;
                ImportButton.Content = $"✓ Import ({ParsedSlides.Count}) Questions into Deck";
            }
            else
            {
                ParseStatusLabel.Text = "Could not identify distinct questions. Ensure questions have question text and answers.";
                ImportButton.IsEnabled = false;
                ImportButton.Content = "✓ Import Questions into Deck";
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            PasteInputBox.Clear();
            ParsedSlides.Clear();
            ParsedQuestionsListBox.ItemsSource = null;
            ImportButton.IsEnabled = false;
            ImportButton.Content = "✓ Import Questions into Deck";
            ParseStatusLabel.Text = "Text cleared. Paste new questions and click Parse.";
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void ImportButton_Click(object sender, RoutedEventArgs e)
        {
            if (ParsedSlides.Count == 0) return;
            ReplaceExisting = ReplaceExistingCheckBox.IsChecked == true;
            DialogResult = true;
            Close();
        }
    }
}
