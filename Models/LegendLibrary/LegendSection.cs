using System.Collections.Generic;

namespace RevitFamilyImporter.Models.LegendLibrary
{
    public class LegendSection
    {
        public string Title { get; set; } = "GRUPO";

        public double SpacingBefore { get; set; }

        public double SpacingAfter { get; set; } = 8.0;

        public List<LegendItem> Items { get; } = new List<LegendItem>();
    }
}

