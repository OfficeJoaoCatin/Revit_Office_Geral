using System.Collections.Generic;

namespace RevitFamilyImporter.LinkConflict.Models
{
    public class LinkConflictReport
    {
        public string LinkName { get; set; } = string.Empty;
        public int LinkInstanceId { get; set; }
        public string LinkedDocumentTitle { get; set; } = string.Empty;
        public string CategoryName { get; set; } = string.Empty;
        public int LinkedElementCount { get; set; }
        public int CandidateHostElementCount { get; set; }
        public List<LinkConflictItem> Conflicts { get; } = new List<LinkConflictItem>();
    }
}
