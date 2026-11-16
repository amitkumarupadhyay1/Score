using System;
using System.Windows;

namespace Score
{
    public partial class QuizQuickTopicDialog : Window
    {
        public string SelectedTopicKey { get; private set; } = "science";
        public string SelectedTopicTitle { get; private set; } = "Science & Technology";
        public bool ReplaceExisting { get; private set; }

        public QuizQuickTopicDialog()
        {
            InitializeComponent();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void TopicCard_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (sender == TopicScienceCard) TopicScienceRadio.IsChecked = true;
            else if (sender == TopicHistoryCard) TopicHistoryRadio.IsChecked = true;
            else if (sender == TopicGeographyCard) TopicGeographyRadio.IsChecked = true;
            else if (sender == TopicPopCultureCard) TopicPopCultureRadio.IsChecked = true;
            else if (sender == TopicSportsCard) TopicSportsRadio.IsChecked = true;
            else if (sender == TopicLiteratureCard) TopicLiteratureRadio.IsChecked = true;
        }

        private void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            if (TopicScienceRadio.IsChecked == true)
            {
                SelectedTopicKey = "science";
                SelectedTopicTitle = "Science & Technology";
            }
            else if (TopicHistoryRadio.IsChecked == true)
            {
                SelectedTopicKey = "history";
                SelectedTopicTitle = "History & Civilization";
            }
            else if (TopicGeographyRadio.IsChecked == true)
            {
                SelectedTopicKey = "geography";
                SelectedTopicTitle = "World Geography & Wonders";
            }
            else if (TopicPopCultureRadio.IsChecked == true)
            {
                SelectedTopicKey = "popculture";
                SelectedTopicTitle = "Pop Culture & Entertainment";
            }
            else if (TopicSportsRadio.IsChecked == true)
            {
                SelectedTopicKey = "sports";
                SelectedTopicTitle = "Sports & Olympic Games";
            }
            else if (TopicLiteratureRadio.IsChecked == true)
            {
                SelectedTopicKey = "literature";
                SelectedTopicTitle = "Literature & Arts";
            }

            ReplaceExisting = ReplaceExistingCheckBox.IsChecked == true;
            DialogResult = true;
            Close();
        }
    }
}
