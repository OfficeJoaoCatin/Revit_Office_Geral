using Autodesk.Revit.DB;

namespace RevitFamilyImporter.VisibilityInspector.Models
{
    public class ViewOption
    {
        public string Name { get; set; } = string.Empty;
        public string ViewType { get; set; } = string.Empty;
        public ElementId Id { get; set; } = ElementId.InvalidElementId;

        public string DisplayName => $"{Name} ({ViewType})";
    }
}
