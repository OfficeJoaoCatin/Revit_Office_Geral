using Autodesk.Revit.DB;

namespace RevitFamilyImporter.DwgClash.Models
{
    public enum DwgCategoryGroup
    {
        Electrical,
        Hydraulic,
        Other
    }

    public sealed class DwgCategoryOption
    {
        public ElementId CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
        public DwgCategoryGroup Group { get; set; }
    }
}
