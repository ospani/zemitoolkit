using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Timers;
using MySql.Data.MySqlClient;
using Newtonsoft.Json.Linq;
using Zemi.IO;
using Zemi.Parsers;
using ZemiScrape;
using ZemiScrape.Models;
using ZemiScrape.Scrapers;
using Block = ZemiScrape.Models.Block;

namespace Zemi
{
    public class JSONReader
    {
        private static Dictionary<string, OpCode> GetOpcodes(bool sb2, bool sb3, bool verbose = false)
        {
            Dictionary<string, OpCode> opCodes = new Dictionary<string, OpCode>();
            using (ApplicationDatabase ctxt = new ApplicationDatabase())
            {
                if(sb2)
                {
                    opCodes = ctxt.OpCodes.Where(o => !string.IsNullOrEmpty(o.OpCodeSymbolLegacy)).ToDictionary(p => p.OpCodeSymbolLegacy);
                }
                if(sb3)
                {
                    foreach (OpCode toAdd in ctxt.OpCodes.Where(o => !string.IsNullOrEmpty(o.OpCodeSymbolSb3)))
                    {
                        if (opCodes.ContainsKey(toAdd.OpCodeSymbolSb3))
                        {
                            if (verbose) Say($"Sb3 opcode: {toAdd.OpCodeSymbolSb3} already in OpCode cache. Skipping.");
                            continue;
                        }
                        opCodes.Add(toAdd.OpCodeSymbolSb3, toAdd);
                    }
                }

            }
            return opCodes;
        }

        internal static void ProcessUnregisteredProjects(string pathToUnregisteredProjects, ProjectScraper p)
        {
            // Create a timer with a two second interval.
            System.Timers.Timer aTimer = new System.Timers.Timer(20000);
            // Hook up the Elapsed event for the timer. 
            aTimer.Elapsed += (Object source, ElapsedEventArgs e) => { Say("Not done yet.."); };
            aTimer.AutoReset = true;
            aTimer.Enabled = true;


            Say($"Enumerating unregistered project files in {Path.Combine(pathToUnregisteredProjects, "UnregisteredProjects.txt")}.");
            string[] unregisteredProjectIds = File.ReadAllLines(Path.Combine(pathToUnregisteredProjects, "UnregisteredProjects.txt")).Distinct<string>().ToArray();
            Say($"Enumerating unregistered project files done");
            aTimer.Stop();aTimer.Start();

            Say($"Enumerating existing project files in {pathToUnregisteredProjects}. This could take a very long time...");
            string[] fileNames = Directory.GetFiles(pathToUnregisteredProjects).Select(o => Path.GetFileName(o)).ToArray();
            Say($"Enumerating existing project files done.");
            aTimer.Stop(); aTimer.Start();

            Say($"Creating projects cache. This could take a very long time...");
            Dictionary<string, string> projectCache = new Dictionary<string, string>(fileNames.ToDictionary(x => x.Substring(0, x.IndexOf('.')), x => $".{x.Substring(x.IndexOf('.') + 1)}"));
            Say($"Creating projects cache done.");

            fileNames = null; //Otherwise, millions of strings will be hanging around for no reason.
            aTimer.Enabled = false;
            aTimer = null;
            GC.Collect();

            using (ApplicationDatabase ctxt = new ApplicationDatabase())
            {
                foreach (string projectId in unregisteredProjectIds)
                {
                    if (!Int32.TryParse(projectId, out int projectIdAsInt)) continue;
                    if (ctxt.Projects.AsNoTracking().Any(o => o.Id == projectIdAsInt)) continue;

                    string baseUrl = "https://api.scratch.mit.edu/projects/{0}";
                    string projectInfoJson = JSONGetter.GetAsJSONString(string.Format(baseUrl, projectId));

                    JObject projectObject = JObject.Parse(projectInfoJson);
                    ProjectAuthor author = Newtonsoft.Json.JsonConvert.DeserializeObject<ProjectAuthor>(projectObject["author"].ToString());
                    if (ctxt.Authors.AsNoTracking().Any(o => o.Id == author.id)) //If the author is known...
                    {
                        projectCache.TryGetValue(projectId, out string fileExtension);//Validate if it exists as a file... 
                        if (string.IsNullOrEmpty(fileExtension))
                        {
                            p.DownloadProjectToFile(projectId);
                        }

                        Project newProject = p.ParseProject(projectInfoJson, false);
                        newProject.AuthorId = author.id;
                        ctxt.Projects.Add(newProject);
                        ctxt.SaveChanges();

                        //TODO: Optionally immediately parse the actual project and its blocks.
                        

                    }
                    else
                    {
                        Say($"Found project from unknown author: {author.id}");
                    }
                    projectCache.Remove(projectId); //This way, the cache will immediately get rid of now useless entries
                }

            }

        }

