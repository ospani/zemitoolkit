using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ZemiScrape.Models
{
#pragma warning disable CA1709, IDE1006
    /// <summary>
    /// The Author object represents the database entity. It is the flattened aggregation of the ProjectAuthor and AuthorHistory JSON models.
    /// </summary>
    public class Author
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
        public string Username { get; set; }
        public bool ScratchTeam { get; set; }
        public DateTime DateJoined { get; set; }
        public DateTime? DateLastLogged { get; set; }
        public int AmountFollowing { get; set; }
        public int AmountFollowers { get; set; }
        public string Country { get; set; }
        public ICollection<Project> Projects { get; set; }
    }

    /// <summary>
    /// An OpCode is an identifier for a specific block command in Scratch. 
    /// For example: An if-else block has an opcode called 'control_if_else'
    /// OpCodes are completely different for Sb2 and Sb3 and therefore a mapping is required to treat sb2 and sb3 blocks in the same way.
    /// This mapping is found here: https://raw.githubusercontent.com/LLK/scratch-vm/develop/src/serialization/sb2_specmap.js
    /// Based on this mapping, MapSb2ToSb3 uses three input files to first import and then map all known sb2 and sb3 blocks.
    /// </summary>
    public class OpCode
    {
        [Key]
        public int id { get; set; }
        public string OpcodeBlockType { get; set; }
        public string OpCodeSymbolLegacy { get; set; }
        public string OpCodeSymbolSb3 { get; set; }
        public string ShapeName { get; set; }
        public string IsInput { get; set; }
    }
    /// <summary>
    /// A block is the elementary unit of composition in Scratch. Blocks have opcodes and belong somewhere in the editor.
    /// They have different types and can do a lot of different things. 
    /// More on blocks here: https://en.scratch-wiki.info/wiki/Blocks
    /// </summary>
    public class Block
    {
        [Key, ForeignKey("Script"), Column(Order=1)]
        public int ScriptId { get; set; }
        public Script Script { get; set; }
        [Key, Column(Order = 2), DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int BlockRank { get; set; }
        public int Indent { get; set; }
        public int OpCodeId { get; set; }
        public OpCode OpCode{ get; set; }

        public string param1 { get; set; }
        public string param2 { get; set; }
        public string param3 { get; set; }
        public string param4 { get; set; }
        public string param5 { get; set; }
        public string param6 { get; set; }
        public string param7 { get; set; }
        public string param8 { get; set; }
        public string param9 { get; set; }
        public string param10 { get; set; }
        public string param11 { get; set; }
        public string param12 { get; set; }
        public string param13 { get; set; }
        public string param14 { get; set; }
        public string param15 { get; set; }
        public string param16 { get; set; }
        public string param17 { get; set; }
        public string param18 { get; set; }
        public string param19 { get; set; }
        public string param20 { get; set; }
        public string param21 { get; set; }
        public string param22 { get; set; }
        public string param23 { get; set; }
        public string param24 { get; set; }

        [NotMapped]
        public string[] parameters;

        [NotMapped]
        public bool IsPartOfProcDef;
    }
    /// <summary>
    /// A SpriteType is an object that represents whether a Scratch script is defined on the stage, on a sprite, or is a procedure definition.
    /// </summary>
    public class SpriteType
    {
        [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
        public string spriteTypeName { get; set; }
    }
    /// <summary>
    /// A script is any sequence of interconnected blocks found in the Scratch editor; a (custom) function made by the user.
    /// The first block of any script is a hat block. Scripts can be procedure definitions or regular scripts.
    /// </summary>
    public class Script
    {
        public Script()
        {
        }

        public Script(int projectId, int spriteTypeId, string spriteName, int scriptRank, string coordinates, int totalBlocks)
        {
            ProjectId = projectId;
            SpriteTypeId = spriteTypeId;
            SpriteName = spriteName;
            ScriptRank = scriptRank;
            Coordinates = coordinates;
            TotalBlocks = totalBlocks;
        }

        [Key]
        public int ScriptId { get; set; }
        public int ProjectId { get; set; } //fk to project
        public Project Project { get; set; }
        public int SpriteTypeId { get;set; } //fk to spritetype
        public SpriteType SpriteType { get; set; }
        public string SpriteName { get; set; }
        public int ScriptRank { get; set; }
        public string Coordinates { get; set; }
        public int TotalBlocks { get; set; }
    }
    /// <summary>
    /// A project is made by an author and is a collection of scripts and their procedures and blocks.
    /// </summary>
    public class Project
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.None)]
        public int Id { get; set; }
        public string ProjectName { get; set; }
        public int AuthorId { get; set; }
        public Author Author { get; set; }
        public int TotalViews { get; set; }
        public int TotalFavorites { get; set; }
        public int TotalLoves { get; set; }
        public bool IsRemix { get; set; }

        public DateTime Created{get;set;}
        public DateTime Modified { get; set; }
        public DateTime Shared{ get; set; }
        public int? RemixParent { get; set; }
        public int? RemixRoot { get; set; }

    }

    //For some reason, cascade delete doesn't work here when Scripts are deleted.
    //Make sure to set the Script-Procedure FK constraint to cascade on delete in SQL Server Explorer
    /// <summary>
    /// A procedure is a special kind of script in that it starts with a user-defined hat block.
    /// The procedure can then be used multiple times within the same sprite or stage.
    /// Oddly enough, procedures cannot be shared across stages or sprites.
    /// </summary>
    public class Procedure
    {
        [Key, ForeignKey("Script"), Column(Order = 1)]
        public int ScriptId { get; set; }
        public Script Script { get; set; }
        public string ProcedureName { get; set; }
        public int TotalArgs { get; set; }
    }
#pragma warning restore CA1709, IDE1006
}
