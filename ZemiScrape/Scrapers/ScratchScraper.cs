using System.IO;

namespace ZemiScrape.Scrapers
{
    public abstract class ScratchScraper
    {
        public string WorkingDirectoryPath;
        public abstract void Scrape();

        private void makeIfNotExists(string path)
        {
            bool existsWhole = Directory.Exists(path);

            if (!existsWhole) Directory.CreateDirectory(path);
        }

        public ScratchScraper(string workingDirectoryPath)
        {
            this.WorkingDirectoryPath = workingDirectoryPath;
            //check to see if the path exists and whether the subfolders /files and /properties do

            makeIfNotExists(workingDirectoryPath);
        }



    }
}
