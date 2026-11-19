using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Microsoft.Win32;

namespace Score
{
    public partial class QuizAudienceWindow : Window
    {
        private string _currentLoadedMediaPath;
        private bool _isFullscreenStage = true;

        public void HookMediaAndEscapeEvents()
        {
            PreviewKeyDown += AudienceWindow_PreviewKeyDown;
            SystemEvents.DisplaySettingsChanged += SystemEvents_DisplaySettingsChanged;
        }

        public void UnhookMediaAndEscapeEvents()
        {
            PreviewKeyDown -= AudienceWindow_PreviewKeyDown;
            SystemEvents.DisplaySettingsChanged -= SystemEvents_DisplaySettingsChanged;
        }

        private void SystemEvents_DisplaySettingsChanged(object sender, EventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                try
                {
                    // If windowed, don't force move
                    if (!_isFullscreenStage) return;
                    var screens = System.Windows.Forms.Screen.AllScreens;
                    if (screens.Length > 1)
                    {
                        var secondary = screens.FirstOrDefault(s => !s.Primary) ?? screens[1];
                        Left = secondary.Bounds.Left;
                        Top = secondary.Bounds.Top;
                        Width = secondary.Bounds.Width;
                        Height = secondary.Bounds.Height;
                        WindowState = WindowState.Maximized;
                    }
                }
                catch { }
            });
        }

        private void AudienceWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                ExitFullScreenStageMode();
                e.Handled = true;
            }
            else if (e.Key == Key.F11)
            {
                ToggleFullScreenStageMode();
                e.Handled = true;
            }
        }

        public void ExitFullScreenStageMode()
        {
            _isFullscreenStage = false;
            WindowStyle = WindowStyle.SingleBorderWindow;
            ResizeMode = ResizeMode.CanResize;
            WindowState = WindowState.Normal;
            Cursor = Cursors.Arrow;
            Mouse.OverrideCursor = null;
            Width = 960;
            Height = 540;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        public void EnterFullScreenStageMode()
        {
            _isFullscreenStage = true;
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
            Cursor = Cursors.None;
            Mouse.OverrideCursor = Cursors.None;
        }

        public void ToggleFullScreenStageMode()
        {
            if (_isFullscreenStage)
                ExitFullScreenStageMode();
            else
                EnterFullScreenStageMode();
        }

        public void PushScoreboard(IEnumerable<string> teamNames, IDictionary<string, int> scores)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => PushScoreboard(teamNames, scores));
                return;
            }

            if (teamNames == null)
            {
                AudienceScoreboard.ItemsSource = null;
                return;
            }

            var dict = scores ?? new Dictionary<string, int>();
            AudienceScoreboard.ItemsSource = teamNames
                .Select(t => new
                {
                    Team = t,
                    Score = dict.TryGetValue(t, out var sc) ? sc : 0
                })
                .OrderByDescending(item => item.Score)
                .ToList();
        }

        public void PauseMediaPlayback()
        {
            try
            {
                if (AudienceMediaPlayer.Visibility == Visibility.Visible && AudienceMediaPlayer.Source != null)
                {
                    AudienceMediaPlayer.Pause();
                }
            }
            catch { }
        }

        public void ResumeMediaPlayback()
        {
            try
            {
                if (AudienceMediaPlayer.Visibility == Visibility.Visible && AudienceMediaPlayer.Source != null)
                {
                    AudienceMediaPlayer.Play();
                }
            }
            catch { }
        }

        public void SetMediaVolume(double volume)
        {
            try
            {
                AudienceMediaPlayer.Volume = Math.Max(0.0, Math.Min(1.0, volume));
            }
            catch { }
        }

        public void ReplayMediaPlayback()
        {
            try
            {
                if (AudienceMediaPlayer.Source != null)
                {
                    AudienceMediaPlayer.Position = TimeSpan.Zero;
                    AudienceMediaPlayer.Play();
                }
            }
            catch { }
        }

        private void ShowMedia(DeckSlide slide)
        {
            var mediaPath = slide?.MediaPath?.Trim();
            if (string.IsNullOrEmpty(_currentLoadedMediaPath) && string.IsNullOrEmpty(mediaPath))
            {
                return;
            }
            if (string.Equals(_currentLoadedMediaPath, mediaPath, StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(mediaPath))
            {
                // Media is already loaded and active. Do not interrupt or reload.
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

        private void ApplyMediaDisplayMode(DeckSlide slide, bool hasMedia)
        {
            if (!hasMedia)
            {
                AudienceQuestionContentScroll.Visibility = Visibility.Visible;
                AudienceMediaPanel.Visibility = Visibility.Collapsed;
                Grid.SetColumn(AudienceMediaPanel, 1);
                Grid.SetColumnSpan(AudienceMediaPanel, 1);
                AudienceMediaPanel.Width = 600;
                return;
            }

            // Mode 1: Fullscreen Clue
            if (slide.MediaDisplayMode == 1)
            {
                AudienceQuestionContentScroll.Visibility = Visibility.Collapsed;
                Grid.SetColumn(AudienceMediaPanel, 0);
                Grid.SetColumnSpan(AudienceMediaPanel, 2);
                AudienceMediaPanel.Width = double.NaN;
                AudienceMediaPanel.HorizontalAlignment = HorizontalAlignment.Stretch;
                AudienceMediaPanel.VerticalAlignment = VerticalAlignment.Stretch;
                AudienceMediaPanel.Visibility = Visibility.Visible;
            }
            else
            {
                AudienceQuestionContentScroll.Visibility = Visibility.Visible;
                Grid.SetColumn(AudienceMediaPanel, 1);
                Grid.SetColumnSpan(AudienceMediaPanel, 1);
                AudienceMediaPanel.Width = 600;
                AudienceMediaPanel.HorizontalAlignment = HorizontalAlignment.Right;
                AudienceMediaPanel.Visibility = Visibility.Visible;
            }
        }

        private void ResetToStandby()
        {
            _currentSessionId = 0;
            _currentDeckTitle = null;
            _renderedSlideId = -1;
            _renderedAnswerRevealed = false;
            _renderedActiveTeam = null;
            _renderedClueStep = -1;
            _renderedScoreboardSignature = null;
            _lastPolledDeckId = -1;
            _cachedSlides = null;
            _lastConsoleTimerUpdate = DateTime.MinValue;
            AudienceDeckTitle.Text = "QUIZ SCORE LIVE";
            AudienceRoundName.Text = "WAITING";
            AudiencePointsLabel.Text = "STANDBY";
            AudienceSlideType.Text = "WELCOME";
            AudienceSlideTitle.Text = "Quiz will begin shortly";
            AudienceSlideContent.Text = "Please take your seats. The host will start the session soon.";
            AudienceOptions.ItemsSource = null;
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
            HideStageRulesMatrix();
            HideInterRoundLeaderboard();
        }

        public void ShowOnAudienceScreen(Window host)
        {
            var hostScreen = System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(host).Handle);
            var audienceScreen = System.Windows.Forms.Screen.AllScreens.FirstOrDefault(screen => screen.DeviceName != hostScreen.DeviceName);
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
