using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Score
{
    public partial class QuizDeckBuilderWindow : Window
    {
        private void UpdateEditorCardVisibility(string slideType)
        {
            if (Card2AnswerKeys == null || Card3ScoringRules == null) return;

            var isInstruction = string.Equals(slideType, "Instruction", StringComparison.OrdinalIgnoreCase);
            var isMedia = string.Equals(slideType, "Media", StringComparison.OrdinalIgnoreCase);
            var isTimer = string.Equals(slideType, "Timer", StringComparison.OrdinalIgnoreCase);
            var isScoreboard = string.Equals(slideType, "Scoreboard", StringComparison.OrdinalIgnoreCase);

            InstructionNoticeBanner.Visibility = isInstruction ? Visibility.Visible : Visibility.Collapsed;
            UpdateStageRulesVisibility(isInstruction);

            if (SaveSlideButton != null)
            {
                if (isInstruction) SaveSlideButton.Content = "✓ Save Instruction";
                else if (isMedia) SaveSlideButton.Content = "✓ Save Media Slide";
                else if (isTimer) SaveSlideButton.Content = "✓ Save Timer";
                else if (isScoreboard) SaveSlideButton.Content = "✓ Save Scoreboard";
                else SaveSlideButton.Content = "✓ Save Question";
            }

            if (isInstruction)
            {
                Card2AnswerKeys.Visibility = Visibility.Collapsed;
                Card3ScoringRules.Visibility = Visibility.Collapsed;
                if (Card4MediaClues != null) Card4MediaClues.Visibility = Visibility.Collapsed;
                SlideTitleLabel.Text = "Announcement / Slide Title (e.g. Round 1 Rules)";
                SlideContentLabel.Text = "Announcement & Rules Content (Displayed on stage)";
                Card1Header.Text = "1. INSTRUCTION / ANNOUNCEMENT ESSENTIALS";
            }
            else if (isMedia)
            {
                Card2AnswerKeys.Visibility = Visibility.Collapsed;
                Card3ScoringRules.Visibility = Visibility.Collapsed;
                if (Card4MediaClues != null) Card4MediaClues.Visibility = Visibility.Visible;
                SlideTitleLabel.Text = "Media Slide Title / Clue Headline";
                SlideContentLabel.Text = "Media Caption / Prompt (Displayed below media)";
                Card1Header.Text = "1. MEDIA ESSENTIALS";
            }
            else if (isTimer)
            {
                Card2AnswerKeys.Visibility = Visibility.Collapsed;
                Card3ScoringRules.Visibility = Visibility.Visible;
                if (Card4MediaClues != null) Card4MediaClues.Visibility = Visibility.Collapsed;
                SlideTitleLabel.Text = "Timer Slide Title";
                SlideContentLabel.Text = "Timer Message / Instructions";
                Card1Header.Text = "1. TIMER ESSENTIALS";
            }
            else if (isScoreboard)
            {
                Card2AnswerKeys.Visibility = Visibility.Collapsed;
                Card3ScoringRules.Visibility = Visibility.Collapsed;
                if (Card4MediaClues != null) Card4MediaClues.Visibility = Visibility.Collapsed;
                SlideTitleLabel.Text = "Scoreboard / Intermission Title";
                SlideContentLabel.Text = "Scoreboard Note / Intermission Message";
                Card1Header.Text = "1. SCOREBOARD ESSENTIALS";
            }
            else
            {
                Card2AnswerKeys.Visibility = Visibility.Visible;
                Card3ScoringRules.Visibility = Visibility.Visible;
                if (Card4MediaClues != null) Card4MediaClues.Visibility = Visibility.Visible;
                SlideTitleLabel.Text = "Question Title / Headline";
                SlideContentLabel.Text = "Question Prompt (Displayed to players & audience)";
                Card1Header.Text = "1. QUESTION ESSENTIALS";
            }
        }

        private void PopulateOptionEditorsFromRaw(string rawOptions, string correctAnswer)
        {
            var options = QuizScoringEngine.ParseOptions(rawOptions);
            OptionAEditor.Text = options.Count > 0 ? options[0] : string.Empty;
            OptionBEditor.Text = options.Count > 1 ? options[1] : string.Empty;
            OptionCEditor.Text = options.Count > 2 ? options[2] : string.Empty;
            OptionDEditor.Text = options.Count > 3 ? options[3] : string.Empty;

            OptionACheck.IsChecked = !string.IsNullOrWhiteSpace(OptionAEditor.Text) &&
                (string.Equals(OptionAEditor.Text.Trim(), correctAnswer?.Trim(), StringComparison.OrdinalIgnoreCase) ||
                 QuizAIGeneratorService.MatchesMcqOption(correctAnswer, options, out var matchedA) && string.Equals(OptionAEditor.Text.Trim(), matchedA, StringComparison.OrdinalIgnoreCase));
            OptionBCheck.IsChecked = !string.IsNullOrWhiteSpace(OptionBEditor.Text) &&
                (string.Equals(OptionBEditor.Text.Trim(), correctAnswer?.Trim(), StringComparison.OrdinalIgnoreCase) ||
                 QuizAIGeneratorService.MatchesMcqOption(correctAnswer, options, out var matchedB) && string.Equals(OptionBEditor.Text.Trim(), matchedB, StringComparison.OrdinalIgnoreCase));
            OptionCCheck.IsChecked = !string.IsNullOrWhiteSpace(OptionCEditor.Text) &&
                (string.Equals(OptionCEditor.Text.Trim(), correctAnswer?.Trim(), StringComparison.OrdinalIgnoreCase) ||
                 QuizAIGeneratorService.MatchesMcqOption(correctAnswer, options, out var matchedC) && string.Equals(OptionCEditor.Text.Trim(), matchedC, StringComparison.OrdinalIgnoreCase));
            OptionDCheck.IsChecked = !string.IsNullOrWhiteSpace(OptionDEditor.Text) &&
                (string.Equals(OptionDEditor.Text.Trim(), correctAnswer?.Trim(), StringComparison.OrdinalIgnoreCase) ||
                 QuizAIGeneratorService.MatchesMcqOption(correctAnswer, options, out var matchedD) && string.Equals(OptionDEditor.Text.Trim(), matchedD, StringComparison.OrdinalIgnoreCase));

            UpdateOptionCardHighlights();
            UpdateTrueFalseHighlight(correctAnswer);
        }

        private void ClearEditor()
        {
            _loadingEditor = true;
            SlideRoundNameEditor.Text = string.Empty;
            SlideTypeSelector.SelectedIndex = 0;
            QuestionTypeSelector.SelectedIndex = 0;
            RoundTypeSelector.SelectedIndex = 0;
            SlideTitleEditor.Text = string.Empty;
            SlideContentEditor.Text = string.Empty;
            SlideMediaPathEditor.Text = string.Empty;
            SlideCorrectAnswerEditor.Text = string.Empty;
            SlideAlternateAnswersEditor.Text = string.Empty;
            SlideOptionsEditor.Text = string.Empty;
            SlidePointsEditor.Text = "10";
            SlidePenaltyEditor.Text = "0";
            SlideBonusEditor.Text = "5";
            SlideTimeLimitEditor.Text = "30";
            SlideOrderEditor.Text = string.Empty;
            SlideAllowPassCheckBox.IsChecked = true;
            SlideHintsEditor.Text = string.Empty;
            SlideExplanationEditor.Text = string.Empty;
            MediaDisplayModeSelector.SelectedIndex = 0;

            OptionAEditor.Text = string.Empty;
            OptionBEditor.Text = string.Empty;
            OptionCEditor.Text = string.Empty;
            OptionDEditor.Text = string.Empty;
            OptionACheck.IsChecked = false;
            OptionBCheck.IsChecked = false;
            OptionCCheck.IsChecked = false;
            OptionDCheck.IsChecked = false;

            UpdateOptionCardHighlights();
            UpdateTrueFalseHighlight(string.Empty);
            UpdateFormatPanels(QuizScoringEngine.QuestionMultipleChoice);
            UpdateEditorCardVisibility("Question");
            ClearSlideStageRules();
            HideValidationAlert();

            EditStatusIndicator.Text = "Ready to add new question";

            _loadingEditor = false;
            UpdatePreview();
        }

        private void UpdateFormatPanels(string questionType)
        {
            if (McqOptionsContainer == null || TrueFalseContainer == null || ManualNoticeContainer == null) return;

            switch (questionType)
            {
                case QuizScoringEngine.QuestionMultipleChoice:
                case QuizScoringEngine.QuestionMultipleSelect:
                    McqOptionsContainer.Visibility = Visibility.Visible;
                    TrueFalseContainer.Visibility = Visibility.Collapsed;
                    ManualNoticeContainer.Visibility = Visibility.Collapsed;
                    break;

                case QuizScoringEngine.QuestionTrueFalse:
                    McqOptionsContainer.Visibility = Visibility.Collapsed;
                    TrueFalseContainer.Visibility = Visibility.Visible;
                    ManualNoticeContainer.Visibility = Visibility.Collapsed;
                    break;

                case QuizScoringEngine.QuestionManual:
                    McqOptionsContainer.Visibility = Visibility.Collapsed;
                    TrueFalseContainer.Visibility = Visibility.Collapsed;
                    ManualNoticeContainer.Visibility = Visibility.Visible;
                    break;

                default: // ShortAnswer, Numeric, Sequence
                    McqOptionsContainer.Visibility = Visibility.Collapsed;
                    TrueFalseContainer.Visibility = Visibility.Collapsed;
                    ManualNoticeContainer.Visibility = Visibility.Collapsed;
                    break;
            }
        }

        private void QuestionTypeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var qType = QuestionTypeSelector.SelectedItem?.ToString() ?? QuizScoringEngine.QuestionMultipleChoice;
            UpdateFormatPanels(qType);

            if (qType == QuizScoringEngine.QuestionTrueFalse && string.IsNullOrWhiteSpace(SlideOptionsEditor.Text))
            {
                SlideOptionsEditor.Text = "True\nFalse";
            }

            if (!_loadingEditor) UpdatePreview();
        }

        private void SlideTypeSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var slideType = SlideTypeSelector.SelectedItem?.ToString() ?? "Question";
            UpdateEditorCardVisibility(slideType);
            if (_selectedSlide != null)
                _selectedSlide.SlideType = slideType;
            if (!_loadingEditor)
            {
                HideValidationAlert();
                UpdatePreview();
            }
        }

        private void OptionEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loadingEditor || _syncingOptions) return;
            _syncingOptions = true;
            try
            {
                var opts = new List<string>();
                if (!string.IsNullOrWhiteSpace(OptionAEditor.Text)) opts.Add(OptionAEditor.Text.Trim());
                if (!string.IsNullOrWhiteSpace(OptionBEditor.Text)) opts.Add(OptionBEditor.Text.Trim());
                if (!string.IsNullOrWhiteSpace(OptionCEditor.Text)) opts.Add(OptionCEditor.Text.Trim());
                if (!string.IsNullOrWhiteSpace(OptionDEditor.Text)) opts.Add(OptionDEditor.Text.Trim());

                SlideOptionsEditor.Text = string.Join("\n", opts);

                if (OptionACheck.IsChecked == true) SlideCorrectAnswerEditor.Text = OptionAEditor.Text.Trim();
                else if (OptionBCheck.IsChecked == true) SlideCorrectAnswerEditor.Text = OptionBEditor.Text.Trim();
                else if (OptionCCheck.IsChecked == true) SlideCorrectAnswerEditor.Text = OptionCEditor.Text.Trim();
                else if (OptionDCheck.IsChecked == true) SlideCorrectAnswerEditor.Text = OptionDEditor.Text.Trim();
            }
            finally
            {
                _syncingOptions = false;
            }

            UpdatePreview();
        }

        private void OptionCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (_loadingEditor) return;

            if (OptionACheck.IsChecked == true) SlideCorrectAnswerEditor.Text = OptionAEditor.Text.Trim();
            else if (OptionBCheck.IsChecked == true) SlideCorrectAnswerEditor.Text = OptionBEditor.Text.Trim();
            else if (OptionCCheck.IsChecked == true) SlideCorrectAnswerEditor.Text = OptionCEditor.Text.Trim();
            else if (OptionDCheck.IsChecked == true) SlideCorrectAnswerEditor.Text = OptionDEditor.Text.Trim();

            UpdateOptionCardHighlights();
            UpdatePreview();
        }

        private void UpdateOptionCardHighlights()
        {
            SetCardHighlight(OptionACard, OptionACheck.IsChecked == true);
            SetCardHighlight(OptionBCard, OptionBCheck.IsChecked == true);
            SetCardHighlight(OptionCCard, OptionCCheck.IsChecked == true);
            SetCardHighlight(OptionDCard, OptionDCheck.IsChecked == true);
        }

        private static void SetCardHighlight(Border card, bool isCorrect)
        {
            if (card == null) return;
            card.Background = isCorrect ? new SolidColorBrush(Color.FromRgb(240, 253, 244)) : new SolidColorBrush(Color.FromRgb(248, 250, 252));
            card.BorderBrush = isCorrect ? new SolidColorBrush(Color.FromRgb(16, 185, 129)) : new SolidColorBrush(Color.FromRgb(226, 232, 240));
            card.BorderThickness = isCorrect ? new Thickness(1.5) : new Thickness(1);
        }

        private void TrueChoiceButton_Click(object sender, RoutedEventArgs e)
        {
            SlideCorrectAnswerEditor.Text = "True";
            UpdateTrueFalseHighlight("True");
            UpdatePreview();
        }

        private void FalseChoiceButton_Click(object sender, RoutedEventArgs e)
        {
            SlideCorrectAnswerEditor.Text = "False";
            UpdateTrueFalseHighlight("False");
            UpdatePreview();
        }

        private void UpdateTrueFalseHighlight(string answer)
        {
            if (TrueChoiceButton == null || FalseChoiceButton == null) return;

            if (string.Equals(answer, "True", StringComparison.OrdinalIgnoreCase))
            {
                TrueChoiceButton.Background = new SolidColorBrush(Color.FromRgb(5, 150, 105));
                TrueChoiceButton.Foreground = Brushes.White;
                TrueChoiceButton.BorderBrush = new SolidColorBrush(Color.FromRgb(4, 120, 87));

                FalseChoiceButton.Background = Brushes.White;
                FalseChoiceButton.Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85));
                FalseChoiceButton.BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225));
            }
            else if (string.Equals(answer, "False", StringComparison.OrdinalIgnoreCase))
            {
                FalseChoiceButton.Background = new SolidColorBrush(Color.FromRgb(220, 38, 38));
                FalseChoiceButton.Foreground = Brushes.White;
                FalseChoiceButton.BorderBrush = new SolidColorBrush(Color.FromRgb(185, 28, 28));

                TrueChoiceButton.Background = Brushes.White;
                TrueChoiceButton.Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85));
                TrueChoiceButton.BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225));
            }
            else
            {
                TrueChoiceButton.Background = Brushes.White;
                TrueChoiceButton.Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85));
                TrueChoiceButton.BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225));

                FalseChoiceButton.Background = Brushes.White;
                FalseChoiceButton.Foreground = new SolidColorBrush(Color.FromRgb(51, 65, 85));
                FalseChoiceButton.BorderBrush = new SolidColorBrush(Color.FromRgb(203, 213, 225));
            }
        }

        private void SlideOptionsEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_loadingEditor || _syncingOptions) return;
            _syncingOptions = true;
            try
            {
                PopulateOptionEditorsFromRaw(SlideOptionsEditor.Text, SlideCorrectAnswerEditor.Text);
            }
            finally
            {
                _syncingOptions = false;
            }
            UpdatePreview();
        }

        private static bool IsHeadlessTestEnvironment()
        {
            var friendly = AppDomain.CurrentDomain.FriendlyName;
            if (friendly != null && friendly.IndexOf("Score.Tests", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("QUIZSCORE_HEADLESS_TEST"));
        }

        private void ShowValidationAlert(List<string> errors)
        {
            if (ValidationAlertBanner == null || ValidationAlertText == null) return;
            ValidationAlertText.Text = string.Join(Environment.NewLine, errors.Select(err => "• " + err));
            ValidationAlertBanner.Visibility = Visibility.Visible;
            ValidationAlertBanner.BringIntoView();
            if (!IsHeadlessTestEnvironment() && Application.Current != null && Application.Current.MainWindow == this)
            {
                MessageBox.Show(this, "Please resolve the following issues to save this slide:\n\n" + string.Join("\n", errors.Select(err => "• " + err)), "Question Incomplete", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void HideValidationAlert()
        {
            if (ValidationAlertBanner != null)
                ValidationAlertBanner.Visibility = Visibility.Collapsed;
        }

        private void DismissAlertButton_Click(object sender, RoutedEventArgs e)
        {
            HideValidationAlert();
        }

        private void OptionCard_PreviewMouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (e.OriginalSource is TextBox) return;
            if (sender == OptionACard) OptionACheck.IsChecked = true;
            else if (sender == OptionBCard) OptionBCheck.IsChecked = true;
            else if (sender == OptionCCard) OptionCCheck.IsChecked = true;
            else if (sender == OptionDCard) OptionDCheck.IsChecked = true;
        }

        private void SyncOptionsBeforeSave(string questionType)
        {
            if (questionType == QuizScoringEngine.QuestionMultipleChoice || questionType == QuizScoringEngine.QuestionMultipleSelect)
            {
                var structuredOpts = new List<string>();
                if (!string.IsNullOrWhiteSpace(OptionAEditor.Text)) structuredOpts.Add(OptionAEditor.Text.Trim());
                if (!string.IsNullOrWhiteSpace(OptionBEditor.Text)) structuredOpts.Add(OptionBEditor.Text.Trim());
                if (!string.IsNullOrWhiteSpace(OptionCEditor.Text)) structuredOpts.Add(OptionCEditor.Text.Trim());
                if (!string.IsNullOrWhiteSpace(OptionDEditor.Text)) structuredOpts.Add(OptionDEditor.Text.Trim());

                // Preserve options 5+ if present in SlideOptionsEditor (e.g. 5-choice or 6-choice MCQs)
                var existingOpts = QuizScoringEngine.ParseOptions(SlideOptionsEditor.Text);
                if (existingOpts.Count > 4)
                {
                    for (var i = 4; i < existingOpts.Count; i++)
                    {
                        structuredOpts.Add(existingOpts[i]);
                    }
                }

                if (structuredOpts.Count > 0)
                {
                    _syncingOptions = true;
                    try
                    {
                        SlideOptionsEditor.Text = string.Join("\n", structuredOpts);
                    }
                    finally
                    {
                        _syncingOptions = false;
                    }

                    if (string.IsNullOrWhiteSpace(SlideCorrectAnswerEditor.Text))
                    {
                        if (OptionACheck.IsChecked == true) SlideCorrectAnswerEditor.Text = OptionAEditor.Text.Trim();
                        else if (OptionBCheck.IsChecked == true) SlideCorrectAnswerEditor.Text = OptionBEditor.Text.Trim();
                        else if (OptionCCheck.IsChecked == true) SlideCorrectAnswerEditor.Text = OptionCEditor.Text.Trim();
                        else if (OptionDCheck.IsChecked == true) SlideCorrectAnswerEditor.Text = OptionDEditor.Text.Trim();
                    }
                }
                else if (!string.IsNullOrWhiteSpace(SlideOptionsEditor.Text))
                {
                    _syncingOptions = true;
                    try
                    {
                        PopulateOptionEditorsFromRaw(SlideOptionsEditor.Text, SlideCorrectAnswerEditor.Text);
                    }
                    finally
                    {
                        _syncingOptions = false;
                    }
                }
            }
            else if (questionType == QuizScoringEngine.QuestionTrueFalse)
            {
                if (string.IsNullOrWhiteSpace(SlideOptionsEditor.Text))
                {
                    SlideOptionsEditor.Text = "True\nFalse";
                }
            }
        }

        private bool ValidateAndPrepareSlideForSave(out List<string> errors)
        {
            errors = new List<string>();
            var slideType = SlideTypeSelector.SelectedItem?.ToString() ?? "Question";

            // If title is missing, provide a friendly default based on content or order
            if (string.IsNullOrWhiteSpace(SlideTitleEditor.Text))
            {
                if (!string.IsNullOrWhiteSpace(SlideContentEditor.Text))
                {
                    var cleanPrompt = SlideContentEditor.Text.Trim();
                    var firstLine = cleanPrompt.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)[0].Trim();
                    SlideTitleEditor.Text = firstLine.Length <= 45 ? firstLine : firstLine.Substring(0, 42) + "...";
                }
                else
                {
                    SlideTitleEditor.Text = slideType == "Instruction" ? "Rules / Announcement" : $"Question {_slides.Count + 1}";
                }
            }

            if (slideType == "Instruction")
            {
                if (string.IsNullOrWhiteSpace(SlideContentEditor.Text) && string.IsNullOrWhiteSpace(SlideTitleEditor.Text))
                {
                    errors.Add("Please enter instruction or announcement content for this slide.");
                }
                return errors.Count == 0;
            }

            if (slideType == "Media")
            {
                if (string.IsNullOrWhiteSpace(SlideMediaPathEditor.Text) && string.IsNullOrWhiteSpace(SlideContentEditor.Text))
                {
                    errors.Add("Please select a media file or provide a caption for the media slide.");
                }
                if (!string.IsNullOrWhiteSpace(SlideMediaPathEditor.Text) && !File.Exists(SlideMediaPathEditor.Text))
                {
                    errors.Add("The selected media file does not exist on disk.");
                }
                return errors.Count == 0;
            }

            if (slideType == "Timer")
            {
                if (!int.TryParse(SlideTimeLimitEditor.Text, out var sec) || sec <= 0)
                {
                    errors.Add("Timer slide must have a positive duration in seconds (e.g. 30, 60).");
                }
                return errors.Count == 0;
            }

            if (slideType == "Scoreboard")
            {
                return errors.Count == 0;
            }

            // Question validation
            if (string.IsNullOrWhiteSpace(SlideContentEditor.Text))
            {
                errors.Add("Question Prompt is required (the prompt displayed to players and audience).");
            }

            var qType = QuestionTypeSelector.SelectedItem?.ToString() ?? QuizScoringEngine.QuestionMultipleChoice;
            SyncOptionsBeforeSave(qType);

            var rawOptions = SlideOptionsEditor.Text ?? string.Empty;
            var correctAnswer = SlideCorrectAnswerEditor.Text?.Trim() ?? string.Empty;

            switch (qType)
            {
                case QuizScoringEngine.QuestionMultipleChoice:
                    var choices = QuizScoringEngine.ParseOptions(rawOptions);
                    if (choices.Count < 2)
                    {
                        errors.Add("Multiple choice questions require at least 2 choices (e.g. Option A and Option B).");
                    }
                    if (string.IsNullOrWhiteSpace(correctAnswer))
                    {
                        errors.Add("Please specify the correct answer key (click 'Correct Answer' next to a choice or type the answer).");
                    }
                    else if (choices.Count >= 2)
                    {
                        if (!QuizAIGeneratorService.MatchesMcqOption(correctAnswer, choices, out var matchedText))
                        {
                            errors.Add($"Correct answer '{correctAnswer}' does not match any of the {choices.Count} choices ({string.Join(", ", choices)}).");
                        }
                        else
                        {
                            SlideCorrectAnswerEditor.Text = matchedText;
                        }
                    }
                    break;

                case QuizScoringEngine.QuestionMultipleSelect:
                    var multiChoices = QuizScoringEngine.ParseOptions(rawOptions);
                    if (multiChoices.Count < 2)
                    {
                        errors.Add("Multiple select questions require at least 2 choices.");
                    }
                    if (string.IsNullOrWhiteSpace(correctAnswer))
                    {
                        errors.Add("Please enter the correct answer key for multiple select.");
                    }
                    break;

                case QuizScoringEngine.QuestionTrueFalse:
                    if (string.IsNullOrWhiteSpace(correctAnswer))
                    {
                        errors.Add("Please select TRUE or FALSE as the correct answer key.");
                    }
                    else if (!QuizAIGeneratorService.IsBooleanAnswer(correctAnswer, out var normBool))
                    {
                        errors.Add("Correct answer for True/False must be 'True' or 'False'.");
                    }
                    else
                    {
                        SlideCorrectAnswerEditor.Text = normBool;
                        SlideOptionsEditor.Text = "True\nFalse";
                    }
                    break;

                case QuizScoringEngine.QuestionNumeric:
                    var cleanNumeric = QuizAIGeneratorService.CleanForMcqMatch(correctAnswer);
                    if (string.IsNullOrWhiteSpace(cleanNumeric))
                    {
                        errors.Add("Please enter the numeric correct answer.");
                    }
                    else if (!decimal.TryParse(cleanNumeric, out _))
                    {
                        errors.Add($"Numeric answer '{cleanNumeric}' is not a valid number (e.g. 42 or 3.14).");
                    }
                    else
                    {
                        SlideCorrectAnswerEditor.Text = cleanNumeric;
                    }
                    break;

                case QuizScoringEngine.QuestionShortAnswer:
                case QuizScoringEngine.QuestionSequence:
                    if (string.IsNullOrWhiteSpace(correctAnswer))
                    {
                        errors.Add("Please enter the correct answer key.");
                    }
                    break;

                case QuizScoringEngine.QuestionManual:
                    // Manual adjudication requires no rigid answer key
                    break;
            }

            if (!int.TryParse(SlidePointsEditor.Text, out var points) || points < 0)
                errors.Add("Correct points must be a non-negative number.");
            if (!int.TryParse(SlidePenaltyEditor.Text, out var penalty) || penalty < 0)
                errors.Add("Wrong penalty points must be a non-negative number.");
            if (!int.TryParse(SlideBonusEditor.Text, out var bonus) || bonus < 0)
                errors.Add("Bonus points must be a non-negative number.");
            if (!int.TryParse(SlideTimeLimitEditor.Text, out var timeLimit) || timeLimit < 0)
                errors.Add("Time limit must be a non-negative number of seconds.");

            return errors.Count == 0;
        }


    }
}
