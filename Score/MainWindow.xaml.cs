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
            return scoreRecord != null ? scoreRecord.ScoreValue : 0;
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
            var originalColor = ((SolidColorBrush)scoreLabel.Foreground).Color;
            var flashColor = Colors.White;

            var colorAnimation = new ColorAnimationUsingKeyFrames();
            colorAnimation.KeyFrames.Add(new EasingColorKeyFrame(flashColor, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.1))));
            colorAnimation.KeyFrames.Add(new EasingColorKeyFrame(originalColor, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.4))));

            scoreLabel.Foreground.BeginAnimation(SolidColorBrush.ColorProperty, colorAnimation);

            var scaleTransform = new ScaleTransform();
            var rotateTransform = new RotateTransform();
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(scaleTransform);
            transformGroup.Children.Add(rotateTransform);
            scoreLabel.RenderTransform = transformGroup;
            scoreLabel.RenderTransformOrigin = new Point(0.5, 0.5);

            var scaleAnimation = new DoubleAnimationUsingKeyFrames();
            scaleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(1.5, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.1))));
            scaleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.4))) { EasingFunction = new BounceEase() { Bounces = 2, EasingMode = EasingMode.EaseOut } });
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
        }

        private void AnimateScoreDecrease(Label scoreLabel)
        {
            var originalColor = ((SolidColorBrush)scoreLabel.Foreground).Color;
            var flashColor = Colors.Red;

            var colorAnimation = new ColorAnimationUsingKeyFrames();
            colorAnimation.KeyFrames.Add(new EasingColorKeyFrame(flashColor, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.1))));
            colorAnimation.KeyFrames.Add(new EasingColorKeyFrame(originalColor, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.4))));

            scoreLabel.Foreground.BeginAnimation(SolidColorBrush.ColorProperty, colorAnimation);

            var scaleTransform = new ScaleTransform();
            scoreLabel.RenderTransform = scaleTransform;
            scoreLabel.RenderTransformOrigin = new Point(0.5, 0.5);

            var scaleAnimation = new DoubleAnimation(0.8, 1, TimeSpan.FromSeconds(0.4));
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