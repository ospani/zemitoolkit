using System;
using System.IO;
using ZemiScrape.Scrapers;

namespace Zemi
{
    class MainApp
   {
      static void Main(string[] args)
      {
#if DEBUG
            Console.WriteLine("***\tZemi is running in Debug mode. This could impact application performance.");
            Console.WriteLine("***\tTo ensure optimal performance, run a Release build instead.");
#endif
            int parserThreads = 1;
            if(args.Length < 3)
            {
                Console.WriteLine("\nIncorrect number of arguments supplied." +
                    "\nYou must specify the location of the Zemi folder and the number of threads." +
                    "\nFor example: Zemi.exe \"C:\\Zemi\" -threads 8");
                return;
            }
            string zemiPath = args[0].Trim('\"','\'');
            string projectScraperPath = Path.Combine(zemiPath, "ProjectScraper/");
            string projectsPath = Path.Combine(projectScraperPath, "projects/");
            if(args[1].StartsWith("-threads"))
            {
                    if (!Int32.TryParse(args[2], out parserThreads)) 
                        Console.WriteLine("Unknown number of threads specified. Using default: 1");
            }
            else if (args[1].StartsWith("validate"))
            {
                Console.WriteLine("Validating unregistered projects...");
                JSONReader.ProcessUnregisteredProjects(projectsPath, new ProjectScraper(@projectsPath));
                return;
            }

            Console.WriteLine("\nDepending on how many threads you chose, the RAM, CPU and Disk IO of this machine will be heavily taxed." +
                "\nThis program is not responsible for potential damage or excessive wear to your system's components. Use with care.\nPress any key to continue.");
            Console.Read();

            if(Directory.Exists(projectsPath))
            {
                JSONReader.ProcessJSON2(projectsPath, parserThreads);
            }
            else
            {
                Console.WriteLine($"Directory \"{projectsPath}\" did not exist. Are you sure you ran the scraper at least once?");
                return;
            }

            while (true) { System.Threading.Thread.Sleep(1000); Console.ReadKey(); }
        }
   }
}
