using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Score
{
    public enum PreflightLevel { Pass, Warning, Error }

    public class PreflightIssue
    {
        public PreflightLevel Level { get; set; }
        public int? SlideOrder { get; set; }
        public string SlideTitle { get; set; }
        public string Category { get; set; }
        public string Message { get; set; }
        public string Detail { get; set; }

        public string LevelBadge => Level == PreflightLevel.Error ? "ERROR" : Level == PreflightLevel.Warning ? "WARNING" : "PASS";
    }

    public class PreflightReport
    {
        public int QuizDeckId { get; set; }
        public string DeckTitle { get; set; }
        public string Theme { get; set; }
        public int TotalSlides { get; set; }
        public int TotalQuestions { get; set; }
        public int TotalPoints { get; set; }
        public DateTime ValidatedAtUtc { get; set; } = DateTime.UtcNow;
        public List<PreflightIssue> Issues { get; set; } = new List<PreflightIssue>();

        public int ErrorCount => Issues.Count(i => i.Level == PreflightLevel.Error);
        public int WarningCount => Issues.Count(i => i.Level == PreflightLevel.Warning);
        public int PassCount => Issues.Count(i => i.Level == PreflightLevel.Pass);
        public bool IsStageReady => ErrorCount == 0 && TotalQuestions > 0;

        public string Summary
        {
            get
            {
                if (IsStageReady)
                    return WarningCount == 0
                        ? $"Ready for live stage: 100% stage ready with {PassCount} passed checks ({TotalQuestions} questions, {TotalPoints} pts)."
                        : $"Ready for live stage: {PassCount} passed checks with {WarningCount} advisory warning(s) and 0 critical errors.";
                if (TotalQuestions == 0)
                    return $"Not stage ready: Deck has no question slides configured ({ErrorCount} error(s)).";
                return $"Not stage ready: {ErrorCount} critical error(s) must be resolved before conducting on live stage ({WarningCount} warning(s), {PassCount} passed).";
            }
        }
    }

    public static class QuizPreflightValidator
    {
        public static PreflightReport ValidateDeck(int deckId)
        {
            try
            {
                using (var db = new ScoreDbContext())
                {
                    var deck = db.QuizDecks.FirstOrDefault(d => d.QuizDeckId == deckId);
                    if (deck == null)
                    {
                        var rep = new PreflightReport { QuizDeckId = deckId, DeckTitle = $"Deck #{deckId} (Not Found)" };
                        rep.Issues.Add(new PreflightIssue { Level = PreflightLevel.Error, Category = "Deck Setup", Message = $"Quiz deck #{deckId} was not found in the database." });
                        return rep;
                    }
                    var slides = db.DeckSlides.Where(s => s.QuizDeckId == deckId && s.IsActive).OrderBy(s => s.SlideOrder).ToList();
                    return ValidateDeck(deck, slides);
                }
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException($"Preflight validation failed for deck #{deckId}.", ex);
                var errReport = new PreflightReport { QuizDeckId = deckId, DeckTitle = $"Deck #{deckId}" };
                errReport.Issues.Add(new PreflightIssue { Level = PreflightLevel.Error, Category = "System Error", Message = "Database query failure: " + ex.Message });
                return errReport;
            }
        }

        internal static PreflightReport ValidateDeck(QuizDeck deck, List<DeckSlide> slides)
        {
            var report = new PreflightReport
            {
                QuizDeckId = deck?.QuizDeckId ?? 0,
                DeckTitle = string.IsNullOrWhiteSpace(deck?.Title) ? "Untitled Quiz" : deck.Title,
                Theme = deck?.Theme ?? "Classic",
                TotalSlides = slides?.Count ?? 0
            };

            if (deck != null && !deck.IsActive)
                report.Issues.Add(new PreflightIssue { Level = PreflightLevel.Error, Category = "Deck Status", Message = "Quiz deck is marked inactive." });

            if (slides == null || slides.Count == 0)
            {
                report.Issues.Add(new PreflightIssue { Level = PreflightLevel.Error, Category = "Deck Setup", Message = "Quiz deck has no active slides.", Detail = "Add at least one question slide before conducting live." });
                return report;
            }

            var questionSlides = slides.Where(s => string.Equals(s.SlideType ?? "Question", "Question", StringComparison.OrdinalIgnoreCase)).ToList();
            report.TotalQuestions = questionSlides.Count;
            report.TotalPoints = questionSlides.Sum(s => s.Points);

            if (questionSlides.Count == 0)
            {
                report.Issues.Add(new PreflightIssue { Level = PreflightLevel.Error, Category = "Deck Setup", Message = "Deck contains 0 question slides.", Detail = "At least one question is required." });
            }
            else
            {
                report.Issues.Add(new PreflightIssue { Level = PreflightLevel.Pass, Category = "Deck Setup", Message = $"Deck contains {questionSlides.Count} question slide(s) ({report.TotalPoints} total points).", Detail = $"Visual theme: '{report.Theme}'." });
            }

            for (int i = 0; i < slides.Count; i++)
            {
                var slide = slides[i];
                var slideNum = slide.SlideOrder > 0 ? slide.SlideOrder : (i + 1);
                var title = string.IsNullOrWhiteSpace(slide.Title) ? $"Slide #{slideNum}" : slide.Title;
                var slideType = slide.SlideType ?? "Question";
                var isQuestion = string.Equals(slideType, "Question", StringComparison.OrdinalIgnoreCase);

                // 1. Referenced MediaPath file validation
                if (!string.IsNullOrWhiteSpace(slide.MediaPath))
                {
                    var cleanPath = slide.MediaPath.Trim();
                    try
                    {
                        if (File.Exists(cleanPath))
                            report.Issues.Add(new PreflightIssue { Level = PreflightLevel.Pass, SlideOrder = slideNum, SlideTitle = title, Category = "Media Asset", Message = $"Media asset verified: {Path.GetFileName(cleanPath)}", Detail = cleanPath });
                        else
                            report.Issues.Add(new PreflightIssue { Level = PreflightLevel.Error, SlideOrder = slideNum, SlideTitle = title, Category = "Media Asset", Message = $"Broken asset: Referenced media file '{Path.GetFileName(cleanPath)}' does not exist on disk.", Detail = $"Missing path: {cleanPath}" });
                    }
                    catch (Exception ex)
                    {
                        report.Issues.Add(new PreflightIssue { Level = PreflightLevel.Error, SlideOrder = slideNum, SlideTitle = title, Category = "Media Asset", Message = $"Broken asset path on slide #{slideNum}.", Detail = ex.Message });
                    }
                }
                else if (string.Equals(slideType, "Media", StringComparison.OrdinalIgnoreCase))
                {
                    report.Issues.Add(new PreflightIssue { Level = PreflightLevel.Warning, SlideOrder = slideNum, SlideTitle = title, Category = "Media Asset", Message = $"Media slide #{slideNum} has no media file assigned." });
                }

                // 2. Question checks
                if (isQuestion)
                {
                    var qType = slide.QuestionType ?? QuizScoringEngine.QuestionMultipleChoice;

                    if (string.IsNullOrWhiteSpace(slide.ContentText))
                        report.Issues.Add(new PreflightIssue { Level = PreflightLevel.Warning, SlideOrder = slideNum, SlideTitle = title, Category = "Prompt", Message = $"Question prompt text is empty on slide #{slideNum}." });

                    // Non-empty CorrectAnswer check
                    if (string.IsNullOrWhiteSpace(slide.CorrectAnswer))
                        report.Issues.Add(new PreflightIssue { Level = PreflightLevel.Error, SlideOrder = slideNum, SlideTitle = title, Category = "Answer Key", Message = $"Missing answer key: Slide #{slideNum} ('{title}') has no correct answer configured.", Detail = "Automated scoring and answer reveal will fail." });
                    else
                        report.Issues.Add(new PreflightIssue { Level = PreflightLevel.Pass, SlideOrder = slideNum, SlideTitle = title, Category = "Answer Key", Message = $"Answer key configured for slide #{slideNum}.", Detail = $"Answer: {slide.CorrectAnswer}" });

                    // MultipleChoice checks
                    if (string.Equals(qType, QuizScoringEngine.QuestionMultipleChoice, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(qType, QuizScoringEngine.QuestionMultipleSelect, StringComparison.OrdinalIgnoreCase))
                    {
                        var options = QuizScoringEngine.ParseOptions(slide.OptionsJson);
                        if (options.Count < 2)
                        {
                            report.Issues.Add(new PreflightIssue { Level = PreflightLevel.Error, SlideOrder = slideNum, SlideTitle = title, Category = "Choices", Message = $"Insufficient choices: Multiple choice slide #{slideNum} has {options.Count} option(s). At least 2 are required.", Detail = options.Count == 1 ? $"Only option: '{options[0]}'." : "No options found." });
                        }
                        else
                        {
                            report.Issues.Add(new PreflightIssue { Level = PreflightLevel.Pass, SlideOrder = slideNum, SlideTitle = title, Category = "Choices", Message = $"Multiple choice options valid ({options.Count} choices) for slide #{slideNum}.", Detail = string.Join(" | ", options) });
                            if (!string.IsNullOrWhiteSpace(slide.CorrectAnswer) && string.Equals(qType, QuizScoringEngine.QuestionMultipleChoice, StringComparison.OrdinalIgnoreCase) && !QuizAIGeneratorService.MatchesMcqOption(slide.CorrectAnswer, options, out _))
                            {
                                report.Issues.Add(new PreflightIssue { Level = PreflightLevel.Warning, SlideOrder = slideNum, SlideTitle = title, Category = "Answer Alignment", Message = $"Answer alignment warning: Correct answer '{slide.CorrectAnswer}' does not strictly match any option on slide #{slideNum}." });
                            }
                        }
                    }
                    else if (string.Equals(qType, QuizScoringEngine.QuestionTrueFalse, StringComparison.OrdinalIgnoreCase))
                    {
                        if (!string.IsNullOrWhiteSpace(slide.CorrectAnswer) && !QuizAIGeneratorService.IsBooleanAnswer(slide.CorrectAnswer, out _))
                            report.Issues.Add(new PreflightIssue { Level = PreflightLevel.Warning, SlideOrder = slideNum, SlideTitle = title, Category = "Answer Alignment", Message = $"True/False answer '{slide.CorrectAnswer}' on slide #{slideNum} is not 'True' or 'False'." });
                    }

                    // Non-zero points check
                    if (slide.Points == 0)
                        report.Issues.Add(new PreflightIssue { Level = PreflightLevel.Warning, SlideOrder = slideNum, SlideTitle = title, Category = "Points", Message = $"Zero points awarded on slide #{slideNum}.", Detail = "If intentional (e.g. tiebreaker), you can proceed." });
                    else if (slide.Points < 0)
                        report.Issues.Add(new PreflightIssue { Level = PreflightLevel.Error, SlideOrder = slideNum, SlideTitle = title, Category = "Points", Message = $"Negative base points ({slide.Points} pts) on slide #{slideNum}." });
                    else
                        report.Issues.Add(new PreflightIssue { Level = PreflightLevel.Pass, SlideOrder = slideNum, SlideTitle = title, Category = "Points", Message = $"Points configured ({slide.Points} pts) on slide #{slideNum}." });

                    // Positive time limits check
                    if (slide.TimeLimitSeconds <= 0)
                        report.Issues.Add(new PreflightIssue { Level = PreflightLevel.Warning, SlideOrder = slideNum, SlideTitle = title, Category = "Timer", Message = $"No positive time limit set ({slide.TimeLimitSeconds}s) on slide #{slideNum}." });
                    else
                        report.Issues.Add(new PreflightIssue { Level = PreflightLevel.Pass, SlideOrder = slideNum, SlideTitle = title, Category = "Timer", Message = $"Positive countdown timer ({slide.TimeLimitSeconds}s) set on slide #{slideNum}." });
                }
                else if (string.Equals(slideType, "Timer", StringComparison.OrdinalIgnoreCase))
                {
                    if (slide.TimeLimitSeconds <= 0)
                        report.Issues.Add(new PreflightIssue { Level = PreflightLevel.Error, SlideOrder = slideNum, SlideTitle = title, Category = "Timer", Message = $"Timer slide #{slideNum} has non-positive duration ({slide.TimeLimitSeconds}s)." });
                    else
                        report.Issues.Add(new PreflightIssue { Level = PreflightLevel.Pass, SlideOrder = slideNum, SlideTitle = title, Category = "Timer", Message = $"Timer slide #{slideNum} configured with {slide.TimeLimitSeconds}s duration." });
                }
            }

            return report;
        }

        public static bool? ShowPreflightDialog(int deckId, Window owner = null)
        {
            var report = ValidateDeck(deckId);
            return ShowPreflightDialog(report, owner);
        }

        public static bool? ShowPreflightDialog(PreflightReport report, Window owner = null)
        {
            var dlg = new PreflightReportDialog(report);
            if (owner != null) dlg.Owner = owner;
            return dlg.ShowDialog();
        }
    }

    public class PreflightReportDialog : Window
    {
        private readonly PreflightReport _report;
        private StackPanel _issuesContainer;
        private string _currentFilter = "ALL";

        public bool WantsConduct { get; private set; }
        public bool WantsEdit { get; private set; }

        public PreflightReportDialog(PreflightReport report)
        {
            _report = report ?? new PreflightReport();
            Title = $"Pre-Flight Validation · {_report.DeckTitle}";
            Width = 780; Height = 620; MinWidth = 650; MinHeight = 500;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Background = (Brush)new BrushConverter().ConvertFromString("#FFF8FAFC");
            FontFamily = new FontFamily("Segoe UI");
            BuildUI();
        }

        private void BuildUI()
        {
            var brushConv = new BrushConverter();
            var mainDock = new DockPanel();

            // Header Banner
            var isReady = _report.IsStageReady;
            var headerBorder = new Border
            {
                Background = (Brush)brushConv.ConvertFromString(isReady ? "#065F46" : "#991B1B"),
                Padding = new Thickness(24, 16, 24, 16)
            };
            DockPanel.SetDock(headerBorder, Dock.Top);

            var headerStack = new StackPanel();
            var statusRow = new StackPanel { Orientation = Orientation.Horizontal };
            statusRow.Children.Add(new TextBlock { Text = isReady ? "STAGE READY FOR LIVE BROADCAST" : "STAGE READINESS FAILED — CRITICAL ERRORS", FontSize = 16, FontWeight = FontWeights.Bold, Foreground = Brushes.White, VerticalAlignment = VerticalAlignment.Center });
            headerStack.Children.Add(statusRow);
            headerStack.Children.Add(new TextBlock { Text = $"Quiz: \"{_report.DeckTitle}\"  •  {_report.TotalQuestions} Questions  •  {_report.TotalPoints} Points  •  {_report.TotalSlides} Total Slides", FontSize = 12, Foreground = (Brush)brushConv.ConvertFromString("#E2E8F0"), Margin = new Thickness(0, 4, 0, 0) });
            headerBorder.Child = headerStack;
            mainDock.Children.Add(headerBorder);

            // Metrics / Filter Strip
            var filterBorder = new Border { Background = Brushes.White, BorderBrush = (Brush)brushConv.ConvertFromString("#E2E8F0"), BorderThickness = new Thickness(0, 0, 0, 1), Padding = new Thickness(24, 10, 24, 10) };
            DockPanel.SetDock(filterBorder, Dock.Top);
            var statsRow = new StackPanel { Orientation = Orientation.Horizontal };
            statsRow.Children.Add(CreateStatBadge($"{_report.PassCount} PASSED", "#ECFDF5", "#047857", "#A7F3D0", () => SetFilter("PASS")));
            statsRow.Children.Add(CreateStatBadge($"{_report.WarningCount} WARNINGS", "#FEF3C7", "#B45309", "#FDE68A", () => SetFilter("WARNING")));
            statsRow.Children.Add(CreateStatBadge($"{_report.ErrorCount} ERRORS", "#FEE2E2", "#B91C1C", "#FECACA", () => SetFilter("ERROR")));
            statsRow.Children.Add(CreateStatBadge($"ALL ({_report.Issues.Count})", "#F1F5F9", "#475569", "#CBD5E1", () => SetFilter("ALL")));
            filterBorder.Child = statsRow;
            mainDock.Children.Add(filterBorder);

            // Footer Bar
            var footerBorder = new Border { Background = Brushes.White, BorderBrush = (Brush)brushConv.ConvertFromString("#E2E8F0"), BorderThickness = new Thickness(0, 1, 0, 0), Padding = new Thickness(20, 12, 20, 12) };
            DockPanel.SetDock(footerBorder, Dock.Bottom);
            var footerDock = new DockPanel();
            var actionButtons = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right };
            DockPanel.SetDock(actionButtons, Dock.Right);

            if (_report.IsStageReady)
            {
                var conductBtn = new Button { Content = "CONDUCT NOW", Background = (Brush)brushConv.ConvertFromString("#16A34A"), BorderBrush = (Brush)brushConv.ConvertFromString("#15803D"), Foreground = Brushes.White, FontWeight = FontWeights.Bold, Height = 36, Padding = new Thickness(16, 0, 16, 0), Margin = new Thickness(0, 0, 10, 0), Cursor = Cursors.Hand };
                conductBtn.Click += (s, e) => { WantsConduct = true; DialogResult = true; Close(); };
                actionButtons.Children.Add(conductBtn);
            }
            else
            {
                var editBtn = new Button { Content = "FIX IN QUIZ MAKER", Background = (Brush)brushConv.ConvertFromString("#005A9C"), BorderBrush = (Brush)brushConv.ConvertFromString("#004377"), Foreground = Brushes.White, FontWeight = FontWeights.Bold, Height = 36, Padding = new Thickness(16, 0, 16, 0), Margin = new Thickness(0, 0, 10, 0), Cursor = Cursors.Hand };
                editBtn.Click += (s, e) => { WantsEdit = true; DialogResult = false; Close(); };
                actionButtons.Children.Add(editBtn);
            }

            var closeBtn = new Button { Content = "CLOSE", Height = 36, Padding = new Thickness(16, 0, 16, 0), Background = (Brush)brushConv.ConvertFromString("#F8FAFC"), BorderBrush = (Brush)brushConv.ConvertFromString("#CBD5E1"), Foreground = (Brush)brushConv.ConvertFromString("#334155"), FontWeight = FontWeights.SemiBold, Cursor = Cursors.Hand };
            closeBtn.Click += (s, e) => Close();
            actionButtons.Children.Add(closeBtn);
            footerDock.Children.Add(actionButtons);

            var summaryText = new TextBlock { Text = _report.Summary, FontSize = 11, Foreground = (Brush)brushConv.ConvertFromString(isReady ? "#047857" : "#B91C1C"), FontWeight = FontWeights.SemiBold, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap };
            footerDock.Children.Add(summaryText);
            footerBorder.Child = footerDock;
            mainDock.Children.Add(footerBorder);

            // Body
            var scroll = new ScrollViewer { VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Padding = new Thickness(24, 16, 24, 16) };
            _issuesContainer = new StackPanel();
            RenderIssues();
            scroll.Content = _issuesContainer;
            mainDock.Children.Add(scroll);

            Content = mainDock;
        }

        private Border CreateStatBadge(string text, string bg, string fg, string border, Action onClick)
        {
            var brushConv = new BrushConverter();
            var b = new Border { Background = (Brush)brushConv.ConvertFromString(bg), BorderBrush = (Brush)brushConv.ConvertFromString(border), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(6), Padding = new Thickness(10, 4, 10, 4), Margin = new Thickness(0, 0, 8, 0), Cursor = Cursors.Hand };
            b.Child = new TextBlock { Text = text, FontSize = 11, FontWeight = FontWeights.Bold, Foreground = (Brush)brushConv.ConvertFromString(fg) };
            b.MouseLeftButtonDown += (s, e) => onClick();
            return b;
        }

        private void SetFilter(string filter) { _currentFilter = filter; RenderIssues(); }

        private void RenderIssues()
        {
            _issuesContainer.Children.Clear();
            var brushConv = new BrushConverter();
            var issues = _report.Issues.AsEnumerable();
            if (_currentFilter == "ERROR") issues = issues.Where(i => i.Level == PreflightLevel.Error);
            else if (_currentFilter == "WARNING") issues = issues.Where(i => i.Level == PreflightLevel.Warning);
            else if (_currentFilter == "PASS") issues = issues.Where(i => i.Level == PreflightLevel.Pass);

            var list = issues.ToList();
            if (list.Count == 0)
            {
                _issuesContainer.Children.Add(new TextBlock { Text = $"No issues found for filter '{_currentFilter}'.", FontSize = 13, Foreground = (Brush)brushConv.ConvertFromString("#64748B"), Margin = new Thickness(0, 20, 0, 0), HorizontalAlignment = HorizontalAlignment.Center });
                return;
            }

            foreach (var issue in list)
            {
                var stripeColor = issue.Level == PreflightLevel.Error ? "#EF4444" : issue.Level == PreflightLevel.Warning ? "#F59E0B" : "#10B981";
                var bgBadge = issue.Level == PreflightLevel.Error ? "#FEE2E2" : issue.Level == PreflightLevel.Warning ? "#FEF3C7" : "#ECFDF5";
                var fgBadge = issue.Level == PreflightLevel.Error ? "#991B1B" : issue.Level == PreflightLevel.Warning ? "#92400E" : "#065F46";

                var card = new Border { Background = Brushes.White, BorderBrush = (Brush)brushConv.ConvertFromString("#E2E8F0"), BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Margin = new Thickness(0, 0, 0, 8), Padding = new Thickness(12, 10, 12, 10) };
                var grid = new Grid();
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(4) });
                grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

                var stripe = new Border { Background = (Brush)brushConv.ConvertFromString(stripeColor), CornerRadius = new CornerRadius(2) };
                Grid.SetColumn(stripe, 0);
                grid.Children.Add(stripe);

                var contentStack = new StackPanel { Margin = new Thickness(10, 0, 0, 0) };
                var metaRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };

                var badge = new Border { Background = (Brush)brushConv.ConvertFromString(bgBadge), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(0, 0, 8, 0) };
                badge.Child = new TextBlock { Text = issue.LevelBadge, FontSize = 9.5, FontWeight = FontWeights.Bold, Foreground = (Brush)brushConv.ConvertFromString(fgBadge) };
                metaRow.Children.Add(badge);

                if (!string.IsNullOrEmpty(issue.Category))
                {
                    var catPill = new Border { Background = (Brush)brushConv.ConvertFromString("#F1F5F9"), CornerRadius = new CornerRadius(4), Padding = new Thickness(6, 1, 6, 1), Margin = new Thickness(0, 0, 8, 0) };
                    catPill.Child = new TextBlock { Text = issue.Category.ToUpperInvariant(), FontSize = 9.5, FontWeight = FontWeights.SemiBold, Foreground = (Brush)brushConv.ConvertFromString("#475569") };
                    metaRow.Children.Add(catPill);
                }

                if (issue.SlideOrder.HasValue)
                {
                    metaRow.Children.Add(new TextBlock { Text = $"Slide #{issue.SlideOrder.Value}" + (!string.IsNullOrEmpty(issue.SlideTitle) ? $" · {issue.SlideTitle}" : string.Empty), FontSize = 10, FontWeight = FontWeights.SemiBold, Foreground = (Brush)brushConv.ConvertFromString("#64748B"), VerticalAlignment = VerticalAlignment.Center });
                }

                contentStack.Children.Add(metaRow);
                contentStack.Children.Add(new TextBlock { Text = issue.Message, FontSize = 12, FontWeight = FontWeights.SemiBold, Foreground = (Brush)brushConv.ConvertFromString("#0F172A"), TextWrapping = TextWrapping.Wrap });

                if (!string.IsNullOrEmpty(issue.Detail))
                {
                    contentStack.Children.Add(new TextBlock { Text = issue.Detail, FontSize = 11, Foreground = (Brush)brushConv.ConvertFromString("#64748B"), Margin = new Thickness(0, 2, 0, 0), TextWrapping = TextWrapping.Wrap });
                }

                Grid.SetColumn(contentStack, 1);
                grid.Children.Add(contentStack);
                card.Child = grid;
                _issuesContainer.Children.Add(card);
            }
        }
    }
}
