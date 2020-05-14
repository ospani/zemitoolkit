using System.Collections.Generic;
using ZemiScrape.Models;

namespace Zemi.Parsers
{
    partial class Sb3Parser
    {
        internal class DecomposedSb3Target
        {
            public Dictionary<Script, List<Block>> AllScriptsAndBlocks = new Dictionary<Script, List<Block>>();
            public List<Procedure> AllProcedures = new List<Procedure>();
        }

    }
}
