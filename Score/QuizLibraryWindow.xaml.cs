using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Score
{
    public class QuizDeckItemViewModel
    {
        public int QuizDeckId { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public string Theme { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastConductedAt { get; set; }
        public int QuestionCount { get; set; }
        public int TotalPoints { get; set; }
        public int SessionCount { get; set; }
        public bool HasActiveSession { get; set; }
        public string ActiveSessionStatus { get; set; }
    }

    public partial class QuizLibraryWindow : Window
    {
        private const int PageSize = 6;
        private List<QuizDeckItemViewModel> _allDecks = new List<QuizDeckItemViewModel>();
        private List<QuizDeckItemViewModel> _filteredDecks = new List<QuizDeckItemViewModel>();
        private int _currentPage = 1;
        private int _totalPages = 1;

        private bool _isLoaded;

        public int? SelectedDeckId { get; private set; }

        public QuizLibraryWindow()
        {
            InitializeComponent();
            Loaded += QuizLibraryWindow_Loaded;
        }

        private void QuizLibraryWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _isLoaded = true;
            LoadDecksAndRefresh();
        }

        private void LoadDecksAndRefresh()
        {
            try
            {
                using (var db = new ScoreDbContext())
                {
                    if (!db.QuizDecks.Any(d => d.IsActive))
                    {
                        QuizSampleDeckSeeder.EnsureSampleDeck(db);
                    }

                    var decks = db.QuizDecks.Where(d => d.IsActive).ToList();
                    var deckIds = decks.Select(d => d.QuizDeckId).ToList();

                    var slidesData = db.DeckSlides
                        .Where(s => deckIds.Contains(s.QuizDeckId) && s.IsActive)
                        .Select(s => new { s.QuizDeckId, s.Points })
                        .ToList();

                    var sessionsData = db.QuizDeckSessions
                        .Where(s => deckIds.Contains(s.QuizDeckId))
                        .Select(s => new { s.QuizDeckId, s.QuizDeckSessionId, s.Status, s.StartedAtUtc, s.CompletedAtUtc })
                        .ToList();

                    var slideStats = slidesData
                        .GroupBy(s => s.QuizDeckId)
                        .ToDictionary(g => g.Key, g => new { Count = g.Count(), TotalPoints = g.Sum(x => x.Points) });

                    var sessionStats = sessionsData
                        .GroupBy(s => s.QuizDeckId)
                        .ToDictionary(g => g.Key, g => new
                        {
                            TotalCount = g.Count(),
                            LastRun = g.Max(s => s.CompletedAtUtc ?? s.StartedAtUtc),
                            Active = g.Where(s => s.Status == "Running" || s.Status == "Paused")
                                      .OrderByDescending(s => s.QuizDeckSessionId)
                                      .FirstOrDefault()
                        });

                    _allDecks = decks.Select(d =>
                    {
                        slideStats.TryGetValue(d.QuizDeckId, out var slideInfo);
                        sessionStats.TryGetValue(d.QuizDeckId, out var sessionInfo);

                        return new QuizDeckItemViewModel
                        {
                            QuizDeckId = d.QuizDeckId,
                            Title = string.IsNullOrWhiteSpace(d.Title) ? "Untitled Quiz" : d.Title,
                            Description = d.Description,
                            Theme = d.Theme ?? "Classic",
                            CreatedAt = d.CreatedAtUtc,
                            LastConductedAt = sessionInfo?.LastRun,
                            QuestionCount = slideInfo?.Count ?? 0,
                            TotalPoints = slideInfo?.TotalPoints ?? 0,
                            SessionCount = sessionInfo?.TotalCount ?? 0,
                            HasActiveSession = sessionInfo?.Active != null,
                            ActiveSessionStatus = sessionInfo?.Active?.Status
                        };
                    }).ToList();
                }

                ApplyFilterAndSort();
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Failed to load quiz decks for QuizLibraryWindow.", ex);
            }
        }

        private void ApplyFilterAndSort()
        {
            var query = _allDecks.AsEnumerable();

            var search = SearchBox?.Text?.Trim();
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(d =>
                    (d.Title != null && d.Title.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (d.Description != null && d.Description.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (d.Theme != null && d.Theme.IndexOf(search, StringComparison.OrdinalIgnoreCase) >= 0));
            }

            var sortIndex = SortSelector?.SelectedIndex ?? 0;
            switch (sortIndex)
            {
                case 0:
                    query = query.OrderBy(d => d.Title, StringComparer.OrdinalIgnoreCase);
                    break;
                case 1:
                    query = query.OrderByDescending(d => d.Title, StringComparer.OrdinalIgnoreCase);
                    break;
                case 2:
                    query = query.OrderByDescending(d => d.CreatedAt);
                    break;
                case 3:
                    query = query.OrderBy(d => d.CreatedAt);
                    break;
                case 4:
                    query = query.OrderByDescending(d => d.QuestionCount).ThenBy(d => d.Title);
                    break;
                case 5:
                    query = query.OrderByDescending(d => d.LastConductedAt ?? DateTime.MinValue).ThenByDescending(d => d.CreatedAt);
                    break;
                default:
                    query = query.OrderBy(d => d.Title);
                    break;
            }

            _filteredDecks = query.ToList();
            _currentPage = 1;
            RenderCurrentPage();
        }

        private void RenderCurrentPage()
        {
            if (!_isLoaded || PrevPageButton == null || NextPageButton == null || PageInfoText == null || DeckCardsPanel == null) return;

            _totalPages = Math.Max(1, (int)Math.Ceiling(_filteredDecks.Count / (double)PageSize));
            if (_currentPage > _totalPages) _currentPage = _totalPages;
            if (_currentPage < 1) _currentPage = 1;

            PrevPageButton.IsEnabled = _currentPage > 1;
            NextPageButton.IsEnabled = _currentPage < _totalPages;
            PageInfoText.Text = $"Page {_currentPage} of {_totalPages}";

            if (_filteredDecks.Count == 0)
            {
                EmptyStatePanel.Visibility = Visibility.Visible;
                DeckCardsPanel.Children.Clear();
                SummaryInfoText.Text = string.IsNullOrEmpty(SearchBox?.Text)
                    ? "No quiz decks in library"
                    : $"No quizzes match \"{SearchBox.Text}\"";
                return;
            }

            EmptyStatePanel.Visibility = Visibility.Collapsed;
            DeckCardsPanel.Children.Clear();

            var pagedItems = _filteredDecks.Skip((_currentPage - 1) * PageSize).Take(PageSize).ToList();
            SummaryInfoText.Text = $"Showing {(_currentPage - 1) * PageSize + 1}–{Math.Min(_currentPage * PageSize, _filteredDecks.Count)} of {_filteredDecks.Count} quizzes";

            foreach (var item in pagedItems)
            {
                DeckCardsPanel.Children.Add(CreateDeckCard(item));
            }
        }

        private Border CreateDeckCard(QuizDeckItemViewModel item)
        {
            var brushConv = new BrushConverter();
            var card = new Border
            {
                Width = 320,
                Margin = new Thickness(10),
                Background = Brushes.White,
                BorderBrush = (Brush)brushConv.ConvertFromString(item.HasActiveSession ? "#F59E0B" : "#E2E8F0"),
                BorderThickness = new Thickness(item.HasActiveSession ? 2 : 1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16, 14, 16, 14),
                Effect = (System.Windows.Media.Effects.Effect)FindResource("PanelGlowEffect"),
                Cursor = Cursors.Hand
            };

            var stack = new StackPanel();

            var headerRow = new DockPanel { LastChildFill = true };
            var statusPill = new Border
            {
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center
            };
            DockPanel.SetDock(statusPill, Dock.Right);

            if (item.HasActiveSession)
            {
                statusPill.Background = (Brush)brushConv.ConvertFromString("#FEF3C7");
                statusPill.BorderBrush = (Brush)brushConv.ConvertFromString("#F59E0B");
                statusPill.BorderThickness = new Thickness(1);
                statusPill.Child = new TextBlock
                {
                    Text = item.ActiveSessionStatus == "Paused" ? "⏸ PAUSED" : "● LIVE",
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)brushConv.ConvertFromString("#B45309")
                };
            }
            else if (item.SessionCount > 0)
            {
                statusPill.Background = (Brush)brushConv.ConvertFromString("#F1F5F9");
                statusPill.Child = new TextBlock
                {
                    Text = $"{item.SessionCount} RUNS",
                    FontSize = 10,
                    FontWeight = FontWeights.SemiBold,
                    Foreground = (Brush)brushConv.ConvertFromString("#64748B")
                };
            }
            else
            {
                statusPill.Background = (Brush)brushConv.ConvertFromString("#ECFDF5");
                statusPill.Child = new TextBlock
                {
                    Text = "READY",
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)brushConv.ConvertFromString("#059669")
                };
            }
            headerRow.Children.Add(statusPill);

            var titleBlock = new TextBlock
            {
                Text = item.Title,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)brushConv.ConvertFromString("#0F172A"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = item.Title
            };
            headerRow.Children.Add(titleBlock);
            stack.Children.Add(headerRow);

            var tagsRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 7, 0, 7) };
            var themeBadge = new Border
            {
                Background = (Brush)brushConv.ConvertFromString("#EEF2FF"),
                BorderBrush = (Brush)brushConv.ConvertFromString("#C7D2FE"),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 1, 6, 1),
                Margin = new Thickness(0, 0, 8, 0)
            };
            themeBadge.Child = new TextBlock
            {
                Text = (item.Theme ?? "Classic").ToUpperInvariant(),
                FontSize = 9,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)brushConv.ConvertFromString("#4338CA")
            };
            tagsRow.Children.Add(themeBadge);

            var statsText = new TextBlock
            {
                Text = $"{item.QuestionCount} Questions  •  {item.TotalPoints} Pts",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)brushConv.ConvertFromString("#475569"),
                VerticalAlignment = VerticalAlignment.Center
            };
            tagsRow.Children.Add(statsText);
            stack.Children.Add(tagsRow);

            var descBlock = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(item.Description) ? "No description provided." : item.Description,
                FontSize = 11,
                Foreground = (Brush)brushConv.ConvertFromString(string.IsNullOrWhiteSpace(item.Description) ? "#94A3B8" : "#64748B"),
                TextTrimming = TextTrimming.CharacterEllipsis,
                TextWrapping = TextWrapping.Wrap,
                Height = 32,
                Margin = new Thickness(0, 2, 0, 6)
            };
            stack.Children.Add(descBlock);

            stack.Children.Add(new Border
            {
                Height = 1,
                Background = (Brush)brushConv.ConvertFromString("#F1F5F9"),
                Margin = new Thickness(0, 4, 0, 8)
            });

            var timestampsGrid = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            timestampsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            timestampsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var createdText = new TextBlock
            {
                Text = $"Created: {item.CreatedAt.ToLocalTime():dd MMM yyyy}",
                FontSize = 10,
                Foreground = (Brush)brushConv.ConvertFromString("#94A3B8")
            };
            Grid.SetColumn(createdText, 0);
            timestampsGrid.Children.Add(createdText);

            var lastRunText = new TextBlock
            {
                Text = item.LastConductedAt.HasValue
                    ? $"Last run: {item.LastConductedAt.Value.ToLocalTime():dd MMM yyyy}"
                    : "Never conducted",
                FontSize = 10,
                Foreground = (Brush)brushConv.ConvertFromString(item.LastConductedAt.HasValue ? "#64748B" : "#94A3B8"),
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetColumn(lastRunText, 1);
            timestampsGrid.Children.Add(lastRunText);
            stack.Children.Add(timestampsGrid);

            var buttonsGrid = new Grid();
            buttonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.3, GridUnitType.Star) });
            buttonsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var conductButton = new Button
            {
                Height = 34,
                Margin = new Thickness(0, 0, 6, 0),
                FontSize = 11,
                FontWeight = FontWeights.Bold,
                Cursor = Cursors.Hand
            };

            if (item.HasActiveSession)
            {
                conductButton.Content = "▶ RESUME";
                conductButton.Background = (Brush)brushConv.ConvertFromString("#F59E0B");
                conductButton.BorderBrush = (Brush)brushConv.ConvertFromString("#D97706");
                conductButton.Foreground = Brushes.White;
                conductButton.ToolTip = "Resume the active session for this quiz";
            }
            else
            {
                conductButton.Content = "▶ CONDUCT";
                conductButton.Background = (Brush)brushConv.ConvertFromString("#16A34A");
                conductButton.BorderBrush = (Brush)brushConv.ConvertFromString("#15803D");
                conductButton.Foreground = Brushes.White;
                conductButton.ToolTip = "Conduct this quiz live with mobile buzzers & stage view";
            }

            int deckId = item.QuizDeckId;
            conductButton.Click += (s, e) =>
            {
                SelectedDeckId = deckId;
                DialogResult = true;
                Close();
            };
            Grid.SetColumn(conductButton, 0);
            buttonsGrid.Children.Add(conductButton);

            var editButton = new Button
            {
                Height = 34,
                Content = "✏ EDIT",
                Margin = new Thickness(6, 0, 0, 0),
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Background = (Brush)brushConv.ConvertFromString("#F8FAFC"),
                BorderBrush = (Brush)brushConv.ConvertFromString("#CBD5E1"),
                Foreground = (Brush)brushConv.ConvertFromString("#1E293B"),
                Cursor = Cursors.Hand,
                ToolTip = "Edit questions and slides in Quiz Maker"
            };
            editButton.Click += (s, e) =>
            {
                var builder = new QuizDeckBuilderWindow(deckId) { Owner = this };
                builder.ShowDialog();
                LoadDecksAndRefresh();
            };
            Grid.SetColumn(editButton, 1);
            buttonsGrid.Children.Add(editButton);

            stack.Children.Add(buttonsGrid);

            var preflightButton = new Button
            {
                Height = 28,
                Margin = new Thickness(0, 6, 0, 0),
                Content = "🔍 PRE-FLIGHT CHECK",
                FontSize = 10.5,
                FontWeight = FontWeights.SemiBold,
                Background = (Brush)brushConv.ConvertFromString("#F8FAFC"),
                BorderBrush = (Brush)brushConv.ConvertFromString("#CBD5E1"),
                Foreground = (Brush)brushConv.ConvertFromString("#334155"),
                Cursor = Cursors.Hand,
                ToolTip = "Run stage pre-flight readiness check for missing keys, choices, broken media & timers"
            };
            preflightButton.Click += (s, e) => RunPreflightCheck(deckId);
            stack.Children.Add(preflightButton);

            var cardActionsGrid = new Grid { Margin = new Thickness(0, 6, 0, 0) };
            cardActionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cardActionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            cardActionsGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

            var previewBtn = new Button
            {
                Height = 26,
                Content = "👁 PREVIEW",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 3, 0),
                Background = (Brush)brushConv.ConvertFromString("#F8FAFC"),
                BorderBrush = (Brush)brushConv.ConvertFromString("#CBD5E1"),
                Foreground = (Brush)brushConv.ConvertFromString("#334155"),
                ToolTip = "Preview all questions and answers in this quiz"
            };
            previewBtn.Click += (s, e) => PreviewDeck(deckId, item.Title);
            Grid.SetColumn(previewBtn, 0);
            cardActionsGrid.Children.Add(previewBtn);

            var cloneBtn = new Button
            {
                Height = 26,
                Content = "📋 CLONE",
                FontSize = 10,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(3, 0, 3, 0),
                Background = (Brush)brushConv.ConvertFromString("#F8FAFC"),
                BorderBrush = (Brush)brushConv.ConvertFromString("#CBD5E1"),
                Foreground = (Brush)brushConv.ConvertFromString("#334155"),
                ToolTip = "Duplicate this quiz deck"
            };
            cloneBtn.Click += (s, e) => DuplicateDeck(deckId, item.Title);
            Grid.SetColumn(cloneBtn, 1);
            cardActionsGrid.Children.Add(cloneBtn);

            var deleteBtn = new Button
            {
                Height = 26,
                Content = "🗑 DELETE",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(3, 0, 0, 0),
                Background = (Brush)brushConv.ConvertFromString("#FEF2F2"),
                BorderBrush = (Brush)brushConv.ConvertFromString("#FECACA"),
                Foreground = (Brush)brushConv.ConvertFromString("#DC2626"),
                ToolTip = "Permanently delete this quiz and its questions"
            };
            deleteBtn.Click += (s, e) => DeleteDeck(deckId, item.Title);
            Grid.SetColumn(deleteBtn, 2);
            cardActionsGrid.Children.Add(deleteBtn);

            stack.Children.Add(cardActionsGrid);

            card.Child = stack;

            card.MouseEnter += (s, e) => card.BorderBrush = (Brush)brushConv.ConvertFromString("#3B82F6");
            card.MouseLeave += (s, e) => card.BorderBrush = (Brush)brushConv.ConvertFromString(item.HasActiveSession ? "#F59E0B" : "#E2E8F0");
            card.MouseLeftButtonDown += (s, e) =>
            {
                if (e.ClickCount == 2)
                {
                    SelectedDeckId = deckId;
                    DialogResult = true;
                    Close();
                }
            };

            return card;
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (SearchPlaceholder != null)
            {
                SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                    ? Visibility.Visible
                    : Visibility.Collapsed;
            }
            ApplyFilterAndSort();
        }

        private void SortSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            ApplyFilterAndSort();
        }

        private void PrevPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage > 1)
            {
                _currentPage--;
                RenderCurrentPage();
            }
        }

        private void NextPageButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;
                RenderCurrentPage();
            }
        }

        private void CreateNewButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var db = new ScoreDbContext())
                {
                    var newDeck = new QuizDeck
                    {
                        Title = "New Quiz - " + DateTime.Now.ToString("dd MMM yyyy"),
                        Theme = "Classic",
                        CreatedAtUtc = DateTime.UtcNow,
                        IsActive = true
                    };
                    db.QuizDecks.Add(newDeck);
                    db.SaveChanges();

                    var starterSlide = new DeckSlide
                    {
                        QuizDeckId = newDeck.QuizDeckId,
                        SlideType = "Question",
                        QuestionType = QuizScoringEngine.QuestionMultipleChoice,
                        RoundType = QuizScoringEngine.RoundStandard,
                        SlideOrder = 1,
                        Title = "Question 1",
                        ContentText = "Enter your question here...",
                        CorrectAnswer = "Option 1",
                        OptionsJson = "Option 1\nOption 2\nOption 3\nOption 4",
                        Points = 10,
                        PenaltyPoints = 0,
                        TimeLimitSeconds = 30,
                        IsActive = true
                    };
                    db.DeckSlides.Add(starterSlide);
                    db.SaveChanges();

                    var builder = new QuizDeckBuilderWindow(newDeck.QuizDeckId) { Owner = this };
                    builder.ShowDialog();
                }

                LoadDecksAndRefresh();
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Failed to create new quiz deck.", ex);
                MessageBox.Show(this, "Could not create new quiz deck: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RunPreflightCheck(int deckId)
        {
            try
            {
                var report = QuizPreflightValidator.ValidateDeck(deckId);
                var dlg = new PreflightReportDialog(report) { Owner = this };
                dlg.ShowDialog();

                if (dlg.WantsConduct)
                {
                    SelectedDeckId = deckId;
                    DialogResult = true;
                    Close();
                }
                else if (dlg.WantsEdit)
                {
                    var builder = new QuizDeckBuilderWindow(deckId) { Owner = this };
                    builder.ShowDialog();
                    LoadDecksAndRefresh();
                }
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException($"Failed to run pre-flight check for deck #{deckId}.", ex);
                MessageBox.Show(this, "Pre-flight validation failed: " + ex.Message, "Pre-Flight Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PreflightCheckToolbarButton_Click(object sender, RoutedEventArgs e)
        {
            if (_filteredDecks == null || _filteredDecks.Count == 0)
            {
                MessageBox.Show(this, "No quiz decks available to validate. Please create or import a quiz deck first.", "Pre-Flight Validation", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var targetDeck = _filteredDecks[0];
            RunPreflightCheck(targetDeck.QuizDeckId);
        }

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                DialogResult = false;
                Close();
                e.Handled = true;
            }
        }
    }
}
