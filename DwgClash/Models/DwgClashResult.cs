using Autodesk.Revit.DB;

namespace RevitFamilyImporter.DwgClash.Models
{
    public sealed class DwgClashResult
    {
        public int Number { get; set; }
        public RevitElementClashInfo Element { get; set; }
        public DwgRegion Region { get; set; }
        public double DistanceFeet { get; set; }
        public string Status => "CLASH";
        public int ElementId => Element?.ElementId?.IntegerValue ?? -1;
        public string Category => Element?.Category ?? string.Empty;
        public string Family => Element?.Family ?? string.Empty;
        public string Type => Element?.Type ?? string.Empty;
        public string Layer => Region?.Layer ?? string.Empty;
        public string RegionName => Region?.Name ?? string.Empty;
        public string DistanceText => DistanceFeet > 0 ? $"{DistanceFeet * 304.8:0.0} mm" : "Interseção";
    }
}
