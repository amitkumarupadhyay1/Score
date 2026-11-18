using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Score
{
    public partial class GuidedSetupWizardWindow : Window
    {
        public string ConfiguredTitle { get; private set; } = "Live Quiz Event";
        public List<string> ConfiguredTeams { get; private set; } = new List<string>();
        public int ConfiguredRounds { get; private set; } = 3;
        public string ConfiguredRoundName { get; private set; } = "General Knowledge";
        public string ConfiguredRoundFormat { get; private set; } = "Standard";
        public int ConfiguredRoundDurationSeconds { get; private set; } = 120;
        public int ConfiguredQuestionDurationSeconds { get; private set; } = 30;
        public string ConfiguredMode { get; private set; } = "LiveDesk";

        private int _selectedTeamCount = 4;

        public GuidedSetupWizardWindow(string initialTitle = null, IEnumerable<string> initialTeams = null)
        {
            InitializeComponent();
            BuildTeamCountButtons();

            if (!string.IsNullOrWhiteSpace(initialTitle))
                EventTitleInput.Text = initialTitle;

            var existing = initialTeams?.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
            if (existing != null && existing.Count >= 2)
            {
                _selectedTeamCount = Math.Min(12, Math.Max(2, existing.Count));
                TeamNamesEditor.Text = string.Join(Environment.NewLine, existing);
            }
            else
            {
                SetDefaultTeams(_selectedTeamCount);
            }

            HighlightTeamCountButton(_selectedTeamCount);
        }

        private void BuildTeamCountButtons()
        {
            TeamCountButtonPanel.Children.Clear();
            var counts = new[] { 2, 3, 4, 5, 6, 8, 10, 12 };
            foreach (var count in counts)
            {
                var btn = new Button
                {
                    Content = count + " Teams",
                    Tag = count,
                    Height = 28,
                    Padding = new Thickness(10, 0, 10, 0),
                    Margin = new Thickness(0, 0, 6, 4),
                    Style = (Style)FindResource("ToolbarButtonStyle"),
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 11
                };
                btn.Click += TeamCountQuickButton_Click;
                TeamCountButtonPanel.Children.Add(btn);
            }
        }

        private void TeamCountQuickButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is int count)
            {
                _selectedTeamCount = count;
                SetDefaultTeams(count);
                HighlightTeamCountButton(count);
            }
        }

        private void HighlightTeamCountButton(int activeCount)
        {
            foreach (Button btn in TeamCountButtonPanel.Children.OfType<Button>())
            {
                var count = (int)btn.Tag;
                if (count == activeCount)
                {
                    btn.Background = (Brush)new BrushConverter().ConvertFromString("#FF4F46E5");
                    btn.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#FF4338CA");
                    btn.Foreground = Brushes.White;
                }
                else
                {
                    btn.ClearValue(BackgroundProperty);
                    btn.ClearValue(BorderBrushProperty);
                    btn.ClearValue(ForegroundProperty);
                }
            }
        }

        private void SetDefaultTeams(int count)
        {
            var names = Enumerable.Range(1, count).Select(i => $"Team {i}").ToList();
            TeamNamesEditor.Text = string.Join(Environment.NewLine, names);
        }

        private void AutoNumberTeamsButton_Click(object sender, RoutedEventArgs e)
        {
            SetDefaultTeams(_selectedTeamCount);
        }

        private void PasteRosterButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (Clipboard.ContainsText())
                {
                    var text = Clipboard.GetText();
                    var lines = text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(l => l.Trim())
                        .Where(l => l.Length > 0)
                        .Take(12)
                        .ToList();

                    if (lines.Count >= 2)
                    {
                        _selectedTeamCount = lines.Count;
                        TeamNamesEditor.Text = string.Join(Environment.NewLine, lines);
                        HighlightTeamCountButton(_selectedTeamCount);
                        SummaryInfoText.Text = $"Pasted {lines.Count} teams from clipboard.";
                    }
                    else
                    {
                        MessageBox.Show(this, "Clipboard must contain at least 2 lines for team names.", "Paste Teams", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not paste from clipboard: " + ex.Message, "Paste Teams", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void TeamNamesEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            var count = GetCurrentEnteredTeams().Count;
            RosterCountIndicator.Text = $"{count} teams configured" + (count < 2 ? " (minimum 2 required)" : count > 12 ? " (maximum 12 supported)" : string.Empty);
            RosterCountIndicator.Foreground = (count >= 2 && count <= 12) ? (Brush)new BrushConverter().ConvertFromString("#FF64748B") : Brushes.Crimson;
        }

        private List<string> GetCurrentEnteredTeams()
        {
            return TeamNamesEditor.Text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .ToList();
        }

        private void ModeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var isConduct = ModeSelector.SelectedIndex == 1;
            ConfiguredMode = isConduct ? "ConductDeck" : "LiveDesk";
            if (LaunchButton != null)
            {
                LaunchButton.Content = isConduct ? "🎙 OPEN QUIZ DECK STUDIO" : "✦ LAUNCH LIVE DESK";
            }
        }

        private void DemoQuickFillButton_Click(object sender, RoutedEventArgs e)
        {
            EventTitleInput.Text = "National Science Bowl Showcase 2026";
            var sampleTeams = new[] { "Cosmic Quarks", "Quantum Leap", "Starlight Nova", "Apex Pulse" };
            _selectedTeamCount = 4;
            TeamNamesEditor.Text = string.Join(Environment.NewLine, sampleTeams);
            HighlightTeamCountButton(4);

            RoundsCountSelector.SelectedIndex = 2; // 3 rounds
            RoundFormatSelector.SelectedIndex = 0; // Standard
            RoundDurationSelector.SelectedIndex = 2; // 2 min
            QuestionTimerSelector.SelectedIndex = 1; // 30 sec
            FirstRoundNameInput.Text = "Astrophysics & Discovery";
            SummaryInfoText.Text = "✦ Sample Championship loaded! Click LAUNCH LIVE DESK to begin.";
        }

        private void LaunchButton_Click(object sender, RoutedEventArgs e)
        {
            var title = string.IsNullOrWhiteSpace(EventTitleInput.Text) ? "Live Quiz Event" : EventTitleInput.Text.Trim();
            var teams = GetCurrentEnteredTeams();

            if (teams.Count < 2)
            {
                MessageBox.Show(this, "Please enter at least 2 teams before launching the scoring desk.", "Guided Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (teams.Count > 12)
            {
                teams = teams.Take(12).ToList();
            }
            if (teams.Distinct(StringComparer.OrdinalIgnoreCase).Count() != teams.Count)
            {
                MessageBox.Show(this, "All team names must be unique.", "Guided Setup", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ConfiguredTitle = title;
            ConfiguredTeams = teams;

            if (RoundsCountSelector.SelectedItem is ComboBoxItem roundsItem && int.TryParse(roundsItem.Content.ToString(), out var rCount))
                ConfiguredRounds = rCount;
            else
                ConfiguredRounds = 3;

            ConfiguredRoundName = string.IsNullOrWhiteSpace(FirstRoundNameInput.Text) ? "General Knowledge" : FirstRoundNameInput.Text.Trim();

            if (RoundFormatSelector.SelectedItem is ComboBoxItem formatItem)
                ConfiguredRoundFormat = formatItem.Content.ToString();

            // Round duration
            switch (RoundDurationSelector.SelectedIndex)
            {
                case 0: ConfiguredRoundDurationSeconds = 0; break; // No limit
                case 1: ConfiguredRoundDurationSeconds = 60; break;
                case 2: ConfiguredRoundDurationSeconds = 120; break;
                case 3: ConfiguredRoundDurationSeconds = 180; break;
                case 4: ConfiguredRoundDurationSeconds = 300; break;
                case 5: ConfiguredRoundDurationSeconds = 600; break;
                default: ConfiguredRoundDurationSeconds = 120; break;
            }

            // Question timer
            switch (QuestionTimerSelector.SelectedIndex)
            {
                case 0: ConfiguredQuestionDurationSeconds = 15; break;
                case 1: ConfiguredQuestionDurationSeconds = 30; break;
                case 2: ConfiguredQuestionDurationSeconds = 45; break;
                case 3: ConfiguredQuestionDurationSeconds = 60; break;
                case 4: ConfiguredQuestionDurationSeconds = 0; break;
                default: ConfiguredQuestionDurationSeconds = 30; break;
            }

            try { DialogResult = true; } catch (InvalidOperationException) { }
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            try { DialogResult = false; } catch (InvalidOperationException) { }
            Close();
        }
    }
}
