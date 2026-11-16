namespace Score.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class enhance_quiz_deck_features : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.DeckSlides", "HintsJson", c => c.String());
            AddColumn("dbo.DeckSlides", "AlternateAnswers", c => c.String());
            AddColumn("dbo.DeckSlides", "Explanation", c => c.String());
            AddColumn("dbo.DeckSlides", "BonusPoints", c => c.Int(nullable: false, defaultValue: 0));
            AddColumn("dbo.DeckSlides", "AllowPass", c => c.Boolean(nullable: false, defaultValue: true));
            AddColumn("dbo.DeckSlides", "RoundName", c => c.String());
            AddColumn("dbo.DeckSlides", "MediaDisplayMode", c => c.Int(nullable: false, defaultValue: 0));

            AddColumn("dbo.QuizDeckSessions", "ActiveTeamOnFloor", c => c.String());
            AddColumn("dbo.QuizDeckSessions", "PassCount", c => c.Int(nullable: false, defaultValue: 0));
            AddColumn("dbo.QuizDeckSessions", "BuzzQueueJson", c => c.String());
            AddColumn("dbo.QuizDeckSessions", "ClueRevealStep", c => c.Int(nullable: false, defaultValue: 0));

            AddColumn("dbo.QuizResponses", "IsPassAttempt", c => c.Boolean(nullable: false, defaultValue: false));
            AddColumn("dbo.QuizResponses", "AdjudicationNotes", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.QuizResponses", "AdjudicationNotes");
            DropColumn("dbo.QuizResponses", "IsPassAttempt");
            DropColumn("dbo.QuizDeckSessions", "ClueRevealStep");
            DropColumn("dbo.QuizDeckSessions", "BuzzQueueJson");
            DropColumn("dbo.QuizDeckSessions", "PassCount");
            DropColumn("dbo.QuizDeckSessions", "ActiveTeamOnFloor");
            DropColumn("dbo.DeckSlides", "MediaDisplayMode");
            DropColumn("dbo.DeckSlides", "RoundName");
            DropColumn("dbo.DeckSlides", "AllowPass");
            DropColumn("dbo.DeckSlides", "BonusPoints");
            DropColumn("dbo.DeckSlides", "Explanation");
            DropColumn("dbo.DeckSlides", "AlternateAnswers");
            DropColumn("dbo.DeckSlides", "HintsJson");
        }
    }
}
