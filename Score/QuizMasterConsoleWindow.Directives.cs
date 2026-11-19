using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace Score
{
    public partial class QuizMasterConsoleWindow : Window
    {
        private int _baseSlidePoints = 10;

        public void UpdateDirectivesDisplay()
        {
            if (_currentSlide == null)
            {
                if (ConsoleDirectivesPanel != null)
                    ConsoleDirectivesPanel.Visibility = Visibility.Collapsed;
                return;
            }

            var hasPromptOn = !string.IsNullOrWhiteSpace(_currentSlide.PromptOnText);
            var hasDoNotAccept = !string.IsNullOrWhiteSpace(_currentSlide.DoNotAcceptText);

            if (ConsolePromptOnText != null && PromptOnBadgeBorder != null)
            {
                ConsolePromptOnText.Text = _currentSlide.PromptOnText ?? string.Empty;
                PromptOnBadgeBorder.Visibility = hasPromptOn ? Visibility.Visible : Visibility.Collapsed;
            }

            if (ConsoleDoNotAcceptText != null && DoNotAcceptBadgeBorder != null)
            {
                ConsoleDoNotAcceptText.Text = _currentSlide.DoNotAcceptText ?? string.Empty;
                DoNotAcceptBadgeBorder.Visibility = hasDoNotAccept ? Visibility.Visible : Visibility.Collapsed;
            }

            if (ConsoleDirectivesPanel != null)
            {
                ConsoleDirectivesPanel.Visibility = (hasPromptOn || hasDoNotAccept) ? Visibility.Visible : Visibility.Collapsed;
            }

            // Cache base points for progressive clue decay calculations
            _baseSlidePoints = _currentSlide.Points > 0 ? _currentSlide.Points : 10;
        }

        public void ApplyClueDecay()
        {
            if (_currentSlide == null) return;

            var decayList = ParseClueDecayList(_currentSlide.ClueDecayJson);
            var hints = (_currentSlide.HintsJson ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

            int newPoints;
            if (decayList.Count > 0)
            {
                // If explicit decay array/list provided, e.g. "50, 40, 30, 20, 10"
                if (_clueStep < decayList.Count)
                {
                    newPoints = decayList[_clueStep];
                }
                else
                {
                    newPoints = decayList[decayList.Count - 1];
                }
            }
            else if (hints.Length > 0)
            {
                // Fallback: scale points down proportionally based on clue step
                var factor = Math.Max(0.2, 1.0 - ((double)_clueStep / (hints.Length + 1)));
                newPoints = Math.Max(1, (int)Math.Round(_baseSlidePoints * factor));
            }
            else
            {
                return;
            }

            _currentSlide.Points = newPoints;
            if (ConsolePointsBadge != null)
            {
                ConsolePointsBadge.Text = $"+{newPoints} pts";
            }

            try
            {
                _audienceWindow?.UpdateAudiencePointsBadge(newPoints);
            }
            catch
            {
            }
        }

        private List<int> ParseClueDecayList(string clueDecayJson)
        {
            var list = new List<int>();
            if (string.IsNullOrWhiteSpace(clueDecayJson)) return list;

            var clean = clueDecayJson.Trim('[', ']', '{', '}', ' ', '"');
            var tokens = clean.Split(new[] { ',', ';', ' ' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var token in tokens)
            {
                var trimmed = token.Trim();
                if (int.TryParse(trimmed, out var val))
                {
                    list.Add(val);
                }
            }
            return list;
        }

        public void CheckInterRoundTransition(int fromIndex, int toIndex)
        {
            if (_slideList == null || fromIndex < 0 || fromIndex >= _slideList.Count ||
                toIndex < 0 || toIndex >= _slideList.Count) return;

            var fromSlide = _slideList[fromIndex];
            var toSlide = _slideList[toIndex];

            var fromRound = fromSlide.RoundName?.Trim() ?? string.Empty;
            var toRound = toSlide.RoundName?.Trim() ?? string.Empty;

            var roundChanged = !string.IsNullOrEmpty(fromRound) &&
                               !string.IsNullOrEmpty(toRound) &&
                               !string.Equals(fromRound, toRound, StringComparison.OrdinalIgnoreCase);

            var roundIdChanged = fromSlide.QuizRoundId.HasValue &&
                                 toSlide.QuizRoundId.HasValue &&
                                 fromSlide.QuizRoundId != toSlide.QuizRoundId;

            // Only trigger inter-round leaderboard when advancing forward past the end of a round
            if (toIndex > fromIndex && (roundChanged || roundIdChanged))
            {
                ShowInterRoundLeaderboardTransition(fromRound, toRound);
            }
            else
            {
                try
                {
                    _audienceWindow?.HideInterRoundLeaderboard();
                }
                catch
                {
                }
            }
        }

        private void ShowInterRoundLeaderboardTransition(string completedRound, string nextRound)
        {
            if (_teams == null || _teams.Count == 0) return;

            var sorted = _teams
                .Select(t => new
                {
                    Name = t,
                    Score = _teamScores != null && _teamScores.TryGetValue(t, out var s) ? s : 0
                })
                .OrderByDescending(x => x.Score)
                .ToList();

            var snapshots = new List<TeamScoreSnapshot>();
            for (int i = 0; i < sorted.Count; i++)
            {
                snapshots.Add(new TeamScoreSnapshot
                {
                    TeamName = sorted[i].Name,
                    TotalScore = sorted[i].Score,
                    Rank = i + 1,
                    RankChange = 0
                });
            }

            try
            {
                _audienceWindow?.ShowInterRoundLeaderboard(
                    string.IsNullOrWhiteSpace(completedRound) ? "Previous Round" : completedRound,
                    string.IsNullOrWhiteSpace(nextRound) ? "Next Round" : nextRound,
                    snapshots);
                ConsoleStatusText.Text = $"Inter-round standings displayed on stage for {completedRound} → {nextRound}.";
            }
            catch
            {
            }
        }
    }
}
