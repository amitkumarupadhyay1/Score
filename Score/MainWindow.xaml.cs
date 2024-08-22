using System;
using System.Linq;
using System.Windows;

namespace Score
{
    public partial class MainWindow : Window
    {
        private ScoreDbContext score;

        public MainWindow()
        {
            InitializeComponent();
            score = new ScoreDbContext();
            LoadScores();
        }

        private void LoadScores()
        {
            lblScoreTeam1.Content = GetScore("JBA 1");
            lblScoreTeam2.Content = GetScore("JBA 2");
            lblScoreTeam3.Content = GetScore("YVM 1");
            lblScoreTeam4.Content = GetScore("YVM 2");
        }

        private int GetScore(string teamName)
        {
            var scoreRecord = score.ScoreCounts.FirstOrDefault(m => m.Team == teamName);
            return scoreRecord != null ? scoreRecord.ScoreValue : 0;
        }

        private void UpdateScore(string teamName, int change)
        {
            var scoreRecord = score.ScoreCounts.FirstOrDefault(m => m.Team == teamName);
            if (scoreRecord != null)
            {
                scoreRecord.ScoreValue += change;
                lblScoreTeam1.Content = teamName == "JBA 1" ? scoreRecord.ScoreValue : lblScoreTeam1.Content;
                lblScoreTeam2.Content = teamName == "JBA 2" ? scoreRecord.ScoreValue : lblScoreTeam2.Content;
                lblScoreTeam3.Content = teamName == "YVM 1" ? scoreRecord.ScoreValue : lblScoreTeam3.Content;
                lblScoreTeam4.Content = teamName == "YVM 2" ? scoreRecord.ScoreValue : lblScoreTeam4.Content;
                score.SaveChanges();
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            UpdateScore("JBA 1", 1);
        }

        private void Button_Click_1(object sender, RoutedEventArgs e)
        {
            UpdateScore("JBA 1", -1);
        }

        private void Button_Click_2(object sender, RoutedEventArgs e)
        {
            UpdateScore("JBA 2", -1);
        }

        private void Button_Click_3(object sender, RoutedEventArgs e)
        {
            UpdateScore("JBA 2", 1);
        }

        private void Button_Click_4(object sender, RoutedEventArgs e)
        {
            UpdateScore("YVM 1", -1);
        }

        private void Button_Click_5(object sender, RoutedEventArgs e)
        {
            UpdateScore("YVM 1", 1);
        }

        private void Button_Click_6(object sender, RoutedEventArgs e)
        {
            UpdateScore("YVM 2", -1);
        }

        private void Button_Click_7(object sender, RoutedEventArgs e)
        {
            UpdateScore("YVM 2", 1);
        }
    }
}
