using System.Collections.Generic;

namespace RevitFamilyImporter.VisibilityInspector.Models
{
    public class FilterResult
    {
        public string Name { get; set; } = string.Empty;
        public string Id { get; set; } = string.Empty;
        public int Priority { get; set; }
        public bool Applied { get; set; }
        public bool? ElementPasses { get; set; }
        public bool Hidden { get; set; }
        public string Color { get; set; } = string.Empty;
        public string Weight { get; set; } = string.Empty;
        public string Transparency { get; set; } = string.Empty;
        public string Halftone { get; set; } = string.Empty;
        public string Pattern { get; set; } = string.Empty;
        public string Conclusion { get; set; } = string.Empty;
        public List<VisibilityRule> Rules { get; } = new List<VisibilityRule>();

        public string AppliedText => Applied ? "Sim" : "Não";
        public string PassesText => ElementPasses.HasValue ? (ElementPasses.Value ? "Sim" : "Não") : "N/A";
        public string HiddenText => Hidden ? "Sim" : "Não";
    }
}
