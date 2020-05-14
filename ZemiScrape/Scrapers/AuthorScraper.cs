using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Json;
using System.Linq;
using System.Threading;
using ZemiScrape.Models;

namespace ZemiScrape.Scrapers
{
    class AuthorScraper : ScratchScraper
    {
        public AuthorScraper(string workingDirectoryPath) : base(workingDirectoryPath)
        {
        }

        public void Scrape(int skip = 0)
        {
            string[] allURLSToConsider = new string[]
            {
                "https://api.scratch.mit.edu/explore/projects?limit=40&offset={0}&mode=trending&q=*",
                "https://api.scratch.mit.edu/explore/projects?limit=40&offset={0}&mode=popular&q=*",
                "https://api.scratch.mit.edu/explore/projects?limit=40&offset={0}&mode=recent&q=*",
                "https://api.scratch.mit.edu/search/projects?limit=40q=*&offset={0}",
            };

            foreach (string URL in allURLSToConsider)
            {
                int offset = skip;
                string baseURL = URL;
                bool stopScraping = false;
                try
                {
                    while (stopScraping != true)
                    {
                        Console.WriteLine("Scraping at offset: " + offset.ToString());

                        string specificURL = string.Format(baseURL, offset.ToString());
                        string rawJson = JSONGetter.GetAsJSONString(specificURL);
                        if (string.IsNullOrEmpty(rawJson)) { Console.WriteLine("\t\tGetJSON2 returned null."); continue; }

                        dynamic projectsObject = JsonValue.Parse(rawJson);
                        List<ProjectAuthor> scrapedAuthors = new List<ProjectAuthor>();
                        foreach (var projectData in projectsObject)
                        {
                            string authorJson = GetAuthorJson(projectData["author"]["username"].ReadAs<string>());
                            ProjectAuthor projectAuthor = Newtonsoft.Json.JsonConvert.DeserializeObject<ProjectAuthor>(authorJson);
                            scrapedAuthors.Add(projectAuthor);
                            WriteAuthorToFile(projectAuthor.username, authorJson);
                        }

                        SaveAuthorsToDatabase(ProjectAuthorsToDatabaseEntities(scrapedAuthors.GroupBy(x => x.id).Select(y => y.First()).ToList()));

                        offset += 40;
                        if (offset > 9980) stopScraping = true;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception ocurred: {ex.Message}");
                    offset += 40;
                    continue;
                }
            }
        }
        public override void Scrape()
        {
            Scrape(0);
        }

        public void WriteAuthorToFile(string authorName, string json, bool overwrite = false)
        {
            try
            {
                string authorDirectory = Path.Combine(this.WorkingDirectoryPath, "authors");
                if (!Directory.Exists(authorDirectory)) Directory.CreateDirectory(authorDirectory);
                string authorFilePath = Path.Combine(authorDirectory, $"{authorName}.json");
                if (!overwrite && File.Exists(authorFilePath)) return;
                File.WriteAllText(authorFilePath, json);
            }
            catch(Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

        }

        public string GetAuthorJson(string userName)
        {
            string apiEndpoint = "https://api.scratch.mit.edu/users/" + userName;
            return JSONGetter.GetAsJSONString(apiEndpoint);
        }

        /// <summary>
        /// The heuristics scrape uses all the usernames in the database to retrieve their followers and the users they are following.
        /// These are added to the list of usernames. By repeating this function, the set of usernames grows accordingly with each pass.
        /// </summary>
        /// <param name="skip">The amount of usernames to skip (for concurrent scraping)</param>
        public void HeuristicScrape(int skip, int max = 0)
        {
            int handledRecords = skip;
            List<Author> scrapedUsers = GetAllAuthorsFromDatabase(skip).OrderBy(o => o.Id).ToList();

            foreach (Author user in scrapedUsers)
            {
                string userName = user.Username;
                Console.WriteLine($"► {userName}");

                JsonArray allConnectedUsers = new JsonArray();
                IEnumerable<JsonValue> allFollowers = GetAllFollowersOrFollowingsByUsername(userName, "followers");
                IEnumerable<JsonValue> allFollowing = GetAllFollowersOrFollowingsByUsername(userName, "following");
                user.AmountFollowers = allFollowers.Count();
                user.AmountFollowing = allFollowing.Count();

                allConnectedUsers.AddRange(allFollowers.Union(allFollowing));

                List<ProjectAuthor> connectedUsersToAuthors = new List<ProjectAuthor>();
                foreach (var connectedUser in allConnectedUsers)
                {
                    if (!connectedUsersToAuthors.Any(o => o.id == Int32.Parse(connectedUser["id"].ToString())))
                        connectedUsersToAuthors.Add(Newtonsoft.Json.JsonConvert.DeserializeObject<ProjectAuthor>(connectedUser.ToString()));
                }

                SaveAuthorsToDatabase(ProjectAuthorsToDatabaseEntities(connectedUsersToAuthors));
                UpdateAuthor(user);

                Console.Title = $"Zemi | AuthorScraper | index {handledRecords++}";
                if (max != 0 && handledRecords - skip > max)
                {
                    Console.WriteLine($"Scraper done with range {skip} to {skip + max}");
                }
            }
        }

        private List<Author> GetAllAuthorsFromDatabase(int skip = 0)
        {
            List<Author> allAuthorsInDatabase = null;
            using (ApplicationDatabase ctxt = new ApplicationDatabase())
            {
                allAuthorsInDatabase = ctxt.Authors.AsNoTracking().ToList();
                allAuthorsInDatabase = allAuthorsInDatabase.Skip(skip).ToList();
            }
            return allAuthorsInDatabase;

        }
        private void UpdateAuthor(Author toUpdate)
        {
            using (ApplicationDatabase _dbContext = new ApplicationDatabase())
            {
                var entity = _dbContext.Authors.Where(c => c.Id == toUpdate.Id).AsQueryable().FirstOrDefault();
                if (entity == null)
                {
                    _dbContext.Authors.Add(toUpdate);
                }
                else
                {
                    _dbContext.Entry(entity).CurrentValues.SetValues(toUpdate);
                }
                _dbContext.SaveChanges();
            }
        }
        List<Author> ProjectAuthorsToDatabaseEntities(List<ProjectAuthor> toConvert)
        {
            List<Author> converted = new List<Author>();
            foreach (ProjectAuthor authorToConvert in toConvert)
            {
                Author convertedAuthor = new Author()
                {
                    Id = authorToConvert.id,
                    DateJoined = authorToConvert.history.joined,
                    DateLastLogged = authorToConvert.history.login,
                    ScratchTeam = authorToConvert.scratchteam,
                    Username = authorToConvert.username,
                    Country = authorToConvert.profile.country
                };
                converted.Add(convertedAuthor);
            }
            return converted;
        }

        private void SaveAuthorsToDatabase(List<Author> toAddToDatabase)
        {
            using (ApplicationDatabase ctxt = new ApplicationDatabase())
            {
                foreach (Author authorToAdd in toAddToDatabase)
                {
                    if (!ctxt.Authors.Any(pa => pa.Id == authorToAdd.Id))
                    {
                        Console.Write("☻");
                        try
                        {
                            ctxt.Authors.Add(authorToAdd);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine(e.Message);
                        }
                    }
                    else Console.Write("☺");
                }
                Console.Write('\n');
                ctxt.SaveChanges();
            }
        }
        /// <summary>
        /// This method can be used for collecting data from API endpoints that return JSON arrays of User objects.
        /// These API endpoints are characterized by the /users/ route.
        /// Examples are users following a specific user, or the users that specific user is following.
        /// </summary>
        /// <param name="userName">The first parameter to the /users/ API endpoint.</param>
        /// <param name="route">The route modifier. Either "followers" or "following"</param>
        /// <returns></returns>
        private JsonArray GetAllFollowersOrFollowingsByUsername(string userName, string route)
        {
            string apiEndpoint = "https://api.scratch.mit.edu/users/" + userName + "/" + route + "?limit=40&offset={0}";
            bool endOfDataReached = false;
            int offset = 0;
            JsonArray allUsers = new JsonArray();

            while (!endOfDataReached)
            {
                string specifiedApiEndpoint = string.Format(apiEndpoint, offset);
                string returnedUsersJson = JSONGetter.GetAsJSONString(specifiedApiEndpoint);
                if (string.IsNullOrEmpty(returnedUsersJson)) break;
                var parsedUsers = JsonValue.Parse(returnedUsersJson);
                if (parsedUsers.Count == 0)
                {
                    endOfDataReached = true;
                }
                foreach (var follower in parsedUsers)
                {
                    allUsers.Add(follower.Value);
                }
                offset += 40;
            }
            return allUsers;
        }

        //This function can take a VERY long time depending on how many authors are in the database.
        private void ValidateAuthors(List<Author> authorsToCheck)
        {
            string authorDirectory = Path.Combine(this.WorkingDirectoryPath, "authors/");
            if (!Directory.Exists(authorDirectory)) Directory.CreateDirectory(authorDirectory);
            Console.WriteLine($"Starting at {authorsToCheck.First().Username} and ending at {authorsToCheck.Last().Username}");
            foreach (Author toCheck in authorsToCheck)
            {
                try
                {
                    ValidateAuthor(toCheck, authorDirectory);
                }
                catch (Exception e)
                {
                    try { File.AppendAllText(@"C:\ScratchScrapeData\AuthorScraper\errors\errors.txt", e.Message); }
                    catch (Exception) { }
                }
            }
            Console.WriteLine("Author validator done!");
        }
        private void ValidateAuthor(Author toCheck, string outputDirectory)
        {
            string authorFilePath = Path.Combine(outputDirectory, $"{toCheck.Username}.json");
            string authorJson = "";
            if (File.Exists(authorFilePath))
            {
                authorJson = File.ReadAllText(authorFilePath);
                if(string.IsNullOrEmpty(authorJson) || string.IsNullOrWhiteSpace(authorJson))
                {
                    authorJson = GetAuthorJson(toCheck.Username);
                    Console.WriteLine($"0-length file encountered for {toCheck.Username}");
                    WriteAuthorToFile(toCheck.Username, authorJson, true);
                }
            }
            else
            {
                authorJson = GetAuthorJson(toCheck.Username);
                WriteAuthorToFile(toCheck.Username, authorJson);
            }

            ProjectAuthor expectedAuthor = Newtonsoft.Json.JsonConvert.DeserializeObject<ProjectAuthor>(authorJson);
            if (toCheck.Username != expectedAuthor.username) toCheck.Username = expectedAuthor.username;
            if (toCheck.DateJoined != expectedAuthor.history.joined) toCheck.DateJoined = expectedAuthor.history.joined;
            if (toCheck.DateLastLogged != expectedAuthor.history.login) toCheck.DateLastLogged = expectedAuthor.history.login;
            if (toCheck.Country != expectedAuthor.profile.country) toCheck.Country = expectedAuthor.profile.country;
            UpdateAuthor(toCheck);
        }
        public void ValidateAllAuthorsInDatabase()
        {
            string authorDirectory = Path.Combine(this.WorkingDirectoryPath, "authors/");
            if (!Directory.Exists(authorDirectory)) Directory.CreateDirectory(authorDirectory);

            ConcurrentStack<Author> allAuthorsInDatabase = new ConcurrentStack<Author>();
            using (ApplicationDatabase ctxt = new ApplicationDatabase())
            {
                allAuthorsInDatabase = new ConcurrentStack<Author>(ctxt.Authors.OrderBy(o => o.Id));
            }
            int maxThreads = 12; //Change this to the amount of cores on the target PC

            int totalAmountOfAuthorsInDatabase = allAuthorsInDatabase.Count();
            int totalAuthorsValidated = 0;

            System.Timers.Timer titleBarUpdateTimer = new System.Timers.Timer();
            titleBarUpdateTimer.Elapsed += (sender, args) => Console.Title = $"{totalAuthorsValidated}/{totalAmountOfAuthorsInDatabase}";
            titleBarUpdateTimer.Interval = 1000;
            titleBarUpdateTimer.Start();

            for (int i = 0; i < maxThreads; i++)
            {
                Console.WriteLine($"Starting author validator {i + 1} of {maxThreads}");
                ThreadPool.QueueUserWorkItem((Action) =>
                {
                    while(true)
                    {
                        try
                        {
                            Author toHandle = null;
                            while (toHandle == null)
                            {
                                allAuthorsInDatabase.TryPop(out toHandle);
                            }
                            ValidateAuthor(toHandle, authorDirectory);
                            Interlocked.Increment(ref totalAuthorsValidated);
                        }
                        catch (Exception e)
                        {
                            try { File.AppendAllText(@"C:\ScratchScrapeData\AuthorScraper\errors\errors.txt", e.Message); }
                            catch (Exception) { }
                        }
                    }
                }, null);
            }
        }
    }
}
