using Autodesk.Revit.DB;

namespace RevitFamilyImporter.Models.DetailImport
{
    public class ImportedDetail
    {
        public string Name { get; set; }

        public XYZ Position { get; set; }

        public double Scale { get; set; }

        public ElementId ImportInstanceId { get; set; } = ElementId.InvalidElementId;

        public ElementId ViewId { get; set; } = ElementId.InvalidElementId;

        public string ViewName { get; set; }

        public bool CreatedAsLegend { get; set; }

        public string SourceFile { get; set; }
    }
}

