using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Score
{
    public partial class QuizAudienceWindow
    {
        private bool _isTimerPulsing = false;

        public void AnimateSlideTransition()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(AnimateSlideTransition);
                return;
            }

            try
            {
                if (AudienceQuestionContentBorder == null) return;

                var fadeAnim = new DoubleAnimation
                {
                    From = 0.6,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(180),
                    EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                };

                AudienceQuestionContentBorder.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
            }
            catch { }
        }

        public void StartTimerPulse()
        {
            if (_isTimerPulsing || AudienceTimerBorder == null) return;
            _isTimerPulsing = true;

            try
            {
                var pulseAnim = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.65,
                    Duration = TimeSpan.FromMilliseconds(450),
                    AutoReverse = true,
                    RepeatBehavior = RepeatBehavior.Forever
                };
                AudienceTimerBorder.BeginAnimation(UIElement.OpacityProperty, pulseAnim);
            }
            catch { }
        }

        public void StopTimerPulse()
        {
            if (!_isTimerPulsing || AudienceTimerBorder == null) return;
            _isTimerPulsing = false;

            try
            {
                AudienceTimerBorder.BeginAnimation(UIElement.OpacityProperty, null);
                AudienceTimerBorder.Opacity = 1.0;
            }
            catch { }
        }

        public void AnimateAnswerReveal()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(AnimateAnswerReveal);
                return;
            }

            try
            {
                if (AudienceAnswerContainer == null) return;

                var fadeAnim = new DoubleAnimation
                {
                    From = 0.0,
                    To = 1.0,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                AudienceAnswerContainer.BeginAnimation(UIElement.OpacityProperty, fadeAnim);
            }
            catch { }
        }
    }
}
