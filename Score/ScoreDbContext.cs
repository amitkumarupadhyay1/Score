using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Score
{
    class ScoreDbContext : DbContext
    {
        public ScoreDbContext() : base("name=cns")
        {
        }

        public virtual DbSet<ScoreCount> ScoreCounts{get; set;}
        public virtual DbSet<QuizSession> QuizSessions { get; set; }
        public virtual DbSet<QuizRoundScore> QuizRoundScores { get; set; }
        public virtual DbSet<QuizEventLog> QuizEventLogs { get; set; }
    }
}
