using RevitFamilyImporter.Models.DetailImport;
using System.Collections.Generic;

namespace RevitFamilyImporter.Models.LegendLibrary
{
    public class LegendComposition
    {
        public List<LegendSection> Sections { get; } = new List<LegendSection>();

        public List<LegendPlacedItem> PlacedItems { get; } = new List<LegendPlacedItem>();

        public Dictionary<string, DwgPoint2> SectionTitlePositions { get; } =
            new Dictionary<string, DwgPoint2>();

        public DwgBounds Bounds { get; set; } = new DwgBounds(0, 0, 0, 0);

        public double Width => Bounds.Width;

        public double Height => Bounds.Height;
    }
}

