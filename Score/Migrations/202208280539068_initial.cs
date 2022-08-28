namespace Score.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class initial : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.ScoreCounts",
                c => new
                    {
                        ScoreCountID = c.Int(nullable: false, identity: true),
                        Team = c.String(),
                        ScoreValue = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.ScoreCountID);
            
        }
        
        public override void Down()
        {
            DropTable("dbo.ScoreCounts");
        }
    }
}
