using System;
using System.IO;
using System.Media;
using System.Threading;
using System.Windows.Media;
using System.Windows.Threading;

namespace Score
{
    internal static class QuizChimes
    {
        public static bool IsMuted { get; set; }

        private static readonly object _threadLock = new object();
        private static Dispatcher _audioDispatcher;
        private static MediaPlayer _scoreMediaPlayer;
        private static MediaPlayer _buzzerMediaPlayer;

        private static SoundPlayer pointsAwarded;
        private static SoundPlayer roundFinished;
        private static SoundPlayer buzzerSound;
        private static SoundPlayer incorrectSound;
        private static SoundPlayer tickWarningSound;
        private static SoundPlayer bounceSound;
        private static SoundPlayer correctSynthSound;

        private static string _tempAudioDir;
        private static string _buzzerWavPath;

        static QuizChimes()
        {
            try
            {
                _tempAudioDir = Path.Combine(Path.GetTempPath(), "QuizScore_Audio");
                if (!Directory.Exists(_tempAudioDir))
                {
                    Directory.CreateDirectory(_tempAudioDir);
                }
            }
            catch { }
        }

        private static void EnsureAudioWorker()
        {
            if (_audioDispatcher != null) return;
            lock (_threadLock)
            {
                if (_audioDispatcher != null) return;
                var readyEvent = new ManualResetEventSlim(false);
                var thread = new Thread(() =>
                {
                    try
                    {
                        _scoreMediaPlayer = new MediaPlayer();
                        _buzzerMediaPlayer = new MediaPlayer();
                    }
                    catch { }
                    _audioDispatcher = Dispatcher.CurrentDispatcher;
                    readyEvent.Set();
                    Dispatcher.Run();
                })
                {
                    IsBackground = true,
                    Name = "QuizChimesAudioWorker"
                };
                thread.SetApartmentState(ApartmentState.STA);
                thread.Start();
                readyEvent.Wait(1500);
            }
        }

        public static void Stop()
        {
            try
            {
                if (_audioDispatcher != null)
                {
                    _audioDispatcher.BeginInvoke((Action)(() =>
                    {
                        try
                        {
                            _scoreMediaPlayer?.Stop();
                            _buzzerMediaPlayer?.Stop();
                        }
                        catch { }
                    }));
                }

                pointsAwarded?.Stop();
                roundFinished?.Stop();
                buzzerSound?.Stop();
                incorrectSound?.Stop();
                tickWarningSound?.Stop();
                bounceSound?.Stop();
                correctSynthSound?.Stop();
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Could not stop active sound playback.", ex);
            }
        }

        public static void PlayPointsAwarded()
        {
            if (IsMuted) return;
            PlayScore("PointsAwarded.wav");
        }

        public static void PlayRoundFinished()
        {
            if (IsMuted) return;
            PlayScore("RoundFinished.wav");
        }

        public static void PlayCorrect()
        {
            if (IsMuted) return;
            try
            {
                if (typeof(QuizChimes).Assembly.GetManifestResourceStream("Score.Sounds.PointsAwarded.wav") != null)
                {
                    PlayPointsAwarded();
                    return;
                }
            }
            catch { }

            PlaySynth(ref correctSynthSound, buf =>
            {
                const int sr = 22050;
                for (int i = 0; i < buf.Length; i++)
                {
                    double t = (double)i / sr;
                    double freq = t < 0.10 ? 1046.5 : (t < 0.20 ? 1318.5 : 1568.0);
                    double envelope = Math.Max(0, 1.0 - (t / 0.45));
                    buf[i] = (short)(Math.Sin(2 * Math.PI * freq * t) * envelope * 22000);
                }
            }, 0.45);
        }

        public static void PlayIncorrect()
        {
            if (IsMuted) return;
            PlaySynth(ref incorrectSound, buf =>
            {
                const int sr = 22050;
                for (int i = 0; i < buf.Length; i++)
                {
                    double t = (double)i / sr;
                    double envelope = Math.Max(0, 1.0 - (t / 0.40));
                    double w1 = Math.Sin(2 * Math.PI * 130.0 * t) > 0 ? 0.5 : -0.5;
                    double w2 = Math.Sin(2 * Math.PI * 138.0 * t) > 0 ? 0.5 : -0.5;
                    buf[i] = (short)((w1 + w2) * envelope * 22000);
                }
            }, 0.40);
        }

