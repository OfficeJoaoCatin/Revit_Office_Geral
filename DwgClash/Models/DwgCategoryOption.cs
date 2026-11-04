using Autodesk.Revit.DB;

namespace RevitFamilyImporter.DwgClash.Models
{
    public sealed class DwgCategoryOption
    {
        public ElementId CategoryId { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsSelected { get; set; }
    }
}
