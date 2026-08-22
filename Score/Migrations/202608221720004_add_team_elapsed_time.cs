namespace Score.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class add_team_elapsed_time : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.QuizRoundScores", "TeamElapsedSeconds", c => c.Int(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.QuizRoundScores", "TeamElapsedSeconds");
        }
    }
}
