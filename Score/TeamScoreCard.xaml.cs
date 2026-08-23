using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Effects;

namespace Score
{
    public partial class TeamScoreCard : UserControl
    {
        private bool isTeamTimerRunning;
        private bool isLiveScoring;

        public TeamScoreCard()
        {
            InitializeComponent();
        }

        public static readonly DependencyProperty TeamNameProperty = DependencyProperty.Register("TeamName", typeof(string), typeof(TeamScoreCard), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty TeamMembersProperty = DependencyProperty.Register("TeamMembers", typeof(string), typeof(TeamScoreCard), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty DatabaseTeamNameProperty = DependencyProperty.Register("DatabaseTeamName", typeof(string), typeof(TeamScoreCard), new PropertyMetadata(string.Empty));
        public static readonly DependencyProperty ScoreColorProperty = DependencyProperty.Register("ScoreColor", typeof(Brush), typeof(TeamScoreCard), new PropertyMetadata(Brushes.White, OnScoreColorChanged));
        public static readonly DependencyProperty ScoreValueProperty = DependencyProperty.Register("ScoreValue", typeof(int), typeof(TeamScoreCard), new PropertyMetadata(0, OnScoreValueChanged));

        public string TeamName { get { return (string)GetValue(TeamNameProperty); } set { SetValue(TeamNameProperty, value); } }
        public string TeamMembers { get { return (string)GetValue(TeamMembersProperty); } set { SetValue(TeamMembersProperty, value); } }
        public string DatabaseTeamName { get { return (string)GetValue(DatabaseTeamNameProperty); } set { SetValue(DatabaseTeamNameProperty, value); } }
        public Brush ScoreColor { get { return (Brush)GetValue(ScoreColorProperty); } set { SetValue(ScoreColorProperty, value); } }
        public int ScoreValue { get { return (int)GetValue(ScoreValueProperty); } set { SetValue(ScoreValueProperty, value); } }

        public event EventHandler<TeamScoreRequestedEventArgs> ScoreRequested;
        public event RoutedEventHandler TeamTimerStarted;
        public event RoutedEventHandler TeamTimerStopped;
        public event RoutedEventHandler TeamDetailsChanged;

        private static void OnScoreColorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as TeamScoreCard;
            if (control != null && control.ScoreLabel != null)
                control.ScoreLabel.Foreground = ((Brush)e.NewValue).Clone();
        }

        private static void OnScoreValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = d as TeamScoreCard;
            if (control != null && control.ScoreLabel != null)
                control.ScoreLabel.Content = e.NewValue.ToString();
        }

