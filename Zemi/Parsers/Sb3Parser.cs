using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using ZemiScrape.Models;

namespace Zemi.Parsers
{
    partial class Sb3Parser : Parser
    {
        public Sb3Parser(Dictionary<string, OpCode> opCodeMap) : base(opCodeMap)
        {

        }

        private bool IsVariable(JProperty block)
        {
            return (block.Value.GetType() == typeof(JArray));
        }

        public void ParseProject(string projectJson, int projectId)
        {
            if (projectJson.StartsWith("PK")) return;

            JObject projectObject;
            try { projectObject = JObject.Parse(projectJson); }
            catch (Exception ex) { Console.WriteLine(ex.Message); return; }

            projectObject.TryGetValue("targets", out JToken targetToken);
            if (targetToken == null) return;

            using (ApplicationDatabase ctxt = new ApplicationDatabase())
            {
                foreach (var obj in (JArray)targetToken)
                {
                    DecomposedSb3Target project = DecomposeTarget(obj as JObject, projectId);
                    foreach (KeyValuePair<Script, List<Block>> script in project.AllScriptsAndBlocks)
                    {
                        script.Key.ProjectId = projectId;
                        script.Key.TotalBlocks = script.Value.Count();

                        ctxt.Scripts.Add(script.Key);
                        ctxt.SaveChanges();

                        foreach (Block b in script.Value)
                        {
                            JSONReader.SaveBlockWithParameters(b, b.parameters);
                        }
                    }
                    ctxt.Procedures.AddRange(project.AllProcedures);
                    ctxt.SaveChanges();
                }
            }
        }

        private Dictionary<JObject, Block> ConvertToDatabaseBlocks(Dictionary<string, JObject> values)
        {
            Dictionary<JObject, Block> toReturn = new Dictionary<JObject, Block>();
            foreach (JObject o in values.Values)
            {
                Block newBlock = new Block()
                {
                    OpCodeId = OpCodes.ContainsKey(o.Value<string>("opcode")) ? OpCodes[o.Value<string>("opcode")].id : OpCodes["unknown"].id,
                    Indent = 0,
                    BlockRank = 0,
                };
                toReturn.Add(o, newBlock);
            }
            return toReturn;
        }

        private DecomposedSb3Target DecomposeTarget(JObject target, int projectId)
        {
            DecomposedSb3Target toReturn = new DecomposedSb3Target();

            Dictionary<string, JObject> allBlocksInTarget = GetAllBlockObjectsById(target);
            Dictionary<JObject, Block> ObjectToBlock = ConvertToDatabaseBlocks(allBlocksInTarget);
            Sb3BlockUnwinder unwinder = new Sb3BlockUnwinder(allBlocksInTarget, ObjectToBlock);

            int spriteTypeId = target.Value<bool>("isStage") ? 2 : 1;
            //For each hat block...
            foreach (KeyValuePair<string, JObject> block in allBlocksInTarget.Where(o => o.Value.Value<bool>("topLevel")).ToDictionary(o => o.Key, o => o.Value))
            {
                Script encounteredScript = new Script()
                {
                    Coordinates = $"{block.Value.Value<int>("x")}-{block.Value.Value<int>("y")}",
                    SpriteName = target.Value<string>("name"),
                    ProjectId = projectId,
                    SpriteTypeId = (block.Value.Value<string>("opcode") == "procedures_definition") ? 3 : spriteTypeId,
                    TotalBlocks = 0,
                };

                if (encounteredScript.SpriteTypeId == 3) //If it's a procedure definition, add the procedure too.
                {
                    Procedure encounteredProcedure = unwinder.GetProcedureFromDefinition(block.Value, encounteredScript);
                    toReturn.AllProcedures.Add(encounteredProcedure);
                }

                int blockRankCount = 0;
                List<Block> blocksOfScript = unwinder.UnwindBlock2(block.Value, 0, ref blockRankCount, encounteredScript);
                toReturn.AllScriptsAndBlocks.Add(encounteredScript, blocksOfScript);

            }
            return toReturn;
        }
        private Dictionary<string, JObject> GetAllBlockObjectsById(JObject target)
        {
            Dictionary<string, JObject> allBlocksInTarget = new Dictionary<string, JObject>();

            target.TryGetValue("blocks", out JToken blocksToken);
            if (blocksToken == null) return allBlocksInTarget;

            foreach (JProperty sb3Block in blocksToken.Children()) 
            {
                if (IsVariable(sb3Block)) continue; //The 'blocks' field of a target has an array of blocks. However, some blocks aren't objects, but arrays. These are 'fake' blocks.
                JObject sb3BlockObject = sb3Block.Value as JObject;
                allBlocksInTarget.Add(sb3Block.Name, sb3BlockObject);
            }
            return allBlocksInTarget;
        }

    }
}
