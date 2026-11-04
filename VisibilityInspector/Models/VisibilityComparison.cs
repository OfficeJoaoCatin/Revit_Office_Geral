using System.Collections.Generic;

namespace RevitFamilyImporter.VisibilityInspector.Models
{
    public class VisibilityComparison
    {
        public string BaseView { get; set; } = string.Empty;
        public string ComparedView { get; set; } = string.Empty;
        public string BaseStatus { get; set; } = string.Empty;
        public string ComparedStatus { get; set; } = string.Empty;
        public List<string> Differences { get; } = new List<string>();
    }
}
