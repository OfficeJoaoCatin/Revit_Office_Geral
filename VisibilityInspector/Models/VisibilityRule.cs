namespace RevitFamilyImporter.VisibilityInspector.Models
{
    public class VisibilityRule
    {
        public string Name { get; set; } = string.Empty;
        public string Expected { get; set; } = string.Empty;
        public string Actual { get; set; } = string.Empty;
        public bool? Passed { get; set; }
    }
}
