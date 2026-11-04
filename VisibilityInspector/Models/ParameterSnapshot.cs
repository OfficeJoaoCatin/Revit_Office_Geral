namespace RevitFamilyImporter.VisibilityInspector.Models
{
    public class ParameterSnapshot
    {
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string StorageType { get; set; } = string.Empty;
        public bool ReadOnly { get; set; }
    }
}
