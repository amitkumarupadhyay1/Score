namespace Score.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class add_quiz_timing_generated : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.QuizRoundScores",
                c => new
                    {
                        QuizRoundScoreId = c.Int(nullable: false, identity: true),
                        QuizSessionId = c.Int(nullable: false),
                        DatabaseTeamName = c.String(),
                        RoundNumber = c.Int(nullable: false),
                        ScoreValue = c.Int(nullable: false),
                        ElapsedSeconds = c.Int(nullable: false),
                        IsFinalized = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.QuizRoundScoreId);
            
            CreateTable(
                "dbo.QuizSessions",
                c => new
                    {
                        QuizSessionId = c.Int(nullable: false, identity: true),
                        Title = c.String(),
                        ConfiguredRounds = c.Int(nullable: false),
                        CurrentRound = c.Int(nullable: false),
                        Status = c.String(),
                        GlobalDurationSeconds = c.Int(nullable: false),
                        RoundDurationSeconds = c.Int(nullable: false),
                        StartedAtUtc = c.DateTime(),
                        PausedAtUtc = c.DateTime(),
                        CompletedAtUtc = c.DateTime(),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.QuizSessionId);
            
        }
        
        public override void Down()
        {
            DropTable("dbo.QuizSessions");
            DropTable("dbo.QuizRoundScores");
        }
    }
}
