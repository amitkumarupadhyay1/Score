using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Score
{
    public class StageRulesData
    {
        public string ModeBadge { get; set; } = "Standard";
        public int Points { get; set; } = 10;
        public int BouncePoints { get; set; } = 5;
        public int Penalty { get; set; } = 0;
        public int TimeSeconds { get; set; } = 15;
        public bool ConferringAllowed { get; set; } = true;
        public bool InfiniteBounce { get; set; } = true;

        public string ToJson()
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "{{\"modeBadge\":\"{0}\",\"points\":{1},\"bouncePoints\":{2},\"penalty\":{3},\"timeSeconds\":{4},\"conferringAllowed\":{5},\"infiniteBounce\":{6}}}",
                EscapeJson(ModeBadge ?? "Standard"),
                Points,
                BouncePoints,
                Penalty,
                TimeSeconds,
                ConferringAllowed ? "true" : "false",
                InfiniteBounce ? "true" : "false"
            );
        }

        public static StageRulesData FromJson(string json)
        {
            var data = new StageRulesData();
            if (string.IsNullOrWhiteSpace(json)) return data;

            try
            {
                data.ModeBadge = ExtractString(json, "modeBadge") ?? ExtractString(json, "mode") ?? "Standard";
                data.Points = ExtractInt(json, "points", 10);
                data.BouncePoints = ExtractInt(json, "bouncePoints", 5);
                data.Penalty = ExtractInt(json, "penalty", 0);
                data.TimeSeconds = ExtractInt(json, "timeSeconds", 15);
                if (data.TimeSeconds == 15 && json.IndexOf("timeLimit", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    data.TimeSeconds = ExtractInt(json, "timeLimit", 15);
                }
                data.ConferringAllowed = ExtractBool(json, "conferringAllowed", true);
                data.InfiniteBounce = ExtractBool(json, "infiniteBounce", true);
            }
            catch
            {
                // Fall back to defaults on parse exception
            }

            return data;
        }

        private static string EscapeJson(string s)
        {
            if (string.IsNullOrEmpty(s)) return string.Empty;
            return s.Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static string ExtractString(string json, string key)
        {
            var match = Regex.Match(json, "\"" + key + "\"\\s*:\\s*\"([^\"]*)\"", RegexOptions.IgnoreCase);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static int ExtractInt(string json, string key, int fallback)
        {
            var match = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(-?\\d+)", RegexOptions.IgnoreCase);
            return match.Success && int.TryParse(match.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var val) ? val : fallback;
        }

        private static bool ExtractBool(string json, string key, bool fallback)
        {
            var match = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
            return match.Success ? bool.Parse(match.Groups[1].Value) : fallback;
        }
    }

    public partial class QuizDeckBuilderWindow : Window
    {
        internal void LoadSlideIntoEditor(DeckSlide slide)
        {
            if (slide == null) return;

            LoadRoundIntoEditor(slide);

            // Questions: Directives & Clue Decay
            if (SlidePromptOnEditor != null)
                SlidePromptOnEditor.Text = slide.PromptOnText ?? string.Empty;
            if (SlideDoNotAcceptEditor != null)
                SlideDoNotAcceptEditor.Text = slide.DoNotAcceptText ?? string.Empty;
            if (SlideClueDecayEditor != null)
                SlideClueDecayEditor.Text = slide.ClueDecayJson ?? string.Empty;

            var isInstruction = string.Equals(slide.SlideType, "Instruction", StringComparison.OrdinalIgnoreCase);

            if (StageRulesEnabledCheckBox != null)
            {
                StageRulesEnabledCheckBox.IsChecked = slide.HasStructuredRules;
            }

            if (slide.HasStructuredRules && !string.IsNullOrWhiteSpace(slide.StageRulesJson))
            {
                var rules = StageRulesData.FromJson(slide.StageRulesJson);
                if (StageRuleModeSelector != null) StageRuleModeSelector.SelectedItem = rules.ModeBadge;
                if (StageRulePointsEditor != null) StageRulePointsEditor.Text = rules.Points.ToString(CultureInfo.InvariantCulture);
                if (StageRuleBounceEditor != null) StageRuleBounceEditor.Text = rules.BouncePoints.ToString(CultureInfo.InvariantCulture);
                if (StageRulePenaltyEditor != null) StageRulePenaltyEditor.Text = rules.Penalty.ToString(CultureInfo.InvariantCulture);
                if (StageRuleTimeEditor != null) StageRuleTimeEditor.Text = rules.TimeSeconds.ToString(CultureInfo.InvariantCulture);
                if (StageRuleConferringCheckBox != null) StageRuleConferringCheckBox.IsChecked = rules.ConferringAllowed;
                if (StageRuleInfiniteBounceCheckBox != null) StageRuleInfiniteBounceCheckBox.IsChecked = rules.InfiniteBounce;
            }
            else
            {
                InheritDefaultsFromActiveRound(silent: true);
                if (StageRulesEnabledCheckBox != null) StageRulesEnabledCheckBox.IsChecked = slide.HasStructuredRules;
            }

            UpdateStageRulesVisibility(isInstruction);
        }

        internal void SaveCurrentSlide(DeckSlide slide)
        {
            if (slide == null) return;

            SaveRoundFromEditor(slide);

            var isInstruction = string.Equals(slide.SlideType, "Instruction", StringComparison.OrdinalIgnoreCase);
            var isQuestion = string.Equals(slide.SlideType, "Question", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(slide.SlideType);

            if (isQuestion)
            {
                slide.PromptOnText = SlidePromptOnEditor?.Text?.Trim() ?? string.Empty;
                slide.DoNotAcceptText = SlideDoNotAcceptEditor?.Text?.Trim() ?? string.Empty;
                slide.ClueDecayJson = SlideClueDecayEditor?.Text?.Trim() ?? string.Empty;
                slide.HasStructuredRules = false;
                slide.StageRulesJson = null;
            }
            else if (isInstruction)
            {
                slide.PromptOnText = null;
                slide.DoNotAcceptText = null;
                slide.ClueDecayJson = null;
                slide.HasStructuredRules = StageRulesEnabledCheckBox?.IsChecked == true;

                if (slide.HasStructuredRules)
                {
                    int.TryParse(StageRulePointsEditor?.Text, out var pts);
                    int.TryParse(StageRuleBounceEditor?.Text, out var bounce);
                    int.TryParse(StageRulePenaltyEditor?.Text, out var pen);
                    int.TryParse(StageRuleTimeEditor?.Text, out var time);

                    var rules = new StageRulesData
                    {
                        ModeBadge = StageRuleModeSelector?.SelectedItem?.ToString() ?? "Standard",
                        Points = pts,
                        BouncePoints = bounce,
                        Penalty = pen,
                        TimeSeconds = time,
                        ConferringAllowed = StageRuleConferringCheckBox?.IsChecked == true,
                        InfiniteBounce = StageRuleInfiniteBounceCheckBox?.IsChecked == true
                    };
                    slide.StageRulesJson = rules.ToJson();
                }
                else
                {
                    slide.StageRulesJson = null;
                }
            }
            else
            {
                slide.PromptOnText = null;
                slide.DoNotAcceptText = null;
                slide.ClueDecayJson = null;
                slide.HasStructuredRules = false;
                slide.StageRulesJson = null;
            }
        }

        private void StageRuleInheritButton_Click(object sender, RoutedEventArgs e)
        {
            InheritDefaultsFromActiveRound(silent: false);
        }

        private void InheritDefaultsFromActiveRound(bool silent)
        {
            QuizRound activeRound = SlideRoundSelector?.SelectedItem as QuizRound;
            if (activeRound == null && _currentDeckRounds.Count > 0)
            {
                if (_selectedSlide?.QuizRoundId.HasValue == true)
                    activeRound = _currentDeckRounds.FindMatchingRound(_selectedSlide.QuizRoundId.Value);
                if (activeRound == null) activeRound = _currentDeckRounds[0];
            }

            if (activeRound != null)
            {
                if (StageRuleModeSelector != null) StageRuleModeSelector.SelectedItem = activeRound.RoundType ?? QuizScoringEngine.RoundStandard;
                if (StageRulePointsEditor != null) StageRulePointsEditor.Text = activeRound.DefaultPoints.ToString(CultureInfo.InvariantCulture);
                if (StageRuleBounceEditor != null) StageRuleBounceEditor.Text = activeRound.DefaultBouncePoints.ToString(CultureInfo.InvariantCulture);
                if (StageRulePenaltyEditor != null) StageRulePenaltyEditor.Text = activeRound.DefaultPenalty.ToString(CultureInfo.InvariantCulture);
                if (StageRuleTimeEditor != null) StageRuleTimeEditor.Text = activeRound.DefaultTimeSeconds.ToString(CultureInfo.InvariantCulture);
                if (StageRuleConferringCheckBox != null) StageRuleConferringCheckBox.IsChecked = activeRound.ConferringAllowed;
                if (StageRuleInfiniteBounceCheckBox != null) StageRuleInfiniteBounceCheckBox.IsChecked = activeRound.InfiniteBounce;

                if (!silent)
                {
                    if (StageRulesEnabledCheckBox != null) StageRulesEnabledCheckBox.IsChecked = true;
                    StatusText.Text = $"✦ Inherited stage rules from round '{activeRound.RoundTitle}'.";
                    UpdatePreview();
                }
            }
        }

        private void StageRulesEnabled_Changed(object sender, RoutedEventArgs e)
        {
            if (_loadingEditor) return;
            var isEnabled = StageRulesEnabledCheckBox?.IsChecked == true;
            if (StageRulesFieldsGrid != null)
            {
                StageRulesFieldsGrid.IsEnabled = isEnabled;
                StageRulesFieldsGrid.Opacity = isEnabled ? 1.0 : 0.5;
            }
            UpdatePreview();
        }

        private void StageRulesCheck_Changed(object sender, RoutedEventArgs e)
        {
            if (!_loadingEditor) UpdatePreview();
        }

        internal void ClearSlideStageRules()
        {
            if (SlidePromptOnEditor != null) SlidePromptOnEditor.Text = string.Empty;
            if (SlideDoNotAcceptEditor != null) SlideDoNotAcceptEditor.Text = string.Empty;
            if (SlideClueDecayEditor != null) SlideClueDecayEditor.Text = string.Empty;
            if (StageRulesEnabledCheckBox != null) StageRulesEnabledCheckBox.IsChecked = false;
            InheritDefaultsFromActiveRound(silent: true);
        }

        internal void UpdateStageRulesVisibility(bool isInstruction)
        {
            if (CardStageRules != null)
            {
                CardStageRules.Visibility = isInstruction ? Visibility.Visible : Visibility.Collapsed;
            }
            if (DirectivesContainer != null)
            {
                DirectivesContainer.Visibility = !isInstruction ? Visibility.Visible : Visibility.Collapsed;
            }
            if (ClueDecayContainer != null)
            {
                ClueDecayContainer.Visibility = !isInstruction ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        internal void RenderStageRulesPreview(StackPanel preview, string slideType)
        {
            if (!string.Equals(slideType, "Instruction", StringComparison.OrdinalIgnoreCase)) return;
            if (StageRulesEnabledCheckBox?.IsChecked != true) return;

            var mode = StageRuleModeSelector?.SelectedItem?.ToString() ?? "Standard";
            int.TryParse(StageRulePointsEditor?.Text, out var pts);
            int.TryParse(StageRuleBounceEditor?.Text, out var bnc);
            int.TryParse(StageRulePenaltyEditor?.Text, out var pen);
            int.TryParse(StageRuleTimeEditor?.Text, out var sec);
            var conferring = StageRuleConferringCheckBox?.IsChecked == true;
            var infinite = StageRuleInfiniteBounceCheckBox?.IsChecked == true;

            var card = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(99, 102, 241)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 10, 0, 10)
            };

            var sp = new StackPanel();
            var titleRow = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
            titleRow.Children.Add(new TextBlock
            {
                Text = "✦ STAGE RULES CARD",
                FontSize = 10.5,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(129, 140, 248))
            });
            sp.Children.Add(titleRow);

            var badgeWrap = new WrapPanel { Margin = new Thickness(0, 0, 0, 6) };
            badgeWrap.Children.Add(CreateStageBadge(mode.ToUpperInvariant(), "#818CF8", "#1E1B4B"));
            badgeWrap.Children.Add(CreateStageBadge($"+{pts} PTS", "#4ADE80", "#14532D"));
            if (bnc > 0) badgeWrap.Children.Add(CreateStageBadge($"+{bnc} BOUNCE", "#FACC15", "#713F12"));
            if (pen > 0) badgeWrap.Children.Add(CreateStageBadge($"-{pen} PENALTY", "#F87171", "#7F1D1D"));
            if (sec > 0) badgeWrap.Children.Add(CreateStageBadge($"⏱ {sec}s", "#38BDF8", "#0C4A6E"));
            sp.Children.Add(badgeWrap);

            var tags = new List<string>
            {
                conferring ? "✓ Conferring Permitted" : "✗ No Conferring",
                infinite ? "✓ Infinite Bounce" : "✗ Single Chance"
            };

            var rulesText = new TextBlock
            {
                Text = string.Join("  •  ", tags),
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(148, 163, 184)),
                FontWeight = FontWeights.Medium
            };
            sp.Children.Add(rulesText);

            card.Child = sp;
            preview.Children.Add(card);
        }
    }

    internal static class QuizRoundCollectionExtensions
    {
        public static QuizRound FindMatchingRound(this IEnumerable<QuizRound> collection, int roundId)
        {
            if (collection == null) return null;
            foreach (var item in collection)
            {
                if (item.QuizRoundId == roundId) return item;
            }
            return null;
        }
    }
}
