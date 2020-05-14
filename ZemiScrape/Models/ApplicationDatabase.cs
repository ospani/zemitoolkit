using System.Data.Entity;
using MySql.Data.EntityFramework;

namespace ZemiScrape.Models
{
    [DbConfigurationType(typeof(MySqlEFConfiguration))]
    public class ApplicationDatabase : DbContext
    {
        // Your context has been configured to use a 'ApplicationDatabase' connection string from your application's 
        // configuration file (App.config or Web.config). By default, this connection string targets the 
        // 'Scraper.ApplicationDatabase' database on your LocalDb instance. 
        // 
        // If you wish to target a different database and/or database provider, modify the 'ApplicationDatabase' 
        // connection string in the application configuration file.
        public ApplicationDatabase()
            : base("name=ApplicationDatabase")
        {
        }

        // Add a DbSet for each entity type that you want to include in your model. For more information 
        // on configuring and using a Code First model, see http://go.microsoft.com/fwlink/?LinkId=390109.

        public virtual DbSet<Author> Authors { get; set; }
        public virtual DbSet<OpCode> OpCodes { get; set; }
        public virtual DbSet<SpriteType> SpriteTypes { get; set; }
        public virtual DbSet<Script> Scripts { get; set; }
        public virtual DbSet<Project> Projects { get; set; }
        public virtual DbSet<Procedure> Procedures { get; set; }
        public virtual DbSet<Block> Blocks { get; set; }
    }

    //public class MyEntity
    //{
    //    public int Id { get; set; }
    //    public string Name { get; set; }
    //}
}