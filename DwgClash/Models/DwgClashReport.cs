using System.Collections.Generic;

namespace RevitFamilyImporter.DwgClash.Models
{
    public sealed class DwgClashReport
    {
        public string ViewName { get; set; } = string.Empty;
        public string ImportName { get; set; } = string.Empty;
        public string Layer { get; set; } = string.Empty;
        public string ModeText { get; set; } = string.Empty;
        public string ClosedRegionText { get; set; } = string.Empty;
        public int ElementsAnalyzed { get; set; }
        public List<DwgClashResult> Results { get; } = new List<DwgClashResult>();
    }
}
