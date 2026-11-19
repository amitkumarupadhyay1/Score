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

        private void PreviewEditor_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_loadingEditor) UpdatePreview();
        }

        private void PreviewSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_loadingEditor) UpdatePreview();
        }

        private void UpdatePreview()
        {
            if (PreviewPanel == null) return;
            PreviewPanel.Children.Clear();

            var slideType = SlideTypeSelector?.SelectedItem?.ToString() ?? "Question";
            var roundName = SlideRoundNameEditor?.Text ?? "General";
            var title = string.IsNullOrWhiteSpace(SlideTitleEditor.Text) ? (slideType == "Instruction" ? "Rules / Announcement" : "Question Title") : SlideTitleEditor.Text;
            var content = string.IsNullOrWhiteSpace(SlideContentEditor.Text) ? (slideType == "Instruction" ? "Enter announcement or rules text here..." : "Enter question prompt here...") : SlideContentEditor.Text;

            var preview = new StackPanel { Margin = new Thickness(4) };

            // Stage Screen Header: Category, Badges & Timer
            var badgeRow = new WrapPanel { Margin = new Thickness(0, 0, 0, 12) };
            badgeRow.Children.Add(CreateStageBadge(roundName, "#38BDF8", "#0369A1"));

            if (slideType == "Instruction")
            {
                badgeRow.Children.Add(CreateStageBadge("INSTRUCTION", "#E2E8F0", "#334155"));
                badgeRow.Children.Add(CreateStageBadge("ANNOUNCEMENT", "#FDE047", "#854D0E"));
            }
            else if (slideType == "Media")
            {
                badgeRow.Children.Add(CreateStageBadge("MEDIA CLUE", "#FEF08A", "#854D0E"));
            }
            else if (slideType == "Timer")
            {
                badgeRow.Children.Add(CreateStageBadge("TIMER", "#BAE6FD", "#0369A1"));
                if (int.TryParse(SlideTimeLimitEditor?.Text, out var sec) && sec > 0)
                    badgeRow.Children.Add(CreateStageBadge($"{sec}s", "#93C5FD", "#1E3A8A"));
            }
            else if (slideType == "Scoreboard")
            {
                badgeRow.Children.Add(CreateStageBadge("SCOREBOARD", "#FEF08A", "#854D0E"));
            }
            else
            {
                badgeRow.Children.Add(CreateStageBadge(slideType, "#86EFAC", "#15803D"));
                badgeRow.Children.Add(CreateStageBadge(QuestionTypeSelector?.SelectedItem?.ToString() ?? "MCQ", "#E0F2FE", "#0369A1"));
                badgeRow.Children.Add(CreateStageBadge($"+{SlidePointsEditor?.Text ?? "10"} pts", "#FDE047", "#854D0E"));
                if (int.TryParse(SlideTimeLimitEditor?.Text, out var sec) && sec > 0)
                    badgeRow.Children.Add(CreateStageBadge($"{sec}s", "#93C5FD", "#1E3A8A"));
            }
            preview.Children.Add(badgeRow);

            // Headline on Stage
            var isSchoolTheme = string.Equals(_currentDeck?.Theme, "school", StringComparison.OrdinalIgnoreCase);
            var headlineBrush = isSchoolTheme ? new SolidColorBrush(Color.FromRgb(15, 23, 42)) : new SolidColorBrush(Color.FromRgb(248, 250, 252));
            var promptBrush = isSchoolTheme ? new SolidColorBrush(Color.FromRgb(51, 65, 85)) : new SolidColorBrush(Color.FromRgb(203, 213, 225));

            preview.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = slideType == "Instruction" ? 17 : 16,
                FontWeight = FontWeights.Bold,
                Foreground = headlineBrush,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            });

            if (slideType == "Instruction")
            {
                // Full Screen / Prominent Announcement Card
                var instructBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(24, 18, 43)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(168, 85, 247)),
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(14, 12, 14, 12),
                    Margin = new Thickness(0, 6, 0, 12)
                };
                var stack = new StackPanel();
                stack.Children.Add(new TextBlock
                {
                    Text = "📢 STAGE ANNOUNCEMENT / RULES",
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(216, 180, 254)),
                    Margin = new Thickness(0, 0, 0, 8)
                });
                stack.Children.Add(new TextBlock
                {
                    Text = content,
                    FontSize = 13,
                    Foreground = new SolidColorBrush(Color.FromRgb(241, 245, 249)),
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 20
                });
                instructBorder.Child = stack;
                preview.Children.Add(instructBorder);

                PreviewPanel.Children.Add(preview);

                if (HostAnswerKeyLabel != null) HostAnswerKeyLabel.Text = "📢 Instruction / Rules Slide";
                if (HostAliasesLabel != null) HostAliasesLabel.Text = "No player answers or scoring required on stage.";
                if (HostNotesLabel != null) HostNotesLabel.Text = string.IsNullOrWhiteSpace(SlideExplanationEditor?.Text) ? "Host secret notes (optional)" : SlideExplanationEditor.Text;
                return;
            }

            if (slideType == "Media")
            {
                // Prominent Media Display Simulator
                var mediaBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(30, 27, 21)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(245, 158, 11)),
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(14, 10, 14, 10),
                    Margin = new Thickness(0, 6, 0, 10)
                };
                var mediaStack = new StackPanel();
                mediaStack.Children.Add(new TextBlock
                {
                    Text = "🎬 MEDIA DISPLAY STAGE",
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(253, 224, 71)),
                    Margin = new Thickness(0, 0, 0, 6)
                });

                if (!string.IsNullOrWhiteSpace(SlideMediaPathEditor?.Text) && File.Exists(SlideMediaPathEditor.Text))
                {
                    var mediaType = GetMediaType(SlideMediaPathEditor.Text);
                    if (mediaType == "Image")
                    {
                        try
                        {
                            var img = new Image
                            {
                                Source = new BitmapImage(new Uri(SlideMediaPathEditor.Text)),
                                MaxHeight = 120,
                                Stretch = Stretch.Uniform,
                                Margin = new Thickness(0, 0, 0, 6)
                            };
                            mediaStack.Children.Add(img);
                        }
                        catch { }
                    }
                    else
                    {
                        mediaStack.Children.Add(new TextBlock
                        {
                            Text = $"🎬 [{mediaType}] {Path.GetFileName(SlideMediaPathEditor.Text)}",
                            FontSize = 12,
                            Foreground = new SolidColorBrush(Color.FromRgb(147, 197, 253)),
                            Margin = new Thickness(0, 0, 0, 6)
                        });
                    }
                }
                else
                {
                    mediaStack.Children.Add(new TextBlock
                    {
                        Text = "🎬 [Select or attach a media clue file]",
                        FontSize = 11.5,
                        FontStyle = FontStyles.Italic,
                        Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                        Margin = new Thickness(0, 0, 0, 6)
                    });
                }

                mediaStack.Children.Add(new TextBlock
                {
                    Text = content,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 18
                });
                mediaBorder.Child = mediaStack;
                preview.Children.Add(mediaBorder);

                PreviewPanel.Children.Add(preview);

                if (HostAnswerKeyLabel != null) HostAnswerKeyLabel.Text = "🎬 Media Clue Slide";
                if (HostAliasesLabel != null) HostAliasesLabel.Text = "Media playback and clue displayed on stage.";
                if (HostNotesLabel != null) HostNotesLabel.Text = string.IsNullOrWhiteSpace(SlideExplanationEditor?.Text) ? "Host secret notes (optional)" : SlideExplanationEditor.Text;
                return;
            }

            if (slideType == "Timer")
            {
                int.TryParse(SlideTimeLimitEditor?.Text, out var secVal);
                if (secVal <= 0) secVal = 30;
                var mm = secVal / 60;
                var ss = secVal % 60;
                var timeDisplay = $"{mm:D2}:{ss:D2}";

                var timerBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(16, 14, 16, 14),
                    Margin = new Thickness(0, 6, 0, 12)
                };
                var tStack = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
                tStack.Children.Add(new TextBlock
                {
                    Text = "⏱ STAGE COUNTDOWN TIMER",
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(186, 230, 253)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8)
                });
                tStack.Children.Add(new TextBlock
                {
                    Text = timeDisplay,
                    FontSize = 36,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Margin = new Thickness(0, 0, 0, 8)
                });
                tStack.Children.Add(new TextBlock
                {
                    Text = content,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                    TextWrapping = TextWrapping.Wrap,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                });
                timerBorder.Child = tStack;
                preview.Children.Add(timerBorder);

                PreviewPanel.Children.Add(preview);

                if (HostAnswerKeyLabel != null) HostAnswerKeyLabel.Text = $"⏱ Timer Slide ({secVal}s)";
                if (HostAliasesLabel != null) HostAliasesLabel.Text = "No scoring or answers required during countdown.";
                if (HostNotesLabel != null) HostNotesLabel.Text = string.IsNullOrWhiteSpace(SlideExplanationEditor?.Text) ? "Host secret notes (optional)" : SlideExplanationEditor.Text;
                return;
            }

            if (slideType == "Scoreboard")
            {
                var scoreBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(234, 179, 8)),
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(10),
                    Padding = new Thickness(16, 12, 16, 12),
                    Margin = new Thickness(0, 6, 0, 12)
                };
                var sStack = new StackPanel();
                sStack.Children.Add(new TextBlock
                {
                    Text = "INTERMISSION / SCOREBOARD PREVIEW",
                    FontSize = 10,
                    FontWeight = FontWeights.Bold,
                    Foreground = new SolidColorBrush(Color.FromRgb(253, 224, 71)),
                    Margin = new Thickness(0, 0, 0, 6)
                });
                sStack.Children.Add(new TextBlock
                {
                    Text = content,
                    FontSize = 12,
                    Foreground = new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 8)
                });

                var row1 = new TextBlock { Text = "1. Team 1 · 35 pts", FontSize = 11.5, FontWeight = FontWeights.SemiBold, Foreground = new SolidColorBrush(Color.FromRgb(250, 204, 21)), Margin = new Thickness(0, 2, 0, 2) };
                var row2 = new TextBlock { Text = "2. Team 2 · 30 pts", FontSize = 11.5, Foreground = new SolidColorBrush(Color.FromRgb(203, 213, 225)), Margin = new Thickness(0, 2, 0, 2) };
                sStack.Children.Add(row1);
                sStack.Children.Add(row2);
                scoreBorder.Child = sStack;
                preview.Children.Add(scoreBorder);

                PreviewPanel.Children.Add(preview);

                if (HostAnswerKeyLabel != null) HostAnswerKeyLabel.Text = "Scoreboard / Standings Slide";
                if (HostAliasesLabel != null) HostAliasesLabel.Text = "Current team scores and rankings displayed on stage.";
                if (HostNotesLabel != null) HostNotesLabel.Text = string.IsNullOrWhiteSpace(SlideExplanationEditor?.Text) ? "Host secret notes (optional)" : SlideExplanationEditor.Text;
                return;
            }

            // Normal Question Prompt
            preview.Children.Add(new TextBlock
            {
                Text = content,
                FontSize = 12.5,
                Foreground = promptBrush,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 18,
                Margin = new Thickness(0, 0, 0, 14)
            });

            // Choices for MCQ / MultiSelect
            var qType = QuestionTypeSelector?.SelectedItem?.ToString();
            var options = QuizScoringEngine.ParseOptions(SlideOptionsEditor.Text);
            if (options.Count > 0 && (qType == QuizScoringEngine.QuestionMultipleChoice || qType == QuizScoringEngine.QuestionMultipleSelect))
            {
                var optPanel = new StackPanel { Margin = new Thickness(0, 0, 0, 12) };
                for (var i = 0; i < options.Count; i++)
                {
                    var letter = (char)('A' + i);
                    var optText = options[i];
                    var isCorrect = !string.IsNullOrWhiteSpace(SlideCorrectAnswerEditor.Text) &&
                                    (string.Equals(optText, SlideCorrectAnswerEditor.Text.Trim(), StringComparison.OrdinalIgnoreCase) ||
                                     QuizAIGeneratorService.MatchesMcqOption(SlideCorrectAnswerEditor.Text, options, out var matched) && string.Equals(optText, matched, StringComparison.OrdinalIgnoreCase));

                    var optBorder = new Border
                    {
                        Background = isCorrect ? new SolidColorBrush(Color.FromRgb(19, 42, 31)) : new SolidColorBrush(Color.FromRgb(19, 28, 49)),
                        BorderBrush = isCorrect ? new SolidColorBrush(Color.FromRgb(34, 197, 94)) : new SolidColorBrush(Color.FromRgb(36, 51, 83)),
                        BorderThickness = isCorrect ? new Thickness(1.5) : new Thickness(1),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(8, 5, 8, 5),
                        Margin = new Thickness(0, 0, 0, 4)
                    };

                    var grid = new Grid();
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(22) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                    var letterBadge = new Border
                    {
                        Background = isCorrect ? new SolidColorBrush(Color.FromRgb(34, 197, 94)) : new SolidColorBrush(Color.FromRgb(30, 41, 59)),
                        CornerRadius = new CornerRadius(4),
                        Width = 18,
                        Height = 18,
                        VerticalAlignment = VerticalAlignment.Center,
                        Child = new TextBlock
                        {
                            Text = letter.ToString(),
                            FontSize = 10,
                            FontWeight = FontWeights.Bold,
                            Foreground = isCorrect ? new SolidColorBrush(Color.FromRgb(15, 23, 42)) : new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    };
                    Grid.SetColumn(letterBadge, 0);
                    grid.Children.Add(letterBadge);

                    var textBlock = new TextBlock
                    {
                        Text = optText,
                        FontSize = 11.5,
                        FontWeight = isCorrect ? FontWeights.SemiBold : FontWeights.Normal,
                        Foreground = isCorrect ? new SolidColorBrush(Color.FromRgb(220, 252, 231)) : new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(6, 0, 0, 0),
                        TextWrapping = TextWrapping.Wrap
                    };
                    Grid.SetColumn(textBlock, 1);
                    grid.Children.Add(textBlock);

                    if (isCorrect)
                    {
                        var checkBadge = new TextBlock
                        {
                            Text = "Key",
                            FontSize = 10,
                            FontWeight = FontWeights.Bold,
                            Foreground = new SolidColorBrush(Color.FromRgb(74, 222, 128)),
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(6, 0, 0, 0)
                        };
                        Grid.SetColumn(checkBadge, 2);
                        grid.Children.Add(checkBadge);
                    }

                    optBorder.Child = grid;
                    optPanel.Children.Add(optBorder);
                }
                preview.Children.Add(optPanel);
            }
            else if (qType == QuizScoringEngine.QuestionTrueFalse)
            {
                var tfGrid = new Grid { Margin = new Thickness(0, 0, 0, 12) };
                tfGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                tfGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var isTrueCorrect = string.Equals(SlideCorrectAnswerEditor.Text, "True", StringComparison.OrdinalIgnoreCase);
                var isFalseCorrect = string.Equals(SlideCorrectAnswerEditor.Text, "False", StringComparison.OrdinalIgnoreCase);

                var trueBorder = new Border
                {
                    Background = isTrueCorrect ? new SolidColorBrush(Color.FromRgb(19, 42, 31)) : new SolidColorBrush(Color.FromRgb(19, 28, 49)),
                    BorderBrush = isTrueCorrect ? new SolidColorBrush(Color.FromRgb(34, 197, 94)) : new SolidColorBrush(Color.FromRgb(36, 51, 83)),
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(8, 6, 8, 6),
                    Margin = new Thickness(0, 0, 4, 0),
                    Child = new TextBlock
                    {
                        Text = "TRUE" + (isTrueCorrect ? " (Key)" : ""),
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        Foreground = isTrueCorrect ? new SolidColorBrush(Color.FromRgb(74, 222, 128)) : new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                };
                Grid.SetColumn(trueBorder, 0);
                tfGrid.Children.Add(trueBorder);

                var falseBorder = new Border
                {
                    Background = isFalseCorrect ? new SolidColorBrush(Color.FromRgb(49, 19, 25)) : new SolidColorBrush(Color.FromRgb(19, 28, 49)),
                    BorderBrush = isFalseCorrect ? new SolidColorBrush(Color.FromRgb(239, 68, 68)) : new SolidColorBrush(Color.FromRgb(36, 51, 83)),
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(6),
                    Padding = new Thickness(8, 6, 8, 6),
                    Margin = new Thickness(4, 0, 0, 0),
                    Child = new TextBlock
                    {
                        Text = "FALSE" + (isFalseCorrect ? " (Key)" : ""),
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        Foreground = isFalseCorrect ? new SolidColorBrush(Color.FromRgb(252, 165, 165)) : new SolidColorBrush(Color.FromRgb(203, 213, 225)),
                        HorizontalAlignment = HorizontalAlignment.Center
                    }
                };
                Grid.SetColumn(falseBorder, 1);
                tfGrid.Children.Add(falseBorder);

                preview.Children.Add(tfGrid);
            }

            // Hints / Clues banner preview
            if (!string.IsNullOrWhiteSpace(SlideHintsEditor?.Text))
            {
                var hints = SlideHintsEditor.Text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (hints.Length > 0)
                {
                    var hintBorder = new Border
                    {
                        Background = new SolidColorBrush(Color.FromRgb(30, 27, 21)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(161, 98, 7)),
                        BorderThickness = new Thickness(1),
                        CornerRadius = new CornerRadius(6),
                        Padding = new Thickness(8, 6, 8, 6),
                        Margin = new Thickness(0, 0, 0, 10)
                    };
                    var hintStack = new StackPanel();
                    hintStack.Children.Add(new TextBlock
                    {
                        Text = $"Clue Available (1 of {hints.Length}):",
                        FontSize = 9.5,
                        FontWeight = FontWeights.Bold,
                        Foreground = new SolidColorBrush(Color.FromRgb(253, 224, 71))
                    });
                    hintStack.Children.Add(new TextBlock
                    {
                        Text = "• " + hints[0].Trim(),
                        FontSize = 11,
                        Foreground = new SolidColorBrush(Color.FromRgb(254, 240, 138)),
                        TextWrapping = TextWrapping.Wrap,
                        Margin = new Thickness(0, 2, 0, 0)
                    });
                    hintBorder.Child = hintStack;
                    preview.Children.Add(hintBorder);
                }
            }

            // Media preview thumbnail
            if (!string.IsNullOrWhiteSpace(SlideMediaPathEditor?.Text) && File.Exists(SlideMediaPathEditor.Text))
            {
                var mediaType = GetMediaType(SlideMediaPathEditor.Text);
                if (mediaType == "Image")
                {
                    try
                    {
                        var img = new Image
                        {
                            Source = new BitmapImage(new Uri(SlideMediaPathEditor.Text)),
                            MaxHeight = 110,
                            Stretch = Stretch.Uniform,
                            Margin = new Thickness(0, 0, 0, 8)
                        };
                        preview.Children.Add(img);
                    }
                    catch
                    {
                        // Ignore decode error
                    }
                }
                else
                {
                    preview.Children.Add(new TextBlock
                    {
                        Text = $"🎬 [{mediaType}] {Path.GetFileName(SlideMediaPathEditor.Text)}",
                        FontSize = 11,
                        Foreground = new SolidColorBrush(Color.FromRgb(129, 140, 248)),
                        Margin = new Thickness(0, 0, 0, 8)
                    });
                }
            }

            RenderStageRulesPreview(preview, slideType);
            PreviewPanel.Children.Add(preview);

            // Update Host Teleprompter Card below Stage Screen
            if (HostAnswerKeyLabel != null)
            {
                var ans = SlideCorrectAnswerEditor?.Text?.Trim();
                HostAnswerKeyLabel.Text = "Answer: " + (string.IsNullOrWhiteSpace(ans) ? "(No answer set)" : ans);
            }
            if (HostAliasesLabel != null)
            {
                var aliases = SlideAlternateAnswersEditor?.Text?.Trim();
                HostAliasesLabel.Text = "Aliases: " + (string.IsNullOrWhiteSpace(aliases) ? "None" : aliases);
            }
            if (HostNotesLabel != null)
            {
                var notes = SlideExplanationEditor?.Text?.Trim();
                var promptOn = SlidePromptOnEditor?.Text?.Trim();
                var doNotAccept = SlideDoNotAcceptEditor?.Text?.Trim();
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(promptOn)) parts.Add("PROMPT ON: " + promptOn);
                if (!string.IsNullOrWhiteSpace(doNotAccept)) parts.Add("DO NOT ACCEPT: " + doNotAccept);
                if (!string.IsNullOrWhiteSpace(notes)) parts.Add(notes);
                HostNotesLabel.Text = parts.Count > 0 ? string.Join("\n", parts) : "No host secret trivia.";
            }
        }

        private static Border CreateStageBadge(string text, string fgHex, string bgHex)
        {
            var border = new Border
            {
                Background = (Brush)new BrushConverter().ConvertFromString(bgHex),
                CornerRadius = new CornerRadius(4),
                Padding = new Thickness(6, 2, 6, 2),
                Margin = new Thickness(0, 0, 6, 4)
            };
            border.Child = new TextBlock
            {
                Text = text,
                FontSize = 9.5,
                FontWeight = FontWeights.Bold,
                Foreground = (Brush)new BrushConverter().ConvertFromString(fgHex)
            };
            return border;
        }
    }
}
