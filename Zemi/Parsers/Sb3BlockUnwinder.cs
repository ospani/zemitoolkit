using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using ZemiScrape.Models;

namespace Zemi.Parsers
{
    class Sb3BlockUnwinder
    {
        private readonly Dictionary<string, JObject> allBlocksById;
        private readonly Dictionary<JObject, Block> objectToBlockMap;

        public Sb3BlockUnwinder(Dictionary<string, JObject> allBlocksInTarget, Dictionary<JObject, Block> objectToBlock)
        {
            allBlocksById = allBlocksInTarget;
            objectToBlockMap = objectToBlock;
        }

        public List<Block> UnwindBlock2(JObject block, int currentIndent, ref int blockRank, Script parentScript)
        {
            if (currentIndent > 1000) return new List<Block>();
            if (blockRank > 2000) return new List<Block>();
            if (block.Value<string>("opcode") == "procedures_prototype") //Stupid hack to block procedures_prototype shadow blocks from being recorded as actual blocks.
            {
                return new List<Block>();
            }

            List<Block> encounteredBlocks = new List<Block>();
            Block currentlyHandling = objectToBlockMap[block];
            currentlyHandling.Script = parentScript;
            currentlyHandling.Indent = currentIndent;
            currentlyHandling.BlockRank = blockRank;
            encounteredBlocks.Add(currentlyHandling);

            blockRank++;

            List<string> parametersOfBlock = new List<string>();
            if (block.Value<string>("opcode") == "procedures_call") //Stupid hack to block procedures_prototype shadow blocks from being recorded as actual blocks.
            {
                JObject mutationObject = block.Value<JObject>("mutation");
                string procedureName = mutationObject.Value<string>("proccode");
                parametersOfBlock.Add(procedureName);
            }

            JObject inputs = block.Value<JObject>("inputs"); //The 'inputs' object of the block object can contain reporter blocks.
            foreach (JProperty input in inputs.Children())
            {
                if (input.Name.StartsWith("SUBSTACK")) //SUBSTACK parameters are not actually used as reporters, but as branches. Example: if-else branches have two substacks, SUBSTACK and SUBSTACK2
                {
                    string calledBlock = input.Value[1].ToString();
                    if (allBlocksById.ContainsKey(calledBlock))
                    {
                        encounteredBlocks.AddRange(UnwindBlock2(allBlocksById[calledBlock], currentIndent + 1, ref blockRank, parentScript));
                    }
                }
                else
                {
                    if (!(input.Value is JArray fieldValue)) continue;
                    int fieldType = (int)fieldValue[0];
                    if (fieldType == 1)
                    {
                        if(fieldValue[1] is JArray)
                        {
                            string parameter = ((JArray)fieldValue[1])[1].ToString();
                            parametersOfBlock.Add(parameter);
                        }
                        else
                        {
                            string calledBlock = fieldValue[1].ToString();
                            if (!string.IsNullOrEmpty(calledBlock)) encounteredBlocks.AddRange(UnwindBlock2(allBlocksById[calledBlock], currentIndent, ref blockRank, parentScript));
                        }
                    }
                    else if (fieldType == 2) //Used for blocks that reference a real block as their input
                    {
                        string calledBlock = fieldValue[1].ToString();
                        if (!string.IsNullOrEmpty(calledBlock))
                        {
                            if(allBlocksById.ContainsKey(calledBlock))
                            {
                                encounteredBlocks.AddRange(UnwindBlock2(allBlocksById[calledBlock], currentIndent, ref blockRank, parentScript));
                            }
                            else
                            {
                                JArray p = JArray.Parse(calledBlock);
                                if(p.Count == 2 || p.Count == 3)
                                {
                                    if(p[0].ToString() == "12"|| p[0].ToString() == "10")
                                    {
                                        parametersOfBlock.Add(p[1].ToString());
                                    }
                                    
                                }
                                Console.WriteLine($"Could not find block in dict: {calledBlock}");
                            }
                        }
                    }
                    else if (fieldType == 3) //Used for blocks that are operators. Such as operator_equals
                    {
                        if (fieldValue[1] is JArray)
                        {
                            string parameter = ((JArray)fieldValue[1])[1].ToString();
                            parametersOfBlock.Add(parameter);
                        }
                        else
                        {
                            string calledBlock = fieldValue[1].ToString();
                            if (!string.IsNullOrEmpty(calledBlock)) encounteredBlocks.AddRange(UnwindBlock2(allBlocksById[calledBlock], currentIndent, ref blockRank, parentScript));
                        }
                    }
                    else
                    {
                        Console.WriteLine($"\rUncaught fieldType: {fieldType}");
                        //Should never be called.
                    }
                }
            }


            JObject fields = block.Value<JObject>("fields");
            foreach (JProperty field in fields.Children())
            {
                string calledBlock = field.Value[0].ToString();
                parametersOfBlock.Add(calledBlock);
            }

            currentlyHandling.parameters = parametersOfBlock.ToArray();


            string nextBlockId = block.Value<string>("next");
            if (!string.IsNullOrEmpty(nextBlockId))
            {
                encounteredBlocks.AddRange(UnwindBlock2(allBlocksById[nextBlockId], currentIndent, ref blockRank, parentScript));
            }
            return encounteredBlocks;
        }
        public Procedure GetProcedureFromDefinition(JObject procedureDefBlock, Script associatedScriptObject)
        {
            //Getting the procedure name is a bit harder than it was for sb2 files.
            //The procedure definition information is not part of the procedures_definition block; rather, it is a shadow block contained within the inputs field of the procedures_definition block.
            string proceduresPrototypeBlockId = procedureDefBlock.Value<JObject>("inputs").Value<JArray>("custom_block")[1].ToString();
            JObject prototypeObject = allBlocksById[proceduresPrototypeBlockId];
            JObject mutationObject = prototypeObject.Value<JObject>("mutation");
            string procedureName = mutationObject.Value<string>("proccode");
            int numberOfArguments = JArray.Parse(mutationObject.Value<string>("argumentnames")).Count(); //frustratingly enough, the argumentnames are not a json array, but a string

            return new Procedure() { ProcedureName = procedureName, Script = associatedScriptObject, TotalArgs = numberOfArguments };
        }


    }
}
