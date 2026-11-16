using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Score
{
    public partial class QuizMasterConsoleWindow : Window
    {
        private readonly ScoreDbContext _db = new ScoreDbContext();
        private readonly QuizTimerService _questionTimer = new QuizTimerService();
        private readonly DispatcherTimer _timerRefresh = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) };

        private QuizDeck _currentDeck;
        private QuizDeckSession _currentSession;
        private int _currentSlideIndex = 0;
        private readonly List<string> _teams = new List<string>();
        private readonly List<string> _buzzQueue = new List<string>();
        private readonly HashSet<string> _buzzedTeams = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, int> _teamScores = new Dictionary<string, int>();
        private int _buzzOrderCounter = 0;
        private string _currentBuzzingTeam = null;
        private List<DeckSlide> _slideList = new List<DeckSlide>();
        private bool _loadingDeckSelector;
        private bool _hasCorrectBuzzForSlide;
        private bool _refreshingSlideList;
        private int _clueStep = 0;
        private int _passCount = 0;
        private int _lastWarningSecond = -1;
        private QuizBuzzerServer _buzzerServer;

        private DeckSlide _currentSlide => _slideList != null && _currentSlideIndex >= 0 && _currentSlideIndex < _slideList.Count
            ? _slideList[_currentSlideIndex] : null;

        public QuizMasterConsoleWindow()
        {
            InitializeComponent();
            _timerRefresh.Tick += TimerRefresh_Tick;
            Loaded += QuizMasterConsoleWindow_Loaded;
            Closing += QuizMasterConsoleWindow_Closing;
        }

        private void UpdateSessionBadge()
        {
            var isLive = _currentSession != null && (_currentSession.Status == "Running" || _currentSession.Status == "Paused");
            if (ConsoleSessionBadge != null)
            {
                ConsoleSessionBadge.Background = isLive
                    ? (Brush)new BrushConverter().ConvertFromString("#FF065F46")
                    : (Brush)new BrushConverter().ConvertFromString("#FF1E293B");
                ConsoleSessionBadge.BorderBrush = isLive
                    ? (Brush)new BrushConverter().ConvertFromString("#FF047857")
                    : (Brush)new BrushConverter().ConvertFromString("#FF334155");
            }
            if (ConsoleSessionDot != null)
            {
                ConsoleSessionDot.Text = isLive ? "●" : "○";
                ConsoleSessionDot.Foreground = isLive
                    ? (Brush)new BrushConverter().ConvertFromString("#FF34D399")
                    : (Brush)new BrushConverter().ConvertFromString("#FF94A3B8");
            }
            if (ConsoleSessionBadgeText != null)
            {
                ConsoleSessionBadgeText.Text = isLive ? "LIVE QUIZ" : "STANDBY";
                ConsoleSessionBadgeText.Foreground = isLive
                    ? (Brush)new BrushConverter().ConvertFromString("#FFFFFFFF")
                    : (Brush)new BrushConverter().ConvertFromString("#FF94A3B8");
            }
        }

        private void QuizMasterConsoleWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                _buzzerServer?.Dispose();
                _buzzerServer = null;

                if (_currentSession != null && (_currentSession.Status == "Running" || _currentSession.Status == "Paused"))
                {
                    if (IsHeadlessTestEnvironment())
                    {
                        _currentSession.Status = "Completed";
                        _currentSession.CompletedAtUtc = DateTime.UtcNow;
                        _db.SaveChanges();
                    }
                    else
                    {
                        var res = MessageBox.Show(this,
                            $"A quiz session for '{_currentDeck?.Title ?? "the current deck"}' is still active.\n\nWould you like to finish and complete this session now?\n\n• Click Yes to mark the session as Completed (unlocks deck for editing).\n• Click No to keep it active (so you can resume later).\n• Click Cancel to stay in the quiz desk.",
                            "Active Quiz Session",
                            MessageBoxButton.YesNoCancel,
                            MessageBoxImage.Question);

                        if (res == MessageBoxResult.Cancel)
                        {
                            e.Cancel = true;
                            return;
                        }

                        if (res == MessageBoxResult.Yes)
                        {
                            _currentSession.Status = "Completed";
                            _currentSession.CompletedAtUtc = DateTime.UtcNow;
                            _currentSession.TimerStartedAtUtc = null;
                            _currentSession.TimerState = "Stopped";
                            _db.SaveChanges();
                        }
                    }
                }
            }
            catch { }
        }

        private static bool IsHeadlessTestEnvironment()
        {
            var friendly = AppDomain.CurrentDomain.FriendlyName;
            if (friendly != null && friendly.IndexOf("Score.Tests", StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("QUIZSCORE_HEADLESS_TEST"));
        }

        private void QuizMasterConsoleWindow_Loaded(object sender, RoutedEventArgs e)
        {
            LoadDeckSelector();
            ResumeActiveSession();
            RefreshTeams();
            UpdateSoundButtonState();
            EnsureBuzzerServerStarted();
            UpdateSessionBadge();
        }

        private void EnsureBuzzerServerStarted()
        {
            try
            {
                if (_buzzerServer == null)
                {
                    _buzzerServer = new QuizBuzzerServer();
                    _buzzerServer.GetTeamsCallback = () => _teams;
                    _buzzerServer.OnBuzzReceived += (team) =>
                    {
                        Dispatcher.Invoke(() => RegisterBuzz(team));
                    };
                    _buzzerServer.Start();
                }
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Could not auto-start QuizBuzzerServer.", ex);
            }
        }

        private void SoundToggleButton_Click(object sender, RoutedEventArgs e)
        {
            QuizChimes.IsMuted = !QuizChimes.IsMuted;
            UpdateSoundButtonState();
        }

        private void UpdateSoundButtonState()
        {
            if (QuizChimes.IsMuted)
            {
                SoundToggleButton.Content = "🔇 MUTE";
                SoundToggleButton.Background = (Brush)new BrushConverter().ConvertFromString("#FF64748B");
                SoundToggleButton.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#FF475569");
            }
            else
            {
                SoundToggleButton.Content = "🔊 SOUND ON";
                SoundToggleButton.Background = (Brush)new BrushConverter().ConvertFromString("#FF059669");
                SoundToggleButton.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#FF047857");
            }
        }

        private void MobileBuzzerButton_Click(object sender, RoutedEventArgs e)
        {
            EnsureBuzzerServerStarted();
            var dialog = new QuizBuzzerDialog(_buzzerServer) { Owner = this };
            dialog.ShowDialog();
        }

        private void LoadDeckSelector()
        {
            var decks = _db.QuizDecks.Where(d => d.IsActive).OrderBy(d => d.Title).ToList();
            if (decks.Count == 0)
            {
                ConsoleStatusText.Text = "No quiz deck found. Create one in Quiz Authoring Studio.";
                return;
            }
            _loadingDeckSelector = true;
            ConsoleDeckSelector.ItemsSource = decks;
            ConsoleDeckSelector.SelectedItem = decks[0];
            _loadingDeckSelector = false;
            LoadDeck(decks[0]);
        }

        private void LoadDeck(QuizDeck deck)
        {
            _currentDeck = deck;
            _slideList = _db.DeckSlides.Where(s => s.QuizDeckId == deck.QuizDeckId && s.IsActive)
                .OrderBy(s => s.SlideOrder).ToList();
            _currentSlideIndex = 0;
            RefreshSlideList();
            ShowCurrentSlide(startTimer: false);
        }

        private void ConsoleDeckSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loadingDeckSelector || !(ConsoleDeckSelector.SelectedItem is QuizDeck deck)) return;

            if (_currentSession != null && _currentSession.Status != "Completed")
            {
                if (deck.QuizDeckId == _currentDeck?.QuizDeckId) return;

                if (!IsHeadlessTestEnvironment())
                {
                    var result = MessageBox.Show(this,
                        $"A quiz session is currently in progress for '{_currentDeck?.Title}'.\n\nWould you like to finish this session before switching to '{deck.Title}'?",
                        "Switch Quiz Deck",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                    {
                        _loadingDeckSelector = true;
                        ConsoleDeckSelector.SelectedValue = _currentDeck.QuizDeckId;
                        _loadingDeckSelector = false;
                        return;
                    }
                }

                _currentSession.Status = "Completed";
                _currentSession.CompletedAtUtc = DateTime.UtcNow;
                _currentSession.TimerStartedAtUtc = null;
                _currentSession.TimerState = "Stopped";
                _db.SaveChanges();
            }

            _currentSession = null;
            StartQuizButton.IsEnabled = true;
            UpdateSessionBadge();
            LoadDeck(deck);
            ConsoleStatusText.Text = $"Deck '{deck.Title}' loaded. Ready to start quiz.";
        }

        private void RefreshSlideList()
        {
            _refreshingSlideList = true;
            ConsoleSlideList.ItemsSource = null;
            ConsoleSlideList.ItemsSource = _slideList.Select((s, i) => new
            {
                Index = i + 1,
                SlideType = s.SlideType == "Question" ? $"{s.QuestionType ?? "Question"}" : s.SlideType,
                s.Title
            });
            if (_slideList.Count > 0 && ConsoleSlideList.SelectedIndex < 0)
                ConsoleSlideList.SelectedIndex = 0;
            _refreshingSlideList = false;
        }

        private void RefreshTeams()
        {
            ConsoleTeamList.ItemsSource = null;
            var teamItems = _teams.Select((t, index) => new
            {
                QuickKey = index < 9 ? $"[{index + 1}]" : "   ",
                Team = t,
                Score = _teamScores.TryGetValue(t, out var sc) ? sc : 0,
                Status = _currentBuzzingTeam == t ? "ANSWERING" :
                         _buzzQueue.Contains(t) ? $"QUEUED #{_buzzQueue.IndexOf(t) + 1}" :
                         _buzzedTeams.Contains(t) ? "BUZZED" : string.Empty
            }).ToList();
            ConsoleTeamList.SelectedValuePath = "Team";
            ConsoleTeamList.ItemsSource = teamItems;
            if (_currentBuzzingTeam != null)
                ConsoleTeamList.SelectedValue = _currentBuzzingTeam;
            else if (teamItems.Count > 0 && ConsoleTeamList.SelectedIndex < 0)
                ConsoleTeamList.SelectedIndex = 0;

            UpdateBuzzQueueDisplay();
        }

        private void UpdateBuzzQueueDisplay()
        {
            if (_buzzQueue.Count > 0)
            {
                BuzzQueueContainer.Visibility = Visibility.Visible;
                BuzzQueueListLabel.Text = string.Join(" → ", _buzzQueue.Select((t, i) => $"{i + 1}. {t}"));
            }
            else
            {
                BuzzQueueContainer.Visibility = Visibility.Collapsed;
            }
        }

        private void ResumeActiveSession()
        {
            _currentSession = _db.QuizDeckSessions
                .Where(s => s.Status == "Running" || s.Status == "Paused")
                .OrderByDescending(s => s.QuizDeckSessionId)
                .FirstOrDefault();
            if (_currentSession == null) return;

            var deck = _db.QuizDecks.SingleOrDefault(d => d.QuizDeckId == _currentSession.QuizDeckId);
            if (deck == null) return;

            _currentDeck = deck;
            _loadingDeckSelector = true;
            ConsoleDeckSelector.SelectedValue = deck.QuizDeckId;
            _loadingDeckSelector = false;
            LoadDeck(deck);
            _teams.Clear();
            _teams.AddRange(_db.QuizDeckSessionTeams
                .Where(t => t.QuizDeckSessionId == _currentSession.QuizDeckSessionId)
                .OrderBy(t => t.QuizDeckSessionTeamId)
                .Select(t => t.TeamName).ToList());

            if (_slideList.Count == 0)
            {
                ConsoleStatusText.Text = "The active quiz has no available slides.";
                return;
            }

            _teamScores.Clear();
            foreach (var score in _db.QuizResponses
                .Where(r => r.QuizDeckSessionId == _currentSession.QuizDeckSessionId)
                .GroupBy(r => r.TeamName)
                .Select(g => new { TeamName = g.Key, Score = g.Sum(r => r.PointsAwarded) }).ToList())
                _teamScores[score.TeamName] = score.Score;

            _currentSlideIndex = Math.Max(0, Math.Min(_currentSession.CurrentSlideIndex, _slideList.Count - 1));
            _clueStep = _currentSession.ClueRevealStep;
            _passCount = _currentSession.PassCount;
            StartQuizButton.IsEnabled = false;
            ConsoleDeckSelector.IsEnabled = true;
            ShowCurrentSlide(false);

            RefreshTeams();
            ConsoleStatusText.Text = $"Active quiz session #{_currentSession.QuizDeckSessionId} restored for '{_currentDeck.Title}'. Click 'FINISH QUIZ' when done, or select another deck to switch.";
        }

        private void StartQuizButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDeck == null || _slideList.Count == 0)
            {
                ConsoleStatusText.Text = "No deck or slides available.";
                return;
            }

            var teamDialog = new TeamEntryDialog { Owner = this };
            if (teamDialog.ShowDialog() == true)
            {
                _teams.Clear();
                _teams.AddRange(teamDialog.TeamNames);
                _teamScores.Clear();
                foreach (var t in _teams) _teamScores[t] = 0;

                _currentSession = new QuizDeckSession
                {
                    QuizDeckId = _currentDeck.QuizDeckId,
                    StartedAtUtc = DateTime.UtcNow,
                    Status = "Running",
                    CurrentSlideIndex = 0,
                    ActiveTeamOnFloor = _teams.Count > 0 ? _teams[0] : string.Empty,
                    PassCount = 0,
                    ClueRevealStep = 0
                };

                using (var transaction = _db.Database.BeginTransaction())
                {
                    _db.QuizDeckSessions.Add(_currentSession);
                    _db.SaveChanges();
                    foreach (var team in _teams)
                        _db.QuizDeckSessionTeams.Add(new QuizDeckSessionTeam
                        {
                            QuizDeckSessionId = _currentSession.QuizDeckSessionId,
                            TeamName = team
                        });
                    _db.SaveChanges();
                    transaction.Commit();
                }

                _currentSlideIndex = 0;
                _buzzOrderCounter = 0;
                _currentBuzzingTeam = null;
                _buzzQueue.Clear();
                _buzzedTeams.Clear();
                _hasCorrectBuzzForSlide = false;
                _clueStep = 0;
                _passCount = 0;
                StartQuizButton.IsEnabled = false;
                ConsoleDeckSelector.IsEnabled = false;
                RefreshSlideList();
                RefreshTeams();
                ShowCurrentSlide();
                ConsoleStatusText.Text = "Quiz started.";
            }
        }

        private void ShowCurrentSlide(bool startTimer = true)
        {
            if (_currentSlide == null) return;

            var isQuestion = string.Equals(_currentSlide.SlideType ?? "Question", "Question", StringComparison.OrdinalIgnoreCase);
            var isInstruction = string.Equals(_currentSlide.SlideType, "Instruction", StringComparison.OrdinalIgnoreCase);
            var isMedia = string.Equals(_currentSlide.SlideType, "Media", StringComparison.OrdinalIgnoreCase);
            var isTimer = string.Equals(_currentSlide.SlideType, "Timer", StringComparison.OrdinalIgnoreCase);
            var isScoreboard = string.Equals(_currentSlide.SlideType, "Scoreboard", StringComparison.OrdinalIgnoreCase);

            ConsoleRoundText.Text = _currentSlide.RoundName ?? "General";
            ConsoleSlideType.Text = isQuestion
                ? $"{_currentSlide.QuestionType ?? "Question"} · {_currentSlide.RoundType ?? "Standard"}"
                : _currentSlide.SlideType;
            ConsolePointsBadge.Text = isQuestion ? $"+{_currentSlide.Points} pts" :
                                      isInstruction ? "RULES" :
                                      isMedia ? "MEDIA" :
                                      isTimer ? $"⏱ {_currentSlide.TimeLimitSeconds}s" :
                                      isScoreboard ? "BOARD" : "INFO";

            SubmitAnswerButton.IsEnabled = isQuestion;
            AcceptBuzzButton.IsEnabled = isQuestion;
            MarkCorrectButton.IsEnabled = isQuestion;
            MarkIncorrectButton.IsEnabled = isQuestion;
            PassButton.IsEnabled = isQuestion && _currentSlide.AllowPass;
            RevealAnswerButton.IsEnabled = isQuestion;

            ConsoleSlideTitle.Text = _currentSlide.Title ?? string.Empty;
            ConsoleSlideContent.Text = _currentSlide.ContentText ?? string.Empty;

            var options = isQuestion ? QuizScoringEngine.ParseOptions(_currentSlide.OptionsJson) : new List<string>();
            if (options.Count > 0)
            {
                ConsoleSlideOptions.Visibility = Visibility.Visible;
                ConsoleSlideOptions.Text = string.Join(Environment.NewLine,
                    options.Select((option, index) => $"{(char)('A' + index)}. {option}"));
            }
            else
            {
                ConsoleSlideOptions.Visibility = Visibility.Collapsed;
                ConsoleSlideOptions.Text = string.Empty;
            }

            ConsoleAnswerKey.Text = string.IsNullOrWhiteSpace(_currentSlide.CorrectAnswer)
                ? "No answer key configured."
                : "Answer Key: " + _currentSlide.CorrectAnswer;
            ConsoleAlternateAnswers.Text = string.IsNullOrWhiteSpace(_currentSlide.AlternateAnswers)
                ? string.Empty
                : "Accepted Aliases: " + _currentSlide.AlternateAnswers;
            ConsoleExplanation.Text = string.IsNullOrWhiteSpace(_currentSlide.Explanation)
                ? string.Empty
                : "Host Notes / Trivia: " + _currentSlide.Explanation;

            // Hints / Clues
            var hints = (_currentSlide.HintsJson ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (hints.Length > 0 && isQuestion)
            {
                RevealClueButton.Visibility = Visibility.Visible;
                UpdateClueDisplay(hints);
            }
            else
            {
                RevealClueButton.Visibility = Visibility.Collapsed;
                ClueStatusLabel.Text = string.Empty;
            }

            // Floor / Turn Setup
            if (startTimer)
            {
                _passCount = 0;
                _clueStep = 0;
                if (_teams.Count > 0 && _currentSession != null)
                {
                    // Rotate default designated team by question index
                    var defaultTeamIdx = _currentSlideIndex % _teams.Count;
                    _currentSession.ActiveTeamOnFloor = _teams[defaultTeamIdx];
                    ConsoleTeamList.SelectedValue = _currentSession.ActiveTeamOnFloor;
                }
            }

            UpdateFloorBanner();

            _timerRefresh.Stop();
            if (startTimer && _currentSlide.TimeLimitSeconds > 0 && _currentSession != null)
            {
                _questionTimer.Start(TimeSpan.FromSeconds(_currentSlide.TimeLimitSeconds));
                _currentSession.TimerStartedAtUtc = DateTime.UtcNow;
                _currentSession.TimerElapsedSeconds = 0;
                _currentSession.TimerState = "Running";
                _timerRefresh.Start();
            }
            else if (startTimer && _currentSession != null)
            {
                _questionTimer.Reset();
                _currentSession.TimerStartedAtUtc = null;
                _currentSession.TimerElapsedSeconds = 0;
                _currentSession.TimerState = "Stopped";
            }
            else if (_currentSession != null)
            {
                var elapsed = TimeSpan.FromSeconds(Math.Max(0, _currentSession.TimerElapsedSeconds));
                if (_currentSession.TimerState == "Running" && _currentSession.TimerStartedAtUtc.HasValue)
                    elapsed += DateTime.UtcNow - _currentSession.TimerStartedAtUtc.Value;
                var state = _currentSession.TimerState == "Paused" ? QuizTimerState.Paused :
                    _currentSession.TimerState == "Running" ? QuizTimerState.Running : QuizTimerState.Stopped;
                _questionTimer.Restore(TimeSpan.FromSeconds(Math.Max(0, _currentSlide.TimeLimitSeconds)), elapsed, state);
                if (state == QuizTimerState.Running) _timerRefresh.Start();
            }
            else
            {
                // Standby mode
                _questionTimer.Reset();
                if (_currentSlide.TimeLimitSeconds > 0)
                    ConsoleTimerLabel.Text = TimeSpan.FromSeconds(_currentSlide.TimeLimitSeconds).ToString(@"mm\:ss");
                else
                    ConsoleTimerLabel.Text = "00:00";
            }

            _currentBuzzingTeam = null;
            _buzzOrderCounter = 0;
            _buzzQueue.Clear();
            _buzzedTeams.Clear();
            _hasCorrectBuzzForSlide = false;
            var isRevealed = _currentSession != null && _currentSession.AnswerRevealed;
            RevealAnswerButton.Content = isRevealed ? "HIDE ANSWER" : "REVEAL ON STAGE (R)";
            ConsoleAnswerEditor.Text = string.Empty;

            if (_currentSession != null)
            {
                _currentSession.PassCount = _passCount;
                _currentSession.ClueRevealStep = _clueStep;
                _lastWarningSecond = -1;
                _db.SaveChanges();
            }
            RefreshTeams();
            if (_currentSession != null)
            {
                UpdateTimerDisplay();
            }
        }

        private void UpdateFloorBanner()
        {
            if (_currentSlide == null)
            {
                TurnBanner.Visibility = Visibility.Collapsed;
                return;
            }

            TurnBanner.Visibility = Visibility.Visible;
            if (_currentSession == null)
            {
                TurnBanner.Background = (Brush)new BrushConverter().ConvertFromString("#FFF0FDF4");
                TurnBanner.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#FFBBF7D0");
                TurnStatusLabel.Text = "✦ READY TO CONDUCT · CLICK '▶ START QUIZ'";
                TurnStatusLabel.Foreground = (Brush)new BrushConverter().ConvertFromString("#FF166534");
                TurnDetailsLabel.Text = "Browse and preview questions above. Click 'START QUIZ' in the top bar to register teams and enable live buzzers.";
                TurnDetailsLabel.Foreground = (Brush)new BrushConverter().ConvertFromString("#FF15803D");
                PassButton.Visibility = Visibility.Collapsed;
                return;
            }

            PassButton.Visibility = _currentSlide.AllowPass ? Visibility.Visible : Visibility.Collapsed;
            TurnBanner.Background = (Brush)new BrushConverter().ConvertFromString("#FFFEF9C3");
            TurnBanner.BorderBrush = (Brush)new BrushConverter().ConvertFromString("#FFFDE047");
            TurnStatusLabel.Foreground = (Brush)new BrushConverter().ConvertFromString("#FF854D0E");
            TurnDetailsLabel.Foreground = (Brush)new BrushConverter().ConvertFromString("#FFA16207");

            var activeTeam = _currentSession?.ActiveTeamOnFloor ?? (ConsoleTeamList.SelectedValue as string);
            if (string.IsNullOrWhiteSpace(activeTeam) && _teams.Count > 0) activeTeam = _teams[0];

            if (_passCount == 0)
            {
                TurnStatusLabel.Text = $"ACTIVE FLOOR: {activeTeam?.ToUpperInvariant()}";
                TurnDetailsLabel.Text = $"Direct question · Full score: +{_currentSlide.Points} pts";
            }
            else
            {
                var bonus = _currentSlide.BonusPoints > 0 ? _currentSlide.BonusPoints : (_currentSlide.Points / 2);
                TurnStatusLabel.Text = $"BOUNCE #{_passCount}: {activeTeam?.ToUpperInvariant()}";
                TurnDetailsLabel.Text = $"Passed question · Bonus score: +{bonus} pts";
            }
        }

        private void UpdateClueDisplay(string[] hints)
        {
            if (_clueStep == 0)
            {
                ClueStatusLabel.Text = $"0 of {hints.Length} clues revealed";
                RevealClueButton.Content = "💡 REVEAL CLUE 1";
            }
            else if (_clueStep <= hints.Length)
            {
                ClueStatusLabel.Text = $"Clue {_clueStep}/{hints.Length}: \"{hints[_clueStep - 1]}\"";
                RevealClueButton.Content = _clueStep < hints.Length
                    ? $"💡 REVEAL CLUE {_clueStep + 1}"
                    : "ALL CLUES REVEALED";
            }
        }

        private void RevealClueButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSlide == null || _currentSession == null) return;
            var hints = (_currentSlide.HintsJson ?? string.Empty)
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (_clueStep < hints.Length)
            {
                _clueStep++;
                _currentSession.ClueRevealStep = _clueStep;
                _db.SaveChanges();
                UpdateClueDisplay(hints);
                ConsoleStatusText.Text = $"Clue {_clueStep} revealed to the audience.";
            }
        }

        private void ConsoleSlideList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_refreshingSlideList || ConsoleSlideList.SelectedItem == null) return;
            var idx = ConsoleSlideList.SelectedIndex;
            if (idx >= 0 && idx < _slideList.Count)
            {
                _currentSlideIndex = idx;
                if (_currentSession != null)
                {
                    _currentSession.CurrentSlideIndex = idx;
                    _db.SaveChanges();
                }
                ShowCurrentSlide(startTimer: _currentSession != null && _currentSession.Status == "Running");
            }
        }

        private void NextSlideButton_Click(object sender, RoutedEventArgs e)
        {
            AdvanceSlide(1);
        }

        private void PrevSlideButton_Click(object sender, RoutedEventArgs e)
        {
            AdvanceSlide(-1);
        }

        private void AdvanceSlide(int delta)
        {
            var newIndex = _currentSlideIndex + delta;
            if (newIndex >= 0 && newIndex < _slideList.Count)
            {
                _currentSlideIndex = newIndex;
                if (_currentSession != null)
                {
                    _currentSession.CurrentSlideIndex = _currentSlideIndex;
                    _db.SaveChanges();
                }
                _questionTimer.Stop();
                ShowCurrentSlide(startTimer: _currentSession != null && _currentSession.Status == "Running");
                RefreshSlideList();
                ConsoleSlideList.SelectedIndex = _currentSlideIndex;
            }
        }



        private void EndQuizButton_Click(object sender, RoutedEventArgs e)
        {
            if (_currentSession != null)
            {
                _currentSession.CompletedAtUtc = DateTime.UtcNow;
                _currentSession.Status = "Completed";
                _currentSession.TimerStartedAtUtc = null;
                _currentSession.TimerState = "Stopped";
                _db.SaveChanges();
                _currentSession = null;
            }
            _questionTimer.Stop();
            _timerRefresh.Stop();
            StartQuizButton.IsEnabled = true;
            ConsoleDeckSelector.IsEnabled = true;
            UpdateSessionBadge();
            ShowCurrentSlide(startTimer: false);
            ConsoleStatusText.Text = "Quiz session completed.";
        }
    }
}
