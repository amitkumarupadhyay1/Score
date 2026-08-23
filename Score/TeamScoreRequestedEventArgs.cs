using System;

namespace Score
{
    public sealed class TeamScoreRequestedEventArgs : EventArgs
    {
        public TeamScoreRequestedEventArgs(int points)
        {
            Points = points;
        }

        public int Points { get; private set; }
    }
}