        public static void PlayBuzzer()
        {
            if (IsMuted) return;

            // Prevent clipping: buzz channel runs independently from score channel
            try
            {
                EnsureAudioWorker();
                if (_buzzerWavPath == null)
                {
                    _buzzerWavPath = EnsureSynthesizedWav("buzzer.wav", buf =>
                    {
                        const int sr = 22050;
                        for (int i = 0; i < buf.Length; i++)
                        {
                            double t = (double)i / sr;
                            double envelope = Math.Max(0, 1.0 - (t / 0.35));
                            double w = Math.Sin(2 * Math.PI * 220.0 * t) > 0 ? 0.8 : -0.8;
                            buf[i] = (short)(w * envelope * 24000);
                        }
                    }, 0.35);
                }

                if (_buzzerWavPath != null && _audioDispatcher != null)
                {
                    _audioDispatcher.BeginInvoke((Action)(() =>
                    {
                        try
                        {
                            if (_buzzerMediaPlayer == null) _buzzerMediaPlayer = new MediaPlayer();
                            _buzzerMediaPlayer.Stop();
                            _buzzerMediaPlayer.Open(new Uri(_buzzerWavPath));
                            _buzzerMediaPlayer.Play();
                        }
                        catch
                        {
                            PlayBuzzerFallback();
                        }
                    }));
                    return;
                }
            }
            catch { }

            PlayBuzzerFallback();
        }

        private static void PlayBuzzerFallback()
        {
            PlaySynth(ref buzzerSound, buf =>
            {
                const int sr = 22050;
                for (int i = 0; i < buf.Length; i++)
                {
                    double t = (double)i / sr;
                    double envelope = Math.Max(0, 1.0 - (t / 0.35));
                    double w = Math.Sin(2 * Math.PI * 220.0 * t) > 0 ? 0.8 : -0.8;
                    buf[i] = (short)(w * envelope * 24000);
                }
            }, 0.35);
        }

        public static void PlayTickWarning()
        {
            if (IsMuted) return;
            PlaySynth(ref tickWarningSound, buf =>
            {
                const int sr = 22050;
                for (int i = 0; i < buf.Length; i++)
                {
                    double t = (double)i / sr;
                    double envelope = Math.Max(0, 1.0 - (t / 0.07));
                    buf[i] = (short)(Math.Sin(2 * Math.PI * 880.0 * t) * envelope * 18000);
                }
            }, 0.07);
        }

        public static void PlayBounce()
        {
            if (IsMuted) return;
            PlaySynth(ref bounceSound, buf =>
            {
                const int sr = 22050;
                for (int i = 0; i < buf.Length; i++)
                {
                    double t = (double)i / sr;
                    double freq = t < 0.12 ? 440.0 : 330.0;
                    double envelope = Math.Max(0, 1.0 - (t / 0.25));
                    buf[i] = (short)(Math.Sin(2 * Math.PI * freq * t) * envelope * 20000);
                }
            }, 0.25);
        }

        private static void PlayScore(string fileName)
        {
            try
            {
                EnsureAudioWorker();
                string path = EnsureExtractedSound(fileName);
                if (path != null && _audioDispatcher != null)
                {
                    _audioDispatcher.BeginInvoke((Action)(() =>
                    {
                        try
                        {
                            if (_scoreMediaPlayer == null) _scoreMediaPlayer = new MediaPlayer();
                            _scoreMediaPlayer.Stop();
                            _scoreMediaPlayer.Open(new Uri(path));
                            _scoreMediaPlayer.Play();
                        }
                        catch
                        {
                            PlayFallback(fileName);
                        }
                    }));
                    return;
                }
            }
            catch { }

            PlayFallback(fileName);
        }

        private static void PlayFallback(string fileName)
        {
            if (fileName == "PointsAwarded.wav") Play(ref pointsAwarded, fileName);
            else if (fileName == "RoundFinished.wav") Play(ref roundFinished, fileName);
        }

