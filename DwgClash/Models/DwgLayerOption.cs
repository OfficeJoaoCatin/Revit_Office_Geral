namespace RevitFamilyImporter.DwgClash.Models
{
    public sealed class DwgLayerOption
    {
        public string Name { get; set; } = string.Empty;
        public int EntityCount { get; set; }
        public string DisplayName => $"{Name} ({EntityCount})";
    }
}
