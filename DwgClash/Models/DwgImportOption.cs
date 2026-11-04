using Autodesk.Revit.DB;

namespace RevitFamilyImporter.DwgClash.Models
{
    public sealed class DwgImportOption
    {
        public ImportInstance ImportInstance { get; set; }
        public string DisplayName { get; set; } = string.Empty;
    }
}
