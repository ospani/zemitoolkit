using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using Newtonsoft.Json.Linq;
using ZemiScrape.Models;

namespace ZemiScrape.Scrapers
{
    public class ProjectScraper : ScratchScraper
    {
        ConcurrentDictionary<string, string> downloadedProjectsCache = new ConcurrentDictionary<string, string>();
        public ProjectScraper(string workingDirectoryPath) : base(workingDirectoryPath)
        {

        }

        public Project ParseProject(string projectJson, bool ignoreRemixes)
        {
            JObject projectObject = JObject.Parse(projectJson);
            JObject remixObject = (JObject)projectObject["remix"];
            JToken remixRootToken = remixObject["root"];
            JToken remixParentToken = remixObject["parent"];
            bool isRemixed = false;
            int remixParent = 0;
            int remixRoot = 0;
            if (!string.IsNullOrEmpty(remixRootToken.ToString())) //Check if this is a remixed project
            {
                isRemixed = true;
                remixRoot = Int32.Parse(remixRoot.ToString());
                if (ignoreRemixes) return null;
            }
            if (!string.IsNullOrEmpty(remixParentToken.ToString()))
            {
                remixParent = Int32.Parse(remixParentToken.ToString());
                if (ignoreRemixes) return null; ;
            }
            if (projectObject["is_published"].Value<bool>() == false) //Check if the project is published (not private)
            {
                Console.WriteLine($"P: {projectObject["id"]}");
                return null;
            }
            ProjectStats projectStats = Newtonsoft.Json.JsonConvert.DeserializeObject<ProjectStats>(projectObject["stats"].ToString());
            ProjectHistory projectHistory = Newtonsoft.Json.JsonConvert.DeserializeObject<ProjectHistory>(projectObject["history"].ToString());
            Project toAdd = new Project
            {
                Id = Int32.Parse(projectObject["id"].ToString()),
                ProjectName = projectObject["title"].ToString(),
                AuthorId = 0,
                Author = null,
                Created = projectHistory.created,
                Modified = projectHistory.modified,
                TotalViews = projectStats.views,
                TotalFavorites = projectStats.favorites,
                TotalLoves = projectStats.loves,
                Shared = projectHistory.shared == null ? DateTime.MinValue : (DateTime)projectHistory.shared,
                IsRemix = isRemixed,
                RemixParent = remixParent,
                RemixRoot = remixRoot
            };
            return toAdd;
        }
        public List<Project> GetProjectsByUsername(string userName, bool ignoreRemixes = false)
        {
            string apiEndpoint = "https://api.scratch.mit.edu/users/" + userName + "/projects?limit=40&offset={0}";
            bool endOfDataReached = false;
            int offset = 0;
            List<Project> allProjectsOfUser = new List<Project>();
            try
            {
                while (!endOfDataReached)
                {
                    string specifiedApiEndpoint = string.Format(apiEndpoint, offset);
                    string returnedProjects = JSONGetter.GetAsJSONString(specifiedApiEndpoint);
                    if (string.IsNullOrEmpty(returnedProjects)) break;
                    JArray parsedProjects = JArray.Parse(returnedProjects);
                    if (parsedProjects.Count == 0)
                    {
                        endOfDataReached = true;
                    }
                    foreach (var project in parsedProjects)
                    {
                        JObject projectObject = JObject.Parse(project.ToString());
                        JObject remixObject = (JObject)projectObject["remix"];
                        JToken remixRootToken = remixObject["root"];
                        JToken remixParentToken = remixObject["parent"];
                        bool isRemixed = false;
                        int remixParent = 0;
                        int remixRoot = 0;
                        if (!string.IsNullOrEmpty(remixRootToken.ToString())) //Check if this is a remixed project
                        {
                            isRemixed = true;
                            remixRoot = Int32.Parse(remixRoot.ToString());
                            if (ignoreRemixes) continue;
                        }
                        if (!string.IsNullOrEmpty(remixParentToken.ToString()))
                        {
                            remixParent = Int32.Parse(remixParentToken.ToString());
                            if (ignoreRemixes) continue;
                        }
                        if (projectObject["is_published"].Value<bool>() == false) //Check if the project is published (not private)
                        {
                            Console.WriteLine($"P: {projectObject["id"]}");
                            continue;
                        }
                        ProjectStats projectStats = Newtonsoft.Json.JsonConvert.DeserializeObject<ProjectStats>(projectObject["stats"].ToString());
                        ProjectHistory projectHistory = Newtonsoft.Json.JsonConvert.DeserializeObject<ProjectHistory>(projectObject["history"].ToString());
                        Project toAdd = new Project
                        {
                            Id = Int32.Parse(projectObject["id"].ToString()),
                            ProjectName = projectObject["title"].ToString(),
                            AuthorId = 0,
                            Author = null,
                            Created = projectHistory.created,
                            Modified = projectHistory.modified,
                            TotalViews = projectStats.views,
                            TotalFavorites = projectStats.favorites,
                            TotalLoves = projectStats.loves,
                            Shared = projectHistory.shared == null ? (DateTime)projectHistory.shared : DateTime.MinValue,
                            IsRemix = isRemixed,
                            RemixParent = remixParent,
                            RemixRoot = remixRoot
                        };
                        allProjectsOfUser.Add(toAdd);
                    }

                    offset += 40;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return allProjectsOfUser;
            }
            return allProjectsOfUser;
        }


        public void DownloadProjectToFile(string id)
        {
            if (downloadedProjectsCache.TryGetValue(id, out _))
            {
                return; //Don't download the project if it exists as a file already.
            }
            try
            {
                System.Net.ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
                string baseURL = $"https://projects.scratch.mit.edu/{id}";
                WebClient c = new WebClient();
                byte[] responseData = c.DownloadData(baseURL);
                string jsonString = System.Text.Encoding.Default.GetString(responseData);
                if (string.IsNullOrEmpty(jsonString) || string.IsNullOrWhiteSpace(jsonString)) { Console.WriteLine("Project download failed"); }
                string sbExtension = "unknown";
                if (jsonString.StartsWith("ScratchV") || jsonString.StartsWith("PK")) //Scratch 1.4 files, which are binary files, start with these strings.
                {
                    sbExtension = "sb";
                }
                else
                {
                    sbExtension = IsProjectSb3(jsonString) ? "sb3" : "sb2";
                }
                File.WriteAllText(Path.Combine(this.WorkingDirectoryPath, "projects", $"{id}.{sbExtension}.json"), jsonString);
                downloadedProjectsCache.TryAdd(id, null); //This is might not be necesary, I think there are no situations where collisions can occur
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private bool IsProjectSb3(string projectJson)
        {
            return !projectJson.Contains("\"objName\":"); //This field is unique to sb2's
        }

        public override void Scrape()
        {
            Console.WriteLine("Starting project scraping...");
            const int maxThreads = 8;
            ConcurrentStack<Author> toHandle = new ConcurrentStack<Author>();
            using (ApplicationDatabase ctxt = new ApplicationDatabase())
            {
                if (!Directory.Exists(Path.Combine(this.WorkingDirectoryPath, "projects")))
                {
                    Console.WriteLine($"Creating initial projects directory at: {Path.Combine(this.WorkingDirectoryPath, "projects")}");
                    try
                    {
                        Directory.CreateDirectory(Path.Combine(this.WorkingDirectoryPath, "projects"));
                    }
                    catch (Exception ex)
                    {
                        Console.Write(ex.Message);
                        return;
                    }
                }

                Console.WriteLine("Enumerating existing projects... (this may take a very long time!)");
                string[] fileNames = Directory.GetFiles(Path.Combine(this.WorkingDirectoryPath, "projects")).Select(o => Path.GetFileNameWithoutExtension(Path.GetFileNameWithoutExtension(o))).ToArray();
                Console.WriteLine($"Enumeration completed. Found {fileNames.Length} already scraped projects.\nGenerating projects cache...");
                downloadedProjectsCache = new ConcurrentDictionary<string, string>(fileNames.ToDictionary(x => x.ToString(), x => ""));
                Console.WriteLine("Generating projects cache completed.\nBuilding stack of authors to scrape...");

                foreach (Author a in ctxt.Authors.Where(o => o.Projects.Count() <= 0).OrderBy(o => o.Id))
                {
                    toHandle.Push(a);
                }
                Console.WriteLine("Building stack done. Starting scrapers...");
            }
            System.Timers.Timer titleBarUpdateTimer = new System.Timers.Timer();
            titleBarUpdateTimer.Elapsed += (sender, args) => Console.Title = $"{toHandle.Count()}";
            titleBarUpdateTimer.Interval = 10000;
            titleBarUpdateTimer.Start();


            int maxToHandle = toHandle.Count();
            for (int i = 0; i < maxThreads; i++)
            {
                ThreadPool.QueueUserWorkItem((Action) =>
                {

                    Console.WriteLine($"Project scraper {Guid.NewGuid().ToString()} of {maxThreads} active.");
                    using (ApplicationDatabase ctxt2 = new ApplicationDatabase())
                    {
                        while (true)
                        {
                            Author handling = null;
                            while (handling == null)
                            {
                                toHandle.TryPop(out handling);
                            }
                            foreach (Project project in GetProjectsByUsername(handling.Username, false))
                            {
                                if (!ctxt2.Projects.AsNoTracking().Any(o => o.Id == project.Id))
                                {
                                    project.AuthorId = handling.Id;
                                    ctxt2.Projects.Add(project);
                                }
                                DownloadProjectToFile(project.Id.ToString());
                            }
                            ctxt2.SaveChanges();
                        }
                    }
                }, null);
            }
        }
    }
}
