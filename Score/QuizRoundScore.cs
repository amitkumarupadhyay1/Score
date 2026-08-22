namespace Score
{
    class QuizRoundScore
    {
        public int QuizRoundScoreId { get; set; }
        public int QuizSessionId { get; set; }
        public string DatabaseTeamName { get; set; }
        public int RoundNumber { get; set; }
        public int ScoreValue { get; set; }
        public int TeamElapsedSeconds { get; set; }
        public int ElapsedSeconds { get; set; }
        public bool IsFinalized { get; set; }
    }
}
