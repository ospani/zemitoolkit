using System;
using System.IO;
using ZemiScrape.Scrapers;

namespace ZemiScrape
{
    class MainApp
    {
        static void Main(string[] args)
        {
            ScratchScraper s;
#if DEBUG
            Console.WriteLine("***\tZemi is running in Debug mode. This could impact application performance.");
            Console.WriteLine("***\tTo ensure optimal performance, run a Release build instead.");
#endif

            if (args.Length < 2)
            {
                Console.WriteLine("Incorrect number of arguments given." +
                    "\n\t-project\tScraper the projects for all Authors in the connected database." +
                    "\n\t-author\tScraper random authors from the front page." +
                    "\n\t-heuristic\tPasses over scraped authors and scraper followers and followings." +
                    "\n Followed by the path to the Zemi directory.");
                Console.ReadLine();
                return;
            }

            string type = args[0];
            string path = args[1].Trim('\"','\'');
            if(!Directory.Exists(path))
            {
                Console.WriteLine($"Directory\"{path}\" did not exist. Creating main directory...");
                try { Directory.CreateDirectory(path); }
                catch(Exception ex) { Console.WriteLine(ex.Message); }
            }
            if (type.StartsWith("-project"))
            {
                ProjectScraper projectScraper = new ProjectScraper(Path.Combine(path, "ProjectScraper/"));
                projectScraper.Scrape();

            }
            else if (type.StartsWith("-author"))
            {
                AuthorScraper authorScraper = new AuthorScraper(Path.Combine(path, "AuthorScraper/"));
                authorScraper.Scrape();
            }
            else if (type.StartsWith("-validate"))
            {
                Console.WriteLine("Validating all Authors in database. This can take a very long time.");
                AuthorScraper authorScraper = new AuthorScraper(Path.Combine(path, "AuthorScraper/"));
                authorScraper.ValidateAllAuthorsInDatabase();
            }
            else if (type.StartsWith("-heuristic"))
            {
                AuthorScraper authorScraper = new AuthorScraper(Path.Combine(path, "AuthorScraper/"));
                authorScraper.HeuristicScrape(0);
            }
            while (true) { Console.ReadKey(); }
        }
    }
}
