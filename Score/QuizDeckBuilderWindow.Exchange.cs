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
        private void QuickAiQuizButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDeck == null) return;
            if (HasActiveSession(_currentDeck.QuizDeckId) && !EnsureDeckUnlockedForEditing())
            {
                return;
            }

            var dlg = new QuizQuickTopicDialog { Owner = this };
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    if (dlg.ReplaceExisting)
                    {
                        foreach (var s in _slides) s.IsActive = false;
                        _slides.Clear();
                    }

                    var startingOrder = _slides.Count + 1;
                    var generated = QuizAIGeneratorService.GenerateTopicQuestions(dlg.SelectedTopicKey, _currentDeck.QuizDeckId, startingOrder);
                    foreach (var slide in generated)
                    {
                        _db.DeckSlides.Add(slide);
                        _slides.Add(slide);
                    }
                    _db.SaveChanges();
                    RefreshSlideList();
                    if (_slides.Count > 0)
                    {
                        SlideListBox.SelectedItem = generated.FirstOrDefault() ?? _slides.Last();
                    }
                    StatusText.Text = $"✓ Generated 5 questions for '{dlg.SelectedTopicTitle}'.";
                    MessageBox.Show(this, $"Successfully added 5 curated questions for '{dlg.SelectedTopicTitle}' to '{_currentDeck.Title}'.", "Round Generated", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    AppDiagnostics.WriteException("Quick AI Generation failed", ex);
                    MessageBox.Show(this, "Failed to generate topic round: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void SmartPasteButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDeck == null) return;
            if (HasActiveSession(_currentDeck.QuizDeckId) && !EnsureDeckUnlockedForEditing())
            {
                return;
            }

            var dlg = new QuizSmartPasteDialog(_currentDeck.QuizDeckId, _slides.Count + 1) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.ParsedSlides != null && dlg.ParsedSlides.Count > 0)
            {
                try
                {
                    if (dlg.ReplaceExisting)
                    {
                        foreach (var s in _slides) s.IsActive = false;
                        _slides.Clear();
                    }

                    var startingOrder = _slides.Count + 1;
                    foreach (var slide in dlg.ParsedSlides)
                    {
                        slide.QuizDeckId = _currentDeck.QuizDeckId;
                        slide.SlideOrder = startingOrder++;
                        _db.DeckSlides.Add(slide);
                        _slides.Add(slide);
                    }
                    _db.SaveChanges();
                    RefreshSlideList();
                    if (_slides.Count > 0)
                    {
                        SlideListBox.SelectedItem = dlg.ParsedSlides.First();
                    }
                    StatusText.Text = $"✓ Imported {dlg.ParsedSlides.Count} questions via Smart AI Paste.";
                    MessageBox.Show(this, $"Successfully imported {dlg.ParsedSlides.Count} questions into '{_currentDeck.Title}'.", "Import Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    AppDiagnostics.WriteException("Smart Paste failed", ex);
                    MessageBox.Show(this, "Failed to import questions: " + ex.Message, "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ImportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDeck == null) return;
            if (HasActiveSession(_currentDeck.QuizDeckId) && !EnsureDeckUnlockedForEditing())
            {
                StatusText.Text = "This deck is in an active quiz and cannot be changed.";
                return;
            }

            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Title = "Import Questions from CSV",
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var text = File.ReadAllText(dlg.FileName);
                    var imported = QuizImportExportService.ImportSlidesFromCsv(text, _currentDeck.QuizDeckId, _slides.Count + 1);
                    if (imported.Count == 0)
                    {
                        MessageBox.Show(this, "No valid question rows found in the selected CSV file.", "Import CSV", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    foreach (var slide in imported)
                    {
                        _db.DeckSlides.Add(slide);
                        _slides.Add(slide);
                    }
                    _db.SaveChanges();
                    RefreshSlideList();
                    StatusText.Text = $"Successfully imported {imported.Count} questions from {Path.GetFileName(dlg.FileName)}.";
                    MessageBox.Show(this, $"Successfully imported {imported.Count} questions into '{_currentDeck.Title}'.", "Import Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    AppDiagnostics.WriteException("CSV Import failed", ex);
                    MessageBox.Show(this, "Failed to import questions: " + ex.Message, "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void ExportCsvButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDeck == null || _slides.Count == 0)
            {
                StatusText.Text = "No slides to export.";
                return;
            }

            var safeTitle = string.Join("_", (_currentDeck.Title ?? "Quiz").Split(Path.GetInvalidFileNameChars()));
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Export Quiz Deck to CSV",
                FileName = $"{safeTitle}_Questions.csv",
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var csv = QuizImportExportService.ExportDeckToCsv(_currentDeck, _slides.ToList());
                    File.WriteAllText(dlg.FileName, csv);
                    StatusText.Text = $"Exported {_slides.Count} questions to {Path.GetFileName(dlg.FileName)}.";
                    MessageBox.Show(this, $"Successfully exported {_slides.Count} questions to:\n{dlg.FileName}", "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    AppDiagnostics.WriteException("CSV Export failed", ex);
                    MessageBox.Show(this, "Failed to export: " + ex.Message, "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void TemplateButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.SaveFileDialog
            {
                Title = "Save Question Import Template",
                FileName = "Quiz_Questions_Template.csv",
                Filter = "CSV Files (*.csv)|*.csv|All Files (*.*)|*.*"
            };

            if (dlg.ShowDialog() == true)
            {
                try
                {
                    var template = QuizImportExportService.GenerateSampleCsvTemplate();
                    File.WriteAllText(dlg.FileName, template);
                    StatusText.Text = $"Template saved to {Path.GetFileName(dlg.FileName)}.";
                    MessageBox.Show(this, "Template saved. You can open and edit this file in Excel, Google Sheets, or Notepad, then import it back into QuizScore Live.", "Template Ready", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, "Failed to save template: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void BrowseMediaButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "Media files (*.jpg;*.jpeg;*.png;*.mp4;*.mp3;*.wav)|*.jpg;*.jpeg;*.png;*.mp4;*.mp3;*.wav|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog() == true)
                SlideMediaPathEditor.Text = dlg.FileName;
        }


        private void UpdateLockBanner()
        {
            if (DeckLockedBanner == null) return;
            var isLocked = _currentDeck != null && HasActiveSession(_currentDeck.QuizDeckId);
            DeckLockedBanner.Visibility = isLocked ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UnlockDeckButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDeck == null) return;
            var result = MessageBox.Show(this,
                $"Are you sure you want to end the active quiz session for '{_currentDeck.Title}'?\n\nThis will mark the session as completed and unlock this deck for editing.",
                "Unlock Deck",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                UnlockCurrentDeck(true);
            }
        }

        private bool EnsureDeckUnlockedForEditing()
        {
            if (_currentDeck == null) return false;
            if (!HasActiveSession(_currentDeck.QuizDeckId)) return true;

            if (IsHeadlessTestEnvironment())
            {
                UnlockCurrentDeck(false);
                return true;
            }

            var result = MessageBox.Show(this,
                $"The deck '{_currentDeck.Title}' is currently linked to an active quiz session.\n\nWould you like to finish that session now and unlock the deck for editing?",
                "Deck Locked - Finish Active Session?",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                UnlockCurrentDeck(true);
                return true;
            }

            return false;
        }

        private void UnlockCurrentDeck(bool showFeedback = true)
        {
            if (_currentDeck == null) return;
            var activeSessions = _db.QuizDeckSessions
                .Where(s => s.QuizDeckId == _currentDeck.QuizDeckId && (s.Status == "Running" || s.Status == "Paused"))
                .ToList();

            foreach (var session in activeSessions)
            {
                session.Status = "Completed";
                session.CompletedAtUtc = DateTime.UtcNow;
                session.TimerState = "Stopped";
                session.TimerStartedAtUtc = null;
            }
            _db.SaveChanges();
            UpdateLockBanner();
            if (showFeedback)
            {
                StatusText.Text = $"✓ Session ended. '{_currentDeck.Title}' is now unlocked for editing.";
            }
        }

        private bool HasActiveSession(int deckId)
        {
            return _db.QuizDeckSessions.Any(s => s.QuizDeckId == deckId &&
                (s.Status == "Running" || s.Status == "Paused"));
        }

        private static string GetMediaType(string mediaPath)
        {
            switch (Path.GetExtension(mediaPath ?? string.Empty).ToLowerInvariant())
            {
                case ".jpg":
                case ".jpeg":
                case ".png":
                    return "Image";
                case ".mp4":
                    return "Video";
                case ".mp3":
                case ".wav":
                    return "Audio";
                default:
                    return string.Empty;
            }
        }

        internal static bool IsValidQuestion(string questionType, string correctAnswer, string options)
        {
            switch (questionType)
            {
                case QuizScoringEngine.QuestionMultipleChoice:
                    var choices = QuizScoringEngine.ParseOptions(options);
                    return choices.Count >= 2 && QuizAIGeneratorService.MatchesMcqOption(correctAnswer, choices, out _);

                case QuizScoringEngine.QuestionMultipleSelect:
                    var multiChoices = QuizScoringEngine.ParseOptions(options);
                    return multiChoices.Count >= 2 && !string.IsNullOrWhiteSpace(correctAnswer);

                case QuizScoringEngine.QuestionTrueFalse:
                    return QuizAIGeneratorService.IsBooleanAnswer(correctAnswer, out _);

                case QuizScoringEngine.QuestionNumeric:
                    var cleanNumeric = QuizAIGeneratorService.CleanForMcqMatch(correctAnswer);
                    return decimal.TryParse(cleanNumeric, out _);

                case QuizScoringEngine.QuestionShortAnswer:
                case QuizScoringEngine.QuestionSequence:
                    return !string.IsNullOrWhiteSpace(correctAnswer);

                case QuizScoringEngine.QuestionManual:
                    return true;

                default:
                    return false;
            }
        }

    }
}
