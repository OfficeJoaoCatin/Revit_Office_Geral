using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace RevitFamilyImporter.DwgClash.Models
{
    public sealed class RevitElementClashInfo
    {
        public ElementId ElementId { get; set; }
        public string Category { get; set; } = string.Empty;
        public string Family { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public List<Point2D> Footprint { get; set; } = new List<Point2D>();
        public Point2D Center { get; set; }
        public double MinX { get; set; }
        public double MaxX { get; set; }
        public double MinY { get; set; }
        public double MaxY { get; set; }
    }
}
