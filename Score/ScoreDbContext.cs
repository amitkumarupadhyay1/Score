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
        public ScoreDbContext() : base(GetConnectionString())
        {
           
        }

        private static string GetConnectionString()
        {
            var cs = ConfigurationManager.ConnectionStrings["cns"];
            if (cs != null && !string.IsNullOrEmpty(cs.ConnectionString))
            {
                return cs.ConnectionString;
            }
            return @"Data Source=(localdb)\MSSQLLocalDB;Initial Catalog=ScoreDB;Integrated Security=True;Connect Timeout=30;Encrypt=False;";
        }

        public virtual DbSet<ScoreCount> ScoreCounts{get; set;}
    }
}
