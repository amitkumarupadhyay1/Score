using System;
using System.IO;
using System.Media;

namespace Score
{
    internal static class QuizChimes
    {
        private static SoundPlayer pointsAwarded;
        private static SoundPlayer roundFinished;

        public static void Stop()
        {
            var player = pointsAwarded ?? roundFinished;
            if (player != null) player.Stop();
        }

        public static void PlayPointsAwarded()
        {
            Play(ref pointsAwarded, "PointsAwarded.wav");
        }

        public static void PlayRoundFinished()
        {
            Play(ref roundFinished, "RoundFinished.wav");
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
