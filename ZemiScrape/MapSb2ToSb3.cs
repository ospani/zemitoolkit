using System;
using System.IO;
using System.Linq;
using ZemiScrape.Models;

namespace ZemiScrape
{
    public static class MapSb2ToSb3
    {
        private static void ImportSb2BlockTypes()
        {
            string[] sb2BlockTypes = File.ReadAllLines(@"[DOES NOT EXIST] Assets\Sb2BlockTypes");
            string[] sb3BlockInputs = File.ReadAllLines(@"[DOES NOT EXIST] Assets\Sb3BlockInputs");
            using (ApplicationDatabase ctxt = new ApplicationDatabase())
            {
                ctxt.OpCodes.RemoveRange(ctxt.OpCodes);
                ctxt.SaveChanges();

                foreach (string blockType in sb2BlockTypes)
                {
                    string[] opCodeInfo = blockType.Split(',');
                    OpCode newOpCode = new OpCode()
                    {
                        OpcodeBlockType = opCodeInfo[0].Trim(),
                        OpCodeSymbolLegacy = opCodeInfo[1].Trim(),
                        OpCodeSymbolSb3 = "".Trim(),
                        ShapeName = opCodeInfo[2].Trim(),
                        IsInput = (opCodeInfo[3] == "Yes").ToString().Trim()
                    };
                    ctxt.OpCodes.Add(newOpCode);
                }
                //Now put in sb3 block input types. They don't have an sb2 counterpart.
                foreach (string blockInput in sb3BlockInputs)
                {
                    OpCode newOpCode = new OpCode()
                    {
                        OpcodeBlockType = "MenuInput",
                        IsInput = "True",
                        OpCodeSymbolLegacy = "",
                        OpCodeSymbolSb3 = blockInput
                    };
                    ctxt.OpCodes.Add(newOpCode);
                }
                ctxt.SaveChanges();
            }
        }

        public static void Map(bool remapCompletely = false)
        {
            //This is simply a file that contains on every line an sb2 opcode and its equivalent sb3 opcode, separated by a ~ character
            //Example: someSb2Opcode ~ someSb3Opcode
            if (remapCompletely) ImportSb2BlockTypes();
            string[] sb2BlocksAndTheirSb3Mappings = File.ReadAllLines(@"[DOES NOT EXIST] Assets\blockMappingSb2toSb3");
            using (ApplicationDatabase ctxt = new ApplicationDatabase())
            {
                foreach (OpCode opCode in ctxt.OpCodes) //To avoid nasty errors, trim everything to sanitize the input somewhat.
                {
                    opCode.OpCodeSymbolSb3 = opCode.OpCodeSymbolSb3.Trim();
                    opCode.OpCodeSymbolLegacy = opCode.OpCodeSymbolLegacy.Trim();
                }

                foreach (string mappedOpCode in sb2BlocksAndTheirSb3Mappings)
                {
                    string[] splitOpCode = mappedOpCode.Split('~');
                    if (splitOpCode.Length != 2)
                    {
                        Console.WriteLine($"Anomaly in sb2 to sb3 mapping: {mappedOpCode} was not able to be parsed.");
                        continue;
                    }
                    string sb2OpCode = splitOpCode.First().Trim();
                    string sb3OpCode = splitOpCode.Last().Trim();

                    OpCode opCodeInDatabase = ctxt.OpCodes.Where(o => o.OpCodeSymbolLegacy.ToLower() == sb2OpCode.ToLower()).FirstOrDefault();
                    if (opCodeInDatabase == null)
                    {
                        Console.WriteLine($"Anomaly in sb2 to sb3 mapping: {sb2OpCode} was not found in the database");
                        continue;
                    }
                    opCodeInDatabase.OpCodeSymbolSb3 = sb3OpCode;
                }
                ctxt.SaveChanges();
            }
        }
    }
}
