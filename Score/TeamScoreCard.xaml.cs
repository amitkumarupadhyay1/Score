using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Score
{
    public partial class TeamScoreCard : UserControl
    {
        private bool isTeamTimerRunning;

        public TeamScoreCard()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty TeamNameProperty = DependencyProperty.Register(
            "TeamName", typeof(string), typeof(TeamScoreCard), new PropertyMetadata(string.Empty, OnTeamNameChanged));

        public string TeamName
        {
            get { return (string)GetValue(TeamNameProperty); }
            set { SetValue(TeamNameProperty, value); }
        }

        public static readonly DependencyProperty DatabaseTeamNameProperty = DependencyProperty.Register(
            "DatabaseTeamName", typeof(string), typeof(TeamScoreCard), new PropertyMetadata(string.Empty));

        public string DatabaseTeamName
        {
            get { return (string)GetValue(DatabaseTeamNameProperty); }
            set { SetValue(DatabaseTeamNameProperty, value); }
        }

        private static void OnTeamNameChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as TeamScoreCard;
            if (control != null)
            {
                if (control.TeamNameEditor != null && control.TeamNameEditor.Text != (string)e.NewValue)
                    control.TeamNameEditor.Text = (string)e.NewValue;
            }
        }

        public static readonly DependencyProperty ScoreColorProperty = DependencyProperty.Register(
            "ScoreColor", typeof(Brush), typeof(TeamScoreCard), new PropertyMetadata(Brushes.White, OnScoreColorChanged));

        public Brush ScoreColor
        {
            get { return (Brush)GetValue(ScoreColorProperty); }
            set { SetValue(ScoreColorProperty, value); }
        }

        private static void OnScoreColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as TeamScoreCard;
            if (control != null)
            {
                control.ScoreLabel.Foreground = ((Brush)e.NewValue).Clone();
            }
        }

        public static readonly DependencyProperty ScoreValueProperty = DependencyProperty.Register(
            "ScoreValue", typeof(int), typeof(TeamScoreCard), new PropertyMetadata(0, OnScoreValueChanged));

        public int ScoreValue
        {
            get { return (int)GetValue(ScoreValueProperty); }
            set { SetValue(ScoreValueProperty, value); }
        }

        public void SetLeading(bool isLeading)
        {
            if (isLeading)
            {
                CardBorder.BorderThickness = new Thickness(5);
                LeadBadge.Visibility = Visibility.Visible;

                var pulseAnimation = new DoubleAnimation
                {
                    From = 1.0,
                    To = 1.08,
                    Duration = TimeSpan.FromSeconds(0.7),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                };

                ((ScaleTransform)LeadBadge.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, pulseAnimation);
                ((ScaleTransform)LeadBadge.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, pulseAnimation);
            }
            else
            {
                CardBorder.BorderThickness = new Thickness(3);
                LeadBadge.Visibility = Visibility.Collapsed;

                var scaleTransform = (ScaleTransform)LeadBadge.RenderTransform;
                scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                scaleTransform.ScaleX = 1.0;
                scaleTransform.ScaleY = 1.0;
            }
        }

        public void SetRanking(int rank)
        {
            RankLabel.Text = rank == 1 ? "1ST" : rank == 2 ? "2ND" : rank == 3 ? "3RD" : rank + "TH";
        }

        public void SetTotalScore(int totalScore)
        {
            TotalScoreLabel.Text = "TOTAL " + totalScore;
        }

        public void SetDangerState(bool isDanger)
        {
            DangerBadge.Visibility = isDanger ? Visibility.Visible : Visibility.Collapsed;
            ScoreLabel.Foreground = isDanger ? Brushes.IndianRed : ScoreColor;
            CardBorder.BorderBrush = isDanger ? Brushes.IndianRed : ScoreColor;
        }

        public void SetFinalWinner(bool isWinner)
        {
            if (isWinner)
            {
                CardBorder.BorderBrush = Brushes.Gold;
                CardBorder.BorderThickness = new Thickness(7);
            }
            else
            {
                SetDangerState(ScoreValue < 0);
                CardBorder.BorderThickness = new Thickness(3);
            }
        }

        public void SetRoundScores(string roundScores)
        {
            RoundScoresLabel.Text = string.IsNullOrWhiteSpace(roundScores) ? "ROUND SCORES" : roundScores;

            var slideAnimation = new DoubleAnimation
            {
                From = 8,
                To = 0,
                Duration = TimeSpan.FromSeconds(0.3)
            };
            var fadeAnimation = new DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromSeconds(0.3)
            };
            ((TranslateTransform)RoundScoresLabel.RenderTransform).BeginAnimation(TranslateTransform.YProperty, slideAnimation);
            RoundScoresLabel.BeginAnimation(OpacityProperty, fadeAnimation);
        }

        public void SetScoringEnabled(bool isEnabled)
        {
            IncreaseButton.IsEnabled = isEnabled;
            DecreaseButton.IsEnabled = isEnabled;
            TeamTimerButton.IsEnabled = isEnabled;
            TeamNameEditor.IsReadOnly = !isEnabled;
        }

        public void SetTeamTimerDisplay(string displayText, bool isOvertime)
        {
            TeamTimerLabel.Text = displayText;
            TeamTimerLabel.Foreground = isOvertime ? Brushes.IndianRed : Brushes.LightGreen;
        }

        public void SetTeamTimerRunning(bool isRunning)
        {
            isTeamTimerRunning = isRunning;
            TeamTimerButton.Content = isRunning ? "STOP TEAM" : "START TEAM";
            TeamTimerButton.Background = isRunning ? Brushes.IndianRed : new SolidColorBrush(Color.FromRgb(58, 111, 155));
        }

        private static void OnScoreValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as TeamScoreCard;
            if (control != null)
            {
                control.ScoreLabel.Content = e.NewValue.ToString();
            }
        }

        public void UpdateScore(int newScore, int change)
        {
            ScoreValue = newScore;
            
            if (change > 0)
                AnimateScoreIncrease();
            else if (change < 0)
                AnimateScoreDecrease();
        }

        public static readonly RoutedEvent ScoreIncreasedEvent = EventManager.RegisterRoutedEvent(
            "ScoreIncreased", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TeamScoreCard));

        public event RoutedEventHandler ScoreIncreased
        {
            add { AddHandler(ScoreIncreasedEvent, value); }
            remove { RemoveHandler(ScoreIncreasedEvent, value); }
        }

        public static readonly RoutedEvent ScoreDecreasedEvent = EventManager.RegisterRoutedEvent(
            "ScoreDecreased", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TeamScoreCard));

        public event RoutedEventHandler ScoreDecreased
        {
            add { AddHandler(ScoreDecreasedEvent, value); }
            remove { RemoveHandler(ScoreDecreasedEvent, value); }
        }

        private void IncreaseButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(ScoreIncreasedEvent));
        }

        private void DecreaseButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(ScoreDecreasedEvent));
        }

        public static readonly RoutedEvent TeamTimerStartedEvent = EventManager.RegisterRoutedEvent(
            "TeamTimerStarted", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TeamScoreCard));

        public event RoutedEventHandler TeamTimerStarted
        {
            add { AddHandler(TeamTimerStartedEvent, value); }
            remove { RemoveHandler(TeamTimerStartedEvent, value); }
        }

        public static readonly RoutedEvent TeamTimerStoppedEvent = EventManager.RegisterRoutedEvent(
            "TeamTimerStopped", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(TeamScoreCard));

        public event RoutedEventHandler TeamTimerStopped
        {
            add { AddHandler(TeamTimerStoppedEvent, value); }
            remove { RemoveHandler(TeamTimerStoppedEvent, value); }
        }

        private void TeamTimerButton_Click(object sender, RoutedEventArgs e)
        {
            RaiseEvent(new RoutedEventArgs(isTeamTimerRunning ? TeamTimerStoppedEvent : TeamTimerStartedEvent));
        }

        private void TeamNameEditor_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                TeamNameEditor.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next));
                e.Handled = true;
            }
        }

        private void TeamNameEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            TeamName = string.IsNullOrWhiteSpace(TeamNameEditor.Text) ? "School" : TeamNameEditor.Text.Trim();
        }

        private void AnimateScoreIncrease()
        {
            var originalColor = GetScoreColor();
            var flashColor = Colors.White;

            var colorAnimation = new ColorAnimationUsingKeyFrames();
            colorAnimation.KeyFrames.Add(new EasingColorKeyFrame(flashColor, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.1))));
            colorAnimation.KeyFrames.Add(new EasingColorKeyFrame(originalColor, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.4))));

            var animatedBrush = new SolidColorBrush(originalColor);
            ScoreLabel.Foreground = animatedBrush;
            animatedBrush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnimation);

            var scaleTransform = new ScaleTransform();
            var rotateTransform = new RotateTransform();
            var transformGroup = new TransformGroup();
            transformGroup.Children.Add(scaleTransform);
            transformGroup.Children.Add(rotateTransform);
            ScoreLabel.RenderTransform = transformGroup;
            ScoreLabel.RenderTransformOrigin = new Point(0.5, 0.5);

            var scaleAnimation = new DoubleAnimationUsingKeyFrames();
            scaleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(1.5, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.1))));
            scaleAnimation.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.4))) { EasingFunction = new BounceEase() { Bounces = 2, EasingMode = EasingMode.EaseOut } });
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
        }

        private void AnimateScoreDecrease()
        {
            var originalColor = GetScoreColor();
            var flashColor = Colors.Red;

            var colorAnimation = new ColorAnimationUsingKeyFrames();
            colorAnimation.KeyFrames.Add(new EasingColorKeyFrame(flashColor, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.1))));
            colorAnimation.KeyFrames.Add(new EasingColorKeyFrame(originalColor, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.4))));

            var animatedBrush = new SolidColorBrush(originalColor);
            ScoreLabel.Foreground = animatedBrush;
            animatedBrush.BeginAnimation(SolidColorBrush.ColorProperty, colorAnimation);

            var scaleTransform = new ScaleTransform();
            ScoreLabel.RenderTransform = scaleTransform;
            ScoreLabel.RenderTransformOrigin = new Point(0.5, 0.5);

            var scaleAnimation = new DoubleAnimation(0.8, 1, TimeSpan.FromSeconds(0.4));
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
        }

        private Color GetScoreColor()
        {
            var brush = ScoreLabel.Foreground as SolidColorBrush;
            return brush != null ? brush.Color : Colors.White;
        }
    }
}
