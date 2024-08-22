using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

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
            return scoreRecord?.ScoreValue ?? 0;
        }

        private void UpdateScore(string teamName, int change)
        {
            var scoreRecord = score.ScoreCounts.FirstOrDefault(m => m.Team == teamName);
            if (scoreRecord != null)
            {
                scoreRecord.ScoreValue += change;
                score.SaveChanges();

                // Update the UI label and animate
                if (teamName == "JBA 1")
                {
                    lblScoreTeam1.Content = scoreRecord.ScoreValue;
                    AnimateScoreChange(lblScoreTeam1, change);
                }
                else if (teamName == "JBA 2")
                {
                    lblScoreTeam2.Content = scoreRecord.ScoreValue;
                    AnimateScoreChange(lblScoreTeam2, change);
                }
                else if (teamName == "YVM 1")
                {
                    lblScoreTeam3.Content = scoreRecord.ScoreValue;
                    AnimateScoreChange(lblScoreTeam3, change);
                }
                else if (teamName == "YVM 2")
                {
                    lblScoreTeam4.Content = scoreRecord.ScoreValue;
                    AnimateScoreChange(lblScoreTeam4, change);
                }
            }
        }

        private void AnimateScoreChange(Label scoreLabel, int change)
        {
            if (change > 0)
            {
                AnimateScoreIncrease(scoreLabel);
            }
            else if (change < 0)
            {
                AnimateScoreDecrease(scoreLabel);
            }
        }

        private void AnimateScoreIncrease(Label scoreLabel)
        {
            // Color Animation
            ColorAnimation colorAnimation = new ColorAnimation(Colors.Green, Colors.MidnightBlue, TimeSpan.FromSeconds(0.5));
            scoreLabel.Foreground = new SolidColorBrush(Colors.Black);
            scoreLabel.Foreground.BeginAnimation(SolidColorBrush.ColorProperty, colorAnimation);

            // Scale Animation
            ScaleTransform scaleTransform = new ScaleTransform();
            scoreLabel.RenderTransform = scaleTransform;
            DoubleAnimation scaleAnimation = new DoubleAnimation(1.2, 1, TimeSpan.FromSeconds(0.5));
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
        }

        private void AnimateScoreDecrease(Label scoreLabel)
        {
            // Color Animation
            ColorAnimation colorAnimation = new ColorAnimation(Colors.HotPink, Colors.DarkRed, TimeSpan.FromSeconds(0.5));
            scoreLabel.Foreground = new SolidColorBrush(Colors.Black);
            scoreLabel.Foreground.BeginAnimation(SolidColorBrush.ColorProperty, colorAnimation);

            // Scale Animation
            ScaleTransform scaleTransform = new ScaleTransform();
            scoreLabel.RenderTransform = scaleTransform;
            DoubleAnimation scaleAnimation = new DoubleAnimation(0.8, 1, TimeSpan.FromSeconds(0.5));
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
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
