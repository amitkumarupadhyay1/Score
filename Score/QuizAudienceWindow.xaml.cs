using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Score
{
    public partial class QuizAudienceWindow : Window
    {
        private readonly ScoreDbContext _db = new ScoreDbContext();
        private readonly DispatcherTimer _pollTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        private readonly DispatcherTimer _clockTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };
        private int _currentSessionId;
        private int _currentSlideIndex = -1;
        private int _timerDurationSeconds;
        private int _timerElapsedSeconds;
        private DateTime? _timerStartedAtUtc;
        private string _timerState;
        private string _lastPollingError;

        public QuizAudienceWindow()
        {
            InitializeComponent();
            _pollTimer.Tick += PollTimer_Tick;
            _clockTimer.Tick += ClockTimer_Tick;
            Loaded += QuizAudienceWindow_Loaded;
            Closed += QuizAudienceWindow_Closed;
        }

        private void QuizAudienceWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _pollTimer.Start();
            _clockTimer.Start();
        }

        private void QuizAudienceWindow_Closed(object sender, EventArgs e)
        {
            _pollTimer.Stop();
            _clockTimer.Stop();
            AudienceMediaPlayer.Stop();
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
                    _currentSessionId = 0;
                    _currentSlideIndex = -1;
                    AudienceDeckTitle.Text = "QUIZ SCORE LIVE";
                    AudienceRoundName.Text = "WAITING";
                    AudiencePointsLabel.Text = "STANDBY";
                    AudienceSlideTitle.Text = "Quiz will begin shortly";
                    AudienceSlideContent.Text = "Please take your seats. The host will start the session soon.";
                    AudienceAnswerContainer.Visibility = Visibility.Collapsed;
                    AudienceCluesPanel.Visibility = Visibility.Collapsed;
                    AudienceMediaPanel.Visibility = Visibility.Collapsed;
                    AudienceImage.Source = null;
                    AudienceMediaPlayer.Stop();
                    AudienceMediaPlayer.Source = null;
                    AudienceBuzzBorder.Visibility = Visibility.Collapsed;
                    AudienceScoreboard.ItemsSource = null;
                    AudienceTimerLabel.Text = "00:00";
                    AudienceStatusText.Text = string.Empty;
                    return;
                }

                var deck = _db.QuizDecks.AsNoTracking()
                    .Single(d => d.QuizDeckId == session.QuizDeckId);
                AudienceDeckTitle.Text = deck.Title ?? "QUIZ";

                var slides = _db.DeckSlides.AsNoTracking()
                    .Where(s => s.QuizDeckId == deck.QuizDeckId && s.IsActive)
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
                var slideChanged = _currentSessionId != session.QuizDeckSessionId || _currentSlideIndex != slideIndex;

                if (slideChanged)
                {
                    _currentSessionId = session.QuizDeckSessionId;
                    _currentSlideIndex = slideIndex;
                    var isInstruction = string.Equals(slide.SlideType, "Instruction", StringComparison.OrdinalIgnoreCase);
                    var isMedia = string.Equals(slide.SlideType, "Media", StringComparison.OrdinalIgnoreCase);
                    var isTimer = string.Equals(slide.SlideType, "Timer", StringComparison.OrdinalIgnoreCase);
                    var isScoreboard = string.Equals(slide.SlideType, "Scoreboard", StringComparison.OrdinalIgnoreCase);
                    var isQuestion = string.Equals(slide.SlideType ?? "Question", "Question", StringComparison.OrdinalIgnoreCase);

                    AudienceSlideType.Text = (slide.SlideType ?? "QUESTION").ToUpperInvariant();
                    AudienceRoundName.Text = (slide.RoundName ?? "ROUND 1").ToUpperInvariant();
                    AudiencePointsLabel.Text = isInstruction ? "RULES / ANNOUNCEMENT" :
                                               isMedia ? "MEDIA CLUE" :
                                               isTimer ? $"TIMER ({slide.TimeLimitSeconds}s)" :
                                               isScoreboard ? "SCOREBOARD" :
                                               $"{slide.Points} POINTS";
                    AudienceSlideTitle.Text = slide.Title ?? string.Empty;
                    AudienceSlideContent.Text = slide.ContentText ?? string.Empty;
                    AudienceOptions.ItemsSource = isQuestion
                        ? QuizScoringEngine.ParseOptions(slide.OptionsJson)
                            .Select((option, index) => $"{(char)('A' + index)}. {option}")
                            .ToList()
                        : new List<string>();
                    ShowMedia(slide);
                }

                // Progressive Clues
                var hints = (slide.HintsJson ?? string.Empty)
                    .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (session.ClueRevealStep > 0 && hints.Length > 0)
                {
                    var revealedCount = Math.Min(session.ClueRevealStep, hints.Length);
                    AudienceCluesList.ItemsSource = hints.Take(revealedCount).Select((h, i) => $"Clue {i + 1}: {h}").ToList();
                    AudienceCluesPanel.Visibility = Visibility.Visible;
                }
                else
                {
                    AudienceCluesPanel.Visibility = Visibility.Collapsed;
                }

                // Answer Reveal
                var shouldRevealAnswer = session.AnswerRevealed && !string.IsNullOrWhiteSpace(slide.CorrectAnswer);
                if (shouldRevealAnswer)
                {
                    AudienceAnswerText.Text = slide.CorrectAnswer;
                    AudienceExplanationText.Text = slide.Explanation ?? string.Empty;
                    AudienceExplanationText.Visibility = string.IsNullOrWhiteSpace(slide.Explanation) ? Visibility.Collapsed : Visibility.Visible;
                    AudienceAnswerContainer.Visibility = Visibility.Visible;
                }
                else
                {
                    AudienceAnswerContainer.Visibility = Visibility.Collapsed;
                }

                // Timer parameters
                _timerDurationSeconds = Math.Max(0, slide.TimeLimitSeconds);
                _timerElapsedSeconds = Math.Max(0, session.TimerElapsedSeconds);
                _timerStartedAtUtc = session.TimerStartedAtUtc;
                _timerState = session.TimerState;

                // Scoreboard
                var teamNames = _db.QuizDeckSessionTeams.AsNoTracking()
                    .Where(t => t.QuizDeckSessionId == session.QuizDeckSessionId)
                    .OrderBy(t => t.QuizDeckSessionTeamId)
                    .Select(t => t.TeamName)
                    .ToList();
                var scores = _db.QuizResponses.AsNoTracking()
                    .Where(r => r.QuizDeckSessionId == session.QuizDeckSessionId)
                    .GroupBy(r => r.TeamName)
                    .Select(g => new { Team = g.Key, Score = g.Sum(r => r.PointsAwarded) })
                    .ToList()
                    .ToDictionary(item => item.Team, item => item.Score);

                AudienceScoreboard.ItemsSource = teamNames
                    .Select(team => new
                    {
                        Team = team,
                        Score = scores.TryGetValue(team, out var sc) ? sc : 0
                    })
                    .OrderByDescending(item => item.Score)
                    .ToList();

                // Buzz / Turn Indicator
                var latestBuzz = _db.QuizAuditEntries.AsNoTracking()
                    .Where(a => a.QuizDeckSessionId == session.QuizDeckSessionId &&
                                a.SlideNumber == slideIndex + 1 &&
                                (a.EventType == "BuzzAccepted" || a.EventType == "PassedToTeam"))
                    .OrderByDescending(a => a.QuizAuditEntryId)
                    .FirstOrDefault();

                if (latestBuzz != null)
                {
                    AudienceBuzzIndicator.Text = latestBuzz.EventType == "BuzzAccepted"
                        ? $"🚨 {latestBuzz.TeamName.ToUpperInvariant()} BUZZED IN! 🚨"
                        : $"👉 PASSED TO {latestBuzz.TeamName.ToUpperInvariant()}";
                    AudienceBuzzBorder.Visibility = Visibility.Visible;
                }
                else
                {
                    AudienceBuzzBorder.Visibility = Visibility.Collapsed;
                }

                AudienceStatusText.Text = session.Status == "Paused" ? "⏸ QUIZ PAUSED" :
                    session.TimerState == "Paused" ? "⏸ TIMER HELD" : string.Empty;
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
            AudienceImage.Source = null;
            AudienceMediaPlayer.Stop();
            AudienceMediaPlayer.Source = null;
            AudienceImage.Visibility = Visibility.Collapsed;
            AudienceMediaPlayer.Visibility = Visibility.Collapsed;
            AudienceMediaPanel.Visibility = Visibility.Collapsed;

            if (string.IsNullOrWhiteSpace(slide.MediaPath)) return;
            var path = Path.GetFullPath(slide.MediaPath);
            if (!File.Exists(path)) return;

            var uri = new Uri(path);
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".jpg" || ext == ".jpeg" || ext == ".png")
            {
                AudienceImage.Source = new BitmapImage(uri);
                AudienceImage.Visibility = Visibility.Visible;
                AudienceMediaPanel.Visibility = Visibility.Visible;
            }
            else if (ext == ".mp4" || ext == ".mp3" || ext == ".wav")
            {
                AudienceMediaPlayer.Source = uri;
                AudienceMediaPlayer.Visibility = Visibility.Visible;
                AudienceMediaPanel.Visibility = Visibility.Visible;
                AudienceMediaPlayer.Play();
            }
        }

        private void ClockTimer_Tick(object sender, EventArgs e)
        {
            if (_timerDurationSeconds <= 0)
            {
                AudienceTimerLabel.Text = "00:00";
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
                AudienceTimerLabel.Foreground = (Brush)new BrushConverter().ConvertFromString("#FFF43F5E");
            }
            else if (remaining == 0)
            {
                AudienceTimerLabel.Foreground = (Brush)new BrushConverter().ConvertFromString("#FFEF4444");
            }
            else
            {
                AudienceTimerLabel.Foreground = (Brush)new BrushConverter().ConvertFromString("#FF6FFFE9");
            }
        }

        public void ShowOnAudienceScreen(Window host)
        {
            var hostScreen = Screen.FromHandle(new WindowInteropHelper(host).Handle);
            var audienceScreen = Screen.AllScreens.FirstOrDefault(screen => screen.DeviceName != hostScreen.DeviceName);
            if (audienceScreen == null)
            {
                WindowStartupLocation = WindowStartupLocation.CenterScreen;
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
