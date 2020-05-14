using System.Collections.Generic;
using ZemiScrape.Models;

namespace Zemi.Parsers
{
    public abstract class Parser
    {
        public Dictionary<string, OpCode> OpCodes;
        public Parser(Dictionary<string, OpCode> opCodeMap)
        {
            this.OpCodes = opCodeMap;
        }
    }
}