        private static string EnsureExtractedSound(string fileName)
        {
            try
            {
                if (string.IsNullOrEmpty(_tempAudioDir))
                    _tempAudioDir = Path.Combine(Path.GetTempPath(), "QuizScore_Audio");
                if (!Directory.Exists(_tempAudioDir)) Directory.CreateDirectory(_tempAudioDir);

                string fullPath = Path.Combine(_tempAudioDir, fileName);
                if (!File.Exists(fullPath) || new FileInfo(fullPath).Length == 0)
                {
                    using (var stream = typeof(QuizChimes).Assembly.GetManifestResourceStream("Score.Sounds." + fileName))
                    {
                        if (stream != null)
                        {
                            using (var fs = File.Create(fullPath))
                            {
                                stream.CopyTo(fs);
                            }
                        }
                    }
                }
                if (File.Exists(fullPath)) return fullPath;
            }
            catch { }
            return null;
        }

        private static string EnsureSynthesizedWav(string fileName, Action<short[]> generator, double durationSeconds)
        {
            try
            {
                if (string.IsNullOrEmpty(_tempAudioDir))
                    _tempAudioDir = Path.Combine(Path.GetTempPath(), "QuizScore_Audio");
                if (!Directory.Exists(_tempAudioDir)) Directory.CreateDirectory(_tempAudioDir);

                string fullPath = Path.Combine(_tempAudioDir, fileName);
                if (File.Exists(fullPath) && new FileInfo(fullPath).Length > 0)
                    return fullPath;

                const int sampleRate = 22050;
                int totalSamples = (int)(durationSeconds * sampleRate);
                short[] buffer = new short[totalSamples];
                generator(buffer);

                using (var fs = File.Create(fullPath))
                using (var bw = new BinaryWriter(fs, System.Text.Encoding.ASCII))
                {
                    bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                    bw.Write(36 + totalSamples * 2);
                    bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                    bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                    bw.Write(16);
                    bw.Write((short)1); // PCM
                    bw.Write((short)1); // Mono
                    bw.Write(sampleRate);
                    bw.Write(sampleRate * 2);
                    bw.Write((short)2);
                    bw.Write((short)16);
                    bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                    bw.Write(totalSamples * 2);
                    for (int i = 0; i < totalSamples; i++)
                    {
                        bw.Write(buffer[i]);
                    }
                }
                return fullPath;
            }
            catch { }
            return null;
        }

        private static void PlaySynth(ref SoundPlayer player, Action<short[]> generator, double durationSeconds)
        {
            try
            {
                if (player == null)
                {
                    const int sampleRate = 22050;
                    int totalSamples = (int)(durationSeconds * sampleRate);
                    short[] buffer = new short[totalSamples];
                    generator(buffer);

                    var ms = new MemoryStream();
                    using (var bw = new BinaryWriter(ms, System.Text.Encoding.ASCII, true))
                    {
                        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                        bw.Write(36 + totalSamples * 2);
                        bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
                        bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                        bw.Write(16);
                        bw.Write((short)1); // PCM
                        bw.Write((short)1); // Mono
                        bw.Write(sampleRate);
                        bw.Write(sampleRate * 2);
                        bw.Write((short)2);
                        bw.Write((short)16);
                        bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                        bw.Write(totalSamples * 2);
                        for (int i = 0; i < totalSamples; i++)
                        {
                            bw.Write(buffer[i]);
                        }
                    }
                    ms.Position = 0;
                    player = new SoundPlayer(ms);
                    try { player.Load(); } catch { }
                }
                player.Play();
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Could not play synthesized chime.", ex);
            }
        }

        private static void Play(ref SoundPlayer player, string fileName)
        {
            try
            {
                if (player == null)
                {
                    var stream = typeof(QuizChimes).Assembly.GetManifestResourceStream("Score.Sounds." + fileName);
                    if (stream == null) throw new FileNotFoundException("The chime is missing from the app.", fileName);
                    player = new SoundPlayer(stream);
                }
                player.Play();
            }
            catch (Exception ex)
            {
                AppDiagnostics.WriteException("Could not play chime " + fileName + ".", ex);
            }
        }
    }
}