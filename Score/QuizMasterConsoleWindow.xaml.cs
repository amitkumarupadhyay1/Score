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
        private QuizAudienceWindow _audienceWindow;
        private bool _refreshingTeamList;

        private DeckSlide _currentSlide => _slideList != null && _currentSlideIndex >= 0 && _currentSlideIndex < _slideList.Count
            ? _slideList[_currentSlideIndex] : null;

        private int? _initialDeckId;

        public QuizMasterConsoleWindow()
        {
            InitializeComponent();
            _timerRefresh.Tick += TimerRefresh_Tick;
            Loaded += QuizMasterConsoleWindow_Loaded;
            Closing += QuizMasterConsoleWindow_Closing;
            if (ConsoleTeamList != null)
                ConsoleTeamList.SelectionChanged += ConsoleTeamList_SelectionChanged;
        }

        public QuizMasterConsoleWindow(int deckId) : this() => _initialDeckId = deckId;

        private void UpdateSessionBadge()
        {
            var isLive = _currentSession != null && (_currentSession.Status == "Running" || _currentSession.Status == "Paused");
            var conv = new BrushConverter();
            if (ConsoleSessionBadge != null)
            {
                ConsoleSessionBadge.Background = (Brush)conv.ConvertFromString(isLive ? "#FF065F46" : "#FF1E293B");
                ConsoleSessionBadge.BorderBrush = (Brush)conv.ConvertFromString(isLive ? "#FF047857" : "#FF334155");
            }
            if (ConsoleSessionDot != null)
            {
                ConsoleSessionDot.Text = isLive ? "●" : "○";
                ConsoleSessionDot.Foreground = (Brush)conv.ConvertFromString(isLive ? "#FF34D399" : "#FF94A3B8");
            }
            if (ConsoleSessionBadgeText != null)
            {
                ConsoleSessionBadgeText.Text = isLive ? "LIVE QUIZ" : "STANDBY";
                ConsoleSessionBadgeText.Foreground = (Brush)conv.ConvertFromString(isLive ? "#FFFFFFFF" : "#FF94A3B8");
            }

            // Toggle button visibility: hide START when live, hide FINISH when standby
            if (StartQuizButton != null)
                StartQuizButton.Visibility = isLive ? Visibility.Collapsed : Visibility.Visible;
            if (EndQuizButton != null)
                EndQuizButton.Visibility = isLive ? Visibility.Visible : Visibility.Collapsed;

            // Lock deck switching during live quiz
            if (ConsoleDeckSelector != null)
                ConsoleDeckSelector.IsEnabled = !isLive;
        }

        private void QuizMasterConsoleWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            try
            {
                if (_currentSession != null && (_currentSession.Status == "Running" || _currentSession.Status == "Paused"))
                {
                    if (IsHeadlessTestEnvironment())
                    {
                        _buzzerServer?.Dispose();
                        _buzzerServer = null;
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
                            _buzzerServer?.Dispose();
                            _buzzerServer = null;
                            _currentSession.Status = "Completed";
                            _currentSession.CompletedAtUtc = DateTime.UtcNow;
                            _currentSession.TimerStartedAtUtc = null;
                            _currentSession.TimerState = "Stopped";
                            _db.SaveChanges();
                        }
                    }
                }
                else
                {
                    _buzzerServer?.Dispose();
                    _buzzerServer = null;
                }
            }
            catch { }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            try
            {
                _buzzerServer?.Dispose();
                _buzzerServer = null;
                if (_audienceWindow != null && _audienceWindow.IsLoaded)
                {
                    _audienceWindow.Close();
                }
                _audienceWindow = null;
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
            var targetDeck = _initialDeckId.HasValue
                ? decks.FirstOrDefault(d => d.QuizDeckId == _initialDeckId.Value) ?? decks[0]
                : decks[0];
            ConsoleDeckSelector.SelectedItem = targetDeck;
            _loadingDeckSelector = false;
            LoadDeck(targetDeck);
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
            _refreshingTeamList = true;
            try
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
            finally
            {
                _refreshingTeamList = false;
            }
        }

        private void UpdateBuzzQueueDisplay()
        {
            BuzzQueueContainer.Visibility = _buzzQueue.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (_buzzQueue.Count > 0)
                BuzzQueueListLabel.Text = string.Join(" → ", _buzzQueue.Select((t, i) => $"{i + 1}. {t}"));
        }

        private void ResumeActiveSession()
        {
            var query = _db.QuizDeckSessions.Where(s => s.Status == "Running" || s.Status == "Paused");
            if (_initialDeckId.HasValue)
            {
                query = query.Where(s => s.QuizDeckId == _initialDeckId.Value);
            }
            _currentSession = query.OrderByDescending(s => s.QuizDeckSessionId).FirstOrDefault();
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
            UpdateSessionBadge();
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
                UpdateSessionBadge();
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

            // Host Media Preview
            if (!string.IsNullOrWhiteSpace(_currentSlide.MediaPath) && System.IO.File.Exists(_currentSlide.MediaPath))
            {
                var ext = System.IO.Path.GetExtension(_currentSlide.MediaPath).ToLowerInvariant();
                if (ext == ".jpg" || ext == ".jpeg" || ext == ".png" || ext == ".gif" || ext == ".bmp")
                {
                    try
                    {
                        ConsoleMediaImage.Source = new System.Windows.Media.Imaging.BitmapImage(new Uri(System.IO.Path.GetFullPath(_currentSlide.MediaPath)));
                        ConsoleMediaPanel.Visibility = Visibility.Visible;
                    }
                    catch
                    {
                        ConsoleMediaPanel.Visibility = Visibility.Collapsed;
                    }
                }
                else
                {
                    ConsoleMediaPanel.Visibility = Visibility.Collapsed;
                }
            }
            else
            {
                ConsoleMediaPanel.Visibility = Visibility.Collapsed;
            }

            var options = isQuestion ? QuizScoringEngine.ParseOptions(_currentSlide.OptionsJson) : new List<string>();
            ConsoleSlideOptions.Visibility = options.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (options.Count > 0)
            {
                var optLines = new List<string>();
                for (int i = 0; i < options.Count; i++)
                {
                    var letter = ((char)('A' + i)).ToString();
                    var isAnswer = string.Equals(_currentSlide.CorrectAnswer?.Trim(), letter, StringComparison.OrdinalIgnoreCase) ||
                                   string.Equals(_currentSlide.CorrectAnswer?.Trim(), options[i], StringComparison.OrdinalIgnoreCase);
                    optLines.Add($"{letter}. {options[i]}{(isAnswer ? "  ✓ [KEY]" : "")}");
                }
                ConsoleSlideOptions.Text = string.Join(Environment.NewLine, optLines);
            }
            else
            {
                ConsoleSlideOptions.Text = string.Empty;
            }

            // Quick Choice Bar
            var isMC = isQuestion && string.Equals(_currentSlide.QuestionType, QuizScoringEngine.QuestionMultipleChoice, StringComparison.OrdinalIgnoreCase);
            var isTF = isQuestion && string.Equals(_currentSlide.QuestionType, QuizScoringEngine.QuestionTrueFalse, StringComparison.OrdinalIgnoreCase);
            if (isMC && options.Count > 0)
            {
                QuickOptionBar.Visibility = Visibility.Visible;
                QuickOptionA.Visibility = Visibility.Visible;
                QuickOptionB.Visibility = options.Count >= 2 ? Visibility.Visible : Visibility.Collapsed;
                QuickOptionC.Visibility = options.Count >= 3 ? Visibility.Visible : Visibility.Collapsed;
                QuickOptionD.Visibility = options.Count >= 4 ? Visibility.Visible : Visibility.Collapsed;
                QuickOptionTrue.Visibility = Visibility.Collapsed;
                QuickOptionFalse.Visibility = Visibility.Collapsed;
            }
            else if (isTF)
            {
                QuickOptionBar.Visibility = Visibility.Visible;
                QuickOptionA.Visibility = Visibility.Collapsed;
                QuickOptionB.Visibility = Visibility.Collapsed;
                QuickOptionC.Visibility = Visibility.Collapsed;
                QuickOptionD.Visibility = Visibility.Collapsed;
                QuickOptionTrue.Visibility = Visibility.Visible;
                QuickOptionFalse.Visibility = Visibility.Visible;
            }
            else
            {
                QuickOptionBar.Visibility = Visibility.Collapsed;
            }

            ConsoleAnswerKey.Text = string.IsNullOrWhiteSpace(_currentSlide.CorrectAnswer) ? "No answer key configured." : "Answer Key: " + _currentSlide.CorrectAnswer;
            ConsoleAlternateAnswers.Text = string.IsNullOrWhiteSpace(_currentSlide.AlternateAnswers) ? string.Empty : "Accepted Aliases: " + _currentSlide.AlternateAnswers;
            ConsoleExplanation.Text = string.IsNullOrWhiteSpace(_currentSlide.Explanation) ? string.Empty : "Host Notes / Trivia: " + _currentSlide.Explanation;

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
            UpdateRevealButtonState();
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
            if (_buzzerServer != null)
            {
                _buzzerServer.IsBuzzerArmed = isQuestion && !_hasCorrectBuzzForSlide;
            }
            PushSlideStateToAudience();
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
                    _currentSession.AnswerRevealed = false;
                    _currentSession.ClueRevealStep = 0;
                    _db.SaveChanges();
                }
                ShowCurrentSlide(startTimer: _currentSession != null && _currentSession.Status == "Running");
            }
        }

        private void NextSlideButton_Click(object sender, RoutedEventArgs e) => AdvanceSlide(1);
        private void PrevSlideButton_Click(object sender, RoutedEventArgs e) => AdvanceSlide(-1);

        private void AdvanceSlide(int delta)
        {
            var newIndex = _currentSlideIndex + delta;
            if (newIndex >= 0 && newIndex < _slideList.Count)
            {
                _currentSlideIndex = newIndex;
                if (_currentSession != null)
                {
                    _currentSession.CurrentSlideIndex = _currentSlideIndex;
                    _currentSession.AnswerRevealed = false;
                    _currentSession.ClueRevealStep = 0;
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
