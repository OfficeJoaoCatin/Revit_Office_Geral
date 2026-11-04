using RevitFamilyImporter.Models.DetailImport;

namespace RevitFamilyImporter.Models.LegendLibrary
{
    public class LegendPlacedItem
    {
        public LegendItem Item { get; set; }

        public LegendSection Section { get; set; }

        public DwgPoint2 TargetOrigin { get; set; }

        public double TextX { get; set; }

        public double TextY { get; set; }
    }
}

