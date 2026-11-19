using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Score
{
    public partial class QuizDeckBuilderWindow : Window
    {
        private readonly ObservableCollection<QuizRound> _currentDeckRounds = new ObservableCollection<QuizRound>();

        public ObservableCollection<QuizRound> CurrentDeckRounds => _currentDeckRounds;

        public void LoadRoundsForCurrentDeck()
        {
            if (_currentDeck == null)
            {
                _currentDeckRounds.Clear();
                return;
            }

            var rounds = _db.QuizRounds
                .Where(r => r.QuizDeckId == _currentDeck.QuizDeckId)
                .OrderBy(r => r.RoundOrder)
                .ToList();

            if (rounds.Count == 0)
            {
                var defaultRound = new QuizRound
                {
                    QuizDeckId = _currentDeck.QuizDeckId,
                    RoundOrder = 1,
                    RoundTitle = "Round 1",
                    RoundType = QuizScoringEngine.RoundStandard,
                    DefaultPoints = 10,
                    DefaultBouncePoints = 5,
                    DefaultPenalty = 0,
                    DefaultTimeSeconds = 15,
                    ConferringAllowed = true,
                    InfiniteBounce = true
                };
                _db.QuizRounds.Add(defaultRound);
                _db.SaveChanges();
                rounds.Add(defaultRound);

                var orphanSlides = _db.DeckSlides
                    .Where(s => s.QuizDeckId == _currentDeck.QuizDeckId && (!s.QuizRoundId.HasValue || s.QuizRoundId.Value == 0))
                    .ToList();
                foreach (var os in orphanSlides)
                {
                    os.QuizRoundId = defaultRound.QuizRoundId;
                    if (string.IsNullOrWhiteSpace(os.RoundName)) os.RoundName = defaultRound.RoundTitle;
                }
                if (orphanSlides.Count > 0) _db.SaveChanges();
            }

            _currentDeckRounds.Clear();
            foreach (var r in rounds) _currentDeckRounds.Add(r);

            if (SlideRoundSelector != null)
            {
                SlideRoundSelector.ItemsSource = null;
                SlideRoundSelector.ItemsSource = _currentDeckRounds;
                if (_selectedSlide != null)
                {
                    LoadRoundIntoEditor(_selectedSlide);
                }
                else if (_currentDeckRounds.Count > 0)
                {
                    SlideRoundSelector.SelectedIndex = 0;
                }
            }

            if (StageRuleModeSelector != null && RoundTypeSelector != null && StageRuleModeSelector.ItemsSource == null)
            {
                StageRuleModeSelector.ItemsSource = RoundTypeSelector.ItemsSource;
            }
        }

        internal void LoadRoundIntoEditor(DeckSlide slide)
        {
            if (slide == null || SlideRoundSelector == null) return;

            QuizRound targetRound = null;
            if (slide.QuizRoundId.HasValue && slide.QuizRoundId.Value > 0)
            {
                targetRound = _currentDeckRounds.FirstOrDefault(r => r.QuizRoundId == slide.QuizRoundId.Value);
            }

            if (targetRound == null && !string.IsNullOrWhiteSpace(slide.RoundName))
            {
                targetRound = _currentDeckRounds.FirstOrDefault(r => string.Equals(r.RoundTitle, slide.RoundName, StringComparison.OrdinalIgnoreCase));
                if (targetRound != null) slide.QuizRoundId = targetRound.QuizRoundId;
            }

            if (targetRound != null)
            {
                SlideRoundSelector.SelectedItem = targetRound;
            }
            else if (_currentDeckRounds.Count > 0)
            {
                SlideRoundSelector.SelectedIndex = 0;
                slide.QuizRoundId = _currentDeckRounds[0].QuizRoundId;
                if (string.IsNullOrWhiteSpace(slide.RoundName)) slide.RoundName = _currentDeckRounds[0].RoundTitle;
            }
        }

        internal void SaveRoundFromEditor(DeckSlide slide)
        {
            if (slide == null) return;

            if (SlideRoundSelector?.SelectedItem is QuizRound round)
            {
                slide.QuizRoundId = round.QuizRoundId;
                slide.RoundName = round.RoundTitle;
            }
            else if (!string.IsNullOrWhiteSpace(SlideRoundNameEditor?.Text))
            {
                slide.RoundName = SlideRoundNameEditor.Text.Trim();
                var match = _currentDeckRounds.FirstOrDefault(r => string.Equals(r.RoundTitle, slide.RoundName, StringComparison.OrdinalIgnoreCase));
                if (match != null) slide.QuizRoundId = match.QuizRoundId;
            }
        }

        internal void ApplyCurrentRoundDefaultsToNewSlide(DeckSlide slide)
        {
            if (slide == null) return;

            var selectedRound = SlideRoundSelector?.SelectedItem as QuizRound;
            if (selectedRound == null && _currentDeckRounds.Count > 0)
            {
                if (_selectedSlide?.QuizRoundId.HasValue == true)
                    selectedRound = _currentDeckRounds.FirstOrDefault(r => r.QuizRoundId == _selectedSlide.QuizRoundId.Value);
                if (selectedRound == null) selectedRound = _currentDeckRounds[0];
            }

            if (selectedRound != null)
            {
                slide.QuizRoundId = selectedRound.QuizRoundId;
                slide.RoundName = selectedRound.RoundTitle;
                slide.RoundType = selectedRound.RoundType ?? QuizScoringEngine.RoundStandard;
                slide.Points = selectedRound.DefaultPoints;
                slide.PenaltyPoints = selectedRound.DefaultPenalty;
                slide.BonusPoints = selectedRound.DefaultBouncePoints;
                slide.TimeLimitSeconds = selectedRound.DefaultTimeSeconds;
                slide.AllowPass = selectedRound.InfiniteBounce || selectedRound.DefaultBouncePoints > 0;
            }
        }

        private void SlideRoundSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loadingEditor || _loadingDeck) return;

            if (SlideRoundSelector.SelectedItem is QuizRound round)
            {
                if (_selectedSlide != null)
                {
                    _selectedSlide.QuizRoundId = round.QuizRoundId;
                    _selectedSlide.RoundName = round.RoundTitle;
                }
                if (SlideRoundNameEditor != null) SlideRoundNameEditor.Text = round.RoundTitle;
                if (RoundTypeSelector != null && !string.IsNullOrWhiteSpace(round.RoundType))
                {
                    RoundTypeSelector.SelectedItem = round.RoundType;
                }
                UpdatePreview();
            }
        }

        private void ManageRoundsButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDeck == null) return;
            var dlg = new QuizRoundManagerDialog(this, _currentDeck, _db, _currentDeckRounds);
            dlg.ShowDialog();
            LoadRoundsForCurrentDeck();
            if (_selectedSlide != null) LoadRoundIntoEditor(_selectedSlide);
            RefreshSlideList();
        }
    }

    internal class QuizRoundManagerDialog : Window
    {
        private readonly QuizDeck _deck;
        private readonly ScoreDbContext _db;
        private readonly ObservableCollection<QuizRound> _roundsCollection;
        private ListBox _roundsListBox;
        private TextBox _titleBox;
        private ComboBox _typeCombo;
        private TextBox _pointsBox;
        private TextBox _bounceBox;
        private TextBox _penaltyBox;
        private TextBox _timeBox;
        private CheckBox _conferringCheck;
        private CheckBox _infiniteBounceCheck;

        internal QuizRoundManagerDialog(Window owner, QuizDeck deck, ScoreDbContext db, ObservableCollection<QuizRound> roundsCollection)
        {
            Owner = owner;
            _deck = deck;
            _db = db;
            _roundsCollection = roundsCollection;

            Title = $"Manage Quiz Rounds — {_deck.Title}";
            Width = 680;
            Height = 490;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Background = new SolidColorBrush(Color.FromRgb(248, 250, 252));
            ResizeMode = ResizeMode.NoResize;

            BuildUi();
            PopulateRounds();
        }

        private void BuildUi()
        {
            var root = new Grid { Margin = new Thickness(16) };
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var contentGrid = new Grid();
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(220) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(16) });
            contentGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            // Left: List
            var leftPanel = new DockPanel();
            var leftHeader = new TextBlock
            {
                Text = "DECK ROUNDS",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(0, 0, 0, 8)
            };
            DockPanel.SetDock(leftHeader, Dock.Top);
            leftPanel.Children.Add(leftHeader);

            var listButtons = new Grid { Margin = new Thickness(0, 8, 0, 0) };
            DockPanel.SetDock(listButtons, Dock.Bottom);
            listButtons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            listButtons.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var addBtn = new Button { Content = "+ Add Round", Height = 28, Margin = new Thickness(0, 0, 4, 0), FontWeight = FontWeights.SemiBold };
            addBtn.Click += AddRound_Click;
            Grid.SetColumn(addBtn, 0);
            listButtons.Children.Add(addBtn);

            var delBtn = new Button { Content = "Delete", Height = 28, Margin = new Thickness(4, 0, 0, 0), Foreground = new SolidColorBrush(Color.FromRgb(185, 28, 28)) };
            delBtn.Click += DeleteRound_Click;
            Grid.SetColumn(delBtn, 1);
            listButtons.Children.Add(delBtn);
            leftPanel.Children.Add(listButtons);

            _roundsListBox = new ListBox
            {
                DisplayMemberPath = "RoundTitle",
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                Background = Brushes.White
            };
            _roundsListBox.SelectionChanged += RoundsListBox_SelectionChanged;
            leftPanel.Children.Add(_roundsListBox);
            Grid.SetColumn(leftPanel, 0);
            contentGrid.Children.Add(leftPanel);

            // Right: Form
            var formBorder = new Border
            {
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(16)
            };

            var formStack = new StackPanel();
            var formHeader = new TextBlock
            {
                Text = "ROUND CONFIGURATION & DEFAULTS",
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(100, 116, 139)),
                Margin = new Thickness(0, 0, 0, 12)
            };
            formStack.Children.Add(formHeader);

            formStack.Children.Add(new TextBlock { Text = "Round Title", FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
            _titleBox = new TextBox { Height = 28, Margin = new Thickness(0, 0, 0, 10), VerticalContentAlignment = VerticalAlignment.Center, Padding = new Thickness(6, 0, 6, 0) };
            formStack.Children.Add(_titleBox);

            formStack.Children.Add(new TextBlock { Text = "Round Type / Conducting Mode", FontSize = 11, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 4) });
            _typeCombo = new ComboBox
            {
                Height = 28,
                Margin = new Thickness(0, 0, 0, 10),
                ItemsSource = new[]
                {
                    QuizScoringEngine.RoundStandard,
                    QuizScoringEngine.RoundPassBounce,
                    QuizScoringEngine.RoundBuzzer,
                    QuizScoringEngine.RoundRapidFire,
                    QuizScoringEngine.RoundTimed,
                    QuizScoringEngine.RoundVideo,
                    QuizScoringEngine.RoundImage,
                    QuizScoringEngine.RoundMixBag
                }
            };
            formStack.Children.Add(_typeCombo);

            var numGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            for (var i = 0; i < 4; i++) numGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            _pointsBox = CreateNumericInput(numGrid, 0, "Default Pts", "10");
            _bounceBox = CreateNumericInput(numGrid, 1, "Bounce Pts", "5");
            _penaltyBox = CreateNumericInput(numGrid, 2, "Penalty", "0");
            _timeBox = CreateNumericInput(numGrid, 3, "Time (s)", "15");
            formStack.Children.Add(numGrid);

            var checksPanel = new WrapPanel { Margin = new Thickness(0, 0, 0, 14) };
            _conferringCheck = new CheckBox { Content = "Conferring Allowed", IsChecked = true, Margin = new Thickness(0, 0, 16, 0), FontSize = 11.5 };
            _infiniteBounceCheck = new CheckBox { Content = "Infinite Bounce", IsChecked = true, FontSize = 11.5 };
            checksPanel.Children.Add(_conferringCheck);
            checksPanel.Children.Add(_infiniteBounceCheck);
            formStack.Children.Add(checksPanel);

            var saveRoundBtn = new Button
            {
                Content = "Save Round Details",
                Height = 30,
                FontWeight = FontWeights.SemiBold,
                Background = new SolidColorBrush(Color.FromRgb(37, 99, 235)),
                Foreground = Brushes.White,
                BorderThickness = new Thickness(0)
            };
            saveRoundBtn.Click += SaveRoundDetails_Click;
            formStack.Children.Add(saveRoundBtn);

            formBorder.Child = formStack;
            Grid.SetColumn(formBorder, 2);
            contentGrid.Children.Add(formBorder);

            Grid.SetRow(contentGrid, 0);
            root.Children.Add(contentGrid);

            // Bottom Close
            var closeBtn = new Button { Content = "Done", Width = 90, Height = 28, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 12, 0, 0) };
            closeBtn.Click += (s, e) => Close();
            Grid.SetRow(closeBtn, 1);
            root.Children.Add(closeBtn);

            Content = root;
        }

        private static TextBox CreateNumericInput(Grid parent, int col, string label, string defaultValue)
        {
            var sp = new StackPanel { Margin = new Thickness(col == 0 ? 0 : 4, 0, col == 3 ? 0 : 4, 0) };
            sp.Children.Add(new TextBlock { Text = label, FontSize = 10.5, FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 0, 0, 3) });
            var tb = new TextBox { Text = defaultValue, Height = 28, TextAlignment = TextAlignment.Center, VerticalContentAlignment = VerticalAlignment.Center };
            sp.Children.Add(tb);
            Grid.SetColumn(sp, col);
            parent.Children.Add(sp);
            return tb;
        }

        private void PopulateRounds()
        {
            var rounds = _db.QuizRounds.Where(r => r.QuizDeckId == _deck.QuizDeckId).OrderBy(r => r.RoundOrder).ToList();
            _roundsListBox.ItemsSource = rounds;
            if (rounds.Count > 0) _roundsListBox.SelectedIndex = 0;
        }

        private void RoundsListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_roundsListBox.SelectedItem is QuizRound round)
            {
                _titleBox.Text = round.RoundTitle ?? string.Empty;
                _typeCombo.SelectedItem = round.RoundType ?? QuizScoringEngine.RoundStandard;
                _pointsBox.Text = round.DefaultPoints.ToString(CultureInfo.InvariantCulture);
                _bounceBox.Text = round.DefaultBouncePoints.ToString(CultureInfo.InvariantCulture);
                _penaltyBox.Text = round.DefaultPenalty.ToString(CultureInfo.InvariantCulture);
                _timeBox.Text = round.DefaultTimeSeconds.ToString(CultureInfo.InvariantCulture);
                _conferringCheck.IsChecked = round.ConferringAllowed;
                _infiniteBounceCheck.IsChecked = round.InfiniteBounce;
            }
        }

        private void SaveRoundDetails_Click(object sender, RoutedEventArgs e)
        {
            if (!(_roundsListBox.SelectedItem is QuizRound round)) return;

            round.RoundTitle = string.IsNullOrWhiteSpace(_titleBox.Text) ? "Round " + round.RoundOrder : _titleBox.Text.Trim();
            round.RoundType = _typeCombo.SelectedItem?.ToString() ?? QuizScoringEngine.RoundStandard;
            if (int.TryParse(_pointsBox.Text, out var p)) round.DefaultPoints = p;
            if (int.TryParse(_bounceBox.Text, out var b)) round.DefaultBouncePoints = b;
            if (int.TryParse(_penaltyBox.Text, out var pen)) round.DefaultPenalty = pen;
            if (int.TryParse(_timeBox.Text, out var t)) round.DefaultTimeSeconds = t;
            round.ConferringAllowed = _conferringCheck.IsChecked == true;
            round.InfiniteBounce = _infiniteBounceCheck.IsChecked == true;

            _db.SaveChanges();
            var currentIdx = _roundsListBox.SelectedIndex;
            PopulateRounds();
            _roundsListBox.SelectedIndex = currentIdx;
            MessageBox.Show(this, $"Round '{round.RoundTitle}' updated.", "Round Saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AddRound_Click(object sender, RoutedEventArgs e)
        {
            var existing = _db.QuizRounds.Where(r => r.QuizDeckId == _deck.QuizDeckId).ToList();
            var nextOrder = (existing.Count > 0 ? existing.Max(r => r.RoundOrder) : 0) + 1;

            var newRound = new QuizRound
            {
                QuizDeckId = _deck.QuizDeckId,
                RoundOrder = nextOrder,
                RoundTitle = $"Round {nextOrder}",
                RoundType = QuizScoringEngine.RoundStandard,
                DefaultPoints = 10,
                DefaultBouncePoints = 5,
                DefaultPenalty = 0,
                DefaultTimeSeconds = 15,
                ConferringAllowed = true,
                InfiniteBounce = true
            };

            _db.QuizRounds.Add(newRound);
            _db.SaveChanges();
            PopulateRounds();
            _roundsListBox.SelectedItem = newRound;
        }

        private void DeleteRound_Click(object sender, RoutedEventArgs e)
        {
            if (!(_roundsListBox.SelectedItem is QuizRound round)) return;

            var count = _db.QuizRounds.Count(r => r.QuizDeckId == _deck.QuizDeckId);
            if (count <= 1)
            {
                MessageBox.Show(this, "Every quiz deck must have at least one round.", "Cannot Delete", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(this, $"Are you sure you want to delete '{round.RoundTitle}'?", "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;

            _db.QuizRounds.Remove(round);
            _db.SaveChanges();

            // Reassign orphaned slides to the first remaining round
            var remaining = _db.QuizRounds.Where(r => r.QuizDeckId == _deck.QuizDeckId).OrderBy(r => r.RoundOrder).ToList();
            if (remaining.Count > 0)
            {
                var orphanedSlides = _db.DeckSlides.Where(s => s.QuizDeckId == _deck.QuizDeckId && s.QuizRoundId == round.QuizRoundId).ToList();
                foreach (var s in orphanedSlides)
                {
                    s.QuizRoundId = remaining[0].QuizRoundId;
                    s.RoundName = remaining[0].RoundTitle;
                }
                _db.SaveChanges();
            }

            PopulateRounds();
        }
    }
}
