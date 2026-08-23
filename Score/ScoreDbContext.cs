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
        static ScoreDbContext()
        {
            System.Data.Entity.Database.SetInitializer<ScoreDbContext>(null);
        }

        public ScoreDbContext() : base("name=cns")
        {
            Database.CommandTimeout = 5;
        }

        public virtual DbSet<ScoreCount> ScoreCounts{get; set;}
        public virtual DbSet<QuizSession> QuizSessions { get; set; }
        public virtual DbSet<QuizRoundScore> QuizRoundScores { get; set; }
        public virtual DbSet<QuizEventLog> QuizEventLogs { get; set; }
    }
}
