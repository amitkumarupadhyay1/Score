using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Score
{
    public partial class QuizLibraryWindow
    {
        private void DeleteDeck(int deckId, string deckTitle)
        {
            var result = MessageBox.Show(this,
                $"Are you sure you want to permanently delete the quiz '{deckTitle}'?\n\nThis will remove all associated slides, past session records, and score logs.\nThis action cannot be undone.",
                "Delete Quiz Deck",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                using (var db = new ScoreDbContext())
                {
                    using (var transaction = db.Database.BeginTransaction())
                    {
                        var sessionIds = db.QuizDeckSessions
                            .Where(s => s.QuizDeckId == deckId)
                            .Select(s => s.QuizDeckSessionId)
                            .ToList();

                        if (sessionIds.Count > 0)
                        {
                            var auditEntries = db.QuizAuditEntries.Where(a => sessionIds.Contains(a.QuizDeckSessionId)).ToList();
                            db.QuizAuditEntries.RemoveRange(auditEntries);

                            var responses = db.QuizResponses.Where(r => sessionIds.Contains(r.QuizDeckSessionId)).ToList();
                            db.QuizResponses.RemoveRange(responses);

                            var sessionTeams = db.QuizDeckSessionTeams.Where(t => sessionIds.Contains(t.QuizDeckSessionId)).ToList();
                            db.QuizDeckSessionTeams.RemoveRange(sessionTeams);

                            var sessions = db.QuizDeckSessions.Where(s => s.QuizDeckId == deckId).ToList();
                            db.QuizDeckSessions.RemoveRange(sessions);
                        }

                        var slides = db.DeckSlides.Where(s => s.QuizDeckId == deckId).ToList();
                        db.DeckSlides.RemoveRange(slides);

                        var deck = db.QuizDecks.FirstOrDefault(d => d.QuizDeckId == deckId);
                        if (deck != null)
                        {
                            db.QuizDecks.Remove(deck);
                        }

                        db.SaveChanges();
                        transaction.Commit();
                    }
                }

                LoadDecksAndRefresh();
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Failed to delete quiz deck.", ex);
                MessageBox.Show(this, "Could not delete quiz: " + ex.Message, "Delete Quiz", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void DuplicateDeck(int deckId, string deckTitle)
        {
            try
            {
                using (var db = new ScoreDbContext())
                {
                    var sourceDeck = db.QuizDecks.FirstOrDefault(d => d.QuizDeckId == deckId);
                    if (sourceDeck == null) return;

                    var newDeck = new QuizDeck
                    {
                        Title = $"{sourceDeck.Title} (Copy)",
                        Description = sourceDeck.Description,
                        Theme = sourceDeck.Theme,
                        CreatedAtUtc = DateTime.UtcNow,
                        UpdatedAtUtc = DateTime.UtcNow,
                        IsActive = true
                    };

                    db.QuizDecks.Add(newDeck);
                    db.SaveChanges();

                    var sourceSlides = db.DeckSlides
                        .Where(s => s.QuizDeckId == deckId && s.IsActive)
                        .OrderBy(s => s.SlideOrder)
                        .ToList();

                    foreach (var s in sourceSlides)
                    {
                        db.DeckSlides.Add(new DeckSlide
                        {
                            QuizDeckId = newDeck.QuizDeckId,
                            SlideOrder = s.SlideOrder,
                            SlideType = s.SlideType,
                            QuestionType = s.QuestionType,
                            RoundType = s.RoundType,
                            RoundName = s.RoundName,
                            Title = s.Title,
                            ContentText = s.ContentText,
                            OptionsJson = s.OptionsJson,
                            CorrectAnswer = s.CorrectAnswer,
                            AlternateAnswers = s.AlternateAnswers,
                            Points = s.Points,
                            PenaltyPoints = s.PenaltyPoints,
                            BonusPoints = s.BonusPoints,
                            TimeLimitSeconds = s.TimeLimitSeconds,
                            AllowPass = s.AllowPass,
                            MediaPath = s.MediaPath,
                            MediaDisplayMode = s.MediaDisplayMode,
                            HintsJson = s.HintsJson,
                            Explanation = s.Explanation,
                            IsActive = true
                        });
                    }

                    db.SaveChanges();
                }

                LoadDecksAndRefresh();
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Failed to clone quiz deck.", ex);
                MessageBox.Show(this, "Could not duplicate quiz: " + ex.Message, "Duplicate Quiz", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void PreviewDeck(int deckId, string deckTitle)
        {
            try
            {
                using (var db = new ScoreDbContext())
                {
                    var slides = db.DeckSlides
                        .Where(s => s.QuizDeckId == deckId && s.IsActive)
                        .OrderBy(s => s.SlideOrder)
                        .ToList();

                    if (slides.Count == 0)
                    {
                        MessageBox.Show(this, $"Quiz '{deckTitle}' has no active slides.", "Preview Slides", MessageBoxButton.OK, MessageBoxImage.Information);
                        return;
                    }

                    var previewWin = new Window
                    {
                        Title = $"Slide Preview · {deckTitle}",
                        Width = 850,
                        Height = 580,
                        MinWidth = 600,
                        MinHeight = 400,
                        WindowStartupLocation = WindowStartupLocation.CenterOwner,
                        Owner = this,
                        Background = (Brush)new BrushConverter().ConvertFromString("#FFF8FAFC")
                    };

                    var mainGrid = new Grid { Margin = new Thickness(16) };
                    mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                    mainGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                    mainGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                    var header = new TextBlock
                    {
                        Text = $"{deckTitle} ({slides.Count} slides)",
                        FontSize = 16,
                        FontWeight = FontWeights.Bold,
                        Foreground = (Brush)new BrushConverter().ConvertFromString("#FF0F172A"),
                        Margin = new Thickness(0, 0, 0, 12)
                    };
                    Grid.SetRow(header, 0);
                    mainGrid.Children.Add(header);

                    var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
                    var stack = new StackPanel();

                    for (int i = 0; i < slides.Count; i++)
                    {
                        var s = slides[i];
                        var card = new Border
                        {
                            Background = Brushes.White,
                            BorderBrush = (Brush)new BrushConverter().ConvertFromString("#FFE2E8F0"),
                            BorderThickness = new Thickness(1),
                            CornerRadius = new CornerRadius(8),
                            Padding = new Thickness(14, 10, 14, 10),
                            Margin = new Thickness(0, 0, 0, 10)
                        };

                        var cardStack = new StackPanel();
                        var topRow = new DockPanel();
                        var pointsText = new TextBlock
                        {
                            Text = $"+{s.Points} pts",
                            FontSize = 11,
                            FontWeight = FontWeights.Bold,
                            Foreground = (Brush)new BrushConverter().ConvertFromString("#FF059669")
                        };
                        DockPanel.SetDock(pointsText, Dock.Right);
                        topRow.Children.Add(pointsText);

                        var titleText = new TextBlock
                        {
                            Text = $"{i + 1}. [{s.SlideType ?? "Question"}] {s.Title ?? "Untitled"}",
                            FontSize = 13,
                            FontWeight = FontWeights.Bold,
                            Foreground = (Brush)new BrushConverter().ConvertFromString("#FF1E293B")
                        };
                        topRow.Children.Add(titleText);
                        cardStack.Children.Add(topRow);

                        if (!string.IsNullOrWhiteSpace(s.ContentText))
                        {
                            cardStack.Children.Add(new TextBlock
                            {
                                Text = s.ContentText,
                                FontSize = 12,
                                Foreground = (Brush)new BrushConverter().ConvertFromString("#FF475569"),
                                TextWrapping = TextWrapping.Wrap,
                                Margin = new Thickness(0, 4, 0, 4)
                            });
                        }

                        if (!string.IsNullOrWhiteSpace(s.CorrectAnswer))
                        {
                            cardStack.Children.Add(new TextBlock
                            {
                                Text = $"Key: {s.CorrectAnswer}",
                                FontSize = 11,
                                FontWeight = FontWeights.SemiBold,
                                Foreground = (Brush)new BrushConverter().ConvertFromString("#FF16A34A"),
                                Margin = new Thickness(0, 2, 0, 0)
                            });
                        }

                        card.Child = cardStack;
                        stack.Children.Add(card);
                    }

                    scroll.Content = stack;
                    Grid.SetRow(scroll, 1);
                    mainGrid.Children.Add(scroll);

                    var closeBtn = new Button
                    {
                        Content = "CLOSE",
                        Width = 100,
                        Height = 32,
                        HorizontalAlignment = HorizontalAlignment.Right,
                        Margin = new Thickness(0, 10, 0, 0),
                        IsCancel = true
                    };
                    closeBtn.Click += (s, e) => previewWin.Close();
                    Grid.SetRow(closeBtn, 2);
                    mainGrid.Children.Add(closeBtn);

                    previewWin.Content = mainGrid;
                    previewWin.ShowDialog();
                }
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Failed to preview quiz slides.", ex);
            }
        }

        private void HistoryToolbarButton_Click(object sender, RoutedEventArgs e)
        {
            var historyWin = new QuizSessionHistoryDialog { Owner = this };
            historyWin.ShowDialog();
            LoadDecksAndRefresh();
        }

        private void SampleDeckToolbarButton_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Load the complete General Knowledge Championship showcase sample quiz into your Quiz Library?",
                "Load Showcase Quiz",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            try
            {
                using (var db = new ScoreDbContext())
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
                    db.QuizDecks.Add(sampleDeck);
                    db.SaveChanges();

                    var slides = QuizSampleDeckSeeder.CreateSampleSlides(sampleDeck.QuizDeckId);
                    db.DeckSlides.AddRange(slides);
                    db.SaveChanges();
                }
                LoadDecksAndRefresh();
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Could not load sample deck from library.", ex);
                MessageBox.Show("Failed to load sample quiz: " + ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
