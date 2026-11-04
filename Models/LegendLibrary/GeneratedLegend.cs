using Autodesk.Revit.DB;

namespace RevitFamilyImporter.Models.LegendLibrary
{
    public class GeneratedLegend
    {
        public string Name { get; set; }

        public double Width { get; set; }

        public double Height { get; set; }

        public ElementId ImportId { get; set; } = ElementId.InvalidElementId;

        public string SourceFile { get; set; }
    }
}

