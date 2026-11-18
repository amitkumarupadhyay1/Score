using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Score
{
    public class AudienceOptionItem
    {
        public string Letter { get; set; }
        public string Text { get; set; }
        public Brush Background { get; set; }
        public Brush BorderBrush { get; set; }
        public Brush Foreground { get; set; }
        public Brush BadgeBackground { get; set; }
        public Brush BadgeForeground { get; set; }
        public FontWeight FontWeight { get; set; }
        public Thickness BorderThickness { get; set; }
        public string Checkmark { get; set; }
        public Brush CheckmarkForeground { get; set; }
        public Visibility CheckmarkVisibility { get; set; }

        public override string ToString() => string.IsNullOrEmpty(Letter) ? (Text ?? string.Empty) : $"{Letter}. {Text}";
    }

    public partial class QuizAudienceWindow : Window
    {
        private static readonly Brush NormalOptionBg = FreezeBrush(248, 250, 252);
        private static readonly Brush NormalOptionBorder = FreezeBrush(226, 232, 240);
        private static readonly Brush NormalOptionFg = FreezeBrush(15, 23, 42);
        private static readonly Brush NormalBadgeBg = FreezeBrush(226, 232, 240);
        private static readonly Brush NormalBadgeFg = FreezeBrush(51, 65, 85);

        private static readonly Brush CorrectOptionBg = FreezeBrush(220, 252, 231);
        private static readonly Brush CorrectOptionBorder = FreezeBrush(34, 197, 94);
        private static readonly Brush CorrectOptionFg = FreezeBrush(20, 83, 45);
        private static readonly Brush CorrectBadgeBg = FreezeBrush(34, 197, 94);
        private static readonly Brush CorrectBadgeFg = FreezeBrush(255, 255, 255);
        private static readonly Brush CorrectCheckmarkFg = FreezeBrush(21, 128, 61);

        private static readonly Brush DimOptionBg = FreezeBrush(241, 245, 249);
        private static readonly Brush DimOptionBorder = FreezeBrush(226, 232, 240);
        private static readonly Brush DimOptionFg = FreezeBrush(100, 116, 139);
        private static readonly Brush DimBadgeBg = FreezeBrush(226, 232, 240);
        private static readonly Brush DimBadgeFg = FreezeBrush(148, 163, 184);

        private static readonly Brush TimerNormalBrush = FreezeBrush(111, 255, 233);
        private static readonly Brush TimerWarningBrush = FreezeBrush(244, 63, 94);
        private static readonly Brush TimerExpiredBrush = FreezeBrush(239, 68, 68);

        private static SolidColorBrush FreezeBrush(byte r, byte g, byte b)
        {
            var brush = new SolidColorBrush(Color.FromRgb(r, g, b));
            brush.Freeze();
            return brush;
        }

        private readonly ScoreDbContext _db = new ScoreDbContext();
        private readonly DispatcherTimer _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private readonly DispatcherTimer _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        private readonly DispatcherTimer _buzzDismissTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };

        private int _currentSessionId;
        private string _currentDeckTitle;
        private int _timerDurationSeconds;
        private int _timerElapsedSeconds;
        private DateTime? _timerStartedAtUtc;
        private string _timerState;
        private string _lastPollingError;
        private int _lastSeenBuzzEntryId;

        public QuizAudienceWindow()
        {
            InitializeComponent();
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            Cursor = System.Windows.Input.Cursors.None;

            _pollTimer.Tick += PollTimer_Tick;
            _clockTimer.Tick += ClockTimer_Tick;
            _buzzDismissTimer.Tick += (s, e) => DismissBuzzAlert();

            Loaded += QuizAudienceWindow_Loaded;
            Closed += QuizAudienceWindow_Closed;
            MouseEnter += (s, e) => { if (_isFullscreenStage) System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.None; };
            MouseLeave += (s, e) => { if (System.Windows.Input.Mouse.OverrideCursor == System.Windows.Input.Cursors.None) System.Windows.Input.Mouse.OverrideCursor = null; };
        }

        private void QuizAudienceWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isFullscreenStage)
            {
                System.Windows.Input.Mouse.OverrideCursor = System.Windows.Input.Cursors.None;
            }
            HookMediaAndEscapeEvents();
            _pollTimer.Start();
            _clockTimer.Start();
        }

        private void QuizAudienceWindow_Closed(object sender, EventArgs e)
        {
            UnhookMediaAndEscapeEvents();
            if (System.Windows.Input.Mouse.OverrideCursor == System.Windows.Input.Cursors.None)
            {
                System.Windows.Input.Mouse.OverrideCursor = null;
            }
            _pollTimer.Stop();
            _clockTimer.Stop();
            _buzzDismissTimer.Stop();
            AudienceMediaPlayer.Stop();
        }

        internal void PushSlideState(DeckSlide slide, QuizDeckSession session, bool isAnswerRevealed, string activeTeam, int clueStep)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => PushSlideState(slide, session, isAnswerRevealed, activeTeam, clueStep));
                return;
            }

            if (slide == null)
            {
                ResetToStandby();
                return;
            }

            if (session != null && session.QuizDeckId > 0 && (_currentSessionId != session.QuizDeckSessionId || string.IsNullOrEmpty(_currentDeckTitle)))
            {
                _currentSessionId = session.QuizDeckSessionId;
                try
                {
                    var deck = _db.QuizDecks.AsNoTracking().FirstOrDefault(d => d.QuizDeckId == session.QuizDeckId);
                    if (deck != null && !string.IsNullOrWhiteSpace(deck.Title))
                    {
                        _currentDeckTitle = deck.Title;
                    }
                }
                catch { }
            }

            if (!string.IsNullOrEmpty(_currentDeckTitle))
            {
                AudienceDeckTitle.Text = _currentDeckTitle;
            }

            var isInstruction = string.Equals(slide.SlideType, "Instruction", StringComparison.OrdinalIgnoreCase);
            var isMedia = string.Equals(slide.SlideType, "Media", StringComparison.OrdinalIgnoreCase);
            var isTimer = string.Equals(slide.SlideType, "Timer", StringComparison.OrdinalIgnoreCase);
            var isScoreboard = string.Equals(slide.SlideType, "Scoreboard", StringComparison.OrdinalIgnoreCase);

            AudienceSlideType.Text = (slide.SlideType ?? "QUESTION").ToUpperInvariant();
            AudienceRoundName.Text = (slide.RoundName ?? "ROUND 1").ToUpperInvariant();
            AudiencePointsLabel.Text = isInstruction ? "RULES / ANNOUNCEMENT" :
                                       isMedia ? "MEDIA CLUE" :
                                       isTimer ? $"TIMER ({slide.TimeLimitSeconds}s)" :
                                       isScoreboard ? "SCOREBOARD" :
                                       $"{slide.Points} POINTS";

            AudienceSlideTitle.Text = slide.Title ?? string.Empty;
            AudienceSlideContent.Text = slide.ContentText ?? string.Empty;

            AudienceOptions.ItemsSource = BuildOptionItems(slide, isAnswerRevealed);
            ShowMedia(slide);

            var hints = (slide.HintsJson ?? string.Empty).Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (clueStep > 0 && hints.Length > 0)
            {
                var revealedCount = Math.Min(clueStep, hints.Length);
                AudienceCluesList.ItemsSource = hints.Take(revealedCount).Select((h, i) => $"Clue {i + 1}: {h}").ToList();
                AudienceCluesPanel.Visibility = Visibility.Visible;
            }
            else
            {
                AudienceCluesPanel.Visibility = Visibility.Collapsed;
            }

            // Reveal Answer (slide.Explanation is strictly removed from stage view)
            if (isAnswerRevealed && !string.IsNullOrWhiteSpace(slide.CorrectAnswer))
            {
                AudienceAnswerText.Text = slide.CorrectAnswer;
                AudienceAnswerContainer.Visibility = Visibility.Visible;
                AnimateAnswerReveal();
            }
            else
            {
                AudienceAnswerContainer.Visibility = Visibility.Collapsed;
            }

            AnimateSlideTransition();

            _timerDurationSeconds = Math.Max(0, slide.TimeLimitSeconds);
            if (session != null)
            {
                _timerElapsedSeconds = Math.Max(0, session.TimerElapsedSeconds);
                _timerStartedAtUtc = session.TimerStartedAtUtc;
                _timerState = session.TimerState;
            }
            UpdateClockDisplay();

            if (!string.IsNullOrWhiteSpace(activeTeam))
            {
                AudienceStatusText.Text = $"Active Floor: {activeTeam.ToUpperInvariant()}";
            }
            else if (session?.Status == "Paused")
            {
                AudienceStatusText.Text = "⏸ QUIZ PAUSED";
            }
            else if (session?.TimerState == "Paused")
            {
                AudienceStatusText.Text = "⏸ TIMER HELD";
            }
            else
            {
                AudienceStatusText.Text = string.Empty;
            }
        }

        public void PushBuzzAlert(string teamName, int buzzOrder)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => PushBuzzAlert(teamName, buzzOrder));
                return;
            }

            _buzzDismissTimer.Stop();
            var orderText = buzzOrder > 1 ? $" #{buzzOrder}" : string.Empty;
            AudienceBuzzIndicator.Text = $"🚨 {teamName?.ToUpperInvariant() ?? "TEAM"}{orderText} BUZZED IN! 🚨";
            AudienceBuzzBorder.Visibility = Visibility.Visible;
            TriggerBuzzAnimation();
            _buzzDismissTimer.Start();
        }

        public void DismissBuzzAlert()
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(DismissBuzzAlert);
                return;
            }

            _buzzDismissTimer.Stop();
            AudienceBuzzBorder.Visibility = Visibility.Collapsed;
        }

        public void ToggleBlackout(bool? forceState = null)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => ToggleBlackout(forceState));
                return;
            }

            if (forceState.HasValue)
            {
                BlackoutOverlay.Visibility = forceState.Value ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                BlackoutOverlay.Visibility = BlackoutOverlay.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }

            if (BlackoutOverlay.Visibility == Visibility.Visible)
            {
                PauseMediaPlayback();
            }
            else
            {
                ResumeMediaPlayback();
            }
        }

        internal void PushScoreboard(List<QuizDeckSessionTeam> teams, Dictionary<string, int> scores)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => PushScoreboard(teams, scores));
                return;
            }

            if (teams == null || teams.Count == 0)
            {
                AudienceScoreboard.ItemsSource = null;
                return;
            }

            var dict = scores ?? new Dictionary<string, int>();
            AudienceScoreboard.ItemsSource = teams
                .Select(t => new
                {
                    Team = t.TeamName,
                    Score = dict.TryGetValue(t.TeamName, out var sc) ? sc : 0
                })
                .OrderByDescending(item => item.Score)
                .ToList();
        }

        private void TriggerBuzzAnimation()
        {
            try
            {
                var scaleAnim = new DoubleAnimation(0.75, 1.0, TimeSpan.FromMilliseconds(280))
                {
                    EasingFunction = new BackEase { Amplitude = 0.4, EasingMode = EasingMode.EaseOut }
                };
                BuzzScaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnim);
                BuzzScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnim);

                var opacityAnim = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200));
                AudienceBuzzBorder.BeginAnimation(UIElement.OpacityProperty, opacityAnim);
            }
            catch { }
        }

        private List<AudienceOptionItem> BuildOptionItems(DeckSlide slide, bool isAnswerRevealed)
        {
            var isQuestion = string.Equals(slide.SlideType ?? "Question", "Question", StringComparison.OrdinalIgnoreCase);
            if (!isQuestion) return new List<AudienceOptionItem>();

            var rawOptions = QuizScoringEngine.ParseOptions(slide.OptionsJson);
            if (rawOptions.Count == 0 && string.Equals(slide.QuestionType, QuizScoringEngine.QuestionTrueFalse, StringComparison.OrdinalIgnoreCase))
            {
                rawOptions = new List<string> { "True", "False" };
            }
            if (rawOptions.Count == 0) return new List<AudienceOptionItem>();

            var items = new List<AudienceOptionItem>();
            for (int i = 0; i < rawOptions.Count; i++)
            {
                var opt = rawOptions[i];
                var letter = ((char)('A' + i)).ToString();
                var isCorrect = IsOptionCorrect(opt, i, rawOptions, slide.CorrectAnswer);

                if (isAnswerRevealed)
                {
                    if (isCorrect)
                    {
                        items.Add(new AudienceOptionItem
                        {
                            Letter = letter,
                            Text = opt,
                            Background = CorrectOptionBg,
                            BorderBrush = CorrectOptionBorder,
                            Foreground = CorrectOptionFg,
                            BadgeBackground = CorrectBadgeBg,
                            BadgeForeground = CorrectBadgeFg,
                            FontWeight = FontWeights.Bold,
                            BorderThickness = new Thickness(3),
                            Checkmark = "✔ CORRECT",
                            CheckmarkForeground = CorrectCheckmarkFg,
                            CheckmarkVisibility = Visibility.Visible
                        });
                    }
                    else
                    {
                        items.Add(new AudienceOptionItem
                        {
                            Letter = letter,
                            Text = opt,
                            Background = DimOptionBg,
                            BorderBrush = DimOptionBorder,
                            Foreground = DimOptionFg,
                            BadgeBackground = DimBadgeBg,
                            BadgeForeground = DimBadgeFg,
                            FontWeight = FontWeights.Normal,
                            BorderThickness = new Thickness(1.5),
                            Checkmark = string.Empty,
                            CheckmarkForeground = Brushes.Transparent,
                            CheckmarkVisibility = Visibility.Collapsed
                        });
                    }
                }
                else
                {
                    items.Add(new AudienceOptionItem
                    {
                        Letter = letter,
                        Text = opt,
                        Background = NormalOptionBg,
                        BorderBrush = NormalOptionBorder,
                        Foreground = NormalOptionFg,
                        BadgeBackground = NormalBadgeBg,
                        BadgeForeground = NormalBadgeFg,
                        FontWeight = FontWeights.SemiBold,
                        BorderThickness = new Thickness(2),
                        Checkmark = string.Empty,
                        CheckmarkForeground = Brushes.Transparent,
                        CheckmarkVisibility = Visibility.Collapsed
                    });
                }
            }
            return items;
        }

        private static bool IsOptionCorrect(string optionText, int optionIndex, List<string> allOptions, string correctAnswer)
        {
            if (string.IsNullOrWhiteSpace(correctAnswer)) return false;
            var trimmed = correctAnswer.Trim();

            var parts = trimmed.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var rawPart in parts)
            {
                var part = rawPart.Trim();
                if (string.Equals(optionText, part, StringComparison.OrdinalIgnoreCase))
                    return true;

                if (QuizAIGeneratorService.MatchesMcqOption(part, allOptions, out var matchedText) &&
                    string.Equals(optionText, matchedText, StringComparison.OrdinalIgnoreCase))
                    return true;

                var letterIdx = QuizAIGeneratorService.ParseOptionLetterIndex(part);
                if (letterIdx == optionIndex)
                    return true;
            }
            return false;
        }

        private void PollTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                var session = _db.QuizDeckSessions.AsNoTracking()
                    .Where(s => s.Status == "Running" || s.Status == "Paused")
                    .OrderByDescending(s => s.QuizDeckSessionId)
                    .FirstOrDefault();

                if (session == null)
                {
                    ResetToStandby();
                    return;
                }

                var deck = _db.QuizDecks.AsNoTracking().FirstOrDefault(d => d.QuizDeckId == session.QuizDeckId);
                if (deck != null)
                {
                    _currentDeckTitle = deck.Title ?? "QUIZ";
                    AudienceDeckTitle.Text = _currentDeckTitle;
                }

                var slides = _db.DeckSlides.AsNoTracking()
                    .Where(s => s.QuizDeckId == session.QuizDeckId && s.IsActive)
                    .OrderBy(s => s.SlideOrder)
                    .ToList();

                if (slides.Count == 0)
                {
                    AudienceSlideTitle.Text = "This quiz has no active slides.";
                    AudienceSlideContent.Text = string.Empty;
                    AudienceMediaPanel.Visibility = Visibility.Collapsed;
                    return;
                }

                var slideIndex = Math.Max(0, Math.Min(session.CurrentSlideIndex, slides.Count - 1));
                var slide = slides[slideIndex];

                PushSlideState(slide, session, session.AnswerRevealed, session.ActiveTeamOnFloor, session.ClueRevealStep);

                var teams = _db.QuizDeckSessionTeams.AsNoTracking()
                    .Where(t => t.QuizDeckSessionId == session.QuizDeckSessionId)
                    .OrderBy(t => t.QuizDeckSessionTeamId)
                    .ToList();
                var scores = _db.QuizResponses.AsNoTracking()
                    .Where(r => r.QuizDeckSessionId == session.QuizDeckSessionId)
                    .GroupBy(r => r.TeamName)
                    .Select(g => new { Team = g.Key, Score = g.Sum(r => r.PointsAwarded) })
                    .ToList()
                    .ToDictionary(item => item.Team, item => item.Score);

                PushScoreboard(teams, scores);

                var latestBuzz = _db.QuizAuditEntries.AsNoTracking()
                    .Where(a => a.QuizDeckSessionId == session.QuizDeckSessionId &&
                                a.SlideNumber == slideIndex + 1 &&
                                (a.EventType == "BuzzAccepted" || a.EventType == "PassedToTeam"))
                    .OrderByDescending(a => a.QuizAuditEntryId)
                    .FirstOrDefault();

                if (latestBuzz != null && _lastSeenBuzzEntryId != latestBuzz.QuizAuditEntryId)
                {
                    _lastSeenBuzzEntryId = latestBuzz.QuizAuditEntryId;
                    if (latestBuzz.EventType == "BuzzAccepted")
                    {
                        PushBuzzAlert(latestBuzz.TeamName, 1);
                    }
                    else
                    {
                        _buzzDismissTimer.Stop();
                        AudienceBuzzIndicator.Text = $"👉 PASSED TO {latestBuzz.TeamName?.ToUpperInvariant()}";
                        AudienceBuzzBorder.Visibility = Visibility.Visible;
                        TriggerBuzzAnimation();
                        _buzzDismissTimer.Start();
                    }
                }
                else if (latestBuzz == null)
                {
                    _lastSeenBuzzEntryId = 0;
                }

                _lastPollingError = null;
            }
            catch (Exception ex)
            {
                if (!string.Equals(_lastPollingError, ex.Message, StringComparison.Ordinal))
                {
                    AppDiagnostics.WriteException("Quiz audience update failed.", ex);
                    _lastPollingError = ex.Message;
                }
                AudienceStatusText.Text = "Audience view updating...";
            }
        }

        private void ShowMedia(DeckSlide slide)
        {
            var mediaPath = slide?.MediaPath?.Trim();
            if (string.Equals(_currentLoadedMediaPath, mediaPath, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(mediaPath))
            {
                // Media is already loaded and active. Do not interrupt or reload every second.
                return;
            }

            _currentLoadedMediaPath = mediaPath;
            AudienceImage.Source = null;
            AudienceMediaPlayer.Stop();
            AudienceMediaPlayer.Source = null;
            AudienceImage.Visibility = Visibility.Collapsed;
            AudienceMediaPlayer.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(mediaPath))
            {
                ApplyMediaDisplayMode(slide, false);
                return;
            }

            var path = Path.GetFullPath(mediaPath);
            if (!File.Exists(path))
            {
                ApplyMediaDisplayMode(slide, false);
                return;
            }

            var uri = new Uri(path);
            var ext = Path.GetExtension(path).ToLowerInvariant();
            var isAudioOnly = slide.MediaDisplayMode == 2;
            var hasVisualMedia = false;

            if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".gif" || ext == ".bmp")
            {
                if (!isAudioOnly)
                {
                    AudienceImage.Source = new BitmapImage(uri);
                    AudienceImage.Visibility = Visibility.Visible;
                    hasVisualMedia = true;
                }
            }
            else if (ext == ".mp4" || ext == ".mp3" || ext == ".wav" || ext == ".wmv" || ext == ".avi")
            {
                AudienceMediaPlayer.Source = uri;
                if (!isAudioOnly)
                {
                    AudienceMediaPlayer.Visibility = Visibility.Visible;
                    hasVisualMedia = true;
                }
                AudienceMediaPlayer.Play();
            }

            ApplyMediaDisplayMode(slide, hasVisualMedia);
        }

        private void ClockTimer_Tick(object sender, EventArgs e)
        {
            UpdateClockDisplay();
        }

        private void UpdateClockDisplay()
        {
            if (_timerDurationSeconds <= 0)
            {
                AudienceTimerLabel.Text = "00:00";
                AudienceTimerLabel.Foreground = TimerNormalBrush;
                return;
            }

            var elapsed = _timerElapsedSeconds;
            if (_timerState == "Running" && _timerStartedAtUtc.HasValue)
            {
                elapsed += (int)(DateTime.UtcNow - _timerStartedAtUtc.Value).TotalSeconds;
            }

            var remaining = Math.Max(0, _timerDurationSeconds - elapsed);
            AudienceTimerLabel.Text = $"{remaining / 60:D2}:{remaining % 60:D2}";

            if (remaining <= 5 && remaining > 0)
            {
                AudienceTimerLabel.Foreground = TimerWarningBrush;
                if (_timerState == "Running") StartTimerPulse();
                else StopTimerPulse();
            }
            else if (remaining == 0)
            {
                AudienceTimerLabel.Foreground = TimerExpiredBrush;
                StopTimerPulse();
            }
            else
            {
                AudienceTimerLabel.Foreground = TimerNormalBrush;
                StopTimerPulse();
            }
        }

        public void ShowOnAudienceScreen(Window host)
        {
            var hostScreen = Screen.FromHandle(new WindowInteropHelper(host).Handle);
            var audienceScreen = Screen.AllScreens.FirstOrDefault(screen => screen.DeviceName != hostScreen.DeviceName);
            if (audienceScreen == null)
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
                WindowState = WindowState.Maximized;
                Show();
                return;
            }
            WindowStartupLocation = WindowStartupLocation.Manual;
            SourceInitialized += (sender, args) =>
            {
                var handle = new WindowInteropHelper(this).Handle;
                var monitor = MonitorFromPoint(new POINT { X = audienceScreen.Bounds.Left + 1, Y = audienceScreen.Bounds.Top + 1 }, 2 /* MONITOR_DEFAULTTONEAREST */);
                var info = new MONITORINFO { Size = Marshal.SizeOf(typeof(MONITORINFO)) };
                if (GetMonitorInfo(monitor, ref info))
                    SetWindowPos(handle, IntPtr.Zero, info.Monitor.Left, info.Monitor.Top, info.Monitor.Right - info.Monitor.Left, info.Monitor.Bottom - info.Monitor.Top, 0x0014);
                else
                    SetWindowPos(handle, IntPtr.Zero, audienceScreen.Bounds.Left, audienceScreen.Bounds.Top, audienceScreen.Bounds.Width, audienceScreen.Bounds.Height, 0x0014);
            };
            WindowState = WindowState.Maximized;
            Show();
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT { public int X; public int Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT { public int Left, Top, Right, Bottom; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int Size;
            public RECT Monitor;
            public RECT WorkArea;
            public uint Flags;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromPoint(POINT pt, uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    }
}
