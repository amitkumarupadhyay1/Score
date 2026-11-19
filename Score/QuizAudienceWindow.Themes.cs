using System;
using System.Windows;
using System.Windows.Media;

namespace Score
{
    public partial class QuizAudienceWindow
    {
        public void ApplyTheme(string themeName)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => ApplyTheme(themeName));
                return;
            }

            try
            {
                var palette = QuizThemePalette.GetPalette(themeName);
                if (AudienceStageRootGrid != null)
                {
                    AudienceStageRootGrid.Background = palette.WindowBackground;
                }
                if (AudienceHeaderBorder != null)
                {
                    AudienceHeaderBorder.Background = palette.HeaderBackground;
                }
                if (AudienceTimerBorder != null)
                {
                    AudienceTimerBorder.Background = palette.TimerBorderBackground;
                }
                if (AudienceTimerLabel != null)
                {
                    AudienceTimerLabel.Foreground = palette.TimerTextBrush;
                }
                if (AudienceScoreboardBorder != null)
                {
                    AudienceScoreboardBorder.Background = palette.HeaderBackground;
                }
                if (AudienceQuestionContentBorder != null)
                {
                    AudienceQuestionContentBorder.Background = palette.CardBackground;
                }
                if (AudienceSlideTitle != null)
                {
                    AudienceSlideTitle.Foreground = palette.TextPrimary;
                }
                if (AudienceSlideContent != null)
                {
                    AudienceSlideContent.Foreground = palette.TextSecondary;
                }

                ApplyStageRulesTheme(themeName);
                ApplyInterRoundTheme(themeName);
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Could not apply stage audience theme.", ex);
            }
        }
    }
}
