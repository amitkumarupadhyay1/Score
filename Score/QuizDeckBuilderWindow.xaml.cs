using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Score
{
    public partial class QuizDeckBuilderWindow : Window
    {
        private readonly ScoreDbContext _db = new ScoreDbContext();
        private QuizDeck _currentDeck;
        private ObservableCollection<DeckSlide> _slides;
        private DeckSlide _selectedSlide;
        private bool _loadingDeck;
        private bool _loadingEditor;
        private bool _syncingOptions;
        private int? _initialDeckId;

        public QuizDeckBuilderWindow()
        {
            InitializeComponent();
            Loaded += QuizDeckBuilderWindow_Loaded;
            InitializeThemeSelector();
            InitializeSlideTypeSelector();
        }

        public QuizDeckBuilderWindow(int deckId) : this()
        {
            _initialDeckId = deckId;
        }

        private void QuizDeckBuilderWindow_Loaded(object sender, RoutedEventArgs e)
        {
            ApplyWorkAreaConstraint();
            LoadOrCreateDeck();
            RefreshSlideList();
        }

        private void ApplyWorkAreaConstraint()
        {
            try
            {
                var workArea = SystemParameters.WorkArea;
                Width = Math.Min(1280, workArea.Width - 40);
                Height = Math.Min(740, workArea.Height - 40);
                Left = Math.Max(workArea.Left, (workArea.Width - Width) / 2 + workArea.Left);
                Top = Math.Max(workArea.Top, (workArea.Height - Height) / 2 + workArea.Top);

                if (workArea.Height < 860 || workArea.Width < 1366)
                {
                    WindowState = WindowState.Maximized;
                }
                else
                {
                    WindowState = WindowState.Normal;
                }
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Failed to apply work area constraint to QuizDeckBuilderWindow.", ex);
            }
        }

        private void InitializeThemeSelector()
        {
            DeckThemeSelector.Items.Clear();
            DeckThemeSelector.Items.Add("Stage");
            DeckThemeSelector.Items.Add("School");
            DeckThemeSelector.Items.Add("Corporate");
            DeckThemeSelector.Items.Add("Broadcast");
            DeckThemeSelector.Items.Add("Classic");
            DeckThemeSelector.Items.Add("Neon");
            DeckThemeSelector.SelectedIndex = 0;

            QuestionTypeSelector.ItemsSource = new[]
            {
                QuizScoringEngine.QuestionMultipleChoice,
                QuizScoringEngine.QuestionMultipleSelect,
                QuizScoringEngine.QuestionTrueFalse,
                QuizScoringEngine.QuestionShortAnswer,
                QuizScoringEngine.QuestionNumeric,
                QuizScoringEngine.QuestionSequence,
                QuizScoringEngine.QuestionManual
            };
            QuestionTypeSelector.SelectedIndex = 0;

            RoundTypeSelector.ItemsSource = new[]
            {
                QuizScoringEngine.RoundStandard,
                QuizScoringEngine.RoundPassBounce,
                QuizScoringEngine.RoundBuzzer,
                QuizScoringEngine.RoundRapidFire,
                QuizScoringEngine.RoundTimed,
                QuizScoringEngine.RoundVideo,
                QuizScoringEngine.RoundImage,
                QuizScoringEngine.RoundMixBag
            };
            RoundTypeSelector.SelectedIndex = 0;

            MediaDisplayModeSelector.Items.Clear();
            MediaDisplayModeSelector.Items.Add("Auto / Split");
            MediaDisplayModeSelector.Items.Add("Fullscreen Clue");
            MediaDisplayModeSelector.Items.Add("Audio Only");
            MediaDisplayModeSelector.SelectedIndex = 0;
        }

        private void InitializeSlideTypeSelector()
        {
            SlideTypeSelector.Items.Clear();
            SlideTypeSelector.Items.Add("Question");
            SlideTypeSelector.Items.Add("Instruction");
            SlideTypeSelector.Items.Add("Media");
            SlideTypeSelector.Items.Add("Timer");
            SlideTypeSelector.Items.Add("Scoreboard");
            SlideTypeSelector.SelectedIndex = 0;
        }

        private void LoadOrCreateDeck()
        {
            var decks = _db.QuizDecks.Where(d => d.IsActive).OrderBy(d => d.Title).ToList();
            if (decks.Count == 0)
            {
                var blankDeck = new QuizDeck
                {
                    Title = "New Quiz Deck",
                    Theme = "Classic",
                    CreatedAtUtc = DateTime.UtcNow,
                    IsActive = true
                };
                _db.QuizDecks.Add(blankDeck);
                _db.SaveChanges();
                decks = new List<QuizDeck> { blankDeck };
            }

            var targetDeck = _initialDeckId.HasValue
                ? decks.FirstOrDefault(d => d.QuizDeckId == _initialDeckId.Value) ?? decks[0]
                : decks[0];

            _loadingDeck = true;
            DeckSelector.ItemsSource = decks;
            DeckSelector.SelectedItem = targetDeck;
            _loadingDeck = false;
            LoadSelectedDeck(targetDeck);
        }

        private void LoadSelectedDeck(QuizDeck deck)
        {
            _currentDeck = deck;
            DeckTitleEditor.Text = _currentDeck.Title ?? string.Empty;
            var currentTheme = _currentDeck.Theme ?? "Stage";
            if (!DeckThemeSelector.Items.Contains(currentTheme)) DeckThemeSelector.Items.Add(currentTheme);
            DeckThemeSelector.SelectedItem = currentTheme;
            ApplyThemeToSimulator(currentTheme);

            var slidesList = _db.DeckSlides.Where(s => s.QuizDeckId == _currentDeck.QuizDeckId && s.IsActive)
                .OrderBy(s => s.SlideOrder)
                .ToList();

            _slides = new ObservableCollection<DeckSlide>(slidesList);
            SlideListBox.ItemsSource = _slides;
            _selectedSlide = null;
            RefreshSlideList();
            UpdateLockBanner();
        }



        private void DeckSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loadingDeck || !(DeckSelector.SelectedItem is QuizDeck deck) || deck == _currentDeck) return;
            if (_currentDeck != null && !HasActiveSession(_currentDeck.QuizDeckId))
            {
                if (!SaveCurrentDeck())
                {
                    _loadingDeck = true;
                    DeckSelector.SelectedValue = _currentDeck.QuizDeckId;
                    _loadingDeck = false;
                    return;
                }
            }
            LoadSelectedDeck(deck);
        }

        private void RefreshDeckSelector(QuizDeck selectedDeck)
        {
            _loadingDeck = true;
            DeckSelector.ItemsSource = _db.QuizDecks.Where(d => d.IsActive).OrderBy(d => d.Title).ToList();
            DeckSelector.SelectedValue = selectedDeck.QuizDeckId;
            _loadingDeck = false;
        }

        private void RefreshSlideList()
        {
            var selectedId = _selectedSlide?.DeckSlideId;
            SlideListBox.ItemsSource = null;
            SlideListBox.ItemsSource = _slides;

            var totalCount = _slides?.Count ?? 0;
            var qCount = _slides?.Count(s => string.Equals(s.SlideType, "Question", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(s.SlideType)) ?? 0;
            var instrCount = _slides?.Count(s => string.Equals(s.SlideType, "Instruction", StringComparison.OrdinalIgnoreCase)) ?? 0;
            var mediaCount = _slides?.Count(s => string.Equals(s.SlideType, "Media", StringComparison.OrdinalIgnoreCase)) ?? 0;
            var timerCount = _slides?.Count(s => string.Equals(s.SlideType, "Timer", StringComparison.OrdinalIgnoreCase)) ?? 0;
            var scoreCount = _slides?.Count(s => string.Equals(s.SlideType, "Scoreboard", StringComparison.OrdinalIgnoreCase)) ?? 0;
            var totalPts = _slides?.Sum(s => s.Points) ?? 0;

            if (totalCount == 0)
            {
                SlideCountLabel.Text = "0 slides · 0 pts";
            }
            else if (qCount == totalCount)
            {
                SlideCountLabel.Text = $"{totalCount} question{(totalCount == 1 ? "" : "s")} · {totalPts} pts";
            }
            else
            {
                var parts = new List<string>();
                if (qCount > 0) parts.Add($"{qCount} Qs");
                if (instrCount > 0) parts.Add($"{instrCount} Rules");
                if (mediaCount > 0) parts.Add($"{mediaCount} Media");
                if (timerCount > 0) parts.Add($"{timerCount} Timers");
                if (scoreCount > 0) parts.Add($"{scoreCount} Boards");
                SlideCountLabel.Text = $"{totalCount} slides ({string.Join(", ", parts)}) · {totalPts} pts";
            }
            EmptySlideListCard.Visibility = totalCount == 0 ? Visibility.Visible : Visibility.Collapsed;

            var selectedIndex = selectedId.HasValue
                ? _slides.ToList().FindIndex(slide => slide.DeckSlideId == selectedId.Value)
                : -1;
            SlideListBox.SelectedIndex = selectedIndex >= 0 ? selectedIndex : (_slides.Count > 0 ? 0 : -1);
        }

        private void NewDeckButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDeck != null && !HasActiveSession(_currentDeck.QuizDeckId))
            {
                SaveCurrentDeck();
            }
            _currentDeck = new QuizDeck
            {
                Title = "New Quiz Deck",
                Theme = "Classic",
                CreatedAtUtc = DateTime.UtcNow,
                IsActive = true
            };
            _db.QuizDecks.Add(_currentDeck);
            _db.SaveChanges();
            RefreshDeckSelector(_currentDeck);
            DeckTitleEditor.Text = _currentDeck.Title;
            DeckThemeSelector.SelectedIndex = 0;
            _slides = new ObservableCollection<DeckSlide>();
            SlideListBox.ItemsSource = _slides;
            _selectedSlide = null;
            SlideListBox.SelectedItem = null;
            ClearEditor();
            RefreshSlideList();
            UpdateLockBanner();
            StatusText.Text = "New blank quiz deck created.";
        }

        private void LoadSampleDeckButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show("Load the complete General Knowledge Championship showcase sample quiz into Quiz Studio?", "Load Showcase Quiz", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (result != MessageBoxResult.Yes) return;

            try
            {
                var sampleDeck = new QuizDeck
                {
                    Title = QuizSampleDeckSeeder.SampleDeckTitle,
                    Description = "Complete sample quiz showcasing MCQ, True/False, Short Answer, Buzzer, Clues, and Tie-Breakers. Fully editable and deletable.",
                    Theme = "Stage",
                    CreatedAtUtc = DateTime.UtcNow,
                    UpdatedAtUtc = DateTime.UtcNow,
                    IsActive = true
                };
                _db.QuizDecks.Add(sampleDeck);
                _db.SaveChanges();

                var slides = QuizSampleDeckSeeder.CreateSampleSlides(sampleDeck.QuizDeckId);
                _db.DeckSlides.AddRange(slides);
                _db.SaveChanges();

                RefreshDeckSelector(sampleDeck);
                LoadSelectedDeck(sampleDeck);
                StatusText.Text = "Showcase sample quiz loaded.";
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Could not load sample deck in studio.", ex);
                MessageBox.Show("Failed to load sample quiz: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DeleteDeckButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDeck == null) return;

            if (HasActiveSession(_currentDeck.QuizDeckId))
            {
                MessageBox.Show("This quiz deck cannot be deleted because it is currently running in an active live session.\n\nPlease finish or close the active session first.", "Deck In Use", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var confirm = MessageBox.Show(string.Format("Are you sure you want to permanently delete quiz deck '{0}' and all its questions?", _currentDeck.Title), "Delete Quiz Deck", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                var deckId = _currentDeck.QuizDeckId;
                var deckToDelete = _db.QuizDecks.FirstOrDefault(d => d.QuizDeckId == deckId);
                if (deckToDelete != null)
                {
                    deckToDelete.IsActive = false;

                    var slides = _db.DeckSlides.Where(s => s.QuizDeckId == deckId).ToList();
                    foreach (var s in slides) s.IsActive = false;

                    var sessionIds = _db.QuizDeckSessions.Where(s => s.QuizDeckId == deckId).Select(s => s.QuizDeckSessionId).ToList();
                    if (sessionIds.Count > 0)
                    {
                        var responses = _db.QuizResponses.Where(r => sessionIds.Contains(r.QuizDeckSessionId)).ToList();
                        _db.QuizResponses.RemoveRange(responses);

                        var audit = _db.QuizAuditEntries.Where(a => sessionIds.Contains(a.QuizDeckSessionId)).ToList();
                        _db.QuizAuditEntries.RemoveRange(audit);

                        var sessions = _db.QuizDeckSessions.Where(s => sessionIds.Contains(s.QuizDeckSessionId)).ToList();
                        _db.QuizDeckSessions.RemoveRange(sessions);
                    }

                    _db.SaveChanges();
                    AppDiagnostics.Write("Deleted deck " + deckId + " from Quiz Studio.");
                }

                LoadOrCreateDeck();
                StatusText.Text = "Quiz deck deleted.";
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Failed to delete deck in studio.", ex);
                MessageBox.Show("Failed to delete deck: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SaveDeckButton_Click(object sender, RoutedEventArgs e)
        {
            if (!SaveCurrentDeck()) return;
            RefreshDeckSelector(_currentDeck);
            StatusText.Text = "Deck saved successfully.";
        }

        private bool SaveCurrentDeck()
        {
            if (_currentDeck == null) return true;
            if (HasActiveSession(_currentDeck.QuizDeckId))
            {
                if (!EnsureDeckUnlockedForEditing())
                {
                    StatusText.Text = "This deck is in an active quiz session and cannot be changed.";
                    return false;
                }
            }
            _currentDeck.Title = string.IsNullOrWhiteSpace(DeckTitleEditor.Text)
                ? "Untitled Quiz"
                : DeckTitleEditor.Text.Trim();
            DeckTitleEditor.Text = _currentDeck.Title;
            _currentDeck.Theme = DeckThemeSelector.SelectedItem?.ToString() ?? "Classic";
            _currentDeck.UpdatedAtUtc = DateTime.UtcNow;
            _db.SaveChanges();
            return true;
        }

        private void SlideListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selectedSlide = SlideListBox.SelectedItem as DeckSlide;
            MoveUpButton.IsEnabled = _selectedSlide != null && SlideListBox.SelectedIndex > 0;
            MoveDownButton.IsEnabled = _selectedSlide != null && SlideListBox.SelectedIndex < _slides.Count - 1;
            DeleteSlideButton.IsEnabled = _selectedSlide != null;
            DuplicateSlideButton.IsEnabled = _selectedSlide != null;
            if (_selectedSlide == null)
            {
                ClearEditor();
                return;
            }
            LoadEditor(_selectedSlide);
        }

        private void LoadEditor(DeckSlide slide)
        {
            _loadingEditor = true;
            SlideRoundNameEditor.Text = slide.RoundName ?? "General";
            SlideTypeSelector.SelectedItem = slide.SlideType ?? "Question";
            QuestionTypeSelector.SelectedItem = slide.QuestionType ?? QuizScoringEngine.QuestionMultipleChoice;
            RoundTypeSelector.SelectedItem = slide.RoundType ?? QuizScoringEngine.RoundStandard;
            SlideTitleEditor.Text = slide.Title ?? string.Empty;
            SlideContentEditor.Text = slide.ContentText ?? string.Empty;
            SlideMediaPathEditor.Text = slide.MediaPath ?? string.Empty;
            SlideCorrectAnswerEditor.Text = slide.CorrectAnswer ?? string.Empty;
            SlideAlternateAnswersEditor.Text = slide.AlternateAnswers ?? string.Empty;
            SlideOptionsEditor.Text = slide.OptionsJson ?? string.Empty;
            SlidePointsEditor.Text = slide.Points.ToString();
            SlidePenaltyEditor.Text = slide.PenaltyPoints.ToString();
            SlideBonusEditor.Text = slide.BonusPoints > 0 ? slide.BonusPoints.ToString() : (slide.Points / 2).ToString();
            SlideTimeLimitEditor.Text = slide.TimeLimitSeconds.ToString();
            SlideOrderEditor.Text = slide.SlideOrder.ToString();
            SlideAllowPassCheckBox.IsChecked = slide.AllowPass;
            SlideHintsEditor.Text = slide.HintsJson ?? string.Empty;
            SlideExplanationEditor.Text = slide.Explanation ?? string.Empty;
            MediaDisplayModeSelector.SelectedIndex = Math.Max(0, Math.Min(2, slide.MediaDisplayMode));

            // Populate context-sensitive option fields
            PopulateOptionEditorsFromRaw(slide.OptionsJson, slide.CorrectAnswer);

            UpdateFormatPanels(slide.QuestionType ?? QuizScoringEngine.QuestionMultipleChoice);
            UpdateEditorCardVisibility(slide.SlideType ?? "Question");
            HideValidationAlert();

            var isInstruction = string.Equals(slide.SlideType, "Instruction", StringComparison.OrdinalIgnoreCase);
            var isMedia = string.Equals(slide.SlideType, "Media", StringComparison.OrdinalIgnoreCase);
            var isTimer = string.Equals(slide.SlideType, "Timer", StringComparison.OrdinalIgnoreCase);
            var isScoreboard = string.Equals(slide.SlideType, "Scoreboard", StringComparison.OrdinalIgnoreCase);

            var slideDesc = isInstruction ? "Instruction" :
                            isMedia ? "Media Clue" :
                            isTimer ? $"Timer · {slide.TimeLimitSeconds}s" :
                            isScoreboard ? "Scoreboard" :
                            $"{slide.QuestionType} · {slide.Points} pts";
            EditStatusIndicator.Text = $"Editing Slide {slide.SlideOrder} of {_slides.Count} · {slideDesc}";

            _loadingEditor = false;
            UpdatePreview();
        }

        private void AddSlideButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDeck == null) return;
            if (HasActiveSession(_currentDeck.QuizDeckId) && !EnsureDeckUnlockedForEditing())
            {
                StatusText.Text = "This deck is in an active quiz and cannot be changed.";
                return;
            }
            var newSlide = new DeckSlide
            {
                QuizDeckId = _currentDeck.QuizDeckId,
                SlideType = "Question",
                QuestionType = QuizScoringEngine.QuestionMultipleChoice,
                RoundType = QuizScoringEngine.RoundStandard,
                RoundName = "General",
                SlideOrder = _slides.Count + 1,
                Title = "New Question " + (_slides.Count + 1),
                ContentText = string.Empty,
                Points = 10,
                PenaltyPoints = 0,
                BonusPoints = 5,
                TimeLimitSeconds = 30,
                AllowPass = true,
                IsActive = true
            };
            _db.DeckSlides.Add(newSlide);
            _db.SaveChanges();
            _slides.Add(newSlide);
            RefreshSlideList();
            SlideListBox.SelectedItem = newSlide;
            StatusText.Text = "New question slide added.";
        }

        private void DuplicateSlideButton_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedSlide == null || _currentDeck == null) return;
            if (HasActiveSession(_currentDeck.QuizDeckId) && !EnsureDeckUnlockedForEditing())
            {
                StatusText.Text = "This deck is in an active quiz and cannot be changed.";
                return;
            }

            var duplicate = new DeckSlide
            {
                QuizDeckId = _currentDeck.QuizDeckId,
                SlideType = _selectedSlide.SlideType,
                QuestionType = _selectedSlide.QuestionType,
                RoundType = _selectedSlide.RoundType,
                RoundName = _selectedSlide.RoundName,
                SlideOrder = _slides.Count + 1,
                Title = _selectedSlide.Title + " (Copy)",
                ContentText = _selectedSlide.ContentText,
                OptionsJson = _selectedSlide.OptionsJson,
                CorrectAnswer = _selectedSlide.CorrectAnswer,
                AlternateAnswers = _selectedSlide.AlternateAnswers,
                Points = _selectedSlide.Points,
                PenaltyPoints = _selectedSlide.PenaltyPoints,
                BonusPoints = _selectedSlide.BonusPoints,
                TimeLimitSeconds = _selectedSlide.TimeLimitSeconds,
                AllowPass = _selectedSlide.AllowPass,
                HintsJson = _selectedSlide.HintsJson,
                Explanation = _selectedSlide.Explanation,
                MediaPath = _selectedSlide.MediaPath,
                MediaType = _selectedSlide.MediaType,
                MediaDisplayMode = _selectedSlide.MediaDisplayMode,
                IsActive = true
            };

            _db.DeckSlides.Add(duplicate);
            _db.SaveChanges();
            _slides.Add(duplicate);
            RefreshSlideList();
            SlideListBox.SelectedItem = duplicate;
            StatusText.Text = "Slide duplicated.";
        }

        private void SaveSlideButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDeck == null) return;
            if (HasActiveSession(_currentDeck.QuizDeckId) && !EnsureDeckUnlockedForEditing())
            {
                StatusText.Text = "This deck is in an active quiz. Finish the session before editing it.";
                return;
            }

            var slideType = SlideTypeSelector.SelectedItem?.ToString() ?? "Question";
            var questionType = QuestionTypeSelector.SelectedItem?.ToString() ?? QuizScoringEngine.QuestionMultipleChoice;

            if (!ValidateAndPrepareSlideForSave(out var errors))
            {
                ShowValidationAlert(errors);
                StatusText.Text = $"⚠️ Unable to save {slideType.ToLowerInvariant()} slide: please resolve the highlighted issues.";
                return;
            }

            HideValidationAlert();

            var isNewSlide = false;
            if (_selectedSlide == null)
            {
                isNewSlide = true;
                _selectedSlide = new DeckSlide
                {
                    QuizDeckId = _currentDeck.QuizDeckId,
                    SlideOrder = _slides.Count + 1,
                    IsActive = true
                };
            }

            if (!isNewSlide && _db.QuizResponses.Any(r => r.DeckSlideId == _selectedSlide.DeckSlideId))
            {
                StatusText.Text = "This question has recorded answers. Add a new slide instead to preserve quiz history.";
                MessageBox.Show(this, "This question has recorded answers in an earlier live session. Add a new slide instead to preserve quiz history.", "Slide Locked", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var order = _selectedSlide.SlideOrder > 0 ? _selectedSlide.SlideOrder : (_slides.Count + 1);
            if (int.TryParse(SlideOrderEditor.Text, out var parsedOrder) && parsedOrder >= 1)
            {
                order = parsedOrder;
            }

            int.TryParse(SlidePointsEditor.Text, out var points);
            int.TryParse(SlidePenaltyEditor.Text, out var penalty);
            int.TryParse(SlideBonusEditor.Text, out var bonus);
            int.TryParse(SlideTimeLimitEditor.Text, out var timeLimit);

            var isQuestion = string.Equals(slideType, "Question", StringComparison.OrdinalIgnoreCase);
            var isTimer = string.Equals(slideType, "Timer", StringComparison.OrdinalIgnoreCase);
            var isMedia = string.Equals(slideType, "Media", StringComparison.OrdinalIgnoreCase);

            _selectedSlide.SlideType = slideType;
            _selectedSlide.QuestionType = isQuestion ? questionType : "Standard";
            _selectedSlide.RoundType = RoundTypeSelector.SelectedItem?.ToString() ?? QuizScoringEngine.RoundStandard;
            _selectedSlide.RoundName = string.IsNullOrWhiteSpace(SlideRoundNameEditor.Text) ? "General" : SlideRoundNameEditor.Text.Trim();
            _selectedSlide.Title = SlideTitleEditor.Text ?? string.Empty;
            _selectedSlide.ContentText = SlideContentEditor.Text ?? string.Empty;
            _selectedSlide.MediaPath = (isQuestion || isMedia) ? (SlideMediaPathEditor.Text ?? string.Empty) : string.Empty;
            _selectedSlide.MediaType = GetMediaType(_selectedSlide.MediaPath);
            _selectedSlide.MediaDisplayMode = MediaDisplayModeSelector.SelectedIndex;
            _selectedSlide.CorrectAnswer = isQuestion ? (SlideCorrectAnswerEditor.Text?.Trim() ?? string.Empty) : string.Empty;
            _selectedSlide.AlternateAnswers = isQuestion ? (SlideAlternateAnswersEditor.Text ?? string.Empty) : string.Empty;
            _selectedSlide.OptionsJson = isQuestion ? (SlideOptionsEditor.Text ?? string.Empty) : string.Empty;
            _selectedSlide.Points = isQuestion ? points : 0;
            _selectedSlide.PenaltyPoints = isQuestion ? penalty : 0;
            _selectedSlide.BonusPoints = isQuestion ? bonus : 0;
            _selectedSlide.TimeLimitSeconds = (isQuestion || isTimer) ? timeLimit : 0;
            _selectedSlide.AllowPass = isQuestion && SlideAllowPassCheckBox.IsChecked == true;
            _selectedSlide.HintsJson = isQuestion ? (SlideHintsEditor.Text ?? string.Empty) : string.Empty;
            _selectedSlide.Explanation = (isQuestion || slideType == "Instruction" || isMedia) ? (SlideExplanationEditor.Text ?? string.Empty) : string.Empty;
            _selectedSlide.SlideOrder = order;

            try
            {
                if (isNewSlide)
                {
                    _db.DeckSlides.Add(_selectedSlide);
                    _slides.Add(_selectedSlide);
                }

                _db.SaveChanges();
                RefreshSlideList();
                SlideListBox.SelectedItem = _selectedSlide;
                EditStatusIndicator.Text = $"Saved at {DateTime.Now:t} · {_selectedSlide.Title}";
                StatusText.Text = $"✓ {slideType} slide saved successfully.";
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Failed to save slide", ex);
                MessageBox.Show(this, "Failed to save slide to database: " + ex.Message, "Database Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusText.Text = "Database save failed.";
            }
        }

        private void MoveUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CanReorderSlides()) return;
            var idx = SlideListBox.SelectedIndex;
            if (idx <= 0) return;
            var item = _slides[idx];
            _slides.RemoveAt(idx);
            _slides.Insert(idx - 1, item);
            ReorderSlideNumbers();
            SlideListBox.SelectedIndex = idx - 1;
        }

        private void MoveDownButton_Click(object sender, RoutedEventArgs e)
        {
            if (!CanReorderSlides()) return;
            var idx = SlideListBox.SelectedIndex;
            if (idx < 0 || idx >= _slides.Count - 1) return;
            var item = _slides[idx];
            _slides.RemoveAt(idx);
            _slides.Insert(idx + 1, item);
            ReorderSlideNumbers();
            SlideListBox.SelectedIndex = idx + 1;
        }

        private void DeleteSlideButton_Click(object sender, RoutedEventArgs e)
        {
            var idx = SlideListBox.SelectedIndex;
            if (idx < 0) return;
            if (HasActiveSession(_currentDeck.QuizDeckId) && !EnsureDeckUnlockedForEditing())
            {
                StatusText.Text = "This deck is in an active quiz and cannot be changed.";
                return;
            }
            var slide = _slides[idx];
            slide.IsActive = false;
            _db.SaveChanges();
            _slides.RemoveAt(idx);
            ReorderSlideNumbers();
            if (_slides.Count > 0)
                SlideListBox.SelectedIndex = Math.Min(idx, _slides.Count - 1);
            else
                ClearEditor();
            RefreshSlideList();
            StatusText.Text = "Slide deleted.";
        }

        private void ReorderSlideNumbers()
        {
            for (var i = 0; i < _slides.Count; i++)
            {
                _slides[i].SlideOrder = i + 1;
            }
            _db.SaveChanges();
            RefreshSlideList();
        }

        private bool CanReorderSlides()
        {
            if (HasActiveSession(_currentDeck.QuizDeckId) && !EnsureDeckUnlockedForEditing())
            {
                StatusText.Text = "This deck is in an active quiz and cannot be changed.";
                return false;
            }
            if (_db.QuizResponses.Any(r => _db.DeckSlides.Any(s =>
                s.DeckSlideId == r.DeckSlideId && s.QuizDeckId == _currentDeck.QuizDeckId)))
            {
                StatusText.Text = "This deck has recorded answers. Its slide order is locked to preserve quiz history.";
                return false;
            }
            return true;
        }


    }
}
