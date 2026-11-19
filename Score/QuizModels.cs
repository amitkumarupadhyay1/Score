using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Score
{
    [Table("QuizRounds")]
    public class QuizRound
    {
        [Key]
        public int QuizRoundId { get; set; }
        public int QuizDeckId { get; set; }
        public int RoundOrder { get; set; } = 1;
        public string RoundTitle { get; set; }
        public string RoundType { get; set; } = "Standard";
        public int DefaultPoints { get; set; } = 10;
        public int DefaultBouncePoints { get; set; } = 5;
        public int DefaultPenalty { get; set; } = 0;
        public int DefaultTimeSeconds { get; set; } = 15;
        public bool ConferringAllowed { get; set; } = true;
        public bool InfiniteBounce { get; set; } = true;
        public string RulesCardJson { get; set; }
    }

    public class TeamScoreSnapshot
    {
        public string TeamName { get; set; }
        public int TotalScore { get; set; }
        public int Rank { get; set; }
        public int RankChange { get; set; }
    }
}
