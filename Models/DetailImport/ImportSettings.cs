using RevitFamilyImporter.Import.DetailImport;

namespace RevitFamilyImporter.Models.DetailImport
{
    public class ImportSettings
    {
        public double Margin { get; set; } = 0.05;

        public double Spacing { get; set; } = 0.03;

        public double Scale { get; set; } = 1.0;

        public string BoundaryLayer { get; set; } = BoundaryDetector.DefaultBoundaryLayer;

        public string NameLayer { get; set; } = DetailNamingService.OfficialNameLayer;

        public int Columns { get; set; } = 3;

        public bool AutomaticInsertion { get; set; } = true;

        public bool AdjustSpacing { get; set; } = true;

        public bool UniformScale { get; set; } = true;

        // O motor interno é o padrão por segurança. O GstarCAD só é usado por opção explícita.
        public bool UseGstarCadHighFidelity { get; set; }

        public int MaxEntitiesPerDetail { get; set; } = 10000;

        public ImportLayoutMode LayoutMode { get; set; } = ImportLayoutMode.Grid;

        public void Normalize()
        {
            if (Margin < 0) Margin = 0;
            if (Spacing < 0) Spacing = 0;
            if (Scale <= 0) Scale = 1.0;
            if (Columns < 1) Columns = 1;
            if (MaxEntitiesPerDetail < 100) MaxEntitiesPerDetail = 100;
            BoundaryLayer = string.IsNullOrWhiteSpace(BoundaryLayer) ? BoundaryDetector.DefaultBoundaryLayer : BoundaryLayer.Trim();
            NameLayer = string.IsNullOrWhiteSpace(NameLayer) ? DetailNamingService.OfficialNameLayer : NameLayer.Trim();
        }
    }
}

