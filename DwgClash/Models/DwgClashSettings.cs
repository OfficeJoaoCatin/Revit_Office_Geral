using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RevitFamilyImporter.DwgClash.Models
{
    public sealed class DwgClashSettings
    {
        public Autodesk.Revit.DB.View View { get; set; }
        public ImportInstance ImportInstance { get; set; }
        public string Layer { get; set; } = string.Empty;
        public double ToleranceFeet { get; set; }
        public DwgClashAnalysisMode Mode { get; set; }
        public DwgClashClosedRegionMode ClosedRegionMode { get; set; } = DwgClashClosedRegionMode.BoundaryOnly;
        public IReadOnlyList<ElementId> CategoryIds { get; set; } = new List<ElementId>();
    }
}