        public void SetLeading(bool isLeading)
        {
            if (isLeading)
            {
                CardBorder.BorderThickness = new Thickness(5);
                LeadBadge.Visibility = Visibility.Visible;
                var pulse = new DoubleAnimation(1.0, 1.08, TimeSpan.FromSeconds(0.7)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
                ((ScaleTransform)LeadBadge.RenderTransform).BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
                ((ScaleTransform)LeadBadge.RenderTransform).BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
            }
            else
            {
                CardBorder.BorderThickness = new Thickness(3);
                LeadBadge.Visibility = Visibility.Collapsed;
                var scale = (ScaleTransform)LeadBadge.RenderTransform;
                scale.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                scale.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                scale.ScaleX = 1.0;
                scale.ScaleY = 1.0;
            }
        }

        public void SetRanking(int rank)
        {
            RankLabel.Text = rank == 1 ? "1ST" : rank == 2 ? "2ND" : rank == 3 ? "3RD" : rank + "TH";
        }

        public void SetTotalScore(int totalScore) { TotalScoreLabel.Text = "TOURNAMENT " + totalScore; }

        public void SetDangerState(bool isDanger)
        {
            DangerBadge.Visibility = isDanger ? Visibility.Visible : Visibility.Collapsed;
            ScoreLabel.Foreground = isDanger ? Brushes.IndianRed : ScoreColor;
            CardBorder.BorderBrush = isDanger ? Brushes.IndianRed : ScoreColor;
        }

        public void SetFinalWinner(bool isWinner)
        {
            CardBorder.BorderBrush = isWinner ? Brushes.Gold : ScoreColor;
            CardBorder.BorderThickness = new Thickness(isWinner ? 7 : 3);
        }

        public void SetLiveScoring(bool isLive)
        {
            if (isLiveScoring == isLive) return;
            isLiveScoring = isLive;
            if (!isLive)
            {
                var transform = ScoreLabel.RenderTransform as ScaleTransform;
                if (transform != null)
                {
                    transform.BeginAnimation(ScaleTransform.ScaleXProperty, null);
                    transform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
                    transform.ScaleX = 1.45;
                    transform.ScaleY = 1.45;
                }
                var effect = new DropShadowEffect { Color = Colors.Black, BlurRadius = 13, ShadowDepth = 7, Direction = 315, Opacity = 0.85 };
                ScoreLabel.Effect = effect;
                return;
            }
            var liveTransform = new ScaleTransform(1.45, 1.45);
            ScoreLabel.RenderTransform = liveTransform;
            ScoreLabel.RenderTransformOrigin = new Point(0.5, 0.5);
            var pulse = new DoubleAnimation(1.45, 1.5, TimeSpan.FromSeconds(0.9)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
            liveTransform.BeginAnimation(ScaleTransform.ScaleXProperty, pulse);
            liveTransform.BeginAnimation(ScaleTransform.ScaleYProperty, pulse);
            var glow = new DropShadowEffect { Color = ScoreColor is SolidColorBrush ? ((SolidColorBrush)ScoreColor).Color : Colors.White, BlurRadius = 18, ShadowDepth = 0, Opacity = 0.72 };
            ScoreLabel.Effect = glow;
            var glowPulse = new DoubleAnimation(18, 32, TimeSpan.FromSeconds(1.1)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever, EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut } };
            glow.BeginAnimation(DropShadowEffect.BlurRadiusProperty, glowPulse);
        }

        public void SetRoundScores(string scores)
        {
            RoundScoresLabel.Text = string.IsNullOrWhiteSpace(scores) ? "NO SCORES RECORDED" : scores;
            ((TranslateTransform)RoundScoresLabel.RenderTransform).BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(8, 0, TimeSpan.FromSeconds(0.25)));
            RoundScoresLabel.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.25)));
        }

        public void SetScoringEnabled(bool isEnabled)
        {
            foreach (var child in ScoreActionsPanel.Children)
            {
                var button = child as Button;
                if (button != null) button.IsEnabled = isEnabled;
            }
            TeamTimerButton.IsEnabled = isEnabled;
            TeamNameEditor.IsReadOnly = !isEnabled;
            TeamMembersEditor.IsReadOnly = !isEnabled;
        }

        public void SetTeamTimerDisplay(string displayText, bool isOvertime)
        {
            TeamTimerLabel.Text = displayText;
            TeamTimerLabel.Foreground = isOvertime ? Brushes.IndianRed : Brushes.LightGreen;
        }

        public void SetTeamTimerRunning(bool isRunning)
        {
            isTeamTimerRunning = isRunning;
            TeamTimerButton.Content = isRunning ? "STOP TIME" : "TIME TEAM";
            TeamTimerButton.Background = isRunning ? Brushes.IndianRed : new SolidColorBrush(Color.FromRgb(33, 102, 164));
        }

        public void UpdateScore(int newScore, int change)
        {
            ScoreValue = newScore;
            AnimateScore(change > 0 ? 1.18 : 0.84, change > 0 ? Colors.White : Colors.IndianRed);
        }

        private void RequestScore(int points)
        {
            var handler = ScoreRequested;
            if (handler != null) handler(this, new TeamScoreRequestedEventArgs(points));
        }

        private void PenaltyButton_Click(object sender, RoutedEventArgs e) { RequestScore(-1); }
        private void AddOneButton_Click(object sender, RoutedEventArgs e) { RequestScore(1); }
        private void AddTwoButton_Click(object sender, RoutedEventArgs e) { RequestScore(2); }
        private void AddFiveButton_Click(object sender, RoutedEventArgs e) { RequestScore(5); }

        private void TeamTimerButton_Click(object sender, RoutedEventArgs e)
        {
            if (isTeamTimerRunning)
            {
                if (TeamTimerStopped != null) TeamTimerStopped(this, new RoutedEventArgs());
            }
            else if (TeamTimerStarted != null)
                TeamTimerStarted(this, new RoutedEventArgs());
        }

        private void TeamNameEditor_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) { MoveOnEnter(TeamNameEditor, e); }
        private void TeamMembersEditor_KeyDown(object sender, System.Windows.Input.KeyEventArgs e) { MoveOnEnter(TeamMembersEditor, e); }

        private void MoveOnEnter(Control control, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Enter) return;
            control.MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.Next));
            e.Handled = true;
        }

        private void TeamNameEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            TeamName = string.IsNullOrWhiteSpace(TeamName) ? "Team" : TeamName.Trim();
            NotifyTeamDetailsChanged();
        }

        private void TeamMembersEditor_LostFocus(object sender, RoutedEventArgs e)
        {
            TeamMembers = (TeamMembers ?? string.Empty).Trim();
            NotifyTeamDetailsChanged();
        }

        private void NotifyTeamDetailsChanged()
        {
            if (TeamDetailsChanged != null) TeamDetailsChanged(this, new RoutedEventArgs());
        }

        private void AnimateScore(double scaleValue, Color flashColor)
        {
            var originalBrush = ScoreLabel.Foreground as SolidColorBrush;
            var original = originalBrush != null ? originalBrush.Color : Colors.White;
            var brush = new SolidColorBrush(original);
            ScoreLabel.Foreground = brush;
            var colors = new ColorAnimationUsingKeyFrames();
            colors.KeyFrames.Add(new EasingColorKeyFrame(flashColor, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.08))));
            colors.KeyFrames.Add(new EasingColorKeyFrame(original, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.35))));
            brush.BeginAnimation(SolidColorBrush.ColorProperty, colors);
            var transform = new ScaleTransform();
            ScoreLabel.RenderTransform = transform;
            ScoreLabel.RenderTransformOrigin = new Point(0.5, 0.5);
            var animation = new DoubleAnimation(scaleValue * 1.45, 1.45, TimeSpan.FromSeconds(0.35)) { EasingFunction = new BackEase { EasingMode = EasingMode.EaseOut } };
            transform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            transform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }
    }
}
