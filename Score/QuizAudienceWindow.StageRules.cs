using System;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Score
{
    public class StageRulesModel
    {
        public string ModeBadge { get; set; } = "DIRECT WITH BOUNCE";
        public int CorrectPoints { get; set; } = 10;
        public int BouncePoints { get; set; } = 5;
        public int PenaltyPoints { get; set; } = 0;
        public int TimeLimitSeconds { get; set; } = 15;
        public bool ConferringAllowed { get; set; } = true;
        public string ConferringBadge { get; set; } = "👥 CONFERRING ALLOWED";
        public bool InfiniteBounce { get; set; } = true;
        public string Notes { get; set; }
    }

    public partial class QuizAudienceWindow
    {
        private DeckSlide _currentSlide;
        private string _activeTheme = "Stage";

        internal void UpdateStageRulesDisplay(DeckSlide slide)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdateStageRulesDisplay(slide));
                return;
            }

            _currentSlide = slide;

            if (slide != null && string.Equals(slide.SlideType, "Instruction", StringComparison.OrdinalIgnoreCase) &&
                (slide.HasStructuredRules || !string.IsNullOrWhiteSpace(slide.StageRulesJson)))
            {
                DisplayStageRulesMatrix(slide);
            }
            else
            {
                HideStageRulesMatrix();
            }
        }

        internal void DisplayStageRulesMatrix(DeckSlide slide)
        {
            if (AudienceStageRulesPanel == null) return;

            var rules = ParseStageRules(slide);

            // Hide options list so matrix card is front and center
            if (AudienceOptions != null)
            {
                AudienceOptions.Visibility = Visibility.Collapsed;
            }

            // Populate Stage Rules Matrix Card
            if (StageRulesModeBadgeText != null)
            {
                StageRulesModeBadgeText.Text = string.IsNullOrWhiteSpace(rules.ModeBadge) ? "DIRECT WITH BOUNCE" : rules.ModeBadge.ToUpperInvariant();
            }

            if (StageRulesConferringText != null)
            {
                StageRulesConferringText.Text = rules.ConferringAllowed
                    ? (string.IsNullOrWhiteSpace(rules.ConferringBadge) ? "👥 CONFERRING ALLOWED" : rules.ConferringBadge.ToUpperInvariant())
                    : "🚫 NO CONFERRING / INDIVIDUAL";
            }

            if (StageRulesDirectPointsText != null)
            {
                StageRulesDirectPointsText.Text = rules.CorrectPoints >= 0 ? $"+{rules.CorrectPoints} PTS" : $"{rules.CorrectPoints} PTS";
            }

            if (StageRulesBouncePointsText != null)
            {
                StageRulesBouncePointsText.Text = rules.BouncePoints >= 0 ? $"+{rules.BouncePoints} PTS" : $"{rules.BouncePoints} PTS";
            }

            if (StageRulesPenaltyPointsText != null)
            {
                StageRulesPenaltyPointsText.Text = rules.PenaltyPoints > 0 ? $"-{rules.PenaltyPoints} PTS" :
                                                   rules.PenaltyPoints < 0 ? $"{rules.PenaltyPoints} PTS" : "-0 PTS";
            }

            if (StageRulesTimeLimitText != null)
            {
                StageRulesTimeLimitText.Text = $"{rules.TimeLimitSeconds}s Clock";
            }

            if (StageRulesNotesBorder != null && StageRulesNotesText != null)
            {
                if (!string.IsNullOrWhiteSpace(rules.Notes))
                {
                    StageRulesNotesText.Text = rules.Notes;
                    StageRulesNotesBorder.Visibility = Visibility.Visible;
                }
                else
                {
                    StageRulesNotesBorder.Visibility = Visibility.Collapsed;
                }
            }

            ApplyStageRulesTheme(_activeTheme);
            AudienceStageRulesPanel.Visibility = Visibility.Visible;

            AnimateStageRulesCard();
        }

        public void HideStageRulesMatrix()
        {
            if (AudienceStageRulesPanel != null)
            {
                AudienceStageRulesPanel.Visibility = Visibility.Collapsed;
            }
            if (AudienceOptions != null && _currentSlide != null &&
                string.Equals(_currentSlide.SlideType ?? "Question", "Question", StringComparison.OrdinalIgnoreCase))
            {
                AudienceOptions.Visibility = Visibility.Visible;
            }
        }

        internal StageRulesModel ParseStageRules(DeckSlide slide)
        {
            var model = new StageRulesModel();
            if (slide == null) return model;

            model.CorrectPoints = slide.Points > 0 ? slide.Points : 10;
            model.PenaltyPoints = slide.PenaltyPoints;
            model.TimeLimitSeconds = slide.TimeLimitSeconds > 0 ? slide.TimeLimitSeconds : 15;
            model.ModeBadge = !string.IsNullOrWhiteSpace(slide.RoundType) ? slide.RoundType : "DIRECT WITH BOUNCE";

            var json = slide.StageRulesJson;
            if (string.IsNullOrWhiteSpace(json)) return model;

            try
            {
                var modeMatch = Regex.Match(json, @"""(?:ModeBadge|mode|Mode|roundType|RoundType)""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                if (modeMatch.Success) model.ModeBadge = modeMatch.Groups[1].Value.Trim();

                var ptsMatch = Regex.Match(json, @"""(?:CorrectPoints|correctPoints|Points|points|direct|DirectPoints)""\s*:\s*(-?\d+)", RegexOptions.IgnoreCase);
                if (ptsMatch.Success && int.TryParse(ptsMatch.Groups[1].Value, out var pts)) model.CorrectPoints = pts;

                var bncMatch = Regex.Match(json, @"""(?:BouncePoints|bouncePoints|bounce|Bounce)""\s*:\s*(-?\d+)", RegexOptions.IgnoreCase);
                if (bncMatch.Success && int.TryParse(bncMatch.Groups[1].Value, out var bnc)) model.BouncePoints = bnc;

                var penMatch = Regex.Match(json, @"""(?:PenaltyPoints|penaltyPoints|penalty|Penalty)""\s*:\s*(-?\d+)", RegexOptions.IgnoreCase);
                if (penMatch.Success && int.TryParse(penMatch.Groups[1].Value, out var pen)) model.PenaltyPoints = pen;

                var timeMatch = Regex.Match(json, @"""(?:TimeLimitSeconds|timeLimitSeconds|time|Time|TimeLimit)""\s*:\s*(\d+)", RegexOptions.IgnoreCase);
                if (timeMatch.Success && int.TryParse(timeMatch.Groups[1].Value, out var t)) model.TimeLimitSeconds = t;

                var confMatch = Regex.Match(json, @"""(?:ConferringAllowed|conferringAllowed|conferring|Conferring)""\s*:\s*(true|false)", RegexOptions.IgnoreCase);
                if (confMatch.Success && bool.TryParse(confMatch.Groups[1].Value, out var c)) model.ConferringAllowed = c;

                var confBadgeMatch = Regex.Match(json, @"""(?:ConferringBadge|conferringBadge)""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                if (confBadgeMatch.Success) model.ConferringBadge = confBadgeMatch.Groups[1].Value.Trim();

                var notesMatch = Regex.Match(json, @"""(?:CustomNotes|notes|Notes|directive|Directive)""\s*:\s*""([^""]+)""", RegexOptions.IgnoreCase);
                if (notesMatch.Success) model.Notes = notesMatch.Groups[1].Value.Trim();
            }
            catch { }

            return model;
        }

        public void ApplyStageRulesTheme(string themeName)
        {
            if (AudienceStageRulesPanel == null) return;
            _activeTheme = themeName ?? "Stage";

            var bc = new BrushConverter();
            Brush Parse(string hex) => (Brush)bc.ConvertFromString(hex);

            switch (_activeTheme.ToLowerInvariant())
            {
                case "school":
                case "light":
                case "daylight":
                    AudienceStageRulesPanel.Background = Parse("#FFFFFFFF");
                    AudienceStageRulesPanel.BorderBrush = Parse("#FFCBD5E1");
                    if (StageRulesModeBadgeBorder != null) StageRulesModeBadgeBorder.Background = Parse("#FFDBEAFE");
                    if (StageRulesModeBadgeText != null) StageRulesModeBadgeText.Foreground = Parse("#FF1E40AF");
                    if (StageRulesConferringBorder != null) { StageRulesConferringBorder.Background = Parse("#FFDCFCE7"); StageRulesConferringBorder.BorderBrush = Parse("#FF86EFAC"); }
                    if (StageRulesConferringText != null) StageRulesConferringText.Foreground = Parse("#FF15803D");
                    SetChipStyle(StageRulesDirectChip, StageRulesDirectLabel, StageRulesDirectPointsText, "#FFDCFCE7", "#FF86EFAC", "#FF166534", "#FF15803D");
                    SetChipStyle(StageRulesBounceChip, StageRulesBounceLabel, StageRulesBouncePointsText, "#FFE0F2FE", "#FF7DD3FC", "#FF0369A1", "#FF0284C7");
                    SetChipStyle(StageRulesPenaltyChip, StageRulesPenaltyLabel, StageRulesPenaltyPointsText, "#FFFEE2E2", "#FFFCA5A5", "#FF991B1B", "#FFDC2626");
                    SetChipStyle(StageRulesTimerChip, StageRulesTimerLabel, StageRulesTimeLimitText, "#FFFEF3C7", "#FFFCD34D", "#FF92400E", "#FFD97706");
                    if (StageRulesNotesBorder != null) StageRulesNotesBorder.Background = Parse("#FFF1F5F9");
                    if (StageRulesNotesText != null) StageRulesNotesText.Foreground = Parse("#FF334155");
                    break;

                case "corporate":
                case "slate":
                    AudienceStageRulesPanel.Background = Parse("#FF1E293B");
                    AudienceStageRulesPanel.BorderBrush = Parse("#FF334155");
                    if (StageRulesModeBadgeBorder != null) StageRulesModeBadgeBorder.Background = Parse("#FF312E81");
                    if (StageRulesModeBadgeText != null) StageRulesModeBadgeText.Foreground = Parse("#FFA5B4FC");
                    if (StageRulesConferringBorder != null) { StageRulesConferringBorder.Background = Parse("#FF064E3B"); StageRulesConferringBorder.BorderBrush = Parse("#FF059669"); }
                    if (StageRulesConferringText != null) StageRulesConferringText.Foreground = Parse("#FF6EE7B7");
                    SetChipStyle(StageRulesDirectChip, StageRulesDirectLabel, StageRulesDirectPointsText, "#FF064E3B", "#FF059669", "#FF6EE7B7", "#FF34D399");
                    SetChipStyle(StageRulesBounceChip, StageRulesBounceLabel, StageRulesBouncePointsText, "#FF1E1B4B", "#FF6366F1", "#FFC7D2FE", "#FFA5B4FC");
                    SetChipStyle(StageRulesPenaltyChip, StageRulesPenaltyLabel, StageRulesPenaltyPointsText, "#FF450A0A", "#FFDC2626", "#FFFECACA", "#FFF87171");
                    SetChipStyle(StageRulesTimerChip, StageRulesTimerLabel, StageRulesTimeLimitText, "#FF312E81", "#FF818CF8", "#FFE0E7FF", "#FFC7D2FE");
                    if (StageRulesNotesBorder != null) StageRulesNotesBorder.Background = Parse("#FF0F172A");
                    if (StageRulesNotesText != null) StageRulesNotesText.Foreground = Parse("#FF94A3B8");
                    break;

                case "broadcast":
                case "studio":
                    AudienceStageRulesPanel.Background = Parse("#FF111111");
                    AudienceStageRulesPanel.BorderBrush = Parse("#FFEAB308");
                    if (StageRulesModeBadgeBorder != null) StageRulesModeBadgeBorder.Background = Parse("#FF78350F");
                    if (StageRulesModeBadgeText != null) StageRulesModeBadgeText.Foreground = Parse("#FFFDE047");
                    if (StageRulesConferringBorder != null) { StageRulesConferringBorder.Background = Parse("#FF052E16"); StageRulesConferringBorder.BorderBrush = Parse("#FF16A34A"); }
                    if (StageRulesConferringText != null) StageRulesConferringText.Foreground = Parse("#FF4ADE80");
                    SetChipStyle(StageRulesDirectChip, StageRulesDirectLabel, StageRulesDirectPointsText, "#FF052E16", "#FF16A34A", "#FF86EFAC", "#FF4ADE80");
                    SetChipStyle(StageRulesBounceChip, StageRulesBounceLabel, StageRulesBouncePointsText, "#FF082F49", "#FF0284C7", "#FF7DD3FC", "#FF38BDF8");
                    SetChipStyle(StageRulesPenaltyChip, StageRulesPenaltyLabel, StageRulesPenaltyPointsText, "#FF450A0A", "#FFE11D48", "#FFFCA5A5", "#FFFB7185");
                    SetChipStyle(StageRulesTimerChip, StageRulesTimerLabel, StageRulesTimeLimitText, "#FF451A03", "#FFEAB308", "#FFFDE047", "#FFF59E0B");
                    if (StageRulesNotesBorder != null) StageRulesNotesBorder.Background = Parse("#FF18181B");
                    if (StageRulesNotesText != null) StageRulesNotesText.Foreground = Parse("#FFE2E8F0");
                    break;

                case "stage":
                case "midnight":
                default:
                    AudienceStageRulesPanel.Background = Parse("#FF1C2541");
                    AudienceStageRulesPanel.BorderBrush = Parse("#FF3A506B");
                    if (StageRulesModeBadgeBorder != null) StageRulesModeBadgeBorder.Background = Parse("#FF3A506B");
                    if (StageRulesModeBadgeText != null) StageRulesModeBadgeText.Foreground = Parse("#FF6FFFE9");
                    if (StageRulesConferringBorder != null) { StageRulesConferringBorder.Background = Parse("#FF1B4332"); StageRulesConferringBorder.BorderBrush = Parse("#FF2D6A4F"); }
                    if (StageRulesConferringText != null) StageRulesConferringText.Foreground = Parse("#FF95D5B2");
                    SetChipStyle(StageRulesDirectChip, StageRulesDirectLabel, StageRulesDirectPointsText, "#FF1B4332", "#FF2D6A4F", "#FF95D5B2", "#FF6EE7B7");
                    SetChipStyle(StageRulesBounceChip, StageRulesBounceLabel, StageRulesBouncePointsText, "#FF1E3A5F", "#FF2563EB", "#FF93C5FD", "#FF60A5FA");
                    SetChipStyle(StageRulesPenaltyChip, StageRulesPenaltyLabel, StageRulesPenaltyPointsText, "#FF4A1521", "#FF991B1B", "#FFFCA5A5", "#FFF43F5E");
                    SetChipStyle(StageRulesTimerChip, StageRulesTimerLabel, StageRulesTimeLimitText, "#FF3D2E05", "#FFD97706", "#FFFDE047", "#FFFBBF24");
                    if (StageRulesNotesBorder != null) StageRulesNotesBorder.Background = Parse("#FF0B132B");
                    if (StageRulesNotesText != null) StageRulesNotesText.Foreground = Parse("#FF94A3B8");
                    break;
            }
        }

        private static void SetChipStyle(System.Windows.Controls.Border chip, System.Windows.Controls.TextBlock label, System.Windows.Controls.TextBlock value,
            string bgHex, string borderHex, string labelFgHex, string valueFgHex)
        {
            if (chip == null) return;
            var bc = new BrushConverter();
            chip.Background = (Brush)bc.ConvertFromString(bgHex);
            chip.BorderBrush = (Brush)bc.ConvertFromString(borderHex);
            if (label != null) label.Foreground = (Brush)bc.ConvertFromString(labelFgHex);
            if (value != null) value.Foreground = (Brush)bc.ConvertFromString(valueFgHex);
        }

        private void AnimateStageRulesCard()
        {
            try
            {
                var fadeAnim = new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(260),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                AudienceStageRulesPanel.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
            }
            catch { }
        }

        public void UpdateAudiencePointsBadge(int points)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdateAudiencePointsBadge(points));
                return;
            }

            if (AudiencePointsLabel != null)
            {
                AudiencePointsLabel.Text = $"{points} POINTS";
            }
            TriggerPointsBadgeDecayAnimation();
        }

        private void TriggerPointsBadgeDecayAnimation()
        {
            try
            {
                if (AudiencePointsBorder == null) return;

                var pulseAnim = new DoubleAnimation
                {
                    From = 0.4,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(220),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                AudiencePointsBorder.BeginAnimation(UIElement.OpacityProperty, pulseAnim);
            }
            catch { }
        }
    }
}
