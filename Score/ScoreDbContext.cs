using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Score
{
    class ScoreDbContext : DbContext
    {
        public ScoreDbContext() : base("cns")
        {
           
        }
        public virtual DbSet<ScoreCount> ScoreCounts{get; set;}
    }
}
