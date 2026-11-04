using Autodesk.Revit.DB;

namespace RevitFamilyImporter.LinkConflict.Models
{
    public class LinkedCategoryOption
    {
        public ElementId CategoryId { get; set; } = ElementId.InvalidElementId;
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
        public string DisplayName => $"{Name} ({Count:N0})";
    }
}
