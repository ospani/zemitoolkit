using System.Data.Entity.Migrations;
using ZemiScrape.Models;

namespace ZemiScrape.Migrations
{
    internal sealed class Configuration : DbMigrationsConfiguration<ApplicationDatabase>
    {
        public Configuration()
        {
            AutomaticMigrationsEnabled = false;
        }

        protected override void Seed(ApplicationDatabase context)
        {
            //  This method will be called after migrating to the latest version.

            //  You can use the DbSet<T>.AddOrUpdate() helper extension method 
            //  to avoid creating duplicate seed data.
        }
    }
}
