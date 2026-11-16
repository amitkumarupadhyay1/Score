using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace Score
{
    public partial class TeamEntryDialog : Window
    {
        public List<string> TeamNames { get; private set; } = new List<string>();

        public TeamEntryDialog(IEnumerable<string> suggestedTeams = null)
        {
            InitializeComponent();
            var teamsList = suggestedTeams?.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
            if (teamsList != null && teamsList.Count > 0)
            {
                TeamNamesEditor.Text = string.Join(Environment.NewLine, teamsList);
            }
            else
            {
                LoadDeskTeamsOrDefaults();
            }
        }

        private void LoadDeskTeamsOrDefaults()
        {
            try
            {
                using (var db = new ScoreDbContext())
                {
                    var deskTeams = db.ScoreCounts
                        .OrderBy(s => s.ScoreCountID)
                        .Select(s => s.Team)
                        .Where(t => !string.IsNullOrEmpty(t))
                        .ToList();

                    if (deskTeams.Count >= 2)
                    {
                        TeamNamesEditor.Text = string.Join(Environment.NewLine, deskTeams);
                        return;
                    }
                }
            }
            catch { }

            TeamNamesEditor.Text = "Team Alpha" + Environment.NewLine + "Team Bravo" + Environment.NewLine + "Team Charlie" + Environment.NewLine + "Team Delta";
        }

        private void UseDeskTeamsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                using (var db = new ScoreDbContext())
                {
                    var deskTeams = db.ScoreCounts
                        .OrderBy(s => s.ScoreCountID)
                        .Select(s => s.Team)
                        .Where(t => !string.IsNullOrEmpty(t))
                        .ToList();

                    if (deskTeams.Count > 0)
                    {
                        TeamNamesEditor.Text = string.Join(Environment.NewLine, deskTeams);
                    }
                    else
                    {
                        MessageBox.Show(this, "No active teams found on the main scoring desk.", "Import Teams", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, "Could not load desk teams: " + ex.Message, "Import Teams", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            var names = TeamNamesEditor.Text
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.Trim())
                .Where(t => t.Length > 0)
                .ToList();

            if (names.Count < 2)
            {
                MessageBox.Show(this, "Please enter at least 2 teams.", "Quiz Master", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (names.Distinct(StringComparer.OrdinalIgnoreCase).Count() != names.Count)
            {
                MessageBox.Show(this, "Team names must be unique.", "Quiz Master", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            TeamNames = names;
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
