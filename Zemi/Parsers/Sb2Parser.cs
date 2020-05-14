using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using ZemiScrape.Models;

namespace Zemi.Parsers
{
    class Sb2Parser : Parser
    {
        public Sb2Parser(Dictionary<string, OpCode> opCodeMap) : base(opCodeMap)
        {
        }

        /// <summary>
        /// Accepts .sb2 project JSON and its corresponding projectId and then saves all the contained scripts and their blocks to the attached database.
        /// </summary>
        /// <param name="projectJson">The raw json string of the project, with or without newline/carriage returns.</param>
        /// <param name="projectId">The project ID of the project the JSON belong to.</param>
        public void ParseProject(string projectJson, string projectId)
        {
            try
            {
                List<Script> projectScripts = GetAllScripts(projectJson, Int32.Parse(projectId));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }
        private List<Script> GetAllScripts(string projectJson, int projectId)
        {
            JObject projectObject = JObject.Parse(projectJson);
            List<Script> toReturn = new List<Script>();

            projectObject.TryGetValue("scripts", out JToken stageScriptToken);
            using (ApplicationDatabase ctxt = new ApplicationDatabase())
            {
                if (stageScriptToken != null)
                {
                    foreach (var obj in stageScriptToken.Children())
                    {

                        string scriptCoordinates = $"{obj[0].Value<Int32>()}-{obj[1].Value<Int32>()}"; //Get X and Y coordinates
                        Script currentScript = new Script() { Coordinates = scriptCoordinates, ProjectId = projectId, SpriteTypeId = 2, SpriteName = "stage" };

                        currentScript = ctxt.Scripts.Add(currentScript);
                        ctxt.SaveChanges();

                        List<Block> blocksInScript = GetAllBlockFromScript((JArray)obj, currentScript.ScriptId);
                        currentScript.TotalBlocks = blocksInScript.Count();

                        foreach (Block b in blocksInScript)
                        {
                            b.ScriptId = currentScript.ScriptId;
                            JSONReader.SaveBlockWithParameters(b, b.parameters);
                        }
                        ctxt.SaveChanges();
                    }
                }

                projectObject.TryGetValue("children", out JToken childrenToken);
                if (childrenToken != null)
                {
                    foreach (JObject sprite in childrenToken.Children())
                    {
                        string spriteName = sprite.Value<string>("objName");
                        sprite.TryGetValue("scripts", out JToken spriteScriptsToken);
                        if (spriteScriptsToken != null)
                        {
                            foreach (var obj in spriteScriptsToken.Children())
                            {
                                string scriptCoordinates = $"{obj[0].Value<Int32>()}-{obj[1].Value<Int32>()}"; //Get X and Y coordinates
                                Script currentScript = new Script() { Coordinates = scriptCoordinates, ProjectId = projectId, SpriteTypeId = 1, SpriteName = spriteName };
                                currentScript = ctxt.Scripts.Add(currentScript);
                                ctxt.SaveChanges();

                                List<Block> blocksInScript = GetAllBlockFromScript((JArray)obj, currentScript.ScriptId);
                                currentScript.TotalBlocks = blocksInScript.Count();

                                foreach (Block b in blocksInScript)
                                {
                                    b.ScriptId = currentScript.ScriptId;
                                    JSONReader.SaveBlockWithParameters(b, b.parameters);
                                }
                                if (blocksInScript.Any(o => o.IsPartOfProcDef)) //Just check if we found any block that was procDef , which can only occur if the entire script is a procDef
                                {
                                    currentScript.SpriteTypeId = 3;
                                }
                                ctxt.SaveChanges();
                            }
                        }
                    }
                }
            }
            return toReturn;
        }

        private List<Block> GetAllBlockFromScript(JArray scriptObject, int scriptId)
        {
            List<Block> allBlocksInScript = new List<Block>();
            JArray blocksArray = (JArray)scriptObject[2];
            int order = 0;
            allBlocksInScript.AddRange(RecurseInto(blocksArray, ref order, 0, scriptId));
            return allBlocksInScript;
        }

        private int GetOpCodeIdByName(string name)
        {
            if (OpCodes.ContainsKey(name))
            {
                return OpCodes[name].id;
            }
            else return OpCodes["unknown"].id;
        }

        private bool IsArrayOfArrays(JArray toCheck)
        {
            foreach (var c in toCheck)
            {
                if (c.GetType() != typeof(JArray))
                    return false;
            }
            return true;
        }

        private bool IsArrayOfPrimitives(JArray toCheck)
        {
            foreach (var c in toCheck)
            {
                if (c.GetType() == typeof(JArray))
                    return false;
            }
            return true;
        }

        private bool IsProcedureDefinition(JArray toCheck)
        {
            return (toCheck.Count > 1 && toCheck[0].ToString() == "procDef");
        }

        private bool IsRegularBlock(JArray toCheck)
        {
            return (toCheck.Count >= 1 && toCheck[0].GetType() != typeof(JArray));
        }

        private List<Block> RecurseInto(JArray arrayToRecurse, ref int order, int indent, int ScriptId)
        {
            if (indent > 1000) return new List<Block>();
            if (order > 2000) return new List<Block>();
            List<Block> encounteredBlocks = new List<Block>();
            if (IsArrayOfArrays(arrayToRecurse))
            {
                foreach (var array in arrayToRecurse)
                {
                    encounteredBlocks.AddRange(RecurseInto((JArray)array, ref order, indent + 1, ScriptId));
                }
            }
            else if (IsProcedureDefinition(arrayToRecurse))
            {
                Block encounteredBlock = new Block() { BlockRank = order, OpCodeId = GetOpCodeIdByName("procDef"), Indent = indent, IsPartOfProcDef = true };
                encounteredBlocks.Add(encounteredBlock);

                using (ApplicationDatabase ctxt = new ApplicationDatabase())
                {
                    Procedure encounteredProcedure = new Procedure()
                    {
                        ProcedureName = arrayToRecurse[1].ToString(),
                        ScriptId = ScriptId,
                        TotalArgs = arrayToRecurse[1].ToString().Split('%').Count() - 1 //Easier than counting the actual parameters of the method.
                    };
                    ctxt.Procedures.Add(encounteredProcedure);
                    ctxt.SaveChanges();

                }
            }
            else if (IsRegularBlock(arrayToRecurse))
            {
                order += 1;
                Block encounteredBlock = new Block() { BlockRank = order, OpCodeId = GetOpCodeIdByName(arrayToRecurse[0].ToString()), Indent = indent };
                if(encounteredBlock.OpCodeId == 2201)
                {
                    int x = 0;
                }
                encounteredBlocks.Add(encounteredBlock);

                List<string> parametersOfBlock = new List<string>();
                if (!IsArrayOfPrimitives(arrayToRecurse))
                {
                    foreach (var element in arrayToRecurse.Skip(1)) //Skip the field where the opCode name was
                    {
                        if (element is JArray) encounteredBlocks.AddRange(RecurseInto((JArray)element, ref order, indent + 1, ScriptId));
                        else parametersOfBlock.Add(element.ToString());//If it is a primitive type, then it's most certainly a parameter
                    }
                    encounteredBlock.parameters = parametersOfBlock.ToArray();
                }
                else
                {
                    //If it's an array of primitives, there will be no more nested blocks and therefore they must all be parameters.
                    encounteredBlock.parameters = arrayToRecurse.Skip(1).Select(o => o.ToString()).ToArray(); 
                }
            }
            return encounteredBlocks;
        }
    }
}
