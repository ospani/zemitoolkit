using System;

namespace ZemiScrape.Models
{
#pragma warning disable CA1709, IDE1006
    /// <summary>
    /// The AuthorHistory object represents the object that is used to contain the date the author joined. 
    /// In the JSON returned by the Scratch API, this is a separate object within the Author object.
    /// </summary>
    public class AuthorHistory
    {
        public DateTime joined { get; set; }
        public DateTime? login { get; set; }
    }
    /// <summary>
    /// This object represents information about the history of a project, such as when it was created or shared.
    /// </summary>
    public class ProjectHistory
    {
        public DateTime created { get; set; }
        public DateTime modified { get; set; }
        public DateTime? shared { get; set; }
    }

    public class AuthorProfile
    {
        public string country { get; set; }
    }

    /// <summary>
    /// The ProjectAuthor object represents the author of a project that is scraped. In the JSON returned by the Scratch API, this is a separate object within the Project object.
    /// </summary>
    public class ProjectAuthor
    {
        public int id { get; set; }
        public string username { get; set; }
        public bool scratchteam { get; set; }
        public AuthorHistory history { get; set; }

        public AuthorProfile profile { get; set; }
    }
    public class ProjectStats
    {
        public int views { get; set; }
        public int loves { get; set; }
        public int favorites { get; set; }
        public int comments { get; set; }
        public int remixes { get; set; }
    }
#pragma warning disable CS1696 // Single-line comment or end-of-line expected after #pragma directive
#pragma warning restore warning-list CA1709, IDE1006
}
#pragma warning restore CS1696 // Single-line comment or end-of-line expected after #pragma directive
