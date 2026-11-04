using RevitFamilyImporter.Import.DetailImport;
using RevitFamilyImporter.Import.LegendLibrary;
using System.Collections.Generic;

namespace RevitFamilyImporter.Models.LegendLibrary
{
    public class LegendSettings
    {
        public string LastDwgPath { get; set; }

        public LegendLayoutMode LayoutMode { get; set; } = LegendLayoutMode.Vertical;

        public double HorizontalSpacing { get; set; } = 15.0;

        public double VerticalSpacing { get; set; } = 8.0;

        public int ItemsPerColumn { get; set; } = 10;

        public double TitleHeight { get; set; } = 3.0;

        public double TitleItemSpacing { get; set; } = 4.0;

        public double GroupSpacing { get; set; } = 10.0;

        public double ImportScale { get; set; } = 1.0;

        public string BoundaryLayer { get; set; } = LegendItemDetector.DefaultBoundaryLayer;

        public string NameLayer { get; set; } = DetailNamingService.OfficialNameLayer;

        public List<string> SectionTitles { get; set; } = new List<string>();

        public void Normalize()
        {
            if (HorizontalSpacing <= 0) HorizontalSpacing = 15.0;
            if (VerticalSpacing <= 0) VerticalSpacing = 8.0;
            if (ItemsPerColumn <= 0) ItemsPerColumn = 10;
            if (TitleHeight <= 0) TitleHeight = 3.0;
            if (TitleItemSpacing < 0) TitleItemSpacing = 4.0;
            if (GroupSpacing < 0) GroupSpacing = 10.0;
            if (ImportScale <= 0) ImportScale = 1.0;
            BoundaryLayer = string.IsNullOrWhiteSpace(BoundaryLayer) ? LegendItemDetector.DefaultBoundaryLayer : BoundaryLayer.Trim();
            NameLayer = string.IsNullOrWhiteSpace(NameLayer) ? DetailNamingService.OfficialNameLayer : NameLayer.Trim();
        }
    }
}