        /// <summary>
        /// This method seeds the SpriteTypes table
        /// Since these were added manually, we have to do that too.
        /// </summary>
        private static void SeedSpriteTypeTable()
        {
            using (ApplicationDatabase ctxt = new ApplicationDatabase())
            {
                List<SpriteType> spriteTypesInDb = ctxt.SpriteTypes.ToList();
                if (!spriteTypesInDb.Any(o => o.Id == 1 && o.spriteTypeName == "sprite"))
                {
                    ctxt.SpriteTypes.Add(new SpriteType() { Id = 1, spriteTypeName = "sprite" });
                }
                if (!spriteTypesInDb.Any(o => o.Id == 2 && o.spriteTypeName == "stage"))
                {
                    ctxt.SpriteTypes.Add(new SpriteType() { Id = 2, spriteTypeName = "stage" });
                }
                if (!spriteTypesInDb.Any(o => o.Id == 3 && o.spriteTypeName == "procDef"))
                {
                    ctxt.SpriteTypes.Add(new SpriteType() { Id = 3, spriteTypeName = "procDef" });
                }
                ctxt.SaveChanges();
            }
        }
        private static void Say(string s, bool newline = true)
        {
            if (newline) Console.WriteLine(s);
            else Console.Write(s);
        }
        public static void ProcessJSON2(string projectDirectoryPath, int threads = 1)
        {
            ZemiIO.EnableConcurrentWriting();
            Say("Preparing database-dependent data...");
            Say("Checking SpriteType rows.. ", false);
            SeedSpriteTypeTable();
            Say("OK");
            
            Say("Mapping sb2 opcodes to sb3 opcodes.. ", false);
            MapSb2ToSb3.Map(false);
            Say("OK");

            Say("Loading sb2 and sb3 opcode caches.. ", false);
            Dictionary<string, OpCode> sb2Opcodes = GetOpcodes(true, false);
            Dictionary<string, OpCode> sb3OpCodes = GetOpcodes(false, true);
            Say("OK");

            Say($"Checking log output files, set to {projectDirectoryPath}.. ", false);
            ZemiIO.CreateFileIfNotexists(projectDirectoryPath, "HandledProjects.txt");
            ZemiIO.CreateFileIfNotexists(projectDirectoryPath, "EmptyProjects.txt");
            ZemiIO.CreateFileIfNotexists(projectDirectoryPath, "UnregisteredProjects.txt");
            ZemiIO.CreateFileIfNotexists(projectDirectoryPath, "MalformedBlocks.txt");
            ZemiIO.CreateDirectoryIfNotExists(projectDirectoryPath, @"remixes\");
            string remixPath = Path.Combine(projectDirectoryPath, @"remixes\");

            ZemiIO.CreateDirectoryIfNotExists(projectDirectoryPath, @"sb1\");
            string sb1Path = Path.Combine(projectDirectoryPath, @"sb1\");
            Say("OK");

            Say($"Enumerating files in {projectDirectoryPath}. This may take a while.. ");
            string[] allFilesInDirectory = Directory.GetFiles(projectDirectoryPath);
            ConcurrentStack<string> allFilesToHandle = new ConcurrentStack<string>(allFilesInDirectory);
            int count_allFilesInDirectory = allFilesInDirectory.Count();
            allFilesInDirectory = null;
            GC.Collect();
            Say($"Enumeration completed. Found {allFilesToHandle.Count()} files.");
            int totalFilesHandled = 0;

            System.Timers.Timer titleBarUpdateTimer = new System.Timers.Timer();
            titleBarUpdateTimer.Elapsed += (sender, args) => Console.Title = $"{totalFilesHandled}/{count_allFilesInDirectory}";
            titleBarUpdateTimer.Interval = 1000;
            titleBarUpdateTimer.Start();

            Say("Starting parsers... NOW!");
            for (int i = 0; i < threads; i++)
            {
                Say($"Starting parser {i+1} of {threads}.");
                new Thread(() =>
                {
                    using (ApplicationDatabase ctxt = new ApplicationDatabase())
                    {
                        Sb2Parser sb2Parser = new Sb2Parser(sb2Opcodes);
                        Sb3Parser sb3Parser = new Sb3Parser(sb3OpCodes);

                        while (true)
                        {
                            string handling = null;
                            while (handling == null)
                            {
                                allFilesToHandle.TryPop(out handling);
                            }
                            Interlocked.Increment(ref totalFilesHandled);   

                            string[] splitProjectFileName = handling.Split('.');
                            if (splitProjectFileName.Length != 3) continue; //This guarantees we only handle projectid.sb3.json files, and not the handledprojects.txt, for example.

                            string projectId = Path.GetFileName(splitProjectFileName[0]);
                            string projectSbVersion = splitProjectFileName[1];
                            int projectIdAsInt = Int32.Parse(projectId);

                            if (projectSbVersion == "sb")
                            {
                                try
                                {
                                    File.Move(handling, Path.Combine(sb1Path, Path.GetFileName(handling)));
                                }
                                catch(Exception ex)
                                {
                                    Console.WriteLine(ex.Message, ex.StackTrace);
                                }
                                continue;
                            }

                            if (ctxt.Scripts.Any(o => o.ProjectId == projectIdAsInt))
                            {
                                //Say($"Already handled {projectIdAsInt}");
                                continue;
                            }

                            Project possibleExistingProject = ctxt.Projects.AsNoTracking().Where(o => o.Id == projectIdAsInt).FirstOrDefault();
                            if(possibleExistingProject == null)
                            {
                                //Say($"Unregistered: {projectId}");
                                HandleUnregisteredProject(projectId.ToString(), projectDirectoryPath);
                                continue;
                            }
                            else if(possibleExistingProject.IsRemix)
                            {
                                //Say($"Remix: {projectId}");
                                File.Move(handling, Path.Combine(remixPath, Path.GetFileName(handling)));
                                continue;
                            }

                            string projectJson = File.ReadAllText(handling);
                            if (string.IsNullOrEmpty(projectJson) || string.IsNullOrWhiteSpace(projectJson))
                            {
                                Say($"Empty: {projectId}");
                                HandleEmptyProject(projectId.ToString(), projectDirectoryPath);
                                continue;
                            }
                            else if(projectJson.StartsWith("ScratchV") || projectJson.StartsWith("PK"))
                            {
                                try
                                {
                                    Say($"Uncaught sb1: {Path.GetFileName(handling)}");
                                    File.Move(handling, Path.Combine(sb1Path, Path.GetFileName(handling)));
                                }
                                catch (Exception ex)
                                {
                                    Console.WriteLine(ex.Message, ex.StackTrace);
                                }
                                continue;
                            }

                            try
                            {
                                switch (projectSbVersion)
                                {
                                    case "sb2":
                                        sb2Parser.ParseProject(projectJson, projectId);
                                        break;
                                    case "sb3":
                                        sb3Parser.ParseProject(projectJson, Int32.Parse(projectId));
                                        break;
                                    default:
                                        Say("Can't parse sb1 formats yet.");
                                        break;
                                }
                                HandleParsedProject(projectId, projectDirectoryPath);
                            }
                            catch(Exception ex)
                            {
                                Say(ex.Message);
                            }
                        }
                    }
                }, 16000000).Start();
            }
            Say("Look at 'em go!");
        
        }
        public static void SaveBlockWithParameters(Block blockToSave, string[] parameters)
        {
            //We want to avoid having to create a explosive if-tree where we fill each parameter column.
            //Instead, we simply compile an insert query.
            string SqlQuery = "INSERT INTO Blocks (ScriptId,BlockRank,Indent,OpCodeId,param1,param2,param3,param4,param5,param6,param7,param8,param9,param10,param11,param12,param13,param14,param15,param16,param17,param18,param19,param20,param21,param22,param23,param24) ";
            SqlQuery += $"VALUES ({(blockToSave.Script == null ? blockToSave.ScriptId : blockToSave.Script.ScriptId)},{blockToSave.BlockRank},{blockToSave.Indent},{blockToSave.OpCodeId}, @param1,@param2,@param3,@param4,@param5,@param6,@param7,@param8,@param9,@param10,@param11,@param12,@param13,@param14,@param15,@param16,@param17,@param18,@param19,@param20,@param21,@param22,@param23,@param24)";
            List<MySqlParameter> blockParameters = new List<MySqlParameter>();
            try
            {
                if (parameters == null) parameters = new string[] { };
                using (ApplicationDatabase ctxt = new ApplicationDatabase())
                {
                    int paramCount = 0;
                    int maxParamCount = 24;

                    for (paramCount = 1; paramCount <= maxParamCount; paramCount++)
                    {
                        blockParameters.Add(new MySqlParameter($"@param{paramCount}", $"{(parameters.Length >= paramCount ? $"{parameters[paramCount - 1]}" : "NULL")}"));
                    }

                    ctxt.Database.ExecuteSqlCommand(SqlQuery, blockParameters.ToArray());
                }
            }
            catch (Exception e)
            {
                Say(e.Message);
            }
        }

        public static void HandleUnregisteredProject(string projectId, string projectDirectoryPath)
        {
            ZemiIO.TryAppendFile(projectId.ToString(), Path.Combine(projectDirectoryPath, "UnregisteredProjects.txt"));
        }

        private static void HandleEmptyProject(string projectId, string projectDirectoryPath)
        {
            ZemiIO.TryAppendFile(projectId.ToString(), Path.Combine(projectDirectoryPath, "EmptyProjects.txt"));
        }

        private static void HandleParsedProject(string projectId, string projectDirectoryPath)
        {
            ZemiIO.TryAppendFile(projectId.ToString(), Path.Combine(projectDirectoryPath, "HandledProjects.txt"));
        }
        

    }
}
