namespace Score.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class add_quiz_deck_module : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.QuizDecks",
                c => new
                    {
                        QuizDeckId = c.Int(nullable: false, identity: true),
                        Title = c.String(),
                        Description = c.String(),
                        Theme = c.String(),
                        CreatedAtUtc = c.DateTime(),
                        UpdatedAtUtc = c.DateTime(),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.QuizDeckId);
            
            CreateTable(
                "dbo.DeckSlides",
                c => new
                    {
                        DeckSlideId = c.Int(nullable: false, identity: true),
                        QuizDeckId = c.Int(nullable: false),
                        SlideType = c.String(),
                        QuestionType = c.String(),
                        RoundType = c.String(),
                        SlideOrder = c.Int(nullable: false),
                        Title = c.String(),
                        ContentText = c.String(),
                        MediaPath = c.String(),
                        MediaType = c.String(),
                        CorrectAnswer = c.String(),
                        OptionsJson = c.String(),
                        Points = c.Int(nullable: false),
                        PenaltyPoints = c.Int(nullable: false),
                        TimeLimitSeconds = c.Int(nullable: false),
                        IsActive = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.DeckSlideId)
                .ForeignKey("dbo.QuizDecks", t => t.QuizDeckId, cascadeDelete: true)
                .Index(t => t.QuizDeckId);
            
            CreateTable(
                "dbo.QuizDeckSessions",
                c => new
                    {
                        QuizDeckSessionId = c.Int(nullable: false, identity: true),
                        QuizDeckId = c.Int(nullable: false),
                        StartedAtUtc = c.DateTime(),
                        CompletedAtUtc = c.DateTime(),
                        Status = c.String(),
                        CurrentSlideIndex = c.Int(nullable: false),
                        QuizMasterNotes = c.String(),
                        TimerStartedAtUtc = c.DateTime(),
                        TimerElapsedSeconds = c.Int(nullable: false),
                        TimerState = c.String(),
                        AnswerRevealed = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.QuizDeckSessionId)
                .ForeignKey("dbo.QuizDecks", t => t.QuizDeckId, cascadeDelete: true)
                .Index(t => t.QuizDeckId);
            
            CreateTable(
                "dbo.QuizResponses",
                c => new
                    {
                        QuizResponseId = c.Int(nullable: false, identity: true),
                        QuizDeckSessionId = c.Int(nullable: false),
                        DeckSlideId = c.Int(nullable: false),
                        TeamName = c.String(),
                        AnswerText = c.String(),
                        AnsweredAtUtc = c.DateTime(),
                        ResponseTimeSeconds = c.Int(nullable: false),
                        IsCorrect = c.Boolean(nullable: false),
                        PointsAwarded = c.Int(nullable: false),
                        BuzzOrder = c.Int(nullable: false),
                        SlideTitleSnapshot = c.String(),
                        SlideTypeSnapshot = c.String(),
                        SlideOrderSnapshot = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.QuizResponseId)
                .ForeignKey("dbo.QuizDeckSessions", t => t.QuizDeckSessionId, cascadeDelete: true)
                .ForeignKey("dbo.DeckSlides", t => t.DeckSlideId, cascadeDelete: true)
                .Index(t => t.QuizDeckSessionId)
                .Index(t => t.DeckSlideId);
            
            CreateTable(
                "dbo.QuizAuditEntries",
                c => new
                    {
                        QuizAuditEntryId = c.Int(nullable: false, identity: true),
                        QuizDeckSessionId = c.Int(nullable: false),
                        OccurredAtUtc = c.DateTime(nullable: false),
                        EventType = c.String(),
                        TeamName = c.String(),
                        SlideNumber = c.Int(nullable: false),
                        OldPoints = c.Int(nullable: false),
                        NewPoints = c.Int(nullable: false),
                        Reason = c.String(),
                    })
                .PrimaryKey(t => t.QuizAuditEntryId)
                .ForeignKey("dbo.QuizDeckSessions", t => t.QuizDeckSessionId, cascadeDelete: true)
                .Index(t => t.QuizDeckSessionId);

            CreateTable(
                "dbo.QuizDeckSessionTeams",
                c => new
                    {
                        QuizDeckSessionTeamId = c.Int(nullable: false, identity: true),
                        QuizDeckSessionId = c.Int(nullable: false),
                        TeamName = c.String(nullable: false),
                    })
                .PrimaryKey(t => t.QuizDeckSessionTeamId)
                .ForeignKey("dbo.QuizDeckSessions", t => t.QuizDeckSessionId, cascadeDelete: true)
                .Index(t => t.QuizDeckSessionId);
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.QuizAuditEntries", "QuizDeckSessionId", "dbo.QuizDeckSessions");
            DropForeignKey("dbo.QuizDeckSessionTeams", "QuizDeckSessionId", "dbo.QuizDeckSessions");
            DropForeignKey("dbo.QuizResponses", "DeckSlideId", "dbo.DeckSlides");
            DropForeignKey("dbo.QuizResponses", "QuizDeckSessionId", "dbo.QuizDeckSessions");
            DropForeignKey("dbo.QuizDeckSessions", "QuizDeckId", "dbo.QuizDecks");
            DropForeignKey("dbo.DeckSlides", "QuizDeckId", "dbo.QuizDecks");
            DropIndex("dbo.QuizAuditEntries", new[] { "QuizDeckSessionId" });
            DropIndex("dbo.QuizDeckSessionTeams", new[] { "QuizDeckSessionId" });
            DropIndex("dbo.QuizResponses", new[] { "DeckSlideId" });
            DropIndex("dbo.QuizResponses", new[] { "QuizDeckSessionId" });
            DropIndex("dbo.QuizDeckSessions", new[] { "QuizDeckId" });
            DropIndex("dbo.DeckSlides", new[] { "QuizDeckId" });
            DropTable("dbo.QuizAuditEntries");
            DropTable("dbo.QuizDeckSessionTeams");
            DropTable("dbo.QuizResponses");
            DropTable("dbo.QuizDeckSessions");
            DropTable("dbo.DeckSlides");
            DropTable("dbo.QuizDecks");
        }
    }
}