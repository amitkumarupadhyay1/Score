using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Score
{
    public class InterRoundTeamItem
    {
        public int Rank { get; set; }
        public string RankText { get; set; }
        public string TeamName { get; set; }
        public string ScoreText { get; set; }
        public string ShiftText { get; set; }
        public Brush RankBadgeBackground { get; set; }
        public Brush RankBadgeForeground { get; set; }
        public Brush TeamNameForeground { get; set; }
        public Brush ScoreForeground { get; set; }
        public Brush ShiftBackground { get; set; }
        public Brush ShiftForeground { get; set; }
        public Brush RowBackground { get; set; }
        public Brush RowBorderBrush { get; set; }
    }

    public partial class QuizAudienceWindow
    {
        private IEnumerable<TeamScoreSnapshot> _lastInterRoundScores;
        private string _lastCompletedRoundTitle;
        private string _lastNextRoundTitle;

        public void ShowInterRoundLeaderboard(string completedRoundTitle, string nextRoundTitle, IEnumerable<TeamScoreSnapshot> teamScores)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => ShowInterRoundLeaderboard(completedRoundTitle, nextRoundTitle, teamScores));
                return;
            }

            if (AudienceInterRoundPanel == null) return;

            _lastCompletedRoundTitle = completedRoundTitle;
            _lastNextRoundTitle = nextRoundTitle;
            _lastInterRoundScores = teamScores;

            if (InterRoundCompletedTitle != null)
            {
                InterRoundCompletedTitle.Text = string.IsNullOrWhiteSpace(completedRoundTitle) ? "ROUND CONCLUDED" : completedRoundTitle.ToUpperInvariant();
            }

            if (InterRoundNextTitle != null)
            {
                InterRoundNextTitle.Text = string.IsNullOrWhiteSpace(nextRoundTitle) ? "Next round starting shortly..." : $"Up Next: {nextRoundTitle}";
            }

            var items = BuildInterRoundItems(teamScores, _activeTheme);
            if (InterRoundStandingsList != null)
            {
                InterRoundStandingsList.ItemsSource = items;
            }

            ApplyInterRoundTheme(_activeTheme);
            AudienceInterRoundPanel.Visibility = Visibility.Visible;

            AnimateInterRoundTransition();
        }

        public void HideInterRoundLeaderboard()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(HideInterRoundLeaderboard);
                return;
            }

            if (AudienceInterRoundPanel != null)
            {
                AudienceInterRoundPanel.Visibility = Visibility.Collapsed;
            }
        }

        private List<InterRoundTeamItem> BuildInterRoundItems(IEnumerable<TeamScoreSnapshot> teamScores, string themeName)
        {
            var result = new List<InterRoundTeamItem>();
            if (teamScores == null) return result;

            var list = teamScores.ToList();
            if (list.Count == 0) return result;

            var ordered = list.OrderByDescending(s => s.TotalScore).ToList();
            var bc = new BrushConverter();
            Brush Parse(string hex) => (Brush)bc.ConvertFromString(hex);

            var isDark = !string.Equals(themeName, "School", StringComparison.OrdinalIgnoreCase) &&
                         !string.Equals(themeName, "Light", StringComparison.OrdinalIgnoreCase) &&
                         !string.Equals(themeName, "Daylight", StringComparison.OrdinalIgnoreCase);

            for (int i = 0; i < ordered.Count; i++)
            {
                var s = ordered[i];
                var rank = s.Rank > 0 ? s.Rank : (i + 1);
                var shift = s.RankChange;

                Brush rankBadgeBg;
                Brush rankBadgeFg = Parse("#FFFFFFFF");
                Brush teamFg = isDark ? Parse("#FFFFFFFF") : Parse("#FF0F172A");
                Brush scoreFg = isDark ? Parse("#FF6FFFE9") : Parse("#FF2563EB");
                Brush rowBg = isDark ? Parse("#FF141E33") : Parse("#FFFFFFFF");
                Brush rowBorder = isDark ? Parse("#FF2A3B5C") : Parse("#FFE2E8F0");

                if (rank == 1)
                {
                    rankBadgeBg = Parse("#FFF59E0B"); // Gold
                    if (isDark)
                    {
                        rowBg = Parse("#FF1C2541");
                        rowBorder = Parse("#FFF59E0B");
                        scoreFg = Parse("#FFFDE047");
                    }
                }
                else if (rank == 2)
                {
                    rankBadgeBg = Parse("#FF94A3B8"); // Silver
                }
                else if (rank == 3)
                {
                    rankBadgeBg = Parse("#FFD97706"); // Bronze
                }
                else
                {
                    rankBadgeBg = isDark ? Parse("#FF334155") : Parse("#FFE2E8F0");
                    if (!isDark) rankBadgeFg = Parse("#FF475569");
                }

                string shiftText;
                Brush shiftBg;
                Brush shiftFg;

                if (shift > 0)
                {
                    shiftText = $"▲ +{shift}";
                    shiftBg = isDark ? Parse("#FF064E3B") : Parse("#FFDCFCE7");
                    shiftFg = isDark ? Parse("#FF6EE7B7") : Parse("#FF15803D");
                }
                else if (shift < 0)
                {
                    shiftText = $"▼ {shift}";
                    shiftBg = isDark ? Parse("#FF450A0A") : Parse("#FFFEE2E2");
                    shiftFg = isDark ? Parse("#FFFCA5A5") : Parse("#FFDC2626");
                }
                else
                {
                    shiftText = "— SAME";
                    shiftBg = isDark ? Parse("#FF1E293B") : Parse("#FFF1F5F9");
                    shiftFg = isDark ? Parse("#FF94A3B8") : Parse("#FF64748B");
                }

                result.Add(new InterRoundTeamItem
                {
                    Rank = rank,
                    RankText = $"#{rank}",
                    TeamName = s.TeamName ?? $"Team {i + 1}",
                    ScoreText = $"{s.TotalScore} pts",
                    ShiftText = shiftText,
                    RankBadgeBackground = rankBadgeBg,
                    RankBadgeForeground = rankBadgeFg,
                    TeamNameForeground = teamFg,
                    ScoreForeground = scoreFg,
                    ShiftBackground = shiftBg,
                    ShiftForeground = shiftFg,
                    RowBackground = rowBg,
                    RowBorderBrush = rowBorder
                });
            }

            return result;
        }

        public void ApplyInterRoundTheme(string themeName)
        {
            if (AudienceInterRoundPanel == null) return;
            var bc = new BrushConverter();
            Brush Parse(string hex) => (Brush)bc.ConvertFromString(hex);

            switch ((themeName ?? "Stage").ToLowerInvariant())
            {
                case "school":
                case "light":
                case "daylight":
                    AudienceInterRoundPanel.Background = Parse("#FFF8FAFC");
                    if (InterRoundBadgeBorder != null) InterRoundBadgeBorder.Background = Parse("#FFDBEAFE");
                    if (InterRoundTransitionBadge != null) InterRoundTransitionBadge.Foreground = Parse("#FF1E40AF");
                    if (InterRoundCompletedTitle != null) InterRoundCompletedTitle.Foreground = Parse("#FF0F172A");
                    if (InterRoundNextTitle != null) InterRoundNextTitle.Foreground = Parse("#FF475569");
                    if (InterRoundTableCard != null)
                    {
                        InterRoundTableCard.Background = Parse("#FFFFFFFF");
                        InterRoundTableCard.BorderBrush = Parse("#FFCBD5E1");
                    }
                    break;

                case "corporate":
                case "slate":
                    AudienceInterRoundPanel.Background = Parse("#FF0F172A");
                    if (InterRoundBadgeBorder != null) InterRoundBadgeBorder.Background = Parse("#FF312E81");
                    if (InterRoundTransitionBadge != null) InterRoundTransitionBadge.Foreground = Parse("#FFA5B4FC");
                    if (InterRoundCompletedTitle != null) InterRoundCompletedTitle.Foreground = Parse("#FFFFFFFF");
                    if (InterRoundNextTitle != null) InterRoundNextTitle.Foreground = Parse("#FF94A3B8");
                    if (InterRoundTableCard != null)
                    {
                        InterRoundTableCard.Background = Parse("#FF1E293B");
                        InterRoundTableCard.BorderBrush = Parse("#FF334155");
                    }
                    break;

                case "broadcast":
                case "studio":
                    AudienceInterRoundPanel.Background = Parse("#FF000000");
                    if (InterRoundBadgeBorder != null) InterRoundBadgeBorder.Background = Parse("#FF78350F");
                    if (InterRoundTransitionBadge != null) InterRoundTransitionBadge.Foreground = Parse("#FFFDE047");
                    if (InterRoundCompletedTitle != null) InterRoundCompletedTitle.Foreground = Parse("#FFFFFFFF");
                    if (InterRoundNextTitle != null) InterRoundNextTitle.Foreground = Parse("#FFEAB308");
                    if (InterRoundTableCard != null)
                    {
                        InterRoundTableCard.Background = Parse("#FF111111");
                        InterRoundTableCard.BorderBrush = Parse("#FFEAB308");
                    }
                    break;

                case "stage":
                case "midnight":
                default:
                    AudienceInterRoundPanel.Background = Parse("#FF0B132B");
                    if (InterRoundBadgeBorder != null) InterRoundBadgeBorder.Background = Parse("#FF1E293B");
                    if (InterRoundTransitionBadge != null) InterRoundTransitionBadge.Foreground = Parse("#FF38BDF8");
                    if (InterRoundCompletedTitle != null) InterRoundCompletedTitle.Foreground = Parse("#FFFFFFFF");
                    if (InterRoundNextTitle != null) InterRoundNextTitle.Foreground = Parse("#FF94A3B8");
                    if (InterRoundTableCard != null)
                    {
                        InterRoundTableCard.Background = Parse("#FF1C2541");
                        InterRoundTableCard.BorderBrush = Parse("#FF3A506B");
                    }
                    break;
            }

            if (_lastInterRoundScores != null && InterRoundStandingsList != null)
            {
                InterRoundStandingsList.ItemsSource = BuildInterRoundItems(_lastInterRoundScores, themeName);
            }
        }

        private void AnimateInterRoundTransition()
        {
            try
            {
                var fadeAnim = new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(320),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };
                AudienceInterRoundPanel.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
            }
            catch { }
        }
    }
}
